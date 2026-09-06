// -----------------------------------------------------------------------
// <copyright file="ExpressGeneratedTypeResolver.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.Numerics;

using TedToolkit.RoslynHelper.Syntaxes;

using TedToolkit.Step21.Analyzer.Express.Analysis;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Resolves supported bound EXPRESS types to deterministic generated C# types.
/// </summary>
internal sealed class ExpressGeneratedTypeResolver
{
    private readonly Dictionary<ExpressBoundSymbol, ExpressBoundDefinedType> _definedTypes;

    private readonly IReadOnlyList<ExpressBoundDefinedType> _orderedDefinedTypes;

    private readonly IReadOnlyDictionary<ExpressBoundSymbol, ExpressBoundEntity> _entities;

    private readonly HashSet<ExpressBoundSymbol> _supportedDefinedTypes = [];

    private readonly Dictionary<ExpressBoundSelectType, IReadOnlyList<ExpressBoundSymbol>> _selectAlternatives = [];

    private ExpressGeneratedTypeResolver(
        IReadOnlyList<ExpressBoundDefinedType> orderedDefinedTypes,
        IReadOnlyList<ExpressBoundEntity> entities)
    {
        _orderedDefinedTypes = orderedDefinedTypes;
        _definedTypes = orderedDefinedTypes.ToDictionary(
            declaration => declaration.Symbol,
            declaration => declaration);
        _entities = entities.ToDictionary(entity => entity.Symbol, entity => entity);
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
    internal static ExpressGeneratedTypeResolver Create(ExpressAnalyzedCompilation compilation)
    {
        var definedTypes = compilation.Schemas
            .SelectMany(schema => schema.Declarations.OfType<ExpressBoundDefinedType>())
            .ToArray();
        var entities = compilation.Schemas
            .SelectMany(schema => schema.Declarations.OfType<ExpressBoundEntity>())
            .ToArray();
        return new(definedTypes, entities);
    }

    /// <summary>
    /// Resolves the aggregate domain beneath any nominal aliases without changing their generated identity.
    /// </summary>
    /// <param name="type">The declared value domain.</param>
    /// <returns>The underlying aggregate, or null for a non-aggregate domain.</returns>
    internal ExpressBoundAggregateType? GetAggregateType(ExpressBoundType type)
    {
        while (type is ExpressBoundNamedType named && named.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            type = GetDefinedType(named.Declaration).UnderlyingType;
        }

        return type as ExpressBoundAggregateType;
    }

    /// <summary>
    /// Gets supported defined-type declarations in their owning schema declaration order.
    /// </summary>
    /// <param name="compilation">The compilation whose order governs generation.</param>
    /// <returns>The supported declarations.</returns>
    internal IReadOnlyList<ExpressBoundDefinedType> GetSupportedDeclarations(ExpressAnalyzedCompilation compilation)
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
        var qualifiedName = GeneratedTypeName(currentSchema, symbol);
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
        if (_selectAlternatives.TryGetValue(select, out var cached))
        {
            return cached;
        }

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

        var result = alternatives.Distinct().ToArray();
        _selectAlternatives.Add(select, result);
        return result;
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

    /// <summary>
    /// Classifies a redeclared domain against the inherited physical-slot domain.
    /// </summary>
    /// <param name="original">The inherited domain.</param>
    /// <param name="narrowed">The redeclared domain.</param>
    /// <returns>The implemented redeclaration classification.</returns>
    internal ExpressRedeclarationClassification ClassifySpecialization(
        ExpressBoundType original,
        ExpressBoundType narrowed)
    {
        if (AreRedeclarationEquivalent(original, narrowed))
        {
            return ExpressRedeclarationClassification.Equivalent;
        }

        if (TryGetTransparentAlias(original, out var inheritedAlias))
        {
            return ClassifySpecialization(inheritedAlias, narrowed);
        }

        if (narrowed is ExpressBoundNamedType namedAlternative)
        {
            var nominalPaths = FindNominalSelectProjection(original, namedAlternative.Declaration, out _);
            if (nominalPaths > 0)
            {
                return nominalPaths == 1
                    ? ExpressRedeclarationClassification.Supported
                    : ExpressRedeclarationClassification.Unsupported;
            }

            var aggregatePaths = FindAggregateSelectProjection(
                original,
                namedAlternative.Declaration,
                out _);
            if (aggregatePaths > 0)
            {
                return aggregatePaths == 1
                    ? ExpressRedeclarationClassification.Supported
                    : ExpressRedeclarationClassification.Unsupported;
            }
        }

        if (TryGetSpecializedAlias(original, narrowed, out _))
        {
            return ExpressRedeclarationClassification.Supported;
        }

        if (TryGetClosedSelect(original, out var inheritedSelect)
            && TryGetClosedSelect(narrowed, out var narrowedSelect))
        {
            var classification = ExpressRedeclarationClassification.Supported;
            foreach (var alternative in GetSelectAlternatives(narrowedSelect))
            {
                var branchClassification = ClassifySpecialization(
                    original,
                    new ExpressBoundNamedType(alternative, narrowed.Span));
                if (branchClassification == ExpressRedeclarationClassification.Invalid)
                {
                    return branchClassification;
                }

                if (branchClassification == ExpressRedeclarationClassification.Unsupported)
                {
                    classification = branchClassification;
                }
            }

            return classification;
        }

        if (TryGetClosedSelect(original, out inheritedSelect)
            && TryGetClosedEntityLeaves(narrowed, out var selectedLeaves))
        {
            if (!selectedLeaves.All(candidate => GetSelectAlternatives(inheritedSelect)
                    .Any(alternative => CanProjectEntityTo(candidate, alternative))))
            {
                return ExpressRedeclarationClassification.Invalid;
            }

            return selectedLeaves.Any(candidate => !TrySelectProjectionAlternative(
                    candidate,
                    inheritedSelect,
                    out _))
                    ? ExpressRedeclarationClassification.Unsupported
                    : ExpressRedeclarationClassification.Supported;
        }

        if (TryGetClosedEntityLeaves(original, out var originalLeaves)
            && TryGetClosedEntityLeaves(narrowed, out var narrowedLeaves))
        {
            return narrowedLeaves.All(candidate => originalLeaves.Any(parent => IsEntitySubtype(candidate, parent)))
                ? ExpressRedeclarationClassification.Supported
                : ExpressRedeclarationClassification.Invalid;
        }

        if (original is ExpressBoundScalarType originalScalar
            && narrowed is ExpressBoundScalarType narrowedScalar)
        {
            return originalScalar.Kind == ExpressScalarKind.Number
                && narrowedScalar.Kind is ExpressScalarKind.Integer or ExpressScalarKind.Real
                    ? ExpressRedeclarationClassification.Supported
                    : ExpressRedeclarationClassification.Invalid;
        }

        if (original is ExpressBoundAggregateType originalAggregate
            && narrowed is ExpressBoundAggregateType narrowedAggregate)
        {
            if (originalAggregate.Kind != narrowedAggregate.Kind
                || (!AggregateMetadataEquivalent(originalAggregate, narrowedAggregate)
                    && !IsCardinalityNarrowing(originalAggregate, narrowedAggregate))
                || originalAggregate.ElementType is ExpressBoundAggregateType
                || narrowedAggregate.ElementType is ExpressBoundAggregateType)
            {
                return ExpressRedeclarationClassification.Unsupported;
            }

            var elementClassification = ClassifySpecialization(
                originalAggregate.ElementType,
                narrowedAggregate.ElementType);
            if (elementClassification == ExpressRedeclarationClassification.Invalid
                && (!TryGetClosedEntityLeaves(originalAggregate.ElementType, out _)
                    || !TryGetClosedEntityLeaves(narrowedAggregate.ElementType, out _)))
            {
                return ExpressRedeclarationClassification.Unsupported;
            }

            return elementClassification == ExpressRedeclarationClassification.Equivalent
                ? ExpressRedeclarationClassification.Supported
                : elementClassification;
        }

        var originalIsEntityDomain = TryGetClosedEntityLeaves(original, out _);
        var narrowedIsEntityDomain = TryGetClosedEntityLeaves(narrowed, out _);
        return originalIsEntityDomain || narrowedIsEntityDomain
            ? ExpressRedeclarationClassification.Invalid
            : ExpressRedeclarationClassification.Unsupported;
    }

    /// <summary>
    /// Determines whether a generated aggregate declaration must expose its interface through a read-only view.
    /// </summary>
    /// <param name="attribute">The bound aggregate attribute.</param>
    /// <returns><see langword="true"/> when the attribute participates in an implemented aggregate specialization.</returns>
    internal bool RequiresAggregateView(ExpressBoundAttribute attribute)
    {
        if (attribute.Type is not ExpressBoundAggregateType)
        {
            return false;
        }

        if (TryGetRedeclaredAttribute(attribute, out var original)
            && ClassifySpecialization(original.Type, attribute.Type) == ExpressRedeclarationClassification.Supported)
        {
            return true;
        }

        return _entities.Values.SelectMany(entity => entity.Attributes).Any(candidate =>
            candidate.Type is ExpressBoundAggregateType
            && TryGetRedeclaredAttribute(candidate, out var candidateOriginal)
            && ReferenceEquals(candidateOriginal, attribute)
            && ClassifySpecialization(attribute.Type, candidate.Type) == ExpressRedeclarationClassification.Supported);
    }

    /// <summary>
    /// Resolves the covariant read-only runtime view for a supported aggregate type.
    /// </summary>
    /// <param name="currentSchema">The generated schema namespace.</param>
    /// <param name="aggregate">The supported aggregate declaration.</param>
    /// <returns>The public read-only view type.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="aggregate"/> has no supported runtime kind.</exception>
    internal DataType ResolveAggregateView(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundAggregateType aggregate)
    {
        var runtimeTypeName = aggregate.Kind switch
        {
            ExpressAggregateKind.Array => "IExpressArray",
            ExpressAggregateKind.Bag => "IExpressBag",
            ExpressAggregateKind.List => "IExpressList",
            ExpressAggregateKind.Set => "IExpressSet",
            _ => throw new InvalidOperationException(
                $"Unsupported generated aggregate kind '{aggregate.Kind.ToString()}'."),
        };
        var elementType = Resolve(currentSchema, aggregate.ElementType).DataType;
        return new DataType($"global::TedToolkit.Step21.{runtimeTypeName}").Generic(elementType);
    }

    /// <summary>
    /// Creates a lossless generated getter expression from a most-specific domain to an inherited domain.
    /// </summary>
    /// <param name="currentSchema">The generated schema namespace.</param>
    /// <param name="source">The most-specific stored domain.</param>
    /// <param name="target">The inherited interface domain.</param>
    /// <param name="expression">The generated storage expression.</param>
    /// <returns>The lossless generated projection expression.</returns>
    /// <exception cref="InvalidOperationException">The requested pair is outside the implemented specialization matrix.</exception>
    internal string CreateSpecializationProjection(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundType source,
        ExpressBoundType target,
        string expression)
    {
        var classification = ClassifySpecialization(target, source);
        if (classification == ExpressRedeclarationClassification.Equivalent
            || (TryGetDirectEntity(source, out _) && TryGetDirectEntity(target, out _)))
        {
            return expression;
        }

        if (target is ExpressBoundNamedType targetAlias
            && TryGetTransparentAlias(target, out var underlyingTarget))
        {
            var projected = CreateSpecializationProjection(
                currentSchema,
                source,
                underlyingTarget,
                expression);
            return $"new {GeneratedTypeName(currentSchema, targetAlias.Declaration)}({projected})";
        }

        if (source is ExpressBoundNamedType sourceAlternative
            && target is ExpressBoundNamedType targetSelect
            && FindNominalSelectProjection(target, sourceAlternative.Declaration, out var nominalPath) == 1)
        {
            for (var index = nominalPath.Count - 1; index >= 0; index--)
            {
                var owner = index == 0 ? targetSelect.Declaration : nominalPath[index - 1];
                expression = $"{GeneratedTypeName(currentSchema, owner)}."
                    + $"From{ExpressEntityProjection.ToPascalCase(nominalPath[index].Name)}({expression})";
            }

            return expression;
        }

        if (source is ExpressBoundNamedType sourceAggregateAlternative
            && target is ExpressBoundNamedType targetAggregateSelect
            && FindAggregateSelectProjection(
                target,
                sourceAggregateAlternative.Declaration,
                out var aggregatePath) == 1)
        {
            var storedAggregate = GetDefinedType(sourceAggregateAlternative.Declaration).UnderlyingType;
            var terminalAlternative = aggregatePath[aggregatePath.Count - 1];
            var terminal = GetDefinedType(terminalAlternative).UnderlyingType;
            var projected = CreateSpecializationProjection(
                currentSchema,
                storedAggregate,
                terminal,
                $"({expression}).ReadOnlyValue");
            expression = $"new {GeneratedTypeName(currentSchema, terminalAlternative)}({projected})";
            for (var index = aggregatePath.Count - 1; index >= 0; index--)
            {
                var owner = index == 0 ? targetAggregateSelect.Declaration : aggregatePath[index - 1];
                expression = $"{GeneratedTypeName(currentSchema, owner)}."
                    + $"From{ExpressEntityProjection.ToPascalCase(aggregatePath[index].Name)}({expression})";
            }

            return expression;
        }

        if (TryGetSpecializedAlias(target, source, out var underlying))
        {
            return CreateSpecializationProjection(currentSchema, underlying, target, $"({expression}).Value");
        }

        if (source is ExpressBoundAggregateType sourceAggregate
            && target is ExpressBoundAggregateType targetAggregate)
        {
            if (AreRedeclarationEquivalent(sourceAggregate.ElementType, targetAggregate.ElementType)
                || (TryGetDirectEntity(sourceAggregate.ElementType, out _)
                    && TryGetDirectEntity(targetAggregate.ElementType, out _)))
            {
                return expression;
            }

            const string parameter = "selected";
            var projected = CreateSpecializationProjection(
                currentSchema,
                sourceAggregate.ElementType,
                targetAggregate.ElementType,
                parameter);
            return $"global::TedToolkit.Step21.ExpressAggregateView.Project({expression}, {parameter} => {projected})";
        }

        if (target is ExpressBoundScalarType { Kind: ExpressScalarKind.Number, }
            && source is ExpressBoundScalarType sourceScalar)
        {
            return sourceScalar.Kind switch
            {
                ExpressScalarKind.Integer =>
                    $"global::TedToolkit.Step21.NumberValue.FromInteger({expression})",
                ExpressScalarKind.Real =>
                    $"global::TedToolkit.Step21.NumberValue.FromReal({expression})",
                _ => throw new InvalidOperationException("The numeric redeclaration projection is unsupported."),
            };
        }

        if (TryGetDirectEntity(source, out var sourceEntity))
        {
            return CreateEntityDomainProjection(currentSchema, sourceEntity, target, expression);
        }

        if (TryGetClosedSelect(source, out var sourceSelect))
        {
            var handlers = GetSelectAlternatives(sourceSelect)
                .Select((alternative, index) =>
                {
                    var parameter = $"selected{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                    var alternativeType = new ExpressBoundNamedType(alternative, source.Span);
                    var projected = CreateSpecializationProjection(
                        currentSchema,
                        alternativeType,
                        target,
                        parameter);
                    return $"{parameter} => {projected}";
                });
            var targetType = target is ExpressBoundNamedType targetNamed
                ? GeneratedTypeName(currentSchema, targetNamed.Declaration)
                : throw new InvalidOperationException("The SELECT projection target must be a named entity domain.");
            return $"{expression}.Match<{targetType}>({string.Join(", ", handlers)})";
        }

        throw new InvalidOperationException(
            $"No lossless generated projection exists from '{Resolve(currentSchema, source).DataType.Type}' "
            + $"to '{Resolve(currentSchema, target).DataType.Type}'.");
    }

    /// <summary>
    /// Finds an exact nominal SELECT route, counting at most two routes to distinguish ambiguity.
    /// </summary>
    /// <param name="type">The inherited SELECT domain.</param>
    /// <param name="alternative">The stored nominal alternative.</param>
    /// <param name="path">The outer-to-inner route when unique.</param>
    /// <returns>Zero for no route, one for a unique route, or two for ambiguity.</returns>
    private int FindNominalSelectProjection(
        ExpressBoundType type,
        ExpressBoundSymbol alternative,
        out IReadOnlyList<ExpressBoundSymbol> path)
    {
        path = [];
        if (!TryGetClosedSelect(type, out var select))
        {
            return 0;
        }

        var alternatives = GetSelectAlternatives(select);
        if (alternatives.Contains(alternative))
        {
            path = [alternative,];
            return 1;
        }

        if (alternative.Kind == ExpressDeclarationKind.Entity)
        {
            return 0;
        }

        var count = 0;
        foreach (var candidate in alternatives)
        {
            var nestedCount = FindNominalSelectProjection(
                new ExpressBoundNamedType(candidate, type.Span),
                alternative,
                out var nestedPath);
            count += nestedCount;
            if (count > 1)
            {
                path = [];
                return 2;
            }

            if (nestedCount == 1)
            {
                path = new[] { candidate, }.Concat(nestedPath).ToArray();
            }
        }

        return count;
    }

    private int FindAggregateSelectProjection(
        ExpressBoundType type,
        ExpressBoundSymbol alternative,
        out IReadOnlyList<ExpressBoundSymbol> path)
    {
        path = [];
        if (!TryGetClosedSelect(type, out var select)
            || !_definedTypes.TryGetValue(alternative, out var narrowedDeclaration)
            || narrowedDeclaration.UnderlyingType is not ExpressBoundAggregateType narrowedAggregate)
        {
            return 0;
        }

        var count = 0;
        foreach (var candidate in GetSelectAlternatives(select))
        {
            if (_definedTypes.TryGetValue(candidate, out var candidateDeclaration)
                && candidateDeclaration.UnderlyingType is ExpressBoundAggregateType candidateAggregate
                && ClassifySpecialization(candidateAggregate, narrowedAggregate)
                    == ExpressRedeclarationClassification.Supported)
            {
                count++;
                path = [candidate,];
            }
            else
            {
                var nestedCount = FindAggregateSelectProjection(
                    new ExpressBoundNamedType(candidate, type.Span),
                    alternative,
                    out var nestedPath);
                count += nestedCount;
                if (nestedCount == 1)
                {
                    path = new[] { candidate, }.Concat(nestedPath).ToArray();
                }
            }

            if (count > 1)
            {
                path = [];
                return 2;
            }
        }

        return count;
    }

    private bool TryGetSpecializedAlias(
        ExpressBoundType original,
        ExpressBoundType narrowed,
        out ExpressBoundNamedType underlying)
    {
        if (narrowed is ExpressBoundNamedType named
            && _definedTypes.TryGetValue(named.Declaration, out var definition)
            && definition.UnderlyingType is ExpressBoundNamedType alias
            && ClassifySpecialization(original, alias)
                is ExpressRedeclarationClassification.Equivalent or ExpressRedeclarationClassification.Supported)
        {
            underlying = alias;
            return true;
        }

        underlying = null!;
        return false;
    }

    private bool TryGetTransparentAlias(
        ExpressBoundType type,
        out ExpressBoundNamedType underlying)
    {
        if (type is ExpressBoundNamedType named
            && named.Declaration.Kind != ExpressDeclarationKind.Entity
            && _definedTypes.TryGetValue(named.Declaration, out var definition)
            && definition.UnderlyingType is ExpressBoundNamedType alias)
        {
            underlying = alias;
            return true;
        }

        underlying = null!;
        return false;
    }

    private string CreateEntityDomainProjection(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundSymbol sourceEntity,
        ExpressBoundType target,
        string expression)
    {
        if (TryGetDirectEntity(target, out var targetEntity) && IsEntitySubtype(sourceEntity, targetEntity))
        {
            return expression;
        }

        if (target is not ExpressBoundNamedType targetNamed
            || !TryGetClosedSelect(target, out var targetSelect))
        {
            throw new InvalidOperationException("The entity-domain redeclaration projection is unsupported.");
        }

        if (!TrySelectProjectionAlternative(sourceEntity, targetSelect, out var alternative))
        {
            throw new InvalidOperationException(
                "The narrowed entity has no unique lossless projection into the inherited SELECT.");
        }

        var alternativeType = new ExpressBoundNamedType(alternative, target.Span);
        var alternativeValue = CreateEntityDomainProjection(
            currentSchema,
            sourceEntity,
            alternativeType,
            expression);
        var targetType = GeneratedTypeName(currentSchema, targetNamed.Declaration);
        return $"{targetType}.From{ExpressEntityProjection.ToPascalCase(alternative.Name)}({alternativeValue})";
    }

    private bool TrySelectProjectionAlternative(
        ExpressBoundSymbol sourceEntity,
        ExpressBoundSelectType targetSelect,
        out ExpressBoundSymbol alternative)
    {
        var candidates = GetSelectAlternatives(targetSelect)
            .Where(candidate => CanProjectEntityTo(sourceEntity, candidate))
            .ToArray();
        var exact = candidates.FirstOrDefault(candidate => ReferenceEquals(candidate, sourceEntity));
        if (exact is not null)
        {
            alternative = exact;
            return true;
        }

        var mostSpecific = candidates.Where(candidate => candidates.All(other =>
                ReferenceEquals(candidate, other) || IsAlternativeAtLeastAsSpecific(candidate, other)))
            .ToArray();
        if (mostSpecific.Length == 1)
        {
            alternative = mostSpecific[0];
            return true;
        }

        var directEntities = candidates
            .Where(candidate => candidate.Kind == ExpressDeclarationKind.Entity)
            .ToArray();
        if (directEntities.Length == 1)
        {
            alternative = directEntities[0];
            return true;
        }

        alternative = null!;
        return false;
    }

    private bool IsAlternativeAtLeastAsSpecific(ExpressBoundSymbol candidate, ExpressBoundSymbol other)
    {
        var candidateType = new ExpressBoundNamedType(candidate, candidate.Span);
        var otherType = new ExpressBoundNamedType(other, other.Span);
        return TryGetClosedEntityLeaves(candidateType, out var candidateLeaves)
            && TryGetClosedEntityLeaves(otherType, out var otherLeaves)
            && candidateLeaves.All(candidateLeaf =>
                otherLeaves.Any(otherLeaf => IsEntitySubtype(candidateLeaf, otherLeaf)));
    }

    private bool CanProjectEntityTo(ExpressBoundSymbol sourceEntity, ExpressBoundSymbol target)
    {
        if (target.Kind == ExpressDeclarationKind.Entity)
        {
            return IsEntitySubtype(sourceEntity, target);
        }

        return _definedTypes.TryGetValue(target, out var declaration)
            && declaration.UnderlyingType is ExpressBoundSelectType select
            && !select.IsExtensible
            && GetSelectAlternatives(select).Any(alternative => CanProjectEntityTo(sourceEntity, alternative));
    }

    private static string GeneratedTypeName(
        ExpressBoundSchemaIdentity currentSchema,
        ExpressBoundSymbol symbol)
    {
        var generatedName = ExpressEntityProjection.ToPascalCase(symbol.Name);
        if (symbol.Kind == ExpressDeclarationKind.Entity)
        {
            generatedName = $"I{generatedName}";
        }

        return ReferenceEquals(currentSchema, symbol.DeclaringSchema)
            ? generatedName
            : $"global::TedToolkit.Step21.Generated.{ExpressEntityProjection.ToPascalCase(symbol.DeclaringSchema.Name)}.{generatedName}";
    }

    private bool TryGetClosedSelect(ExpressBoundType type, out ExpressBoundSelectType select)
    {
        if (type is ExpressBoundNamedType named
            && _definedTypes.TryGetValue(named.Declaration, out var declaration)
            && declaration.UnderlyingType is ExpressBoundSelectType candidate
            && !candidate.IsExtensible)
        {
            select = candidate;
            return true;
        }

        select = null!;
        return false;
    }

    /// <summary>
    /// Finds the inherited attribute named by a qualified redeclaration.
    /// </summary>
    /// <param name="attribute">The redeclared attribute.</param>
    /// <param name="original">The resolved inherited attribute.</param>
    /// <returns><see langword="true"/> when exactly one inherited declaration is found.</returns>
    internal bool TryGetRedeclaredAttribute(
        ExpressBoundAttribute attribute,
        out ExpressBoundAttribute original)
    {
        original = null!;
        if (attribute.RedeclaredEntity is null || attribute.RedeclaredAttributeName is null
            || !_entities.TryGetValue(attribute.RedeclaredEntity, out var entity))
        {
            return false;
        }

        var attributes = EnumerateAttributes(entity, new HashSet<ExpressBoundSymbol>()).ToArray();
        var matches = attributes
            .Where(candidate => string.Equals(
                candidate.Name,
                attribute.RedeclaredAttributeName,
                StringComparison.OrdinalIgnoreCase)
                && !attributes.Any(redeclaration =>
                redeclaration.RedeclaredEntity is not null
                && StringComparer.OrdinalIgnoreCase.Equals(redeclaration.RedeclaredAttributeName, candidate.Name)
                && (ReferenceEquals(redeclaration.RedeclaredEntity, candidate.DeclaringEntity)
                    || (ReferenceEquals(redeclaration.RedeclaredEntity, candidate.RedeclaredEntity)
                        && !ReferenceEquals(redeclaration.DeclaringEntity, candidate.DeclaringEntity)
                        && IsEntitySubtype(redeclaration.DeclaringEntity, candidate.DeclaringEntity)))))
            .ToArray();
        if (matches.Length != 1)
        {
            return false;
        }

        original = matches[0];
        return true;
    }

    /// <summary>
    /// Determines whether the qualified origin is a transitive supertype of the declaring entity.
    /// </summary>
    /// <param name="attribute">The qualified redeclaration.</param>
    /// <returns><see langword="true"/> when its origin belongs to the declaring entity's supertype closure.</returns>
    internal bool HasValidRedeclarationOrigin(ExpressBoundAttribute attribute)
    {
        return attribute.RedeclaredEntity is not null
            && IsEntitySubtype(attribute.DeclaringEntity, attribute.RedeclaredEntity)
            && !ReferenceEquals(attribute.DeclaringEntity, attribute.RedeclaredEntity)
            && TryGetRedeclaredAttribute(attribute, out _);
    }

    private static bool AreRedeclarationEquivalent(ExpressBoundType first, ExpressBoundType second)
    {
        return (first, second) switch
        {
            (ExpressBoundScalarType left, ExpressBoundScalarType right) => left.Kind == right.Kind,
            (ExpressBoundAggregateType left, ExpressBoundAggregateType right) =>
                left.Kind == right.Kind
                && AggregateMetadataEquivalent(left, right)
                && AreRedeclarationEquivalent(left.ElementType, right.ElementType),
            (ExpressBoundNamedType left, ExpressBoundNamedType right) =>
                ReferenceEquals(left.Declaration, right.Declaration),
            _ => ReferenceEquals(first, second),
        };
    }

    private static bool AggregateMetadataEquivalent(
        ExpressBoundAggregateType first,
        ExpressBoundAggregateType second)
    {
        return string.Equals(EffectiveBound(first.ResolvedLowerBoundText, first.LowerBoundText),
                EffectiveBound(second.ResolvedLowerBoundText, second.LowerBoundText),
                StringComparison.Ordinal)
            && string.Equals(EffectiveBound(first.ResolvedUpperBoundText, first.UpperBoundText),
                EffectiveBound(second.ResolvedUpperBoundText, second.UpperBoundText),
                StringComparison.Ordinal)
            && first.IsOptional == second.IsOptional
            && first.IsUnique == second.IsUnique;
    }

    private static string? EffectiveBound(string? resolved, string? source)
    {
        return resolved ?? source?.Replace(" ", "");
    }

    private static bool IsCardinalityNarrowing(ExpressBoundAggregateType original, ExpressBoundAggregateType narrowed)
    {
        if (original.Kind == ExpressAggregateKind.Array
            || original.IsOptional != narrowed.IsOptional
            || original.IsUnique != narrowed.IsUnique)
        {
            return false;
        }

        if (!BigInteger.TryParse(EffectiveBound(original.ResolvedLowerBoundText, original.LowerBoundText) ?? "0",
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var originalLower)
            || !BigInteger.TryParse(EffectiveBound(narrowed.ResolvedLowerBoundText, narrowed.LowerBoundText) ?? "0",
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var narrowedLower)
            || narrowedLower < originalLower)
        {
            return false;
        }

        var originalUpperText = EffectiveBound(original.ResolvedUpperBoundText, original.UpperBoundText) ?? "?";
        var narrowedUpperText = EffectiveBound(narrowed.ResolvedUpperBoundText, narrowed.UpperBoundText) ?? "?";
        if (narrowedUpperText == "?")
        {
            return originalUpperText == "?";
        }

        return BigInteger.TryParse(narrowedUpperText, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var narrowedUpper)
            && narrowedUpper >= narrowedLower
            && (originalUpperText == "?"
                || (BigInteger.TryParse(originalUpperText, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var originalUpper)
                    && narrowedUpper <= originalUpper));
    }

    private bool TryGetClosedEntityLeaves(ExpressBoundType type, out IReadOnlyList<ExpressBoundSymbol> leaves)
    {
        var result = new List<ExpressBoundSymbol>();
        var supported = AddClosedEntityLeaves(type, result, new HashSet<ExpressBoundSymbol>());
        leaves = result.Distinct().ToArray();
        return supported && leaves.Count > 0;
    }

    private bool AddClosedEntityLeaves(
        ExpressBoundType type,
        ICollection<ExpressBoundSymbol> leaves,
        ISet<ExpressBoundSymbol> path)
    {
        if (type is ExpressBoundNamedType named && named.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            leaves.Add(named.Declaration);
            return true;
        }

        if (type is not ExpressBoundNamedType defined
            || !_definedTypes.TryGetValue(defined.Declaration, out var declaration)
            || declaration.UnderlyingType is not ExpressBoundSelectType select
            || select.IsExtensible
            || !path.Add(defined.Declaration))
        {
            return false;
        }

        var supported = GetSelectAlternatives(select).All(alternative =>
            AddClosedEntityLeaves(new ExpressBoundNamedType(alternative, type.Span), leaves, path));
        _ = path.Remove(defined.Declaration);
        return supported;
    }

    private static bool TryGetDirectEntity(ExpressBoundType type, out ExpressBoundSymbol entity)
    {
        if (type is ExpressBoundNamedType named && named.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            entity = named.Declaration;
            return true;
        }

        entity = null!;
        return false;
    }

    private bool IsEntitySubtype(ExpressBoundSymbol candidate, ExpressBoundSymbol original)
    {
        if (ReferenceEquals(candidate, original))
        {
            return true;
        }

        return _entities.TryGetValue(candidate, out var entity)
            && entity.DirectSupertypes.Any(supertype => IsEntitySubtype(supertype, original));
    }

    private IEnumerable<ExpressBoundAttribute> EnumerateAttributes(
        ExpressBoundEntity entity,
        ISet<ExpressBoundSymbol> visited)
    {
        if (!visited.Add(entity.Symbol))
        {
            yield break;
        }

        foreach (var attribute in entity.Attributes)
        {
            yield return attribute;
        }

        foreach (var supertype in entity.DirectSupertypes)
        {
            foreach (var attribute in EnumerateAttributes(_entities[supertype], visited))
            {
                yield return attribute;
            }
        }
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

        if (!_definedTypes.TryGetValue(symbol, out var declaration))
        {
            return false;
        }

        if (!path.Add(symbol))
        {
            // The binder already rejects uncontained type cycles. A recursive aggregate
            // can use its nominal carrier; every non-recursive dependency is still checked.
            return true;
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