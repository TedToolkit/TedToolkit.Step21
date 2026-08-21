// -----------------------------------------------------------------------
// <copyright file="ExpressGeneratedTypeResolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.RoslynHelper.Syntaxes;

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Resolves supported bound EXPRESS types to deterministic generated C# types.
/// </summary>
internal sealed class ExpressGeneratedTypeResolver
{
    private readonly Dictionary<ExpressBoundSymbol, ExpressBoundDefinedType> _definedTypes;

    private readonly IReadOnlyList<ExpressBoundDefinedType> _orderedDefinedTypes;

    private readonly HashSet<ExpressBoundSymbol> _supportedDefinedTypes = [];

    private ExpressGeneratedTypeResolver(
        IReadOnlyList<ExpressBoundDefinedType> orderedDefinedTypes)
    {
        _orderedDefinedTypes = orderedDefinedTypes;
        _definedTypes = orderedDefinedTypes.ToDictionary(
            declaration => declaration.Symbol,
            declaration => declaration);
        foreach (var declaration in orderedDefinedTypes)
        {
            if (IsSupported(declaration.UnderlyingType, new HashSet<ExpressBoundSymbol>()))
            {
                _supportedDefinedTypes.Add(declaration.Symbol);
            }
        }
    }

    /// <summary>
    /// Creates a resolver for one closed schema compilation.
    /// </summary>
    /// <param name="compilation">The closed bound schema compilation.</param>
    /// <returns>The resolver and its stable supported-type set.</returns>
    internal static ExpressGeneratedTypeResolver Create(ExpressSchemaCompilation compilation)
    {
        var definedTypes = compilation.Schemas
            .SelectMany(schema => schema.Declarations.OfType<ExpressBoundDefinedType>())
            .ToArray();
        return new(definedTypes);
    }

    /// <summary>
    /// Gets supported defined-type declarations in their owning schema declaration order.
    /// </summary>
    /// <param name="compilation">The compilation whose order governs generation.</param>
    /// <returns>The supported declarations.</returns>
    internal IReadOnlyList<ExpressBoundDefinedType> GetSupportedDeclarations(ExpressSchemaCompilation compilation)
    {
        return compilation.Schemas
            .SelectMany(schema => schema.Declarations.OfType<ExpressBoundDefinedType>())
            .Where(declaration => _supportedDefinedTypes.Contains(declaration.Symbol))
            .ToArray();
    }

    /// <summary>
    /// Determines whether a bound attribute type has a supported generated value projection.
    /// </summary>
    /// <param name="type">The bound type.</param>
    /// <returns><see langword="true"/> when a generated or runtime C# type exists.</returns>
    internal bool IsSupported(ExpressBoundType type)
    {
        return type switch
        {
            ExpressBoundScalarType => true,
            ExpressBoundAggregateType aggregate => IsSupported(
                aggregate,
                new HashSet<ExpressBoundSymbol>()),
            ExpressBoundNamedType named when named.Declaration.Kind == ExpressDeclarationKind.Entity => true,
            ExpressBoundNamedType named => _supportedDefinedTypes.Contains(named.Declaration),
            _ => false,
        };
    }

    /// <summary>
    /// Resolves one supported bound type in the context of a generated schema namespace.
    /// </summary>
    /// <param name="currentSchema">The schema owning the generated use site.</param>
    /// <param name="type">The supported bound type.</param>
    /// <returns>The generated C# type and whether it is a reference type.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="type"/> is not supported by this value stage.</exception>
    internal (DataType DataType, bool IsReferenceType) Resolve(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundType type)
    {
        return type switch
        {
            ExpressBoundScalarType scalar => ResolveScalar(scalar.Kind),
            ExpressBoundAggregateType aggregate => ResolveAggregate(currentSchema, aggregate),
            ExpressBoundNamedType named => Resolve(currentSchema, named.Declaration),
            _ => throw new InvalidOperationException(
                $"Bound type '{type.GetType().Name}' has no supported generated value projection."),
        };
    }

    /// <summary>
    /// Resolves one entity or supported defined-type symbol at a generated use site.
    /// </summary>
    /// <param name="currentSchema">The schema owning the generated use site.</param>
    /// <param name="symbol">The resolved named type.</param>
    /// <returns>The generated C# type and whether it is a reference type.</returns>
    internal (DataType DataType, bool IsReferenceType) Resolve(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundSymbol symbol)
    {
        var generatedName = ExpressEntityProjection.ToPascalCase(symbol.Name);
        if (symbol.Kind == ExpressDeclarationKind.Entity)
        {
            generatedName = $"I{generatedName}";
        }

        var qualifiedName = ReferenceEquals(currentSchema, symbol.DeclaringSchema)
            ? generatedName
            : $"global::TedToolkit.Step21.Generated.{ExpressEntityProjection.ToPascalCase(symbol.DeclaringSchema.Name)}.{generatedName}";
        var isReferenceType = symbol.Kind == ExpressDeclarationKind.Entity
            || (_definedTypes.TryGetValue(symbol, out var declaration)
                && declaration.UnderlyingType is ExpressBoundSelectType);
        return (new(qualifiedName), isReferenceType);
    }

    /// <summary>
    /// Gets the supported declaration represented by a defined-type symbol.
    /// </summary>
    /// <param name="symbol">The defined-type symbol.</param>
    /// <returns>The bound declaration.</returns>
    internal ExpressBoundDefinedType GetDefinedType(ExpressBoundSymbol symbol)
    {
        return _definedTypes[symbol];
    }

    /// <summary>
    /// Gets the complete inherited enumeration value set in stable declaration order.
    /// </summary>
    /// <param name="enumeration">The enumeration definition or extension.</param>
    /// <returns>The case-insensitively distinct declared values.</returns>
    internal IReadOnlyList<string> GetEnumerationValues(ExpressBoundEnumerationType enumeration)
    {
        var values = new List<string>();
        if (enumeration.BaseType is not null
            && GetDefinedType(enumeration.BaseType).UnderlyingType is ExpressBoundEnumerationType baseEnumeration)
        {
            values.AddRange(GetEnumerationValues(baseEnumeration));
        }

        values.AddRange(enumeration.Values);
        if (enumeration.IsExtensible)
        {
            var owner = GetOwner(enumeration);
            values.AddRange(_orderedDefinedTypes
                .Select(declaration => declaration.UnderlyingType)
                .OfType<ExpressBoundEnumerationType>()
                .Where(candidate => IsExtensionOf(candidate.BaseType, owner))
                .SelectMany(candidate => candidate.Values));
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Gets the complete inherited SELECT alternative set in stable declaration order.
    /// </summary>
    /// <param name="select">The SELECT definition or extension.</param>
    /// <returns>The distinct declared alternatives.</returns>
    internal IReadOnlyList<ExpressBoundSymbol> GetSelectAlternatives(ExpressBoundSelectType select)
    {
        var alternatives = new List<ExpressBoundSymbol>();
        if (select.BaseType is not null
            && GetDefinedType(select.BaseType).UnderlyingType is ExpressBoundSelectType baseSelect)
        {
            alternatives.AddRange(GetSelectAlternatives(baseSelect));
        }

        alternatives.AddRange(select.Alternatives);
        if (select.IsExtensible)
        {
            var owner = GetOwner(select);
            alternatives.AddRange(_orderedDefinedTypes
                .Select(declaration => declaration.UnderlyingType)
                .OfType<ExpressBoundSelectType>()
                .Where(candidate => IsExtensionOf(candidate.BaseType, owner))
                .SelectMany(candidate => candidate.Alternatives));
        }

        return alternatives.Distinct().ToArray();
    }

    /// <summary>
    /// Determines whether two supported attribute types require the same generated property type.
    /// </summary>
    /// <param name="first">The first bound type.</param>
    /// <param name="second">The second bound type.</param>
    /// <returns><see langword="true"/> when both map to one identical generated type.</returns>
    internal static bool AreEquivalent(ExpressBoundType first, ExpressBoundType second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        return (first, second) switch
        {
            (ExpressBoundScalarType left, ExpressBoundScalarType right) => left.Kind == right.Kind,
            (ExpressBoundAggregateType left, ExpressBoundAggregateType right) =>
                left.Kind == right.Kind && AreEquivalent(left.ElementType, right.ElementType),
            (ExpressBoundNamedType left, ExpressBoundNamedType right) =>
                ReferenceEquals(left.Declaration, right.Declaration),
            _ => false,
        };
    }

    private bool IsSupported(ExpressBoundType type, HashSet<ExpressBoundSymbol> path)
    {
        switch (type)
        {
            case ExpressBoundScalarType:
            case ExpressBoundEnumerationType:
                return true;

            case ExpressBoundAggregateType aggregate:
                return aggregate.Kind is ExpressAggregateKind.Array
                    or ExpressAggregateKind.Bag
                    or ExpressAggregateKind.List
                    or ExpressAggregateKind.Set
                    && IsSupported(aggregate.ElementType, path);

            case ExpressBoundSelectType select:
                return GetSelectAlternatives(select).All(alternative =>
                    alternative.Kind == ExpressDeclarationKind.Entity
                    || IsSupportedDefinedType(alternative, path));

            case ExpressBoundNamedType named when named.Declaration.Kind == ExpressDeclarationKind.Entity:
                return true;

            case ExpressBoundNamedType named:
                return IsSupportedDefinedType(named.Declaration, path);

            default:
                return false;
        }
    }

    private bool IsSupportedDefinedType(ExpressBoundSymbol symbol, HashSet<ExpressBoundSymbol> path)
    {
        if (_supportedDefinedTypes.Contains(symbol))
        {
            return true;
        }

        if (!_definedTypes.TryGetValue(symbol, out var declaration) || !path.Add(symbol))
        {
            return false;
        }

        var supported = IsSupported(declaration.UnderlyingType, path);
        _ = path.Remove(symbol);
        return supported;
    }

    private ExpressBoundSymbol GetOwner(ExpressBoundType type)
    {
        return _orderedDefinedTypes.Single(declaration => ReferenceEquals(declaration.UnderlyingType, type)).Symbol;
    }

    private bool IsExtensionOf(ExpressBoundSymbol? baseType, ExpressBoundSymbol target)
    {
        while (baseType is not null)
        {
            if (ReferenceEquals(baseType, target))
            {
                return true;
            }

            var underlyingType = GetDefinedType(baseType).UnderlyingType;
            baseType = underlyingType switch
            {
                ExpressBoundEnumerationType enumeration => enumeration.BaseType,
                ExpressBoundSelectType select => select.BaseType,
                _ => null,
            };
        }

        return false;
    }

    private static (DataType DataType, bool IsReferenceType) ResolveScalar(ExpressScalarKind kind)
    {
        return kind switch
        {
            ExpressScalarKind.Binary => (new("global::TedToolkit.Step21.BinaryValue"), true),
            ExpressScalarKind.Boolean => (DataType.Bool, false),
            ExpressScalarKind.Integer => (new("global::System.Numerics.BigInteger"), false),
            ExpressScalarKind.Logical => (new("global::TedToolkit.Step21.LogicalValue"), false),
            ExpressScalarKind.Number => (new("global::TedToolkit.Step21.NumberValue"), false),
            ExpressScalarKind.Real => (new("global::TedToolkit.Step21.RealValue"), false),
            ExpressScalarKind.String => (DataType.String, true),
            _ => throw new InvalidOperationException($"Unsupported EXPRESS scalar kind '{kind.ToString()}'."),
        };
    }

    private (DataType DataType, bool IsReferenceType) ResolveAggregate(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundAggregateType aggregate)
    {
        var runtimeTypeName = aggregate.Kind switch
        {
            ExpressAggregateKind.Array => "ExpressArray",
            ExpressAggregateKind.Bag => "ExpressBag",
            ExpressAggregateKind.List => "ExpressList",
            ExpressAggregateKind.Set => "ExpressSet",
            _ => throw new InvalidOperationException(
                $"Unsupported generated aggregate kind '{aggregate.Kind.ToString()}'."),
        };
        var elementType = Resolve(currentSchema, aggregate.ElementType).DataType;
        return (new DataType($"global::TedToolkit.Step21.{runtimeTypeName}").Generic(elementType), true);
    }
}