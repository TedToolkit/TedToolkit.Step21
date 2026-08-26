// -----------------------------------------------------------------------
// <copyright file="ExpressEntityProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Analysis;
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
        ExpressSchemaAnalysis analysis,
        ExpressBoundEntity entity,
        IEnumerable<ExpressEntityAttributeProjection> ownAttributes,
        IEnumerable<ExpressEntityAttributeProjection> flattenedAttributes,
        IEnumerable<ExpressEntityAttributeProjection> effectiveAttributes,
        IEnumerable<ExpressBoundEntity> physicalComponents,
        IEnumerable<ExpressEntityAttributeProjection> derivedRedeclaredAttributes)
    {
        Schema = schema;
        Analysis = analysis;
        Entity = entity;
        Name = ToPascalCase(entity.Name);
        OwnAttributes = new ReadOnlyCollection<ExpressEntityAttributeProjection>(ownAttributes.ToArray());
        FlattenedAttributes = new ReadOnlyCollection<ExpressEntityAttributeProjection>(flattenedAttributes.ToArray());
        EffectiveAttributes = new ReadOnlyCollection<ExpressEntityAttributeProjection>(effectiveAttributes.ToArray());
        PhysicalComponents = new ReadOnlyCollection<ExpressBoundEntity>(physicalComponents.ToArray());
        DerivedRedeclaredAttributes = new ReadOnlyCollection<ExpressEntityAttributeProjection>(
            derivedRedeclaredAttributes.ToArray());
    }

    /// <summary>
    /// Gets the declaring schema.
    /// </summary>
    internal ExpressBoundSchema Schema { get; }

    /// <summary>
    /// Gets the syntax-detached semantic analysis for the declaring schema.
    /// </summary>
    internal ExpressSchemaAnalysis Analysis { get; }

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
    /// Gets the complete inheritance closure in the ascending entity-name order required by external mapping.
    /// </summary>
    internal IReadOnlyList<ExpressBoundEntity> PhysicalComponents { get; }

    /// <summary>
    /// Gets the physical explicit slots replaced by derived redeclarations in this entity closure.
    /// </summary>
    internal IReadOnlyList<ExpressEntityAttributeProjection> DerivedRedeclaredAttributes { get; }

    /// <summary>
    /// Gets a value indicating whether the physical explicit slot is represented by a derived marker.
    /// </summary>
    /// <param name="attribute">The effective physical slot.</param>
    /// <returns><see langword="true"/> when external mapping must use <c>*</c>.</returns>
    internal bool IsDerivedRedeclared(ExpressEntityAttributeProjection attribute)
    {
        return DerivedRedeclaredAttributes.Contains(attribute);
    }

    /// <summary>
    /// Creates projections for every entity in a valid closed schema compilation.
    /// </summary>
    /// <param name="compilation">The valid closed schema compilation.</param>
    /// <param name="valueResolver">The closed-set generated type resolver.</param>
    /// <returns>The projections in schema and declaration order.</returns>
    internal static IReadOnlyList<ExpressEntityProjection> Create(
        ExpressAnalyzedCompilation compilation,
        ExpressGeneratedTypeResolver valueResolver)
    {
        var entities = compilation.Schemas
            .SelectMany(schema => schema.Declarations
                .OfType<ExpressBoundEntity>()
                .Select(entity => (Schema: schema, Analysis: compilation.GetAnalysis(schema), Entity: entity)))
            .ToArray();
        var entityBySymbol = entities.ToDictionary(item => item.Entity.Symbol, item => item);

        return entities
            .Select(item => CreateProjection(
                item.Schema,
                item.Analysis,
                item.Entity,
                entityBySymbol,
                valueResolver))
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
        ExpressSchemaAnalysis analysis,
        ExpressBoundEntity entity,
        IReadOnlyDictionary<
            ExpressBoundSymbol,
            (ExpressBoundSchema Schema, ExpressSchemaAnalysis Analysis, ExpressBoundEntity Entity)> entityBySymbol,
        ExpressGeneratedTypeResolver valueResolver)
    {
        var ownAttributes = ProjectOwnAttributes(entity, analysis, valueResolver).ToArray();
        var (flattenedAttributes, effectiveAttributes) = FlattenAttributes(entity, entityBySymbol, valueResolver);
        foreach (var group in flattenedAttributes
                     .GroupBy(attribute => attribute.Name, StringComparer.Ordinal)
                     .Where(group => group
                         .Select(attribute => (attribute.StorageEntity.Symbol, attribute.StorageAttributeName))
                         .Distinct()
                         .Count() > 1))
        {
            foreach (var attribute in group)
            {
                attribute.DisambiguateStorageMember();
            }
        }

        var components = CreatePhysicalComponents(entity, entityBySymbol);
        var derivedRedeclaredAttributes = FindDerivedRedeclaredAttributes(
            entity,
            analysis,
            entityBySymbol,
            effectiveAttributes);
        return new(
            schema,
            analysis,
            entity,
            ownAttributes,
            flattenedAttributes,
            effectiveAttributes,
            components,
            derivedRedeclaredAttributes);
    }

    private static ExpressEntityAttributeProjection[] FindDerivedRedeclaredAttributes(
        ExpressBoundEntity entity,
        ExpressSchemaAnalysis analysis,
        IReadOnlyDictionary<
            ExpressBoundSymbol,
            (ExpressBoundSchema Schema, ExpressSchemaAnalysis Analysis, ExpressBoundEntity Entity)> entityBySymbol,
        IReadOnlyList<ExpressEntityAttributeProjection> effectiveAttributes)
    {
        var storageNames = new List<(string Entity, string Attribute)>();
        AddDerivedStorageNames(
            entity,
            analysis,
            entityBySymbol,
            new HashSet<ExpressBoundSymbol>(),
            storageNames);
        return effectiveAttributes.Where(attribute => storageNames.Any(storage =>
                StringComparer.OrdinalIgnoreCase.Equals(storage.Entity, attribute.StorageEntity.Name)
                && StringComparer.OrdinalIgnoreCase.Equals(storage.Attribute, attribute.StorageAttributeName)))
            .ToArray();
    }

    private static void AddDerivedStorageNames(
        ExpressBoundEntity entity,
        ExpressSchemaAnalysis analysis,
        IReadOnlyDictionary<
            ExpressBoundSymbol,
            (ExpressBoundSchema Schema, ExpressSchemaAnalysis Analysis, ExpressBoundEntity Entity)> entityBySymbol,
        ISet<ExpressBoundSymbol> visited,
        ICollection<(string Entity, string Attribute)> storageNames)
    {
        if (!visited.Add(entity.Symbol))
        {
            return;
        }

        foreach (var supertype in entity.DirectSupertypes)
        {
            var inherited = entityBySymbol[supertype];
            AddDerivedStorageNames(
                inherited.Entity,
                inherited.Analysis,
                entityBySymbol,
                visited,
                storageNames);
        }

        foreach (var derived in analysis.GetDeclaration(entity).DescendantsAndSelf()
                     .Where(rule => rule.Role == "derivedAttr"))
        {
            var redeclared = derived.DescendantsAndSelf()
                .SingleOrDefault(rule => rule.Role == "redeclaredAttribute");
            var names = redeclared?.Identifiers;
            if (names is { Count: 2, })
            {
                storageNames.Add((names[0], names[1]));
            }
        }
    }

    private static ExpressBoundEntity[] CreatePhysicalComponents(
        ExpressBoundEntity entity,
        IReadOnlyDictionary<
            ExpressBoundSymbol,
            (ExpressBoundSchema Schema, ExpressSchemaAnalysis Analysis, ExpressBoundEntity Entity)> entityBySymbol)
    {
        var closure = new HashSet<ExpressBoundSymbol>();
        AddEntityAndSupertypes(entity, entityBySymbol, closure);
        return closure
            .Select(symbol => entityBySymbol[symbol].Entity)
            .OrderBy(candidate => candidate.Name.ToUpperInvariant(), StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddEntityAndSupertypes(
        ExpressBoundEntity entity,
        IReadOnlyDictionary<
            ExpressBoundSymbol,
            (ExpressBoundSchema Schema, ExpressSchemaAnalysis Analysis, ExpressBoundEntity Entity)> entityBySymbol,
        ISet<ExpressBoundSymbol> closure)
    {
        if (!closure.Add(entity.Symbol))
        {
            return;
        }

        foreach (var supertype in entity.DirectSupertypes)
        {
            AddEntityAndSupertypes(entityBySymbol[supertype].Entity, entityBySymbol, closure);
        }
    }

    private static IEnumerable<ExpressEntityAttributeProjection> ProjectOwnAttributes(
        ExpressBoundEntity entity,
        ExpressSchemaAnalysis analysis,
        ExpressGeneratedTypeResolver valueResolver)
    {
        return entity.Attributes
            .Where(attribute => attribute.Kind == ExpressAttributeKind.Explicit)
            .Select(attribute => CreateAttribute(entity, analysis, attribute, valueResolver))
            .Where(attribute => attribute is not null)
            .Select(attribute => attribute!);
    }

    private static (List<ExpressEntityAttributeProjection> Flattened, List<ExpressEntityAttributeProjection> Effective)
        FlattenAttributes(
        ExpressBoundEntity entity,
        IReadOnlyDictionary<
            ExpressBoundSymbol,
            (ExpressBoundSchema Schema, ExpressSchemaAnalysis Analysis, ExpressBoundEntity Entity)> entityBySymbol,
        ExpressGeneratedTypeResolver valueResolver)
    {
        var result = new List<ExpressEntityAttributeProjection>();
        var effective = new List<ExpressEntityAttributeProjection>();

        AddAttributes(entity, entityBySymbol, valueResolver, result, effective);
        return (result, effective);
    }

    private static void AddAttributes(
        ExpressBoundEntity entity,
        IReadOnlyDictionary<
            ExpressBoundSymbol,
            (ExpressBoundSchema Schema, ExpressSchemaAnalysis Analysis, ExpressBoundEntity Entity)> entityBySymbol,
        ExpressGeneratedTypeResolver valueResolver,
        List<ExpressEntityAttributeProjection> result,
        List<ExpressEntityAttributeProjection> effective)
    {
        foreach (var supertype in entity.DirectSupertypes)
        {
            AddAttributes(entityBySymbol[supertype].Entity, entityBySymbol, valueResolver, result, effective);
        }

        foreach (var attribute in ProjectOwnAttributes(
                     entity,
                     entityBySymbol[entity.Symbol].Analysis,
                     valueResolver))
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
        ExpressSchemaAnalysis analysis,
        ExpressBoundAttribute attribute,
        ExpressGeneratedTypeResolver valueResolver)
    {
        if (!valueResolver.IsSupported(attribute.Type))
        {
            return null;
        }

        var redeclaredAttribute = analysis.GetDeclaration(declaringEntity).DescendantsAndSelf()
            .Where(rule => rule.Role == "attributeDecl")
            .SingleOrDefault(rule => SameLocation(rule.Span.Start, attribute.Span.Start))
            ?.DescendantsAndSelf()
            .SingleOrDefault(rule => rule.Role == "redeclaredAttribute");
        var redeclaredNames = redeclaredAttribute?.Identifiers;

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