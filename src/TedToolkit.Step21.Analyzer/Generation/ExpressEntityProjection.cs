// -----------------------------------------------------------------------
// <copyright file="ExpressEntityProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Projects bound EXPRESS entities into deterministic C# entity shapes.
/// </summary>
internal sealed class ExpressEntityProjection
{
    private static readonly char[] _identifierSeparators = ['_',];

    private ExpressEntityProjection(
        ExpressBoundSchema schema,
        ExpressBoundEntity entity,
        IEnumerable<ExpressEntityAttributeProjection> ownAttributes,
        IEnumerable<ExpressEntityAttributeProjection> flattenedAttributes,
        IEnumerable<ExpressEntityAttributeProjection> effectiveAttributes)
    {
        Schema = schema;
        Entity = entity;
        Name = ToPascalCase(entity.Name);
        OwnAttributes = new ReadOnlyCollection<ExpressEntityAttributeProjection>(ownAttributes.ToArray());
        FlattenedAttributes = new ReadOnlyCollection<ExpressEntityAttributeProjection>(flattenedAttributes.ToArray());
        EffectiveAttributes = new ReadOnlyCollection<ExpressEntityAttributeProjection>(effectiveAttributes.ToArray());
    }

    /// <summary>
    /// Gets the declaring schema.
    /// </summary>
    internal ExpressBoundSchema Schema { get; }

    /// <summary>
    /// Gets the bound entity.
    /// </summary>
    internal ExpressBoundEntity Entity { get; }

    /// <summary>
    /// Gets the generated C# entity name.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Gets locally declared projected attributes.
    /// </summary>
    internal IReadOnlyList<ExpressEntityAttributeProjection> OwnAttributes { get; }

    /// <summary>
    /// Gets inherited and local projected attributes with diamond occurrences removed.
    /// </summary>
    internal IReadOnlyList<ExpressEntityAttributeProjection> FlattenedAttributes { get; }

    /// <summary>
    /// Gets the latest projected attribute for each physical storage slot.
    /// </summary>
    internal IReadOnlyList<ExpressEntityAttributeProjection> EffectiveAttributes { get; }

    /// <summary>
    /// Creates projections for every entity in a valid closed schema compilation.
    /// </summary>
    /// <param name="compilation">The valid closed schema compilation.</param>
    /// <param name="valueResolver">The closed-set generated type resolver.</param>
    /// <returns>The projections in schema and declaration order.</returns>
    internal static IReadOnlyList<ExpressEntityProjection> Create(
        ExpressSchemaCompilation compilation,
        ExpressGeneratedTypeResolver valueResolver)
    {
        var entities = compilation.Schemas
            .SelectMany(schema => schema.Declarations
                .OfType<ExpressBoundEntity>()
                .Select(entity => (Schema: schema, Entity: entity)))
            .ToArray();
        var entityBySymbol = entities.ToDictionary(item => item.Entity.Symbol, item => item);

        return entities
            .Select(item => CreateProjection(item.Schema, item.Entity, entityBySymbol, valueResolver))
            .ToArray();
    }

    /// <summary>
    /// Converts one EXPRESS identifier to its generated PascalCase spelling.
    /// </summary>
    /// <param name="identifier">The EXPRESS identifier.</param>
    /// <returns>The generated C# spelling.</returns>
    internal static string ToPascalCase(string identifier)
    {
        return string.Concat(identifier
            .Split(_identifierSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => char.ToUpperInvariant(segment[0]) + LowerAscii(segment.Substring(1))));
    }

    private static ExpressEntityProjection CreateProjection(
        ExpressBoundSchema schema,
        ExpressBoundEntity entity,
        IReadOnlyDictionary<ExpressBoundSymbol, (ExpressBoundSchema Schema, ExpressBoundEntity Entity)> entityBySymbol,
        ExpressGeneratedTypeResolver valueResolver)
    {
        var ownAttributes = ProjectOwnAttributes(entity, valueResolver).ToArray();
        var (flattenedAttributes, effectiveAttributes) = FlattenAttributes(entity, entityBySymbol, valueResolver);
        return new(schema, entity, ownAttributes, flattenedAttributes, effectiveAttributes);
    }

    private static IEnumerable<ExpressEntityAttributeProjection> ProjectOwnAttributes(
        ExpressBoundEntity entity,
        ExpressGeneratedTypeResolver valueResolver)
    {
        return entity.Attributes
            .Where(attribute => attribute.Kind == ExpressAttributeKind.Explicit)
            .Select(attribute => CreateAttribute(entity, attribute, valueResolver))
            .Where(attribute => attribute is not null)
            .Select(attribute => attribute!);
    }

    private static (List<ExpressEntityAttributeProjection> Flattened, List<ExpressEntityAttributeProjection> Effective)
        FlattenAttributes(
        ExpressBoundEntity entity,
        IReadOnlyDictionary<ExpressBoundSymbol, (ExpressBoundSchema Schema, ExpressBoundEntity Entity)> entityBySymbol,
        ExpressGeneratedTypeResolver valueResolver)
    {
        var result = new List<ExpressEntityAttributeProjection>();
        var effective = new List<ExpressEntityAttributeProjection>();

        AddAttributes(entity, entityBySymbol, valueResolver, result, effective);
        return (result, effective);
    }

    private static void AddAttributes(
        ExpressBoundEntity entity,
        IReadOnlyDictionary<ExpressBoundSymbol, (ExpressBoundSchema Schema, ExpressBoundEntity Entity)> entityBySymbol,
        ExpressGeneratedTypeResolver valueResolver,
        List<ExpressEntityAttributeProjection> result,
        List<ExpressEntityAttributeProjection> effective)
    {
        foreach (var supertype in entity.DirectSupertypes)
        {
            AddAttributes(entityBySymbol[supertype].Entity, entityBySymbol, valueResolver, result, effective);
        }

        foreach (var attribute in ProjectOwnAttributes(entity, valueResolver))
        {
            var effectiveIndex = FindRedeclaredStorage(effective, attribute);
            if (effectiveIndex >= 0)
            {
                var inherited = effective[effectiveIndex];
                var isRenamed = !StringComparer.Ordinal.Equals(inherited.Name, attribute.Name);
                attribute.BindStorage(
                    inherited,
                    isRenamed ? inherited.Name : null);
                effective[effectiveIndex] = attribute;
                if (isRenamed)
                {
                    AddPublicProperty(result, attribute);
                }
                else
                {
                    var publicIndex = result.FindIndex(candidate => SameStorage(candidate, inherited));
                    result[publicIndex] = attribute;
                }

                continue;
            }

            AddPublicProperty(result, attribute);
            if (!effective.Any(candidate => SameStorage(candidate, attribute)))
            {
                effective.Add(attribute);
            }
        }
    }

    private static ExpressEntityAttributeProjection? CreateAttribute(
        ExpressBoundEntity declaringEntity,
        ExpressBoundAttribute attribute,
        ExpressGeneratedTypeResolver valueResolver)
    {
        if (!valueResolver.IsSupported(attribute.Type))
        {
            return null;
        }

        var redeclaredAttribute = declaringEntity.Syntax.DescendantsAndSelf()
            .Where(rule => rule.Production == "attributeDecl")
            .SingleOrDefault(rule => SameLocation(rule.Span.Start, attribute.Span.Start))
            ?.DescendantsAndSelf()
            .SingleOrDefault(rule => rule.Production == "redeclaredAttribute");
        var redeclaredNames = redeclaredAttribute?.DescendantTokens()
            .Where(token => token.TokenName == "SimpleId")
            .Select(token => token.Text)
            .ToArray();

        return new(
            declaringEntity,
            attribute,
            ToPascalCase(attribute.Name),
            attribute.Type,
            redeclaredNames?[0],
            redeclaredNames?[1]);
    }

    private static int FindRedeclaredStorage(
        List<ExpressEntityAttributeProjection> effective,
        ExpressEntityAttributeProjection attribute)
    {
        if (attribute.RedeclaredEntityName is null || attribute.RedeclaredAttributeName is null)
        {
            return -1;
        }

        return effective.FindIndex(candidate =>
            StringComparer.OrdinalIgnoreCase.Equals(
                candidate.StorageEntity.Name,
                attribute.RedeclaredEntityName)
            && StringComparer.OrdinalIgnoreCase.Equals(
                candidate.StorageAttributeName,
                attribute.RedeclaredAttributeName));
    }

    private static void AddPublicProperty(
        List<ExpressEntityAttributeProjection> result,
        ExpressEntityAttributeProjection attribute)
    {
        if (result.Any(candidate => SameStorage(candidate, attribute)
                && StringComparer.Ordinal.Equals(candidate.Name, attribute.Name)))
        {
            return;
        }

        result.Add(attribute);
    }

    private static bool SameStorage(
        ExpressEntityAttributeProjection first,
        ExpressEntityAttributeProjection second)
    {
        return ReferenceEquals(first.StorageEntity, second.StorageEntity)
            && StringComparer.OrdinalIgnoreCase.Equals(first.StorageAttributeName, second.StorageAttributeName);
    }

    private static bool SameLocation(ExpressSourceLocation first, ExpressSourceLocation second)
    {
        return StringComparer.Ordinal.Equals(first.FilePath, second.FilePath)
            && first.Line == second.Line
            && first.Column == second.Column;
    }

    private static string LowerAscii(string value)
    {
        return new(value
            .Select(character => character is >= 'A' and <= 'Z'
                ? (char)(character + ('a' - 'A'))
                : character)
            .ToArray());
    }
}