// -----------------------------------------------------------------------
// <copyright file="ExpressReachableRuleEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.RoslynHelper;
using TedToolkit.RoslynHelper.Syntaxes;

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Analysis;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Emits the validated private constant, function, and derived-attribute rule closure.
/// </summary>
internal static class ExpressReachableRuleEmitter
{
    private const int ENTITY_VALUE_BRANCHES_PER_METHOD = 32;

    private const int SELECT_VALUE_EQUALITY_METHODS_PER_SHARD = 16;

    private const string ACTIVE_PAIR_LIST =
        "new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<"
        + "global::TedToolkit.Step21.Entity, global::TedToolkit.Step21.Entity>>()";

    private const string POPULATION_TYPE =
        "global::System.Collections.Generic.IReadOnlyList<global::System.Collections.Generic.KeyValuePair<global::System.String, global::TedToolkit.Step21.Entity>>";

    private const string FAILURE_LIST_TYPE =
        "global::System.Collections.Generic.List<global::TedToolkit.Step21.ValidationFailure>";

    private const string INVERSE_CACHE_TYPE =
        "global::System.Collections.Generic.Dictionary<global::TedToolkit.Step21.Entity, "
        + "global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object?>>";

    /// <summary>
    /// Creates private static helpers for every reachable declaration dependency.
    /// </summary>
    /// <param name="plan">The validated reachability plan.</param>
    /// <param name="resolver">The generated value-type resolver.</param>
    /// <param name="entities">The generated entity projections available to private model operations.</param>
    /// <param name="complexEntities">The generated complex entity projections available to value equality.</param>
    /// <param name="shards">The independent compiler-cache owners for large descriptors.</param>
    /// <returns>The helper methods in deterministic declaration order.</returns>
    /// <exception cref="InvalidOperationException">A reachable plan contains a non-executable declaration kind.</exception>
    internal static IReadOnlyList<Method> CreateDependencyMethods(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        IReadOnlyList<ExpressEntityProjection> entities,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntities,
        ExpressDescriptorShards shards)
    {
        var result = new List<Method>();
        foreach (var declaration in plan.ReachableDeclarations)
        {
            if (declaration is not ExpressBoundOpaqueDeclaration opaque)
            {
                continue;
            }

            result.Add(declaration.Kind switch
            {
                ExpressDeclarationKind.Constant when opaque.DeclaredType is not null =>
                    CreateConstantMethod(plan, opaque, resolver),
                ExpressDeclarationKind.Function when opaque.DeclaredType is not null =>
                    CreateFunctionMethod(plan, opaque, resolver),
                ExpressDeclarationKind.Procedure => CreateProcedureMethod(plan, opaque),
                _ => throw new InvalidOperationException(
                    $"Reachable declaration '{declaration.Name}' is not an executable algorithm."),
            });
        }

        result.AddRange(plan.ReachableDerivedAttributes.Select(attribute =>
            CreateDerivedMethod(plan, attribute, resolver)));
        result.AddRange(CreateModelMethods(plan, resolver, entities));
        if (plan.RequiresEntityValueEquality)
        {
            result.AddRange(CreateEntityValueEqualsMethods(plan, resolver, entities, complexEntities, shards));
            result.Add(CreateOrderedValueEqualsMethod());
            result.Add(CreateArrayValueEqualsMethod());
            result.Add(CreateUnorderedValueEqualsMethod());
            result.Add(CreateHasPerfectMatchMethod());
            result.Add(CreateTryMatchMethod());
        }

        return result;
    }

    /// <summary>
    /// Creates an expression-emission context for a generated validation or helper method.
    /// </summary>
    /// <param name="plan">The validated reachability plan.</param>
    /// <param name="selfExpression">The enclosing EXPRESS SELF expression.</param>
    /// <param name="populationExpression">The generated spelling for the current validation population.</param>
    /// <param name="lexicalNames">Generated spellings for formal or local names.</param>
    /// <param name="safeIndices">Aggregate and REPEAT-variable pairs whose index access is proven present.</param>
    /// <param name="allocateTemporaryName">Allocates deterministic private names within the generated method.</param>
    /// <param name="selectNarrowings">Maps lexical SELECT values to branch-proven entity alternatives.</param>
    /// <param name="pathNarrowings">Maps qualified reference paths to branch-proven entity alternatives.</param>
    /// <param name="determinateLexicals">Identifies branch-proven determinate lexical values.</param>
    /// <param name="safeIndexPaths">Qualified aggregate paths proven present at a REPEAT index.</param>
    /// <param name="scalarNarrowings">Branch-local scalar projections over stable lexical storage.</param>
    /// <param name="currentDerivedAttribute">The derived redeclaration currently being evaluated, if any.</param>
    /// <param name="selfEntity">The concrete entity projection represented by SELF, if known.</param>
    /// <returns>The immutable emission context.</returns>
    internal static ExpressExpressionEmissionContext CreateContext(
        ExpressReachableRulePlan plan,
        string? selfExpression,
        string populationExpression,
        IReadOnlyDictionary<string, (string Code, ExpressBoundType Type)>? lexicalNames = null,
        IReadOnlyCollection<KeyValuePair<ExpressBoundName, ExpressBoundName>>? safeIndices = null,
        Func<string, string>? allocateTemporaryName = null,
        IReadOnlyDictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings = null,
        IReadOnlyList<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>? pathNarrowings = null,
        ISet<ExpressBoundName>? determinateLexicals = null,
        IReadOnlyList<KeyValuePair<ExpressBoundExpression, ExpressBoundName>>? safeIndexPaths = null,
        IReadOnlyDictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>?
            scalarNarrowings = null,
        ExpressBoundAttribute? currentDerivedAttribute = null,
        ExpressBoundSymbol? selfEntity = null)
    {
        var temporaryOrdinal = 0;
        allocateTemporaryName ??= prefix => prefix
            + (temporaryOrdinal++).ToString(CultureInfo.InvariantCulture);

        string ResolveBoundReference(
            ExpressBoundName reference,
            ExpressBoundType? narrowedType,
            string? narrowedCode,
            ExpressBoundSymbol? narrowedAlternative)
        {
            if (scalarNarrowings?.TryGetValue(reference.Name, out var scalarNarrowing) == true
                && lexicalNames?.TryGetValue(reference.Name, out var scalarStorage) == true
                && string.Equals(
                    scalarStorage.Code,
                    scalarNarrowing.StorageCode,
                    StringComparison.Ordinal))
            {
                return scalarNarrowing.Code;
            }

            if (reference.Kind == ExpressBoundNameKind.Variable
                && reference.Type is ExpressBoundScalarType scalarType
                && scalarType.Kind is ExpressScalarKind.Boolean
                    or ExpressScalarKind.Integer
                    or ExpressScalarKind.Number
                    or ExpressScalarKind.Real
                && plan.Schema.IndeterminateLocals.Contains(reference)
                && determinateLexicals?.Contains(reference) == true
                && lexicalNames?.TryGetValue(reference.Name, out var determinateLexical) == true)
            {
                return $"({determinateLexical.Code}).Value";
            }

            return ResolveReference(
                plan,
                reference,
                selfExpression,
                populationExpression,
                lexicalNames,
                selectNarrowings,
                narrowedType,
                narrowedCode,
                narrowedAlternative);
        }

        (string Code, ExpressBoundType Type)? ResolveLexicalBound(string name)
        {
            if (scalarNarrowings?.TryGetValue(name, out var scalarNarrowing) == true
                && lexicalNames?.TryGetValue(name, out var scalarStorage) == true
                && string.Equals(
                    scalarStorage.Code,
                    scalarNarrowing.StorageCode,
                    StringComparison.Ordinal))
            {
                return (scalarNarrowing.Code, scalarNarrowing.Type);
            }

            return lexicalNames is not null && lexicalNames.TryGetValue(name, out var lexical)
                ? lexical
                : null;
        }

        return new(
            ResolveBoundReference,
            selfExpression,
            resolveModelFunction: (operation, expression, arguments) =>
                ResolveModelFunction(
                    plan,
                    operation,
                    expression,
                    arguments,
                    populationExpression,
                    aggregateUnionAllowsRuntimeNarrowing: operation == "AGGREGATE_UNION"
                        && expression.Children.Any(child => IsNarrowedEntityExpression(
                            child,
                            selectNarrowings,
                            pathNarrowings)),
                    typeOfCarrierTypeOverride: operation == "TYPEOF"
                        && expression.Children[0].Reference is { } typeOfReference
                        ? ResolveLexicalBound(typeOfReference.Name)?.Type
                        : null),
            resolveValueEquality: (left, leftCode, right, rightCode, leftTypeOverride) =>
                ResolveValueEquality(plan, left, leftCode, right, rightCode, leftTypeOverride),
            resolveAttribute: (sourceExpression, reference, source) =>
                ResolveAttribute(
                    plan,
                    sourceExpression,
                    reference,
                    source,
                    populationExpression,
                    selectNarrowings,
                    pathNarrowings,
                    currentDerivedAttribute),
            resolveApplication: (expression, arguments) =>
                ResolveApplication(
                    plan,
                    expression,
                    arguments,
                    populationExpression,
                    selectNarrowings,
                    pathNarrowings,
                    selfEntity),
            safeIndices: safeIndices,
            resolveLexicalBound: ResolveLexicalBound,
            genericTypeLabels: ExpressTypeAnalysis.GenericTypeLabels(
                lexicalNames?.Values.Select(lexical => lexical.Type) ?? []),
            allocateTemporaryName: allocateTemporaryName,
            isKnownDeterminate: expression => (expression.Kind == ExpressExpressionKind.Reference
                && expression.Reference is { } lexicalReference
                && determinateLexicals?.Contains(lexicalReference) == true)
                || (expression.Kind == ExpressExpressionKind.IndexQualifier
                    && expression.Children.Count >= 2
                    && expression.Children[1].Reference is { } indexReference
                    && safeIndexPaths?.Any(pair => ReferenceEquals(pair.Value, indexReference)
                        && SameDirectReferencePath(pair.Key, expression.Children[0])) == true)
                || (expression.Kind == ExpressExpressionKind.AttributeQualifier
                && expression.Reference is { IsOptional: false, } attributeReference
                && attributeReference.AttributeCandidates.All(attribute =>
                    attribute.Kind == ExpressAttributeKind.Explicit)
                && ((expression.Children[0].Reference is { } sourceReference
                    && selectNarrowings?.TryGetValue(sourceReference, out var alternative) == true
                    && alternative.Kind == ExpressDeclarationKind.Entity
                    && plan.EntityProjections.Single(projection => projection.Entity.Symbol == alternative)
                        .PhysicalComponents.Any(component =>
                            attributeReference.AttributeCandidates.Any(attribute =>
                                ReferenceEquals(component, plan.GetAttributeOwner(attribute)))))
                || (expression.Children[0].Kind == ExpressExpressionKind.GroupQualifier
                    && expression.Children[0].Children[0].Reference is { } groupedSourceReference
                    && selectNarrowings?.TryGetValue(groupedSourceReference, out var groupedAlternative) == true
                    && groupedAlternative.Kind == ExpressDeclarationKind.Entity
                    && plan.EntityProjections.Single(projection =>
                            projection.Entity.Symbol == groupedAlternative)
                        .PhysicalComponents.Any(component =>
                            attributeReference.AttributeCandidates.Any(attribute =>
                                ReferenceEquals(component, plan.GetAttributeOwner(attribute)))))))
                || (expression.Kind == ExpressExpressionKind.GroupQualifier
                && expression.Reference?.SchemaDeclaration is { } group
                && expression.Children[0].Reference is { } groupSourceReference
                && selectNarrowings?.TryGetValue(groupSourceReference, out var groupAlternative) == true
                && plan.EntityProjections.Single(projection =>
                        projection.Entity.Symbol == groupAlternative)
                    .PhysicalComponents.Any(component => component.Symbol == group)),
            mayReturnIndeterminate: expression => (expression.Kind == ExpressExpressionKind.Application
                    && expression.Reference?.SchemaDeclaration is { } application
                    && plan.Schema.IndeterminateFunctions.Contains(application))
                || (expression.Kind == ExpressExpressionKind.Reference
                    && expression.Reference is { Kind: ExpressBoundNameKind.Variable, } variable
                    && plan.Schema.IndeterminateLocals.Contains(variable)
                    && determinateLexicals?.Contains(variable) != true),
            resolveNarrowedScalarReference: (reference, carrierType, carrier, narrowedScalar) =>
                ResolveNarrowedScalarReference(
                    plan,
                    reference,
                    selfExpression,
                    populationExpression,
                    lexicalNames,
                    selectNarrowings,
                    carrierType,
                    carrier,
                    narrowedScalar),
            resolveSelectToEntityValue: (expression, source, target) =>
                ResolveSelectToEntityValue(plan, expression, source, target, allocateTemporaryName),
            resolveNarrowedEntityCarrier: (carrierType, carrier, target) =>
                ResolveNarrowedEntityCarrier(plan, carrierType, carrier, target.Declaration),
            resolveAggregateElement: (expression, source, target, sourceIsDeterminate) =>
                ResolveAggregateElementValue(plan, expression, source, target, sourceIsDeterminate),
            resolveAggregateType: plan.Resolver.GetAggregateType,
            resolveSelectedAggregateIndex: (expression, arguments, resultType) =>
                ResolveSelectedAggregateIndex(plan, expression, arguments, resultType),
            resolveAggregateSource: (sourceType, source, aggregate) =>
                ResolveAggregateSource(plan, sourceType, source, aggregate),
            resolveDefinedValueType: type => ResolveDefinedValueType(plan, type),
            isSelectValueType: type => IsSelectValueType(plan, type));
    }

    private static ExpressBoundType ResolveDefinedValueType(
        ExpressReachableRulePlan plan,
        ExpressBoundType type)
    {
        while (type is ExpressBoundNamedType named
               && named.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            var underlying = plan.Resolver.GetDefinedType(named.Declaration).UnderlyingType;
            if (underlying is ExpressBoundEnumerationType or ExpressBoundSelectType)
            {
                return type;
            }

            type = underlying;
        }

        return type;
    }

    private static bool CanEmitIndeterminate(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression)
    {
        return expression.DescendantsAndSelf().Any(candidate =>
            candidate.Kind == ExpressExpressionKind.Indeterminate
            || candidate.Type.CanBeIndeterminate
            || (candidate.Kind == ExpressExpressionKind.Application
                && candidate.Reference?.SchemaDeclaration is { } application
                && plan.Schema.IndeterminateFunctions.Contains(application)));
    }

    private static bool IsDeterminateBooleanExpression(ExpressBoundExpression expression)
    {
        if (expression.Type.Kind == ExpressExpressionTypeKind.Boolean
            && !expression.Type.CanBeIndeterminate)
        {
            return true;
        }

        return expression.Operation?.ToUpperInvariant() switch
        {
            "NOT" => expression.Children.Count == 1
                && IsDeterminateBooleanExpression(expression.Children[0]),
            "AND" or "OR" or "XOR" => expression.Children.Count == 2
                && expression.Children.All(IsDeterminateBooleanExpression),
            _ => false,
        };
    }

    private static bool IsLogicalOperatorExpression(ExpressBoundExpression expression)
    {
        return expression.Operation?.ToUpperInvariant() is "NOT" or "AND" or "OR" or "XOR";
    }

    private static bool IsSelectValueType(
        ExpressReachableRulePlan plan,
        ExpressBoundType type)
    {
        var visited = new HashSet<ExpressBoundSymbol>();
        while (type is ExpressBoundNamedType named
               && named.Declaration.Kind != ExpressDeclarationKind.Entity
               && visited.Add(named.Declaration))
        {
            type = plan.Resolver.GetDefinedType(named.Declaration).UnderlyingType;
        }

        return type is ExpressBoundSelectType;
    }

    private static string UnwrapDefinedValue(
        ExpressReachableRulePlan plan,
        ExpressBoundType type,
        string value)
    {
        var visited = new HashSet<ExpressBoundSymbol>();
        while (type is ExpressBoundNamedType named
               && named.Declaration.Kind != ExpressDeclarationKind.Entity
               && visited.Add(named.Declaration))
        {
            var underlying = plan.Resolver.GetDefinedType(named.Declaration).UnderlyingType;
            if (underlying is ExpressBoundEnumerationType or ExpressBoundSelectType)
            {
                break;
            }

            value = $"({value}).Value";
            type = underlying;
        }

        return value;
    }

    private static bool TryStaticStringValue(
        ExpressBoundExpression expression,
        out string value)
    {
        if (expression.Kind == ExpressExpressionKind.Literal
            && expression.Type.Kind == ExpressExpressionTypeKind.String
            && expression.SourceText.Length >= 2)
        {
            value = expression.SourceText.Substring(1, expression.SourceText.Length - 2)
                .Replace("''", "'");
            return true;
        }

        if (expression.Kind == ExpressExpressionKind.Binary
            && expression.Operation == "+"
            && expression.Children.Count == 2
            && TryStaticStringValue(expression.Children[0], out var left)
            && TryStaticStringValue(expression.Children[1], out var right))
        {
            value = left + right;
            return true;
        }

        value = "";
        return false;
    }

    private static bool TryAdaptScalarUnionElement(
        ExpressReachableRulePlan plan,
        ExpressBoundScalarType source,
        ExpressBoundType target,
        string value,
        ISet<ExpressBoundSymbol> visited,
        out string adapted,
        out int rank)
    {
        if (target is ExpressBoundScalarType targetScalar && CanProjectScalar(source.Kind, targetScalar.Kind))
        {
            adapted = PromoteScalarValue(value, source.Kind, targetScalar.Kind);
            rank = source.Kind == targetScalar.Kind ? 0 : 1;
            return true;
        }

        if (target is not ExpressBoundNamedType named
            || named.Declaration.Kind == ExpressDeclarationKind.Entity
            || !visited.Add(named.Declaration))
        {
            adapted = "";
            rank = int.MaxValue;
            return false;
        }

        var underlying = plan.Resolver.GetDefinedType(named.Declaration).UnderlyingType;
        if (underlying is ExpressBoundSelectType select)
        {
            var candidates = plan.Resolver.GetSelectAlternatives(select)
                .Select(alternative =>
                {
                    var alternativeType = new ExpressBoundNamedType(alternative, select.Span);
                    var succeeds = TryAdaptScalarUnionElement(
                        plan,
                        source,
                        alternativeType,
                        value,
                        new HashSet<ExpressBoundSymbol>(visited),
                        out var candidate,
                        out var candidateRank);
                    return (Alternative: alternative, Succeeds: succeeds, Value: candidate, Rank: candidateRank);
                })
                .Where(candidate => candidate.Succeeds)
                .ToArray();
            if (candidates.Length > 0)
            {
                var bestRank = candidates.Min(candidate => candidate.Rank);
                var best = candidates.Where(candidate => candidate.Rank == bestRank).ToArray();
                if (best.Length == 1)
                {
                    adapted = ExpressExpressionEmitter.BoundTypeName(named)
                        + ".From"
                        + ExpressEntityProjection.ToPascalCase(best[0].Alternative.Name)
                        + $"({best[0].Value})";
                    rank = bestRank;
                    return true;
                }
            }

            adapted = "";
            rank = int.MaxValue;
            return false;
        }

        if (!TryAdaptScalarUnionElement(
                plan,
                source,
                underlying,
                value,
                visited,
                out var underlyingValue,
                out rank))
        {
            adapted = "";
            return false;
        }

        adapted = $"new {ExpressExpressionEmitter.BoundTypeName(named)}({underlyingValue})";
        return true;
    }

    private static string PromoteScalarValue(
        string value,
        ExpressScalarKind source,
        ExpressScalarKind target)
    {
        return (source, target) switch
        {
            (ExpressScalarKind.Integer, ExpressScalarKind.Number) =>
                $"global::TedToolkit.Step21.NumberValue.FromInteger({value})",
            (ExpressScalarKind.Real, ExpressScalarKind.Number) =>
                $"global::TedToolkit.Step21.NumberValue.FromReal({value})",
            (ExpressScalarKind.Integer, ExpressScalarKind.Real) =>
                $"new global::TedToolkit.Step21.RealValue(({value}), "
                + "global::System.Numerics.BigInteger.Zero)",
            (ExpressScalarKind.Number, ExpressScalarKind.Integer) =>
                $"({value}).ToIntegerTruncated()",
            (ExpressScalarKind.Number, ExpressScalarKind.Real) => $"({value}).ToReal()",
            _ when source == target => value,
            _ => throw new InvalidOperationException(
                $"Scalar {source.ToString()} cannot be promoted to {target.ToString()} for aggregate union."),
        };
    }

    private static ExpressBoundScalarType? ScalarTypeOf(ExpressBoundExpression expression)
    {
        var kind = expression.Type.Kind switch
        {
            ExpressExpressionTypeKind.Binary => ExpressScalarKind.Binary,
            ExpressExpressionTypeKind.Boolean => ExpressScalarKind.Boolean,
            ExpressExpressionTypeKind.Integer => ExpressScalarKind.Integer,
            ExpressExpressionTypeKind.Logical => ExpressScalarKind.Logical,
            ExpressExpressionTypeKind.Number => ExpressScalarKind.Number,
            ExpressExpressionTypeKind.Real => ExpressScalarKind.Real,
            ExpressExpressionTypeKind.String => ExpressScalarKind.String,
            _ => (ExpressScalarKind?)null,
        };
        return kind is { } scalarKind
            ? new ExpressBoundScalarType(
                scalarKind,
                constraintText: null,
                isFixed: false,
                expression.Span)
            : null;
    }

    private static ExpressScalarKind? ResolveExpressionScalarKind(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression)
    {
        if (ScalarTypeOf(expression) is { } inferred)
        {
            return inferred.Kind;
        }

        var type = expression.Type.DeclaredType;
        var visited = new HashSet<ExpressBoundSymbol>();
        while (type is ExpressBoundNamedType named
               && named.Declaration.Kind != ExpressDeclarationKind.Entity
               && visited.Add(named.Declaration))
        {
            type = plan.Resolver.GetDefinedType(named.Declaration).UnderlyingType;
        }

        return type is ExpressBoundScalarType scalar ? scalar.Kind : null;
    }

    private static string? ResolveScalarExpressionToSelectValue(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType target,
        ExpressBoundExpression expression,
        string source,
        bool canBeIndeterminate,
        string presentName)
    {
        if (ScalarTypeOf(expression) is not { } scalarSource)
        {
            return null;
        }

        var candidate = canBeIndeterminate ? presentName : source;
        if (!TryAdaptScalarUnionElement(
            plan,
            scalarSource,
            target,
            candidate,
            new HashSet<ExpressBoundSymbol>(),
            out var adapted,
            out _))
        {
            return null;
        }

        if (!canBeIndeterminate)
        {
            return adapted;
        }

        var targetName = ExpressExpressionEmitter.BoundTypeName(target);
        return $"(({source}) is {{ }} {presentName} ? {adapted} : ({targetName}?)null)";
    }

    private static string ResolveValueUsedIn(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        string candidate,
        string populationExpression,
        string resultElementType)
    {
        if (!TryStaticStringValue(expression.Children[1], out var role))
        {
            throw new InvalidOperationException(
                "Value-based USEDIN requires a statically known attribute role.");
        }

        var attribute = plan.EntityProjections
            .SelectMany(entity => entity.OwnAttributes.Select(item => (Entity: entity, Attribute: item)))
            .SingleOrDefault(item => string.Equals(
                role,
                $"{plan.Schema.Name}.{item.Entity.Entity.Name}.{item.Attribute.Attribute.Name}",
                StringComparison.OrdinalIgnoreCase));
        if (attribute.Entity is null)
        {
            throw new InvalidOperationException(
                $"Value-based USEDIN role '{role}' does not resolve to one generated attribute.");
        }

        var ownerType = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
            attribute.Entity.Entity.Symbol,
            expression.Span));
        var owner = "__expressUsedInValueOwner_"
            + expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var member = $"({owner}).{attribute.Attribute.StorageMemberName}";
        var value = attribute.Attribute.Attribute.IsOptional
            ? "__expressUsedInValue_"
                + expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                + "_"
                + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
            : member;
        var semanticType = ResolveDefinedValueType(plan, attribute.Attribute.Type);
        var equality = "global::System.Collections.Generic.EqualityComparer<"
            + ExpressExpressionEmitter.BoundTypeName(semanticType)
            + $">.Default.Equals({UnwrapDefinedValue(plan, attribute.Attribute.Type, value)}, ({candidate}))";
        var predicate = attribute.Attribute.Attribute.IsOptional
            ? $"{member} is {{ }} {value} && {equality}"
            : equality;
        return $"(global::TedToolkit.Step21.ExpressBag<{resultElementType}>)[.."
            + "global::System.Linq.Enumerable.Where("
            + $"global::System.Linq.Enumerable.OfType<{ownerType}>("
            + $"global::System.Linq.Enumerable.Select({populationExpression}, entry => entry.Value)), "
            + $"{owner} => {predicate})]";
    }

    private static bool IsNarrowedEntityExpression(
        ExpressBoundExpression expression,
        IReadOnlyDictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings,
        IReadOnlyList<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>? pathNarrowings)
    {
        return (expression.Reference is { } reference
                && selectNarrowings?.TryGetValue(reference, out var alternative) == true
                && alternative.Kind == ExpressDeclarationKind.Entity)
            || pathNarrowings?.Any(narrowing => narrowing.Value.Kind == ExpressDeclarationKind.Entity
                && SameDirectReferencePath(narrowing.Key, expression)) == true;
    }

    private static string ResolveValueEquality(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression left,
        string leftCode,
        ExpressBoundExpression right,
        string rightCode,
        ExpressBoundType? leftTypeOverride,
        bool instanceEquality = false)
    {
        if (leftTypeOverride is null
            && left.Type.DeclaredType is ExpressBoundAggregateType leftAggregate)
        {
            return CreateBoundValueEquality(
                plan,
                plan.Resolver,
                leftAggregate,
                leftCode,
                rightCode,
                ACTIVE_PAIR_LIST);
        }

        if (leftTypeOverride is null && left.Type.Kind != ExpressExpressionTypeKind.Select)
        {
            if (right.Type.Kind == ExpressExpressionTypeKind.Select)
            {
                return ResolveValueEquality(
                    plan,
                    right,
                    rightCode,
                    left,
                    leftCode,
                    null,
                    instanceEquality);
            }

            return instanceEquality
                ? $"global::System.Object.ReferenceEquals(({leftCode}), ({rightCode}))"
                : "__ExpressEntityValueEquals("
                    + $"(global::TedToolkit.Step21.Entity)({leftCode}), "
                    + $"(global::TedToolkit.Step21.Entity)({rightCode}), "
                    + ACTIVE_PAIR_LIST
                    + ")";
        }

        ExpressBoundType? selectType = leftTypeOverride ?? left.Type.DeclaredType;
        while (selectType is ExpressBoundNamedType namedSelect)
        {
            selectType = plan.Resolver.GetDefinedType(namedSelect.Declaration).UnderlyingType;
        }

        if (selectType is not ExpressBoundSelectType select)
        {
            throw new InvalidOperationException(
                "SELECT value equality requires a statically resolved generated SELECT type.");
        }

        if (right.Type.Kind == ExpressExpressionTypeKind.Select)
        {
            return CreateSelectValueEquality(
                plan,
                plan.Resolver,
                select,
                leftCode,
                rightCode,
                ACTIVE_PAIR_LIST,
                instanceEquality,
                right.Type.DeclaredType);
        }

        ExpressBoundSymbol? rightNominal = right.Type.DeclaredType is ExpressBoundNamedType namedRight
            ? namedRight.Declaration
            : null;
        while (rightNominal is not null
               && rightNominal.Kind != ExpressDeclarationKind.Entity
               && plan.Resolver.GetDefinedType(rightNominal).UnderlyingType is ExpressBoundNamedType nestedRight)
        {
            rightNominal = nestedRight.Declaration;
        }

        var branches = new List<string>();
        var alternatives = plan.Resolver.GetSelectAlternatives(select);
        for (var index = 0; index < alternatives.Count; index++)
        {
            var alternative = alternatives[index];
            var variable = "__expressSelected_"
                + left.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                + "_"
                + left.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                + "_"
                + index.ToString(CultureInfo.InvariantCulture);
            var value = variable;
            var selected = alternative;
            while (selected.Kind != ExpressDeclarationKind.Entity
                   && plan.Resolver.GetDefinedType(selected).UnderlyingType is ExpressBoundNamedType nested)
            {
                value = $"({value}).Value";
                selected = nested.Declaration;
            }

            string equality;
            var selectedType = selected.Kind == ExpressDeclarationKind.Entity
                ? null
                : plan.Resolver.GetDefinedType(selected).UnderlyingType;
            if (selectedType is ExpressBoundSelectType nestedSelect)
            {
                equality = right.Type.Kind == ExpressExpressionTypeKind.Entity
                    ? CreateSelectToEntityEquality(
                        plan,
                        nestedSelect,
                        value,
                        rightCode,
                        ACTIVE_PAIR_LIST,
                        instanceEquality)
                    : CreateSelectValueEquality(
                        plan,
                        plan.Resolver,
                        nestedSelect,
                        value,
                        rightCode,
                        ACTIVE_PAIR_LIST,
                        instanceEquality,
                        right.Type.DeclaredType);
            }
            else if (selected.Kind == ExpressDeclarationKind.Entity)
            {
                if (right.Type.Kind != ExpressExpressionTypeKind.Entity)
                {
                    equality = instanceEquality
                        ? "false"
                        : "global::TedToolkit.Step21.LogicalValue.False";
                }
                else if (instanceEquality)
                {
                    equality = $"global::System.Object.ReferenceEquals(({value}), ({rightCode}))";
                }
                else
                {
                    equality = "__ExpressEntityValueEquals("
                        + $"(global::TedToolkit.Step21.Entity)({value}), "
                        + $"(global::TedToolkit.Step21.Entity)({rightCode}), "
                        + "new global::System.Collections.Generic.List<"
                        + "global::System.Collections.Generic.KeyValuePair<"
                        + "global::TedToolkit.Step21.Entity, global::TedToolkit.Step21.Entity>>())";
                }
            }
            else if (instanceEquality)
            {
                equality = "false";
            }
            else
            {
                var underlying = selectedType!;
                var booleanEquality = underlying switch
                {
                    ExpressBoundScalarType scalar when scalar.Kind switch
                    {
                        ExpressScalarKind.Binary => right.Type.Kind == ExpressExpressionTypeKind.Binary,
                        ExpressScalarKind.Boolean => right.Type.Kind == ExpressExpressionTypeKind.Boolean,
                        ExpressScalarKind.Integer => right.Type.Kind == ExpressExpressionTypeKind.Integer,
                        ExpressScalarKind.Logical => right.Type.Kind == ExpressExpressionTypeKind.Logical,
                        ExpressScalarKind.Number => right.Type.Kind == ExpressExpressionTypeKind.Number,
                        ExpressScalarKind.Real => right.Type.Kind == ExpressExpressionTypeKind.Real,
                        ExpressScalarKind.String => right.Type.Kind == ExpressExpressionTypeKind.String,
                        _ => false,
                    }

                        => ExpressExpressionEmitter.ValueEqualityCore(
                        right,
                        $"({value}).Value",
                        right,
                        rightCode),
                    ExpressBoundEnumerationType when ReferenceEquals(selected, rightNominal) =>
                        $"global::System.StringComparer.Ordinal.Equals(({value}).Value, ({rightCode}).Value)",
                    _ => "false",
                };
                equality = $"(({booleanEquality}) ? global::TedToolkit.Step21.LogicalValue.True : "
                    + "global::TedToolkit.Step21.LogicalValue.False)";
            }

            branches.Add($"{variable} => {equality}");
        }

        return $"({leftCode}).Match({string.Join(", ", branches)})";
    }

    private static string CreateSelectToEntityEquality(
        ExpressReachableRulePlan plan,
        ExpressBoundSelectType select,
        string selectedCode,
        string entityCode,
        string activePairs,
        bool instanceEquality,
        int depth = 0)
    {
        var branches = new List<string>();
        var alternatives = plan.Resolver.GetSelectAlternatives(select);
        for (var index = 0; index < alternatives.Count; index++)
        {
            var alternative = alternatives[index];
            var variable = "__expressSelectedEntityComparison_"
                + depth.ToString(CultureInfo.InvariantCulture)
                + "_"
                + index.ToString(CultureInfo.InvariantCulture);
            var value = variable;
            var resolved = alternative;
            while (resolved.Kind != ExpressDeclarationKind.Entity
                   && plan.Resolver.GetDefinedType(resolved).UnderlyingType is ExpressBoundNamedType nested)
            {
                value = $"({value}).Value";
                resolved = nested.Declaration;
            }

            string equality;
            var resolvedType = resolved.Kind == ExpressDeclarationKind.Entity
                ? null
                : plan.Resolver.GetDefinedType(resolved).UnderlyingType;
            if (resolvedType is ExpressBoundSelectType nestedSelect)
            {
                equality = CreateSelectToEntityEquality(
                    plan,
                    nestedSelect,
                    value,
                    entityCode,
                    activePairs,
                    instanceEquality,
                    depth + 1);
            }
            else if (resolved.Kind == ExpressDeclarationKind.Entity && instanceEquality)
            {
                equality = $"global::System.Object.ReferenceEquals(({value}), ({entityCode}))";
            }
            else if (resolved.Kind == ExpressDeclarationKind.Entity)
            {
                equality = "__ExpressEntityValueEquals("
                    + $"(global::TedToolkit.Step21.Entity)({value}), "
                    + $"(global::TedToolkit.Step21.Entity)({entityCode}), {activePairs})";
            }
            else
            {
                equality = instanceEquality
                    ? "false"
                    : "global::TedToolkit.Step21.LogicalValue.False";
            }

            branches.Add($"{variable} => {equality}");
        }

        return $"({selectedCode}).Match({string.Join(", ", branches)})";
    }

    private static Method CreateConstantMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundOpaqueDeclaration declaration,
        ExpressGeneratedTypeResolver resolver)
    {
        var method = CreateMethod(
            ConstantMethodName(declaration.Symbol),
            resolver.Resolve(plan.Schema.Identity, declaration.DeclaredType!).DataType);
        var temporaryOrdinal = 0;
        Func<string, string> allocateTemporaryName = prefix => prefix
            + (temporaryOrdinal++).ToString(CultureInfo.InvariantCulture);
        AddPopulationParameter(method);
        AddValidationContextParameters(method, plan);
        var expression = plan.GetExpression(
            plan.Analysis.GetDeclaration(declaration).RequiredChild("expression"));
        var generated = ExpressExpressionEmitter.Emit(
            expression,
            CreateContext(
                plan,
                selfExpression: null,
                "entities",
                allocateTemporaryName: allocateTemporaryName));
        var value = CopyAggregateValueForAssignment(
            resolver,
            declaration.DeclaredType!,
            expression,
            generated.Code,
            expression.Type.CanBeIndeterminate,
            allocateTemporaryName,
            copyValue: expression.Kind != ExpressExpressionKind.AggregateInitializer);
        method.AddStatement(new CustomExpression(value).Return);
        AddSummary(method, $"Evaluates reachable EXPRESS constant {declaration.Name}.");
        return method;
    }

    private static Method CreateFunctionMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundOpaqueDeclaration declaration,
        ExpressGeneratedTypeResolver resolver)
    {
        var resultGenericLabels = ExpressTypeAnalysis.GenericTypeLabels([declaration.DeclaredType!,]);
        var returnType = resultGenericLabels.Count == 0
            ? resolver.Resolve(plan.Schema.Identity, declaration.DeclaredType!).DataType
            : new DataType(ExpressExpressionEmitter.BoundTypeName(declaration.DeclaredType!));
        var canReturnIndeterminate = plan.Schema.IndeterminateFunctions.Contains(declaration.Symbol);
        if (canReturnIndeterminate)
        {
            returnType = returnType.Null;
        }

        var method = CreateMethod(
            FunctionMethodName(plan, declaration.Symbol),
            returnType);
        var temporaryOrdinal = 0;
        Func<string, string> allocateTemporaryName = prefix => prefix
            + (temporaryOrdinal++).ToString(CultureInfo.InvariantCulture);
        var lexicalNames = new Dictionary<string, (string Code, ExpressBoundType Type)>(
            StringComparer.OrdinalIgnoreCase);
        var formalTypes = new List<ExpressBoundType>();
        var declarationRule = plan.Analysis.GetDeclaration(declaration);
        var head = declarationRule.RequiredChild("functionHead");
        var copiedParameters = new List<(ExpressBoundName Name, string ParameterCode, string LocalCode)>();
        foreach (var formal in head.ChildRules("formalParameter"))
        {
            foreach (var parameter in formal.ChildRules("parameterId"))
            {
                var name = parameter.Identifier!;
                var boundName = plan.Schema.LexicalNames
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                        && SameStart(candidate.Span, parameter.Span))
                    .Distinct()
                    .Single();
                var generatedName = ParameterName(name);
                var lexicalName = generatedName;
                if (resolver.GetAggregateType(boundName.Type!) is { Kind: ExpressAggregateKind.List, }
                    && IsMutatedByListProcedure(plan, declarationRule, boundName))
                {
                    lexicalName = "__mutableParameter_" + ExpressEntityProjection.ToPascalCase(name);
                    copiedParameters.Add((boundName, generatedName, lexicalName));
                }

                lexicalNames.Add(name, (lexicalName, boundName.Type!));
                formalTypes.Add(boundName.Type!);
                DataType parameterType;
                if (boundName.Type is ExpressBoundAggregateType aggregateParameter)
                {
                    parameterType = new(
                        ExpressExpressionEmitter.AggregateInterfaceTypeName(aggregateParameter));
                }
                else
                {
                    parameterType = resolver.IsSupported(boundName.Type!)
                        ? resolver.Resolve(plan.Schema.Identity, boundName.Type!).DataType
                        : new DataType(ExpressExpressionEmitter.BoundTypeName(boundName.Type!));
                }

                method.AddParameter(SourceComposer.Parameter(parameterType, generatedName));
            }
        }

        AddPopulationParameter(method);
        AddValidationContextParameters(method, plan);
        foreach (var parameter in copiedParameters)
        {
            method.AddStatement(new VariableExpression(
                    new DataType(ExpressExpressionEmitter.BoundTypeName(parameter.Name.Type!)),
                    parameter.LocalCode)
                .AddDefault(new CustomExpression(CopyAggregateParameterValue(
                    resolver,
                    parameter.Name.Type!,
                    parameter.ParameterCode))));
        }

        var determinateLexicals = new HashSet<ExpressBoundName>();
        var localTypes = new List<ExpressBoundType>();
        var algorithmHead = declarationRule.RequiredChild("algorithmHead");
        foreach (var local in algorithmHead.ChildRules()
                     .Where(child => child.Role is "constantDecl" or "localDecl")
                     .SelectMany(declarationGroup => declarationGroup.ChildRules(
                         declarationGroup.Role == "constantDecl" ? "constantBody" : "localVariable")))
        {
            var isConstant = local.Role == "constantBody";
            foreach (var variable in isConstant ? [local,] : local.ChildRules("variableId"))
            {
                var boundName = plan.Schema.LexicalNames
                    .Where(candidate => candidate.Kind == (isConstant
                            ? ExpressBoundNameKind.Constant
                            : ExpressBoundNameKind.Variable)
                        && SameStart(candidate.Span, variable.Span))
                    .Distinct()
                    .SingleOrDefault();
                if (boundName?.Type is null)
                {
                    continue;
                }

                var generatedName = $"__local_{ExpressEntityProjection.ToPascalCase(boundName.Name)}";
                lexicalNames.Add(boundName.Name, (generatedName, boundName.Type));
                localTypes.Add(boundName.Type);
                var variableType = new DataType(
                    ExpressExpressionEmitter.BoundTypeName(boundName.Type)
                    + (plan.Schema.IndeterminateLocals.Contains(boundName) ? "?" : ""));
                var variableExpression = new VariableExpression(variableType, generatedName);
                if (local.ChildRules("expression").SingleOrDefault() is { } initializer)
                {
                    var initializerExpression = plan.GetExpression(initializer);
                    var generated = ExpressExpressionEmitter.Emit(
                        initializerExpression,
                        CreateContext(
                            plan,
                            selfExpression: null,
                            "entities",
                            lexicalNames,
                            allocateTemporaryName: allocateTemporaryName));
                    var initializerCode = generated.Code;
                    if (boundName.Type is ExpressBoundNamedType
                        { Declaration.Kind: not ExpressDeclarationKind.Entity, }
                        && initializerExpression.Kind != ExpressExpressionKind.Indeterminate)
                    {
                        var definedTypes = new List<ExpressBoundNamedType>();
                        var underlyingTarget = boundName.Type;
                        while (underlyingTarget is ExpressBoundNamedType definedType
                               && definedType.Declaration.Kind != ExpressDeclarationKind.Entity)
                        {
                            definedTypes.Add(definedType);
                            underlyingTarget = plan.Resolver.GetDefinedType(
                                definedType.Declaration).UnderlyingType;
                        }

                        var actualNominal = initializerExpression.Type.DeclaredType
                            as ExpressBoundNamedType;
                        var primitiveActual = actualNominal is null;
                        var sameDefinedActual = definedTypes.Count > 0
                            && actualNominal is not null
                            && ReferenceEquals(
                                actualNominal.Declaration,
                                definedTypes[0].Declaration);
                        var exactScalar = underlyingTarget is ExpressBoundScalarType targetScalar
                            && targetScalar.Kind switch
                            {
                                ExpressScalarKind.Binary =>
                                    initializerExpression.Type.Kind == ExpressExpressionTypeKind.Binary,
                                ExpressScalarKind.Boolean =>
                                    initializerExpression.Type.Kind == ExpressExpressionTypeKind.Boolean,
                                ExpressScalarKind.Integer =>
                                    initializerExpression.Type.Kind == ExpressExpressionTypeKind.Integer,
                                ExpressScalarKind.Logical =>
                                    initializerExpression.Type.Kind == ExpressExpressionTypeKind.Logical,
                                ExpressScalarKind.Number =>
                                    initializerExpression.Type.Kind == ExpressExpressionTypeKind.Number,
                                ExpressScalarKind.Real =>
                                    initializerExpression.Type.Kind == ExpressExpressionTypeKind.Real,
                                ExpressScalarKind.String =>
                                    initializerExpression.Type.Kind == ExpressExpressionTypeKind.String,
                                _ => false,
                            };
                        if (exactScalar)
                        {
                            var sourceCode = initializerCode;
                            var presentValue = initializerExpression.Type.CanBeIndeterminate
                                ? allocateTemporaryName("__expressInitializedDefinedValue")
                                : null;
                            initializerCode = presentValue ?? initializerCode;
                            for (var definedIndex = definedTypes.Count - 1;
                                 definedIndex >= 0;
                                 definedIndex--)
                            {
                                initializerCode = "new "
                                    + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                                    + $"({initializerCode})";
                            }

                            if (presentValue is not null)
                            {
                                initializerCode = $"(({sourceCode}) is {{ }} {presentValue} ? "
                                    + initializerCode
                                    + " : ("
                                    + ExpressExpressionEmitter.BoundTypeName(boundName.Type)
                                    + "?)null)";
                            }
                        }
                    }

                    if (boundName.Type is ExpressBoundNamedType
                        { Declaration.Kind: ExpressDeclarationKind.Entity, } initializerTargetEntity
                        && initializerExpression.Type.DeclaredType is ExpressBoundNamedType initializerSelect
                        && initializerSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                        && plan.Resolver.GetDefinedType(initializerSelect.Declaration).UnderlyingType
                            is ExpressBoundSelectType)
                    {
                        initializerCode = ResolveSelectToEntityValue(
                            plan,
                            initializerExpression,
                            initializerCode,
                            initializerTargetEntity,
                            allocateTemporaryName);
                    }
                    else if (boundName.Type is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } initializerSubtype
                             && initializerExpression.Type.DeclaredType is ExpressBoundNamedType
                             { Declaration.Kind: ExpressDeclarationKind.Entity, } initializerSupertype
                             && !ReferenceEquals(
                                 initializerSubtype.Declaration,
                                 initializerSupertype.Declaration)
                             && plan.EntityProjections.Single(projection =>
                                     projection.Entity.Symbol == initializerSubtype.Declaration)
                                 .PhysicalComponents.Any(component =>
                                     component.Symbol == initializerSupertype.Declaration))
                    {
                        var targetName = ExpressExpressionEmitter.BoundTypeName(initializerSubtype);
                        var typedValue = allocateTemporaryName("__expressInitializedSubtype");
                        initializerCode = $"(({initializerCode}) is {targetName} {typedValue} ? "
                            + $"{typedValue} : ({targetName}?)null)";
                    }

                    if (boundName.Type is ExpressBoundScalarType
                        { Kind: ExpressScalarKind.Logical, }
                        && initializerExpression.Type.Kind == ExpressExpressionTypeKind.Boolean)
                    {
                        initializerCode = ExpressExpressionEmitter.AsLogical(
                            initializerExpression,
                            initializerCode);
                    }
                    else if (boundName.Type is ExpressBoundScalarType
                    { Kind: ExpressScalarKind.Real, }
                             && initializerExpression.Type.Kind == ExpressExpressionTypeKind.Integer)
                    {
                        initializerCode = ExpressExpressionEmitter.PromoteNumeric(
                            initializerExpression,
                            initializerCode,
                            ExpressExpressionTypeKind.Real);
                    }

                    if (boundName.Type is ExpressBoundNamedType
                        { Declaration.Kind: not ExpressDeclarationKind.Entity, } initializerSelectTarget
                        && initializerExpression.Type.DeclaredType is ExpressBoundNamedType
                        { Declaration.Kind: ExpressDeclarationKind.Entity, } initializerEntity
                        && ResolveEntityToSelectValue(
                            plan,
                            initializerSelectTarget,
                            initializerEntity.Declaration,
                            initializerCode) is { } selectedInitializer)
                    {
                        initializerCode = selectedInitializer;
                    }

                    initializerCode = CopyAggregateValueForAssignment(
                        resolver,
                        boundName.Type,
                        initializerExpression,
                        initializerCode,
                        initializerExpression.Type.CanBeIndeterminate,
                        allocateTemporaryName,
                        copyValue: initializerExpression.Kind != ExpressExpressionKind.AggregateInitializer);

                    variableExpression.AddDefault(new CustomExpression(initializerCode));
                    if (!boundName.IsOptional && !initializerExpression.Type.CanBeIndeterminate)
                    {
                        determinateLexicals.Add(boundName);
                    }
                }
                else if (plan.Schema.IndeterminateLocals.Contains(boundName))
                {
                    variableExpression.AddDefault(new CustomExpression("null"));
                }

                method.AddStatement(variableExpression);
            }
        }

        foreach (var label in ExpressTypeAnalysis.GenericTypeLabels(
                     formalTypes.Concat([declaration.DeclaredType!,]).Concat(localTypes)))
        {
            method.AddTypeParameter(SourceComposer.TypeParameter(
                "T" + ExpressEntityProjection.ToPascalCase(label)));
        }

        var sizeAliases = new Dictionary<ExpressBoundName, ExpressBoundName>();
        var scalarNarrowings = new Dictionary<
            string,
            (string StorageCode, string Code, ExpressBoundType Type)>(
            StringComparer.OrdinalIgnoreCase);
        var fallsThrough = true;
        foreach (var statement in declarationRule.ChildRules("stmt"))
        {
            if (!fallsThrough)
            {
                break;
            }

            fallsThrough = EmitFunctionStatement(
                plan,
                statement,
                declaration.DeclaredType,
                canReturnIndeterminate,
                method,
                lexicalNames,
                allocateTemporaryName,
                sizeAliases: sizeAliases,
                determinateLexicals: determinateLexicals,
                scalarNarrowings: scalarNarrowings);
        }

        if (fallsThrough)
        {
            if (canReturnIndeterminate)
            {
                var indeterminateResult = declaration.DeclaredType is ExpressBoundScalarType
                { Kind: ExpressScalarKind.Logical, }
                    ? "global::TedToolkit.Step21.LogicalValue.Unknown"
                    : "null";
                method.AddStatement(new CustomExpression(indeterminateResult).Return);
            }
            else
            {
                method.AddStatement(new CustomExpression(
                    "throw new global::System.InvalidOperationException(\"EXPRESS function completed without RETURN.\")"));
            }
        }

        AddSummary(method, $"Evaluates reachable EXPRESS function {declaration.Name}.");
        return method;
    }

    private static Method CreateProcedureMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundOpaqueDeclaration declaration)
    {
        var method = CreateMethod(ProcedureMethodName(plan, declaration.Symbol), DataType.Void);
        var temporaryOrdinal = 0;
        Func<string, string> allocateTemporaryName = prefix => prefix
            + (temporaryOrdinal++).ToString(CultureInfo.InvariantCulture);
        var lexicalNames = new Dictionary<string, (string Code, ExpressBoundType Type)>(
            StringComparer.OrdinalIgnoreCase);
        var declarationRule = plan.Analysis.GetDeclaration(declaration);
        foreach (var parameter in ProcedureParameters(plan, declaration))
        {
            var generatedName = ParameterName(parameter.Name.Name);
            lexicalNames.Add(parameter.Name.Name, (generatedName, parameter.Name.Type!));
            var parameterType = parameter.Name.Type is ExpressBoundAggregateType aggregateParameter
                && !parameter.IsVar
                ? ExpressExpressionEmitter.AggregateInterfaceTypeName(aggregateParameter)
                : ExpressExpressionEmitter.BoundTypeName(parameter.Name.Type!);
            method.AddParameter(SourceComposer.Parameter(
                new DataType((parameter.IsVar ? "ref " : "") + parameterType),
                generatedName));
        }

        AddPopulationParameter(method);
        AddValidationContextParameters(method, plan);
        var determinateLexicals = new HashSet<ExpressBoundName>();
        foreach (var local in declarationRule.RequiredChild("algorithmHead").ChildRules("localDecl")
                     .SelectMany(group => group.ChildRules("localVariable"))
                     .SelectMany(group => group.ChildRules("variableId")))
        {
            var boundName = plan.Schema.LexicalNames
                .Where(candidate => candidate.Kind == ExpressBoundNameKind.Variable
                    && SameStart(candidate.Span, local.Span))
                .Distinct()
                .Single();
            var generatedName = "__local_" + ExpressEntityProjection.ToPascalCase(boundName.Name);
            lexicalNames.Add(boundName.Name, (generatedName, boundName.Type!));
            var variable = new VariableExpression(
                new DataType(ExpressExpressionEmitter.BoundTypeName(boundName.Type!)
                    + (plan.Schema.IndeterminateLocals.Contains(boundName) ? "?" : "")),
                generatedName);
            if (plan.Schema.IndeterminateLocals.Contains(boundName))
            {
                variable.AddDefault(new CustomExpression("null"));
            }

            method.AddStatement(variable);
        }

        var sizeAliases = new Dictionary<ExpressBoundName, ExpressBoundName>();
        var scalarNarrowings = new Dictionary<
            string,
            (string StorageCode, string Code, ExpressBoundType Type)>(
            StringComparer.OrdinalIgnoreCase);
        var fallsThrough = true;
        foreach (var statement in declarationRule.ChildRules("stmt"))
        {
            if (!fallsThrough)
            {
                break;
            }

            fallsThrough = EmitFunctionStatement(
                plan,
                statement,
                functionResultType: null,
                canReturnIndeterminate: false,
                method,
                lexicalNames,
                allocateTemporaryName,
                sizeAliases: sizeAliases,
                determinateLexicals: determinateLexicals,
                scalarNarrowings: scalarNarrowings);
        }

        AddSummary(method, $"Executes reachable EXPRESS procedure {declaration.Name}.");
        return method;
    }

    /// <summary>
    /// Emits the executable algorithm that precedes a validation-root RULE WHERE clause.
    /// </summary>
    /// <param name="plan">The validated reachable-rule plan.</param>
    /// <param name="declaration">The RULE declaration.</param>
    /// <param name="owner">The validation method receiving the generated statements.</param>
    /// <param name="lexicalNames">The RULE population names, extended with its local names.</param>
    /// <param name="allocateTemporaryName">Allocates validation-method-unique temporary names.</param>
    internal static void EmitRuleAlgorithm(
        ExpressReachableRulePlan plan,
        ExpressBoundDeclaration declaration,
        IStatementOwner owner,
        Dictionary<string, (string Code, ExpressBoundType Type)> lexicalNames,
        Func<string, string> allocateTemporaryName)
    {
        var declarationRule = plan.GetSemanticDeclaration(declaration);
        var determinateLexicals = new HashSet<ExpressBoundName>();
        var localPrefix = "__rule_"
            + ExpressEntityProjection.ToPascalCase(declaration.Name)
            + "_local_";
        foreach (var local in declarationRule.RequiredChild("algorithmHead").ChildRules()
                     .Where(child => child.Role is "constantDecl" or "localDecl")
                     .SelectMany(group => group.ChildRules(
                         group.Role == "constantDecl" ? "constantBody" : "localVariable")))
        {
            var isConstant = local.Role == "constantBody";
            foreach (var variable in isConstant ? [local,] : local.ChildRules("variableId"))
            {
                var boundName = plan.Schema.LexicalNames
                    .Where(candidate => candidate.Kind == (isConstant
                            ? ExpressBoundNameKind.Constant
                            : ExpressBoundNameKind.Variable)
                        && SameStart(candidate.Span, variable.Span))
                    .Distinct()
                    .SingleOrDefault();
                if (boundName?.Type is null)
                {
                    continue;
                }

                var generatedName = localPrefix + ExpressEntityProjection.ToPascalCase(boundName.Name);
                lexicalNames.Add(boundName.Name, (generatedName, boundName.Type));
                var variableExpression = new VariableExpression(
                    new DataType(
                        ExpressExpressionEmitter.BoundTypeName(boundName.Type)
                        + (plan.Schema.IndeterminateLocals.Contains(boundName) ? "?" : "")),
                    generatedName);
                if (local.ChildRules("expression").SingleOrDefault() is { } initializer)
                {
                    var initializerExpression = plan.GetExpression(initializer);
                    var initializerCode = ExpressExpressionEmitter.Emit(
                        initializerExpression,
                        CreateContext(
                            plan,
                            selfExpression: null,
                            "entities",
                            lexicalNames,
                            allocateTemporaryName: allocateTemporaryName)).Code;
                    if (boundName.Type is ExpressBoundScalarType
                        { Kind: ExpressScalarKind.Logical, }
                        && initializerExpression.Type.Kind == ExpressExpressionTypeKind.Boolean)
                    {
                        initializerCode = ExpressExpressionEmitter.AsLogical(
                            initializerExpression,
                            initializerCode);
                    }
                    else if (boundName.Type is ExpressBoundScalarType
                    { Kind: ExpressScalarKind.Real, }
                             && initializerExpression.Type.Kind == ExpressExpressionTypeKind.Integer)
                    {
                        initializerCode = ExpressExpressionEmitter.PromoteNumeric(
                            initializerExpression,
                            initializerCode,
                            ExpressExpressionTypeKind.Real);
                    }

                    initializerCode = CopyAggregateValueForAssignment(
                        plan.Resolver,
                        boundName.Type,
                        initializerExpression,
                        initializerCode,
                        initializerExpression.Type.CanBeIndeterminate,
                        allocateTemporaryName,
                        copyValue: initializerExpression.Kind != ExpressExpressionKind.AggregateInitializer);
                    variableExpression.AddDefault(new CustomExpression(initializerCode));
                    if (!boundName.IsOptional && !initializerExpression.Type.CanBeIndeterminate)
                    {
                        determinateLexicals.Add(boundName);
                    }
                }
                else if (plan.Schema.IndeterminateLocals.Contains(boundName))
                {
                    variableExpression.AddDefault(new CustomExpression("null"));
                }

                owner.AddStatement(variableExpression);
            }
        }

        var sizeAliases = new Dictionary<ExpressBoundName, ExpressBoundName>();
        var scalarNarrowings = new Dictionary<
            string,
            (string StorageCode, string Code, ExpressBoundType Type)>(
            StringComparer.OrdinalIgnoreCase);
        var fallsThrough = true;
        foreach (var statement in declarationRule.ChildRules("stmt"))
        {
            if (!fallsThrough)
            {
                break;
            }

            fallsThrough = EmitFunctionStatement(
                plan,
                statement,
                functionResultType: null,
                canReturnIndeterminate: false,
                owner,
                lexicalNames,
                allocateTemporaryName,
                sizeAliases: sizeAliases,
                determinateLexicals: determinateLexicals,
                scalarNarrowings: scalarNarrowings);
        }
    }

    private static string CopyAggregateValueForAssignment(
        ExpressGeneratedTypeResolver resolver,
        ExpressBoundType targetType,
        ExpressBoundExpression valueExpression,
        string valueCode,
        bool valueCanBeIndeterminate,
        Func<string, string> allocateTemporaryName,
        bool copyValue = true)
    {
        if (valueExpression.Kind == ExpressExpressionKind.Indeterminate
            || resolver.GetAggregateType(targetType) is not { } targetAggregate
            || targetAggregate.Kind is not (ExpressAggregateKind.Bag or ExpressAggregateKind.List or ExpressAggregateKind.Set)
            || valueExpression.Type.DeclaredType is not { } sourceType
            || resolver.GetAggregateType(sourceType) is not { } sourceAggregate
            || sourceAggregate.Kind != targetAggregate.Kind
            || !StringComparer.Ordinal.Equals(
                ExpressExpressionEmitter.BoundTypeName(sourceAggregate.ElementType),
                ExpressExpressionEmitter.BoundTypeName(targetAggregate.ElementType)))
        {
            return valueCode;
        }

        var targetName = ExpressExpressionEmitter.BoundTypeName(targetType);
        var aggregateName = ExpressExpressionEmitter.BoundTypeName(targetAggregate);
        var presentAggregate = valueCanBeIndeterminate
            ? allocateTemporaryName("__expressAssignedAggregate")
            : null;
        var result = presentAggregate ?? valueCode;
        if (copyValue)
        {
            result = $"({aggregateName})[..{result}]";
        }

        var wrappers = ResolveTransparentDefinedWrappers(resolver, ref targetType);

        for (var index = wrappers.Count - 1; index >= 0; index--)
        {
            result = $"new {ExpressExpressionEmitter.BoundTypeName(wrappers[index])}({result})";
        }

        return presentAggregate is null
            ? result
            : $"(({valueCode}) is {{ }} {presentAggregate} ? {result} : ({targetName}?)null)";
    }

    private static bool IsMutatedByListProcedure(
        ExpressReachableRulePlan plan,
        ExpressSemanticRule declaration,
        ExpressBoundName parameter)
    {
        return declaration.DescendantsAndSelf()
            .Where(candidate => candidate.Role == "procedureCallStmt"
                && candidate.ChildRules("builtInProcedure").SingleOrDefault()?.SourceText.ToUpperInvariant()
                    is "INSERT" or "REMOVE")
            .SelectMany(candidate => candidate.RequiredChild("actualParameterList").ChildRules("parameter").Take(1))
            .Select(candidate => plan.GetExpression(candidate.RequiredChild("expression")).Reference)
            .Any(reference => ReferenceEquals(reference, parameter));
    }

    private static string CopyAggregateParameterValue(
        ExpressGeneratedTypeResolver resolver,
        ExpressBoundType parameterType,
        string parameterCode)
    {
        var aggregate = resolver.GetAggregateType(parameterType)
            ?? throw new InvalidOperationException("A copied EXPRESS parameter must be aggregate-valued.");
        var result = $"({ExpressExpressionEmitter.BoundTypeName(aggregate)})[..{parameterCode}]";
        var wrappers = ResolveTransparentDefinedWrappers(resolver, ref parameterType);

        for (var index = wrappers.Count - 1; index >= 0; index--)
        {
            result = $"new {ExpressExpressionEmitter.BoundTypeName(wrappers[index])}({result})";
        }

        return result;
    }

    private static List<ExpressBoundNamedType> ResolveTransparentDefinedWrappers(
        ExpressGeneratedTypeResolver resolver,
        ref ExpressBoundType type)
    {
        var wrappers = new List<ExpressBoundNamedType>();
        while (type is ExpressBoundNamedType named
               && named.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            var underlying = resolver.GetDefinedType(named.Declaration).UnderlyingType;
            if (underlying is ExpressBoundEnumerationType or ExpressBoundSelectType)
            {
                type = underlying;
                break;
            }

            wrappers.Add(named);
            type = underlying;
        }

        return wrappers;
    }

    private static List<(ExpressBoundName Name, bool IsVar)> ProcedureParameters(
        ExpressReachableRulePlan plan,
        ExpressBoundOpaqueDeclaration declaration)
    {
        var head = plan.Analysis.GetDeclaration(declaration).RequiredChild("procedureHead");
        var result = new List<(ExpressBoundName Name, bool IsVar)>();
        var cursor = 0;
        foreach (var formal in head.ChildRules("formalParameter"))
        {
            var formalIndex = head.SourceText.IndexOf(formal.SourceText, cursor, StringComparison.Ordinal);
            if (formalIndex < 0)
            {
                throw new InvalidOperationException("An EXPRESS procedure parameter has no source position.");
            }

            var isVar = head.SourceText.IndexOf("VAR", cursor, formalIndex - cursor,
                StringComparison.OrdinalIgnoreCase) >= 0;
            cursor = formalIndex + formal.SourceText.Length;
            foreach (var parameter in formal.ChildRules("parameterId"))
            {
                var boundName = plan.Schema.LexicalNames
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                        && SameStart(candidate.Span, parameter.Span))
                    .Distinct()
                    .Single();
                result.Add((boundName, isVar));
            }
        }

        return result;
    }

    private static bool EmitFunctionStatement(
        ExpressReachableRulePlan plan,
        ExpressSemanticRule statement,
        ExpressBoundType? functionResultType,
        bool canReturnIndeterminate,
        IStatementOwner owner,
        Dictionary<string, (string Code, ExpressBoundType Type)> lexicalNames,
        Func<string, string> allocateTemporaryName,
        List<KeyValuePair<ExpressBoundName, ExpressBoundName>>? safeIndices = null,
        IDictionary<ExpressBoundName, ExpressBoundName>? sizeAliases = null,
        Dictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings = null,
        List<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>? pathNarrowings = null,
        ISet<ExpressBoundName>? determinateLexicals = null,
        List<KeyValuePair<ExpressBoundExpression, ExpressBoundName>>? safeIndexPaths = null,
        Dictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>?
            scalarNarrowings = null,
        LoopTransfers? loopTransfers = null)
    {
        var operation = statement.Role == "stmt"
            ? statement.ChildRules().Single()
            : statement;
        if (operation.Role == "nullStmt")
        {
            return true;
        }

        if (operation.Role == "aliasStmt")
        {
            var sourceSyntax = operation.RequiredChild("generalRef");
            var target = plan.Schema.NameReferences
                .Where(reference => SameStart(reference.Span, sourceSyntax.Span))
                .Select(reference => reference.Target)
                .Single();
            if (!lexicalNames.TryGetValue(target.Name, out var lexicalTarget)
                || target.Type is null)
            {
                throw new InvalidOperationException("An EXPRESS ALIAS target has no generated lexical value.");
            }

            var targetCode = lexicalTarget.Code;
            var targetType = target.Type;
            foreach (var qualifier in operation.ChildRules("qualifier"))
            {
                if (qualifier.ChildRules("groupQualifier").SingleOrDefault() is { } groupSyntax)
                {
                    var groupReference = plan.Schema.NameReferences
                        .Where(reference => SameStart(
                                reference.Span,
                                groupSyntax.RequiredChild("entityRef").Span)
                            && reference.Target.Kind == ExpressBoundNameKind.Entity)
                        .Select(reference => reference.Target.SchemaDeclaration)
                        .OfType<ExpressBoundSymbol>()
                        .Distinct()
                        .Single();
                    var groupType = new ExpressBoundNamedType(groupReference, groupSyntax.Span);
                    targetCode = $"(({ExpressExpressionEmitter.BoundTypeName(groupType)})({targetCode}))";
                    targetType = groupType;
                    continue;
                }

                if (qualifier.ChildRules("attributeQualifier").SingleOrDefault() is { } attributeSyntax)
                {
                    var sourceEntity = targetType as ExpressBoundNamedType
                        ?? throw new InvalidOperationException(
                            "An EXPRESS ALIAS attribute target requires an entity carrier.");
                    var attribute = plan.GetReferencedAttribute(attributeSyntax);
                    targetCode = $"(({ExpressExpressionEmitter.BoundTypeName(sourceEntity)})({targetCode}))."
                        + ExpressEntityProjection.ToPascalCase(attribute.Name);
                    targetType = attribute.Type;
                    continue;
                }

                throw new InvalidOperationException(
                    "An EXPRESS ALIAS qualifier passed shape validation without a static generator.");
            }

            var aliasSyntax = operation.RequiredChild("variableId");
            var alias = plan.Schema.LexicalNames
                .Where(candidate => candidate.Kind == ExpressBoundNameKind.Alias
                    && SameStart(candidate.Span, aliasSyntax.Span))
                .Distinct()
                .Single();
            var nestedNames = lexicalNames.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            nestedNames[alias.Name] = (targetCode, targetType);
            var fallsThrough = true;
            foreach (var nested in operation.ChildRules("stmt"))
            {
                if (!fallsThrough)
                {
                    break;
                }

                fallsThrough = EmitFunctionStatement(
                    plan,
                    nested,
                    functionResultType,
                    canReturnIndeterminate,
                    owner,
                    nestedNames,
                    allocateTemporaryName,
                    safeIndices,
                    sizeAliases,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings,
                    loopTransfers);
            }

            if (fallsThrough)
            {
                safeIndices?.Clear();
                sizeAliases?.Clear();
                selectNarrowings?.Clear();
                pathNarrowings?.Clear();
                determinateLexicals?.Clear();
                safeIndexPaths?.Clear();
                scalarNarrowings?.Clear();
            }

            return fallsThrough;
        }

        if (operation.Role is "escapeStmt" or "skipStmt")
        {
            if (loopTransfers is null)
            {
                throw new InvalidOperationException("A loop transfer requires an enclosing REPEAT.");
            }

            var facts = new LoopFlowFacts(
                safeIndices, sizeAliases, selectNarrowings, pathNarrowings,
                determinateLexicals, safeIndexPaths, scalarNarrowings);
            var isSkip = operation.Role == "skipStmt";
            (isSkip ? loopTransfers.SkipPaths : loopTransfers.ExitPaths).Add(facts);
            owner.AddStatement(new Custom((ref SourceBuilder source) =>
                source.AppendLine(isSkip ? $"goto {loopTransfers.SkipLabel};" : "break;")));
            return false;
        }

        if (operation.Role == "procedureCallStmt")
        {
            var arguments = operation.RequiredChild("actualParameterList").ChildRules("parameter")
                .Select(parameter => plan.GetExpression(parameter.RequiredChild("expression")))
                .ToArray();
            var context = CreateContext(
                plan,
                selfExpression: null,
                "entities",
                lexicalNames,
                safeIndices,
                allocateTemporaryName,
                selectNarrowings,
                pathNarrowings,
                determinateLexicals,
                safeIndexPaths,
                scalarNarrowings);
            if (operation.ChildRules("procedureRef").SingleOrDefault() is { } procedureReference)
            {
                EmitProcedureCall(plan, procedureReference, arguments, context, lexicalNames, owner);
                safeIndices?.Clear();
                sizeAliases?.Clear();
                selectNarrowings?.Clear();
                pathNarrowings?.Clear();
                safeIndexPaths?.Clear();
                scalarNarrowings?.Clear();
                return true;
            }

            var fallsThrough = EmitListProcedure(
                plan,
                operation.RequiredChild("builtInProcedure").SourceText,
                arguments,
                context,
                owner,
                functionResultType,
                canReturnIndeterminate);
            var target = arguments[0].Reference!;
            safeIndices?.RemoveAll(pair => ReferenceEquals(pair.Key, target));
            safeIndexPaths?.Clear();
            pathNarrowings?.Clear();
            if (sizeAliases is not null)
            {
                foreach (var alias in sizeAliases.Where(pair => ReferenceEquals(pair.Value, target)).ToArray())
                {
                    sizeAliases.Remove(alias.Key);
                }
            }

            return fallsThrough;
        }

        if (operation.Role == "assignmentStmt")
        {
            var targetSyntax = operation.RequiredChild("generalRef");
            var target = plan.Schema.NameReferences
                .Where(reference => SameStart(reference.Span, targetSyntax.Span))
                .Select(reference => reference.Target)
                .Single();
            var hasQualifier = operation.ChildRules("qualifier").Any();
            var targetWasDeterminate = !hasQualifier
                && determinateLexicals?.Contains(target) == true;
            var canEstablishDeterminacy = target.Type is ExpressBoundScalarType
                || targetWasDeterminate;
            var targetCode = lexicalNames[target.Name].Code;
            var targetType = target.Type!;
            var targetIsOptional = target.IsOptional;
            string? optionalUnsetCode = null;
            string? assignedEntityCode = null;
            ExpressBoundNamedType? assignedEntityType = null;
            ExpressBoundAttribute? assignedAttribute = null;
            var assignedMemberSuffix = "";
            var assignedAggregateDepth = 0;
            foreach (var qualifier in operation.ChildRules("qualifier"))
            {
                if (qualifier.ChildRules("groupQualifier").SingleOrDefault() is { } groupSyntax)
                {
                    var groupReference = plan.Schema.NameReferences
                        .Where(reference => SameStart(
                                reference.Span,
                                groupSyntax.RequiredChild("entityRef").Span)
                            && reference.Target.Kind == ExpressBoundNameKind.Entity)
                        .Select(reference => reference.Target.SchemaDeclaration)
                        .OfType<ExpressBoundSymbol>()
                        .Distinct()
                        .Single();
                    var groupType = new ExpressBoundNamedType(groupReference, groupSyntax.Span);
                    targetCode = $"(({ExpressExpressionEmitter.BoundTypeName(groupType)})({targetCode}))";
                    targetType = groupType;
                    targetIsOptional = false;
                    optionalUnsetCode = null;
                    assignedEntityCode = null;
                    assignedEntityType = null;
                    assignedAttribute = null;
                    assignedMemberSuffix = "";
                    assignedAggregateDepth = 0;
                    continue;
                }

                if (qualifier.ChildRules("attributeQualifier").SingleOrDefault() is { } attributeSyntax)
                {
                    var sourceEntity = (ExpressBoundNamedType)targetType;
                    var attribute = plan.GetReferencedAttribute(attributeSyntax);
                    assignedEntityCode = targetCode;
                    assignedEntityType = sourceEntity;
                    assignedAttribute = attribute;
                    assignedMemberSuffix = "";
                    assignedAggregateDepth = 0;
                    targetCode = $"(({ExpressExpressionEmitter.BoundTypeName(sourceEntity)})({targetCode}))."
                        + ExpressEntityProjection.ToPascalCase(attribute.Name);
                    targetType = attribute.Type;
                    targetIsOptional = attribute.IsOptional;
                    optionalUnsetCode = null;
                    continue;
                }

                var indexSyntax = qualifier.RequiredChild("indexQualifier")
                    .RequiredChild("index1")
                    .RequiredChild("index")
                    .RequiredChild("numericExpression");
                var index = plan.GetExpression(indexSyntax);
                var emittedIndex = ExpressExpressionEmitter.Emit(
                    index,
                    CreateContext(
                        plan,
                        selfExpression: null,
                        "entities",
                        lexicalNames,
                        safeIndices,
                        allocateTemporaryName,
                        selectNarrowings,
                        pathNarrowings,
                        determinateLexicals,
                        safeIndexPaths,
                        scalarNarrowings));
                var indexCode = index.Type.Kind == ExpressExpressionTypeKind.Number
                    ? $"checked((int)({emittedIndex.Code}).ToIntegerTruncated())"
                    : $"checked((int)({emittedIndex.Code}))";
                var aggregate = (ExpressBoundAggregateType)targetType;
                var aggregateCode = targetCode;
                var indexSuffix = aggregate.Kind == ExpressAggregateKind.Array
                    ? $"[{indexCode}]"
                    : $"[{indexCode} - 1]";
                targetCode += indexSuffix;
                if (assignedEntityCode is not null)
                {
                    assignedMemberSuffix += indexSuffix;
                    assignedAggregateDepth++;
                }

                targetType = aggregate.ElementType;
                targetIsOptional = aggregate.IsOptional;
                optionalUnsetCode = aggregate.Kind == ExpressAggregateKind.Array && aggregate.IsOptional
                    ? $"{aggregateCode}.Unset({indexCode})"
                    : null;
            }

            var valueExpression = plan.GetExpression(operation.RequiredChild("expression"));
            var valueCanBeIndeterminate = CanEmitIndeterminate(plan, valueExpression)
                || (valueExpression.Operation is "+" or "-" or "*" or "/"
                    && valueExpression.DescendantsAndSelf().Any(candidate =>
                        candidate.Kind == ExpressExpressionKind.IndexQualifier))
                || (valueExpression.Operation is "+" or "-" or "*" or "/"
                    && valueExpression.Children.Any(child =>
                        child.Type.DeclaredType is ExpressBoundNamedType dynamicSelect
                        && dynamicSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                        && plan.Resolver.GetDefinedType(dynamicSelect.Declaration).UnderlyingType
                            is ExpressBoundSelectType));
            var valueIsKnownDeterminate = !valueCanBeIndeterminate
                || (valueExpression.Kind == ExpressExpressionKind.Reference
                    && valueExpression.Reference is { } valueReference
                    && ((ReferenceEquals(valueReference, target) && targetWasDeterminate)
                        || determinateLexicals?.Contains(valueReference) == true));
            var value = ExpressExpressionEmitter.Emit(
                valueExpression,
                CreateContext(
                    plan,
                    selfExpression: null,
                    "entities",
                    lexicalNames,
                    safeIndices,
                    allocateTemporaryName,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings));
            safeIndexPaths?.Clear();
            if (!hasQualifier)
            {
                determinateLexicals?.Remove(target);
                scalarNarrowings?.Remove(target.Name);

                if (selectNarrowings?.ContainsKey(target) == true
                    && target.Type is ExpressBoundNamedType invalidatedSelect
                    && invalidatedSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(invalidatedSelect.Declaration).UnderlyingType
                        is ExpressBoundSelectType)
                {
                    selectNarrowings[target] = invalidatedSelect.Declaration;
                }
                else
                {
                    selectNarrowings?.Remove(target);
                }

                safeIndices?.RemoveAll(pair => ReferenceEquals(pair.Key, target)
                    || ReferenceEquals(pair.Value, target));
            }

            if (sizeAliases is not null)
            {
                foreach (var alias in sizeAliases
                             .Where(alias => ReferenceEquals(alias.Key, target) || ReferenceEquals(alias.Value, target))
                             .ToArray())
                {
                    sizeAliases.Remove(alias.Key);
                }
            }

            if (hasQualifier)
            {
                if (pathNarrowings is not null)
                {
                    for (var index = pathNarrowings.Count - 1; index >= 0; index--)
                    {
                        var narrowing = pathNarrowings[index];
                        if (narrowing.Key.Type.DeclaredType is ExpressBoundNamedType invalidatedSelect
                            && invalidatedSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                            && plan.Resolver.GetDefinedType(invalidatedSelect.Declaration).UnderlyingType
                                is ExpressBoundSelectType)
                        {
                            pathNarrowings[index] = new(narrowing.Key, invalidatedSelect.Declaration);
                        }
                        else
                        {
                            pathNarrowings.RemoveAt(index);
                        }
                    }
                }
            }
            else if (pathNarrowings is not null)
            {
                for (var index = pathNarrowings.Count - 1; index >= 0; index--)
                {
                    var narrowing = pathNarrowings[index];
                    if (!narrowing.Key.DescendantsAndSelf()
                        .Any(expression => ReferenceEquals(expression.Reference, target)))
                    {
                        continue;
                    }

                    if (narrowing.Key.Type.DeclaredType is ExpressBoundNamedType invalidatedSelect
                        && invalidatedSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                        && plan.Resolver.GetDefinedType(invalidatedSelect.Declaration).UnderlyingType
                            is ExpressBoundSelectType)
                    {
                        pathNarrowings[index] = new(narrowing.Key, invalidatedSelect.Declaration);
                    }
                    else
                    {
                        pathNarrowings.RemoveAt(index);
                    }
                }
            }

            if (sizeAliases is not null
                && !operation.ChildRules("qualifier").Any()
                && target.Kind == ExpressBoundNameKind.Variable
                && valueExpression.Operation == "SIZEOF"
                && valueExpression.Children.Count == 1
                && valueExpression.Children[0].Reference is { Type: ExpressBoundAggregateType, } sourceAggregate)
            {
                sizeAliases.Clear();
                sizeAliases.Add(target, sourceAggregate);
            }

            var valueCode = value.Code;
            var wasAssignedAggregateAdapted = false;
            if (valueExpression.Type.DeclaredType is ExpressBoundGenericType
                && targetType is not ExpressBoundGenericType)
            {
                valueCode = ResolveGenericValueToTarget(
                    plan,
                    targetType,
                    valueCode,
                    allocateTemporaryName("__expressAssignedGeneric"));
                valueCanBeIndeterminate = true;
            }

            if (targetType is ExpressBoundScalarType { Kind: ExpressScalarKind.Number, }
                && valueExpression.Type.Kind == ExpressExpressionTypeKind.Integer
                && valueExpression.Type.DeclaredType is null
                    or ExpressBoundScalarType { Kind: ExpressScalarKind.Integer, })
            {
                valueCode = valueCanBeIndeterminate
                    ? $"global::TedToolkit.Step21.NumberValue.FromInteger({valueCode}.Value)"
                    : $"global::TedToolkit.Step21.NumberValue.FromInteger({valueCode})";
            }

            if (targetType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, } assignedSelect
                && plan.Resolver.GetDefinedType(assignedSelect.Declaration).UnderlyingType
                    is ExpressBoundSelectType
                && valueExpression.Type.DeclaredType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, } assignedNamedSource
                && !ReferenceEquals(assignedSelect.Declaration, assignedNamedSource.Declaration))
            {
                var assignedSelectedValue = plan.Resolver.GetDefinedType(assignedNamedSource.Declaration)
                        .UnderlyingType is ExpressBoundSelectType
                    ? ResolveSelectToSelectValue(
                        plan,
                        assignedSelect,
                        assignedNamedSource,
                        valueCode,
                        allocateTemporaryName("__expressAssignedSelectValue"))
                    : ResolveNamedExpressionToSelectValue(
                        plan,
                        assignedSelect,
                        assignedNamedSource,
                        valueCode,
                        valueCanBeIndeterminate,
                        allocateTemporaryName("__expressAssignedSelectValue"));
                if (assignedSelectedValue is not null)
                {
                    valueCode = assignedSelectedValue;
                }
            }

            if (targetType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, } assignedScalarSelect
                && plan.Resolver.GetDefinedType(assignedScalarSelect.Declaration).UnderlyingType
                    is ExpressBoundSelectType
                && valueExpression.Type.DeclaredType is null or ExpressBoundScalarType
                && ResolveScalarExpressionToSelectValue(
                    plan,
                    assignedScalarSelect,
                    valueExpression,
                    valueCode,
                    valueCanBeIndeterminate,
                    allocateTemporaryName("__expressAssignedScalarValue")) is { } assignedScalarValue)
            {
                valueCode = assignedScalarValue;
            }

            if (targetType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } projectedTargetEntity
                && valueExpression.Type.DeclaredType is ExpressBoundNamedType sourceName
                && sourceName.Declaration.Kind != ExpressDeclarationKind.Entity
                && plan.Resolver.GetDefinedType(sourceName.Declaration).UnderlyingType
                    is ExpressBoundSelectType)
            {
                valueCode = ResolveSelectToEntityValue(
                    plan,
                    valueExpression,
                    valueCode,
                    projectedTargetEntity,
                    allocateTemporaryName);
            }

            if (targetType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, } projectedNominalTarget
                && plan.Resolver.GetDefinedType(projectedNominalTarget.Declaration).UnderlyingType
                    is not ExpressBoundSelectType
                && ResolveSelectCarrierType(plan, valueExpression) is { } nominalSelectCarrier
                && ResolveSelectToRuntimeTarget(
                    plan,
                    nominalSelectCarrier,
                    valueCode,
                    projectedNominalTarget,
                    allocateTemporaryName("__expressAssignedNominalSelect"),
                    valueCanBeIndeterminate) is { } projectedNominalValue)
            {
                if (!targetIsOptional && !canReturnIndeterminate)
                {
                    var requiredType = ExpressExpressionEmitter.BoundTypeName(projectedNominalTarget);
                    var requiredValue = allocateTemporaryName("__expressRequiredNominalSelect");
                    valueCode = $"({projectedNominalValue}) switch {{ "
                        + $"{requiredType} {requiredValue} => {requiredValue}, "
                        + "_ => throw new global::System.InvalidOperationException() }";
                    valueCanBeIndeterminate = false;
                }
                else
                {
                    valueCode = projectedNominalValue;
                    valueCanBeIndeterminate = true;
                }
            }

            if (targetType is ExpressBoundScalarType indexedScalarTarget
                && valueExpression.Kind == ExpressExpressionKind.IndexQualifier
                && valueExpression.Type.DeclaredType is ExpressBoundNamedType indexedSelectName
                && indexedSelectName.Declaration.Kind != ExpressDeclarationKind.Entity
                && plan.Resolver.GetDefinedType(indexedSelectName.Declaration).UnderlyingType
                    is ExpressBoundSelectType)
            {
                var targetName = ExpressExpressionEmitter.BoundTypeName(indexedScalarTarget);
                var incompatible = $"({targetName}?)null";
                if (valueCanBeIndeterminate)
                {
                    var presentSelect = allocateTemporaryName("__expressAssignedIndexedSelect");
                    valueCode = $"(({valueCode}) is {{ }} {presentSelect} ? "
                        + ResolveSelectScalarValue(
                            plan,
                            indexedSelectName,
                            presentSelect,
                            indexedScalarTarget)
                        + $" : {incompatible})";
                }
                else
                {
                    valueCode = ResolveSelectScalarValue(
                        plan,
                        indexedSelectName,
                        valueCode,
                        indexedScalarTarget);
                }

                valueCanBeIndeterminate = true;
            }

            if (targetType is ExpressBoundScalarType scalarAssignmentTarget
                && valueExpression.Type.Kind == ExpressExpressionTypeKind.Select
                && valueExpression.Kind != ExpressExpressionKind.IndexQualifier
                && valueExpression.Reference is { } selectValueReference
                && scalarNarrowings?.ContainsKey(selectValueReference.Name) != true
                && valueExpression.Type.DeclaredType is ExpressBoundNamedType selectValueName
                && selectValueName.Declaration.Kind != ExpressDeclarationKind.Entity
                && plan.Resolver.GetDefinedType(selectValueName.Declaration).UnderlyingType
                    is ExpressBoundSelectType)
            {
                var targetName = ExpressExpressionEmitter.BoundTypeName(scalarAssignmentTarget);
                var incompatible = $"({targetName}?)null";
                if (valueCanBeIndeterminate)
                {
                    var presentSelect = allocateTemporaryName("__expressAssignedScalarSelect");
                    var projected = ResolveReference(
                        plan,
                        selectValueReference,
                        selfExpression: null,
                        "entities",
                        lexicalNames,
                        selectNarrowings,
                        selectValueName,
                        presentSelect,
                        narrowedScalarType: scalarAssignmentTarget,
                        incompatibleScalarCode: incompatible);
                    valueCode = $"(({valueCode}) is {{ }} {presentSelect} ? {projected} : {incompatible})";
                }
                else
                {
                    valueCode = ResolveReference(
                        plan,
                        selectValueReference,
                        selfExpression: null,
                        "entities",
                        lexicalNames,
                        selectNarrowings,
                        selectValueName,
                        valueCode,
                        narrowedScalarType: scalarAssignmentTarget,
                        incompatibleScalarCode: incompatible);
                }

                valueCanBeIndeterminate = true;
            }

            if (targetType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } subtypeTarget
                && (valueExpression.Reference?.Type is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, }
                        ? valueExpression.Reference.Type
                        : valueExpression.Type.DeclaredType) is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } assignedSourceEntity
                && !ReferenceEquals(assignedSourceEntity.Declaration, subtypeTarget.Declaration)
                && plan.EntityProjections.Single(projection =>
                        projection.Entity.Symbol == subtypeTarget.Declaration)
                    .PhysicalComponents.Any(component =>
                        component.Symbol == assignedSourceEntity.Declaration))
            {
                var targetName = ExpressExpressionEmitter.BoundTypeName(subtypeTarget);
                var typedValue = allocateTemporaryName("__expressAssignedSubtype");
                valueCode = $"(({valueCode}) is {targetName} {typedValue} ? {typedValue} : "
                    + $"({targetName}?)null)";
                valueCanBeIndeterminate = true;
            }

            if (targetType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, })
            {
                var underlyingTarget = targetType;
                var definedTypes = ResolveTransparentDefinedWrappers(
                    plan.Resolver,
                    ref underlyingTarget);

                var actualNominal = valueExpression.Type.DeclaredType as ExpressBoundNamedType;
                var primitiveActual = actualNominal is null;
                var sameDefinedActual = definedTypes.Count > 0
                    && actualNominal is not null
                    && (definedTypes.Any(candidate =>
                            ReferenceEquals(actualNominal.Declaration, candidate.Declaration))
                        || IsTransparentAliasOf(
                            plan,
                            actualNominal,
                            definedTypes[0].Declaration));
                var semanticDefinedActual = actualNominal is not null
                    && ResolveDefinedValueType(plan, targetType) is ExpressBoundNamedType semanticTarget
                    && ReferenceEquals(actualNominal.Declaration, semanticTarget.Declaration);
                var exactScalar = underlyingTarget is ExpressBoundScalarType targetScalar
                    && targetScalar.Kind switch
                    {
                        ExpressScalarKind.Binary => valueExpression.Type.Kind == ExpressExpressionTypeKind.Binary,
                        ExpressScalarKind.Boolean => valueExpression.Type.Kind == ExpressExpressionTypeKind.Boolean,
                        ExpressScalarKind.Integer => valueExpression.Type.Kind == ExpressExpressionTypeKind.Integer,
                        ExpressScalarKind.Logical => valueExpression.Type.Kind == ExpressExpressionTypeKind.Logical,
                        ExpressScalarKind.Number => valueExpression.Type.Kind == ExpressExpressionTypeKind.Number,
                        ExpressScalarKind.Real => valueExpression.Type.Kind == ExpressExpressionTypeKind.Real,
                        ExpressScalarKind.String => valueExpression.Type.Kind == ExpressExpressionTypeKind.String,
                        _ => false,
                    };
                var exactSelect = underlyingTarget is ExpressBoundSelectType
                    && valueExpression.Type.Kind == ExpressExpressionTypeKind.Select;
                var exactEnumeration = underlyingTarget is ExpressBoundEnumerationType
                    && valueExpression.Type.Kind == ExpressExpressionTypeKind.Enumeration;
                if (exactScalar
                    || ((primitiveActual || sameDefinedActual || semanticDefinedActual)
                        && (exactSelect || exactEnumeration)))
                {
                    if (valueCanBeIndeterminate)
                    {
                        var presentValue = allocateTemporaryName("__expressAssignedDefinedValue");
                        var presentCode = presentValue;
                        for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                        {
                            presentCode = "new "
                                + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                                + $"({presentCode})";
                        }

                        valueCode = $"(({valueCode}) is {{ }} {presentValue} ? {presentCode} : ("
                            + ExpressExpressionEmitter.BoundTypeName(targetType)
                            + "?)null)";
                    }
                    else
                    {
                        for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                        {
                            valueCode = "new "
                                + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                                + $"({valueCode})";
                        }
                    }
                }
            }

            if (plan.Resolver.GetAggregateType(targetType) is { } selectedAggregateTarget
                && valueExpression.Type.DeclaredType is { } selectedAggregateSource
                && plan.Resolver.GetAggregateType(selectedAggregateSource) is null
                && IsSelectValueType(plan, selectedAggregateSource)
                && ResolveAggregateSource(
                    plan,
                    selectedAggregateSource,
                    valueCode,
                    selectedAggregateTarget,
                    allowIncompatible: true) is { } selectedAggregate)
            {
                var presentAggregate = allocateTemporaryName("__expressSelectedAssignedAggregate");
                var assignedAggregate = $"({ExpressExpressionEmitter.BoundTypeName(selectedAggregateTarget)})"
                    + $"[..{presentAggregate}]";
                var wrappers = new List<ExpressBoundNamedType>();
                var pendingTarget = targetType;
                while (pendingTarget is ExpressBoundNamedType wrapper
                       && wrapper.Declaration.Kind != ExpressDeclarationKind.Entity)
                {
                    wrappers.Add(wrapper);
                    pendingTarget = plan.Resolver.GetDefinedType(wrapper.Declaration).UnderlyingType;
                }

                for (var index = wrappers.Count - 1; index >= 0; index--)
                {
                    assignedAggregate = "new "
                        + ExpressExpressionEmitter.BoundTypeName(wrappers[index])
                        + $"({assignedAggregate})";
                }

                valueCode = $"(({selectedAggregate}) is {{ }} {presentAggregate} ? "
                    + $"{assignedAggregate} : ("
                    + ExpressExpressionEmitter.BoundTypeName(targetType)
                    + "?)null)";
                valueCanBeIndeterminate = true;
            }

            if (targetType is ExpressBoundAggregateType assignedAggregateTarget
                && valueExpression.Kind != ExpressExpressionKind.Indeterminate
                && ResolveExpressionAggregateType(plan, valueExpression, assignedAggregateTarget)
                    is { } assignedAggregateSource
                && ResolveAggregateValueToTarget(
                    plan,
                    assignedAggregateSource,
                    assignedAggregateTarget,
                    valueCode,
                    allocateTemporaryName("__expressAssignedAggregate"),
                    valueCanBeIndeterminate,
                    materializeEquivalent: false) is { } adaptedAssignedAggregate)
            {
                valueCode = adaptedAssignedAggregate;
                wasAssignedAggregateAdapted = true;
            }

            valueCode = CopyAggregateValueForAssignment(
                plan.Resolver,
                targetType,
                valueExpression,
                valueCode,
                valueCanBeIndeterminate,
                allocateTemporaryName);

            if (!wasAssignedAggregateAdapted
                && targetType is ExpressBoundAggregateType targetSelectSet
                && targetSelectSet.Kind == ExpressAggregateKind.Set
                && targetSelectSet.ElementType is ExpressBoundNamedType targetSelectElement
                && targetSelectElement.Declaration.Kind != ExpressDeclarationKind.Entity
                && plan.Resolver.GetDefinedType(targetSelectElement.Declaration).UnderlyingType
                    is ExpressBoundSelectType
                && valueExpression.Type.DeclaredType is ExpressBoundAggregateType sourceSelectSet
                && sourceSelectSet.Kind == ExpressAggregateKind.Set
                && sourceSelectSet.ElementType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } sourceSelectEntity)
            {
                var sourceItem = allocateTemporaryName("__expressAssignedSelectSetItem");
                if (ResolveEntityToSelectValue(
                        plan,
                        targetSelectElement,
                        sourceSelectEntity.Declaration,
                        sourceItem) is { } selectedItem)
                {
                    var targetSetName = ExpressExpressionEmitter.BoundTypeName(targetSelectSet);
                    var projected = $"({targetSetName})[..global::System.Linq.Enumerable.Select("
                        + $"({valueCode}), {sourceItem} => {selectedItem})]";
                    if (valueCanBeIndeterminate)
                    {
                        var presentSet = allocateTemporaryName("__expressAssignedSelectSet");
                        valueCode = $"(({valueCode}) is {{ }} {presentSet} ? "
                            + $"({targetSetName})[..global::System.Linq.Enumerable.Select("
                            + $"{presentSet}, {sourceItem} => {selectedItem})] : "
                            + $"({targetSetName}?)null)";
                    }
                    else
                    {
                        valueCode = projected;
                    }
                }
            }

            if (!wasAssignedAggregateAdapted
                && targetType is ExpressBoundAggregateType targetEntityAggregate
                && (targetEntityAggregate.Kind == ExpressAggregateKind.Set
                    || (targetEntityAggregate.Kind == ExpressAggregateKind.Bag
                        && valueExpression.Kind == ExpressExpressionKind.Query))
                && targetEntityAggregate.ElementType is ExpressBoundNamedType targetEntity
                && targetEntity.Declaration.Kind == ExpressDeclarationKind.Entity
                && valueExpression.Type.DeclaredType is ExpressBoundAggregateType sourceEntityAggregate
                && sourceEntityAggregate.Kind == targetEntityAggregate.Kind)
            {
                var sourceCanContainTarget = sourceEntityAggregate.ElementType
                    is ExpressBoundGenericType { IsEntity: true, };
                if (sourceEntityAggregate.ElementType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } sourceEntity
                    && !ReferenceEquals(sourceEntity.Declaration, targetEntity.Declaration))
                {
                    sourceCanContainTarget = plan.EntityProjections.Single(projection =>
                            ReferenceEquals(projection.Entity.Symbol, targetEntity.Declaration))
                        .PhysicalComponents.Any(component =>
                            ReferenceEquals(component.Symbol, sourceEntity.Declaration));
                }

                if (sourceCanContainTarget)
                {
                    var source = allocateTemporaryName("__expressAssignedSet");
                    var testedItem = allocateTemporaryName("__expressTestedSetItem");
                    var selectedItem = allocateTemporaryName("__expressSelectedSetItem");
                    var typedItem = allocateTemporaryName("__expressTypedSetItem");
                    var targetSetName = ExpressExpressionEmitter.BoundTypeName(targetEntityAggregate);
                    var targetEntityName = ExpressExpressionEmitter.BoundTypeName(targetEntity);
                    valueCode = $"(({valueCode}) is {{ }} {source} && "
                        + "global::System.Linq.Enumerable.All("
                        + $"{source}, {testedItem} => {testedItem} is {targetEntityName}) ? "
                        + $"({targetSetName})[..global::System.Linq.Enumerable.Select("
                        + $"{source}, {selectedItem} => {selectedItem} switch {{ "
                        + $"{targetEntityName} {typedItem} => {typedItem}, "
                        + "_ => throw new global::System.InvalidOperationException() })] : "
                        + $"({targetSetName}?)null)";
                }
            }

            if (targetType is ExpressBoundNamedType targetSelectName
                && targetSelectName.Declaration.Kind != ExpressDeclarationKind.Entity
                && plan.Resolver.GetDefinedType(targetSelectName.Declaration).UnderlyingType
                    is ExpressBoundSelectType
                && (valueExpression.Reference?.Type ?? valueExpression.Type.DeclaredType)
                    is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } valueEntity)
            {
                var selectedValue = ResolveEntityToSelectValue(
                    plan,
                    targetSelectName,
                    valueEntity.Declaration,
                    valueCode);
                if (selectedValue is not null)
                {
                    valueCode = selectedValue;
                }
                else if (ResolveRuntimeEntityToSelectValue(
                    plan,
                    targetSelectName,
                    valueEntity,
                    valueCode,
                    allocateTemporaryName) is { } runtimeSelectedValue
                    && HasSelectAssignmentProof(valueExpression, targetSelectName))
                {
                    valueCode = runtimeSelectedValue;
                    valueCanBeIndeterminate = true;
                }
            }

            if (targetType is ExpressBoundScalarType { Kind: ExpressScalarKind.Real, }
                && valueExpression.Type.Kind == ExpressExpressionTypeKind.Integer)
            {
                if (valueCanBeIndeterminate)
                {
                    var presentInteger = allocateTemporaryName("__expressAssignedInteger");
                    valueCode = $"(({valueCode}) is {{ }} {presentInteger} ? "
                        + ExpressExpressionEmitter.PromoteNumeric(
                            valueExpression,
                            presentInteger,
                            ExpressExpressionTypeKind.Real)
                        + " : (global::TedToolkit.Step21.RealValue?)null)";
                }
                else
                {
                    valueCode = ExpressExpressionEmitter.PromoteNumeric(
                        valueExpression,
                        valueCode,
                        ExpressExpressionTypeKind.Real);
                }
            }

            if (targetType is ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, }
                && valueExpression.Type.Kind == ExpressExpressionTypeKind.Boolean)
            {
                valueCode = ExpressExpressionEmitter.AsLogical(valueExpression, valueCode);
            }
            else if (targetType is ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, }
                     && (valueExpression.Type.Kind == ExpressExpressionTypeKind.Logical
                        || IsLogicalOperatorExpression(valueExpression)))
            {
                if (IsDeterminateBooleanExpression(valueExpression))
                {
                    valueCode = $"(({valueCode}) switch {{ "
                        + "global::TedToolkit.Step21.LogicalValue.True => true, "
                        + "global::TedToolkit.Step21.LogicalValue.False => false, "
                        + "_ => throw new global::System.InvalidOperationException() })";
                }
                else
                {
                    valueCode = $"(({valueCode}) switch {{ "
                        + "global::TedToolkit.Step21.LogicalValue.True => true, "
                        + "global::TedToolkit.Step21.LogicalValue.False => false, "
                        + "global::TedToolkit.Step21.LogicalValue.Unknown => (global::System.Boolean?)null })";
                    valueCanBeIndeterminate = true;
                    valueIsKnownDeterminate = false;
                }
            }

            if (valueCanBeIndeterminate
                && !valueIsKnownDeterminate
                && !targetIsOptional
                && !canReturnIndeterminate
                && targetType is ExpressBoundScalarType { Kind: ExpressScalarKind.Real, }
                && valueExpression.Operation is "+" or "-" or "*" or "/")
            {
                var requiredValue = allocateTemporaryName("__expressRequiredAssignedValue");
                valueCode = $"(({valueCode}) is {{ }} {requiredValue} ? {requiredValue} : "
                    + "throw new global::System.InvalidOperationException())";
                valueCanBeIndeterminate = false;
                valueIsKnownDeterminate = true;
            }

            if (valueCanBeIndeterminate)
            {
                var unknownResult = functionResultType is ExpressBoundScalarType
                { Kind: ExpressScalarKind.Logical, }
                        ? "global::TedToolkit.Step21.LogicalValue.Unknown"
                        : "null";
                if (valueExpression.Kind == ExpressExpressionKind.Indeterminate)
                {
                    if (targetIsOptional && optionalUnsetCode is not null)
                    {
                        owner.AddStatement(new CustomExpression(optionalUnsetCode));
                        return true;
                    }

                    if (!targetIsOptional && canReturnIndeterminate)
                    {
                        owner.AddStatement(new CustomExpression(unknownResult).Return);
                        return false;
                    }
                }

                var presentValue = allocateTemporaryName("__expressAssignedValue");
                if (targetIsOptional && optionalUnsetCode is not null)
                {
                    var presence = new IfStatement(new CustomExpression(
                            $"({valueCode}) is {{ }} {presentValue}"))
                        .AddStatement(CreateAssignmentStatement(
                            plan,
                            targetCode,
                            presentValue,
                            assignedEntityCode,
                            assignedEntityType,
                            assignedAttribute,
                            assignedMemberSuffix,
                            assignedAggregateDepth,
                            targetType));
                    presence.Else().AddStatement(new CustomExpression(optionalUnsetCode));
                    owner.AddStatement(presence);
                    return true;
                }

                if (!targetIsOptional && canReturnIndeterminate)
                {
                    if (!hasQualifier && canEstablishDeterminacy)
                    {
                        determinateLexicals?.Add(target);
                    }

                    var presence = new IfStatement(new CustomExpression(
                            $"({valueCode}) is {{ }} {presentValue}"))
                        .AddStatement(CreateAssignmentStatement(
                            plan,
                            targetCode,
                            presentValue,
                            assignedEntityCode,
                            assignedEntityType,
                            assignedAttribute,
                            assignedMemberSuffix,
                            assignedAggregateDepth,
                            targetType));
                    presence.Else().AddStatement(new CustomExpression(unknownResult).Return);
                    owner.AddStatement(presence);
                    return true;
                }
            }

            if (!hasQualifier
                && !targetIsOptional
                && canEstablishDeterminacy
                && valueIsKnownDeterminate)
            {
                determinateLexicals?.Add(target);
            }

            owner.AddStatement(CreateAssignmentStatement(
                plan,
                targetCode,
                valueCode,
                assignedEntityCode,
                assignedEntityType,
                assignedAttribute,
                assignedMemberSuffix,
                assignedAggregateDepth,
                targetType));
            return true;
        }

        if (operation.Role == "returnStmt")
        {
            if (!operation.ChildRules("expression").Any())
            {
                owner.AddStatement(new CustomExpression("return"));
                return false;
            }

            if (functionResultType is null)
            {
                throw new InvalidOperationException("An EXPRESS procedure RETURN cannot carry a value.");
            }

            var boundExpression = plan.GetExpression(operation.RequiredChild("expression"));
            var expression = ExpressExpressionEmitter.Emit(
                boundExpression,
                CreateContext(
                    plan,
                    selfExpression: null,
                    "entities",
                    lexicalNames,
                    safeIndices,
                    allocateTemporaryName,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings));
            var expressionCode = expression.Code;
            var expressionCanBeIndeterminate = CanEmitIndeterminate(plan, boundExpression)
                || (boundExpression.Operation is "+" or "-" or "*" or "/"
                    && boundExpression.Children.Any(child =>
                        child.Type.DeclaredType is ExpressBoundNamedType dynamicSelect
                        && dynamicSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                        && plan.Resolver.GetDefinedType(dynamicSelect.Declaration).UnderlyingType
                            is ExpressBoundSelectType));
            if (boundExpression.Type.DeclaredType is ExpressBoundGenericType
                && functionResultType is not ExpressBoundGenericType)
            {
                expressionCode = ResolveGenericValueToTarget(
                    plan,
                    functionResultType,
                    expressionCode,
                    allocateTemporaryName("__expressReturnedGeneric"));
                expressionCanBeIndeterminate = true;
            }

            if (functionResultType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, } returnedSelectTarget
                && plan.Resolver.GetDefinedType(returnedSelectTarget.Declaration).UnderlyingType
                    is ExpressBoundSelectType
                && boundExpression.Type.DeclaredType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, } returnedNamedSource
                && !ReferenceEquals(returnedSelectTarget.Declaration, returnedNamedSource.Declaration))
            {
                var returnedSelectedValue = plan.Resolver.GetDefinedType(returnedNamedSource.Declaration)
                        .UnderlyingType is ExpressBoundSelectType
                    ? ResolveSelectToSelectValue(
                        plan,
                        returnedSelectTarget,
                        returnedNamedSource,
                        expressionCode,
                        allocateTemporaryName("__expressReturnedSelectValue"))
                    : ResolveNamedExpressionToSelectValue(
                        plan,
                        returnedSelectTarget,
                        returnedNamedSource,
                        expressionCode,
                        expressionCanBeIndeterminate,
                        allocateTemporaryName("__expressReturnedSelectValue"));
                if (returnedSelectedValue is not null)
                {
                    expressionCode = returnedSelectedValue;
                }
            }

            if (functionResultType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, } returnedScalarSelect
                && plan.Resolver.GetDefinedType(returnedScalarSelect.Declaration).UnderlyingType
                    is ExpressBoundSelectType
                && boundExpression.Type.DeclaredType is null or ExpressBoundScalarType
                && ResolveScalarExpressionToSelectValue(
                    plan,
                    returnedScalarSelect,
                    boundExpression,
                    expressionCode,
                    expressionCanBeIndeterminate,
                    allocateTemporaryName("__expressReturnedScalarValue")) is { } returnedScalarValue)
            {
                expressionCode = returnedScalarValue;
            }

            if (functionResultType is ExpressBoundScalarType returnedScalar
                && returnedScalar.Kind is ExpressScalarKind.Integer
                    or ExpressScalarKind.Number
                    or ExpressScalarKind.Real
                && boundExpression.Type.DeclaredType is ExpressBoundNamedType returnedSelect
                && returnedSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                && plan.Resolver.GetDefinedType(returnedSelect.Declaration).UnderlyingType
                    is ExpressBoundSelectType)
            {
                var fallback = "("
                    + ExpressExpressionEmitter.BoundTypeName(returnedScalar)
                    + "?)null";
                if (expressionCanBeIndeterminate)
                {
                    var present = allocateTemporaryName("__expressReturnedScalarSelect");
                    expressionCode = $"(({expressionCode}) is {{ }} {present} ? "
                        + ResolveSelectScalarValue(plan, returnedSelect, present, returnedScalar)
                        + $" : {fallback})";
                }
                else
                {
                    expressionCode = ResolveSelectScalarValue(
                        plan,
                        returnedSelect,
                        expressionCode,
                        returnedScalar);
                }
            }

            if (functionResultType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } selectedReturnedEntityTarget
                && boundExpression.Type.DeclaredType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, } returnedEntitySelect
                && plan.Resolver.GetDefinedType(returnedEntitySelect.Declaration).UnderlyingType
                    is ExpressBoundSelectType
                && !(boundExpression.Reference is { } directlyNarrowedEntityReference
                    && selectNarrowings?.ContainsKey(directlyNarrowedEntityReference) == true)
                && ResolveSelectToEntityValue(
                    plan,
                    selectedReturnedEntityTarget,
                    returnedEntitySelect,
                    expressionCode,
                    allocateTemporaryName("__expressReturnedEntity")) is { } selectedReturnedEntity)
            {
                expressionCode = selectedReturnedEntity;
            }

            if (functionResultType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, } returnedNominalTarget
                && plan.Resolver.GetDefinedType(returnedNominalTarget.Declaration).UnderlyingType
                    is not ExpressBoundSelectType
                && ResolveSelectCarrierType(plan, boundExpression) is { } returnedNominalCarrier
                && ResolveSelectToRuntimeTarget(
                    plan,
                    returnedNominalCarrier,
                    expressionCode,
                    returnedNominalTarget,
                    allocateTemporaryName("__expressReturnedNominalSelect"),
                    expressionCanBeIndeterminate) is { } returnedNominalValue)
            {
                expressionCode = returnedNominalValue;
                expressionCanBeIndeterminate = true;
            }

            if (functionResultType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } returnedEntity
                && boundExpression.Reference is { Type: { } returnedCarrier, } returnedReference
                && selectNarrowings?.TryGetValue(returnedReference, out var returnedAlternative) == true
                && returnedAlternative.Kind == ExpressDeclarationKind.Entity)
            {
                var carrierType = ResolveDefinedValueType(plan, returnedCarrier);
                if (expressionCanBeIndeterminate)
                {
                    var targetName = ExpressExpressionEmitter.BoundTypeName(returnedEntity);
                    var present = allocateTemporaryName("__expressReturnedEntitySelect");
                    expressionCode = $"(({expressionCode}) is {{ }} {present} ? "
                        + ResolveNarrowedEntityCarrier(
                            plan,
                            carrierType,
                            present,
                            returnedEntity.Declaration)
                        + $" : ({targetName}?)null)";
                }
                else
                {
                    expressionCode = ResolveNarrowedEntityCarrier(
                        plan,
                        carrierType,
                        expressionCode,
                        returnedEntity.Declaration);
                }
            }

            if (functionResultType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } returnedSubtype
                && (boundExpression.Reference?.Type is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, }
                        ? boundExpression.Reference.Type
                        : boundExpression.Type.DeclaredType) is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } returnedSupertype
                && !ReferenceEquals(
                    returnedSubtype.Declaration,
                    returnedSupertype.Declaration)
                && plan.EntityProjections.Single(projection =>
                        projection.Entity.Symbol == returnedSubtype.Declaration)
                    .PhysicalComponents.Any(component =>
                        component.Symbol == returnedSupertype.Declaration))
            {
                var targetName = ExpressExpressionEmitter.BoundTypeName(returnedSubtype);
                var typedValue = allocateTemporaryName("__expressReturnedSubtype");
                expressionCode = $"(({expressionCode}) is {targetName} {typedValue} ? "
                    + $"{typedValue} : ({targetName}?)null)";
            }

            if (functionResultType is ExpressBoundNamedType resultSelectName
                && resultSelectName.Declaration.Kind != ExpressDeclarationKind.Entity
                && plan.Resolver.GetDefinedType(resultSelectName.Declaration).UnderlyingType
                    is ExpressBoundSelectType resultSelect
                && boundExpression.Type.DeclaredType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } resultEntity)
            {
                var directValue = ResolveEntityToSelectValue(
                    plan,
                    resultSelectName,
                    resultEntity.Declaration,
                    expressionCode);
                var resultProjection = plan.EntityProjections.Single(projection =>
                    projection.Entity.Symbol == resultEntity.Declaration);
                var alternatives = plan.Resolver.GetSelectAlternatives(resultSelect)
                    .Where(alternative => alternative.Kind == ExpressDeclarationKind.Entity
                        && resultProjection.PhysicalComponents.Any(component =>
                            component.Symbol == alternative))
                    .ToArray();
                if (directValue is not null)
                {
                    expressionCode = directValue;
                }
                else if (alternatives.Length == 1)
                {
                    expressionCode = ExpressExpressionEmitter.BoundTypeName(resultSelectName)
                        + ".From"
                        + ExpressEntityProjection.ToPascalCase(alternatives[0].Name)
                        + $"({expressionCode})";
                }
                else if (alternatives.Length == 0)
                {
                    if (ResolveRuntimeEntityToSelectValue(
                        plan,
                        resultSelectName,
                        resultEntity,
                        expressionCode,
                        allocateTemporaryName) is { } runtimeSelectedValue)
                    {
                        expressionCode = runtimeSelectedValue;
                    }
                }
            }

            if (plan.Resolver.GetAggregateType(functionResultType) is { } returnedAggregateTarget
                && boundExpression.Kind != ExpressExpressionKind.Indeterminate
                && ResolveExpressionAggregateType(plan, boundExpression, returnedAggregateTarget)
                    is { } returnedAggregateSource
                && ResolveAggregateValueToTarget(
                    plan,
                    returnedAggregateSource,
                    returnedAggregateTarget,
                    expressionCode,
                    allocateTemporaryName("__expressReturnedAggregate"),
                    expressionCanBeIndeterminate,
                    materializeEquivalent: true) is { } returnedAggregate)
            {
                expressionCode = returnedAggregate;
            }

            if (functionResultType is ExpressBoundNamedType
                { Declaration.Kind: not ExpressDeclarationKind.Entity, }
                && boundExpression.Kind != ExpressExpressionKind.Indeterminate)
            {
                var targetType = functionResultType;
                var definedTypes = ResolveTransparentDefinedWrappers(plan.Resolver, ref targetType);

                var actualNominal = boundExpression.Type.DeclaredType as ExpressBoundNamedType;
                var primitiveActual = actualNominal is null;
                var sameDefinedActual = definedTypes.Count > 0
                    && actualNominal is not null
                    && ReferenceEquals(actualNominal.Declaration, definedTypes[0].Declaration);
                var semanticDefinedActual = actualNominal is not null
                    && ResolveDefinedValueType(plan, functionResultType) is ExpressBoundNamedType semanticResult
                    && ReferenceEquals(actualNominal.Declaration, semanticResult.Declaration);
                var exactScalar = targetType is ExpressBoundScalarType targetScalar
                    && targetScalar.Kind switch
                    {
                        ExpressScalarKind.Binary => boundExpression.Type.Kind == ExpressExpressionTypeKind.Binary,
                        ExpressScalarKind.Boolean => boundExpression.Type.Kind == ExpressExpressionTypeKind.Boolean,
                        ExpressScalarKind.Integer => boundExpression.Type.Kind == ExpressExpressionTypeKind.Integer,
                        ExpressScalarKind.Logical => boundExpression.Type.Kind == ExpressExpressionTypeKind.Logical,
                        ExpressScalarKind.Number => boundExpression.Type.Kind == ExpressExpressionTypeKind.Number,
                        ExpressScalarKind.Real => boundExpression.Type.Kind == ExpressExpressionTypeKind.Real,
                        ExpressScalarKind.String => boundExpression.Type.Kind == ExpressExpressionTypeKind.String,
                        _ => false,
                    };
                var exactSelect = targetType is ExpressBoundSelectType
                    && boundExpression.Type.Kind == ExpressExpressionTypeKind.Select;
                var exactEnumeration = targetType is ExpressBoundEnumerationType
                    && boundExpression.Type.Kind == ExpressExpressionTypeKind.Enumeration;
                if (exactScalar
                    || ((primitiveActual || sameDefinedActual || semanticDefinedActual)
                        && (exactSelect || exactEnumeration)))
                {
                    if (expressionCanBeIndeterminate)
                    {
                        var presentValue = allocateTemporaryName("__expressReturnedValue");
                        var presentCode = presentValue;
                        for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                        {
                            presentCode = "new "
                                + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                                + $"({presentCode})";
                        }

                        expressionCode = $"(({expressionCode}) is {{ }} {presentValue} ? {presentCode} : ("
                            + ExpressExpressionEmitter.BoundTypeName(functionResultType)
                            + "?)null)";
                    }
                    else
                    {
                        for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                        {
                            expressionCode = "new "
                                + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                                + $"({expressionCode})";
                        }
                    }
                }
            }

            expressionCode = CopyAggregateValueForAssignment(
                plan.Resolver,
                functionResultType,
                boundExpression,
                expressionCode,
                expressionCanBeIndeterminate,
                allocateTemporaryName,
                copyValue: boundExpression.Kind != ExpressExpressionKind.AggregateInitializer);

            if (functionResultType is ExpressBoundScalarType returnedNumeric
                && ScalarTypeOf(boundExpression) is { } returnedSourceNumeric
                && (returnedSourceNumeric.Kind, returnedNumeric.Kind) is
                    (ExpressScalarKind.Integer, ExpressScalarKind.Real)
                    or (ExpressScalarKind.Integer, ExpressScalarKind.Number)
                    or (ExpressScalarKind.Real, ExpressScalarKind.Number))
            {
                if (expressionCanBeIndeterminate)
                {
                    var present = allocateTemporaryName("__expressReturnedNumeric");
                    expressionCode = $"(({expressionCode}) is {{ }} {present} ? "
                        + PromoteScalarValue(present, returnedSourceNumeric.Kind, returnedNumeric.Kind)
                        + " : ("
                        + ExpressExpressionEmitter.BoundTypeName(returnedNumeric)
                        + "?)null)";
                }
                else
                {
                    expressionCode = PromoteScalarValue(
                        expressionCode,
                        returnedSourceNumeric.Kind,
                        returnedNumeric.Kind);
                }
            }

            if (functionResultType is ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, }
                && boundExpression.Type.Kind == ExpressExpressionTypeKind.Boolean)
            {
                expressionCode = ExpressExpressionEmitter.AsLogical(boundExpression, expressionCode);
            }
            else if (functionResultType is ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, }
                     && boundExpression.Type.Kind == ExpressExpressionTypeKind.Logical)
            {
                expressionCode = $"(({expressionCode}) switch {{ "
                    + "global::TedToolkit.Step21.LogicalValue.True => true, "
                    + "global::TedToolkit.Step21.LogicalValue.False => false, "
                    + "global::TedToolkit.Step21.LogicalValue.Unknown => (global::System.Boolean?)null })";
            }

            owner.AddStatement(new CustomExpression(expressionCode).Return);
            return false;
        }

        if (operation.Role == "repeatStmt")
        {
            var repeatControl = operation.RequiredChild("repeatControl");
            var increment = repeatControl.ChildRules("incrementControl").SingleOrDefault();
            if (increment is null)
            {
                return EmitNonIncrementRepeat(
                    plan,
                    operation,
                    functionResultType,
                    canReturnIndeterminate,
                    owner,
                    lexicalNames,
                    allocateTemporaryName,
                    safeIndices,
                    sizeAliases,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings);
            }

            var variable = increment.RequiredChild("variableId");
            var variableName = variable.Identifier!;
            var generatedName = "__repeat_"
                + ExpressEntityProjection.ToPascalCase(variableName)
                + "_"
                + variable.Span.Start.Line.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "_"
                + variable.Span.Start.Column.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var repeatName = plan.Schema.NameReferences
                .Select(reference => reference.Target)
                .Where(candidate => candidate.Kind == ExpressBoundNameKind.RepeatVariable
                    && SameStart(candidate.Span, variable.Span))
                .Distinct()
                .SingleOrDefault();
            var nestedNames = lexicalNames.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            if (repeatName?.Type is not null)
            {
                nestedNames[variableName] = (generatedName, repeatName.Type);
            }

            var lower = plan.GetExpression(increment.RequiredChild("bound1").RequiredChild("numericExpression"));
            var upper = plan.GetExpression(increment.RequiredChild("bound2").RequiredChild("numericExpression"));
            var step = increment.ChildRules("increment").SingleOrDefault() is { } stepSyntax
                ? plan.GetExpression(stepSyntax.RequiredChild("numericExpression"))
                : null;
            if (lower.Kind == ExpressExpressionKind.Indeterminate
                || upper.Kind == ExpressExpressionKind.Indeterminate
                || step?.Kind == ExpressExpressionKind.Indeterminate)
            {
                return true;
            }

            var lowerValue = ExpressExpressionEmitter.Emit(
                lower,
                CreateContext(
                    plan,
                    selfExpression: null,
                    "entities",
                    lexicalNames,
                    safeIndices,
                    allocateTemporaryName,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings));
            var upperValue = ExpressExpressionEmitter.Emit(
                upper,
                CreateContext(
                    plan,
                    selfExpression: null,
                    "entities",
                    lexicalNames,
                    safeIndices,
                    allocateTemporaryName,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings));
            var stepValue = step is null
                ? null
                : ExpressExpressionEmitter.Emit(
                    step,
                    CreateContext(
                        plan,
                        selfExpression: null,
                        "entities",
                        lexicalNames,
                        safeIndices,
                        allocateTemporaryName,
                        selectNarrowings,
                        pathNarrowings,
                        determinateLexicals,
                        safeIndexPaths,
                        scalarNarrowings));
            var boundGuards = new List<string>();
            string PresentControlValue(ExpressBoundExpression expression, string code, string prefix)
            {
                if (!expression.Type.CanBeIndeterminate)
                {
                    return code;
                }

                var present = allocateTemporaryName(prefix);
                boundGuards.Add($"({code}) is {{ }} {present}");
                return present;
            }

            var lowerPresent = PresentControlValue(lower, lowerValue.Code, "__expressRepeatLower");
            var upperPresent = PresentControlValue(upper, upperValue.Code, "__expressRepeatUpper");
            var stepPresent = step is null
                ? null
                : PresentControlValue(step, stepValue!.Code, "__expressRepeatStep");
            var lowerCode = lower.Type.Kind switch
            {
                ExpressExpressionTypeKind.Integer =>
                    $"global::TedToolkit.Step21.NumberValue.FromInteger({lowerPresent})",
                ExpressExpressionTypeKind.Real =>
                    $"global::TedToolkit.Step21.NumberValue.FromReal({lowerPresent})",
                _ => lowerPresent,
            };
            var upperCode = upper.Type.Kind switch
            {
                ExpressExpressionTypeKind.Integer =>
                    $"global::TedToolkit.Step21.NumberValue.FromInteger({upperPresent})",
                ExpressExpressionTypeKind.Real =>
                    $"global::TedToolkit.Step21.NumberValue.FromReal({upperPresent})",
                _ => upperPresent,
            };
            var stepCode = step?.Type.Kind switch
            {
                null => "global::TedToolkit.Step21.NumberValue.FromInteger(global::System.Numerics.BigInteger.One)",
                ExpressExpressionTypeKind.Integer =>
                    $"global::TedToolkit.Step21.NumberValue.FromInteger({stepPresent})",
                ExpressExpressionTypeKind.Real =>
                    $"global::TedToolkit.Step21.NumberValue.FromReal({stepPresent})",
                _ => stepPresent!,
            };
            var whileControl = repeatControl.ChildRules("whileControl").SingleOrDefault();
            var untilControl = repeatControl.ChildRules("untilControl").SingleOrDefault();
            var whileCode = whileControl is null
                ? null
                : EmitRepeatCondition(
                    plan,
                    whileControl,
                    CreateContext(
                        plan,
                        selfExpression: null,
                        "entities",
                        nestedNames,
                        safeIndices,
                        allocateTemporaryName,
                        selectNarrowings,
                        pathNarrowings,
                        determinateLexicals,
                        safeIndexPaths,
                        scalarNarrowings));
            var repeatSafeIndices = safeIndices?.ToList() ?? [];
            var repeatSafeIndexPaths = safeIndexPaths?.ToList()
                ?? [];
            ExpressBoundName? upperAggregate = null;
            ExpressBoundExpression? upperAggregatePath = null;
            var upperUsesSize = upper.Operation == "SIZEOF";
            var upperUsesHighIndex = upper.Operation == "HIINDEX";
            if ((upperUsesSize || upperUsesHighIndex)
                && upper.Children.Count == 1
                && upper.Children[0].Reference is { Type: ExpressBoundAggregateType, } directAggregate)
            {
                upperAggregate = directAggregate;
                upperAggregatePath = upper.Children[0];
            }
            else if (upper.Reference is { } upperAlias
                     && sizeAliases?.TryGetValue(upperAlias, out var aliasedAggregate) == true)
            {
                upperAggregate = aliasedAggregate;
                upperUsesSize = true;
            }

            if (System.Numerics.BigInteger.TryParse(
                    lower.SourceText,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var lowerBound)
                && lowerBound >= System.Numerics.BigInteger.One
                && upperAggregate is { Type: ExpressBoundAggregateType upperAggregateType, }
                && ((upperUsesSize && upperAggregateType.Kind == ExpressAggregateKind.List)
                    || (upperUsesHighIndex
                        && lowerBound == System.Numerics.BigInteger.One
                        && upperAggregateType.Kind == ExpressAggregateKind.Bag))
                && repeatName is not null
                && (step is null
                    || (System.Numerics.BigInteger.TryParse(
                            step.SourceText,
                            System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var stepValueInteger)
                        && stepValueInteger == System.Numerics.BigInteger.One)))
            {
                repeatSafeIndices = [.. repeatSafeIndices, new(upperAggregate, repeatName),];
                if (upperAggregatePath is { Kind: not ExpressExpressionKind.Reference, })
                {
                    repeatSafeIndexPaths.Add(new(upperAggregatePath, repeatName));
                }
            }

            var nestedSizeAliases = sizeAliases is null
                ? null
                : new Dictionary<ExpressBoundName, ExpressBoundName>(sizeAliases);
            var nestedSelectNarrowings = selectNarrowings is null
                ? new Dictionary<ExpressBoundName, ExpressBoundSymbol>()
                : selectNarrowings.ToDictionary(pair => pair.Key, pair => pair.Value);
            var nestedPathNarrowings = pathNarrowings?.ToList() ?? [];
            var nestedDeterminateLexicals = determinateLexicals is null
                ? null
                : new HashSet<ExpressBoundName>(determinateLexicals);
            var nestedScalarNarrowings = scalarNarrowings is null
                ? new Dictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>(
                    StringComparer.OrdinalIgnoreCase)
                : scalarNarrowings.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
            var repeatTransfers = CreateLoopTransfers(operation, allocateTemporaryName);
            var bodyOwner = new IfStatement(new CustomExpression("true"));
            var bodyFallsThrough = true;
            foreach (var nested in operation.ChildRules("stmt"))
            {
                if (!bodyFallsThrough)
                {
                    break;
                }

                bodyFallsThrough = EmitFunctionStatement(
                    plan,
                    nested,
                    functionResultType,
                    canReturnIndeterminate,
                    bodyOwner,
                    nestedNames,
                    allocateTemporaryName,
                    repeatSafeIndices,
                    nestedSizeAliases,
                    nestedSelectNarrowings,
                    nestedPathNarrowings,
                    nestedDeterminateLexicals,
                    repeatSafeIndexPaths,
                    nestedScalarNarrowings,
                    repeatTransfers);
            }

            if (repeatTransfers is not null)
            {
                bodyFallsThrough = MergeLoopPaths(plan, repeatTransfers.SkipPaths, bodyFallsThrough,
                    repeatSafeIndices, nestedSizeAliases, nestedSelectNarrowings, nestedPathNarrowings,
                    nestedDeterminateLexicals, repeatSafeIndexPaths, nestedScalarNarrowings);
            }

            var untilCode = bodyFallsThrough && untilControl is not null
                ? EmitRepeatCondition(
                    plan,
                    untilControl,
                    CreateContext(
                        plan,
                        selfExpression: null,
                        "entities",
                        nestedNames,
                        repeatSafeIndices,
                        allocateTemporaryName,
                        nestedSelectNarrowings,
                        nestedPathNarrowings,
                        nestedDeterminateLexicals,
                        repeatSafeIndexPaths,
                        nestedScalarNarrowings))
                : null;
            if (repeatTransfers is not null)
            {
                bodyFallsThrough = MergeLoopPaths(plan, repeatTransfers.ExitPaths, bodyFallsThrough,
                    repeatSafeIndices, nestedSizeAliases, nestedSelectNarrowings, nestedPathNarrowings,
                    nestedDeterminateLexicals, repeatSafeIndexPaths, nestedScalarNarrowings);
            }

            if (bodyFallsThrough)
            {
                IntersectDictionaryFacts(
                    scalarNarrowings,
                    static (left, right) => string.Equals(
                            left.StorageCode,
                            right.StorageCode,
                            StringComparison.Ordinal)
                        && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
                        && ReferenceEquals(left.Type, right.Type),
                    scalarNarrowings?.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.OrdinalIgnoreCase)
                        ?? [],
                    nestedScalarNarrowings);
                IntersectCollectionFacts(
                    safeIndices,
                    static (left, right) => ReferenceEquals(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    safeIndices?.ToList() ?? [],
                    repeatSafeIndices);
                IntersectDictionaryFacts(
                    sizeAliases,
                    static (left, right) => ReferenceEquals(left, right),
                    sizeAliases?.ToDictionary(pair => pair.Key, pair => pair.Value)
                        ?? [],
                    nestedSizeAliases ?? []);
                JoinSelectFacts(
                    selectNarrowings,
                    static (left, right) => ReferenceEquals(left, right),
                    key => key.Type is ExpressBoundNamedType declaredSelect
                        && declaredSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                        && plan.Resolver.GetDefinedType(declaredSelect.Declaration).UnderlyingType
                            is ExpressBoundSelectType
                            ? declaredSelect.Declaration
                            : null,
                    nestedSelectNarrowings,
                    selectNarrowings?.ToDictionary(pair => pair.Key, pair => pair.Value)
                        ?? []);
                JoinSelectFacts(
                    pathNarrowings,
                    SameDirectReferencePath,
                    key => key.Type.DeclaredType is ExpressBoundNamedType declaredSelect
                        && declaredSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                        && plan.Resolver.GetDefinedType(declaredSelect.Declaration).UnderlyingType
                            is ExpressBoundSelectType
                            ? declaredSelect.Declaration
                            : null,
                    nestedPathNarrowings,
                    pathNarrowings?.ToList() ?? []);
                IntersectCollectionFacts(
                    determinateLexicals,
                    static (left, right) => ReferenceEquals(left, right),
                    determinateLexicals?.ToArray() ?? Array.Empty<ExpressBoundName>(),
                    (IReadOnlyCollection<ExpressBoundName>?)nestedDeterminateLexicals
                        ?? Array.Empty<ExpressBoundName>());
                IntersectCollectionFacts(
                    safeIndexPaths,
                    static (left, right) => ReferenceEquals(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    safeIndexPaths?.ToList() ?? [],
                    repeatSafeIndexPaths);
            }

            var upperName = allocateTemporaryName("__expressRepeatLimit");
            var stepName = allocateTemporaryName("__expressRepeatIncrement");
            const string zero = "global::TedToolkit.Step21.NumberValue.FromInteger(global::System.Numerics.BigInteger.Zero)";
            var loopStatement = new Custom((ref SourceBuilder source) =>
            {
                source.Append($"for (global::TedToolkit.Step21.NumberValue {generatedName} = {lowerCode}, "
                    + $"{upperName} = {upperCode}, {stepName} = {stepCode}; "
                    + $"{stepName} > {zero} ? {generatedName} <= {upperName} "
                    + $": {stepName} < {zero} && {generatedName} >= {upperName}; "
                    + $"{generatedName} += {stepName})");
                source.BeginBlock();
                if (whileCode is not null)
                {
                    source.Append($"if (!({whileCode}))");
                    source.BeginBlock();
                    source.AppendLine("break;");
                    source.EndBlock();
                }

                foreach (var bodyStatement in bodyOwner.Statements)
                {
                    bodyStatement.ToCode(ref source);
                    source.AppendLine();
                }

                if (repeatTransfers is { SkipPaths.Count: > 0, })
                {
                    source.AppendLine($"{repeatTransfers.SkipLabel}:;");
                }

                if (untilCode is not null)
                {
                    source.Append($"if ({untilCode})");
                    source.BeginBlock();
                    source.AppendLine("break;");
                    source.EndBlock();
                }

                source.EndBlock();
            });
            if (boundGuards.Count == 0)
            {
                owner.AddStatement(loopStatement);
            }
            else
            {
                owner.AddStatement(new IfStatement(new CustomExpression(string.Join(" && ", boundGuards)))
                    .AddStatement(loopStatement));
            }

            return true;
        }

        if (operation.Role == "compoundStmt")
        {
            var fallsThrough = true;
            foreach (var nested in operation.ChildRules("stmt"))
            {
                if (!fallsThrough)
                {
                    break;
                }

                fallsThrough = EmitFunctionStatement(
                    plan,
                    nested,
                    functionResultType,
                    canReturnIndeterminate,
                    owner,
                    lexicalNames,
                    allocateTemporaryName,
                    safeIndices,
                    sizeAliases,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings,
                    loopTransfers);
            }

            return fallsThrough;
        }

        if (operation.Role == "caseStmt")
        {
            var selectorSyntax = operation.RequiredChild("selector").RequiredChild("expression");
            var selector = plan.GetExpression(selectorSyntax);
            var caseContext = CreateContext(
                plan,
                selfExpression: null,
                "entities",
                lexicalNames,
                safeIndices,
                allocateTemporaryName,
                selectNarrowings,
                pathNarrowings,
                determinateLexicals,
                safeIndexPaths,
                scalarNarrowings);
            var selectorCode = selector.Kind == ExpressExpressionKind.Indeterminate
                ? "(object?)null"
                : ExpressExpressionEmitter.Emit(
                    selector,
                    caseContext).Code;
            var selectorName = "__case_"
                + operation.Span.Start.Line.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "_"
                + operation.Span.Start.Column.ToString(System.Globalization.CultureInfo.InvariantCulture);
            owner.AddStatement(new VariableExpression(DataType.Var, selectorName)
                .AddDefault(new CustomExpression(selectorCode)));
            var incomingLexicalNames = lexicalNames.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var incomingSafeIndices = safeIndices?.ToList() ?? [];
            var incomingAliases = sizeAliases?.ToDictionary(pair => pair.Key, pair => pair.Value)
                ?? [];
            var incomingSelectNarrowings = selectNarrowings?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value)
                ?? [];
            var incomingPathNarrowings = pathNarrowings?.ToList() ?? [];
            var incomingDeterminateLexicals = determinateLexicals is null
                ? new HashSet<ExpressBoundName>()
                : new HashSet<ExpressBoundName>(determinateLexicals);
            var incomingSafeIndexPaths = safeIndexPaths?.ToList() ?? [];
            var incomingScalarNarrowings = scalarNarrowings?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase)
                ?? new(
                    StringComparer.OrdinalIgnoreCase);
            var fallingSafeIndices = new List<List<KeyValuePair<ExpressBoundName, ExpressBoundName>>>();
            var fallingAliases = new List<Dictionary<ExpressBoundName, ExpressBoundName>>();
            var fallingSelectNarrowings = new List<Dictionary<ExpressBoundName, ExpressBoundSymbol>>();
            var fallingPathNarrowings =
                new List<List<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>>();
            var fallingDeterminateLexicals = new List<HashSet<ExpressBoundName>>();
            var fallingSafeIndexPaths =
                new List<List<KeyValuePair<ExpressBoundExpression, ExpressBoundName>>>();
            var fallingScalarNarrowings = new List<Dictionary<
                string,
                (string StorageCode, string Code, ExpressBoundType Type)>>();
            IfStatement? conditionalCase = null;
            foreach (var action in operation.ChildRules("caseAction"))
            {
                var comparisons = action.ChildRules("caseLabel").Select(label =>
                {
                    var expression = plan.GetExpression(label.RequiredChild("expression"));
                    if (expression.Kind == ExpressExpressionKind.Indeterminate)
                    {
                        return "false";
                    }

                    var emitted = ExpressExpressionEmitter.Emit(
                        expression,
                        caseContext);
                    return ExpressExpressionEmitter.CaseValueMatches(
                        selector,
                        selectorName,
                        expression,
                        emitted.Code,
                        caseContext);
                });
                var caseCondition = string.Join(" || ", comparisons.Select(comparison => $"({comparison})"));
                var caseBranch = conditionalCase is null
                    ? new IfStatement(new CustomExpression(caseCondition))
                    : conditionalCase.ElseIf(new CustomExpression(caseCondition));
                conditionalCase ??= caseBranch;
                var actionLexicalNames = incomingLexicalNames.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
                var actionSafeIndices = incomingSafeIndices.ToList();
                var actionAliases = incomingAliases.ToDictionary(pair => pair.Key, pair => pair.Value);
                var actionSelectNarrowings = incomingSelectNarrowings.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value);
                var actionPathNarrowings = incomingPathNarrowings.ToList();
                var actionDeterminateLexicals = new HashSet<ExpressBoundName>(
                    incomingDeterminateLexicals);
                var actionSafeIndexPaths = incomingSafeIndexPaths.ToList();
                var actionScalarNarrowings = incomingScalarNarrowings.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
                var actionOwner = new IfStatement(new CustomExpression("true"));
                var actionFallsThrough = EmitFunctionStatement(
                    plan,
                    action.RequiredChild("stmt"),
                    functionResultType,
                    canReturnIndeterminate,
                    actionOwner,
                    actionLexicalNames,
                    allocateTemporaryName,
                    actionSafeIndices,
                    actionAliases,
                    actionSelectNarrowings,
                    actionPathNarrowings,
                    actionDeterminateLexicals,
                    actionSafeIndexPaths,
                    actionScalarNarrowings,
                    loopTransfers);
                foreach (var actionStatement in actionOwner.Statements)
                {
                    caseBranch.AddStatement(actionStatement);
                }

                if (actionFallsThrough)
                {
                    fallingSafeIndices.Add(actionSafeIndices);
                    fallingAliases.Add(actionAliases);
                    fallingSelectNarrowings.Add(actionSelectNarrowings);
                    fallingPathNarrowings.Add(actionPathNarrowings);
                    fallingDeterminateLexicals.Add(actionDeterminateLexicals);
                    fallingSafeIndexPaths.Add(actionSafeIndexPaths);
                    fallingScalarNarrowings.Add(actionScalarNarrowings);
                }
            }

            if (operation.ChildRules("stmt").SingleOrDefault() is { } otherwise)
            {
                var otherwiseBranch = conditionalCase?.Else()
                    ?? throw new InvalidOperationException("CASE requires at least one action.");
                var otherwiseLexicalNames = incomingLexicalNames.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
                var otherwiseSafeIndices = incomingSafeIndices.ToList();
                var otherwiseAliases = incomingAliases.ToDictionary(pair => pair.Key, pair => pair.Value);
                var otherwiseSelectNarrowings = incomingSelectNarrowings.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value);
                var otherwisePathNarrowings = incomingPathNarrowings.ToList();
                var otherwiseDeterminateLexicals = new HashSet<ExpressBoundName>(
                    incomingDeterminateLexicals);
                var otherwiseSafeIndexPaths = incomingSafeIndexPaths.ToList();
                var otherwiseScalarNarrowings = incomingScalarNarrowings.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
                var otherwiseOwner = new IfStatement(new CustomExpression("true"));
                var otherwiseFallsThrough = EmitFunctionStatement(
                    plan,
                    otherwise,
                    functionResultType,
                    canReturnIndeterminate,
                    otherwiseOwner,
                    otherwiseLexicalNames,
                    allocateTemporaryName,
                    otherwiseSafeIndices,
                    otherwiseAliases,
                    otherwiseSelectNarrowings,
                    otherwisePathNarrowings,
                    otherwiseDeterminateLexicals,
                    otherwiseSafeIndexPaths,
                    otherwiseScalarNarrowings,
                    loopTransfers);
                foreach (var otherwiseStatement in otherwiseOwner.Statements)
                {
                    otherwiseBranch.AddStatement(otherwiseStatement);
                }

                if (otherwiseFallsThrough)
                {
                    fallingSafeIndices.Add(otherwiseSafeIndices);
                    fallingAliases.Add(otherwiseAliases);
                    fallingSelectNarrowings.Add(otherwiseSelectNarrowings);
                    fallingPathNarrowings.Add(otherwisePathNarrowings);
                    fallingDeterminateLexicals.Add(otherwiseDeterminateLexicals);
                    fallingSafeIndexPaths.Add(otherwiseSafeIndexPaths);
                    fallingScalarNarrowings.Add(otherwiseScalarNarrowings);
                }
            }
            else if (plan.IsExhaustiveCase(operation) && !selector.Type.CanBeIndeterminate)
            {
                var unmatched = conditionalCase?.Else()
                    ?? throw new InvalidOperationException("CASE requires at least one action.");
                unmatched.AddStatement(new CustomExpression(
                    "throw new global::System.InvalidOperationException(\"An exhaustive EXPRESS CASE was unmatched.\")"));
            }
            else
            {
                fallingSafeIndices.Add(incomingSafeIndices);
                fallingAliases.Add(incomingAliases);
                fallingSelectNarrowings.Add(incomingSelectNarrowings);
                fallingPathNarrowings.Add(incomingPathNarrowings);
                fallingDeterminateLexicals.Add(incomingDeterminateLexicals);
                fallingSafeIndexPaths.Add(incomingSafeIndexPaths);
                fallingScalarNarrowings.Add(incomingScalarNarrowings);
            }

            owner.AddStatement(conditionalCase
                ?? throw new InvalidOperationException("CASE requires at least one action."));
            if (fallingScalarNarrowings.Count == 0)
            {
                return false;
            }

            IntersectDictionaryFacts(
                scalarNarrowings,
                static (left, right) => string.Equals(
                        left.StorageCode,
                        right.StorageCode,
                        StringComparison.Ordinal)
                    && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
                    && ReferenceEquals(left.Type, right.Type),
                fallingScalarNarrowings[0],
                fallingScalarNarrowings[0]);
            IntersectCollectionFacts(
                safeIndices,
                static (left, right) => ReferenceEquals(left.Key, right.Key)
                    && ReferenceEquals(left.Value, right.Value),
                fallingSafeIndices[0],
                fallingSafeIndices[0]);
            IntersectDictionaryFacts(
                sizeAliases,
                static (left, right) => ReferenceEquals(left, right),
                fallingAliases[0],
                fallingAliases[0]);
            JoinSelectFacts(
                selectNarrowings,
                static (left, right) => ReferenceEquals(left, right),
                key => key.Type is ExpressBoundNamedType declaredSelect
                    && declaredSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(declaredSelect.Declaration).UnderlyingType
                        is ExpressBoundSelectType
                        ? declaredSelect.Declaration
                        : null,
                fallingSelectNarrowings[0],
                fallingSelectNarrowings[0]);
            JoinSelectFacts(
                pathNarrowings,
                SameDirectReferencePath,
                key => key.Type.DeclaredType is ExpressBoundNamedType declaredSelect
                    && declaredSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(declaredSelect.Declaration).UnderlyingType
                        is ExpressBoundSelectType
                        ? declaredSelect.Declaration
                        : null,
                fallingPathNarrowings[0],
                fallingPathNarrowings[0]);
            IntersectCollectionFacts(
                determinateLexicals,
                static (left, right) => ReferenceEquals(left, right),
                fallingDeterminateLexicals[0],
                fallingDeterminateLexicals[0]);
            IntersectCollectionFacts(
                safeIndexPaths,
                static (left, right) => ReferenceEquals(left.Key, right.Key)
                    && ReferenceEquals(left.Value, right.Value),
                fallingSafeIndexPaths[0],
                fallingSafeIndexPaths[0]);
            for (var index = 1; index < fallingScalarNarrowings.Count; index++)
            {
                var currentScalarNarrowings = scalarNarrowings?.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase)
                    ?? new(
                        StringComparer.OrdinalIgnoreCase);
                var currentSafeIndices = safeIndices?.ToList() ?? [];
                var currentAliases = sizeAliases?.ToDictionary(pair => pair.Key, pair => pair.Value)
                    ?? [];
                var currentSelectNarrowings = selectNarrowings?.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value)
                    ?? [];
                var currentPathNarrowings = pathNarrowings?.ToList() ?? [];
                var currentDeterminateLexicals = determinateLexicals?.ToArray()
                    ?? Array.Empty<ExpressBoundName>();
                var currentSafeIndexPaths = safeIndexPaths?.ToList() ?? [];
                IntersectDictionaryFacts(
                    scalarNarrowings,
                    static (left, right) => string.Equals(
                            left.StorageCode,
                            right.StorageCode,
                            StringComparison.Ordinal)
                        && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
                        && ReferenceEquals(left.Type, right.Type),
                    currentScalarNarrowings,
                    fallingScalarNarrowings[index]);
                IntersectCollectionFacts(
                    safeIndices,
                    static (left, right) => ReferenceEquals(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    currentSafeIndices,
                    fallingSafeIndices[index]);
                IntersectDictionaryFacts(
                    sizeAliases,
                    static (left, right) => ReferenceEquals(left, right),
                    currentAliases,
                    fallingAliases[index]);
                JoinSelectFacts(
                    selectNarrowings,
                    static (left, right) => ReferenceEquals(left, right),
                    key => key.Type is ExpressBoundNamedType declaredSelect
                        && declaredSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                        && plan.Resolver.GetDefinedType(declaredSelect.Declaration).UnderlyingType
                            is ExpressBoundSelectType
                            ? declaredSelect.Declaration
                            : null,
                    currentSelectNarrowings,
                    fallingSelectNarrowings[index]);
                JoinSelectFacts(
                    pathNarrowings,
                    SameDirectReferencePath,
                    key => key.Type.DeclaredType is ExpressBoundNamedType declaredSelect
                        && declaredSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                        && plan.Resolver.GetDefinedType(declaredSelect.Declaration).UnderlyingType
                            is ExpressBoundSelectType
                            ? declaredSelect.Declaration
                            : null,
                    currentPathNarrowings,
                    fallingPathNarrowings[index]);
                IntersectCollectionFacts(
                    determinateLexicals,
                    static (left, right) => ReferenceEquals(left, right),
                    currentDeterminateLexicals,
                    fallingDeterminateLexicals[index]);
                IntersectCollectionFacts(
                    safeIndexPaths,
                    static (left, right) => ReferenceEquals(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    currentSafeIndexPaths,
                    fallingSafeIndexPaths[index]);
            }

            return true;
        }

        var conditional = operation.RequiredChild("logicalExpression").RequiredChild("expression");
        var boundCondition = plan.GetExpression(conditional);
        var conditionCode = "false";
        if (boundCondition.Kind != ExpressExpressionKind.Indeterminate)
        {
            var condition = ExpressExpressionEmitter.Emit(
                boundCondition,
                CreateContext(
                    plan,
                    selfExpression: null,
                    "entities",
                    lexicalNames,
                    safeIndices,
                    allocateTemporaryName,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings));
            conditionCode = $"({ExpressExpressionEmitter.AsLogical(boundCondition, condition.Code)}) "
                + "== global::TedToolkit.Step21.LogicalValue.True";
        }

        var thenStatements = operation.ThenStatements;
        var elseStatements = operation.ElseStatements;

        var thenAliases = sizeAliases is null
            ? null
            : new Dictionary<ExpressBoundName, ExpressBoundName>(sizeAliases);
        var thenSafeIndices = safeIndices?.ToList() ?? [];
        var thenLexicalNames = lexicalNames.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        var elseLexicalNames = lexicalNames.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        var thenScalarNarrowings = scalarNarrowings is null
            ? new Dictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>(
                StringComparer.OrdinalIgnoreCase)
            : scalarNarrowings.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        var elseScalarNarrowings = scalarNarrowings is null
            ? new Dictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>(
                StringComparer.OrdinalIgnoreCase)
            : scalarNarrowings.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        var thenSelectNarrowings = selectNarrowings is null
            ? new Dictionary<ExpressBoundName, ExpressBoundSymbol>()
            : selectNarrowings.ToDictionary(pair => pair.Key, pair => pair.Value);
        var elseSelectNarrowings = selectNarrowings is null
            ? new Dictionary<ExpressBoundName, ExpressBoundSymbol>()
            : selectNarrowings.ToDictionary(pair => pair.Key, pair => pair.Value);
        var thenPathNarrowings = pathNarrowings?.ToList()
            ?? [];
        var elsePathNarrowings = pathNarrowings?.ToList()
            ?? [];
        var thenDeterminateLexicals = determinateLexicals is null
            ? new HashSet<ExpressBoundName>()
            : new HashSet<ExpressBoundName>(determinateLexicals);
        var elseDeterminateLexicals = determinateLexicals is null
            ? new HashSet<ExpressBoundName>()
            : new HashSet<ExpressBoundName>(determinateLexicals);
        var thenSafeIndexPaths = safeIndexPaths?.ToList()
            ?? [];
        var elseSafeIndexPaths = safeIndexPaths?.ToList()
            ?? [];
        var typeName = boundCondition.Children.Count == 2 ? boundCondition.Children[0] : null;
        var typeOf = boundCondition.Children.Count == 2 ? boundCondition.Children[1] : null;
        if (boundCondition.Operation == "IN"
            && typeName?.Kind == ExpressExpressionKind.Literal
            && typeName.Type.Kind == ExpressExpressionTypeKind.String
            && typeOf?.Kind == ExpressExpressionKind.Application
            && typeOf.Operation == "TYPEOF"
            && typeOf.Children.Count == 1)
        {
            var narrowedExpression = typeOf.Children[0];
            var narrowedReference = narrowedExpression.Kind == ExpressExpressionKind.Reference
                ? narrowedExpression.Reference
                : null;
            ExpressBoundType? narrowedType = narrowedExpression.Type.DeclaredType;
            while (narrowedType is ExpressBoundNamedType namedNarrowed
                   && namedNarrowed.Declaration.Kind != ExpressDeclarationKind.Entity)
            {
                narrowedType = plan.Resolver.GetDefinedType(namedNarrowed.Declaration).UnderlyingType;
            }

            if (narrowedType is ExpressBoundSelectType narrowedSelect)
            {
                var alternatives = new List<ExpressBoundSymbol>();
                var pendingAlternatives = new Queue<ExpressBoundSymbol>(
                    plan.Resolver.GetSelectAlternatives(narrowedSelect));
                var visitedAlternatives = new HashSet<ExpressBoundSymbol>();
                var allAlternativesClosed = !narrowedSelect.IsExtensible;
                while (pendingAlternatives.Count > 0)
                {
                    var alternative = pendingAlternatives.Dequeue();
                    if (!visitedAlternatives.Add(alternative))
                    {
                        continue;
                    }

                    ExpressBoundType alternativeType = new ExpressBoundNamedType(
                        alternative,
                        narrowedSelect.Span);
                    while (alternativeType is ExpressBoundNamedType namedAlternative
                           && namedAlternative.Declaration.Kind != ExpressDeclarationKind.Entity)
                    {
                        var underlying = plan.Resolver.GetDefinedType(
                            namedAlternative.Declaration).UnderlyingType;
                        if (underlying is ExpressBoundSelectType nestedSelect)
                        {
                            allAlternativesClosed &= !nestedSelect.IsExtensible;
                            foreach (var nestedAlternative in plan.Resolver.GetSelectAlternatives(
                                         nestedSelect))
                            {
                                pendingAlternatives.Enqueue(nestedAlternative);
                            }

                            alternativeType = nestedSelect;
                            break;
                        }

                        alternativeType = underlying;
                    }

                    if (alternativeType is not ExpressBoundSelectType)
                    {
                        alternatives.Add(alternativeType is ExpressBoundNamedType namedLeaf
                            ? namedLeaf.Declaration
                            : alternative);
                    }
                }

                var qualifiedName = typeName.SourceText.Length >= 2
                    ? typeName.SourceText.Substring(1, typeName.SourceText.Length - 2)
                    : "";
                var selected = alternatives.SingleOrDefault(alternative => string.Equals(
                    alternative.DeclaringSchema.Name + "." + alternative.Name,
                    qualifiedName,
                    StringComparison.OrdinalIgnoreCase));
                if (selected is null)
                {
                    var selectedEntity = plan.EntityProjections
                        .Select(projection => projection.Entity.Symbol)
                        .SingleOrDefault(entity => string.Equals(
                            entity.DeclaringSchema.Name + "." + entity.Name,
                            qualifiedName,
                            StringComparison.OrdinalIgnoreCase));
                    if (selectedEntity is not null
                        && plan.EntityProjections.Single(projection =>
                                projection.Entity.Symbol == selectedEntity)
                            .PhysicalComponents.Any(component => alternatives.Contains(component.Symbol)))
                    {
                        selected = selectedEntity;
                    }
                }

                if (selected is not null)
                {
                    if (narrowedReference is not null)
                    {
                        thenSelectNarrowings[narrowedReference] = selected;
                    }
                    else
                    {
                        thenPathNarrowings.Add(new(narrowedExpression, selected));
                    }

                    var remaining = alternatives.Where(alternative => alternative != selected).ToArray();
                    if (alternatives.Contains(selected)
                        && allAlternativesClosed
                        && remaining.Length == 1)
                    {
                        if (narrowedReference is not null)
                        {
                            elseSelectNarrowings[narrowedReference] = remaining[0];
                        }
                        else
                        {
                            elsePathNarrowings.Add(new(narrowedExpression, remaining[0]));
                        }
                    }
                }
                else
                {
                    ExpressScalarKind? selectedScalarKind = qualifiedName.ToUpperInvariant() switch
                    {
                        "INTEGER" => ExpressScalarKind.Integer,
                        "NUMBER" => ExpressScalarKind.Number,
                        "REAL" => ExpressScalarKind.Real,
                        "STRING" => ExpressScalarKind.String,
                        _ => null,
                    };
                    if (selectedScalarKind is not null
                        && narrowedReference is not null
                        && lexicalNames.TryGetValue(narrowedReference.Name, out var lexicalName))
                    {
                        var scalarAlternatives = new List<(ExpressBoundSymbol Alternative, ExpressBoundScalarType Type)>();
                        foreach (var alternative in alternatives)
                        {
                            ExpressBoundType terminalType = new ExpressBoundNamedType(
                                alternative,
                                narrowedSelect.Span);
                            while (terminalType is ExpressBoundNamedType namedTerminal
                                   && namedTerminal.Declaration.Kind != ExpressDeclarationKind.Entity)
                            {
                                terminalType = plan.Resolver.GetDefinedType(
                                    namedTerminal.Declaration).UnderlyingType;
                            }

                            if (terminalType is ExpressBoundScalarType scalarTerminal)
                            {
                                scalarAlternatives.Add((alternative, scalarTerminal));
                            }
                        }

                        var matching = scalarAlternatives
                            .Where(candidate => candidate.Type.Kind == selectedScalarKind
                                || (candidate.Type.Kind == ExpressScalarKind.Number
                                    && selectedScalarKind is ExpressScalarKind.Integer or ExpressScalarKind.Real))
                            .ToArray();
                        if (matching.Length > 0)
                        {
                            var terminalType = new ExpressBoundScalarType(
                                selectedScalarKind.Value,
                                constraintText: null,
                                isFixed: false,
                                narrowedSelect.Span);
                            thenScalarNarrowings[narrowedReference.Name] = (
                                lexicalName.Code,
                                ResolveReference(
                                    plan,
                                    narrowedReference,
                                    selfExpression: null,
                                    "entities",
                                    lexicalNames,
                                    selectNarrowings,
                                    lexicalName.Type,
                                    lexicalName.Code,
                                    narrowedScalarType: terminalType),
                                terminalType);
                        }

                        var remaining = scalarAlternatives
                            .Select(candidate => candidate.Type.Kind switch
                            {
                                var kind when kind == selectedScalarKind => (ExpressScalarKind?)null,
                                ExpressScalarKind.Number when selectedScalarKind == ExpressScalarKind.Real =>
                                    ExpressScalarKind.Integer,
                                ExpressScalarKind.Number when selectedScalarKind == ExpressScalarKind.Integer =>
                                    ExpressScalarKind.Real,
                                var kind => kind,
                            })
                            .Where(kind => kind is not null)
                            .ToArray();
                        ExpressScalarKind? remainingKind = null;
                        if (remaining.Length > 0 && remaining.All(kind => kind == remaining[0]))
                        {
                            remainingKind = remaining[0];
                        }
                        else if (remaining.Length > 0
                                 && remaining.All(kind => kind is ExpressScalarKind.Integer
                                     or ExpressScalarKind.Number
                                     or ExpressScalarKind.Real))
                        {
                            remainingKind = ExpressScalarKind.Number;
                        }

                        if (allAlternativesClosed
                            && scalarAlternatives.Count == alternatives.Count
                            && remainingKind is not null)
                        {
                            var terminalType = new ExpressBoundScalarType(
                                remainingKind.Value,
                                constraintText: null,
                                isFixed: false,
                                narrowedSelect.Span);
                            elseScalarNarrowings[narrowedReference.Name] = (
                                lexicalName.Code,
                                ResolveReference(
                                    plan,
                                    narrowedReference,
                                    selfExpression: null,
                                    "entities",
                                    lexicalNames,
                                    selectNarrowings,
                                    lexicalName.Type,
                                    lexicalName.Code,
                                    narrowedScalarType: terminalType),
                                terminalType);
                        }
                    }
                }
            }
        }

        var sizeOf = boundCondition.Children.Count == 2
            ? boundCondition.Children[0]
            : null;
        if (boundCondition.Operation == ">"
            && boundCondition.Children.Count == 2
            && sizeOf?.Kind == ExpressExpressionKind.Application
            && sizeOf.Operation == "SIZEOF"
            && sizeOf.Children.Count == 1
            && sizeOf.Children[0].Kind == ExpressExpressionKind.Reference
            && sizeOf.Children[0].Reference is { } determinateReference
            && boundCondition.Children[1].Kind == ExpressExpressionKind.Literal
            && System.Numerics.BigInteger.TryParse(
                boundCondition.Children[1].SourceText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var guardBound)
            && guardBound == System.Numerics.BigInteger.Zero)
        {
            thenDeterminateLexicals.Add(determinateReference);
        }

        var conditionalStatement = new IfStatement(new CustomExpression(conditionCode));
        var thenOwner = new IfStatement(new CustomExpression("true"));
        var thenFallsThrough = true;
        foreach (var nested in thenStatements)
        {
            if (!thenFallsThrough)
            {
                break;
            }

            thenFallsThrough = EmitFunctionStatement(
                plan,
                nested,
                functionResultType,
                canReturnIndeterminate,
                thenOwner,
                thenLexicalNames,
                allocateTemporaryName,
                thenSafeIndices,
                thenAliases,
                thenSelectNarrowings,
                thenPathNarrowings,
                thenDeterminateLexicals,
                thenSafeIndexPaths,
                thenScalarNarrowings,
                loopTransfers);
        }

        foreach (var thenStatement in thenOwner.Statements)
        {
            conditionalStatement.AddStatement(thenStatement);
        }

        if (elseStatements.Count == 0)
        {
            var implicitElseScalarNarrowings = scalarNarrowings?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase)
                ?? new(
                    StringComparer.OrdinalIgnoreCase);
            if (thenFallsThrough)
            {
                IntersectDictionaryFacts(
                    scalarNarrowings,
                    static (left, right) => string.Equals(
                            left.StorageCode,
                            right.StorageCode,
                            StringComparison.Ordinal)
                        && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
                        && ReferenceEquals(left.Type, right.Type),
                    thenScalarNarrowings,
                    implicitElseScalarNarrowings);
                IntersectCollectionFacts(
                    safeIndices,
                    static (left, right) => ReferenceEquals(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    thenSafeIndices,
                    safeIndices?.ToList() ?? []);
                IntersectDictionaryFacts(
                    sizeAliases,
                    static (left, right) => ReferenceEquals(left, right),
                    thenAliases ?? [],
                    sizeAliases?.ToDictionary(pair => pair.Key, pair => pair.Value)
                        ?? []);
                JoinSelectFacts(
                    selectNarrowings,
                    static (left, right) => ReferenceEquals(left, right),
                    key => key.Type is ExpressBoundNamedType declaredSelect
                        && declaredSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                        && plan.Resolver.GetDefinedType(declaredSelect.Declaration).UnderlyingType
                            is ExpressBoundSelectType
                            ? declaredSelect.Declaration
                            : null,
                    thenSelectNarrowings,
                    selectNarrowings?.ToDictionary(pair => pair.Key, pair => pair.Value)
                        ?? []);
                JoinSelectFacts(
                    pathNarrowings,
                    SameDirectReferencePath,
                    key => key.Type.DeclaredType is ExpressBoundNamedType declaredSelect
                        && declaredSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                        && plan.Resolver.GetDefinedType(declaredSelect.Declaration).UnderlyingType
                            is ExpressBoundSelectType
                            ? declaredSelect.Declaration
                            : null,
                    thenPathNarrowings,
                    pathNarrowings?.ToList() ?? []);
                IntersectCollectionFacts(
                    determinateLexicals,
                    static (left, right) => ReferenceEquals(left, right),
                    thenDeterminateLexicals,
                    determinateLexicals?.ToArray() ?? Array.Empty<ExpressBoundName>());
                IntersectCollectionFacts(
                    safeIndexPaths,
                    static (left, right) => ReferenceEquals(left.Key, right.Key)
                        && ReferenceEquals(left.Value, right.Value),
                    thenSafeIndexPaths,
                    safeIndexPaths?.ToList() ?? []);
            }

            owner.AddStatement(conditionalStatement);
            return true;
        }

        var elseAliases = sizeAliases is null
            ? null
            : new Dictionary<ExpressBoundName, ExpressBoundName>(sizeAliases);
        var elseSafeIndices = safeIndices?.ToList() ?? [];
        var elseOwner = new IfStatement(new CustomExpression("true"));
        var elseFallsThrough = true;
        foreach (var nested in elseStatements)
        {
            if (!elseFallsThrough)
            {
                break;
            }

            elseFallsThrough = EmitFunctionStatement(
                plan,
                nested,
                functionResultType,
                canReturnIndeterminate,
                elseOwner,
                elseLexicalNames,
                allocateTemporaryName,
                elseSafeIndices,
                elseAliases,
                elseSelectNarrowings,
                elsePathNarrowings,
                elseDeterminateLexicals,
                elseSafeIndexPaths,
                elseScalarNarrowings,
                loopTransfers);
        }

        var elseStatement = conditionalStatement.Else();
        foreach (var nestedElseStatement in elseOwner.Statements)
        {
            elseStatement.AddStatement(nestedElseStatement);
        }

        if (thenFallsThrough || elseFallsThrough)
        {
            var leftScalarNarrowings = thenFallsThrough
                ? thenScalarNarrowings
                : elseScalarNarrowings;
            var rightScalarNarrowings = elseFallsThrough
                ? elseScalarNarrowings
                : thenScalarNarrowings;
            var leftSafeIndices = thenFallsThrough ? thenSafeIndices : elseSafeIndices;
            var rightSafeIndices = elseFallsThrough ? elseSafeIndices : thenSafeIndices;
            var leftAliases = thenFallsThrough ? thenAliases : elseAliases;
            var rightAliases = elseFallsThrough ? elseAliases : thenAliases;
            var leftSelectNarrowings = thenFallsThrough ? thenSelectNarrowings : elseSelectNarrowings;
            var rightSelectNarrowings = elseFallsThrough ? elseSelectNarrowings : thenSelectNarrowings;
            var leftPathNarrowings = thenFallsThrough ? thenPathNarrowings : elsePathNarrowings;
            var rightPathNarrowings = elseFallsThrough ? elsePathNarrowings : thenPathNarrowings;
            var leftDeterminateLexicals = thenFallsThrough
                ? thenDeterminateLexicals
                : elseDeterminateLexicals;
            var rightDeterminateLexicals = elseFallsThrough
                ? elseDeterminateLexicals
                : thenDeterminateLexicals;
            var leftSafeIndexPaths = thenFallsThrough ? thenSafeIndexPaths : elseSafeIndexPaths;
            var rightSafeIndexPaths = elseFallsThrough ? elseSafeIndexPaths : thenSafeIndexPaths;
            IntersectDictionaryFacts(
                scalarNarrowings,
                static (left, right) => string.Equals(
                        left.StorageCode,
                        right.StorageCode,
                        StringComparison.Ordinal)
                    && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
                    && ReferenceEquals(left.Type, right.Type),
                leftScalarNarrowings,
                rightScalarNarrowings);
            IntersectCollectionFacts(
                safeIndices,
                static (left, right) => ReferenceEquals(left.Key, right.Key)
                    && ReferenceEquals(left.Value, right.Value),
                leftSafeIndices,
                rightSafeIndices);
            IntersectDictionaryFacts(
                sizeAliases,
                static (left, right) => ReferenceEquals(left, right),
                leftAliases ?? [],
                rightAliases ?? []);
            JoinSelectFacts(
                selectNarrowings,
                static (left, right) => ReferenceEquals(left, right),
                key => key.Type is ExpressBoundNamedType declaredSelect
                    && declaredSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(declaredSelect.Declaration).UnderlyingType
                        is ExpressBoundSelectType
                        ? declaredSelect.Declaration
                        : null,
                leftSelectNarrowings,
                rightSelectNarrowings);
            JoinSelectFacts(
                pathNarrowings,
                SameDirectReferencePath,
                key => key.Type.DeclaredType is ExpressBoundNamedType declaredSelect
                    && declaredSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(declaredSelect.Declaration).UnderlyingType
                        is ExpressBoundSelectType
                        ? declaredSelect.Declaration
                        : null,
                leftPathNarrowings,
                rightPathNarrowings);
            IntersectCollectionFacts(
                determinateLexicals,
                static (left, right) => ReferenceEquals(left, right),
                leftDeterminateLexicals,
                rightDeterminateLexicals);
            IntersectCollectionFacts(
                safeIndexPaths,
                static (left, right) => ReferenceEquals(left.Key, right.Key)
                    && ReferenceEquals(left.Value, right.Value),
                leftSafeIndexPaths,
                rightSafeIndexPaths);
        }

        owner.AddStatement(conditionalStatement);
        return thenFallsThrough || elseFallsThrough;
    }

    private static Custom CreateAssignmentStatement(
        ExpressReachableRulePlan plan,
        string targetCode,
        string valueCode,
        string? assignedEntityCode,
        ExpressBoundNamedType? assignedEntityType,
        ExpressBoundAttribute? assignedAttribute,
        string assignedMemberSuffix,
        int assignedAggregateDepth,
        ExpressBoundType assignedTargetType)
    {
        if (assignedEntityCode is null || assignedEntityType is null || assignedAttribute is null)
        {
            return new((ref SourceBuilder source) =>
                source.AppendLine($"{targetCode} = {valueCode};"));
        }

        var ownerProjection = plan.EntityProjections.Single(projection => ReferenceEquals(
            projection.Entity.Symbol,
            assignedEntityType.Declaration));
        var ownerAttribute = ownerProjection.FlattenedAttributes.Single(attribute => ReferenceEquals(
            attribute.Attribute,
            assignedAttribute));
        bool SameStorage(ExpressEntityAttributeProjection attribute)
        {
            return ReferenceEquals(attribute.StorageEntity.Symbol, ownerAttribute.StorageEntity.Symbol)
                && StringComparer.OrdinalIgnoreCase.Equals(
                    attribute.StorageAttributeName,
                    ownerAttribute.StorageAttributeName);
        }

        string AdaptValue(ExpressBoundType branchType)
        {
            if (assignedAggregateDepth == 0
                && plan.Resolver.GetAggregateType(branchType) is { } branchAggregate
                && plan.Resolver.GetAggregateType(assignedAttribute.Type) is { } declaredAggregate
                && branchAggregate.Kind == declaredAggregate.Kind
                && branchAggregate.ElementType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } branchElement
                && (declaredAggregate.ElementType is ExpressBoundGenericType { IsEntity: true, }
                    || (declaredAggregate.ElementType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } declaredElement
                        && plan.EntityProjections.Single(projection =>
                                projection.Entity.Symbol == branchElement.Declaration)
                            .PhysicalComponents.Any(component =>
                                component.Symbol == declaredElement.Declaration))))
            {
                const string item = "__expressAssignedElement";
                const string typed = "__expressTypedAssignedElement";
                var branchElementName = ExpressExpressionEmitter.BoundTypeName(branchElement);
                return $"({ExpressExpressionEmitter.BoundTypeName(branchAggregate)})"
                    + $"[..global::System.Linq.Enumerable.Select(({valueCode}), {item} => "
                    + $"((object)({item})) switch {{ {branchElementName} {typed} => {typed}, "
                    + "_ => throw new global::System.InvalidOperationException() })]";
            }

            var branchTargetType = branchType;
            for (var depth = 0; depth < assignedAggregateDepth; depth++)
            {
                if (plan.Resolver.GetAggregateType(branchTargetType) is not { } indexedAggregate)
                {
                    return valueCode;
                }

                branchTargetType = indexedAggregate.ElementType;
            }

            if (branchTargetType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } branchEntity
                && assignedTargetType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } declaredEntity
                && !ReferenceEquals(branchEntity.Declaration, declaredEntity.Declaration))
            {
                return $"({ExpressExpressionEmitter.BoundTypeName(branchEntity)})({valueCode})";
            }

            return valueCode;
        }

        var targets = plan.EntityProjections
            .Where(projection => projection.PhysicalComponents.Any(component => ReferenceEquals(
                component.Symbol,
                assignedEntityType.Declaration)))
            .Select(projection => (
                Type: "global::TedToolkit.Step21.Generated."
                    + ExpressEntityProjection.ToPascalCase(projection.Schema.Identity.Name)
                    + "."
                    + projection.Name,
                Attribute: projection.EffectiveAttributes.SingleOrDefault(SameStorage)))
            .Where(target => target.Attribute is not null)
            .Select(target => (target.Type, Attribute: target.Attribute!))
            .Concat(plan.ComplexEntityProjections
                .Where(projection => projection.Components.Any(component => ReferenceEquals(
                    component.Entity.Symbol,
                    assignedEntityType.Declaration)))
                .Select(projection => (
                    Type: "global::TedToolkit.Step21.Generated."
                        + ExpressEntityProjection.ToPascalCase(projection.Schema.Identity.Name)
                        + "."
                        + projection.Name,
                    Attribute: projection.Properties.SingleOrDefault(SameStorage)))
                .Where(target => target.Attribute is not null)
                .Select(target => (target.Type, Attribute: target.Attribute!)))
            .GroupBy(target => target.Type, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return new((ref SourceBuilder source) =>
        {
            source.AppendLine($"switch ({assignedEntityCode})");
            source.AppendLine("{");
            for (var index = 0; index < targets.Length; index++)
            {
                var variable = "__expressAssignmentTarget" + index.ToString(CultureInfo.InvariantCulture);
                source.AppendLine($"case {targets[index].Type} {variable}:");
                source.AppendLine($"{variable}.{targets[index].Attribute.StorageMemberName}{assignedMemberSuffix} = "
                    + $"{AdaptValue(targets[index].Attribute.Type)};");
                source.AppendLine("break;");
            }

            source.AppendLine("default:");
            source.AppendLine("throw new global::System.InvalidOperationException("
                + "\"EXPRESS assignment target has no mutable generated entity projection.\");");
            source.AppendLine("}");
        });
    }

    private static void EmitProcedureCall(
        ExpressReachableRulePlan plan,
        ExpressSemanticRule procedureReference,
        ExpressBoundExpression[] arguments,
        ExpressExpressionEmissionContext context,
        Dictionary<string, (string Code, ExpressBoundType Type)> lexicalNames,
        IStatementOwner owner)
    {
        var symbol = plan.Schema.NameReferences
            .Where(reference => SameStart(reference.Span, procedureReference.Span))
            .Select(reference => reference.Target.SchemaDeclaration)
            .OfType<ExpressBoundSymbol>()
            .Distinct()
            .Single();
        var declaration = (ExpressBoundOpaqueDeclaration)plan.GetDeclaration(symbol);
        var parameters = ProcedureParameters(plan, declaration);
        if (parameters.Count != arguments.Length)
        {
            throw new InvalidOperationException("An EXPRESS procedure call has an invalid argument count.");
        }

        var emitted = new string[arguments.Length];
        var presenceConditions = new List<string>();
        var procedureVariables = new List<(DataType Type, string Name, string Initializer)>();
        var copyBacks = new List<(string Target, string Value)>();
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            var parameter = parameters[index];
            var code = ExpressExpressionEmitter.Emit(argument, context).Code;
            if (parameter.Name.Type is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } targetEntity
                && ResolveSelectCarrierType(plan, argument) is not null)
            {
                code = (context.ResolveSelectToEntityValue?.Invoke(argument, code, targetEntity) ?? code) + "!";
            }

            var argumentCanBeIndeterminate = argument.Type.CanBeIndeterminate
                && context.IsKnownDeterminate?.Invoke(argument) != true;
            if (parameter.Name.Type is ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, }
                && argument.Type.Kind == ExpressExpressionTypeKind.Boolean)
            {
                code = ExpressExpressionEmitter.AsLogical(argument, code);
                argumentCanBeIndeterminate = false;
            }
            else if (parameter.Name.Type is ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, }
                     && (argument.Type.Kind == ExpressExpressionTypeKind.Logical
                        || IsLogicalOperatorExpression(argument)))
            {
                if (IsDeterminateBooleanExpression(argument))
                {
                    code = $"(({code}) switch {{ "
                        + "global::TedToolkit.Step21.LogicalValue.True => true, "
                        + "global::TedToolkit.Step21.LogicalValue.False => false, "
                        + "_ => throw new global::System.InvalidOperationException() })";
                    argumentCanBeIndeterminate = false;
                }
                else
                {
                    code = $"(({code}) switch {{ "
                        + "global::TedToolkit.Step21.LogicalValue.True => true, "
                        + "global::TedToolkit.Step21.LogicalValue.False => false, "
                        + "_ => (global::System.Boolean?)null })";
                    argumentCanBeIndeterminate = true;
                }
            }

            if (argumentCanBeIndeterminate)
            {
                var present = context.AllocateTemporaryName("__expressProcedureArgument");
                presenceConditions.Add($"({code}) is {{ }} {present}");
                code = present;
            }

            if (!parameter.IsVar)
            {
                emitted[index] = code;
                continue;
            }

            var temporary = context.AllocateTemporaryName("__expressProcedureVariable");
            procedureVariables.Add((
                new DataType(ExpressExpressionEmitter.BoundTypeName(parameter.Name.Type!)),
                temporary,
                code));
            emitted[index] = "ref " + temporary;
            var copyBackTarget = argument.Kind == ExpressExpressionKind.Reference
                && argument.Reference is not null
                && lexicalNames.TryGetValue(argument.Reference.Name, out var lexical)
                    ? lexical.Code
                    : code;
            copyBacks.Add((copyBackTarget, temporary));
        }

        var invocationOwner = owner;
        if (presenceConditions.Count > 0)
        {
            var presence = new IfStatement(new CustomExpression(string.Join(" && ", presenceConditions)));
            owner.AddStatement(presence);
            invocationOwner = presence;
        }

        foreach (var variable in procedureVariables)
        {
            invocationOwner.AddStatement(new VariableExpression(variable.Type, variable.Name)
                .AddDefault(new CustomExpression(variable.Initializer)));
        }

        var invocationArguments = emitted.Append("entities" + ValidationContextArgumentSuffix(plan));
        invocationOwner.AddStatement(new CustomExpression(
            $"{ProcedureMethodName(plan, symbol)}({string.Join(", ", invocationArguments)})"));
        foreach (var copyBack in copyBacks)
        {
            invocationOwner.AddStatement(new CustomExpression($"{copyBack.Target} = {copyBack.Value}"));
        }
    }

    private static bool EmitListProcedure(
        ExpressReachableRulePlan plan,
        string procedureName,
        ExpressBoundExpression[] arguments,
        ExpressExpressionEmissionContext context,
        IStatementOwner owner,
        ExpressBoundType? functionResultType,
        bool canReturnIndeterminate)
    {
        var codes = new string[arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            var expression = arguments[index];
            var emitted = ExpressExpressionEmitter.Emit(expression, context);
            var variable = context.AllocateTemporaryName("__expressProcedureArgument");
            var containsGroupQualification = expression.DescendantsAndSelf().Any(candidate =>
                candidate.Kind == ExpressExpressionKind.GroupQualifier
                && candidate.Children.Count == 1);
            if ((CanEmitIndeterminate(plan, expression)
                    && context.IsKnownDeterminate?.Invoke(expression) != true)
                || containsGroupQualification)
            {
                if (!canReturnIndeterminate)
                {
                    throw new InvalidOperationException(
                        "A list procedure with indeterminate arguments requires a nullable function result.");
                }

                var unknown = functionResultType is ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, }
                    ? "global::TedToolkit.Step21.LogicalValue.Unknown"
                    : "null";
                if (expression.Kind == ExpressExpressionKind.Indeterminate)
                {
                    owner.AddStatement(new CustomExpression(unknown).Return);
                    return false;
                }

                owner.AddStatement(new IfStatement(new CustomExpression($"({emitted.Code}) is not {{ }} {variable}"))
                    .AddStatement(new CustomExpression(unknown).Return));
            }
            else
            {
                owner.AddStatement(new VariableExpression(DataType.Var, variable)
                    .AddDefault(new CustomExpression(emitted.Code)));
            }

            codes[index] = variable;
        }

        var position = codes[codes.Length - 1];
        if (arguments[arguments.Length - 1].Type.Kind == ExpressExpressionTypeKind.Number)
        {
            position = $"({position}).ToIntegerTruncated()";
        }

        if (string.Equals(procedureName, "INSERT", StringComparison.OrdinalIgnoreCase))
        {
            var aggregate = plan.Resolver.GetAggregateType(arguments[0].Type.DeclaredType!)!;
            var element = ExpressExpressionEmitter.ConvertAggregateElement(
                arguments[1],
                codes[1],
                aggregate.ElementType,
                context,
                sourceIsDeterminate: true);
            owner.AddStatement(codes[0].ToSimpleName().Sub("Insert").Invoke()
                .AddArgument(SourceComposer.Argument(new CustomExpression($"checked((int)({position}))")))
                .AddArgument(SourceComposer.Argument(new CustomExpression(element))));
        }
        else
        {
            owner.AddStatement(codes[0].ToSimpleName().Sub("RemoveAt").Invoke()
                .AddArgument(SourceComposer.Argument(new CustomExpression(
                    $"checked((int)(({position}) - global::System.Numerics.BigInteger.One))"))));
        }

        return true;
    }

    private static string EmitRepeatCondition(
        ExpressReachableRulePlan plan,
        ExpressSemanticRule control,
        ExpressExpressionEmissionContext context)
    {
        var condition = plan.GetExpression(control.RequiredChild("logicalExpression").RequiredChild("expression"));
        if (condition.Kind == ExpressExpressionKind.Indeterminate)
        {
            return "false";
        }

        var emitted = ExpressExpressionEmitter.Emit(condition, context);
        return $"({ExpressExpressionEmitter.AsLogical(condition, emitted.Code)}) "
            + "== global::TedToolkit.Step21.LogicalValue.True";
    }

    private static bool EmitNonIncrementRepeat(
        ExpressReachableRulePlan plan,
        ExpressSemanticRule operation,
        ExpressBoundType? functionResultType,
        bool canReturnIndeterminate,
        IStatementOwner owner,
        Dictionary<string, (string Code, ExpressBoundType Type)> lexicalNames,
        Func<string, string> allocateTemporaryName,
        List<KeyValuePair<ExpressBoundName, ExpressBoundName>>? safeIndices,
        IDictionary<ExpressBoundName, ExpressBoundName>? sizeAliases,
        Dictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings,
        List<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>? pathNarrowings,
        ISet<ExpressBoundName>? determinateLexicals,
        List<KeyValuePair<ExpressBoundExpression, ExpressBoundName>>? safeIndexPaths,
        Dictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>? scalarNarrowings)
    {
        var control = operation.RequiredChild("repeatControl");
        var whileControl = control.ChildRules("whileControl").SingleOrDefault();
        var untilControl = control.ChildRules("untilControl").SingleOrDefault();

        var incomingSafeIndices = safeIndices?.ToList() ?? [];
        var incomingAliases = sizeAliases?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [];
        var incomingSelectNarrowings = selectNarrowings?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [];
        var incomingPathNarrowings = pathNarrowings?.ToList() ?? [];
        var incomingDeterminateLexicals = determinateLexicals?.ToArray() ?? Array.Empty<ExpressBoundName>();
        var incomingSafeIndexPaths = safeIndexPaths?.ToList() ?? [];
        var incomingScalarNarrowings = scalarNarrowings?.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase)
            ?? new(StringComparer.OrdinalIgnoreCase);
        var nestedSafeIndices = incomingSafeIndices.ToList();
        var nestedAliases = incomingAliases.ToDictionary(pair => pair.Key, pair => pair.Value);
        var nestedSelectNarrowings = incomingSelectNarrowings.ToDictionary(pair => pair.Key, pair => pair.Value);
        var nestedPathNarrowings = incomingPathNarrowings.ToList();
        var nestedDeterminateLexicals = new HashSet<ExpressBoundName>(incomingDeterminateLexicals);
        var nestedSafeIndexPaths = incomingSafeIndexPaths.ToList();
        var nestedScalarNarrowings = incomingScalarNarrowings.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        var nestedNames = lexicalNames.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

        var whileCode = whileControl is null
            ? null
            : EmitRepeatCondition(
                plan,
                whileControl,
                CreateContext(
                    plan,
                    selfExpression: null,
                    "entities",
                    lexicalNames,
                    safeIndices,
                    allocateTemporaryName,
                    selectNarrowings,
                    pathNarrowings,
                    determinateLexicals,
                    safeIndexPaths,
                    scalarNarrowings));

        var repeatTransfers = CreateLoopTransfers(operation, allocateTemporaryName);
        var bodyOwner = new IfStatement(new CustomExpression("true"));
        var bodyFallsThrough = true;
        foreach (var nested in operation.ChildRules("stmt"))
        {
            if (!bodyFallsThrough)
            {
                break;
            }

            bodyFallsThrough = EmitFunctionStatement(
                plan,
                nested,
                functionResultType,
                canReturnIndeterminate,
                bodyOwner,
                nestedNames,
                allocateTemporaryName,
                nestedSafeIndices,
                nestedAliases,
                nestedSelectNarrowings,
                nestedPathNarrowings,
                nestedDeterminateLexicals,
                nestedSafeIndexPaths,
                nestedScalarNarrowings,
                repeatTransfers);
        }

        if (repeatTransfers is not null)
        {
            bodyFallsThrough = MergeLoopPaths(plan, repeatTransfers.SkipPaths, bodyFallsThrough,
                nestedSafeIndices, nestedAliases, nestedSelectNarrowings, nestedPathNarrowings,
                nestedDeterminateLexicals, nestedSafeIndexPaths, nestedScalarNarrowings);
        }

        var untilCode = bodyFallsThrough && untilControl is not null
            ? EmitRepeatCondition(
                plan,
                untilControl,
                CreateContext(
                    plan,
                    selfExpression: null,
                    "entities",
                    nestedNames,
                    nestedSafeIndices,
                    allocateTemporaryName,
                    nestedSelectNarrowings,
                    nestedPathNarrowings,
                    nestedDeterminateLexicals,
                    nestedSafeIndexPaths,
                    nestedScalarNarrowings))
            : null;

        if (repeatTransfers is not null)
        {
            bodyFallsThrough = MergeLoopPaths(plan, repeatTransfers.ExitPaths, bodyFallsThrough,
                nestedSafeIndices, nestedAliases, nestedSelectNarrowings, nestedPathNarrowings,
                nestedDeterminateLexicals, nestedSafeIndexPaths, nestedScalarNarrowings);
        }

        if (bodyFallsThrough)
        {
            IntersectDictionaryFacts(
                scalarNarrowings,
                static (left, right) => string.Equals(left.StorageCode, right.StorageCode, StringComparison.Ordinal)
                    && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
                    && ReferenceEquals(left.Type, right.Type),
                incomingScalarNarrowings,
                nestedScalarNarrowings);
            IntersectCollectionFacts(
                safeIndices,
                static (left, right) => ReferenceEquals(left.Key, right.Key)
                    && ReferenceEquals(left.Value, right.Value),
                incomingSafeIndices,
                nestedSafeIndices);
            IntersectDictionaryFacts(
                sizeAliases,
                static (left, right) => ReferenceEquals(left, right),
                incomingAliases,
                nestedAliases);
            JoinSelectFacts(
                selectNarrowings,
                static (left, right) => ReferenceEquals(left, right),
                key => key.Type is ExpressBoundNamedType declaredSelect
                    && declaredSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(declaredSelect.Declaration).UnderlyingType is ExpressBoundSelectType
                        ? declaredSelect.Declaration
                        : null,
                incomingSelectNarrowings,
                nestedSelectNarrowings);
            JoinSelectFacts(
                pathNarrowings,
                SameDirectReferencePath,
                key => key.Type.DeclaredType is ExpressBoundNamedType declaredSelect
                    && declaredSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(declaredSelect.Declaration).UnderlyingType is ExpressBoundSelectType
                        ? declaredSelect.Declaration
                        : null,
                incomingPathNarrowings,
                nestedPathNarrowings);
            IntersectCollectionFacts(
                determinateLexicals,
                static (left, right) => ReferenceEquals(left, right),
                incomingDeterminateLexicals,
                nestedDeterminateLexicals);
            IntersectCollectionFacts(
                safeIndexPaths,
                static (left, right) => ReferenceEquals(left.Key, right.Key)
                    && ReferenceEquals(left.Value, right.Value),
                incomingSafeIndexPaths,
                nestedSafeIndexPaths);
        }

        owner.AddStatement(new Custom((ref SourceBuilder source) =>
        {
            source.Append($"while ({whileCode ?? "true"})");
            source.BeginBlock();
            foreach (var bodyStatement in bodyOwner.Statements)
            {
                bodyStatement.ToCode(ref source);
                source.AppendLine();
            }

            if (repeatTransfers is { SkipPaths.Count: > 0, })
            {
                source.AppendLine($"{repeatTransfers.SkipLabel}:;");
            }

            if (untilCode is not null)
            {
                source.Append($"if ({untilCode})");
                source.BeginBlock();
                source.AppendLine("break;");
                source.EndBlock();
            }

            source.EndBlock();
        }));
        return whileControl is not null || bodyFallsThrough;
    }

    private static Method CreateDerivedMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundAttribute attribute,
        ExpressGeneratedTypeResolver resolver)
    {
        var owner = plan.GetAttributeOwner(attribute);
        var ownerName = ExpressEntityProjection.ToPascalCase(owner.Name);
        var expression = plan.GetDerivedExpression(attribute);
        var returnType = resolver.Resolve(plan.Schema.Identity, attribute.Type).DataType;
        if (expression.Type.CanBeIndeterminate)
        {
            returnType = returnType.Null;
        }

        var method = CreateMethod(
            DerivedMethodName(owner, attribute),
            returnType);
        method.AddParameter(SourceComposer.Parameter(
            new DataType($"I{ownerName}"),
            "value"));
        var temporaryOrdinal = 0;
        Func<string, string> allocateTemporaryName = prefix => prefix
            + (temporaryOrdinal++).ToString(CultureInfo.InvariantCulture);
        AddPopulationParameter(method);
        AddValidationContextParameters(method, plan);
        var generated = ExpressExpressionEmitter.Emit(
            expression,
            CreateContext(
                plan,
                "value",
                "entities",
                allocateTemporaryName: allocateTemporaryName,
                currentDerivedAttribute: attribute));
        var generatedCode = generated.Code
            ?? throw new InvalidOperationException("A derived expression must emit a value.");
        var derivedValueTarget = attribute.Type;
        var derivedValueWrappers = ResolveTransparentDefinedWrappers(
            plan.Resolver,
            ref derivedValueTarget);
        if (derivedValueTarget is ExpressBoundScalarType derivedScalarTarget
            && expression.Type.DeclaredType is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, } derivedScalarCarrier
            && plan.Resolver.GetDefinedType(derivedScalarCarrier.Declaration).UnderlyingType
                is ExpressBoundSelectType)
        {
            var projected = ResolveSelectScalarValue(
                plan,
                derivedScalarCarrier,
                generatedCode,
                derivedScalarTarget);
            var present = allocateTemporaryName("__expressDerivedScalar");
            var presentCode = present;
            for (var index = derivedValueWrappers.Count - 1; index >= 0; index--)
            {
                presentCode = "new "
                    + ExpressExpressionEmitter.BoundTypeName(derivedValueWrappers[index])
                    + $"({presentCode})";
            }

            generatedCode = $"(({projected}) is {{ }} {present} ? {presentCode} : throw new "
                + "global::System.InvalidOperationException())";
        }

        if (attribute.Type is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, } derivedSelectedEntityTarget
            && expression.Type.DeclaredType is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, } derivedSelectedEntityCarrier
            && plan.Resolver.GetDefinedType(derivedSelectedEntityCarrier.Declaration).UnderlyingType
                is ExpressBoundSelectType
            && ResolveSelectToEntityValue(
                plan,
                derivedSelectedEntityTarget,
                derivedSelectedEntityCarrier,
                generatedCode,
                "__expressDerivedEntity") is { } selectedEntity)
        {
            generatedCode = selectedEntity;
        }

        if (attribute.Type is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, } derivedTargetSelect
            && plan.Resolver.GetDefinedType(derivedTargetSelect.Declaration).UnderlyingType
                is ExpressBoundSelectType
            && expression.Type.DeclaredType is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, } derivedSourceSelect
            && !ReferenceEquals(derivedTargetSelect.Declaration, derivedSourceSelect.Declaration)
            && plan.Resolver.GetDefinedType(derivedSourceSelect.Declaration).UnderlyingType
                is ExpressBoundSelectType
            && ResolveSelectToSelectValue(
                plan,
                derivedTargetSelect,
                derivedSourceSelect,
                generatedCode,
                "__expressDerivedSelect") is { } selectedDerivedValue)
        {
            generatedCode = selectedDerivedValue;
        }

        if (plan.Resolver.GetAggregateType(attribute.Type) is { } selectedDerivedAggregateTarget
            && ResolveSelectCarrierType(plan, expression) is { } selectedDerivedAggregateCarrier
            && ResolveAggregateSource(
                plan,
                selectedDerivedAggregateCarrier,
                generatedCode,
                selectedDerivedAggregateTarget,
                allowIncompatible: true) is { } selectedDerivedAggregate)
        {
            var presentAggregate = allocateTemporaryName("__expressDerivedSelectedAggregate");
            generatedCode = $"(({selectedDerivedAggregate}) is {{ }} {presentAggregate} ? ("
                + ExpressExpressionEmitter.BoundTypeName(selectedDerivedAggregateTarget)
                + $")[..{presentAggregate}] : ("
                + ExpressExpressionEmitter.BoundTypeName(selectedDerivedAggregateTarget)
                + "?)null)";
        }
        else if (plan.Resolver.GetAggregateType(attribute.Type) is { } emptyDerivedAggregateTarget
            && expression.Kind == ExpressExpressionKind.AggregateInitializer
            && expression.Children.Count == 0)
        {
            generatedCode = "("
                + ExpressExpressionEmitter.BoundTypeName(emptyDerivedAggregateTarget)
                + ")[]";
            var emptyDerivedTarget = attribute.Type;
            var emptyDerivedWrappers = ResolveTransparentDefinedWrappers(
                plan.Resolver,
                ref emptyDerivedTarget);
            for (var wrapperIndex = emptyDerivedWrappers.Count - 1;
                 wrapperIndex >= 0;
                 wrapperIndex--)
            {
                generatedCode = "new "
                    + ExpressExpressionEmitter.BoundTypeName(emptyDerivedWrappers[wrapperIndex])
                    + $"({generatedCode})";
            }
        }
        else if (plan.Resolver.GetAggregateType(attribute.Type) is { } derivedAggregateTarget
            && ResolveExpressionAggregateType(plan, expression, derivedAggregateTarget)
                is { } derivedAggregateSource
            && ResolveAggregateValueToTarget(
                plan,
                derivedAggregateSource,
                derivedAggregateTarget,
                generatedCode,
                allocateTemporaryName("__expressDerivedAggregate"),
                CanEmitIndeterminate(plan, expression),
                materializeEquivalent: true,
                allowRuntimeEntityNarrowing: true) is { } derivedAggregate)
        {
            generatedCode = derivedAggregate;
        }

        if (attribute.Type is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, }
            && expression.Kind != ExpressExpressionKind.Indeterminate)
        {
            var targetType = attribute.Type;
            var definedTypes = ResolveTransparentDefinedWrappers(plan.Resolver, ref targetType);

            var actualNominal = expression.Type.DeclaredType as ExpressBoundNamedType;
            var primitiveActual = actualNominal is null;
            var sameDefinedActual = definedTypes.Count > 0
                && actualNominal is not null
                && ReferenceEquals(actualNominal.Declaration, definedTypes[0].Declaration);
            var semanticDefinedActual = actualNominal is not null
                && ResolveDefinedValueType(plan, attribute.Type) is ExpressBoundNamedType semanticTarget
                && ReferenceEquals(actualNominal.Declaration, semanticTarget.Declaration);
            var exactScalar = targetType is ExpressBoundScalarType targetScalar
                && targetScalar.Kind switch
                {
                    ExpressScalarKind.Binary => expression.Type.Kind == ExpressExpressionTypeKind.Binary,
                    ExpressScalarKind.Boolean => expression.Type.Kind == ExpressExpressionTypeKind.Boolean,
                    ExpressScalarKind.Integer => expression.Type.Kind == ExpressExpressionTypeKind.Integer,
                    ExpressScalarKind.Logical => expression.Type.Kind == ExpressExpressionTypeKind.Logical,
                    ExpressScalarKind.Number => expression.Type.Kind == ExpressExpressionTypeKind.Number,
                    ExpressScalarKind.Real => expression.Type.Kind == ExpressExpressionTypeKind.Real,
                    ExpressScalarKind.String => expression.Type.Kind == ExpressExpressionTypeKind.String,
                    _ => false,
                };
            var exactSelect = targetType is ExpressBoundSelectType
                && expression.Type.Kind == ExpressExpressionTypeKind.Select;
            var exactEnumeration = targetType is ExpressBoundEnumerationType
                && expression.Type.Kind == ExpressExpressionTypeKind.Enumeration;
            if (exactScalar
                || ((primitiveActual || sameDefinedActual || semanticDefinedActual)
                    && (exactSelect || exactEnumeration)))
            {
                if (expression.Type.CanBeIndeterminate)
                {
                    var presentValue = allocateTemporaryName("__expressDerivedValue");
                    var presentCode = presentValue;
                    for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                    {
                        presentCode = "new "
                            + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                            + $"({presentCode})";
                    }

                    generatedCode = $"(({generatedCode}) is {{ }} {presentValue} ? {presentCode} : ("
                        + ExpressExpressionEmitter.BoundTypeName(attribute.Type)
                        + "?)null)";
                }
                else
                {
                    for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                    {
                        generatedCode = "new "
                            + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                            + $"({generatedCode})";
                    }
                }
            }
        }

        if (attribute.Type is ExpressBoundNamedType derivedSelectName
            && derivedSelectName.Declaration.Kind != ExpressDeclarationKind.Entity
            && plan.Resolver.GetDefinedType(derivedSelectName.Declaration).UnderlyingType
                is ExpressBoundSelectType derivedSelect
            && expression.Type.DeclaredType is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, } derivedEntity)
        {
            if (ResolveEntityToSelectValue(
                    plan,
                    derivedSelectName,
                    derivedEntity.Declaration,
                    generatedCode) is { } selectedValue)
            {
                generatedCode = selectedValue;
            }
            else
            {
                var sourceProjection = plan.EntityProjections.Single(projection =>
                    projection.Entity.Symbol == derivedEntity.Declaration);
                var directlyCompatible = plan.Resolver.GetSelectAlternatives(derivedSelect)
                    .Where(alternative => alternative.Kind == ExpressDeclarationKind.Entity
                        && sourceProjection.PhysicalComponents.Any(component =>
                            component.Symbol == alternative))
                    .ToArray();
                if (directlyCompatible.Length == 0)
                {
                    var resultTypeName = ExpressExpressionEmitter.BoundTypeName(derivedSelectName);
                    var runtimeAlternatives = plan.Resolver.GetSelectAlternatives(derivedSelect)
                        .Where(alternative => alternative.Kind == ExpressDeclarationKind.Entity
                            && plan.EntityProjections.Single(projection =>
                                    projection.Entity.Symbol == alternative)
                                .PhysicalComponents.Any(component =>
                                    component.Symbol == derivedEntity.Declaration))
                        .OrderByDescending(alternative => plan.EntityProjections.Single(projection =>
                                projection.Entity.Symbol == alternative)
                            .PhysicalComponents.Count)
                        .ToArray();
                    if (runtimeAlternatives.Length > 0)
                    {
                        var branches = runtimeAlternatives.Select(alternative =>
                        {
                            var alternativeType = ExpressExpressionEmitter.BoundTypeName(
                                new ExpressBoundNamedType(alternative, derivedSelect.Span));
                            var value = allocateTemporaryName("__expressDerivedSelectEntity");
                            return $"{alternativeType} {value} => {resultTypeName}.From"
                                + ExpressEntityProjection.ToPascalCase(alternative.Name)
                                + $"({value})";
                        });
                        generatedCode = $"((object)({generatedCode})) switch {{ "
                            + $"{string.Join(", ", branches)}, _ => throw new "
                            + "global::System.InvalidOperationException() }";
                    }
                }
            }
        }

        if (attribute.Type is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, } derivedEntityTarget
            && expression.Type.DeclaredType is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, } derivedEntitySource
            && !ReferenceEquals(
                derivedEntityTarget.Declaration,
                derivedEntitySource.Declaration)
            && (plan.EntityProjections.Single(projection =>
                        projection.Entity.Symbol == derivedEntitySource.Declaration)
                    .PhysicalComponents.Any(component =>
                        component.Symbol == derivedEntityTarget.Declaration)
                || plan.EntityProjections.Single(projection =>
                        projection.Entity.Symbol == derivedEntityTarget.Declaration)
                    .PhysicalComponents.Any(component =>
                        component.Symbol == derivedEntitySource.Declaration)))
        {
            generatedCode = ResolveNarrowedEntityCarrier(
                plan,
                derivedEntitySource,
                generatedCode,
                derivedEntityTarget.Declaration);
        }

        if (!expression.Type.CanBeIndeterminate
            && attribute.Type is ExpressBoundScalarType
            && expression.Operation is "+" or "-" or "*" or "/"
            && expression.DescendantsAndSelf().Any(candidate =>
                candidate.Kind == ExpressExpressionKind.IndexQualifier))
        {
            var requiredValue = allocateTemporaryName("__expressRequiredDerivedScalar");
            generatedCode = $"(({generatedCode}) is {{ }} {requiredValue} ? {requiredValue} : "
                + "throw new global::System.InvalidOperationException())";
        }

        method.AddStatement(new CustomExpression(generatedCode).Return);
        AddSummary(method, $"Evaluates reachable derived attribute {owner.Name}.{attribute.Name}.");
        return method;
    }

    private static IEnumerable<Method> CreateModelMethods(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        IReadOnlyList<ExpressEntityProjection> entities)
    {
        yield return CreateTypeOfMethod(plan, entities);
        yield return CreateGenericTypeOfMethod();
        yield return CreateIncludeTypeOfNameMethod();
        foreach (var declaration in plan.SupportedDefinedTypes)
        {
            if (declaration.UnderlyingType is ExpressBoundSelectType select)
            {
                yield return CreateSelectTypeOfMethod(plan, declaration, select);
            }
        }

        yield return CreateUsesRoleMethod(plan, resolver, entities);
        yield return CreateUsedInMethod();
        yield return CreateGenericIndexMethod();
        yield return CreateGenericSizeMethod();
        yield return CreateRolesOfMethod(plan, entities);
        var aggregateInverses = plan.Schema.Declarations
                     .OfType<ExpressBoundEntity>()
                     .SelectMany(entity => entity.Attributes
                         .Where(attribute => attribute.Kind == ExpressAttributeKind.Inverse)
                         .Select(attribute => (Entity: entity, Attribute: attribute)))
                     .Where(candidate => candidate.Attribute.Type is ExpressBoundAggregateType);
        var singularInverses = plan.ReachableSingularInverseAttributes
            .Select(attribute => (Entity: plan.GetAttributeOwner(attribute), Attribute: attribute));
        foreach (var inverse in aggregateInverses.Concat(singularInverses))
        {
            yield return CreateInverseMethod(plan, inverse.Entity, inverse.Attribute, resolver);
        }
    }

    private static Method CreateTypeOfMethod(
        ExpressReachableRulePlan plan,
        IReadOnlyList<ExpressEntityProjection> entities)
    {
        var method = CreateMethod(
            "__ExpressTypeOf",
            new DataType("global::TedToolkit.Step21.ExpressSet<global::System.String>"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "candidate"));
        method.AddStatement(new CustomExpression(
            "var result = new global::TedToolkit.Step21.ExpressSet<global::System.String>(0)"));
        foreach (var entity in entities)
        {
            var schemaType = $"{plan.Schema.Name}.{entity.Entity.Name}".ToUpperInvariant();
            method.AddStatement(new IfStatement(new CustomExpression($"candidate is I{entity.Name}"))
                .AddStatement(new CustomExpression($"result.Add(\"{schemaType}\")")));
        }

        method.AddStatement(new CustomExpression("result").Return);
        return method;
    }

    private static Method CreateGenericTypeOfMethod()
    {
        const string setType = "global::TedToolkit.Step21.ExpressSet<global::System.String>";
        var method = CreateMethod("__ExpressGenericTypeOf", new DataType(setType));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Object?"), "candidate"));
        method.AddStatement(new IfStatement(new CustomExpression(
                "candidate is global::TedToolkit.Step21.Entity entity"))
            .AddStatement(new CustomExpression("return __ExpressTypeOf(entity)")));
        method.AddStatement(new IfStatement(new CustomExpression(
                "candidate is global::System.Numerics.BigInteger"))
            .AddStatement(new CustomExpression(
                $"return new {setType}(0) {{ \"INTEGER\", \"NUMBER\" }}")));
        method.AddStatement(new IfStatement(new CustomExpression(
                "candidate is global::TedToolkit.Step21.RealValue"))
            .AddStatement(new CustomExpression(
                $"return new {setType}(0) {{ \"REAL\", \"NUMBER\" }}")));
        method.AddStatement(new IfStatement(new CustomExpression(
                "candidate is global::TedToolkit.Step21.NumberValue number"))
            .AddStatement(new CustomExpression(
                "return number.Kind == global::TedToolkit.Step21.NumberValueKind.Integer "
                + $"? new {setType}(0) {{ \"INTEGER\", \"NUMBER\" }} "
                + $": new {setType}(0) {{ \"REAL\", \"NUMBER\" }}")));
        method.AddStatement(new IfStatement(new CustomExpression("candidate is global::System.Boolean"))
            .AddStatement(new CustomExpression(
                $"return new {setType}(0) {{ \"BOOLEAN\", \"LOGICAL\" }}")));
        method.AddStatement(new IfStatement(new CustomExpression(
                "candidate is global::TedToolkit.Step21.LogicalValue"))
            .AddStatement(new CustomExpression(
                $"return new {setType}(0) {{ \"LOGICAL\" }}")));
        method.AddStatement(new IfStatement(new CustomExpression("candidate is global::System.String"))
            .AddStatement(new CustomExpression(
                $"return new {setType}(0) {{ \"STRING\" }}")));
        method.AddStatement(new IfStatement(new CustomExpression(
                "candidate is global::TedToolkit.Step21.BinaryValue"))
            .AddStatement(new CustomExpression(
                $"return new {setType}(0) {{ \"BINARY\" }}")));
        method.AddStatement(new CustomExpression($"var result = new {setType}(0)"));
        var enumerable = new IfStatement(new CustomExpression(
            "candidate is global::System.Collections.IEnumerable"));
        enumerable.AddStatement(new CustomExpression("var runtimeType = candidate.GetType()"));
        var generic = new IfStatement(new CustomExpression("runtimeType.IsGenericType"))
            .AddStatement(new CustomExpression("var definition = runtimeType.GetGenericTypeDefinition()"))
            .AddStatement(new IfStatement(new CustomExpression(
                "definition == typeof(global::TedToolkit.Step21.ExpressArray<>)"))
                .AddStatement(new CustomExpression("result.Add(\"ARRAY\")")))
            .AddStatement(new IfStatement(new CustomExpression(
                "definition == typeof(global::TedToolkit.Step21.ExpressBag<>)"))
                .AddStatement(new CustomExpression("result.Add(\"BAG\")")))
            .AddStatement(new IfStatement(new CustomExpression(
                "definition == typeof(global::TedToolkit.Step21.ExpressList<>)"))
                .AddStatement(new CustomExpression("result.Add(\"LIST\")")))
            .AddStatement(new IfStatement(new CustomExpression(
                "definition == typeof(global::TedToolkit.Step21.ExpressSet<>)"))
                .AddStatement(new CustomExpression("result.Add(\"SET\")")));
        enumerable.AddStatement(generic);
        method.AddStatement(enumerable);
        method.AddStatement(new CustomExpression("return result"));
        return method;
    }

    private static Method CreateIncludeTypeOfNameMethod()
    {
        var type = new DataType("global::TedToolkit.Step21.ExpressSet<global::System.String>");
        var method = CreateMethod("__ExpressIncludeTypeOfName", type);
        method.AddParameter(SourceComposer.Parameter(type, "types"));
        method.AddParameter(SourceComposer.Parameter(DataType.String, "name"));
        method.AddStatement(new IfStatement(new CustomExpression("!types.Contains(name)"))
            .AddStatement(new CustomExpression("types.Add(name)")));
        method.AddStatement(new CustomExpression("types").Return);
        return method;
    }

    private static Method CreateSelectTypeOfMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundDefinedType declaration,
        ExpressBoundSelectType select)
    {
        var setType = new DataType("global::TedToolkit.Step21.ExpressSet<global::System.String>");
        var method = CreateMethod(SelectTypeOfMethodName(declaration.Symbol), setType);
        var selectType = plan.Resolver.Resolve(plan.Schema.Identity, declaration.Symbol).DataType;
        method.AddParameter(SourceComposer.Parameter(selectType, "candidate"));
        var alternatives = plan.Resolver.GetSelectAlternatives(select);
        for (var index = 0; index < alternatives.Count; index++)
        {
            var alternative = alternatives[index];
            var suffix = ExpressEntityProjection.ToPascalCase(alternative.Name);
            var selected = "selected" + suffix + index.ToString(CultureInfo.InvariantCulture);
            var selectedType = new ExpressBoundNamedType(alternative, select.Span);
            var result = LowerTypeOf(plan, selectedType, selected);
            method.AddStatement(new IfStatement(new CustomExpression(
                    $"candidate.TryGet{suffix}(out var {selected})"))
                .AddStatement(new CustomExpression($"return {result}")));
        }

        method.AddStatement(new CustomExpression(
            "throw new global::System.InvalidOperationException(\"The SELECT value has no supported alternative.\")"));
        return method;
    }

    private static Method CreateUsesRoleMethod(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        IReadOnlyList<ExpressEntityProjection> entities)
    {
        var method = CreateMethod("__ExpressUsesRole", DataType.Bool);
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "owner"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "candidate"));
        method.AddParameter(SourceComposer.Parameter(DataType.String, "role"));
        foreach (var entity in entities)
        {
            for (var attributeIndex = 0; attributeIndex < entity.OwnAttributes.Count; attributeIndex++)
            {
                var attribute = entity.OwnAttributes[attributeIndex];
                var typedName = $"typed{entity.Name}"
                    + attributeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var role = $"{plan.Schema.Name}.{entity.Entity.Name}.{attribute.Attribute.Name}"
                    .ToUpperInvariant();
                var references = ExpressDirectReferenceExpression.Create(
                    attribute.Type,
                    $"{typedName}.{attribute.StorageMemberName}",
                    resolver);
                var roleMatches = "(role.Length == 0 || global::System.String.Equals("
                    + $"role, \"{role}\", global::System.StringComparison.OrdinalIgnoreCase))";
                var contains = "global::System.Linq.Enumerable.Any("
                    + $"{references}, reference => global::System.Object.ReferenceEquals(reference, candidate))";
                method.AddStatement(new IfStatement(new CustomExpression(
                        $"owner is I{entity.Name} {typedName} && {roleMatches} && {contains}"))
                    .AddStatement(true.ToLiteral().Return));
            }
        }

        method.AddStatement(false.ToLiteral().Return);
        return method;
    }

    private static Method CreateUsedInMethod()
    {
        var method = CreateMethod(
            "__ExpressUsedIn",
            new DataType("global::TedToolkit.Step21.ExpressBag<TEntity>"));
        method.AddTypeParameter(SourceComposer.TypeParameter("TEntity"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "candidate"));
        method.AddParameter(SourceComposer.Parameter(DataType.String, "role"));
        AddPopulationParameter(method);
        method.AddStatement(new CustomExpression(
            "var result = new global::TedToolkit.Step21.ExpressBag<TEntity>(0)"));
        var loop = new ForEachStatement(DataType.Var, "entry", new CustomExpression("entities"));
        loop.AddStatement(new IfStatement(new CustomExpression(
                "__ExpressUsesRole(entry.Value, candidate, role) && entry.Value is TEntity typedEntry"))
            .AddStatement(new CustomExpression("result.Add(typedEntry)")));
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression("result").Return);
        return method;
    }

    private static Method CreateGenericIndexMethod()
    {
        var method = CreateMethod("__ExpressGenericIndex", new DataType("global::System.Object?"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Object"), "candidate"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::System.Numerics.BigInteger"),
            "index"));
        method.AddStatement(new IfStatement(new CustomExpression(
                "index < global::System.Numerics.BigInteger.One "
                + "|| index > global::System.Int32.MaxValue "
                + "|| candidate is not global::System.Collections.IEnumerable values"))
            .AddStatement(new CustomExpression("return null")));
        method.AddStatement(new CustomExpression(
            "var position = global::System.Numerics.BigInteger.One"));
        var loop = new ForEachStatement(DataType.Var, "value", new CustomExpression("values"));
        loop.AddStatement(new IfStatement(new CustomExpression("position == index"))
            .AddStatement(new CustomExpression("return value")));
        loop.AddStatement(new CustomExpression("position++"));
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression("return null"));
        return method;
    }

    private static Method CreateGenericSizeMethod()
    {
        var method = CreateMethod(
            "__ExpressGenericSize",
            new DataType("global::System.Numerics.BigInteger"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Object?"), "candidate"));
        method.AddStatement(new IfStatement(new CustomExpression(
                "candidate is not global::System.Collections.IEnumerable values"))
            .AddStatement(new CustomExpression(
                "throw new global::System.InvalidOperationException()")));
        method.AddStatement(new CustomExpression(
            "var result = global::System.Numerics.BigInteger.Zero"));
        var loop = new ForEachStatement(DataType.Var, "value", new CustomExpression("values"));
        loop.AddStatement(new CustomExpression("result++"));
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression("return result"));
        return method;
    }

    private static Method CreateRolesOfMethod(
        ExpressReachableRulePlan plan,
        IReadOnlyList<ExpressEntityProjection> entities)
    {
        var method = CreateMethod(
            "__ExpressRolesOf",
            new DataType("global::TedToolkit.Step21.ExpressSet<global::System.String>"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "candidate"));
        AddPopulationParameter(method);
        method.AddStatement(new CustomExpression(
            "var result = new global::TedToolkit.Step21.ExpressSet<global::System.String>(0)"));
        foreach (var role in entities
                     .SelectMany(entity => entity.OwnAttributes.Select(attribute =>
                         $"{plan.Schema.Name}.{entity.Entity.Name}.{attribute.Attribute.Name}".ToUpperInvariant()))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(role => role, StringComparer.Ordinal))
        {
            method.AddStatement(new IfStatement(new CustomExpression(
                    $"__ExpressUsedIn<global::TedToolkit.Step21.Entity>(candidate, \"{role}\", entities).Count > 0"))
                .AddStatement(new CustomExpression($"result.Add(\"{role}\")")));
        }

        method.AddStatement(new CustomExpression("result").Return);
        return method;
    }

    private static Method CreateInverseMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundEntity owner,
        ExpressBoundAttribute attribute,
        ExpressGeneratedTypeResolver resolver)
    {
        var returnType = resolver.Resolve(plan.Schema.Identity, attribute.Type).DataType;
        var method = CreateMethod(InverseMethodName(owner, attribute), returnType);
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::TedToolkit.Step21.Entity"),
            "candidate"));
        AddPopulationParameter(method);
        AddValidationContextParameters(method, plan);
        var role = $"{plan.Schema.Name}.{attribute.InverseEntity!.Name}.{attribute.InverseAttributeName}"
            .ToUpperInvariant();
        if (attribute.Type is not ExpressBoundAggregateType aggregate)
        {
            AddSingularInverseBody(method, plan, owner, attribute, role);
            return method;
        }

        var returnTypeName = ExpressExpressionEmitter.BoundTypeName(aggregate);
        var elementTypeName = ExpressExpressionEmitter.BoundTypeName(aggregate.ElementType);
        method.AddStatement(new CustomExpression($"var result = new {returnTypeName}(0)"));
        var loop = new ForEachStatement(
            DataType.Var,
            "user",
            new CustomExpression(
                $"__ExpressUsedIn<global::TedToolkit.Step21.Entity>(candidate, \"{role}\", entities)"));
        loop.AddStatement(new IfStatement(new CustomExpression($"user is {elementTypeName} typedUser"))
            .AddStatement(new CustomExpression("result.Add(typedUser)")));
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression("result").Return);
        return method;
    }

    private static void AddPopulationParameter(Method method)
    {
        method.AddParameter(SourceComposer.Parameter(new DataType(POPULATION_TYPE), "entities"));
    }

    private static void AddValidationContextParameters(Method method, ExpressReachableRulePlan plan)
    {
        if (plan.ReachableSingularInverseAttributes.Count == 0)
        {
            return;
        }

        method.AddParameter(SourceComposer.Parameter(new DataType(FAILURE_LIST_TYPE), "failures"));
        method.AddParameter(SourceComposer.Parameter(new DataType(INVERSE_CACHE_TYPE), "inverseCache"));
    }

    private static string ValidationContextArgumentSuffix(ExpressReachableRulePlan plan)
    {
        return plan.ReachableSingularInverseAttributes.Count == 0
            ? ""
            : ", failures, inverseCache";
    }

    private static void AddSingularInverseBody(
        Method method,
        ExpressReachableRulePlan plan,
        ExpressBoundEntity owner,
        ExpressBoundAttribute attribute,
        string role)
    {
        var returnTypeName = ExpressExpressionEmitter.BoundTypeName(attribute.Type);
        var declaration = $"{plan.Schema.Name}.{owner.Name}.{attribute.Name}".ToUpperInvariant();
        var code = declaration + ".INVERSE_CARDINALITY";
        var member = ExpressEntityProjection.ToPascalCase(attribute.Name);
        var location = attribute.Span.Start;
        var filePath = StableFilePath(location.FilePath);

        method.AddStatement(new CustomExpression(
            "if (inverseCache.TryGetValue(candidate, out var cachedByDeclaration) "
            + $"&& cachedByDeclaration.TryGetValue({Literal(declaration)}, out var cachedValue)) "
            + $"{{ return cachedValue is {returnTypeName} typedCachedValue "
            + "? typedCachedValue : throw new __ExpressInverseUnavailableException(); }"));
        method.AddStatement(new CustomExpression(
            "var candidates = new global::System.Collections.Generic.List<"
            + returnTypeName + ">()"));
        var loop = new ForEachStatement(
            DataType.Var,
            "user",
            new CustomExpression(
                $"__ExpressUsedIn<global::TedToolkit.Step21.Entity>(candidate, {Literal(role)}, entities)"));
        loop.AddStatement(new IfStatement(new CustomExpression(
                $"user is {returnTypeName} typedUser && !global::System.Linq.Enumerable.Any("
                + "candidates, existing => global::System.Object.ReferenceEquals(existing, typedUser))"))
            .AddStatement(new CustomExpression("candidates.Add(typedUser)")));
        method.AddStatement(loop);
        method.AddStatement(new IfStatement(new CustomExpression(
                "!inverseCache.TryGetValue(candidate, out cachedByDeclaration)"))
            .AddStatement(new CustomExpression(
                "cachedByDeclaration = new global::System.Collections.Generic.Dictionary<"
                + "global::System.String, global::System.Object?>(global::System.StringComparer.Ordinal)"))
            .AddStatement(new CustomExpression(
                "inverseCache.Add(candidate, cachedByDeclaration)")));
        method.AddStatement(new IfStatement(new CustomExpression("candidates.Count == 1"))
            .AddStatement(new CustomExpression(
                $"cachedByDeclaration.Add({Literal(declaration)}, candidates[0])"))
            .AddStatement(new CustomExpression("candidates[0]").Return));
        method.AddStatement(new CustomExpression(
            $"cachedByDeclaration.Add({Literal(declaration)}, null)"));
        method.AddStatement(new CustomExpression(
            "var candidateEntry = global::System.Linq.Enumerable.FirstOrDefault(entities, entry => "
            + "global::System.Object.ReferenceEquals(entry.Value, candidate))"));
        method.AddStatement(new IfStatement(new CustomExpression("candidateEntry.Value is null"))
            .AddStatement(new CustomExpression(
                "throw new __ExpressInverseUnavailableException()")));
        method.AddStatement(new CustomExpression("var candidatePath = candidateEntry.Key"));
        method.AddStatement(new CustomExpression(
            "failures.Add(new global::TedToolkit.Step21.ValidationFailure("
            + $"{Literal(code)}, candidatePath + {Literal($".{member}")}, "
            + $"{Literal("The singular inverse resolved an invalid number of distinct owner candidates for role ")}"
            + $"+ {Literal(role)} + {Literal(": ")} + candidates.Count.ToString("
            + "global::System.Globalization.CultureInfo.InvariantCulture), "
            + "new global::TedToolkit.Step21.SourceLocation("
            + $"{Literal(filePath)}, {location.Line.ToString(CultureInfo.InvariantCulture)}, "
            + $"{location.Column.ToString(CultureInfo.InvariantCulture)})))"));
        method.AddStatement(new CustomExpression(
            "throw new __ExpressInverseUnavailableException()"));
    }

    private static string ResolveModelFunction(
        ExpressReachableRulePlan plan,
        string operation,
        ExpressBoundExpression expression,
        IReadOnlyList<string> arguments,
        string populationExpression,
        ExpressBoundType? usedInCarrierType = null,
        int usedInDepth = 0,
        ExpressBoundType? aggregateUnionSourceType = null,
        ExpressBoundType? aggregateUnionTargetType = null,
        int aggregateUnionDepth = 0,
        ExpressBoundType?[]? aggregateUnionSourceTypeOverrides = null,
        bool aggregateUnionAllowsRuntimeNarrowing = false,
        ExpressBoundType? typeOfCarrierTypeOverride = null)
    {
        if (operation == "AGGREGATE_UNION_ELEMENT")
        {
            string? entityTargetType = null;
            if (aggregateUnionSourceType is ExpressBoundScalarType scalarSource
                && aggregateUnionTargetType is not null
                && TryAdaptScalarUnionElement(
                    plan,
                    scalarSource,
                    aggregateUnionTargetType,
                    arguments[0],
                    new HashSet<ExpressBoundSymbol>(),
                    out var scalarAdapted,
                    out _))
            {
                return scalarAdapted;
            }

            if (aggregateUnionTargetType is ExpressBoundGenericType { IsEntity: true, })
            {
                if (aggregateUnionSourceType is ExpressBoundGenericType { IsEntity: true, })
                {
                    return arguments[0];
                }

                if (aggregateUnionSourceType is not ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, })
                {
                    throw new InvalidOperationException(
                        "Aggregate entity union requires an entity source element.");
                }

                entityTargetType = "global::TedToolkit.Step21.Entity";
            }

            if (aggregateUnionSourceType is not ExpressBoundNamedType source
                || (aggregateUnionTargetType is not ExpressBoundNamedType
                    && entityTargetType is null))
            {
                throw new InvalidOperationException(
                    "Aggregate SELECT union elements require named source and target types.");
            }

            var target = aggregateUnionTargetType as ExpressBoundNamedType;
            if (target is not null && source.Declaration == target.Declaration)
            {
                return arguments[0];
            }

            if (target is not null
                && target.Declaration.Kind != ExpressDeclarationKind.Entity
                && plan.Resolver.GetDefinedType(target.Declaration).UnderlyingType
                    is ExpressBoundNamedType namedTargetUnderlying)
            {
                var adapted = ResolveModelFunction(
                    plan,
                    operation,
                    expression,
                    arguments,
                    populationExpression,
                    aggregateUnionSourceType: source,
                    aggregateUnionTargetType: namedTargetUnderlying,
                    aggregateUnionDepth: aggregateUnionDepth,
                    aggregateUnionAllowsRuntimeNarrowing: aggregateUnionAllowsRuntimeNarrowing);
                return $"new {ExpressExpressionEmitter.BoundTypeName(target)}({adapted})";
            }

            if (source.Declaration.Kind == ExpressDeclarationKind.Entity && target is not null)
            {
                var sourceProjection = plan.EntityProjections.Single(projection =>
                    projection.Entity.Symbol == source.Declaration);
                if (target.Declaration.Kind == ExpressDeclarationKind.Entity)
                {
                    if (source.Declaration == target.Declaration)
                    {
                        return arguments[0];
                    }

                    var targetProjection = plan.EntityProjections.Single(projection =>
                        projection.Entity.Symbol == target.Declaration);
                    var sourceIsTargetSubtype = sourceProjection.PhysicalComponents.Any(component =>
                        component.Symbol == target.Declaration);
                    var targetIsSourceSubtype = targetProjection.PhysicalComponents.Any(component =>
                        component.Symbol == source.Declaration);
                    if (!sourceIsTargetSubtype && !targetIsSourceSubtype)
                    {
                        if (!aggregateUnionAllowsRuntimeNarrowing)
                        {
                            throw new InvalidOperationException(
                                "Aggregate SELECT union entity alternative is incompatible with the result element type.");
                        }

                        return "throw new global::System.InvalidOperationException()";
                    }

                    if (!sourceIsTargetSubtype && !aggregateUnionAllowsRuntimeNarrowing)
                    {
                        throw new InvalidOperationException(
                            "Aggregate SELECT union entity alternative is incompatible with the result element type.");
                    }

                    entityTargetType = ExpressExpressionEmitter.BoundTypeName(target);
                }
                else
                {
                    var targetDefined = plan.Resolver.GetDefinedType(target.Declaration).UnderlyingType;
                    if (targetDefined is not ExpressBoundSelectType targetSelect)
                    {
                        throw new InvalidOperationException(
                            "Aggregate union adapts entity elements only to compatible sole-alternative SELECT targets.");
                    }

                    var targetAlternatives = plan.Resolver.GetSelectAlternatives(targetSelect)
                        .Where(alternative => alternative.Kind == ExpressDeclarationKind.Entity)
                        .Select(alternative => (
                            Alternative: alternative,
                            Projection: plan.EntityProjections.Single(projection =>
                                projection.Entity.Symbol == alternative)))
                        .Where(candidate => sourceProjection.PhysicalComponents.Any(component =>
                                component.Symbol == candidate.Alternative)
                            || candidate.Projection.PhysicalComponents.Any(component =>
                                component.Symbol == source.Declaration))
                        .ToArray();
                    if (targetAlternatives.Length == 0)
                    {
                        throw new InvalidOperationException(
                            "Aggregate union SELECT target has no compatible entity alternative.");
                    }

                    var directAlternatives = targetAlternatives
                        .Where(candidate => sourceProjection.PhysicalComponents.Any(component =>
                            component.Symbol == candidate.Alternative))
                        .ToArray();
                    if (targetAlternatives.Length == 1 && directAlternatives.Length == 1)
                    {
                        return ExpressExpressionEmitter.BoundTypeName(target)
                            + ".From"
                            + ExpressEntityProjection.ToPascalCase(
                                targetAlternatives[0].Alternative.Name)
                            + $"({arguments[0]})";
                    }

                    if (directAlternatives.Length > 0)
                    {
                        throw new InvalidOperationException(
                            "Aggregate union SELECT target is ambiguous for the statically known entity element.");
                    }

                    var targetType = ExpressExpressionEmitter.BoundTypeName(target);
                    var branches = targetAlternatives
                        .OrderByDescending(candidate => candidate.Projection.PhysicalComponents.Count)
                        .Select((candidate, index) =>
                        {
                            var alternativeType = ExpressExpressionEmitter.BoundTypeName(
                                new ExpressBoundNamedType(candidate.Alternative, targetSelect.Span));
                            var selected = "__expressAggregateUnionSelected"
                                + aggregateUnionDepth.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + index.ToString(CultureInfo.InvariantCulture);
                            return alternativeType + " " + selected + " => "
                                + targetType
                                + ".From"
                                + ExpressEntityProjection.ToPascalCase(candidate.Alternative.Name)
                                + $"({selected})";
                        });
                    return $"({arguments[0]}) switch {{ {string.Join(", ", branches)}, "
                        + "_ => throw new global::System.InvalidOperationException() }";
                }
            }

            if (entityTargetType is not null)
            {
                var entity = "__expressAggregateUnionEntity"
                    + aggregateUnionDepth.ToString(CultureInfo.InvariantCulture);
                return $"({arguments[0]}) switch {{ {entityTargetType} {entity} => {entity}, "
                    + "_ => throw new global::System.InvalidOperationException() }";
            }

            if (target is null)
            {
                throw new InvalidOperationException(
                    "Aggregate SELECT union elements require named source and target types.");
            }

            var underlying = plan.Resolver.GetDefinedType(source.Declaration).UnderlyingType;
            if (underlying is ExpressBoundNamedType namedUnderlying)
            {
                return ResolveModelFunction(
                    plan,
                    operation,
                    expression,
                    [$"({arguments[0]}).Value",],
                    populationExpression,
                    aggregateUnionSourceType: namedUnderlying,
                    aggregateUnionTargetType: target,
                    aggregateUnionDepth: aggregateUnionDepth,
                    aggregateUnionAllowsRuntimeNarrowing: aggregateUnionAllowsRuntimeNarrowing);
            }

            if (underlying is not ExpressBoundSelectType select)
            {
                throw new InvalidOperationException(
                    "Aggregate union adapts only sole-alternative entity SELECT elements.");
            }

            var alternatives = plan.Resolver.GetSelectAlternatives(select);
            var sourceBranches = alternatives.Select((alternative, index) =>
            {
                var selected = "__expressAggregateUnionSelected"
                    + aggregateUnionDepth.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + index.ToString(CultureInfo.InvariantCulture);
                return selected + " => " + ResolveModelFunction(
                    plan,
                    operation,
                    expression,
                    [selected,],
                    populationExpression,
                    aggregateUnionSourceType: new ExpressBoundNamedType(alternative, select.Span),
                    aggregateUnionTargetType: target,
                    aggregateUnionDepth: aggregateUnionDepth + 1,
                    aggregateUnionAllowsRuntimeNarrowing: aggregateUnionAllowsRuntimeNarrowing);
            });
            return $"({arguments[0]}).Match<"
                + ExpressExpressionEmitter.BoundTypeName(target)
                + $">({string.Join(", ", sourceBranches)})";
        }

        if (operation == "AGGREGATE_UNION")
        {
            var sourceTypes = expression.Children
                .Select((child, index) => aggregateUnionSourceTypeOverrides?[index]
                    ?? child.Type.DeclaredType)
                .ToArray();
            var resultAggregate = expression.Type.DeclaredType as ExpressBoundAggregateType
                ?? (expression.Type.DeclaredType is { } resultType
                    ? plan.Resolver.GetAggregateType(resultType)
                    : null);
            var operandAggregates = sourceTypes
                .Select(sourceType => sourceType is null
                    ? null
                    : plan.Resolver.GetAggregateType(sourceType))
                .ToArray();
            if (resultAggregate is not { IsOptional: false, }
                || resultAggregate.Kind is not (ExpressAggregateKind.List
                    or ExpressAggregateKind.Bag
                    or ExpressAggregateKind.Set)
                || operandAggregates.All(aggregate => aggregate is null)
                || operandAggregates.Any(aggregate => aggregate is { IsOptional: true, }))
            {
                throw new InvalidOperationException(
                    "Aggregate union requires at least one non-optional LIST, BAG, or SET operand.");
            }

            for (var index = 0; index < sourceTypes.Length; index++)
            {
                if (operandAggregates[index] is not null
                    || sourceTypes[index] is not ExpressBoundNamedType selectName
                    || selectName.Declaration.Kind == ExpressDeclarationKind.Entity
                    || plan.Resolver.GetDefinedType(selectName.Declaration).UnderlyingType
                        is not ExpressBoundSelectType mixedSelect)
                {
                    continue;
                }

                var alternatives = plan.Resolver.GetSelectAlternatives(mixedSelect)
                    .Select(alternative => new ExpressBoundNamedType(alternative, mixedSelect.Span))
                    .ToArray();
                if (!alternatives.Any(alternative =>
                        plan.Resolver.GetAggregateType(alternative) is not null))
                {
                    continue;
                }

                var branches = alternatives.Select((alternative, branchIndex) =>
                {
                    var selected = "__expressAggregateUnionAlternative_"
                        + aggregateUnionDepth.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + branchIndex.ToString(CultureInfo.InvariantCulture);
                    var branchArguments = arguments.ToArray();
                    if (plan.Resolver.GetAggregateType(alternative) is { } alternativeAggregate)
                    {
                        branchArguments[index] = ResolveAggregateSource(
                                plan,
                                alternative,
                                selected,
                                alternativeAggregate)
                            ?? throw new InvalidOperationException(
                                "Aggregate union could not read a selected aggregate alternative.");
                    }
                    else
                    {
                        branchArguments[index] = selected;
                    }

                    var branchSourceTypes = sourceTypes.ToArray();
                    branchSourceTypes[index] = alternative;
                    var branch = ResolveModelFunction(
                        plan,
                        operation,
                        expression,
                        branchArguments,
                        populationExpression,
                        aggregateUnionDepth: aggregateUnionDepth + 1,
                        aggregateUnionSourceTypeOverrides: branchSourceTypes,
                        aggregateUnionAllowsRuntimeNarrowing: true);
                    return selected + " => " + branch;
                });
                return $"({arguments[index]}).Match<"
                    + ExpressExpressionEmitter.BoundTypeName(resultAggregate)
                    + $">({string.Join(", ", branches)})";
            }

            var target = resultAggregate.ElementType;
            var values = new string[2];
            for (var index = 0; index < operandAggregates.Length; index++)
            {
                var source = operandAggregates[index]?.ElementType
                    ?? sourceTypes[index];
                if (source is null)
                {
                    throw new InvalidOperationException(
                        "Aggregate entity union requires typed operands.");
                }

                if (operandAggregates[index] is null
                    && expression.Children[index].Type.DefinedValueDepth > 0)
                {
                    values[index] = ResolveAggregateElementValue(
                        plan,
                        expression.Children[index],
                        arguments[index],
                        target,
                        sourceIsDeterminate: true);
                    continue;
                }

                if ((source is ExpressBoundNamedType namedSource
                        && target is ExpressBoundNamedType namedTarget
                        && namedSource.Declaration == namedTarget.Declaration)
                    || (source is ExpressBoundGenericType { IsEntity: true, }
                        && target is ExpressBoundGenericType { IsEntity: true, }))
                {
                    values[index] = arguments[index];
                    continue;
                }

                var item = "__expressAggregateUnionItem"
                    + index.ToString(CultureInfo.InvariantCulture);
                var adapted = ResolveModelFunction(
                        plan,
                        "AGGREGATE_UNION_ELEMENT",
                        expression,
                        [operandAggregates[index] is null ? arguments[index] : item,],
                        populationExpression,
                        aggregateUnionSourceType: source,
                        aggregateUnionTargetType: target,
                        aggregateUnionDepth: index,
                        aggregateUnionAllowsRuntimeNarrowing: aggregateUnionAllowsRuntimeNarrowing
                            || (operandAggregates[index] is null && IsSelectValueType(plan, source)));
                values[index] = operandAggregates[index] is null
                    ? adapted
                    : "global::System.Linq.Enumerable.Select("
                        + $"({arguments[index]}), {item} => {adapted})";
            }

            var targetName = ExpressExpressionEmitter.BoundTypeName(target);
            var combined = (operandAggregates[0] is not null, operandAggregates[1] is not null) switch
            {
                (true, true) => $"global::System.Linq.Enumerable.Concat<{targetName}>({values[0]}, {values[1]})",
                (true, false) => $"global::System.Linq.Enumerable.Append<{targetName}>({values[0]}, {values[1]})",
                (false, true) => $"global::System.Linq.Enumerable.Prepend<{targetName}>({values[1]}, {values[0]})",
                _ => throw new InvalidOperationException(
                    "Aggregate union requires at least one aggregate operand."),
            };
            var distinct = resultAggregate.Kind == ExpressAggregateKind.Set
                ? $"global::System.Linq.Enumerable.Distinct({combined})"
                : combined;
            return $"({ExpressExpressionEmitter.BoundTypeName(resultAggregate)})[..{distinct}]";
        }

        var usedInElementType = expression.Type.DeclaredType is ExpressBoundAggregateType aggregate
            ? ExpressExpressionEmitter.BoundTypeName(aggregate.ElementType)
            : "global::TedToolkit.Step21.Entity";
        if (operation == "USEDIN")
        {
            var carrierType = usedInCarrierType ?? expression.Children[0].Type.DeclaredType;
            var carrier = arguments[0];
            while (carrierType is ExpressBoundNamedType namedCarrier
                   && namedCarrier.Declaration.Kind != ExpressDeclarationKind.Entity)
            {
                var underlying = plan.Resolver.GetDefinedType(namedCarrier.Declaration).UnderlyingType;
                if (underlying is ExpressBoundNamedType)
                {
                    carrier = $"({carrier}).Value";
                }

                carrierType = underlying;
            }

            if (carrierType is ExpressBoundSelectType select)
            {
                var alternatives = plan.Resolver.GetSelectAlternatives(select);
                var branches = alternatives.Select((alternative, index) =>
                {
                    var selected = "__expressUsedInCarrier"
                        + usedInDepth.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture);
                    return selected
                        + " => "
                        + ResolveModelFunction(
                            plan,
                            operation,
                            expression,
                            [selected, arguments[1],],
                            populationExpression,
                            new ExpressBoundNamedType(alternative, select.Span),
                            usedInDepth + 1);
                });
                return $"({carrier}).Match<global::TedToolkit.Step21.ExpressBag<"
                    + $"{usedInElementType}>>({string.Join(", ", branches)})";
            }

            if (!(carrierType is ExpressBoundNamedType namedEntityCarrier
                    && namedEntityCarrier.Declaration.Kind == ExpressDeclarationKind.Entity)
                && carrierType is not ExpressBoundGenericType { IsEntity: true, })
            {
                return ResolveValueUsedIn(
                    plan,
                    expression,
                    carrier,
                    populationExpression,
                    usedInElementType);
            }

            return $"__ExpressUsedIn<{usedInElementType}>("
                + $"(global::TedToolkit.Step21.Entity)({carrier}), {arguments[1]}, {populationExpression})";
        }

        return operation switch
        {
            "GENERIC_INDEX" => $"__ExpressGenericIndex({arguments[0]}, {arguments[1]})",
            "GENERIC_SIZE" => $"__ExpressGenericSize({arguments[0]})",
            "SELECT_AGGREGATE_SIZE" => ResolveAggregateSize(
                plan,
                ResolveSelectCarrierType(plan, expression.Children[0])
                    ?? expression.Children[0].Type.DeclaredType
                    ?? expression.Children[0].Reference?.Attribute?.Type
                    ?? expression.Children[0].Reference?.Type
                    ?? throw new InvalidOperationException(
                        "A selected aggregate SIZEOF operand must retain its declared carrier."),
                arguments[0],
                expression.Children[0].Type.DefinedValueDepth),
            "COMPLEX_CONSTRUCTOR" => LowerComplexConstructor(plan, expression, arguments),
            "ORDERED_SELECT_COMPARISON" => ResolveOrderedSelectComparison(
                plan,
                expression,
                arguments),
            "SELECT_LIKE" => ResolveSelectLike(plan, expression, arguments),
            "NUMERIC_SELECT_BINARY" => ResolveNumericSelectBinary(
                plan,
                expression,
                arguments),
            "NUMERIC_SELECT_UNARY" => ResolveNumericSelectUnary(
                plan,
                expression,
                arguments[0]),
            "SELECT_SCALAR_BUILTIN" => ResolveSelectScalarBuiltIn(
                plan,
                expression,
                arguments),
            "INSTANCE_EQUALITY" => ResolveValueEquality(
                plan,
                expression.Children[0],
                arguments[0],
                expression.Children[1],
                arguments[1],
                null,
                instanceEquality: true),
            "TYPEOF" => LowerTypeOf(
                plan,
                typeOfCarrierTypeOverride ?? expression.Children[0].Type.DeclaredType,
                arguments[0],
                expression.Children[0].Type.DefinedValueDepth),
            "ROLESOF" => "__ExpressRolesOf("
                + $"(global::TedToolkit.Step21.Entity)({arguments[0]}), {populationExpression})",
            _ => throw new InvalidOperationException($"{operation}: {expression.SourceText}"),
        };
    }

    private static string ResolveAggregateSize(
        ExpressReachableRulePlan plan,
        ExpressBoundType carrierType,
        string carrier,
        int unwrappedDepth = 0,
        int dispatchDepth = 0)
    {
        while (unwrappedDepth > 0
               && carrierType is ExpressBoundNamedType unwrappedName
               && unwrappedName.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            carrierType = plan.Resolver.GetDefinedType(unwrappedName.Declaration).UnderlyingType;
            unwrappedDepth--;
        }

        while (carrierType is ExpressBoundNamedType namedCarrier
               && namedCarrier.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            var underlying = plan.Resolver.GetDefinedType(namedCarrier.Declaration).UnderlyingType;
            if (underlying is ExpressBoundSelectType)
            {
                carrierType = underlying;
                break;
            }

            carrier = $"({carrier}).Value";
            carrierType = underlying;
        }

        if (carrierType is ExpressBoundSelectType select)
        {
            var branches = plan.Resolver.GetSelectAlternatives(select)
                .Select((alternative, index) =>
                {
                    var variable = "__expressSizedAggregate"
                        + dispatchDepth.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture);
                    var value = ResolveAggregateSize(
                        plan,
                        new ExpressBoundNamedType(alternative, select.Span),
                        variable,
                        dispatchDepth: dispatchDepth + 1);
                    return $"{variable} => {value}";
                });
            return $"({carrier}).Match<global::System.Numerics.BigInteger>("
                + $"{string.Join(", ", branches)})";
        }

        if (carrierType is ExpressBoundAggregateType aggregate)
        {
            return "new global::System.Numerics.BigInteger("
                + "global::System.Linq.Enumerable.Count<"
                + ExpressExpressionEmitter.BoundTypeName(aggregate.ElementType)
                + $">({carrier}))";
        }

        return "throw new global::System.InvalidOperationException()";
    }

    private static string? ResolveSelectedAggregateIndex(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        IReadOnlyList<string> arguments,
        string resultType)
    {
        if (ResolveSelectCarrierType(plan, expression.Children[0]) is not { } sourceType)
        {
            return null;
        }

        var index = expression.Children[1].Type.Kind == ExpressExpressionTypeKind.Number
            ? $"({arguments[1]}).ToIntegerTruncated()"
            : arguments[1];
        string? endIndex = null;
        if (arguments.Count == 3)
        {
            endIndex = expression.Children[2].Type.Kind == ExpressExpressionTypeKind.Number
                ? $"({arguments[2]}).ToIntegerTruncated()"
                : arguments[2];
        }

        var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var nullValue = $"({resultType}?)null";

        string? Dispatch(ExpressBoundType carrierType, string carrier, int depth)
        {
            while (carrierType is ExpressBoundNamedType namedCarrier
                   && namedCarrier.Declaration.Kind != ExpressDeclarationKind.Entity)
            {
                var underlying = plan.Resolver.GetDefinedType(namedCarrier.Declaration).UnderlyingType;
                if (underlying is ExpressBoundSelectType)
                {
                    carrierType = underlying;
                    break;
                }

                carrier = underlying is ExpressBoundAggregateType
                    ? $"({carrier}).ReadOnlyValue"
                    : $"({carrier}).Value";
                carrierType = underlying;
            }

            if (carrierType is ExpressBoundSelectType select)
            {
                var branches = new List<string>();
                foreach (var alternative in plan.Resolver.GetSelectAlternatives(select))
                {
                    var selected = "__expressSelectedAggregate_"
                        + suffix
                        + "_"
                        + depth.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + branches.Count.ToString(CultureInfo.InvariantCulture);
                    var value = Dispatch(
                        new ExpressBoundNamedType(alternative, select.Span),
                        selected,
                        depth + 1) ?? nullValue;

                    branches.Add($"{selected} => {value}");
                }

                return $"({carrier}).Match<{resultType}?>({string.Join(", ", branches)})";
            }

            if (carrierType is not ExpressBoundAggregateType aggregate)
            {
                return null;
            }

            var valueName = "__expressSelectedValues_" + suffix + "_" + depth.ToString(CultureInfo.InvariantCulture);
            var position = "__expressSelectedIndex_" + suffix + "_" + depth.ToString(CultureInfo.InvariantCulture);
            var samePosition = endIndex is null
                ? ""
                : $" && {position} == checked((int)({endIndex}))";
            string ResultValue(string candidate)
            {
                if (expression.Type.DeclaredType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } narrowedEntity
                    && aggregate.ElementType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } declaredEntity
                    && !ReferenceEquals(narrowedEntity.Declaration, declaredEntity.Declaration))
                {
                    return $"({candidate}) as {ExpressExpressionEmitter.BoundTypeName(narrowedEntity)}";
                }

                return ResolveAggregateElementValue(
                    plan,
                    expression,
                    candidate,
                    aggregate.ElementType,
                    sourceIsDeterminate: true);
            }

            if (aggregate.Kind == ExpressAggregateKind.Array)
            {
                return $"(({carrier}), checked((int)({index}))) switch {{ "
                    + $"var ({valueName}, {position}) when {position} >= {valueName}.LowerIndex "
                    + $"&& {position} <= {valueName}.UpperIndex{samePosition} "
                    + $"&& {valueName}.IsSet({position}) => {ResultValue($"{valueName}[{position}]")}, "
                    + $"_ => {nullValue} }}";
            }

            var indexed = aggregate.Kind == ExpressAggregateKind.List
                ? $"{valueName}[{position} - 1]"
                : $"global::System.Linq.Enumerable.ElementAt({valueName}, {position} - 1)";
            return $"(({carrier}), checked((int)({index}))) switch {{ "
                + $"var ({valueName}, {position}) when {position} >= 1 "
                + $"&& {position} <= {valueName}.Count{samePosition} => {ResultValue(indexed)}, "
                + $"_ => {nullValue} }}";
        }

        return Dispatch(sourceType, arguments[0], 0);
    }

    private static string? ResolveAggregateSource(
        ExpressReachableRulePlan plan,
        ExpressBoundType sourceType,
        string source,
        ExpressBoundAggregateType target,
        bool allowIncompatible = false)
    {
        var targetType = ExpressExpressionEmitter.AggregateInterfaceTypeName(target)
            + (allowIncompatible ? "?" : "");

        string? Dispatch(ExpressBoundType carrierType, string carrier, int depth)
        {
            while (carrierType is ExpressBoundNamedType namedCarrier
                   && namedCarrier.Declaration.Kind != ExpressDeclarationKind.Entity)
            {
                var underlying = plan.Resolver.GetDefinedType(namedCarrier.Declaration).UnderlyingType;
                if (underlying is ExpressBoundSelectType)
                {
                    carrierType = underlying;
                    break;
                }

                carrier = underlying is ExpressBoundAggregateType
                    ? $"({carrier}).ReadOnlyValue"
                    : $"({carrier}).Value";
                carrierType = underlying;
            }

            if (carrierType is ExpressBoundAggregateType sourceAggregate)
            {
                var projectedCarrier = carrier;
                var projectedElement = false;
                if (!ExpressGeneratedTypeResolver.AreEquivalent(
                        sourceAggregate.ElementType,
                        target.ElementType)
                    && sourceAggregate.Kind == target.Kind
                    && sourceAggregate.Kind is ExpressAggregateKind.Bag
                        or ExpressAggregateKind.List
                        or ExpressAggregateKind.Set
                    && sourceAggregate.ElementType is ExpressBoundNamedType sourceElement
                    && target.ElementType is ExpressBoundNamedType targetElement
                    && targetElement.Declaration.Kind != ExpressDeclarationKind.Entity)
                {
                    var element = "__expressAggregateElement_"
                        + depth.ToString(CultureInfo.InvariantCulture);
                    if (ResolveNamedToSelectValue(
                            plan,
                            targetElement,
                            sourceElement,
                            element) is { } selectedElement)
                    {
                        projectedCarrier = "global::System.Linq.Enumerable.Select("
                            + $"{carrier}, {element} => {selectedElement})";
                        projectedElement = true;
                    }
                }

                if (!ExpressGeneratedTypeResolver.AreEquivalent(
                        sourceAggregate.ElementType,
                        target.ElementType)
                    && sourceAggregate.Kind == target.Kind
                    && sourceAggregate.Kind is ExpressAggregateKind.Bag
                        or ExpressAggregateKind.List
                        or ExpressAggregateKind.Set
                    && sourceAggregate.ElementType is ExpressBoundNamedType
                    { Declaration.Kind: not ExpressDeclarationKind.Entity, } selectedSourceElement
                    && target.ElementType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } selectedTargetEntity
                    && TryResolveSelectCarrier(
                        plan,
                        selectedSourceElement,
                        out var selectedSourceCarrier,
                        out var selectedSourceWrappers))
                {
                    var element = "__expressAggregateEntityElement_"
                        + depth.ToString(CultureInfo.InvariantCulture);
                    var selectedSource = element;
                    for (var wrapperIndex = 0;
                         wrapperIndex < selectedSourceWrappers.Count;
                         wrapperIndex++)
                    {
                        selectedSource = $"({selectedSource}).Value";
                    }

                    if (ResolveSelectToEntityValue(
                        plan,
                        selectedTargetEntity,
                        selectedSourceCarrier,
                        selectedSource,
                        element + "Selected") is { } selectedEntity)
                    {
                        var present = element + "Value";
                        projectedCarrier = "global::System.Linq.Enumerable.Select("
                            + $"{carrier}, {element} => ({selectedEntity}) is {{ }} {present} "
                            + $"? {present} : throw new global::System.InvalidOperationException())";
                        projectedElement = true;
                    }
                }

                if (!ExpressGeneratedTypeResolver.AreEquivalent(
                        sourceAggregate.ElementType,
                        target.ElementType)
                    && sourceAggregate.ElementType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, }
                    && target.ElementType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, })
                {
                    projectedCarrier = "global::System.Linq.Enumerable.Cast<"
                        + ExpressExpressionEmitter.BoundTypeName(target.ElementType)
                        + $">({carrier})";
                    projectedElement = true;
                }

                if (!projectedElement
                    && !ExpressGeneratedTypeResolver.AreEquivalent(
                        sourceAggregate.ElementType,
                        target.ElementType))
                {
                    return null;
                }

                if (target.Kind == ExpressAggregateKind.Aggregate)
                {
                    return projectedCarrier;
                }

                if ((sourceAggregate.Kind != target.Kind || projectedElement)
                    && target.Kind is ExpressAggregateKind.Bag
                        or ExpressAggregateKind.List
                        or ExpressAggregateKind.Set)
                {
                    var aggregateType = target.Kind switch
                    {
                        ExpressAggregateKind.Bag => "global::TedToolkit.Step21.ExpressBag",
                        ExpressAggregateKind.List => "global::TedToolkit.Step21.ExpressList",
                        ExpressAggregateKind.Set => "global::TedToolkit.Step21.ExpressSet",
                        _ => throw new InvalidOperationException(
                            "Only variable-size aggregates support element projection."),
                    };
                    return $"({aggregateType}<{ExpressExpressionEmitter.BoundTypeName(target.ElementType)}>)"
                        + $"[..{projectedCarrier}]";
                }

                return projectedCarrier;
            }

            if (carrierType is not ExpressBoundSelectType select)
            {
                return null;
            }

            var branches = new List<string>();
            var hasCompatibleBranch = false;
            foreach (var alternative in plan.Resolver.GetSelectAlternatives(select))
            {
                var selected = "__expressAggregateSource_"
                    + depth.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + branches.Count.ToString(CultureInfo.InvariantCulture);
                var value = Dispatch(
                    new ExpressBoundNamedType(alternative, select.Span),
                    selected,
                    depth + 1);
                if (value is null)
                {
                    value = allowIncompatible
                        ? "null"
                        : "throw new global::System.InvalidOperationException()";
                }
                else
                {
                    hasCompatibleBranch = true;
                }

                branches.Add($"{selected} => {value}");
            }

            if (!hasCompatibleBranch)
            {
                return null;
            }

            return $"({carrier}).Match<{targetType}>({string.Join(", ", branches)})";
        }

        return Dispatch(sourceType, source, 0);
    }

    private static bool IsSelectedAggregateExhaustive(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType carrier,
        ExpressBoundAggregateType target)
    {
        if (!TryResolveSelectCarrier(plan, carrier, out var selectCarrier, out _)
            || plan.Resolver.GetDefinedType(selectCarrier.Declaration).UnderlyingType
                is not ExpressBoundSelectType select)
        {
            return false;
        }

        return plan.Resolver.GetSelectAlternatives(select).All(alternative =>
        {
            var alternativeType = new ExpressBoundNamedType(alternative, carrier.Span);
            return TryResolveSelectCarrier(plan, alternativeType, out _, out _)
                ? IsSelectedAggregateExhaustive(plan, alternativeType, target)
                : ResolveAggregateSource(
                    plan,
                    alternativeType,
                    "__expressAggregateProof",
                    target) is not null;
        });
    }

    private static string ResolveOrderedSelectComparison(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        IReadOnlyList<string> arguments)
    {
        const string unknown = "global::TedToolkit.Step21.LogicalValue.Unknown";
        const string trueValue = "global::TedToolkit.Step21.LogicalValue.True";
        const string falseValue = "global::TedToolkit.Step21.LogicalValue.False";

        static string Promote(string value, ExpressScalarKind source, ExpressScalarKind target)
        {
            return (source, target) switch
            {
                (ExpressScalarKind.Integer, ExpressScalarKind.Real) =>
                    $"new global::TedToolkit.Step21.RealValue(({value}), "
                    + "global::System.Numerics.BigInteger.Zero)",
                (ExpressScalarKind.Integer, ExpressScalarKind.Number) =>
                    $"global::TedToolkit.Step21.NumberValue.FromInteger({value})",
                (ExpressScalarKind.Real, ExpressScalarKind.Number) =>
                    $"global::TedToolkit.Step21.NumberValue.FromReal({value})",
                _ => value,
            };
        }

        string Compare(
            string left,
            ExpressScalarKind leftKind,
            string right,
            ExpressScalarKind rightKind,
            string operation)
        {
            var target = ExpressScalarKind.Integer;
            if (leftKind == ExpressScalarKind.Number || rightKind == ExpressScalarKind.Number)
            {
                target = ExpressScalarKind.Number;
            }
            else if (leftKind == ExpressScalarKind.Real || rightKind == ExpressScalarKind.Real)
            {
                target = ExpressScalarKind.Real;
            }

            return $"(({Promote(left, leftKind, target)}) {operation} "
                + $"({Promote(right, rightKind, target)}) ? {trueValue} : {falseValue})";
        }

        string ProjectPair(int leftIndex, int rightIndex, string operation, string name)
        {
            return ProjectSelectScalar(
                plan,
                expression.Children[leftIndex].Type.DeclaredType,
                arguments[leftIndex],
                leftIndex,
                (left, leftKind) => ProjectSelectScalar(
                    plan,
                    expression.Children[rightIndex].Type.DeclaredType,
                    arguments[rightIndex],
                    rightIndex,
                    (right, rightKind) => Compare(left, leftKind, right, rightKind, operation),
                    unknown,
                    "global::TedToolkit.Step21.LogicalValue",
                    name,
                    allowNonNumeric: false,
                    []),
                unknown,
                "global::TedToolkit.Step21.LogicalValue",
                name,
                allowNonNumeric: false,
                []);
        }

        if (expression.Children.Count == 3 && arguments.Count == 3)
        {
            var operations = expression.Operation!.Split(',');
            var lower = ProjectPair(0, 1, operations[0], "OrderedLower");
            var upper = ProjectPair(1, 2, operations[1], "OrderedUpper");
            var typedUpper = $"((global::TedToolkit.Step21.LogicalValue)({upper}))";
            return $"(({lower}) switch {{ {falseValue} => {falseValue}, "
                + $"{trueValue} => {typedUpper}, _ => {typedUpper} switch {{ "
                + $"{falseValue} => {falseValue}, _ => {unknown} }} }})";
        }

        return ProjectPair(0, 1, expression.Operation!, "Ordered");
    }

    private static string ResolveSelectLike(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        IReadOnlyList<string> arguments)
    {
        const string unknown = "global::TedToolkit.Step21.LogicalValue.Unknown";
        return ProjectSelectScalar(
            plan,
            expression.Children[0].Type.DeclaredType,
            arguments[0],
            0,
            (left, leftKind) => ProjectSelectScalar(
                plan,
                expression.Children[1].Type.DeclaredType,
                arguments[1],
                1,
                (right, rightKind) => leftKind == ExpressScalarKind.String
                        && rightKind == ExpressScalarKind.String
                    ? $"(({ExpressExpressionEmitter.LikeMatch(left, right)}) ? "
                        + "global::TedToolkit.Step21.LogicalValue.True : "
                        + "global::TedToolkit.Step21.LogicalValue.False)"
                    : unknown,
                unknown,
                "global::TedToolkit.Step21.LogicalValue",
                "LikeRight",
                allowNonNumeric: true,
                []),
            unknown,
            "global::TedToolkit.Step21.LogicalValue",
            "LikeLeft",
            allowNonNumeric: true,
            []);
    }

    private static string ResolveNumericSelectBinary(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        IReadOnlyList<string> arguments)
    {
        var concatenatedKind = expression.Operation == "+"
            ? expression.Children
                .Select(child => child.Type.Kind)
                .FirstOrDefault(kind => kind is ExpressExpressionTypeKind.Binary
                    or ExpressExpressionTypeKind.String)
            : ExpressExpressionTypeKind.Unresolved;
        if (concatenatedKind is ExpressExpressionTypeKind.Binary or ExpressExpressionTypeKind.String)
        {
            var scalarKind = concatenatedKind == ExpressExpressionTypeKind.Binary
                ? ExpressScalarKind.Binary
                : ExpressScalarKind.String;
            var resultType = concatenatedKind == ExpressExpressionTypeKind.Binary
                ? "global::TedToolkit.Step21.BinaryValue"
                : "global::System.String";
            var concatenationFallback = $"({resultType}?)null";
            string Concatenate(string left, string right)
            {
                return concatenatedKind == ExpressExpressionTypeKind.Binary
                    ? "new global::TedToolkit.Step21.BinaryValue(global::System.String.Concat("
                        + $"({left}).ToString(), ({right}).ToString()))"
                    : $"global::System.String.Concat(({left}), ({right}))";
            }

            return ProjectSelectScalar(
                plan,
                expression.Children[0].Type.DeclaredType ?? ScalarTypeOf(expression.Children[0]),
                arguments[0],
                0,
                (left, leftKind) => leftKind == scalarKind
                    ? ProjectSelectScalar(
                        plan,
                        expression.Children[1].Type.DeclaredType ?? ScalarTypeOf(expression.Children[1]),
                        arguments[1],
                        1,
                        (right, rightKind) => rightKind == scalarKind
                            ? Concatenate(left, right)
                            : concatenationFallback,
                        concatenationFallback,
                        resultType + "?",
                        "ConcatenatedRight",
                        allowNonNumeric: true,
                        [])
                    : concatenationFallback,
                concatenationFallback,
                resultType + "?",
                "ConcatenatedLeft",
                allowNonNumeric: true,
                []);
        }

        var targetKind = expression.Type.Kind switch
        {
            ExpressExpressionTypeKind.Integer => ExpressScalarKind.Integer,
            ExpressExpressionTypeKind.Number => ExpressScalarKind.Number,
            ExpressExpressionTypeKind.Real => ExpressScalarKind.Real,
            _ => throw new InvalidOperationException(
                "SELECT arithmetic requires a numeric result type."),
        };
        var fallbackType = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundScalarType(
            targetKind,
            constraintText: null,
            isFixed: false,
            expression.Span));
        var fallback = $"({fallbackType}?)null";

        static ExpressExpressionTypeKind ExpressionKind(ExpressScalarKind kind)
        {
            return kind switch
            {
                ExpressScalarKind.Integer => ExpressExpressionTypeKind.Integer,
                ExpressScalarKind.Number => ExpressExpressionTypeKind.Number,
                ExpressScalarKind.Real => ExpressExpressionTypeKind.Real,
                _ => ExpressExpressionTypeKind.Unresolved,
            };
        }

        string? Promote(string value, ExpressScalarKind source)
        {
            return (source, targetKind) switch
            {
                (ExpressScalarKind.Integer, ExpressScalarKind.Integer) => value,
                (ExpressScalarKind.Number, ExpressScalarKind.Integer) => $"({value}).ToIntegerTruncated()",
                (ExpressScalarKind.Integer, ExpressScalarKind.Number) =>
                    $"global::TedToolkit.Step21.NumberValue.FromInteger({value})",
                (ExpressScalarKind.Number, ExpressScalarKind.Number) => value,
                (ExpressScalarKind.Real, ExpressScalarKind.Number) =>
                    $"global::TedToolkit.Step21.NumberValue.FromReal({value})",
                (ExpressScalarKind.Integer, ExpressScalarKind.Real) =>
                    $"new global::TedToolkit.Step21.RealValue(({value}), global::System.Numerics.BigInteger.Zero)",
                (ExpressScalarKind.Number, ExpressScalarKind.Real) => $"({value}).ToReal()",
                (ExpressScalarKind.Real, ExpressScalarKind.Real) => value,
                _ => null,
            };
        }

        return ProjectSelectScalar(
            plan,
            expression.Children[0].Type.DeclaredType,
            arguments[0],
            0,
            (left, leftKind) => Promote(left, leftKind) is { } promotedLeft
                ? ProjectSelectScalar(
                    plan,
                    expression.Children[1].Type.DeclaredType,
                    arguments[1],
                    1,
                    (right, rightKind) => Promote(right, rightKind) is { } promotedRight
                        ? ExpressExpressionEmitter.EmitNumericBinary(
                            expression,
                            expression.Operation!,
                            expression.Children[0],
                            promotedLeft,
                            expression.Children[1],
                            promotedRight,
                            ExpressionKind(targetKind),
                            ExpressionKind(targetKind))
                        : fallback,
                    fallback,
                    fallbackType + "?",
                    "Numeric",
                    allowNonNumeric: false,
                    [])
                : fallback,
            fallback,
            fallbackType + "?",
            "Numeric",
            allowNonNumeric: false,
            []);
    }

    private static string ResolveNumericSelectUnary(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        string argument)
    {
        var operand = expression.Children[0];
        var carrier = ResolveScalarSelectCarrierType(plan, operand)
            ?? throw new InvalidOperationException("SELECT unary operation has no nominal carrier.");
        var resultType = ExpressExpressionEmitter.BoundTypeName(carrier);
        var fallback = $"({resultType}?)null";
        return ProjectSelectScalar(
            plan,
            carrier,
            argument,
            0,
            (value, kind) =>
            {
                var operated = expression.Operation == "-" ? $"(-({value}))" : value;
                return TryAdaptScalarUnionElement(
                    plan,
                    new ExpressBoundScalarType(kind, null, false, expression.Span),
                    carrier,
                    operated,
                    new HashSet<ExpressBoundSymbol>(),
                    out var adapted,
                    out _)
                        ? adapted
                        : fallback;
            },
            fallback,
            resultType + "?",
            "Unary",
            allowNonNumeric: false,
            []);
    }

    private static string ResolveSelectScalarBuiltIn(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        IReadOnlyList<string> arguments)
    {
        var operation = expression.Operation!.ToUpperInvariant();
        var firstCarrier = ResolveScalarSelectCarrierType(plan, expression.Children[0])
            ?? throw new InvalidOperationException($"SELECT built-in {operation} has no nominal carrier.");
        if (operation is "LENGTH" or "BLENGTH")
        {
            const string fallback = "(global::System.Numerics.BigInteger?)null";
            return ProjectSelectScalar(
                plan,
                firstCarrier,
                arguments[0],
                0,
                (value, kind) => kind is ExpressScalarKind.String or ExpressScalarKind.Binary
                    ? $"new global::System.Numerics.BigInteger(({value}).Length)"
                    : fallback,
                fallback,
                "global::System.Numerics.BigInteger?",
                "Length",
                allowNonNumeric: true,
                []);
        }

        if (operation == "ODD")
        {
            const string unknown = "global::TedToolkit.Step21.LogicalValue.Unknown";
            return ProjectSelectScalar(
                plan,
                firstCarrier,
                arguments[0],
                0,
                (value, kind) => kind == ExpressScalarKind.Integer
                    ? $"(({value}).IsEven ? global::TedToolkit.Step21.LogicalValue.False : "
                        + "global::TedToolkit.Step21.LogicalValue.True)"
                    : unknown,
                unknown,
                "global::TedToolkit.Step21.LogicalValue",
                "Odd",
                allowNonNumeric: true,
                []);
        }

        if (operation == "VALUE")
        {
            const string fallback = "(global::TedToolkit.Step21.NumberValue?)null";
            var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                + "_"
                + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
            return ProjectSelectScalar(
                plan,
                firstCarrier,
                arguments[0],
                0,
                (value, kind) => kind == ExpressScalarKind.String
                    ? "(global::TedToolkit.Step21.NumberValue.TryParse("
                        + $"({value}), out var __expressSelectedNumber_{suffix}) "
                        + $"? __expressSelectedNumber_{suffix} : {fallback})"
                    : fallback,
                fallback,
                "global::TedToolkit.Step21.NumberValue?",
                "Value",
                allowNonNumeric: true,
                []);
        }

        if (operation == "FORMAT")
        {
            const string fallback = "(global::System.String?)null";
            string Format(string number, ExpressScalarKind numberKind, string format)
            {
                var promoted = numberKind switch
                {
                    ExpressScalarKind.Integer =>
                        $"global::TedToolkit.Step21.NumberValue.FromInteger({number})",
                    ExpressScalarKind.Real =>
                        $"global::TedToolkit.Step21.NumberValue.FromReal({number})",
                    ExpressScalarKind.Number => number,
                    _ => "default(global::TedToolkit.Step21.NumberValue)",
                };
                var suffix = expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
                var value = "__expressSelectedFormatValue_" + suffix;
                var text = "__expressSelectedFormatText_" + suffix;
                return "((global::System.Func<global::TedToolkit.Step21.NumberValue, "
                    + "global::System.String, global::System.String?>)("
                    + $"({value}, {text}) => {{ try {{ return {value}.Format({text}); }} "
                    + "catch (global::System.ArgumentException) { return null; } }))"
                    + $"({promoted}, {format})";
            }

            string WithFormat(string number, ExpressScalarKind numberKind)
            {
                var secondCarrier = ResolveScalarSelectCarrierType(plan, expression.Children[1]);
                return secondCarrier is null
                    ? Format(number, numberKind, arguments[1])
                    : ProjectSelectScalar(
                        plan,
                        secondCarrier,
                        arguments[1],
                        1,
                        (format, formatKind) => formatKind == ExpressScalarKind.String
                            ? Format(number, numberKind, format)
                            : fallback,
                        fallback,
                        "global::System.String?",
                        "FormatText",
                        allowNonNumeric: true,
                        []);
            }

            return ProjectSelectScalar(
                plan,
                firstCarrier,
                arguments[0],
                0,
                (number, numberKind) => numberKind is ExpressScalarKind.Integer
                    or ExpressScalarKind.Real
                    or ExpressScalarKind.Number
                        ? WithFormat(number, numberKind)
                        : fallback,
                fallback,
                "global::System.String?",
                "FormatValue",
                allowNonNumeric: true,
                []);
        }

        var resultType = ExpressExpressionEmitter.BoundTypeName(firstCarrier);
        var incompatible = $"({resultType}?)null";
        return ProjectSelectScalar(
            plan,
            firstCarrier,
            arguments[0],
            0,
            (value, kind) =>
            {
                var absolute = kind switch
                {
                    ExpressScalarKind.Integer => $"global::System.Numerics.BigInteger.Abs({value})",
                    ExpressScalarKind.Number => $"(({value}) < global::TedToolkit.Step21.NumberValue.FromInteger(0) "
                        + $"? -({value}) : ({value}))",
                    ExpressScalarKind.Real => $"(({value}).Significand.Sign < 0 ? -({value}) : ({value}))",
                    _ => null,
                };
                return absolute is not null
                    && TryAdaptScalarUnionElement(
                        plan,
                        new ExpressBoundScalarType(kind, null, false, expression.Span),
                        firstCarrier,
                        absolute,
                        new HashSet<ExpressBoundSymbol>(),
                        out var adapted,
                        out _)
                            ? adapted
                            : incompatible;
            },
            incompatible,
            resultType + "?",
            "Absolute",
            allowNonNumeric: false,
            []);
    }

    private static ExpressBoundNamedType? ResolveScalarSelectCarrierType(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression)
    {
        if (ResolveSelectCarrierType(plan, expression) is { } carrier)
        {
            return carrier;
        }

        if (expression.Kind != ExpressExpressionKind.IndexQualifier
            || expression.Children.Count == 0
            || expression.Children[0].Type.DeclaredType is not { } sourceType
            || plan.Resolver.GetAggregateType(sourceType)?.ElementType is not ExpressBoundNamedType element
            || element.Declaration.Kind == ExpressDeclarationKind.Entity
            || plan.Resolver.GetDefinedType(element.Declaration).UnderlyingType
                is not ExpressBoundSelectType)
        {
            return null;
        }

        return element;
    }

    private static string ResolveSelectScalarValue(
        ExpressReachableRulePlan plan,
        ExpressBoundType carrierType,
        string carrier,
        ExpressBoundScalarType target)
    {
        var targetName = ExpressExpressionEmitter.BoundTypeName(target);
        bool AllAlternativesConvert(ExpressBoundType type, HashSet<ExpressBoundSymbol> visited)
        {
            while (type is ExpressBoundNamedType named
                   && named.Declaration.Kind != ExpressDeclarationKind.Entity)
            {
                if (!visited.Add(named.Declaration))
                {
                    return false;
                }

                var underlying = plan.Resolver.GetDefinedType(named.Declaration).UnderlyingType;
                if (underlying is ExpressBoundSelectType select)
                {
                    return plan.Resolver.GetSelectAlternatives(select).All(alternative =>
                        AllAlternativesConvert(
                            new ExpressBoundNamedType(alternative, select.Span),
                            new HashSet<ExpressBoundSymbol>(visited)));
                }

                type = underlying;
            }

            return type is ExpressBoundScalarType scalar
                && CanProjectScalar(scalar.Kind, target.Kind);
        }

        var allAlternativesConvert = AllAlternativesConvert(carrierType, []);
        var incompatible = allAlternativesConvert
            ? "throw new global::System.InvalidOperationException()"
            : $"({targetName}?)null";
        string Convert(string value, ExpressScalarKind source)
        {
            return (source, target.Kind) switch
            {
                (ExpressScalarKind.Integer, ExpressScalarKind.Number) =>
                    $"global::TedToolkit.Step21.NumberValue.FromInteger({value})",
                (ExpressScalarKind.Real, ExpressScalarKind.Number) =>
                    $"global::TedToolkit.Step21.NumberValue.FromReal({value})",
                (ExpressScalarKind.Integer, ExpressScalarKind.Real) =>
                    $"new global::TedToolkit.Step21.RealValue(({value}), "
                    + "global::System.Numerics.BigInteger.Zero)",
                (ExpressScalarKind.Number, ExpressScalarKind.Integer) =>
                    $"({value}).ToIntegerTruncated()",
                (ExpressScalarKind.Number, ExpressScalarKind.Real) => $"({value}).ToReal()",
                _ when source == target.Kind => value,
                _ => incompatible,
            };
        }

        return ProjectSelectScalar(
            plan,
            carrierType,
            carrier,
            0,
            Convert,
            incompatible,
            targetName + (allAlternativesConvert ? "" : "?"),
            "Projected",
            allowNonNumeric: true,
            []);
    }

    private static string ResolveGenericValueToTarget(
        ExpressReachableRulePlan plan,
        ExpressBoundType target,
        string source,
        string variablePrefix)
    {
        var targetName = ExpressExpressionEmitter.BoundTypeName(target);
        var direct = variablePrefix + "Direct";
        var branches = new List<string>() { $"{targetName} {direct} => {direct}", };
        var underlying = target;
        var wrappers = ResolveTransparentDefinedWrappers(plan.Resolver, ref underlying);
        if (wrappers.Count > 0)
        {
            var underlyingName = ExpressExpressionEmitter.BoundTypeName(underlying);
            var primitive = variablePrefix + "Primitive";
            var adapted = primitive;
            for (var index = wrappers.Count - 1; index >= 0; index--)
            {
                adapted = "new "
                    + ExpressExpressionEmitter.BoundTypeName(wrappers[index])
                    + $"({adapted})";
            }

            branches.Add($"{underlyingName} {primitive} => {adapted}");
        }

        return $"((object?)({source})) switch {{ {string.Join(", ", branches)}, "
            + $"_ => ({targetName}?)null }}";
    }

    private static string ProjectSelectScalar(
        ExpressReachableRulePlan plan,
        ExpressBoundType? type,
        string value,
        int depth,
        Func<string, ExpressScalarKind, string> continuation,
        string incompatible,
        string resultType,
        string name,
        bool allowNonNumeric,
        HashSet<ExpressBoundSymbol> visited)
    {
        while (type is ExpressBoundNamedType named
               && named.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            if (!visited.Add(named.Declaration))
            {
                return incompatible;
            }

            var underlying = plan.Resolver.GetDefinedType(named.Declaration).UnderlyingType;
            if (underlying is ExpressBoundSelectType select)
            {
                var branches = plan.Resolver.GetSelectAlternatives(select)
                    .Select((alternative, index) =>
                    {
                        var selected = "__express" + name + "Select"
                            + depth.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + index.ToString(CultureInfo.InvariantCulture);
                        return selected + " => " + ProjectSelectScalar(
                            plan,
                            new ExpressBoundNamedType(alternative, select.Span),
                            selected,
                            depth + 1,
                            continuation,
                            incompatible,
                            resultType,
                            name,
                            allowNonNumeric,
                            new HashSet<ExpressBoundSymbol>(visited));
                    });
                return $"({value}).Match<{resultType}>({string.Join(", ", branches)})";
            }

            value = $"({value}).Value";
            type = underlying;
        }

        if (type is not ExpressBoundScalarType scalar)
        {
            return incompatible;
        }

        return allowNonNumeric
            || scalar.Kind is ExpressScalarKind.Integer
                or ExpressScalarKind.Number
                or ExpressScalarKind.Real
            ? continuation(value, scalar.Kind)
            : incompatible;
    }

    private static string LowerTypeOf(
        ExpressReachableRulePlan plan,
        ExpressBoundType? type,
        string value,
        int unwrappedDepth = 0)
    {
        while (unwrappedDepth > 0
               && type is ExpressBoundNamedType unwrappedName
               && unwrappedName.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            type = plan.Resolver.GetDefinedType(unwrappedName.Declaration).UnderlyingType;
            unwrappedDepth--;
        }

        if (type is ExpressBoundGenericType)
        {
            return $"__ExpressGenericTypeOf({value})";
        }

        var names = new List<string>();
        while (type is ExpressBoundNamedType named)
        {
            if (named.Declaration.Kind == ExpressDeclarationKind.Entity)
            {
                return $"__ExpressTypeOf((global::TedToolkit.Step21.Entity)({value}))";
            }

            var defined = plan.Resolver.GetDefinedType(named.Declaration);
            if (defined.UnderlyingType is ExpressBoundSelectType select)
            {
                var selectName = $"{named.Declaration.DeclaringSchema.Name}.{named.Declaration.Name}"
                    .ToUpperInvariant();
                return "__ExpressIncludeTypeOfName("
                    + $"{SelectTypeOfMethodName(named.Declaration)}({value}), \"{selectName}\")";
            }

            if (defined.UnderlyingType is ExpressBoundAggregateType aggregate)
            {
                type = aggregate;
                break;
            }

            names.Add($"{named.Declaration.DeclaringSchema.Name}.{named.Declaration.Name}".ToUpperInvariant());
            value = $"({value}).Value";
            type = defined.UnderlyingType;
        }

        if (type is ExpressBoundScalarType scalar)
        {
            if (scalar.Kind == ExpressScalarKind.Number)
            {
                var declaredNames = names.Append("NUMBER").Distinct(StringComparer.Ordinal).ToArray();
                var integerItems = string.Join(", ", declaredNames.Append("INTEGER")
                    .Distinct(StringComparer.Ordinal)
                    .Select(name => $"\"{name}\""));
                var realItems = string.Join(", ", declaredNames.Append("REAL")
                    .Distinct(StringComparer.Ordinal)
                    .Select(name => $"\"{name}\""));
                return $"({value}).Kind switch {{ "
                    + "global::TedToolkit.Step21.NumberValueKind.Integer => "
                    + "new global::TedToolkit.Step21.ExpressSet<global::System.String>(0) "
                    + $"{{ {integerItems} }}, "
                    + "global::TedToolkit.Step21.NumberValueKind.Real => "
                    + "new global::TedToolkit.Step21.ExpressSet<global::System.String>(0) "
                    + $"{{ {realItems} }}, "
                    + "_ => new global::TedToolkit.Step21.ExpressSet<global::System.String>(0) }";
            }

            names.AddRange(scalar.Kind switch
            {
                ExpressScalarKind.Integer => ["INTEGER", "NUMBER",],
                ExpressScalarKind.Real => ["REAL", "NUMBER",],
                ExpressScalarKind.Boolean => ["BOOLEAN", "LOGICAL",],
                ExpressScalarKind.Binary => ["BINARY",],
                ExpressScalarKind.Logical => ["LOGICAL",],
                ExpressScalarKind.String => ["STRING",],
                _ => throw new InvalidOperationException(
                    $"Unsupported TYPEOF scalar kind '{scalar.Kind.ToString()}'."),
            });
        }
        else if (type is ExpressBoundAggregateType aggregate)
        {
            var kind = aggregate.Kind switch
            {
                ExpressAggregateKind.Array => "ARRAY",
                ExpressAggregateKind.Bag => "BAG",
                ExpressAggregateKind.List => "LIST",
                ExpressAggregateKind.Set => "SET",
                _ => throw new InvalidOperationException(
                    $"Unsupported TYPEOF aggregate kind '{aggregate.Kind.ToString()}'."),
            };
            return "new global::TedToolkit.Step21.ExpressSet<global::System.String>(0)"
                + $" {{ \"{kind}\" }}";
        }

        if (names.Count > 0)
        {
            var items = string.Join(", ", names.Distinct(StringComparer.Ordinal).Select(name => $"\"{name}\""));
            return "new global::TedToolkit.Step21.ExpressSet<global::System.String>(0)"
                + $" {{ {items} }}";
        }

        return $"__ExpressTypeOf((global::TedToolkit.Step21.Entity)({value}))";
    }

    private static string SelectTypeOfMethodName(ExpressBoundSymbol symbol)
    {
        return "__ExpressTypeOf"
            + ExpressEntityProjection.ToPascalCase(symbol.DeclaringSchema.Name)
            + ExpressEntityProjection.ToPascalCase(symbol.Name);
    }

    private static string LowerComplexConstructor(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        IReadOnlyList<string> arguments)
    {
        var components = new List<ExpressBoundExpression>() { expression, };
        for (var index = 0; index < components.Count; index++)
        {
            var component = components[index];
            if (component.Kind == ExpressExpressionKind.Binary
                && component.Operation == "||"
                && component.Children.All(child => child.Type.Kind == ExpressExpressionTypeKind.Entity))
            {
                components.RemoveAt(index);
                components.InsertRange(index, component.Children);
                index--;
            }
        }

        var target = plan.GetComplexConstructionTarget(expression)
            ?? throw new InvalidOperationException(
                "Complex entity construction has no unique most-specific supplied projection.");
        var variables = Enumerable.Range(0, arguments.Count)
            .Select(index => $"__complexArgument{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
            .ToArray();
        var supplied = new Dictionary<(ExpressBoundSymbol Entity, string Attribute), string>();
        var argumentIndex = 0;
        var temporaryOrdinal = 0;
        Func<string, string> allocateTemporaryName = prefix => prefix
            + expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
            + "_"
            + (temporaryOrdinal++).ToString(CultureInfo.InvariantCulture);
        foreach (var component in components)
        {
            if (component.Kind == ExpressExpressionKind.Application
                && component.Reference is
                {
                    Kind: ExpressBoundNameKind.Entity,
                    SchemaDeclaration: { } componentSymbol,
                })
            {
                var projection = plan.EntityProjections.Single(candidate => ReferenceEquals(
                    candidate.Entity.Symbol,
                    componentSymbol));
                var constructorAttributes = ExpressComplexEntityProjection.GetComponentAttributes(projection);
                for (var attributeIndex = 0; attributeIndex < constructorAttributes.Count; attributeIndex++)
                {
                    var attribute = constructorAttributes[attributeIndex];
                    var actual = component.Children[attributeIndex];
                    var value = variables[argumentIndex++];
                    var targetAttribute = target.EffectiveAttributes.SingleOrDefault(candidate =>
                        ReferenceEquals(candidate.StorageEntity.Symbol, attribute.StorageEntity.Symbol)
                        && StringComparer.OrdinalIgnoreCase.Equals(
                            candidate.StorageAttributeName,
                            attribute.StorageAttributeName));
                    var attributeType = targetAttribute?.Type ?? attribute.Type;
                    var definedTypes = new List<ExpressBoundNamedType>();
                    while (attributeType is ExpressBoundNamedType definedType
                           && definedType.Declaration.Kind != ExpressDeclarationKind.Entity)
                    {
                        definedTypes.Add(definedType);
                        attributeType = plan.Resolver.GetDefinedType(definedType.Declaration).UnderlyingType;
                    }

                    var actualNominal = actual.Type.DeclaredType as ExpressBoundNamedType;
                    var primitiveActual = actualNominal is null;
                    var sameDefinedActual = definedTypes.Count > 0
                        && actualNominal is not null
                        && ReferenceEquals(actualNominal.Declaration, definedTypes[0].Declaration);
                    var exactScalar = attributeType is ExpressBoundScalarType targetScalar
                        && targetScalar.Kind switch
                        {
                            ExpressScalarKind.Binary => actual.Type.Kind == ExpressExpressionTypeKind.Binary,
                            ExpressScalarKind.Boolean => actual.Type.Kind == ExpressExpressionTypeKind.Boolean,
                            ExpressScalarKind.Integer => actual.Type.Kind == ExpressExpressionTypeKind.Integer,
                            ExpressScalarKind.Logical => actual.Type.Kind == ExpressExpressionTypeKind.Logical,
                            ExpressScalarKind.Number => actual.Type.Kind == ExpressExpressionTypeKind.Number,
                            ExpressScalarKind.Real => actual.Type.Kind == ExpressExpressionTypeKind.Real,
                            ExpressScalarKind.String => actual.Type.Kind == ExpressExpressionTypeKind.String,
                            _ => false,
                        };
                    var widensToReal = actual.Type.Kind == ExpressExpressionTypeKind.Integer
                        && attributeType is ExpressBoundScalarType { Kind: ExpressScalarKind.Real, };
                    if (attributeType is ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, }
                        && IsLogicalOperatorExpression(actual))
                    {
                        value = $"(({value}) switch {{ "
                            + "global::TedToolkit.Step21.LogicalValue.True => true, "
                            + "global::TedToolkit.Step21.LogicalValue.False => false, "
                            + "_ => throw new global::System.InvalidOperationException() })";
                    }

                    if (exactScalar || widensToReal)
                    {
                        if (widensToReal)
                        {
                            value = ExpressExpressionEmitter.PromoteNumeric(
                                actual,
                                value,
                                ExpressExpressionTypeKind.Real);
                        }

                        for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                        {
                            value = "new "
                                + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                                + $"({value})";
                        }
                    }

                    if (attributeType is ExpressBoundSelectType
                        && definedTypes.Count > 0
                        && actualNominal is { Declaration.Kind: ExpressDeclarationKind.Entity, })
                    {
                        var selectedEntity = ResolveEntityToSelectValue(
                            plan,
                            definedTypes[0],
                            actualNominal.Declaration,
                            value);
                        var runtimeSelected = selectedEntity is null;
                        selectedEntity ??= ResolveRuntimeEntityToSelectValue(
                            plan,
                            definedTypes[0],
                            actualNominal,
                            value,
                            allocateTemporaryName);
                        if (selectedEntity is not null)
                        {
                            if (runtimeSelected)
                            {
                                var presentSelected = allocateTemporaryName("__expressConstructedSelect");
                                selectedEntity = $"(({selectedEntity}) is {{ }} {presentSelected} ? "
                                    + $"{presentSelected} : throw new global::System.InvalidOperationException())";
                            }

                            value = selectedEntity;
                        }
                    }

                    if (attributeType is ExpressBoundAggregateType emptyTargetAggregate
                        && actual.Kind == ExpressExpressionKind.AggregateInitializer
                        && actual.Children.Count == 0
                        && emptyTargetAggregate.Kind == ExpressAggregateKind.Set
                        && string.Equals(
                            emptyTargetAggregate.ResolvedLowerBoundText ?? emptyTargetAggregate.LowerBoundText,
                            "0",
                            StringComparison.Ordinal))
                    {
                        value = "(" + ExpressExpressionEmitter.BoundTypeName(emptyTargetAggregate) + ")[]";
                    }
                    else if (attributeType is ExpressBoundAggregateType targetAggregate
                        && ResolveExpressionAggregateType(plan, actual, targetAggregate)
                            is { } sourceAggregate
                        && ResolveAggregateValueToTarget(
                            plan,
                            sourceAggregate,
                            targetAggregate,
                            value,
                            "__expressConstructedAggregate_"
                                + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture),
                            sourceCanBeIndeterminate: false,
                            materializeEquivalent: true) is { } constructedAggregate)
                    {
                        value = constructedAggregate;
                    }

                    if (attributeType is ExpressBoundNamedType
                        { Declaration.Kind: ExpressDeclarationKind.Entity, } targetEntity
                        && ResolveSelectCarrierType(plan, actual) is { } selectCarrier)
                    {
                        value = actual.Type.DeclaredType is ExpressBoundNamedType
                        { Declaration.Kind: ExpressDeclarationKind.Entity, } staticallyNarrowed
                            && plan.EntityProjections.Single(projection =>
                                    projection.Entity.Symbol == staticallyNarrowed.Declaration)
                                .PhysicalComponents.Any(component =>
                                    component.Symbol == targetEntity.Declaration)
                                ? ResolveNarrowedEntityCarrier(
                                    plan,
                                    selectCarrier,
                                    value,
                                    targetEntity.Declaration)
                                : ResolveSelectToEntityValue(
                                    plan,
                                    actual,
                                    value,
                                    targetEntity,
                                    allocateTemporaryName);
                    }

                    supplied.Add(
                        (attribute.StorageEntity.Symbol, attribute.StorageAttributeName),
                        value);
                }

                continue;
            }

            var sourceType = (ExpressBoundNamedType)component.Type.DeclaredType!;
            var source = plan.EntityProjections.Single(candidate => ReferenceEquals(
                candidate.Entity.Symbol,
                sourceType.Declaration));
            var sourceVariable = variables[argumentIndex++];
            foreach (var attribute in source.EffectiveAttributes)
            {
                supplied.Add(
                    (attribute.StorageEntity.Symbol, attribute.StorageAttributeName),
                    $"({sourceVariable}).{attribute.StorageMemberName}");
            }
        }

        var constructorArguments = target.EffectiveAttributes
            .Where(attribute => !attribute.Attribute.IsOptional)
            .Select(attribute => supplied[(attribute.StorageEntity.Symbol, attribute.StorageAttributeName)]);
        var initializers = target.EffectiveAttributes
            .Where(attribute => attribute.Attribute.IsOptional
                && supplied.ContainsKey((attribute.StorageEntity.Symbol, attribute.StorageAttributeName)))
            .Select(attribute => $"{attribute.StorageMemberName} = "
                + supplied[(attribute.StorageEntity.Symbol, attribute.StorageAttributeName)])
            .ToArray();
        var construction = $"new {target.Name}({string.Join(", ", constructorArguments)})"
            + (initializers.Length == 0 ? "" : $" {{ {string.Join(", ", initializers)} }}");
        if (arguments.Count == 0)
        {
            return construction;
        }

        if (arguments.Count == 1)
        {
            return $"({arguments[0]}) switch {{ var {variables[0]} => {construction} }}";
        }

        return $"(({string.Join(", ", arguments.Select(argument => $"({argument})"))})) switch "
            + $"{{ var ({string.Join(", ", variables)}) => {construction} }}";
    }

    private static string FunctionInvocation(
        ExpressReachableRulePlan plan,
        ExpressBoundSymbol symbol,
        string populationExpression)
    {
        var declaration = (ExpressBoundOpaqueDeclaration)plan.GetDeclaration(symbol);
        var parameterTypes = plan.Analysis.GetDeclaration(declaration).RequiredChild("functionHead")
            .ChildRules("formalParameter")
            .SelectMany(formal => formal.ChildRules("parameterId"))
            .Select(parameter => plan.Schema.LexicalNames
                .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                    && SameStart(candidate.Span, parameter.Span))
                .Distinct()
                .Single())
            .Select(parameter => parameter.Type is ExpressBoundAggregateType aggregate
                ? ExpressExpressionEmitter.AggregateInterfaceTypeName(aggregate)
                : ExpressExpressionEmitter.BoundTypeName(parameter.Type!))
            .ToArray();
        var parameters = Enumerable.Range(0, parameterTypes.Length)
            .Select(index => $"__argument{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
            .ToArray();
        var invocationArguments = parameters.Append(
            populationExpression + ValidationContextArgumentSuffix(plan));
        var returnType = ExpressExpressionEmitter.BoundTypeName(declaration.DeclaredType!)
            + (plan.Schema.IndeterminateFunctions.Contains(symbol) ? "?" : "");
        var delegateTypes = parameterTypes.Append(returnType);
        return $"((global::System.Func<{string.Join(", ", delegateTypes)}>)("
            + $"({string.Join(", ", parameters)}) => "
            + $"{FunctionMethodName(plan, symbol)}({string.Join(", ", invocationArguments)})))";
    }

    private static string ResolveApplication(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        IReadOnlyList<string> arguments,
        string populationExpression,
        IReadOnlyDictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings,
        IReadOnlyList<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>? pathNarrowings,
        ExpressBoundSymbol? selfEntity)
    {
        var symbol = expression.Reference!.SchemaDeclaration!;
        var declaration = plan.GetDeclaration(symbol);
        if (declaration is ExpressBoundOpaqueDeclaration genericFunction
            && ExpressTypeAnalysis.GenericTypeLabels([genericFunction.DeclaredType!,]).Count > 0)
        {
            var genericArguments = arguments.ToArray();
            var genericFormalTypes = plan.Analysis.GetDeclaration(genericFunction)
                .RequiredChild("functionHead")
                .ChildRules("formalParameter")
                .SelectMany(formal => formal.ChildRules("parameterId"))
                .Select(parameter => plan.Schema.LexicalNames
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                        && SameStart(candidate.Span, parameter.Span))
                    .Distinct()
                    .Single()
                    .Type!)
                .ToArray();
            if (genericFormalTypes.Length == expression.Children.Count)
            {
                for (var index = 0; index < genericFormalTypes.Length; index++)
                {
                    if (genericFormalTypes[index] is not ExpressBoundAggregateType
                        { Kind: ExpressAggregateKind.Bag, ElementType: ExpressBoundGenericType, } genericBag
                        || ResolveExpressionAggregateType(plan, expression.Children[index], genericBag)
                            is not { Kind: ExpressAggregateKind.Set, } sourceSet)
                    {
                        continue;
                    }

                    var concreteBag = new ExpressBoundAggregateType(
                        ExpressAggregateKind.Bag,
                        sourceSet.ElementType,
                        genericBag.LowerBoundText,
                        genericBag.UpperBoundText,
                        genericBag.IsOptional,
                        genericBag.IsUnique,
                        genericBag.TypeLabel,
                        genericBag.Span,
                        genericBag.ResolvedLowerBoundText,
                        genericBag.ResolvedUpperBoundText);
                    genericArguments[index] = $"({ExpressExpressionEmitter.BoundTypeName(concreteBag)})"
                        + $"[..({genericArguments[index]})]";
                }
            }

            return $"{FunctionMethodName(plan, symbol)}({string.Join(", ", genericArguments.Append(
                populationExpression + ValidationContextArgumentSuffix(plan)))})";
        }

        var emittedArguments = arguments.ToArray();
        ExpressBoundType[] formalTypes;
        string invocation;
        ExpressBoundSymbol? invokedFunction = null;
        IReadOnlyList<ExpressEntityAttributeProjection>? invokedEntityAttributes = null;
        if (declaration is ExpressBoundEntity entity)
        {
            var projection = plan.EntityProjections.Single(candidate => candidate.Entity == entity);
            var componentAttributes = ExpressComplexEntityProjection.GetComponentAttributes(projection);
            invokedEntityAttributes = componentAttributes.Count == expression.Children.Count
                ? componentAttributes
                : projection.EffectiveAttributes;
            formalTypes = invokedEntityAttributes
                .Select(attribute => attribute.Type)
                .ToArray();
            invocation = $"new {projection.Name}";
        }
        else
        {
            var function = (ExpressBoundOpaqueDeclaration)declaration;
            formalTypes = plan.Analysis.GetDeclaration(function).RequiredChild("functionHead")
                .ChildRules("formalParameter")
                .SelectMany(formal => formal.ChildRules("parameterId"))
                .Select(parameter => plan.Schema.LexicalNames
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                        && SameStart(candidate.Span, parameter.Span))
                    .Distinct()
                    .Single()
                    .Type!)
                .ToArray();
            invocation = "";
            invokedFunction = symbol;
        }

        var dynamicArguments = new List<(
            string Carrier,
            string Placeholder,
            IReadOnlyList<(string Pattern, string? Value)> Branches,
            bool UsesSelectMatch,
            bool HasStaticProof,
            ExpressBoundNamedType? SelectEntityCarrier,
            ExpressBoundNamedType? SelectEntityTarget)>();
        if (formalTypes.Length == expression.Children.Count
            && formalTypes.Length == emittedArguments.Length)
        {
            for (var index = 0; index < formalTypes.Length; index++)
            {
                var targetType = formalTypes[index];
                var actual = expression.Children[index];
                var wasEntityAdapted = false;
                if (targetType is ExpressBoundScalarType { Kind: ExpressScalarKind.Logical, }
                    && actual.Type.Kind == ExpressExpressionTypeKind.Boolean)
                {
                    emittedArguments[index] = ExpressExpressionEmitter.AsLogical(
                        actual,
                        emittedArguments[index]);
                }
                else if (targetType is ExpressBoundScalarType { Kind: ExpressScalarKind.Boolean, }
                         && (actual.Type.Kind == ExpressExpressionTypeKind.Logical
                            || IsLogicalOperatorExpression(actual)))
                {
                    if (IsDeterminateBooleanExpression(actual))
                    {
                        emittedArguments[index] = $"(({emittedArguments[index]}) switch {{ "
                            + "global::TedToolkit.Step21.LogicalValue.True => true, "
                            + "global::TedToolkit.Step21.LogicalValue.False => false, "
                            + "_ => throw new global::System.InvalidOperationException() })";
                    }
                    else
                    {
                        var placeholder = "__expressDynamicBooleanArgument_"
                            + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + index.ToString(CultureInfo.InvariantCulture)
                            + "__";
                        dynamicArguments.Add((
                            emittedArguments[index],
                            placeholder,
                            [
                                ("global::TedToolkit.Step21.LogicalValue.True", "true"),
                                ("global::TedToolkit.Step21.LogicalValue.False", "false"),
                            ],
                            false,
                            false,
                            null,
                            null));
                        emittedArguments[index] = placeholder;
                    }
                }

                ExpressBoundSymbol? projectedEntity = null;
                var directEntityProjection = false;
                if (actual.Reference is { } actualReference
                    && selectNarrowings?.TryGetValue(actualReference, out var directProjection) == true
                    && directProjection.Kind == ExpressDeclarationKind.Entity)
                {
                    projectedEntity = directProjection;
                    directEntityProjection = true;
                }
                else
                {
                    var pathProjections = pathNarrowings
                        ?.Where(narrowing => SameDirectReferencePath(narrowing.Key, actual)
                            && narrowing.Value.Kind == ExpressDeclarationKind.Entity)
                        .Select(narrowing => narrowing.Value)
                        .Distinct()
                        .ToArray();
                    if (pathProjections is { Length: 1, })
                    {
                        projectedEntity = pathProjections[0];
                    }
                }

                ExpressBoundSymbol? inferredAttributeProjection = null;
                if (actual.Kind == ExpressExpressionKind.AttributeQualifier
                    && actual.Type.DeclaredType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } inferredAttributeEntity
                    && actual.Reference?.Type is ExpressBoundNamedType
                    { Declaration.Kind: not ExpressDeclarationKind.Entity, } inferredAttributeCarrier
                    && plan.Resolver.GetDefinedType(inferredAttributeCarrier.Declaration).UnderlyingType
                        is ExpressBoundSelectType)
                {
                    inferredAttributeProjection = inferredAttributeEntity.Declaration;
                }

                var emittedProjection = inferredAttributeProjection ?? projectedEntity;
                var emittedEntityAlreadyProjected = targetType is ExpressBoundNamedType
                { Declaration.Kind: ExpressDeclarationKind.Entity, } projectedFormal
                    && emittedProjection is not null
                    && (inferredAttributeProjection is not null
                        || (directEntityProjection
                            && actual.Type.DeclaredType is ExpressBoundNamedType
                            { Declaration.Kind: ExpressDeclarationKind.Entity, }))
                    && plan.EntityProjections.Single(projection =>
                            projection.Entity.Symbol == emittedProjection)
                        .PhysicalComponents.Any(component =>
                            component.Symbol == projectedFormal.Declaration);
                var actualSelectCarrier = ResolveSelectCarrierType(plan, actual);
                var wasAggregateSelectAdapted = false;
                var wasAggregateValueAdapted = false;

                if (targetType is ExpressBoundScalarType scalarFormalTarget
                    && actual.Type.DeclaredType is ExpressBoundNamedType
                    { Declaration.Kind: not ExpressDeclarationKind.Entity, } scalarSelectCarrier
                    && plan.Resolver.GetDefinedType(scalarSelectCarrier.Declaration).UnderlyingType
                        is ExpressBoundSelectType)
                {
                    var placeholder = "__expressDynamicScalarArgument_"
                        + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture)
                        + "__";
                    var variable = "__expressDynamicScalarValue_"
                        + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture);
                    dynamicArguments.Add((
                        ResolveSelectScalarValue(
                            plan,
                            scalarSelectCarrier,
                            emittedArguments[index],
                            scalarFormalTarget),
                        placeholder,
                        [
                            (ExpressExpressionEmitter.BoundTypeName(scalarFormalTarget)
                                + " " + variable, variable),
                        ],
                        false,
                        actual.Type.DeclaredType is { } staticallySelectedAggregate
                            && plan.Resolver.GetAggregateType(staticallySelectedAggregate) is not null,
                        null,
                        null));
                    emittedArguments[index] = placeholder;
                }

                if (!emittedEntityAlreadyProjected
                    && targetType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } formalEntityTarget
                    && actualSelectCarrier is { } actualSelectName
                    && plan.Resolver.GetDefinedType(actualSelectName.Declaration).UnderlyingType
                        is ExpressBoundSelectType actualSelect)
                {
                    if (HasCompatibleSelectEntity(
                            plan,
                            actualSelectName,
                            formalEntityTarget,
                            new HashSet<ExpressBoundSymbol>()))
                    {
                        var placeholder = "__expressDynamicApplicationArgument_"
                            + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + index.ToString(CultureInfo.InvariantCulture)
                            + "__";
                        dynamicArguments.Add((
                            emittedArguments[index],
                            placeholder,
                            Array.Empty<(string Pattern, string? Value)>(),
                            true,
                            emittedProjection is not null
                                && plan.EntityProjections.Single(projection =>
                                        projection.Entity.Symbol == emittedProjection)
                                    .PhysicalComponents.Any(component =>
                                        component.Symbol == formalEntityTarget.Declaration),
                            actualSelectName,
                            formalEntityTarget));
                        emittedArguments[index] = placeholder;
                    }
                }

                if (actual.Type.DeclaredType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } actualEntity)
                {
                    var actualEntityProjection = plan.EntityProjections.Single(candidate =>
                        candidate.Entity.Symbol == actualEntity.Declaration);
                    var pendingTypes = new Stack<(
                        ExpressBoundType Type,
                        IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
                    pendingTypes.Push((
                        targetType,
                        Array.Empty<(ExpressBoundSymbol, ExpressBoundSymbol)>()));
                    var visitedTypes = new HashSet<ExpressBoundSymbol>();
                    var matchingEntities = new List<(
                        ExpressBoundSymbol Leaf,
                        ExpressEntityProjection Projection,
                        IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
                    var hasStaticallyAssignableAlternative = false;
                    while (pendingTypes.Count > 0)
                    {
                        var pending = pendingTypes.Pop();
                        if (pending.Type is not ExpressBoundNamedType pendingNamed
                            || !visitedTypes.Add(pendingNamed.Declaration))
                        {
                            continue;
                        }

                        if (pendingNamed.Declaration.Kind == ExpressDeclarationKind.Entity)
                        {
                            var projection = plan.EntityProjections.Single(candidate =>
                                candidate.Entity.Symbol == pendingNamed.Declaration);
                            var isExact = ReferenceEquals(
                                pendingNamed.Declaration,
                                actualEntity.Declaration);
                            var actualIsAssignableToFormal = isExact
                                || actualEntityProjection.PhysicalComponents.Any(component =>
                                    component.Symbol == pendingNamed.Declaration);
                            hasStaticallyAssignableAlternative |= actualIsAssignableToFormal;
                            var canShareComplexInstance = projection.PhysicalComponents.Any(
                                formalComponent => actualEntityProjection.PhysicalComponents.Any(
                                    actualComponent => actualComponent.Symbol == formalComponent.Symbol));
                            if (!actualIsAssignableToFormal
                                && (projection.PhysicalComponents.Any(component =>
                                        component.Symbol == actualEntity.Declaration)
                                    || canShareComplexInstance))
                            {
                                matchingEntities.Add((pendingNamed.Declaration, projection, pending.Path));
                            }

                            continue;
                        }

                        var underlying = plan.Resolver.GetDefinedType(pendingNamed.Declaration).UnderlyingType;
                        if (underlying is ExpressBoundSelectType select)
                        {
                            foreach (var alternative in plan.Resolver.GetSelectAlternatives(select))
                            {
                                pendingTypes.Push((
                                    new ExpressBoundNamedType(alternative, select.Span),
                                    pending.Path.Append((pendingNamed.Declaration, alternative)).ToArray()));
                            }

                            continue;
                        }

                        if (underlying is ExpressBoundNamedType)
                        {
                            pendingTypes.Push((underlying, pending.Path));
                        }
                    }

                    var hasOverlappingAlternatives = matchingEntities.SelectMany(
                            (left, leftIndex) => matchingEntities.Skip(leftIndex + 1)
                                .Select(right => (Left: left, Right: right)))
                        .Any(pair => pair.Left.Projection.PhysicalComponents.Any(component =>
                                component.Symbol == pair.Right.Leaf)
                            || pair.Right.Projection.PhysicalComponents.Any(component =>
                                component.Symbol == pair.Left.Leaf));
                    if (matchingEntities.Count > 0
                        && !hasStaticallyAssignableAlternative
                        && !hasOverlappingAlternatives)
                    {
                        var placeholder = "__expressDynamicApplicationArgument_"
                            + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + index.ToString(CultureInfo.InvariantCulture)
                            + "__";
                        var branches = matchingEntities
                            .OrderByDescending(candidate => candidate.Projection.PhysicalComponents.Count)
                            .Select((candidate, branchIndex) =>
                            {
                                var variable = "__expressDynamicApplicationValue_"
                                    + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                                    + "_"
                                    + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                                    + "_"
                                    + index.ToString(CultureInfo.InvariantCulture)
                                    + "_"
                                    + branchIndex.ToString(CultureInfo.InvariantCulture);
                                var value = variable;
                                for (var pathIndex = candidate.Path.Count - 1; pathIndex >= 0; pathIndex--)
                                {
                                    var step = candidate.Path[pathIndex];
                                    value = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
                                            step.Wrapper,
                                            targetType.Span))
                                        + ".From"
                                        + ExpressEntityProjection.ToPascalCase(step.Alternative.Name)
                                        + $"({value})";
                                }

                                var pattern = ExpressExpressionEmitter.BoundTypeName(
                                        new ExpressBoundNamedType(candidate.Leaf, targetType.Span))
                                    + " "
                                    + variable;
                                return (Pattern: pattern, Value: (string?)value);
                            })
                            .ToArray();
                        dynamicArguments.Add((
                            emittedArguments[index],
                            placeholder,
                            branches,
                            false,
                            false,
                            null,
                            null));
                        emittedArguments[index] = placeholder;
                        wasEntityAdapted = true;
                    }
                }

                if (!wasEntityAdapted
                    && targetType is ExpressBoundNamedType entityFormalSelect
                    && actual.Type.DeclaredType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } directEntityArgument
                    && TryResolveSelectCarrier(
                        plan,
                        entityFormalSelect,
                        out var entitySelectCarrier,
                        out var entitySelectWrappers))
                {
                    var selectedEntityArgument = ResolveEntityToSelectValue(
                        plan,
                        entitySelectCarrier,
                        directEntityArgument.Declaration,
                        emittedArguments[index]);
                    var requiresRuntimeSelection = selectedEntityArgument is null;
                    selectedEntityArgument ??= ResolveRuntimeEntityToSelectValue(
                            plan,
                            entitySelectCarrier,
                            directEntityArgument,
                            emittedArguments[index],
                            prefix => prefix
                                + "_"
                                + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + index.ToString(CultureInfo.InvariantCulture));
                    if (selectedEntityArgument is not null)
                    {
                        if (requiresRuntimeSelection)
                        {
                            var presentSelected = "__expressApplicationEntitySelect_"
                                + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + index.ToString(CultureInfo.InvariantCulture);
                            selectedEntityArgument = $"(({selectedEntityArgument}) is {{ }} {presentSelected} ? "
                                + $"{presentSelected} : throw new global::System.InvalidOperationException())";
                        }

                        for (var wrapperIndex = entitySelectWrappers.Count - 1;
                             wrapperIndex >= 0;
                             wrapperIndex--)
                        {
                            selectedEntityArgument = "new "
                                + ExpressExpressionEmitter.BoundTypeName(entitySelectWrappers[wrapperIndex])
                                + $"({selectedEntityArgument})";
                        }

                        emittedArguments[index] = selectedEntityArgument;
                        wasEntityAdapted = true;
                    }
                }

                if (plan.Resolver.GetAggregateType(targetType) is { } selectedFormalAggregate
                    && actualSelectCarrier is { } selectedAggregateCarrier
                    && ResolveAggregateSource(
                        plan,
                        selectedAggregateCarrier,
                        emittedArguments[index],
                        selectedFormalAggregate,
                        allowIncompatible: true) is { } projectedAggregateArgument)
                {
                    var placeholder = "__expressDynamicSelectedAggregateArgument_"
                        + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture)
                        + "__";
                    var variable = "__expressDynamicSelectedAggregateValue_"
                        + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture);
                    var selectedValue = variable;
                    var selectedTarget = targetType;
                    var selectedWrappers = ResolveTransparentDefinedWrappers(
                        plan.Resolver,
                        ref selectedTarget);
                    for (var wrapperIndex = selectedWrappers.Count - 1;
                         wrapperIndex >= 0;
                         wrapperIndex--)
                    {
                        selectedValue = "new "
                            + ExpressExpressionEmitter.BoundTypeName(selectedWrappers[wrapperIndex])
                            + $"({selectedValue})";
                    }

                    dynamicArguments.Add((
                        projectedAggregateArgument,
                        placeholder,
                        [
                            (ExpressExpressionEmitter.AggregateInterfaceTypeName(selectedFormalAggregate)
                                + " " + variable, selectedValue),
                        ],
                        false,
                        (actual.Type.DeclaredType is { } staticallySelectedAggregate
                            && plan.Resolver.GetAggregateType(staticallySelectedAggregate) is not null)
                            || IsSelectedAggregateExhaustive(
                                plan,
                                selectedAggregateCarrier,
                                selectedFormalAggregate),
                        null,
                        null));
                    emittedArguments[index] = placeholder;
                    wasAggregateSelectAdapted = true;
                }

                if (!wasAggregateSelectAdapted
                    && targetType is ExpressBoundAggregateType { Kind: ExpressAggregateKind.Aggregate, }
                    && ResolveSelectCarrierType(plan, actual) is { } actualAggregateSelectName
                    && actualAggregateSelectName.Declaration.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(actualAggregateSelectName.Declaration).UnderlyingType
                        is ExpressBoundSelectType actualAggregateSelect)
                {
                    var placeholder = "__expressDynamicAggregateArgument_"
                        + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture)
                        + "__";
                    var branches = plan.Resolver.GetSelectAlternatives(actualAggregateSelect)
                        .Select((alternative, branchIndex) =>
                        {
                            var variable = "__expressDynamicAggregateValue_"
                                + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + index.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + branchIndex.ToString(CultureInfo.InvariantCulture);
                            var value = variable;
                            ExpressBoundType pending = new ExpressBoundNamedType(
                                alternative,
                                actualAggregateSelect.Span);
                            var visited = new HashSet<ExpressBoundSymbol>();
                            while (pending is ExpressBoundNamedType pendingNamed
                                   && pendingNamed.Declaration.Kind != ExpressDeclarationKind.Entity
                                   && visited.Add(pendingNamed.Declaration))
                            {
                                pending = plan.Resolver.GetDefinedType(pendingNamed.Declaration).UnderlyingType;
                                value = $"({value}).Value";
                            }

                            return (
                                Pattern: variable,
                                Value: pending is ExpressBoundAggregateType ? value : null);
                        })
                        .ToArray();
                    if (branches.Any(branch => branch.Value is not null))
                    {
                        dynamicArguments.Add((
                            emittedArguments[index],
                            placeholder,
                            branches,
                            true,
                            false,
                            null,
                            null));
                        emittedArguments[index] = placeholder;
                    }
                }

                var wasSelectToSelectAdapted = false;
                if (targetType is ExpressBoundNamedType projectedFormalSelect
                    && projectedFormalSelect.Declaration.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(projectedFormalSelect.Declaration).UnderlyingType
                        is ExpressBoundSelectType
                    && actualSelectCarrier is { } projectedActualSelect
                    && !ReferenceEquals(
                        projectedFormalSelect.Declaration,
                        projectedActualSelect.Declaration)
                    && ResolveSelectToSelectValue(
                        plan,
                        projectedFormalSelect,
                        projectedActualSelect,
                        emittedArguments[index],
                        "__expressApplicationSelect") is { } projectedSelectArgument)
                {
                    emittedArguments[index] = projectedSelectArgument;
                    wasSelectToSelectAdapted = true;
                }

                if (!wasSelectToSelectAdapted
                    && targetType is ExpressBoundNamedType formalSelectName
                    && formalSelectName.Declaration.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(formalSelectName.Declaration).UnderlyingType
                        is ExpressBoundSelectType formalSelectType
                    && actual.Type.DeclaredType is ExpressBoundNamedType actualApplicationSelectName
                    && actualApplicationSelectName.Declaration.Kind != ExpressDeclarationKind.Entity
                    && !ReferenceEquals(
                        formalSelectName.Declaration,
                        actualApplicationSelectName.Declaration)
                    && plan.Resolver.GetDefinedType(actualApplicationSelectName.Declaration).UnderlyingType
                        is ExpressBoundSelectType actualSelectType)
                {
                    var pendingFormalSelects = new Stack<(
                        ExpressBoundSymbol Wrapper,
                        ExpressBoundSelectType Select,
                        IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
                    pendingFormalSelects.Push((
                        formalSelectName.Declaration,
                        formalSelectType,
                        Array.Empty<(ExpressBoundSymbol, ExpressBoundSymbol)>()));
                    var visitedFormalSelects = new HashSet<ExpressBoundSymbol>();
                    var formalEntityPaths = new List<(
                        ExpressBoundSymbol Leaf,
                        IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
                    while (pendingFormalSelects.Count > 0)
                    {
                        var pending = pendingFormalSelects.Pop();
                        if (!visitedFormalSelects.Add(pending.Wrapper))
                        {
                            continue;
                        }

                        foreach (var alternative in plan.Resolver.GetSelectAlternatives(pending.Select))
                        {
                            var path = pending.Path
                                .Append((pending.Wrapper, alternative))
                                .ToArray();
                            if (alternative.Kind == ExpressDeclarationKind.Entity)
                            {
                                formalEntityPaths.Add((alternative, path));
                            }
                            else if (plan.Resolver.GetDefinedType(alternative).UnderlyingType
                                     is ExpressBoundSelectType nestedSelect)
                            {
                                pendingFormalSelects.Push((alternative, nestedSelect, path));
                            }
                        }
                    }

                    var placeholder = "__expressDynamicSelectApplicationArgument_"
                        + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture)
                        + "__";
                    var branches = plan.Resolver.GetSelectAlternatives(actualSelectType)
                        .Select((alternative, branchIndex) =>
                        {
                            var variable = "__expressDynamicSelectApplicationValue_"
                                + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + index.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + branchIndex.ToString(CultureInfo.InvariantCulture);
                            if (alternative.Kind != ExpressDeclarationKind.Entity)
                            {
                                return (Pattern: variable, Value: (string?)null);
                            }

                            var projection = plan.EntityProjections.Single(candidate =>
                                candidate.Entity.Symbol == alternative);
                            var compatiblePaths = formalEntityPaths
                                .Where(candidate => projection.PhysicalComponents.Any(component =>
                                    component.Symbol == candidate.Leaf))
                                .ToArray();
                            if (compatiblePaths.Length != 1)
                            {
                                return (Pattern: variable, Value: (string?)null);
                            }

                            var value = variable;
                            var path = compatiblePaths[0].Path;
                            for (var pathIndex = path.Count - 1; pathIndex >= 0; pathIndex--)
                            {
                                var step = path[pathIndex];
                                value = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
                                        step.Wrapper,
                                        formalSelectName.Span))
                                    + ".From"
                                    + ExpressEntityProjection.ToPascalCase(step.Alternative.Name)
                                    + $"({value})";
                            }

                            return (Pattern: variable, Value: (string?)value);
                        })
                        .ToArray();
                    if (branches.Any(branch => branch.Value is not null))
                    {
                        dynamicArguments.Add((
                            emittedArguments[index],
                            placeholder,
                            branches,
                            true,
                            false,
                            null,
                            null));
                        emittedArguments[index] = placeholder;
                    }
                }

                if (targetType is ExpressBoundNamedType
                    { Declaration.Kind: not ExpressDeclarationKind.Entity, } scalarFormalSelect
                    && plan.Resolver.GetDefinedType(scalarFormalSelect.Declaration).UnderlyingType
                        is ExpressBoundSelectType
                    && actualSelectCarrier is null
                    && actual.Type.DeclaredType is ExpressBoundNamedType
                    { Declaration.Kind: not ExpressDeclarationKind.Entity, } namedActual
                    && plan.Resolver.GetDefinedType(namedActual.Declaration).UnderlyingType
                        is not ExpressBoundSelectType
                    && ResolveNamedExpressionToSelectValue(
                        plan,
                        scalarFormalSelect,
                        namedActual,
                        emittedArguments[index],
                        CanEmitIndeterminate(plan, actual),
                        "__expressApplicationSelectValue_"
                            + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + index.ToString(CultureInfo.InvariantCulture)) is { } selectedArgument)
                {
                    emittedArguments[index] = selectedArgument;
                }

                if (targetType is ExpressBoundNamedType
                    { Declaration.Kind: not ExpressDeclarationKind.Entity, } primitiveFormalSelect
                    && plan.Resolver.GetDefinedType(primitiveFormalSelect.Declaration).UnderlyingType
                        is ExpressBoundSelectType
                    && actual.Type.DeclaredType is null or ExpressBoundScalarType
                    && ResolveScalarExpressionToSelectValue(
                        plan,
                        primitiveFormalSelect,
                        actual,
                        emittedArguments[index],
                        CanEmitIndeterminate(plan, actual),
                        "__expressApplicationScalarValue_"
                            + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + index.ToString(CultureInfo.InvariantCulture)) is { } selectedScalarArgument)
                {
                    emittedArguments[index] = selectedScalarArgument;
                }

                if (targetType is ExpressBoundNamedType
                    { Declaration.Kind: not ExpressDeclarationKind.Entity, } aggregateFormalSelect
                    && actualSelectCarrier is null
                    && TryResolveSelectCarrier(
                        plan,
                        aggregateFormalSelect,
                        out var aggregateSelectCarrier,
                        out var aggregateSelectWrappers)
                    && ResolveExpressionAggregateType(
                        plan,
                        actual,
                        new ExpressBoundAggregateType(
                            ExpressAggregateKind.Aggregate,
                            new ExpressBoundGenericType(
                                isEntity: false,
                                typeLabel: null,
                                span: actual.Span),
                            lowerBoundText: null,
                            upperBoundText: null,
                            isOptional: false,
                            isUnique: false,
                            typeLabel: null,
                            actual.Span)) is { } aggregateSelectSource
                    && ResolveAggregateTypeToSelectValue(
                        plan,
                        aggregateSelectCarrier,
                        aggregateSelectSource,
                        emittedArguments[index],
                        "__expressApplicationAggregateSelect_"
                            + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + index.ToString(CultureInfo.InvariantCulture)) is { } aggregateSelectedArgument)
                {
                    for (var wrapperIndex = aggregateSelectWrappers.Count - 1;
                         wrapperIndex >= 0;
                         wrapperIndex--)
                    {
                        aggregateSelectedArgument = "new "
                            + ExpressExpressionEmitter.BoundTypeName(aggregateSelectWrappers[wrapperIndex])
                            + $"({aggregateSelectedArgument})";
                    }

                    emittedArguments[index] = aggregateSelectedArgument;
                }

                if (plan.Resolver.GetAggregateType(targetType) is { } projectedAggregateTarget
                    && actual.Kind != ExpressExpressionKind.AggregateInitializer
                    && ResolveExpressionAggregateCandidates(plan, actual) is { } projectedAggregateCandidates
                    && projectedAggregateCandidates.FirstOrDefault(candidate =>
                            candidate.Kind == projectedAggregateTarget.Kind
                            && !ExpressGeneratedTypeResolver.AreEquivalent(
                                candidate.ElementType,
                                projectedAggregateTarget.ElementType))
                        is { } projectedAggregateSource
                    && ResolveAggregateValueToTarget(
                        plan,
                        projectedAggregateSource,
                        projectedAggregateTarget,
                        emittedArguments[index],
                        "__expressApplicationAggregate_"
                            + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + index.ToString(CultureInfo.InvariantCulture),
                        sourceCanBeIndeterminate: false,
                        materializeEquivalent: false,
                        allowRuntimeEntityNarrowing: projectedAggregateCandidates
                            .Any(candidate => candidate.Kind == projectedAggregateTarget.Kind
                                && ExpressGeneratedTypeResolver.AreEquivalent(
                                    candidate.ElementType,
                                    projectedAggregateTarget.ElementType))
                            || HasSelfAggregateRedeclarationProof(
                                plan,
                                actual,
                                projectedAggregateTarget,
                                selfEntity)) is { } projectedAggregateValue)
                {
                    emittedArguments[index] = projectedAggregateValue;
                    wasAggregateValueAdapted = true;
                }

                if (!wasAggregateValueAdapted
                    && targetType is ExpressBoundAggregateType targetAggregate
                    && (targetAggregate.Kind is ExpressAggregateKind.Bag
                            or ExpressAggregateKind.List
                        || (targetAggregate.Kind == ExpressAggregateKind.Set
                            && string.Equals(
                                targetAggregate.ResolvedLowerBoundText ?? targetAggregate.LowerBoundText,
                                "0",
                                StringComparison.Ordinal)))
                    && actual.Kind == ExpressExpressionKind.AggregateInitializer
                    && actual.Type.DeclaredType is { } actualDeclaredType
                    && plan.Resolver.GetAggregateType(actualDeclaredType) is { } actualAggregate)
                {
                    if (actual.Children.Count == 0)
                    {
                        emittedArguments[index] = $"({ExpressExpressionEmitter.BoundTypeName(targetAggregate)})[]";
                    }
                    else if (actualAggregate.Kind != targetAggregate.Kind
                        && targetAggregate.Kind != ExpressAggregateKind.Set
                        && ExpressGeneratedTypeResolver.AreEquivalent(
                        actualAggregate.ElementType,
                        targetAggregate.ElementType))
                    {
                        emittedArguments[index] = $"({ExpressExpressionEmitter.BoundTypeName(targetAggregate)})"
                            + $"[..({emittedArguments[index]})]";
                    }
                    else if (targetAggregate.ElementType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } targetElementEntity
                        && actual.Children.All(child =>
                            child.Type.DeclaredType is ExpressBoundNamedType
                            { Declaration.Kind: ExpressDeclarationKind.Entity, } childEntity
                            && plan.EntityProjections.Single(projection =>
                                    projection.Entity.Symbol == childEntity.Declaration)
                                .PhysicalComponents.Any(component =>
                                    component.Symbol == targetElementEntity.Declaration)))
                    {
                        var actualPrefix = "("
                            + ExpressExpressionEmitter.BoundTypeName(actualAggregate)
                            + ")[";
                        if (emittedArguments[index].StartsWith(actualPrefix, StringComparison.Ordinal))
                        {
                            emittedArguments[index] = "("
                                + ExpressExpressionEmitter.BoundTypeName(targetAggregate)
                                + ")["
                                + emittedArguments[index].Substring(actualPrefix.Length);
                        }
                    }
                }

                if (!wasAggregateValueAdapted
                    && targetType is ExpressBoundAggregateType projectedTargetAggregate
                    && ResolveExpressionAggregateType(plan, actual, projectedTargetAggregate)
                        is { } projectedActualAggregate
                    && projectedTargetAggregate.ElementType is ExpressBoundNamedType
                    { Declaration.Kind: not ExpressDeclarationKind.Entity, } projectedTargetElement
                    && plan.Resolver.GetDefinedType(projectedTargetElement.Declaration).UnderlyingType
                        is ExpressBoundSelectType
                    && projectedActualAggregate.ElementType is ExpressBoundNamedType projectedSourceElement)
                {
                    var projectedElement = "__expressApplicationElement_"
                        + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture);
                    var selectedElement = projectedSourceElement.Declaration.Kind
                            != ExpressDeclarationKind.Entity
                        && plan.Resolver.GetDefinedType(projectedSourceElement.Declaration).UnderlyingType
                            is ExpressBoundSelectType
                            ? ResolveSelectToSelectValue(
                                plan,
                                projectedTargetElement,
                                projectedSourceElement,
                                projectedElement,
                                projectedElement)
                            : ResolveNamedExpressionToSelectValue(
                                plan,
                                projectedTargetElement,
                                projectedSourceElement,
                                projectedElement,
                                canBeIndeterminate: false,
                                projectedElement,
                                sourceIsNominal: true);
                    if (selectedElement is not null)
                    {
                        var projectedValue = projectedElement + "Value";
                        emittedArguments[index] = "("
                            + ExpressExpressionEmitter.BoundTypeName(projectedTargetAggregate)
                            + ")[..global::System.Linq.Enumerable.Select("
                            + emittedArguments[index]
                            + $", {projectedElement} => ({selectedElement}) is {{ }} {projectedValue} "
                            + $"? {projectedValue} : throw new global::System.InvalidOperationException())]";
                    }
                }

                if (!wasAggregateValueAdapted
                    && targetType is ExpressBoundAggregateType projectedEntityTargetAggregate
                    && ResolveExpressionAggregateType(plan, actual, projectedEntityTargetAggregate)
                        is { } projectedEntityActualAggregate
                    && projectedEntityTargetAggregate.ElementType is ExpressBoundNamedType
                    { Declaration.Kind: ExpressDeclarationKind.Entity, } projectedTargetEntity
                    && projectedEntityActualAggregate.ElementType is ExpressBoundNamedType
                    { Declaration.Kind: not ExpressDeclarationKind.Entity, } projectedSourceSelect
                    && plan.Resolver.GetDefinedType(projectedSourceSelect.Declaration).UnderlyingType
                        is ExpressBoundSelectType)
                {
                    var projectedElement = "__expressApplicationEntityElement_"
                        + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture);
                    if (ResolveSelectToEntityValue(
                        plan,
                        projectedTargetEntity,
                        projectedSourceSelect,
                        projectedElement,
                        projectedElement) is { } selectedEntityElement)
                    {
                        var projectedValue = projectedElement + "Value";
                        emittedArguments[index] = "("
                            + ExpressExpressionEmitter.BoundTypeName(projectedEntityTargetAggregate)
                            + ")[..global::System.Linq.Enumerable.Select("
                            + emittedArguments[index]
                            + $", {projectedElement} => ({selectedEntityElement}) is {{ }} {projectedValue} "
                            + $"? {projectedValue} : throw new global::System.InvalidOperationException())]";
                    }
                }

                var definedTypes = ResolveTransparentDefinedWrappers(plan.Resolver, ref targetType);

                var actualNominal = actual.Type.DeclaredType as ExpressBoundNamedType;
                var primitiveActual = actualNominal is null;
                var sameDefinedActual = definedTypes.Count > 0
                    && actualNominal is not null
                    && (definedTypes.Any(candidate =>
                            ReferenceEquals(actualNominal.Declaration, candidate.Declaration))
                        || IsTransparentAliasOf(
                            plan,
                            actualNominal,
                            definedTypes[0].Declaration));
                var semanticDefinedActual = actualNominal is not null
                    && ResolveDefinedValueType(plan, formalTypes[index])
                        is ExpressBoundNamedType semanticTarget
                    && ReferenceEquals(actualNominal.Declaration, semanticTarget.Declaration);
                var actualScalarKind = ResolveExpressionScalarKind(plan, actual);
                var compatibleScalar = targetType is ExpressBoundScalarType targetScalar
                    && actualScalarKind is { } sourceScalar
                    && CanProjectScalar(sourceScalar, targetScalar.Kind);
                if (compatibleScalar
                    && actual.Kind == ExpressExpressionKind.Reference
                    && actual.Reference is { Kind: ExpressBoundNameKind.Variable, } actualVariable
                    && plan.Schema.IndeterminateLocals.Contains(actualVariable))
                {
                    var placeholder = "__expressDynamicRequiredApplicationArgument_"
                        + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture)
                        + "__";
                    var variable = "__expressDynamicRequiredApplicationValue_"
                        + actual.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + actual.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture);
                    dynamicArguments.Add((
                        emittedArguments[index],
                        placeholder,
                        [(ExpressExpressionEmitter.BoundTypeName(new ExpressBoundScalarType(
                            actualScalarKind!.Value,
                            constraintText: null,
                            isFixed: false,
                            actual.Span)) + " " + variable, variable),],
                        false,
                        false,
                        null,
                        null));
                    emittedArguments[index] = placeholder;
                }

                var exactAggregate = targetType is ExpressBoundAggregateType
                    && actual.Type.Kind == ExpressExpressionTypeKind.Aggregate;
                var exactSelect = targetType is ExpressBoundSelectType
                    && actual.Type.Kind == ExpressExpressionTypeKind.Select;
                if ((primitiveActual || sameDefinedActual || semanticDefinedActual)
                    && (compatibleScalar || exactAggregate || exactSelect))
                {
                    if (compatibleScalar
                        && actualScalarKind!.Value != ((ExpressBoundScalarType)targetType).Kind)
                    {
                        emittedArguments[index] = PromoteScalarValue(
                            emittedArguments[index],
                            actualScalarKind.Value,
                            ((ExpressBoundScalarType)targetType).Kind);
                    }

                    for (var definedIndex = definedTypes.Count - 1; definedIndex >= 0; definedIndex--)
                    {
                        emittedArguments[index] = "new "
                            + ExpressExpressionEmitter.BoundTypeName(definedTypes[definedIndex])
                            + $"({emittedArguments[index]})";
                    }
                }

                if (wasEntityAdapted || declaration is not ExpressBoundOpaqueDeclaration)
                {
                    continue;
                }

                if (formalTypes[index] is not ExpressBoundNamedType formalNamed
                    || formalNamed.Declaration.Kind == ExpressDeclarationKind.Entity
                    || plan.Resolver.GetDefinedType(formalNamed.Declaration).UnderlyingType
                        is not ExpressBoundSelectType formalSelect
                    || expression.Children[index].Type.DeclaredType
                        is not ExpressBoundNamedType actualNamed
                    || actualNamed.Declaration.Kind != ExpressDeclarationKind.Entity)
                {
                    continue;
                }

                var actualProjection = plan.EntityProjections.Single(projection =>
                    projection.Entity.Symbol == actualNamed.Declaration);
                var pendingSelectTypes = new Stack<(
                    ExpressBoundSymbol Wrapper,
                    ExpressBoundSelectType Select,
                    IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
                pendingSelectTypes.Push((
                    formalNamed.Declaration,
                    formalSelect,
                    Array.Empty<(ExpressBoundSymbol, ExpressBoundSymbol)>()));
                var visitedSelectTypes = new HashSet<ExpressBoundSymbol>();
                var alternatives = new List<(
                    ExpressBoundSymbol Leaf,
                    IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
                while (pendingSelectTypes.Count > 0)
                {
                    var pending = pendingSelectTypes.Pop();
                    if (!visitedSelectTypes.Add(pending.Wrapper))
                    {
                        continue;
                    }

                    foreach (var alternative in plan.Resolver.GetSelectAlternatives(pending.Select))
                    {
                        var path = pending.Path
                            .Append((pending.Wrapper, alternative))
                            .ToArray();
                        if (alternative.Kind == ExpressDeclarationKind.Entity)
                        {
                            if (actualProjection.PhysicalComponents.Any(component =>
                                    component.Symbol == alternative))
                            {
                                alternatives.Add((alternative, path));
                            }

                            continue;
                        }

                        if (plan.Resolver.GetDefinedType(alternative).UnderlyingType
                            is ExpressBoundSelectType nestedSelect)
                        {
                            pendingSelectTypes.Push((alternative, nestedSelect, path));
                        }
                    }
                }

                if (alternatives.Count == 1)
                {
                    var adapted = emittedArguments[index];
                    var path = alternatives[0].Path;
                    for (var pathIndex = path.Count - 1; pathIndex >= 0; pathIndex--)
                    {
                        var step = path[pathIndex];
                        adapted = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
                                step.Wrapper,
                                formalNamed.Span))
                            + ".From"
                            + ExpressEntityProjection.ToPascalCase(step.Alternative.Name)
                            + $"({adapted})";
                    }

                    emittedArguments[index] = adapted;
                }
            }
        }

        string result;
        if (invokedFunction is null && invokedEntityAttributes is not null)
        {
            var requiredArguments = invokedEntityAttributes
                .Select((attribute, index) => (Attribute: attribute, Value: emittedArguments[index]))
                .Where(item => !item.Attribute.Attribute.IsOptional)
                .Select(item => item.Value);
            var initializers = invokedEntityAttributes
                .Select((attribute, index) => (Attribute: attribute, Value: emittedArguments[index]))
                .Where(item => item.Attribute.Attribute.IsOptional)
                .Select(item => item.Attribute.StorageMemberName + " = " + item.Value)
                .ToArray();
            result = $"{invocation}({string.Join(", ", requiredArguments)})"
                + (initializers.Length == 0 ? "" : $" {{ {string.Join(", ", initializers)} }}");
        }
        else
        {
            result = $"{FunctionMethodName(plan, invokedFunction!)}({string.Join(", ", emittedArguments.Append(
                populationExpression + ValidationContextArgumentSuffix(plan)))})";
        }

        var nullResult = dynamicArguments.Count == 0
            ? ""
            : "(" + ExpressExpressionEmitter.BoundTypeName(expression.Type.DeclaredType!) + "?)null";
        for (var index = dynamicArguments.Count - 1; index >= 0; index--)
        {
            var dynamicArgument = dynamicArguments[index];
            if (dynamicArgument.SelectEntityCarrier is { } selectCarrier
                && dynamicArgument.SelectEntityTarget is { } selectTarget)
            {
                var hasIncompatibleLeaf = HasIncompatibleSelectEntity(
                    plan,
                    selectCarrier,
                    selectTarget,
                    new HashSet<ExpressBoundSymbol>())
                    && !dynamicArgument.HasStaticProof;
                var resultType = ExpressExpressionEmitter.BoundTypeName(expression.Type.DeclaredType!)
                    + (expression.Type.CanBeIndeterminate || hasIncompatibleLeaf ? "?" : "");
                var failureResult = dynamicArgument.HasStaticProof
                    ? "throw new global::System.InvalidOperationException()"
                    : nullResult;
                var variableIndex = 0;
                result = CreateSelectEntityApplicationDispatch(
                    plan,
                    selectCarrier,
                    selectTarget,
                    dynamicArgument.Carrier,
                    result,
                    dynamicArgument.Placeholder,
                    resultType,
                    failureResult,
                    ref variableIndex,
                    new HashSet<ExpressBoundSymbol>());
                continue;
            }

            var branches = dynamicArgument.Branches.Select(branch =>
            {
                string branchResult;
                if (branch.Value is not null)
                {
                    branchResult = result.Replace(dynamicArgument.Placeholder, branch.Value);
                }
                else
                {
                    branchResult = dynamicArgument.HasStaticProof
                        ? "throw new global::System.InvalidOperationException()"
                        : nullResult;
                }

                return branch.Pattern + " => " + branchResult;
            });
            result = dynamicArgument.UsesSelectMatch
                ? $"({dynamicArgument.Carrier}).Match<"
                    + ExpressExpressionEmitter.BoundTypeName(expression.Type.DeclaredType!)
                    + (expression.Type.CanBeIndeterminate ? "?" : "")
                    + $">({string.Join(", ", branches)})"
                : $"((object)({dynamicArgument.Carrier})) switch {{ {string.Join(", ", branches)}, "
                    + $"_ => {nullResult} }}";
        }

        return result;
    }

    private static bool HasCompatibleSelectEntity(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType carrier,
        ExpressBoundNamedType target,
        HashSet<ExpressBoundSymbol> visited)
    {
        if (!visited.Add(carrier.Declaration)
            || plan.Resolver.GetDefinedType(carrier.Declaration).UnderlyingType
                is not ExpressBoundSelectType select)
        {
            return false;
        }

        return plan.Resolver.GetSelectAlternatives(select).Any(alternative =>
            IsCompatibleEntity(plan, alternative, target)
            || CanRuntimeProjectEntity(plan, alternative, target)
            || (alternative.Kind != ExpressDeclarationKind.Entity
                && HasCompatibleSelectEntity(
                    plan,
                    new ExpressBoundNamedType(alternative, carrier.Span),
                    target,
                    new HashSet<ExpressBoundSymbol>(visited))));
    }

    private static bool HasIncompatibleSelectEntity(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType carrier,
        ExpressBoundNamedType target,
        HashSet<ExpressBoundSymbol> visited)
    {
        if (!visited.Add(carrier.Declaration)
            || plan.Resolver.GetDefinedType(carrier.Declaration).UnderlyingType
                is not ExpressBoundSelectType select)
        {
            return true;
        }

        return plan.Resolver.GetSelectAlternatives(select).Any(alternative =>
            alternative.Kind == ExpressDeclarationKind.Entity
                ? !IsCompatibleEntity(plan, alternative, target)
                : HasIncompatibleSelectEntity(
                    plan,
                    new ExpressBoundNamedType(alternative, carrier.Span),
                    target,
                    new HashSet<ExpressBoundSymbol>(visited)));
    }

    private static string CreateSelectEntityApplicationDispatch(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType carrier,
        ExpressBoundNamedType target,
        string carrierCode,
        string resultTemplate,
        string placeholder,
        string resultType,
        string nullResult,
        ref int variableIndex,
        HashSet<ExpressBoundSymbol> visited)
    {
        if (!visited.Add(carrier.Declaration)
            || plan.Resolver.GetDefinedType(carrier.Declaration).UnderlyingType
                is not ExpressBoundSelectType select)
        {
            return nullResult;
        }

        var branches = new List<string>();
        foreach (var alternative in plan.Resolver.GetSelectAlternatives(select))
        {
            var variable = "__expressDynamicEntityValue_"
                + (variableIndex++).ToString(CultureInfo.InvariantCulture);
            string branchResult;
            if (IsCompatibleEntity(plan, alternative, target))
            {
                branchResult = resultTemplate.Replace(placeholder, variable);
            }
            else if (CanRuntimeProjectEntity(plan, alternative, target))
            {
                var targetName = ExpressExpressionEmitter.BoundTypeName(target);
                var typed = "__expressDynamicEntitySubtype_"
                    + (variableIndex++).ToString(CultureInfo.InvariantCulture);
                branchResult = $"((object)({variable})) switch {{ {targetName} {typed} => "
                    + resultTemplate.Replace(placeholder, typed)
                    + $", _ => {nullResult} }}";
            }
            else if (alternative.Kind != ExpressDeclarationKind.Entity)
            {
                branchResult = CreateSelectEntityApplicationDispatch(
                    plan,
                    new ExpressBoundNamedType(alternative, carrier.Span),
                    target,
                    variable,
                    resultTemplate,
                    placeholder,
                    resultType,
                    nullResult,
                    ref variableIndex,
                    new HashSet<ExpressBoundSymbol>(visited));
            }
            else
            {
                branchResult = nullResult;
            }

            branches.Add(variable + " => " + branchResult);
        }

        return $"({carrierCode}).Match<{resultType}>({string.Join(", ", branches)})";
    }

    private static bool IsCompatibleEntity(
        ExpressReachableRulePlan plan,
        ExpressBoundSymbol alternative,
        ExpressBoundNamedType target)
    {
        return alternative.Kind == ExpressDeclarationKind.Entity
            && plan.EntityProjections.Single(projection => projection.Entity.Symbol == alternative)
                .PhysicalComponents.Any(component => component.Symbol == target.Declaration);
    }

    private static bool CanRuntimeProjectEntity(
        ExpressReachableRulePlan plan,
        ExpressBoundSymbol alternative,
        ExpressBoundNamedType target)
    {
        return alternative.Kind == ExpressDeclarationKind.Entity
            && plan.EntityProjections.Single(projection =>
                    projection.Entity.Symbol == target.Declaration)
                .PhysicalComponents.Any(component => component.Symbol == alternative);
    }

    private static bool SameDirectReferencePath(
        ExpressBoundExpression left,
        ExpressBoundExpression right)
    {
        if (left.Kind != right.Kind
            || !string.Equals(left.Operation, right.Operation, StringComparison.OrdinalIgnoreCase)
            || left.Children.Count != right.Children.Count)
        {
            return false;
        }

        if (left.Reference?.Attribute is not null || right.Reference?.Attribute is not null)
        {
            if (!ReferenceEquals(left.Reference?.Attribute, right.Reference?.Attribute))
            {
                return false;
            }
        }
        else if (left.Reference?.SchemaDeclaration is not null
                 || right.Reference?.SchemaDeclaration is not null)
        {
            if (!ReferenceEquals(
                left.Reference?.SchemaDeclaration,
                right.Reference?.SchemaDeclaration))
            {
                return false;
            }
        }
        else if (!ReferenceEquals(left.Reference, right.Reference))
        {
            return false;
        }

        if (left.Kind is ExpressExpressionKind.Literal or ExpressExpressionKind.Indeterminate
            && !string.Equals(left.SourceText, right.SourceText, StringComparison.Ordinal))
        {
            return false;
        }

        return left.Children.Zip(right.Children, SameDirectReferencePath).All(matches => matches);
    }

    private static void IntersectDictionaryFacts<TKey, TValue>(
        IDictionary<TKey, TValue>? target,
        Func<TValue, TValue, bool> sameValue,
        params IReadOnlyDictionary<TKey, TValue>[] branches)
        where TKey : notnull
    {
        if (target is null || branches.Length == 0)
        {
            return;
        }

        target.Clear();
        foreach (var pair in branches[0])
        {
            if (branches.Skip(1).All(branch => branch.TryGetValue(pair.Key, out var candidate)
                    && sameValue(candidate, pair.Value)))
            {
                target.Add(pair.Key, pair.Value);
            }
        }
    }

    private static void IntersectCollectionFacts<T>(
        ICollection<T>? target,
        Func<T, T, bool> sameItem,
        params IReadOnlyCollection<T>[] branches)
    {
        if (target is null || branches.Length == 0)
        {
            return;
        }

        target.Clear();
        foreach (var item in branches[0])
        {
            if (branches.Skip(1).All(branch => branch.Any(candidate => sameItem(candidate, item))))
            {
                target.Add(item);
            }
        }
    }

    private static void JoinSelectFacts<TKey>(
        ICollection<KeyValuePair<TKey, ExpressBoundSymbol>>? target,
        Func<TKey, TKey, bool> sameKey,
        Func<TKey, ExpressBoundSymbol?> resolveDeclaredSelect,
        params IReadOnlyCollection<KeyValuePair<TKey, ExpressBoundSymbol>>[] branches)
    {
        if (target is null || branches.Length == 0)
        {
            return;
        }

        target.Clear();
        foreach (var pair in branches[0])
        {
            if (branches.Skip(1).Any(branch => !branch.Any(candidate =>
                    sameKey(candidate.Key, pair.Key))))
            {
                continue;
            }

            var values = branches.Select(branch => branch.First(candidate =>
                    sameKey(candidate.Key, pair.Key)).Value)
                .ToArray();
            if (values.All(value => value.Kind == ExpressDeclarationKind.Entity)
                && values.All(value => ReferenceEquals(value, values[0])))
            {
                target.Add(new(pair.Key, values[0]));
                continue;
            }

            var markers = values
                .Where(value => value.Kind != ExpressDeclarationKind.Entity)
                .ToArray();
            if (markers.Length > 0
                && markers.All(marker => ReferenceEquals(marker, markers[0]))
                && ReferenceEquals(resolveDeclaredSelect(pair.Key), markers[0]))
            {
                target.Add(new(pair.Key, markers[0]));
            }
        }
    }

    private static Method CreateMethod(string name, DataType returnType)
    {
        var method = SourceComposer<ExpressIncrementalGenerator>.Method(
            name,
            SourceComposer.ReturnType(returnType));
        method.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
        method.IsStatic = true;
        return method;
    }

    private static List<Method> CreateEntityValueEqualsMethods(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        IReadOnlyList<ExpressEntityProjection> entities,
        IReadOnlyList<ExpressComplexEntityProjection> complexEntities,
        ExpressDescriptorShards shards)
    {
        const string logicalTypeName = "global::TedToolkit.Step21.LogicalValue";
        var logicalType = new DataType(logicalTypeName);
        var method = CreateMethod("__ExpressEntityValueEquals", logicalType);
        AddEntityValueEqualsParameters(method);
        method.AddStatement(new IfStatement(new CustomExpression(
            "global::System.Linq.Enumerable.Any(activePairs, pair => "
            + "global::System.Object.ReferenceEquals(pair.Key, left) "
            + "&& global::System.Object.ReferenceEquals(pair.Value, right))"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.True").Return));

        var methods = new List<Method>() { method, };
        var selectValueEqualityHelpers = new SelectValueEqualityHelpers(plan, resolver, shards, methods);
        var branches = new List<IfStatement>();
        foreach (var entity in entities.Where(entity => !entity.Entity.IsAbstract))
        {
            branches.Add(CreateEntityValueBranch(
                plan,
                resolver,
                entity.Name,
                entity.EffectiveAttributes,
                selectValueEqualityHelpers));
        }

        foreach (var complex in complexEntities)
        {
            branches.Add(CreateEntityValueBranch(
                plan,
                resolver,
                complex.Name,
                complex.Properties,
                selectValueEqualityHelpers));
        }

        for (var offset = 0; offset < branches.Count; offset += ENTITY_VALUE_BRANCHES_PER_METHOD)
        {
            var groupIndex = offset / ENTITY_VALUE_BRANCHES_PER_METHOD;
            var suffix = groupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var groupName = $"__ExpressTryEntityValueEqualsGroup{suffix}";
            var shardName = $"__ExpressEntityEqualityShard{suffix}";
            var resultName = $"entityValueEqualityResult{suffix}";
            method.AddStatement(new CustomExpression(
                $"var {resultName} = {shards.Qualify(groupName, shardName)}(left, right, activePairs)"));
            method.AddStatement(new IfStatement(new CustomExpression($"{resultName} is not null"))
                .AddStatement(new CustomExpression($"{resultName}.Value").Return));

            var group = CreateMethod(groupName, new DataType(logicalTypeName).Null);
            AddEntityValueEqualsParameters(group);
            foreach (var branch in branches.Skip(offset).Take(ENTITY_VALUE_BRANCHES_PER_METHOD))
            {
                group.AddStatement(branch);
            }

            group.AddStatement(new CustomExpression("null").Return);
            if (shards.IsEnabled)
            {
                shards.Add(group, shardName);
            }
            else
            {
                methods.Add(group);
            }
        }

        method.AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return);
        AddSummary(method, "Compares generated entity values by exact dynamic projection and effective storage slots.");
        return methods;
    }

    private static void AddEntityValueEqualsParameters(Method method)
    {
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.Entity"), "left"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.Entity"), "right"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType(
                "global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<"
                + "global::TedToolkit.Step21.Entity, global::TedToolkit.Step21.Entity>>"),
            "activePairs"));
    }

    private static IfStatement CreateEntityValueBranch(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        string typeName,
        IReadOnlyList<ExpressEntityAttributeProjection> attributes,
        SelectValueEqualityHelpers selectValueEqualityHelpers)
    {
        var variableSuffix = typeName.TrimStart('_');
        var typedLeft = "typedLeft" + variableSuffix;
        var typedRight = "typedRight" + variableSuffix;
        var branch = new IfStatement(new CustomExpression($"left is {typeName} {typedLeft}"))
            .AddStatement(new IfStatement(new CustomExpression($"right is not {typeName} {typedRight}"))
                .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return))
            .AddStatement(new Statement(new CustomExpression(
                "activePairs.Add(new global::System.Collections.Generic.KeyValuePair<"
                + "global::TedToolkit.Step21.Entity, global::TedToolkit.Step21.Entity>(left, right))")))
            .AddStatement(new Statement(new CustomExpression(
                "var comparisonState = global::TedToolkit.Step21.LogicalValue.True")));
        for (var index = 0; index < attributes.Count; index++)
        {
            var attribute = attributes[index];
            var comparison = "attributeComparison" + index.ToString(CultureInfo.InvariantCulture);
            var equality = CreateAttributeValueEquality(
                plan,
                resolver,
                attribute,
                $"{typedLeft}.{attribute.StorageMemberName}",
                $"{typedRight}.{attribute.StorageMemberName}",
                "activePairs",
                selectValueEqualityHelpers);
            branch.AddStatement(new Statement(new CustomExpression($"var {comparison} = {equality}")))
                .AddStatement(new IfStatement(new CustomExpression(
                        $"{comparison} == global::TedToolkit.Step21.LogicalValue.False"))
                    .AddStatement(new Statement(new CustomExpression(
                        "activePairs.RemoveAt(activePairs.Count - 1)")))
                    .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return))
                .AddStatement(new IfStatement(new CustomExpression(
                        $"{comparison} == global::TedToolkit.Step21.LogicalValue.Unknown"))
                    .AddStatement(new Statement(new CustomExpression(
                        "comparisonState = global::TedToolkit.Step21.LogicalValue.Unknown"))));
        }

        branch.AddStatement(new Statement(new CustomExpression(
                "activePairs.RemoveAt(activePairs.Count - 1)")))
            .AddStatement(new CustomExpression("comparisonState").Return);
        return branch;
    }

    private static string CreateAttributeValueEquality(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        ExpressEntityAttributeProjection attribute,
        string left,
        string right,
        string activePairs,
        SelectValueEqualityHelpers? selectValueEqualityHelpers = null)
    {
        var projection = resolver.Resolve(plan.Schema.Identity, attribute.Type);
        var canBeNull = attribute.Attribute.IsOptional || projection.IsReferenceType;
        if (!canBeNull)
        {
            return CreateBoundValueEquality(
                plan,
                resolver,
                attribute.Type,
                left,
                right,
                activePairs,
                selectValueEqualityHelpers: selectValueEqualityHelpers);
        }

        var leftValue = projection.IsReferenceType ? left : $"({left}).Value";
        var rightValue = projection.IsReferenceType ? right : $"({right}).Value";
        return $"(({left}) is null || ({right}) is null ? global::TedToolkit.Step21.LogicalValue.Unknown : "
            + CreateBoundValueEquality(
                plan,
                resolver,
                attribute.Type,
                leftValue,
                rightValue,
                activePairs,
                selectValueEqualityHelpers: selectValueEqualityHelpers)
            + ")";
    }

    private static string CreateBoundValueEquality(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        ExpressBoundType type,
        string left,
        string right,
        string activePairs,
        ISet<ExpressBoundSymbol>? activeTypes = null,
        SelectValueEqualityHelpers? selectValueEqualityHelpers = null)
    {
        if (type is ExpressBoundNamedType named)
        {
            if (named.Declaration.Kind == ExpressDeclarationKind.Entity)
            {
                return $"__ExpressEntityValueEquals((global::TedToolkit.Step21.Entity)({left}), "
                    + $"(global::TedToolkit.Step21.Entity)({right}), {activePairs})";
            }

            activeTypes ??= new HashSet<ExpressBoundSymbol>();
            if (!activeTypes.Add(named.Declaration))
            {
                return LogicalValueComparison(
                    $"global::System.Object.Equals(({left}), ({right}))");
            }

            try
            {
                var defined = plan.Resolver.GetDefinedType(named.Declaration);
                if (defined.UnderlyingType is ExpressBoundSelectType select)
                {
                    if (selectValueEqualityHelpers is not null)
                    {
                        return selectValueEqualityHelpers.CreateCall(named, select, left, right, activePairs);
                    }

                    return CreateSelectValueEquality(
                        plan,
                        resolver,
                        select,
                        left,
                        right,
                        activePairs,
                        activeTypes: activeTypes);
                }

                if (defined.UnderlyingType is ExpressBoundEnumerationType)
                {
                    return LogicalValueComparison(
                        $"global::System.StringComparer.Ordinal.Equals(({left}).Value, ({right}).Value)");
                }

                return CreateBoundValueEquality(
                    plan,
                    resolver,
                    defined.UnderlyingType,
                    $"({left}).Value",
                    $"({right}).Value",
                    activePairs,
                    activeTypes,
                    selectValueEqualityHelpers);
            }
            finally
            {
                activeTypes.Remove(named.Declaration);
            }
        }

        if (type is ExpressBoundAggregateType aggregate)
        {
            var elementType = ExpressExpressionEmitter.BoundTypeName(aggregate.ElementType);
            var comparison = CreateBoundValueEquality(
                plan,
                resolver,
                aggregate.ElementType,
                "elementLeft",
                "elementRight",
                activePairs,
                activeTypes,
                selectValueEqualityHelpers);
            var callback = $"(elementLeft, elementRight) => {comparison}";
            return aggregate.Kind switch
            {
                ExpressAggregateKind.Array => $"__ExpressArrayValueEquals<{elementType}>({left}, {right}, {callback})",
                ExpressAggregateKind.List => $"__ExpressOrderedValueEquals<{elementType}>({left}, {right}, {callback})",
                ExpressAggregateKind.Set or ExpressAggregateKind.Bag =>
                    $"__ExpressUnorderedValueEquals<{elementType}>({left}, {right}, {callback})",
                _ => throw new InvalidOperationException("The aggregate kind has no generated value comparer."),
            };
        }

        return LogicalValueComparison(
            $"global::System.Collections.Generic.EqualityComparer<{ExpressExpressionEmitter.BoundTypeName(type)}>"
            + $".Default.Equals(({left}), ({right}))");
    }

    private static string CreateSelectValueEquality(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver,
        ExpressBoundSelectType select,
        string left,
        string right,
        string activePairs,
        bool instanceEquality = false,
        ExpressBoundType? rightType = null,
        int nestingDepth = 0,
        ISet<ExpressBoundSymbol>? activeTypes = null)
    {
        ExpressBoundSelectType? rightSelect = null;
        var resolvedRightType = rightType;
        while (resolvedRightType is ExpressBoundNamedType namedRight)
        {
            if (namedRight.Declaration.Kind == ExpressDeclarationKind.Entity)
            {
                break;
            }

            resolvedRightType = resolver.GetDefinedType(namedRight.Declaration).UnderlyingType;
        }

        rightSelect = resolvedRightType as ExpressBoundSelectType;
        if (!instanceEquality
            && (rightType is null || ReferenceEquals(rightSelect, select)))
        {
            var valueOuter = new List<string>();
            var valueAlternatives = plan.Resolver.GetSelectAlternatives(select);
            for (var leftIndex = 0; leftIndex < valueAlternatives.Count; leftIndex++)
            {
                var leftAlternative = valueAlternatives[leftIndex];
                var variableSuffix = select.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + select.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + leftIndex.ToString(CultureInfo.InvariantCulture);
                var leftName = "selectedLeft" + variableSuffix;
                var rightName = "selectedRight" + variableSuffix;
                var alternativeName = ExpressEntityProjection.ToPascalCase(leftAlternative.Name);
                var equality = CreateBoundValueEquality(
                    plan,
                    resolver,
                    new ExpressBoundNamedType(leftAlternative, select.Span),
                    leftName,
                    rightName + "!",
                    activePairs,
                    activeTypes);
                valueOuter.Add($"{leftName} => ({right}).TryGet{alternativeName}(out var {rightName}) "
                    + $"? {equality} : global::TedToolkit.Step21.LogicalValue.False");
            }

            return $"({left}).Match({string.Join(", ", valueOuter)})";
        }

        var outer = new List<string>();
        var alternatives = plan.Resolver.GetSelectAlternatives(select);
        for (var leftIndex = 0; leftIndex < alternatives.Count; leftIndex++)
        {
            var leftAlternative = alternatives[leftIndex];
            var leftName = "selectedLeft"
                + nestingDepth.ToString(CultureInfo.InvariantCulture)
                + "_"
                + leftIndex.ToString(CultureInfo.InvariantCulture);
            var leftValue = leftName;
            var resolvedLeft = leftAlternative;
            ExpressBoundSelectType? nestedLeft = null;
            while (resolvedLeft.Kind != ExpressDeclarationKind.Entity)
            {
                var underlying = resolver.GetDefinedType(resolvedLeft).UnderlyingType;
                if (underlying is ExpressBoundNamedType nestedNamed)
                {
                    leftValue = $"({leftValue}).Value";
                    resolvedLeft = nestedNamed.Declaration;
                    continue;
                }

                nestedLeft = underlying as ExpressBoundSelectType;
                break;
            }

            if (nestedLeft is not null)
            {
                outer.Add($"{leftName} => {CreateSelectValueEquality(
                    plan,
                    resolver,
                    nestedLeft,
                    leftValue,
                    right,
                    activePairs,
                    instanceEquality,
                    rightType,
                    nestingDepth + 1,
                    activeTypes)}");
                continue;
            }

            if (rightSelect is null)
            {
                var rightIsEntity = (resolvedRightType is ExpressBoundNamedType namedRightEntity
                    && namedRightEntity.Declaration.Kind == ExpressDeclarationKind.Entity)
                    || resolvedRightType is ExpressBoundGenericType { IsEntity: true, };
                string equality;
                if (resolvedLeft.Kind == ExpressDeclarationKind.Entity && rightIsEntity)
                {
                    equality = instanceEquality
                        ? $"global::System.Object.ReferenceEquals(({leftValue}), ({right}))"
                        : "__ExpressEntityValueEquals("
                            + $"(global::TedToolkit.Step21.Entity)({leftValue}), "
                            + $"(global::TedToolkit.Step21.Entity)({right}), "
                            + activePairs
                            + ")";
                }
                else
                {
                    equality = instanceEquality
                        ? "false"
                        : "global::TedToolkit.Step21.LogicalValue.False";
                }

                outer.Add($"{leftName} => {equality}");
                continue;
            }

            var inner = new List<string>();
            var rightAlternatives = plan.Resolver.GetSelectAlternatives(rightSelect);
            for (var rightIndex = 0; rightIndex < rightAlternatives.Count; rightIndex++)
            {
                var rightAlternative = rightAlternatives[rightIndex];
                var rightName = "selectedRight"
                    + nestingDepth.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + rightIndex.ToString(CultureInfo.InvariantCulture);
                var rightValue = rightName;
                var resolvedRight = rightAlternative;
                ExpressBoundSelectType? nestedRight = null;
                while (resolvedRight.Kind != ExpressDeclarationKind.Entity)
                {
                    var underlying = resolver.GetDefinedType(resolvedRight).UnderlyingType;
                    if (underlying is ExpressBoundNamedType nestedNamed)
                    {
                        rightValue = $"({rightValue}).Value";
                        resolvedRight = nestedNamed.Declaration;
                        continue;
                    }

                    nestedRight = underlying as ExpressBoundSelectType;
                    break;
                }

                string equality;
                if (nestedRight is not null)
                {
                    equality = CreateSelectValueEquality(
                        plan,
                        resolver,
                        nestedRight,
                        rightValue,
                        leftValue,
                        activePairs,
                        instanceEquality,
                        new ExpressBoundNamedType(resolvedLeft, select.Span),
                        nestingDepth + 1,
                        activeTypes);
                }
                else if (resolvedLeft.Kind == ExpressDeclarationKind.Entity
                    && resolvedRight.Kind == ExpressDeclarationKind.Entity)
                {
                    equality = instanceEquality
                        ? $"global::System.Object.ReferenceEquals(({leftValue}), ({rightValue}))"
                        : "__ExpressEntityValueEquals("
                            + $"(global::TedToolkit.Step21.Entity)({leftValue}), "
                            + $"(global::TedToolkit.Step21.Entity)({rightValue}), "
                            + activePairs
                            + ")";
                }
                else if (!instanceEquality && ReferenceEquals(resolvedLeft, resolvedRight))
                {
                    equality = CreateBoundValueEquality(
                        plan,
                        resolver,
                        new ExpressBoundNamedType(resolvedLeft, select.Span),
                        leftValue,
                        rightValue,
                        activePairs,
                        activeTypes);
                }
                else
                {
                    equality = instanceEquality
                        ? "false"
                        : "global::TedToolkit.Step21.LogicalValue.False";
                }

                inner.Add($"{rightName} => {equality}");
            }

            outer.Add($"{leftName} => ({right}).Match({string.Join(", ", inner)})");
        }

        return $"({left}).Match({string.Join(", ", outer)})";
    }

    private static string LogicalValueComparison(string comparison)
    {
        return $"(({comparison}) ? global::TedToolkit.Step21.LogicalValue.True : "
            + "global::TedToolkit.Step21.LogicalValue.False)";
    }

    private static Method CreateOrderedValueEqualsMethod()
    {
        var method = CreateMethod("__ExpressOrderedValueEquals", new DataType("global::TedToolkit.Step21.LogicalValue"));
        method.AddTypeParameter(SourceComposer.TypeParameter("T"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Collections.Generic.IReadOnlyList<T>"), "left"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Collections.Generic.IReadOnlyList<T>"), "right"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::System.Func<T, T, global::TedToolkit.Step21.LogicalValue>"),
            "equals"));
        method.AddStatement(new IfStatement(new CustomExpression("left.Count != right.Count"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return));
        method.AddStatement(new Statement(new CustomExpression("var hasUnknown = false")));
        var loop = new ForEachStatement(DataType.Var, "index", new CustomExpression("global::System.Linq.Enumerable.Range(0, left.Count)"));
        loop.AddStatement(new Statement(new CustomExpression("var comparison = equals(left[index], right[index])")));
        loop.AddStatement(new IfStatement(new CustomExpression("comparison == global::TedToolkit.Step21.LogicalValue.False"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return));
        loop.AddStatement(new IfStatement(new CustomExpression("comparison == global::TedToolkit.Step21.LogicalValue.Unknown"))
            .AddStatement(new Statement(new CustomExpression("hasUnknown = true"))));
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression(
            "hasUnknown ? global::TedToolkit.Step21.LogicalValue.Unknown : "
            + "global::TedToolkit.Step21.LogicalValue.True").Return);
        return method;
    }

    private static Method CreateArrayValueEqualsMethod()
    {
        var method = CreateMethod("__ExpressArrayValueEquals", new DataType("global::TedToolkit.Step21.LogicalValue"));
        method.AddTypeParameter(SourceComposer.TypeParameter("T"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.ExpressArray<T>"), "left"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.ExpressArray<T>"), "right"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::System.Func<T, T, global::TedToolkit.Step21.LogicalValue>"),
            "equals"));
        method.AddStatement(new IfStatement(new CustomExpression(
            "left.LowerIndex != right.LowerIndex || left.UpperIndex != right.UpperIndex"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return));
        method.AddStatement(new Statement(new CustomExpression("var hasUnknown = false")));
        var loop = new ForEachStatement(
            DataType.Var,
            "index",
            new CustomExpression("global::System.Linq.Enumerable.Range(left.LowerIndex, left.Count)"));
        var unset = new IfStatement(new CustomExpression("!left.IsSet(index) || !right.IsSet(index)"))
            .AddStatement(new Statement(new CustomExpression("hasUnknown = true")));
        var set = unset.Else()
            .AddStatement(new Statement(new CustomExpression(
                "var comparison = equals(left[index], right[index])")))
            .AddStatement(new IfStatement(new CustomExpression(
                "comparison == global::TedToolkit.Step21.LogicalValue.False"))
                .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return))
            .AddStatement(new IfStatement(new CustomExpression(
                "comparison == global::TedToolkit.Step21.LogicalValue.Unknown"))
                .AddStatement(new Statement(new CustomExpression("hasUnknown = true"))));
        loop.AddStatement(unset);
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression(
            "hasUnknown ? global::TedToolkit.Step21.LogicalValue.Unknown : "
            + "global::TedToolkit.Step21.LogicalValue.True").Return);
        return method;
    }

    private static Method CreateUnorderedValueEqualsMethod()
    {
        var method = CreateMethod("__ExpressUnorderedValueEquals", new DataType("global::TedToolkit.Step21.LogicalValue"));
        method.AddTypeParameter(SourceComposer.TypeParameter("T"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Collections.Generic.IReadOnlyCollection<T>"), "left"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Collections.Generic.IReadOnlyCollection<T>"), "right"));
        method.AddParameter(SourceComposer.Parameter(
            new DataType("global::System.Func<T, T, global::TedToolkit.Step21.LogicalValue>"),
            "equals"));
        method.AddStatement(new IfStatement(new CustomExpression("left.Count != right.Count"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return));
        method.AddStatement(new Statement(new CustomExpression("var leftValues = global::System.Linq.Enumerable.ToArray(left)")));
        method.AddStatement(new Statement(new CustomExpression("var rightValues = global::System.Linq.Enumerable.ToArray(right)")));
        method.AddStatement(new Statement(new CustomExpression(
            "var comparisons = new global::TedToolkit.Step21.LogicalValue[left.Count, right.Count]")));
        var leftLoop = new ForEachStatement(
            DataType.Var,
            "leftIndex",
            new CustomExpression("global::System.Linq.Enumerable.Range(0, left.Count)"));
        var rightLoop = new ForEachStatement(
            DataType.Var,
            "rightIndex",
            new CustomExpression("global::System.Linq.Enumerable.Range(0, right.Count)"))
            .AddStatement(new Statement(new CustomExpression(
                "comparisons[leftIndex, rightIndex] = equals(leftValues[leftIndex], rightValues[rightIndex])")));
        leftLoop.AddStatement(rightLoop);
        method.AddStatement(leftLoop);
        method.AddStatement(new IfStatement(new CustomExpression("__ExpressHasPerfectMatch(comparisons, false)"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.True").Return));
        method.AddStatement(new IfStatement(new CustomExpression("__ExpressHasPerfectMatch(comparisons, true)"))
            .AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.Unknown").Return));
        method.AddStatement(new CustomExpression("global::TedToolkit.Step21.LogicalValue.False").Return);
        return method;
    }

    private static Method CreateHasPerfectMatchMethod()
    {
        var method = CreateMethod("__ExpressHasPerfectMatch", new DataType("global::System.Boolean"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.LogicalValue[,]"), "comparisons"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Boolean"), "allowUnknown"));
        method.AddStatement(new Statement(new CustomExpression(
            "var rightToLeft = global::System.Linq.Enumerable.ToArray("
            + "global::System.Linq.Enumerable.Repeat(-1, comparisons.GetLength(1)))")));
        var loop = new ForEachStatement(
            DataType.Var,
            "leftIndex",
            new CustomExpression("global::System.Linq.Enumerable.Range(0, comparisons.GetLength(0))"));
        loop.AddStatement(new Statement(new CustomExpression("var visitedRight = new bool[comparisons.GetLength(1)]")));
        loop.AddStatement(new IfStatement(new CustomExpression(
            "!__ExpressTryMatch(leftIndex, comparisons, allowUnknown, visitedRight, rightToLeft)"))
            .AddStatement(new CustomExpression("false").Return));
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression("true").Return);
        return method;
    }

    private static Method CreateTryMatchMethod()
    {
        var method = CreateMethod("__ExpressTryMatch", new DataType("global::System.Boolean"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Int32"), "leftIndex"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::TedToolkit.Step21.LogicalValue[,]"), "comparisons"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Boolean"), "allowUnknown"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Boolean[]"), "visitedRight"));
        method.AddParameter(SourceComposer.Parameter(new DataType("global::System.Int32[]"), "rightToLeft"));
        var loop = new ForEachStatement(
            DataType.Var,
            "rightIndex",
            new CustomExpression("global::System.Linq.Enumerable.Range(0, comparisons.GetLength(1))"));
        var eligible = new IfStatement(new CustomExpression(
            "!visitedRight[rightIndex] && (comparisons[leftIndex, rightIndex] == global::TedToolkit.Step21.LogicalValue.True "
            + "|| (allowUnknown && comparisons[leftIndex, rightIndex] == global::TedToolkit.Step21.LogicalValue.Unknown))"))
            .AddStatement(new Statement(new CustomExpression("visitedRight[rightIndex] = true")));
        var match = new IfStatement(new CustomExpression(
            "rightToLeft[rightIndex] < 0 || __ExpressTryMatch(rightToLeft[rightIndex], comparisons, "
            + "allowUnknown, visitedRight, rightToLeft)"))
            .AddStatement(new Statement(new CustomExpression("rightToLeft[rightIndex] = leftIndex")));
        match.AddStatement(new CustomExpression("true").Return);
        eligible.AddStatement(match);
        loop.AddStatement(eligible);
        method.AddStatement(loop);
        method.AddStatement(new CustomExpression("false").Return);
        return method;
    }

    private static string ResolveAggregateElementValue(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        string source,
        ExpressBoundType target,
        bool sourceIsDeterminate)
    {
        var canBeIndeterminate = !sourceIsDeterminate && expression.Type.CanBeIndeterminate;
        if (target is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, } scalarTargetSelect
            && plan.Resolver.GetDefinedType(scalarTargetSelect.Declaration).UnderlyingType
                is ExpressBoundSelectType
            && ResolveScalarExpressionToSelectValue(
                plan,
                scalarTargetSelect,
                expression,
                source,
                canBeIndeterminate,
                "__expressSelectedScalarElement_"
                    + expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture))
                is { } selectedScalar)
        {
            return selectedScalar;
        }

        if (target is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, } aggregateTargetSelect
            && TryResolveSelectCarrier(
                plan,
                aggregateTargetSelect,
                out var aggregateSelectCarrier,
                out var aggregateSelectWrappers)
            && ResolveExpressionAggregateCandidates(plan, expression)
                .Select(aggregateSource => ResolveAggregateTypeToSelectValue(
                    plan,
                    aggregateSelectCarrier,
                    aggregateSource,
                    source,
                    "__expressSelectedAggregateElement_"
                        + expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture)))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .SingleOrDefault() is { } selectedAggregate)
        {
            for (var wrapperIndex = aggregateSelectWrappers.Count - 1;
                 wrapperIndex >= 0;
                 wrapperIndex--)
            {
                selectedAggregate = "new "
                    + ExpressExpressionEmitter.BoundTypeName(aggregateSelectWrappers[wrapperIndex])
                    + $"({selectedAggregate})";
            }

            return selectedAggregate;
        }

        if (target is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, } targetSelect
            && plan.Resolver.GetDefinedType(targetSelect.Declaration).UnderlyingType
                is ExpressBoundSelectType
            && expression.Type.DeclaredType is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, } sourceName
            && !ReferenceEquals(targetSelect.Declaration, sourceName.Declaration)
            && ResolveNamedExpressionToSelectValue(
                plan,
                targetSelect,
                sourceName,
                source,
                canBeIndeterminate,
                "__expressSelectedElement_"
                    + expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture)) is { } selected)
        {
            return selected;
        }

        if (target is ExpressBoundNamedType { Declaration.Kind: not ExpressDeclarationKind.Entity, } targetName
            && expression.Type.DeclaredType is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, } sourceEntity)
        {
            return ResolveEntityToSelectValue(plan, targetName, sourceEntity.Declaration, source) ?? source;
        }

        if (target is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, } targetEntity
            && expression.Type.DeclaredType is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, } sourceEntityType
            && !ReferenceEquals(targetEntity.Declaration, sourceEntityType.Declaration)
            && ResolveAggregateElementToEntity(
                plan,
                sourceEntityType,
                targetEntity,
                source,
                "__expressNarrowedAggregateElement_"
                    + expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                    + "_"
                    + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture),
                allowRuntimeNarrowing: true) is { } narrowedEntity)
        {
            var presentEntity = "__expressNarrowedAggregateElementValue_"
                + expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                + "_"
                + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
            return $"(({narrowedEntity}) is {{ }} {presentEntity} ? {presentEntity} "
                + ": throw new global::System.InvalidOperationException())";
        }

        var definedTypes = new List<ExpressBoundNamedType>();
        while (target is ExpressBoundNamedType definedType
               && definedType.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            var underlying = plan.Resolver.GetDefinedType(definedType.Declaration).UnderlyingType;
            if (underlying is ExpressBoundEnumerationType or ExpressBoundSelectType)
            {
                target = underlying;
                break;
            }

            definedTypes.Add(definedType);
            target = underlying;
        }

        if (definedTypes.Count == 0 || target is not ExpressBoundScalarType targetScalar)
        {
            if (definedTypes.Count == 0
                || target is not ExpressBoundEnumerationType
                || expression.Type.Kind != ExpressExpressionTypeKind.Enumeration)
            {
                return source;
            }

            var actualEnumeration = expression.Type.DeclaredType as ExpressBoundNamedType;
            var enumerationSemanticTarget = ResolveDefinedValueType(plan, definedTypes[0]);
            if (actualEnumeration is not null
                && !ReferenceEquals(actualEnumeration.Declaration, definedTypes[0].Declaration)
                && !(enumerationSemanticTarget is ExpressBoundNamedType semanticName
                    && ReferenceEquals(actualEnumeration.Declaration, semanticName.Declaration)))
            {
                return source;
            }

            var enumerationSource = sourceIsDeterminate && expression.Type.CanBeIndeterminate
                ? $"({source}).Value"
                : source;
            return WrapAggregateElementDefinedValue(
                enumerationSource,
                definedTypes,
                canBeIndeterminate,
                expression);
        }

        var actualNominal = expression.Type.DeclaredType as ExpressBoundNamedType;
        if (actualNominal is not null
            && !ReferenceEquals(actualNominal.Declaration, definedTypes[0].Declaration)
            && !(ResolveDefinedValueType(plan, definedTypes[0]) is ExpressBoundNamedType semanticTarget
                && ReferenceEquals(actualNominal.Declaration, semanticTarget.Declaration)))
        {
            return source;
        }

        var exactScalar = targetScalar.Kind switch
        {
            ExpressScalarKind.Binary => expression.Type.Kind == ExpressExpressionTypeKind.Binary,
            ExpressScalarKind.Boolean => expression.Type.Kind == ExpressExpressionTypeKind.Boolean,
            ExpressScalarKind.Integer => expression.Type.Kind == ExpressExpressionTypeKind.Integer,
            ExpressScalarKind.Logical => expression.Type.Kind == ExpressExpressionTypeKind.Logical,
            ExpressScalarKind.Number => expression.Type.Kind == ExpressExpressionTypeKind.Number,
            ExpressScalarKind.Real => expression.Type.Kind == ExpressExpressionTypeKind.Real,
            ExpressScalarKind.String => expression.Type.Kind == ExpressExpressionTypeKind.String,
            _ => false,
        };
        var widensToReal = expression.Type.Kind == ExpressExpressionTypeKind.Integer
            && targetScalar.Kind == ExpressScalarKind.Real;
        if (!exactScalar && !widensToReal)
        {
            return source;
        }

        if (widensToReal)
        {
            source = ExpressExpressionEmitter.PromoteNumeric(
                expression,
                source,
                ExpressExpressionTypeKind.Real);
        }

        return WrapAggregateElementDefinedValue(
            source,
            definedTypes,
            canBeIndeterminate,
            expression);
    }

    private static string WrapAggregateElementDefinedValue(
        string source,
        List<ExpressBoundNamedType> wrappers,
        bool canBeIndeterminate,
        ExpressBoundExpression expression)
    {
        var present = "__expressAggregateElement_"
            + expression.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
            + "_"
            + expression.Span.Start.Column.ToString(CultureInfo.InvariantCulture);
        var result = canBeIndeterminate ? present : source;
        for (var index = wrappers.Count - 1; index >= 0; index--)
        {
            result = "new " + ExpressExpressionEmitter.BoundTypeName(wrappers[index])
                + $"({result})";
        }

        return canBeIndeterminate
            ? $"(({source}) is {{ }} {present} ? {result} : ("
                + ExpressExpressionEmitter.BoundTypeName(wrappers[0])
                + "?)null)"
            : result;
    }

    private static string? ResolveNamedExpressionToSelectValue(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType target,
        ExpressBoundNamedType sourceType,
        string source,
        bool canBeIndeterminate,
        string presentName,
        bool sourceIsNominal = false)
    {
        var semanticSource = (ExpressBoundType)sourceType;
        var wrappers = sourceIsNominal
            ? []
            : ResolveTransparentDefinedWrappers(plan.Resolver, ref semanticSource);
        var nominalSource = canBeIndeterminate ? presentName : source;
        for (var index = wrappers.Count - 1; index >= 0; index--)
        {
            nominalSource = "new "
                + ExpressExpressionEmitter.BoundTypeName(wrappers[index])
                + $"({nominalSource})";
        }

        if (ResolveNamedToSelectValue(plan, target, sourceType, nominalSource) is not { } selected)
        {
            return null;
        }

        return canBeIndeterminate
            ? $"(({source}) is {{ }} {presentName} ? {selected} : ("
                + ExpressExpressionEmitter.BoundTypeName(target)
                + "?)null)"
            : selected;
    }

    private static string? ResolveSelectToSelectValue(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType target,
        ExpressBoundNamedType sourceType,
        string source,
        string variablePrefix)
    {
        if (plan.Resolver.GetDefinedType(sourceType.Declaration).UnderlyingType
                is not ExpressBoundSelectType sourceSelect)
        {
            return null;
        }

        var resultType = ExpressExpressionEmitter.BoundTypeName(target);
        var variableIndex = 0;
        string Dispatch(
            ExpressBoundNamedType carrier,
            ExpressBoundSelectType select,
            string carrierCode,
            HashSet<ExpressBoundSymbol> visited)
        {
            if (!visited.Add(carrier.Declaration))
            {
                return $"({resultType}?)null";
            }

            var branches = new List<string>();
            foreach (var alternative in plan.Resolver.GetSelectAlternatives(select))
            {
                var variable = variablePrefix
                    + "Source"
                    + (variableIndex++).ToString(CultureInfo.InvariantCulture);
                string value;
                if (alternative.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(alternative).UnderlyingType
                        is ExpressBoundSelectType nestedSelect)
                {
                    value = Dispatch(
                        new ExpressBoundNamedType(alternative, carrier.Span),
                        nestedSelect,
                        variable,
                        new HashSet<ExpressBoundSymbol>(visited));
                }
                else
                {
                    value = ResolveNamedToSelectValue(
                            plan,
                            target,
                            new ExpressBoundNamedType(alternative, carrier.Span),
                            variable)
                        ?? $"({resultType}?)null";
                }

                branches.Add(variable + " => " + value);
            }

            return $"({carrierCode}).Match<{resultType}?>({string.Join(", ", branches)})";
        }

        return Dispatch(
            sourceType,
            sourceSelect,
            source,
            new HashSet<ExpressBoundSymbol>());
    }

    private static string? ResolveSelectToRuntimeTarget(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType sourceType,
        string source,
        ExpressBoundNamedType target,
        string variablePrefix,
        bool sourceCanBeIndeterminate)
    {
        if (plan.Resolver.GetDefinedType(sourceType.Declaration).UnderlyingType
                is not ExpressBoundSelectType sourceSelect)
        {
            return null;
        }

        var variableIndex = 0;
        string Dispatch(
            ExpressBoundNamedType carrier,
            ExpressBoundSelectType select,
            string carrierCode,
            HashSet<ExpressBoundSymbol> visited)
        {
            if (!visited.Add(carrier.Declaration))
            {
                return "null";
            }

            var branches = plan.Resolver.GetSelectAlternatives(select).Select(alternative =>
            {
                var variable = variablePrefix
                    + "Source"
                    + (variableIndex++).ToString(CultureInfo.InvariantCulture);
                var value = alternative.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(alternative).UnderlyingType
                        is ExpressBoundSelectType nestedSelect
                        ? Dispatch(
                            new ExpressBoundNamedType(alternative, carrier.Span),
                            nestedSelect,
                            variable,
                            new HashSet<ExpressBoundSymbol>(visited))
                        : $"(object?){variable}";
                return variable + " => " + value;
            });
            return $"({carrierCode}).Match<object?>({string.Join(", ", branches)})";
        }

        var targetName = ExpressExpressionEmitter.BoundTypeName(target);
        var selected = variablePrefix + "Selected";
        var projected = $"({Dispatch(sourceType, sourceSelect, source, [])}) switch {{ "
            + $"{targetName} {selected} => {selected}, _ => ({targetName}?)null }}";
        if (!sourceCanBeIndeterminate)
        {
            return projected;
        }

        var present = variablePrefix + "Present";
        projected = $"({Dispatch(sourceType, sourceSelect, present, [])}) switch {{ "
            + $"{targetName} {selected} => {selected}, _ => ({targetName}?)null }}";
        return $"(({source}) is {{ }} {present} ? {projected} : ({targetName}?)null)";
    }

    private static string? ResolveSelectToEntityValue(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType target,
        ExpressBoundNamedType sourceType,
        string source,
        string variablePrefix)
    {
        if (plan.Resolver.GetDefinedType(sourceType.Declaration).UnderlyingType
                is not ExpressBoundSelectType sourceSelect)
        {
            return null;
        }

        var resultType = ExpressExpressionEmitter.BoundTypeName(target);
        var variableIndex = 0;
        string Dispatch(
            ExpressBoundNamedType carrier,
            ExpressBoundSelectType select,
            string carrierCode,
            HashSet<ExpressBoundSymbol> visited)
        {
            if (!visited.Add(carrier.Declaration))
            {
                return $"({resultType}?)null";
            }

            var branches = new List<string>();
            foreach (var alternative in plan.Resolver.GetSelectAlternatives(select))
            {
                var variable = variablePrefix
                    + "Source"
                    + (variableIndex++).ToString(CultureInfo.InvariantCulture);
                string value;
                if (IsCompatibleEntity(plan, alternative, target))
                {
                    value = variable;
                }
                else if (CanRuntimeProjectEntity(plan, alternative, target))
                {
                    var typed = variablePrefix
                        + "Typed"
                        + (variableIndex++).ToString(CultureInfo.InvariantCulture);
                    value = $"((object)({variable})) switch {{ {resultType} {typed} => {typed}, "
                        + $"_ => ({resultType}?)null }}";
                }
                else if (alternative.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(alternative).UnderlyingType
                        is ExpressBoundSelectType nestedSelect)
                {
                    value = Dispatch(
                        new ExpressBoundNamedType(alternative, carrier.Span),
                        nestedSelect,
                        variable,
                        new HashSet<ExpressBoundSymbol>(visited));
                }
                else
                {
                    value = $"({resultType}?)null";
                }

                branches.Add(variable + " => " + value);
            }

            return $"({carrierCode}).Match<{resultType}?>({string.Join(", ", branches)})";
        }

        return Dispatch(
            sourceType,
            sourceSelect,
            source,
            new HashSet<ExpressBoundSymbol>());
    }

    private static string? ResolveNarrowedScalarReference(
        ExpressReachableRulePlan plan,
        ExpressBoundName reference,
        string? selfExpression,
        string populationExpression,
        IReadOnlyDictionary<string, (string Code, ExpressBoundType Type)>? lexicalNames,
        IReadOnlyDictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings,
        ExpressBoundType carrierType,
        string carrier,
        ExpressBoundScalarType narrowedScalar)
    {
        var pendingType = carrierType;
        while (pendingType is ExpressBoundNamedType named
               && named.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            pendingType = plan.Resolver.GetDefinedType(named.Declaration).UnderlyingType;
        }

        return pendingType is ExpressBoundSelectType
            ? ResolveReference(
                plan,
                reference,
                selfExpression,
                populationExpression,
                lexicalNames,
                selectNarrowings,
                carrierType,
                carrier,
                narrowedScalarType: narrowedScalar)
            : null;
    }

    private static string ResolveSelectToEntityValue(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        string source,
        ExpressBoundNamedType target,
        Func<string, string> allocateTemporaryName)
    {
        if (ResolveSelectCarrierType(plan, expression) is not { } sourceName
            || plan.Resolver.GetDefinedType(sourceName.Declaration).UnderlyingType
                is not ExpressBoundSelectType sourceSelect)
        {
            return source;
        }

        var targetName = ExpressExpressionEmitter.BoundTypeName(target);
        var alternatives = plan.Resolver.GetSelectAlternatives(sourceSelect);
        var branches = alternatives.Select(alternative =>
        {
            var variable = allocateTemporaryName("__expressSelectedEntity");
            var isCompatible = alternative.Kind == ExpressDeclarationKind.Entity
                && plan.EntityProjections.Single(projection =>
                        projection.Entity.Symbol == alternative)
                    .PhysicalComponents.Any(component =>
                        component.Symbol == target.Declaration);
            return variable + " => " + (isCompatible
                ? $"({targetName}?){variable}"
                : $"({targetName}?)null");
        });
        var match = $".Match<{targetName}?>({string.Join(", ", branches)})";
        if (!expression.Type.CanBeIndeterminate)
        {
            return $"({source}){match}";
        }

        var present = allocateTemporaryName("__expressPresentSelect");
        return $"(({source}) is {{ }} {present} ? {present}{match} : ({targetName}?)null)";
    }

    private static ExpressBoundNamedType? ResolveSelectCarrierType(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression)
    {
        var declarationType = expression.Reference?.SchemaDeclaration is { } declaration
            && plan.GetDeclaration(declaration) is ExpressBoundOpaqueDeclaration opaque
                ? opaque.DeclaredType
                : null;
        ExpressBoundType?[] candidates =
        {
            expression.Type.DeclaredType,
            expression.Reference?.Type,
            expression.Reference?.Attribute?.Type,
            declarationType,
        };
        foreach (var named in candidates.OfType<ExpressBoundNamedType>())
        {
            if (TryResolveSelectCarrier(plan, named, out var carrier, out _))
            {
                return carrier;
            }
        }

        return null;
    }

    private static ExpressBoundAggregateType? ResolveExpressionAggregateType(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        ExpressBoundAggregateType target)
    {
        var aggregates = ResolveExpressionAggregateCandidates(plan, expression);
        var genericAggregate = aggregates.FirstOrDefault(candidate =>
            candidate.ElementType is ExpressBoundGenericType);
        if (genericAggregate is not null)
        {
            var actualElement = expression.Children
                .Select(child => ResolveExpressionAggregateType(plan, child, target))
                .OfType<ExpressBoundAggregateType>()
                .Select(candidate => candidate.ElementType)
                .FirstOrDefault(candidate => candidate is not ExpressBoundGenericType);
            if (actualElement is not null)
            {
                return new(
                    genericAggregate.Kind,
                    actualElement,
                    genericAggregate.LowerBoundText,
                    genericAggregate.UpperBoundText,
                    genericAggregate.IsOptional,
                    genericAggregate.IsUnique,
                    genericAggregate.TypeLabel,
                    genericAggregate.Span,
                    genericAggregate.ResolvedLowerBoundText,
                    genericAggregate.ResolvedUpperBoundText);
            }
        }

        return aggregates.FirstOrDefault(candidate =>
                !ExpressGeneratedTypeResolver.AreEquivalent(
                    candidate.ElementType,
                    target.ElementType))
            ?? (aggregates.Length > 0 ? aggregates[0] : null);
    }

    private static ExpressBoundAggregateType[] ResolveExpressionAggregateCandidates(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression)
    {
        var candidates = new List<ExpressBoundType?>();
        var qualifiedEntities = expression.Children
            .SelectMany(child => child.DescendantsAndSelf())
            .Where(candidate => candidate.Kind == ExpressExpressionKind.GroupQualifier)
            .Select(candidate => candidate.Reference?.SchemaDeclaration)
            .Where(candidate => candidate?.Kind == ExpressDeclarationKind.Entity)
            .Cast<ExpressBoundSymbol>()
            .ToList();
        qualifiedEntities.AddRange(expression.Children
            .Select(child => child.Type.DeclaredType)
            .OfType<ExpressBoundNamedType>()
            .Where(candidate => candidate.Declaration.Kind == ExpressDeclarationKind.Entity)
            .Select(candidate => candidate.Declaration));

        if (expression.Kind == ExpressExpressionKind.AttributeQualifier
            && expression.Reference is { } attributeReference)
        {
            candidates.AddRange(qualifiedEntities
                .Distinct()
                .Select(qualifiedEntity => plan.EntityProjections
                    .Single(projection => projection.Entity.Symbol == qualifiedEntity)
                    .EffectiveAttributes
                    .Where(candidate => string.Equals(
                        candidate.Attribute.Name,
                        attributeReference.Name,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => candidate.Type)
                    .FirstOrDefault()));
        }

        if (expression.Reference?.AttributeCandidates is { Count: > 0, } attributeCandidates)
        {
            candidates.AddRange(attributeCandidates.Select(candidate => candidate.Type));
        }

        candidates.Add(expression.Reference?.Attribute?.Type);
        candidates.Add(expression.Reference?.Type);
        candidates.Add(expression.Type.DeclaredType);
        return candidates
            .Where(candidate => candidate is not null)
            .Select(candidate => plan.Resolver.GetAggregateType(candidate!))
            .OfType<ExpressBoundAggregateType>()
            .Distinct()
            .ToArray();
    }

    private static bool HasSelfAggregateRedeclarationProof(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression expression,
        ExpressBoundAggregateType target,
        ExpressBoundSymbol? selfEntity)
    {
        if (selfEntity?.Kind != ExpressDeclarationKind.Entity
            || expression.Kind != ExpressExpressionKind.AttributeQualifier
            || expression.Reference is not { } attributeReference)
        {
            return false;
        }

        return plan.EntityProjections
            .Single(projection => projection.Entity.Symbol == selfEntity)
            .EffectiveAttributes
            .Where(candidate => string.Equals(
                candidate.Attribute.Name,
                attributeReference.Name,
                StringComparison.OrdinalIgnoreCase))
            .Select(candidate => plan.Resolver.GetAggregateType(candidate.Type))
            .OfType<ExpressBoundAggregateType>()
            .Any(candidate => candidate.Kind == target.Kind
                && ExpressGeneratedTypeResolver.AreEquivalent(
                    candidate.ElementType,
                    target.ElementType));
    }

    private static bool TryResolveSelectCarrier(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType type,
        out ExpressBoundNamedType carrier,
        out IReadOnlyList<ExpressBoundNamedType> wrappers)
    {
        var pending = type;
        var resolvedWrappers = new List<ExpressBoundNamedType>();
        var visited = new HashSet<ExpressBoundSymbol>();
        while (pending.Declaration.Kind != ExpressDeclarationKind.Entity
               && visited.Add(pending.Declaration))
        {
            var underlying = plan.Resolver.GetDefinedType(pending.Declaration).UnderlyingType;
            if (underlying is ExpressBoundSelectType)
            {
                carrier = pending;
                wrappers = resolvedWrappers;
                return true;
            }

            if (underlying is not ExpressBoundNamedType next
                || next.Declaration.Kind == ExpressDeclarationKind.Entity)
            {
                break;
            }

            resolvedWrappers.Add(pending);
            pending = next;
        }

        carrier = type;
        wrappers = Array.Empty<ExpressBoundNamedType>();
        return false;
    }

    private static bool IsTransparentAliasOf(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType source,
        ExpressBoundSymbol target)
    {
        ExpressBoundType pending = source;
        var visited = new HashSet<ExpressBoundSymbol>();
        while (pending is ExpressBoundNamedType named
               && named.Declaration.Kind != ExpressDeclarationKind.Entity
               && visited.Add(named.Declaration))
        {
            if (ReferenceEquals(named.Declaration, target))
            {
                return true;
            }

            var underlying = plan.Resolver.GetDefinedType(named.Declaration).UnderlyingType;
            if (underlying is ExpressBoundEnumerationType or ExpressBoundSelectType)
            {
                break;
            }

            pending = underlying;
        }

        return false;
    }

    private static string? ResolveAggregateValueToTarget(
        ExpressReachableRulePlan plan,
        ExpressBoundAggregateType sourceType,
        ExpressBoundAggregateType targetType,
        string source,
        string variablePrefix,
        bool sourceCanBeIndeterminate,
        bool materializeEquivalent,
        bool allowRuntimeEntityNarrowing = false)
    {
        if (targetType.Kind == ExpressAggregateKind.Array
            || sourceType.Kind != targetType.Kind)
        {
            return null;
        }

        var targetName = ExpressExpressionEmitter.BoundTypeName(targetType);
        if (ExpressGeneratedTypeResolver.AreEquivalent(sourceType.ElementType, targetType.ElementType))
        {
            if (!materializeEquivalent)
            {
                return null;
            }

            var input = variablePrefix + "Input";
            return sourceCanBeIndeterminate
                ? $"(({source}) is {{ }} {input} ? ({targetName})[..{input}] : ({targetName}?)null)"
                : $"({targetName})[..({source})]";
        }

        if (targetType.ElementType is not ExpressBoundNamedType targetElement)
        {
            return null;
        }

        var element = variablePrefix + "Element";
        string? adapted = null;
        if (sourceType.ElementType is ExpressBoundNamedType sourceElement
            && targetType.Kind == ExpressAggregateKind.Set
            && (sourceElement.Declaration.Kind, targetElement.Declaration.Kind)
                is (ExpressDeclarationKind.Entity, ExpressDeclarationKind.Entity))
        {
            var sourceProjection = plan.EntityProjections.Single(candidate =>
                candidate.Entity.Symbol == sourceElement.Declaration);
            var targetProjection = plan.EntityProjections.Single(candidate =>
                candidate.Entity.Symbol == targetElement.Declaration);
            var requiresRuntimeNarrowing = !sourceProjection.PhysicalComponents.Any(component =>
                    component.Symbol == targetElement.Declaration)
                && targetProjection.PhysicalComponents.Any(component =>
                    component.Symbol == sourceElement.Declaration);
            if (requiresRuntimeNarrowing)
            {
                var input = variablePrefix + "Input";
                var tested = variablePrefix + "Tested";
                var selected = variablePrefix + "Selected";
                var typed = variablePrefix + "Typed";
                var targetElementName = ExpressExpressionEmitter.BoundTypeName(targetElement);
                return $"(({source}) is {{ }} {input} && global::System.Linq.Enumerable.All("
                    + $"{input}, {tested} => {tested} is {targetElementName}) ? "
                    + $"({targetName})[..global::System.Linq.Enumerable.Select({input}, {selected} => "
                    + $"{selected} switch {{ {targetElementName} {typed} => {typed}, "
                    + "_ => throw new global::System.InvalidOperationException() })] : "
                    + $"({targetName}?)null)";
            }
        }

        if (sourceType.ElementType is ExpressBoundAggregateType sourceElementAggregate
            && TryResolveSelectCarrier(
                plan,
                targetElement,
                out var aggregateElementSelect,
                out var aggregateElementSelectWrappers)
            && ResolveAggregateTypeToSelectValue(
                plan,
                aggregateElementSelect,
                sourceElementAggregate,
                element,
                variablePrefix + "AggregateElement") is { } selectedAggregateElement)
        {
            for (var wrapperIndex = aggregateElementSelectWrappers.Count - 1;
                 wrapperIndex >= 0;
                 wrapperIndex--)
            {
                selectedAggregateElement = "new "
                    + ExpressExpressionEmitter.BoundTypeName(aggregateElementSelectWrappers[wrapperIndex])
                    + $"({selectedAggregateElement})";
            }

            adapted = selectedAggregateElement;
        }
        else if (sourceType.ElementType is ExpressBoundScalarType sourceScalarElement
            && TryResolveSelectCarrier(
                plan,
                targetElement,
                out var scalarElementSelect,
                out var scalarElementSelectWrappers)
            && TryAdaptScalarUnionElement(
                plan,
                sourceScalarElement,
                scalarElementSelect,
                element,
                new HashSet<ExpressBoundSymbol>(),
                out var selectedScalarElement,
                out _))
        {
            for (var wrapperIndex = scalarElementSelectWrappers.Count - 1;
                 wrapperIndex >= 0;
                 wrapperIndex--)
            {
                selectedScalarElement = "new "
                    + ExpressExpressionEmitter.BoundTypeName(scalarElementSelectWrappers[wrapperIndex])
                    + $"({selectedScalarElement})";
            }

            adapted = selectedScalarElement;
        }
        else if (sourceType.ElementType is ExpressBoundNamedType namedSourceElement
            && TryResolveSelectCarrier(
            plan,
            targetElement,
            out var targetSelectCarrier,
            out var targetSelectWrappers))
        {
            string? selected;
            if (TryResolveSelectCarrier(
                plan,
                namedSourceElement,
                out var sourceSelectCarrier,
                out var sourceSelectWrappers))
            {
                var sourceValue = element;
                for (var wrapperIndex = 0;
                     wrapperIndex < sourceSelectWrappers.Count;
                     wrapperIndex++)
                {
                    sourceValue = $"({sourceValue}).Value";
                }

                selected = ReferenceEquals(
                    targetSelectCarrier.Declaration,
                    sourceSelectCarrier.Declaration)
                    ? sourceValue
                    : ResolveSelectToSelectValue(
                        plan,
                        targetSelectCarrier,
                        sourceSelectCarrier,
                        sourceValue,
                        variablePrefix + "Selected");
            }
            else
            {
                selected = ResolveNamedExpressionToSelectValue(
                    plan,
                    targetSelectCarrier,
                    namedSourceElement,
                    element,
                    canBeIndeterminate: false,
                    element,
                    sourceIsNominal: true);
            }

            if (selected is not null)
            {
                for (var wrapperIndex = targetSelectWrappers.Count - 1;
                     wrapperIndex >= 0;
                     wrapperIndex--)
                {
                    selected = "new "
                        + ExpressExpressionEmitter.BoundTypeName(targetSelectWrappers[wrapperIndex])
                        + $"({selected})";
                }

                adapted = selected;
            }
        }
        else if (sourceType.ElementType is ExpressBoundNamedType sourceEntityElement
            && targetElement.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            adapted = ResolveAggregateElementToEntity(
                plan,
                sourceEntityElement,
                targetElement,
                element,
                variablePrefix,
                allowRuntimeNarrowing: allowRuntimeEntityNarrowing
                    || targetType.Kind == ExpressAggregateKind.Set);
        }

        if (adapted is null)
        {
            return null;
        }

        var present = variablePrefix + "Value";
        var inputValue = variablePrefix + "Input";
        var projected = $"({targetName})[..global::System.Linq.Enumerable.Select("
            + (sourceCanBeIndeterminate ? inputValue : $"({source})")
            + $", {element} => "
            + $"({adapted}) is {{ }} {present} ? {present} "
            + ": throw new global::System.InvalidOperationException())]";
        return sourceCanBeIndeterminate
            ? $"(({source}) is {{ }} {inputValue} ? {projected} : ({targetName}?)null)"
            : projected;
    }

    private static string? ResolveAggregateTypeToSelectValue(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType target,
        ExpressBoundAggregateType sourceType,
        string source,
        string variablePrefix)
    {
        if (plan.Resolver.GetDefinedType(target.Declaration).UnderlyingType
            is not ExpressBoundSelectType targetSelect)
        {
            return null;
        }

        var pending = new Stack<(ExpressBoundNamedType Carrier, ExpressBoundSelectType Select, int Depth)>();
        pending.Push((target, targetSelect, 0));
        var visited = new HashSet<ExpressBoundSymbol>();
        var candidates = new List<(string Value, int Depth)>();
        while (pending.Count > 0)
        {
            var (carrier, select, depth) = pending.Pop();
            if (!visited.Add(carrier.Declaration))
            {
                continue;
            }

            foreach (var alternative in plan.Resolver.GetSelectAlternatives(select))
            {
                var alternativeType = new ExpressBoundNamedType(alternative, target.Span);
                if (alternative.Kind != ExpressDeclarationKind.Entity
                    && plan.Resolver.GetDefinedType(alternative).UnderlyingType
                        is ExpressBoundSelectType nestedSelect)
                {
                    pending.Push((alternativeType, nestedSelect, depth + 1));
                }

                if (plan.Resolver.GetAggregateType(alternativeType) is not { } alternativeAggregate
                    || ResolveAggregateValueToTarget(
                        plan,
                        sourceType,
                        alternativeAggregate,
                        source,
                        variablePrefix + "Value",
                        sourceCanBeIndeterminate: false,
                        materializeEquivalent: true) is not { } aggregateValue)
                {
                    continue;
                }

                var nominalValue = "new "
                    + ExpressExpressionEmitter.BoundTypeName(alternativeType)
                    + $"({aggregateValue})";
                if (ResolveNamedToSelectValue(
                    plan,
                    target,
                    alternativeType,
                    nominalValue) is { } selected)
                {
                    candidates.Add((selected, depth));
                }
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var bestDepth = candidates.Min(candidate => candidate.Depth);
        var best = candidates
            .Where(candidate => candidate.Depth == bestDepth)
            .Select(candidate => candidate.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return best.Length == 1 ? best[0] : null;
    }

    private static string? ResolveAggregateElementToEntity(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType source,
        ExpressBoundNamedType target,
        string element,
        string variablePrefix,
        bool allowRuntimeNarrowing)
    {
        if (source.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            return plan.Resolver.GetDefinedType(source.Declaration).UnderlyingType
                    is ExpressBoundSelectType
                ? ResolveSelectToEntityValue(
                    plan,
                    target,
                    source,
                    element,
                    variablePrefix + "SelectedEntity")
                : null;
        }

        var sourceProjection = plan.EntityProjections.Single(candidate =>
            candidate.Entity.Symbol == source.Declaration);
        if (sourceProjection.PhysicalComponents.Any(component =>
            component.Symbol == target.Declaration))
        {
            return element;
        }

        var targetProjection = plan.EntityProjections.Single(candidate =>
            candidate.Entity.Symbol == target.Declaration);
        if (!allowRuntimeNarrowing
            || !targetProjection.PhysicalComponents.Any(component =>
            component.Symbol == source.Declaration))
        {
            return null;
        }

        var typed = variablePrefix + "TypedEntity";
        var targetName = ExpressExpressionEmitter.BoundTypeName(target);
        return $"({element} is {targetName} {typed} ? {typed} : ({targetName}?)null)";
    }

    private static string? ResolveEntityToSelectValue(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType target,
        ExpressBoundSymbol sourceEntity,
        string source)
    {
        return ResolveNamedToSelectValue(
            plan,
            target,
            new ExpressBoundNamedType(sourceEntity, target.Span),
            source);
    }

    private static bool HasSelectAssignmentProof(
        ExpressBoundExpression expression,
        ExpressBoundNamedType target)
    {
        return expression.Type.DeclaredType is ExpressBoundNamedType flowType
            && (flowType.Declaration.Kind == ExpressDeclarationKind.Entity
                || ReferenceEquals(flowType.Declaration, target.Declaration));
    }

    private static string? ResolveRuntimeEntityToSelectValue(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType target,
        ExpressBoundNamedType sourceType,
        string source,
        Func<string, string> allocateTemporaryName)
    {
        if (plan.Resolver.GetDefinedType(target.Declaration).UnderlyingType
            is not ExpressBoundSelectType targetSelect)
        {
            return null;
        }

        var pendingSelects = new Stack<(
            ExpressBoundSymbol Wrapper,
            ExpressBoundSelectType Select,
            IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
        pendingSelects.Push((
            target.Declaration,
            targetSelect,
            Array.Empty<(ExpressBoundSymbol, ExpressBoundSymbol)>()));
        var visitedSelects = new HashSet<ExpressBoundSymbol>();
        var candidates = new List<(
            ExpressBoundSymbol Leaf,
            ExpressEntityProjection Projection,
            IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
        while (pendingSelects.Count > 0)
        {
            var pending = pendingSelects.Pop();
            if (!visitedSelects.Add(pending.Wrapper))
            {
                continue;
            }

            foreach (var alternative in plan.Resolver.GetSelectAlternatives(pending.Select))
            {
                var path = pending.Path.Append((pending.Wrapper, alternative)).ToArray();
                if (alternative.Kind == ExpressDeclarationKind.Entity)
                {
                    var projection = plan.EntityProjections.Single(candidate =>
                        candidate.Entity.Symbol == alternative);
                    if (projection.PhysicalComponents.Any(component =>
                        component.Symbol == sourceType.Declaration))
                    {
                        candidates.Add((alternative, projection, path));
                    }

                    continue;
                }

                if (plan.Resolver.GetDefinedType(alternative).UnderlyingType
                    is ExpressBoundSelectType nestedSelect)
                {
                    pendingSelects.Push((alternative, nestedSelect, path));
                }
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var resultType = ExpressExpressionEmitter.BoundTypeName(target);
        var branches = candidates
            .OrderByDescending(candidate => candidate.Projection.PhysicalComponents.Count)
            .Select(candidate =>
            {
                var value = allocateTemporaryName("__expressSelectedRuntimeEntity");
                var result = value;
                for (var pathIndex = candidate.Path.Count - 1; pathIndex >= 0; pathIndex--)
                {
                    var step = candidate.Path[pathIndex];
                    result = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
                            step.Wrapper,
                            target.Span))
                        + ".From"
                        + ExpressEntityProjection.ToPascalCase(step.Alternative.Name)
                        + $"({result})";
                }

                return ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
                        candidate.Leaf,
                        target.Span))
                    + " "
                    + value
                    + " => "
                    + result;
            });
        return $"((object?)({source})) switch {{ {string.Join(", ", branches)}, "
            + $"_ => ({resultType}?)null }}";
    }

    private static string? ResolveNamedToSelectValue(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType target,
        ExpressBoundNamedType sourceType,
        string source)
    {
        if (plan.Resolver.GetDefinedType(target.Declaration).UnderlyingType
            is not ExpressBoundSelectType targetSelect)
        {
            return null;
        }

        var pendingSelects = new Stack<(
            ExpressBoundSymbol Wrapper,
            ExpressBoundSelectType Select,
            IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
        pendingSelects.Push((
            target.Declaration,
            targetSelect,
            Array.Empty<(ExpressBoundSymbol, ExpressBoundSymbol)>()));
        var visitedSelects = new HashSet<ExpressBoundSymbol>();
        var sourceProjection = sourceType.Declaration.Kind == ExpressDeclarationKind.Entity
            ? plan.EntityProjections.Single(projection =>
                projection.Entity.Symbol == sourceType.Declaration)
            : null;
        var compatiblePaths = new List<
            IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)>>();
        while (pendingSelects.Count > 0)
        {
            var pending = pendingSelects.Pop();
            if (!visitedSelects.Add(pending.Wrapper))
            {
                continue;
            }

            foreach (var alternative in plan.Resolver.GetSelectAlternatives(pending.Select))
            {
                var path = pending.Path
                    .Append((pending.Wrapper, alternative))
                    .ToArray();
                if (ReferenceEquals(alternative, sourceType.Declaration))
                {
                    compatiblePaths.Add(path);
                    continue;
                }

                if (alternative.Kind == ExpressDeclarationKind.Entity)
                {
                    if (sourceProjection?.PhysicalComponents.Any(component =>
                            component.Symbol == alternative) == true)
                    {
                        compatiblePaths.Add(path);
                    }

                    continue;
                }

                if (plan.Resolver.GetDefinedType(alternative).UnderlyingType
                    is ExpressBoundSelectType nestedSelect)
                {
                    pendingSelects.Push((alternative, nestedSelect, path));
                }
            }
        }

        if (compatiblePaths.Count != 1)
        {
            return null;
        }

        var result = source;
        var compatiblePath = compatiblePaths[0];
        for (var pathIndex = compatiblePath.Count - 1; pathIndex >= 0; pathIndex--)
        {
            var step = compatiblePath[pathIndex];
            result = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
                    step.Wrapper,
                    target.Span))
                + ".From"
                + ExpressEntityProjection.ToPascalCase(step.Alternative.Name)
                + $"({result})";
        }

        return result;
    }

    private static string ResolveReference(
        ExpressReachableRulePlan plan,
        ExpressBoundName reference,
        string? selfExpression,
        string populationExpression,
        IReadOnlyDictionary<string, (string Code, ExpressBoundType Type)>? lexicalNames,
        IReadOnlyDictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings,
        ExpressBoundType? narrowedType = null,
        string? narrowedCode = null,
        ExpressBoundSymbol? narrowedAlternative = null,
        ExpressBoundScalarType? narrowedScalarType = null,
        int narrowingDepth = 0,
        string? incompatibleScalarCode = null)
    {
        if (narrowedType is not null
            && narrowedCode is null
            && lexicalNames?.TryGetValue(reference.Name, out var narrowedLexical) == true)
        {
            narrowedCode = narrowedLexical.Code;
        }

        if (narrowedType is not null
            && narrowedCode is not null
            && (narrowedAlternative is not null || narrowedScalarType is not null))
        {
            if (narrowedAlternative is not null
                && narrowedType is ExpressBoundNamedType narrowedNamed
                && ReferenceEquals(narrowedNamed.Declaration, narrowedAlternative))
            {
                return narrowedCode;
            }

            if (narrowedAlternative is { Kind: not ExpressDeclarationKind.Entity, }
                && plan.Resolver.GetDefinedType(narrowedAlternative).UnderlyingType
                    is ExpressBoundSelectType targetSelect)
            {
                var pendingSelects = new Stack<(
                    ExpressBoundSymbol Wrapper,
                    ExpressBoundSelectType Select,
                    IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
                pendingSelects.Push((
                    narrowedAlternative,
                    targetSelect,
                    Array.Empty<(ExpressBoundSymbol, ExpressBoundSymbol)>()));
                var leafPaths = new List<(
                    ExpressBoundSymbol Leaf,
                    IReadOnlyList<(ExpressBoundSymbol Wrapper, ExpressBoundSymbol Alternative)> Path)>();
                var visitedSelects = new HashSet<ExpressBoundSymbol>();
                var allEntityLeaves = true;
                while (pendingSelects.Count > 0 && allEntityLeaves)
                {
                    var pending = pendingSelects.Pop();
                    if (!visitedSelects.Add(pending.Wrapper))
                    {
                        continue;
                    }

                    foreach (var alternative in plan.Resolver.GetSelectAlternatives(pending.Select))
                    {
                        var path = pending.Path
                            .Append((pending.Wrapper, alternative))
                            .ToArray();
                        if (alternative.Kind == ExpressDeclarationKind.Entity)
                        {
                            leafPaths.Add((alternative, path));
                            continue;
                        }

                        if (plan.Resolver.GetDefinedType(alternative).UnderlyingType
                            is ExpressBoundSelectType nestedSelect)
                        {
                            pendingSelects.Push((alternative, nestedSelect, path));
                            continue;
                        }

                        allEntityLeaves = false;
                        break;
                    }
                }

                if (allEntityLeaves && leafPaths.Count > 0)
                {
                    var branches = leafPaths
                        .OrderByDescending(candidate => plan.EntityProjections
                            .Single(projection => projection.Entity.Symbol == candidate.Leaf)
                            .PhysicalComponents.Count)
                        .Select((candidate, index) =>
                        {
                            var variable = "__expressNarrowedReference"
                                + narrowingDepth.ToString(CultureInfo.InvariantCulture)
                                + "_"
                                + index.ToString(CultureInfo.InvariantCulture);
                            var value = variable;
                            for (var pathIndex = candidate.Path.Count - 1; pathIndex >= 0; pathIndex--)
                            {
                                var step = candidate.Path[pathIndex];
                                value = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
                                        step.Wrapper,
                                        targetSelect.Span))
                                    + ".From"
                                    + ExpressEntityProjection.ToPascalCase(step.Alternative.Name)
                                    + $"({value})";
                            }

                            var leafType = ExpressExpressionEmitter.BoundTypeName(
                                new ExpressBoundNamedType(candidate.Leaf, targetSelect.Span));
                            return $"{leafType} {variable} => {value}";
                        });
                    return $"((object)({narrowedCode})) switch {{ {string.Join(", ", branches)}, "
                        + "_ => throw new global::System.InvalidOperationException() }";
                }
            }

            var carrierType = narrowedType;
            var carrier = narrowedCode;
            if (carrierType is ExpressBoundGenericType { IsEntity: true, }
                && narrowedAlternative is { Kind: ExpressDeclarationKind.Entity, })
            {
                var narrowedTypeName = ExpressExpressionEmitter.BoundTypeName(
                    new ExpressBoundNamedType(narrowedAlternative, carrierType.Span));
                var narrowedVariable = "__expressNarrowedReference"
                    + narrowingDepth.ToString(CultureInfo.InvariantCulture);
                return $"((object)({carrier})) switch {{ {narrowedTypeName} {narrowedVariable} => "
                    + $"{narrowedVariable}, _ => throw new global::System.InvalidOperationException() }}";
            }

            while (carrierType is ExpressBoundNamedType namedCarrier)
            {
                if (narrowedAlternative is not null
                    && ReferenceEquals(namedCarrier.Declaration, narrowedAlternative))
                {
                    return carrier;
                }

                if (namedCarrier.Declaration.Kind == ExpressDeclarationKind.Entity)
                {
                    if (narrowedAlternative is
                        {
                            Kind: ExpressDeclarationKind.Entity,
                        })
                    {
                        var narrowedTypeName = ExpressExpressionEmitter.BoundTypeName(
                            new ExpressBoundNamedType(narrowedAlternative, namedCarrier.Span));
                        var narrowedVariable = "__expressNarrowedReference"
                            + narrowingDepth.ToString(CultureInfo.InvariantCulture);
                        return $"((object)({carrier})) switch {{ {narrowedTypeName} {narrowedVariable} => "
                            + $"{narrowedVariable}, _ => throw new global::System.InvalidOperationException() }}";
                    }

                    return "throw new global::System.InvalidOperationException()";
                }

                var underlying = plan.Resolver.GetDefinedType(namedCarrier.Declaration).UnderlyingType;
                if (underlying is not ExpressBoundSelectType)
                {
                    carrier = $"({carrier}).Value";
                }

                carrierType = underlying;
            }

            if (carrierType is ExpressBoundSelectType select)
            {
                var branches = plan.Resolver.GetSelectAlternatives(select)
                    .Select((alternative, index) =>
                    {
                        var variable = "__expressNarrowedReference"
                            + narrowingDepth.ToString(CultureInfo.InvariantCulture)
                            + "_"
                            + index.ToString(CultureInfo.InvariantCulture);
                        var containsNarrowing = narrowedAlternative is not null
                            && (ReferenceEquals(alternative, narrowedAlternative)
                                || (alternative.Kind == ExpressDeclarationKind.Entity
                                    && narrowedAlternative.Kind == ExpressDeclarationKind.Entity
                                    && (plan.EntityProjections.Single(projection =>
                                                projection.Entity.Symbol == narrowedAlternative)
                                            .PhysicalComponents.Any(component =>
                                                component.Symbol == alternative)
                                        || plan.EntityProjections.Single(projection =>
                                                projection.Entity.Symbol == alternative)
                                            .PhysicalComponents.Any(component =>
                                                component.Symbol == narrowedAlternative))));
                        if (!containsNarrowing
                            && alternative.Kind != ExpressDeclarationKind.Entity)
                        {
                            var pendingTypes = new Stack<ExpressBoundType>();
                            pendingTypes.Push(new ExpressBoundNamedType(alternative, select.Span));
                            var visitedDeclarations = new HashSet<ExpressBoundSymbol>();
                            while (pendingTypes.Count > 0 && !containsNarrowing)
                            {
                                var pendingType = pendingTypes.Pop();
                                if (pendingType is ExpressBoundNamedType pendingNamed)
                                {
                                    if (!visitedDeclarations.Add(pendingNamed.Declaration))
                                    {
                                        continue;
                                    }

                                    if (ReferenceEquals(
                                        pendingNamed.Declaration,
                                        narrowedAlternative))
                                    {
                                        containsNarrowing = true;
                                        break;
                                    }

                                    if (pendingNamed.Declaration.Kind == ExpressDeclarationKind.Entity
                                        && narrowedAlternative?.Kind == ExpressDeclarationKind.Entity
                                        && plan.EntityProjections.Single(projection =>
                                                projection.Entity.Symbol == narrowedAlternative)
                                            .PhysicalComponents.Any(component =>
                                                component.Symbol == pendingNamed.Declaration))
                                    {
                                        containsNarrowing = true;
                                        break;
                                    }

                                    if (pendingNamed.Declaration.Kind != ExpressDeclarationKind.Entity)
                                    {
                                        pendingTypes.Push(plan.Resolver.GetDefinedType(
                                            pendingNamed.Declaration).UnderlyingType);
                                    }
                                }
                                else if (pendingType is ExpressBoundSelectType pendingSelect)
                                {
                                    foreach (var pendingAlternative in plan.Resolver.GetSelectAlternatives(
                                                 pendingSelect))
                                    {
                                        pendingTypes.Push(new ExpressBoundNamedType(
                                            pendingAlternative,
                                            pendingSelect.Span));
                                    }
                                }
                                else if (pendingType is ExpressBoundScalarType pendingScalar
                                         && narrowedScalarType is not null
                                         && CanProjectScalar(pendingScalar.Kind, narrowedScalarType.Kind))
                                {
                                    containsNarrowing = true;
                                }
                            }
                        }

                        var value = containsNarrowing
                            ? ResolveReference(
                                plan,
                                reference,
                                selfExpression,
                                populationExpression,
                                lexicalNames,
                                selectNarrowings,
                                new ExpressBoundNamedType(alternative, select.Span),
                                variable,
                                narrowedAlternative,
                                narrowedScalarType,
                                narrowingDepth + 1,
                                incompatibleScalarCode)
                            : incompatibleScalarCode
                                ?? "throw new global::System.InvalidOperationException()";
                        return $"{variable} => {value}";
                    });
                if (narrowedAlternative is null)
                {
                    return $"({carrier}).Match({string.Join(", ", branches)})";
                }

                var resultType = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
                    narrowedAlternative,
                    select.Span));
                return $"({carrier}).Match<{resultType}>({string.Join(", ", branches)})";
            }

            if (carrierType is ExpressBoundScalarType scalar
                && narrowedScalarType is not null
                && CanProjectScalar(scalar.Kind, narrowedScalarType.Kind))
            {
                return (scalar.Kind, narrowedScalarType.Kind) switch
                {
                    (ExpressScalarKind.Integer, ExpressScalarKind.Number) =>
                        $"global::TedToolkit.Step21.NumberValue.FromInteger({carrier})",
                    (ExpressScalarKind.Real, ExpressScalarKind.Number) =>
                        $"global::TedToolkit.Step21.NumberValue.FromReal({carrier})",
                    (ExpressScalarKind.Integer, ExpressScalarKind.Real) =>
                        $"new global::TedToolkit.Step21.RealValue(({carrier}), "
                        + "global::System.Numerics.BigInteger.Zero)",
                    (ExpressScalarKind.Number, ExpressScalarKind.Integer) =>
                        $"({carrier}).ToIntegerTruncated()",
                    (ExpressScalarKind.Number, ExpressScalarKind.Real) => $"({carrier}).ToReal()",
                    _ => carrier,
                };
            }

            return incompatibleScalarCode
                ?? "throw new global::System.InvalidOperationException()";
        }

        if (lexicalNames is not null
            && lexicalNames.TryGetValue(reference.Name, out var lexicalName))
        {
            return lexicalName.Code;
        }

        if (reference.Kind == ExpressBoundNameKind.Enumeration
            && reference.Type is ExpressBoundNamedType enumeration)
        {
            return ExpressExpressionEmitter.BoundTypeName(enumeration)
                + "."
                + ExpressEntityProjection.ToPascalCase(reference.Name);
        }

        if (reference.Attribute is { } attribute)
        {
            if (selfExpression is null)
            {
                throw new InvalidOperationException(
                    $"Unqualified attribute '{reference.Name}' has no enclosing entity value.");
            }

            return ResolveAttribute(plan, null, reference, selfExpression, populationExpression, null, null);
        }

        if (reference.SchemaDeclaration is { } symbol)
        {
            return symbol.Kind switch
            {
                ExpressDeclarationKind.Constant =>
                    $"{ConstantMethodName(symbol)}({populationExpression}{ValidationContextArgumentSuffix(plan)})",
                ExpressDeclarationKind.Function => FunctionInvocation(
                    plan,
                    symbol,
                    populationExpression),
                _ => throw new InvalidOperationException(
                    $"Reachable reference '{reference.Name}' has no private generated evaluator."),
            };
        }

        throw new InvalidOperationException(
            $"Lexical reference '{reference.Name}' has no generated spelling in this operation.");
    }

    private static bool CanProjectScalar(ExpressScalarKind carrier, ExpressScalarKind target)
    {
        return carrier == target
            || (carrier == ExpressScalarKind.Number
                && target is ExpressScalarKind.Integer or ExpressScalarKind.Real)
            || (target == ExpressScalarKind.Number
                && carrier is ExpressScalarKind.Integer or ExpressScalarKind.Real)
            || (carrier == ExpressScalarKind.Integer && target == ExpressScalarKind.Real);
    }

    private static string ResolveAttribute(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression? sourceExpression,
        ExpressBoundName reference,
        string source,
        string populationExpression,
        IReadOnlyDictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings,
        IReadOnlyList<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>? pathNarrowings,
        ExpressBoundAttribute? currentDerivedAttribute = null)
    {
        ExpressBoundType? sourceType = sourceExpression?.Type.DeclaredType;
        var sourceHasNarrowedStaticType = sourceType is ExpressBoundNamedType
        {
            Declaration.Kind: ExpressDeclarationKind.Entity,
        };
        var sourceIsNarrowedCarrier = false;
        var sourceCarrierType = sourceType;
        var sourceHasDeclaredSelectCarrier = false;
        var declaredSourceType = sourceExpression?.Reference?.Attribute?.Type
            ?? (sourceExpression?.Reference?.AttributeCandidates.Count == 1
                ? sourceExpression.Reference.AttributeCandidates[0].Type
                : sourceExpression?.Reference?.Type);
        if (declaredSourceType is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, } declaredCarrier
            && ResolvesToSelectType(plan, declaredCarrier))
        {
            sourceCarrierType = ResolveDefinedValueType(plan, declaredCarrier);
            sourceHasDeclaredSelectCarrier = true;
        }

        // Attribute/reference emission already unwraps a SELECT when flow analysis gives
        // the expression an entity type.  Preserve that fact while resolving a following
        // group qualifier; otherwise the already-unwrapped entity receives a second Match.
        sourceIsNarrowedCarrier = sourceHasNarrowedStaticType
            && sourceHasDeclaredSelectCarrier;

        ExpressBoundSymbol? narrowedAlternative = null;
        if (sourceExpression?.Reference is { } sourceReference
            && selectNarrowings?.TryGetValue(sourceReference, out narrowedAlternative) == true)
        {
            if (sourceExpression.Kind != ExpressExpressionKind.AttributeQualifier
                && sourceCarrierType is not null)
            {
                source = ResolveNarrowedEntityCarrier(
                    plan,
                    sourceCarrierType,
                    source,
                    narrowedAlternative);
                sourceIsNarrowedCarrier = true;
            }

            sourceType = new ExpressBoundNamedType(
                narrowedAlternative,
                sourceType?.Span ?? narrowedAlternative.Span);
        }

        if (narrowedAlternative is null
            && sourceType is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, } staticallyNarrowedSource
            && sourceCarrierType is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, } staticallyNarrowedCarrier
            && ResolvesToSelectType(plan, staticallyNarrowedCarrier))
        {
            narrowedAlternative = staticallyNarrowedSource.Declaration;
            if (sourceExpression?.Kind is not (ExpressExpressionKind.Reference
                or ExpressExpressionKind.AttributeQualifier
                or ExpressExpressionKind.GroupQualifier))
            {
                source = ResolveNarrowedEntityCarrier(
                    plan,
                    sourceCarrierType,
                    source,
                    narrowedAlternative);
                sourceIsNarrowedCarrier = true;
            }
        }

        if (narrowedAlternative is null && sourceExpression is not null)
        {
            var matchingPathNarrowings = pathNarrowings
                ?.Where(narrowing => SameDirectReferencePath(narrowing.Key, sourceExpression))
                .ToArray();
            var pathAlternatives = matchingPathNarrowings
                ?.Select(narrowing => narrowing.Value)
                .Distinct()
                .ToArray();
            if (pathAlternatives is { Length: 1, }
                && pathAlternatives[0].Kind == ExpressDeclarationKind.Entity)
            {
                narrowedAlternative = pathAlternatives[0];
                if (sourceCarrierType is not ExpressBoundNamedType
                    { Declaration.Kind: not ExpressDeclarationKind.Entity, }
                    && matchingPathNarrowings?.Select(narrowing => narrowing.Key.Type.DeclaredType)
                        .OfType<ExpressBoundNamedType>()
                        .FirstOrDefault(candidate =>
                            candidate.Declaration.Kind != ExpressDeclarationKind.Entity
                            && ResolvesToSelectType(plan, candidate)) is { } pathCarrierType)
                {
                    sourceCarrierType = pathCarrierType;
                }

                var sourceCodeAlreadyNarrowed = sourceHasNarrowedStaticType
                    && (sourceExpression.Kind is ExpressExpressionKind.GroupQualifier
                        or ExpressExpressionKind.IndexQualifier
                        || sourceHasDeclaredSelectCarrier);
                if (!sourceCodeAlreadyNarrowed && sourceCarrierType is not null)
                {
                    source = ResolveNarrowedEntityCarrier(
                        plan,
                        sourceCarrierType,
                        source,
                        narrowedAlternative);
                    sourceIsNarrowedCarrier = true;
                }
                else if (sourceCodeAlreadyNarrowed)
                {
                    sourceIsNarrowedCarrier = true;
                }

                sourceType = new ExpressBoundNamedType(
                    narrowedAlternative,
                    sourceType?.Span ?? narrowedAlternative.Span);
            }
        }

        while (sourceType is ExpressBoundNamedType namedSource
               && namedSource.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            sourceType = plan.Resolver.GetDefinedType(namedSource.Declaration).UnderlyingType;
        }

        if (reference.Kind == ExpressBoundNameKind.Entity
            && reference.SchemaDeclaration is { } narrowedGroup
            && sourceType is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, })
        {
            return ResolveNarrowedEntityCarrier(
                plan,
                sourceIsNarrowedCarrier ? sourceType : sourceCarrierType ?? sourceType,
                source,
                narrowedGroup);
        }

        if (reference.Kind == ExpressBoundNameKind.Entity
            && reference.SchemaDeclaration is { } genericGroup
            && sourceType is ExpressBoundGenericType { IsEntity: true, })
        {
            var groupType = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
                genericGroup,
                sourceType.Span));
            return $"(({groupType})({source}))";
        }

        if (sourceType is ExpressBoundSelectType select)
        {
            var resultType = reference.Type
                ?? throw new InvalidOperationException(
                    $"SELECT attribute '{reference.Name}' has no statically resolved type.");
            var alternatives = plan.Resolver.GetSelectAlternatives(select);
            var group = reference.Kind == ExpressBoundNameKind.Entity
                ? reference.SchemaDeclaration
                : null;
            if (group is not null
                && !alternatives.Any(alternative =>
                    alternative.Kind == ExpressDeclarationKind.Entity
                    && plan.EntityProjections.Single(candidate => candidate.Entity.Symbol == alternative)
                        .PhysicalComponents.Any(component => component.Symbol == group)))
            {
                return ResolveNarrowedEntityCarrier(
                    plan,
                    sourceCarrierType ?? select,
                    source,
                    group);
            }

            var branches = alternatives.Select((alternative, index) =>
            {
                var variable = "__selected"
                    + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var projection = alternative.Kind == ExpressDeclarationKind.Entity
                    ? plan.EntityProjections.Single(candidate => candidate.Entity.Symbol == alternative)
                    : null;
                var supportsGroup = group is not null
                    && projection?.PhysicalComponents.Any(component => component.Symbol == group) == true;
                var supportedAttributes = projection is null
                    ? []
                    : reference.AttributeCandidates
                        .Where(candidate => projection.PhysicalComponents.Contains(plan.GetAttributeOwner(candidate)))
                        .ToArray();
                var mostSpecificAttributes = supportedAttributes
                    .Where(candidate => !supportedAttributes.Any(other =>
                        !ReferenceEquals(candidate, other)
                        && plan.EntityProjections.Single(ownerProjection => ReferenceEquals(
                                ownerProjection.Entity,
                                plan.GetAttributeOwner(other)))
                            .PhysicalComponents.Contains(plan.GetAttributeOwner(candidate))))
                    .ToArray();
                var narrowingProvesGroup = group is not null
                    && narrowedAlternative is not null
                    && alternatives.Contains(narrowedAlternative)
                    && plan.EntityProjections.Single(candidate =>
                            candidate.Entity.Symbol == narrowedAlternative)
                        .PhysicalComponents.Any(component => component.Symbol == group);
                var supportsAttribute = supportedAttributes.Length > 0;
                var narrowingProvesAttribute = narrowedAlternative is not null
                    && alternatives.Contains(narrowedAlternative)
                    && plan.EntityProjections.Single(candidate =>
                            candidate.Entity.Symbol == narrowedAlternative)
                        .PhysicalComponents.Any(component =>
                            reference.AttributeCandidates.Any(candidate =>
                                ReferenceEquals(component, plan.GetAttributeOwner(candidate))));
                ExpressBoundAttribute? selectedAttribute = null;
                string value;
                if ((narrowingProvesGroup || narrowingProvesAttribute)
                    && alternative != narrowedAlternative)
                {
                    value = "throw new global::System.InvalidOperationException()";
                }
                else if (supportsGroup)
                {
                    value = variable;
                }
                else if (supportsAttribute)
                {
                    if (mostSpecificAttributes.Length == 1)
                    {
                        var candidate = mostSpecificAttributes[0];
                        if (reference.Attribute is { Kind: ExpressAttributeKind.Explicit, } inheritedSlot
                            && ExpressGeneratedTypeResolver.AreEquivalent(inheritedSlot.Type, resultType)
                            && !ExpressGeneratedTypeResolver.AreEquivalent(candidate.Type, resultType))
                        {
                            candidate = inheritedSlot;
                        }

                        selectedAttribute = candidate;
                        var owner = plan.GetAttributeOwner(candidate);
                        value = candidate.Kind switch
                        {
                            ExpressAttributeKind.Derived =>
                                $"{DerivedMethodName(owner, candidate)}({variable}, {populationExpression}{ValidationContextArgumentSuffix(plan)})",
                            ExpressAttributeKind.Inverse =>
                                $"{InverseMethodName(owner, candidate)}((global::TedToolkit.Step21.Entity)({variable}), {populationExpression}{ValidationContextArgumentSuffix(plan)})",
                            _ => ResolveExplicitAttribute(
                                plan,
                                projection,
                                candidate,
                                variable,
                                populationExpression,
                                currentDerivedAttribute),
                        };
                    }
                    else
                    {
                        value = ResolveAttribute(
                            plan,
                            null,
                            reference,
                            variable,
                            populationExpression,
                            null,
                            null,
                            currentDerivedAttribute);
                    }

                    if (resultType is ExpressBoundGenericType { IsEntity: true, })
                    {
                        value = $"((global::TedToolkit.Step21.Entity?)({value}))";
                    }
                    else if (selectedAttribute is not null)
                    {
                        value = AdaptAttributeValueToDeclaredCarrier(
                            plan,
                            selectedAttribute.Type,
                            resultType,
                            value);
                    }
                }
                else
                {
                    value = $"default({ExpressExpressionEmitter.BoundTypeName(resultType)}?)";
                }

                return $"{variable} => {value}";
            });
            var groupResultType = reference.Kind == ExpressBoundNameKind.Entity
                || resultType is ExpressBoundGenericType { IsEntity: true, }
                ? $"<{ExpressExpressionEmitter.BoundTypeName(resultType)}?>"
                : "";
            return $"({source}).Match{groupResultType}({string.Join(", ", branches)})";
        }

        if (reference.AttributeCandidates.Count > 1)
        {
            var resultType = reference.Type
                ?? throw new InvalidOperationException(
                    $"Polymorphic attribute '{reference.Name}' has no statically resolved type.");
            var sourceProjection = sourceExpression?.Type.DeclaredType is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, } namedCarrier
                ? plan.EntityProjections.Single(candidate => ReferenceEquals(
                    candidate.Entity.Symbol,
                    namedCarrier.Declaration))
                : null;
            var compatibleCandidates = sourceProjection is null
                ? reference.AttributeCandidates.ToArray()
                : reference.AttributeCandidates
                    .Where(candidate => sourceProjection.PhysicalComponents.Contains(
                        plan.GetAttributeOwner(candidate)))
                    .ToArray();
            if (compatibleCandidates.Length == 0)
            {
                compatibleCandidates = reference.AttributeCandidates.ToArray();
            }

            var mostSpecificCandidates = compatibleCandidates
                .Where(candidate => !compatibleCandidates.Any(other =>
                    !ReferenceEquals(candidate, other)
                    && plan.EntityProjections.Single(ownerProjection => ReferenceEquals(
                            ownerProjection.Entity,
                            plan.GetAttributeOwner(other)))
                        .PhysicalComponents.Contains(plan.GetAttributeOwner(candidate))))
                .ToArray();
            if (mostSpecificCandidates.Length == 1)
            {
                var candidate = mostSpecificCandidates[0];
                var owner = plan.GetAttributeOwner(candidate);
                var value = candidate.Kind switch
                {
                    ExpressAttributeKind.Derived =>
                        $"{DerivedMethodName(owner, candidate)}({source}, {populationExpression}{ValidationContextArgumentSuffix(plan)})",
                    ExpressAttributeKind.Inverse =>
                        $"{InverseMethodName(owner, candidate)}((global::TedToolkit.Step21.Entity)({source}), {populationExpression}{ValidationContextArgumentSuffix(plan)})",
                    _ => ResolveExplicitAttribute(
                        plan,
                        sourceProjection,
                        candidate,
                        source,
                        populationExpression,
                        currentDerivedAttribute),
                };
                return AdaptAttributeValueToDeclaredCarrier(
                    plan,
                    candidate.Type,
                    resultType,
                    value);
            }

            var candidateOwners = compatibleCandidates
                .Select(plan.GetAttributeOwner)
                .ToArray();
            var orderedCandidates = compatibleCandidates
                .OrderByDescending(candidate => plan.EntityProjections.Single(ownerProjection => ReferenceEquals(
                        ownerProjection.Entity,
                        plan.GetAttributeOwner(candidate)))
                    .PhysicalComponents.Count(component => candidateOwners.Contains(component)))
                .ThenBy(candidate => plan.GetAttributeOwner(candidate).Name, StringComparer.Ordinal)
                .ToArray();
            var cases = orderedCandidates.Select((candidate, index) =>
            {
                var owner = plan.GetAttributeOwner(candidate);
                var ownerType = "global::TedToolkit.Step21.Generated."
                    + ExpressEntityProjection.ToPascalCase(owner.Symbol.DeclaringSchema.Name)
                    + ".I"
                    + ExpressEntityProjection.ToPascalCase(owner.Name);
                var variable = $"__candidate{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                var access = candidate.Kind switch
                {
                    ExpressAttributeKind.Derived =>
                        $"{DerivedMethodName(owner, candidate)}({variable}, {populationExpression}{ValidationContextArgumentSuffix(plan)})",
                    ExpressAttributeKind.Inverse =>
                        $"{InverseMethodName(owner, candidate)}((global::TedToolkit.Step21.Entity)({variable}), {populationExpression}{ValidationContextArgumentSuffix(plan)})",
                    _ => $"{variable}.{ExpressEntityProjection.ToPascalCase(candidate.Name)}",
                };
                access = AdaptAttributeValueToDeclaredCarrier(
                    plan,
                    candidate.Type,
                    resultType,
                    access);
                return $"{ownerType} {variable} => {access}";
            });
            return $"((object)({source})) switch {{ {string.Join(", ", cases)}, "
                + "_ => throw new global::System.InvalidOperationException() }";
        }

        var attribute = reference.Attribute;
        if (attribute?.Kind == ExpressAttributeKind.Derived)
        {
            var owner = plan.GetAttributeOwner(attribute);
            return $"{DerivedMethodName(owner, attribute)}({source}, {populationExpression}{ValidationContextArgumentSuffix(plan)})";
        }

        if (attribute?.Kind == ExpressAttributeKind.Inverse)
        {
            var owner = plan.GetAttributeOwner(attribute);
            return $"{InverseMethodName(owner, attribute)}("
                + $"(global::TedToolkit.Step21.Entity)({source}), {populationExpression}{ValidationContextArgumentSuffix(plan)})";
        }

        if (attribute?.Kind == ExpressAttributeKind.Explicit)
        {
            var sourceProjection = GetSourceProjection(plan, sourceExpression);
            return ResolveExplicitAttribute(
                plan,
                sourceProjection,
                attribute,
                source,
                populationExpression,
                currentDerivedAttribute);
        }

        return $"({source}).{ExpressEntityProjection.ToPascalCase(reference.Name)}";
    }

    private static string AdaptAttributeValueToDeclaredCarrier(
        ExpressReachableRulePlan plan,
        ExpressBoundType sourceType,
        ExpressBoundType targetType,
        string source)
    {
        if (targetType is ExpressBoundNamedType
            { Declaration.Kind: not ExpressDeclarationKind.Entity, } targetName
            && sourceType is ExpressBoundNamedType sourceName
            && !ReferenceEquals(sourceName.Declaration, targetName.Declaration))
        {
            return ResolveNamedToSelectValue(plan, targetName, sourceName, source) ?? source;
        }

        return source;
    }

    private static string ResolveNarrowedEntityCarrier(
        ExpressReachableRulePlan plan,
        ExpressBoundType carrierType,
        string carrier,
        ExpressBoundSymbol target,
        int depth = 0)
    {
        while (carrierType is ExpressBoundNamedType namedCarrier
               && namedCarrier.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            var underlying = plan.Resolver.GetDefinedType(namedCarrier.Declaration).UnderlyingType;
            if (underlying is ExpressBoundSelectType)
            {
                carrierType = underlying;
                break;
            }

            carrier = $"({carrier}).Value";
            carrierType = underlying;
        }

        var targetType = ExpressExpressionEmitter.BoundTypeName(new ExpressBoundNamedType(
            target,
            carrierType.Span));
        if (carrierType is ExpressBoundSelectType select)
        {
            var branches = plan.Resolver.GetSelectAlternatives(select)
                .Select((alternative, index) =>
                {
                    var variable = "__expressNarrowedCarrier"
                        + depth.ToString(CultureInfo.InvariantCulture)
                        + "_"
                        + index.ToString(CultureInfo.InvariantCulture);
                    var value = ResolveNarrowedEntityCarrier(
                        plan,
                        new ExpressBoundNamedType(alternative, select.Span),
                        variable,
                        target,
                        depth + 1);
                    return $"{variable} => {value}";
                });
            return $"({carrier}).Match<{targetType}>({string.Join(", ", branches)})";
        }

        if (carrierType is ExpressBoundNamedType
            { Declaration.Kind: ExpressDeclarationKind.Entity, } entityCarrier)
        {
            if (ReferenceEquals(entityCarrier.Declaration, target)
                || plan.EntityProjections.Single(projection =>
                        projection.Entity.Symbol == entityCarrier.Declaration)
                    .PhysicalComponents.Any(component => component.Symbol == target))
            {
                return carrier;
            }

            var variable = "__expressNarrowedCarrier"
                + depth.ToString(CultureInfo.InvariantCulture);
            return $"((object)({carrier})) switch {{ {targetType} {variable} => {variable}, "
                + "_ => throw new global::System.InvalidOperationException() }";
        }

        return "throw new global::System.InvalidOperationException()";
    }

    private static bool ResolvesToSelectType(
        ExpressReachableRulePlan plan,
        ExpressBoundNamedType type)
    {
        var visited = new HashSet<ExpressBoundSymbol>();
        ExpressBoundType current = type;
        while (current is ExpressBoundNamedType named
               && named.Declaration.Kind != ExpressDeclarationKind.Entity
               && visited.Add(named.Declaration))
        {
            current = plan.Resolver.GetDefinedType(named.Declaration).UnderlyingType;
            if (current is ExpressBoundSelectType)
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveExplicitAttribute(
        ExpressReachableRulePlan plan,
        ExpressEntityProjection? sourceProjection,
        ExpressBoundAttribute attribute,
        string source,
        string populationExpression,
        ExpressBoundAttribute? currentDerivedAttribute)
    {
        var property = ExpressEntityProjection.ToPascalCase(attribute.Name);
        var owner = plan.GetAttributeOwner(attribute);
        var ownerType = "global::TedToolkit.Step21.Generated."
            + ExpressEntityProjection.ToPascalCase(owner.Symbol.DeclaringSchema.Name)
            + ".I"
            + ExpressEntityProjection.ToPascalCase(owner.Name);
        var explicitAccess = $"(({ownerType})({source})).{property}";
        var derivedOverrides = GetDerivedOverrides(
            plan,
            sourceProjection,
            attribute,
            currentDerivedAttribute);
        if (derivedOverrides.Length == 0)
        {
            return explicitAccess;
        }

        var cases = derivedOverrides.Select((item, index) =>
        {
            var ownerType = "global::TedToolkit.Step21.Generated."
                + ExpressEntityProjection.ToPascalCase(item.Owner.Symbol.DeclaringSchema.Name)
                + ".I"
                + ExpressEntityProjection.ToPascalCase(item.Owner.Name);
            var variable = $"__derived{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            var value = $"{DerivedMethodName(item.Owner, item.Attribute)}("
                + $"{variable}, {populationExpression}{ValidationContextArgumentSuffix(plan)})";
            value = AdaptAttributeValueToDeclaredCarrier(
                plan,
                item.Attribute.Type,
                attribute.Type,
                value);
            return $"{ownerType} {variable} => {value}";
        });
        return $"((object)({source})) switch {{ {string.Join(", ", cases)}, _ => {explicitAccess} }}";
    }

    private static ExpressEntityProjection? GetSourceProjection(
        ExpressReachableRulePlan plan,
        ExpressBoundExpression? sourceExpression)
    {
        while (sourceExpression?.Kind == ExpressExpressionKind.GroupQualifier)
        {
            sourceExpression = sourceExpression.Children.Count == 0
                ? null
                : sourceExpression.Children[0];
        }

        var namedSource = sourceExpression?.Type.DeclaredType as ExpressBoundNamedType;
        return namedSource?.Declaration.Kind == ExpressDeclarationKind.Entity
            ? plan.EntityProjections.SingleOrDefault(projection =>
                ReferenceEquals(projection.Entity.Symbol, namedSource.Declaration))
            : null;
    }

    private static (ExpressBoundEntity Owner, ExpressBoundAttribute Attribute)[] GetDerivedOverrides(
        ExpressReachableRulePlan plan,
        ExpressEntityProjection? sourceProjection,
        ExpressBoundAttribute attribute,
        ExpressBoundAttribute? excludedAttribute)
    {
        if (sourceProjection is null)
        {
            return Array.Empty<(ExpressBoundEntity Owner, ExpressBoundAttribute Attribute)>();
        }

        return plan.EntityProjections
            .Where(projection => projection.DerivedRedeclaredAttributes.Any(candidate =>
                    ReferenceEquals(candidate.Attribute, attribute))
                && (sourceProjection.PhysicalComponents.Contains(projection.Entity)
                    || plan.ComplexEntityProjections.Any(complex =>
                        complex.Leaves.Any(leaf => leaf.PhysicalComponents.Contains(sourceProjection.Entity))
                        && complex.Leaves.Any(leaf => leaf.PhysicalComponents.Contains(projection.Entity)))))
            .SelectMany(projection => projection.Entity.Attributes
                .Where(candidate => candidate.Kind == ExpressAttributeKind.Derived
                    && !ReferenceEquals(candidate, excludedAttribute)
                    && StringComparer.OrdinalIgnoreCase.Equals(candidate.Name, attribute.Name))
                .Select(candidate => (Owner: projection.Entity, Attribute: candidate)))
            .OrderByDescending(item => plan.EntityProjections.Single(projection =>
                    ReferenceEquals(projection.Entity, item.Owner))
                .PhysicalComponents.Count)
            .ThenBy(item => item.Owner.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ConstantMethodName(ExpressBoundSymbol symbol)
    {
        return $"__ExpressConstant_{ExpressEntityProjection.ToPascalCase(symbol.Name)}";
    }

    private static string FunctionMethodName(
        ExpressReachableRulePlan plan,
        ExpressBoundSymbol symbol)
    {
        var name = $"__ExpressFunction_{ExpressEntityProjection.ToPascalCase(symbol.Name)}";
        return plan.Schema.NestedDeclarations.Any(declaration => ReferenceEquals(declaration.Symbol, symbol))
            ? name
                + "_"
                + symbol.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                + "_"
                + symbol.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
            : name;
    }

    private static string ProcedureMethodName(
        ExpressReachableRulePlan plan,
        ExpressBoundSymbol symbol)
    {
        var name = $"__ExpressProcedure_{ExpressEntityProjection.ToPascalCase(symbol.Name)}";
        return plan.Schema.NestedDeclarations.Any(declaration => ReferenceEquals(declaration.Symbol, symbol))
            ? name
                + "_"
                + symbol.Span.Start.Line.ToString(CultureInfo.InvariantCulture)
                + "_"
                + symbol.Span.Start.Column.ToString(CultureInfo.InvariantCulture)
            : name;
    }

    private static string DerivedMethodName(
        ExpressBoundEntity owner,
        ExpressBoundAttribute attribute)
    {
        return "__ExpressDerived_"
            + ExpressEntityProjection.ToPascalCase(owner.Name)
            + "_"
            + ExpressEntityProjection.ToPascalCase(attribute.Name);
    }

    private static string InverseMethodName(
        ExpressBoundEntity owner,
        ExpressBoundAttribute attribute)
    {
        return "__ExpressInverse_"
            + ExpressEntityProjection.ToPascalCase(owner.Name)
            + "_"
            + ExpressEntityProjection.ToPascalCase(attribute.Name);
    }

    private static string ParameterName(string name)
    {
        return $"__parameter_{ExpressEntityProjection.ToPascalCase(name)}";
    }

    private static bool SameStart(ExpressSourceSpan left, ExpressSourceSpan right)
    {
        return string.Equals(left.Start.FilePath, right.Start.FilePath, StringComparison.Ordinal)
            && left.Start.Line == right.Start.Line
            && left.Start.Column == right.Start.Column;
    }

    private static string StableFilePath(string value)
    {
        var normalized = value.Replace('\\', '/');
        var separator = normalized.LastIndexOf('/');
        return separator < 0 ? normalized : normalized.Substring(separator + 1);
    }

    private static string Literal(string value)
    {
        return SymbolDisplay.FormatLiteral(value, quote: true);
    }

    private static LoopTransfers? CreateLoopTransfers(
        ExpressSemanticRule repeat,
        Func<string, string> allocateTemporaryName)
    {
        static bool HasTransfer(ExpressSemanticRule rule)
        {
            return rule.Role != "repeatStmt"
                && (rule.Role is "escapeStmt" or "skipStmt" || rule.ChildRules().Any(HasTransfer));
        }

        return repeat.ChildRules("stmt").Any(HasTransfer)
            ? new(allocateTemporaryName("__expressRepeatTail"))
            : null;
    }

    private static bool MergeLoopPaths(
        ExpressReachableRulePlan plan,
        IReadOnlyList<LoopFlowFacts> paths,
        bool hasCurrentPath,
        List<KeyValuePair<ExpressBoundName, ExpressBoundName>>? safeIndices,
        IDictionary<ExpressBoundName, ExpressBoundName>? sizeAliases,
        Dictionary<ExpressBoundName, ExpressBoundSymbol>? selectNarrowings,
        List<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>? pathNarrowings,
        ISet<ExpressBoundName>? determinateLexicals,
        List<KeyValuePair<ExpressBoundExpression, ExpressBoundName>>? safeIndexPaths,
        Dictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>? scalarNarrowings)
    {
        foreach (var path in paths)
        {
            var current = hasCurrentPath
                ? new LoopFlowFacts(safeIndices, sizeAliases, selectNarrowings, pathNarrowings,
                    determinateLexicals, safeIndexPaths, scalarNarrowings)
                : path;
            IntersectDictionaryFacts(scalarNarrowings,
                static (left, right) => StringComparer.Ordinal.Equals(left.StorageCode, right.StorageCode)
                    && StringComparer.Ordinal.Equals(left.Code, right.Code) && ReferenceEquals(left.Type, right.Type),
                current.Scalars, path.Scalars);
            IntersectCollectionFacts(safeIndices,
                static (left, right) => ReferenceEquals(left.Key, right.Key) && ReferenceEquals(left.Value, right.Value),
                current.Indices, path.Indices);
            IntersectDictionaryFacts(sizeAliases, static (left, right) => ReferenceEquals(left, right),
                current.Aliases, path.Aliases);
            JoinSelectFacts(selectNarrowings, static (left, right) => ReferenceEquals(left, right),
                key => DeclaredSelect(key.Type), current.Selects, path.Selects);
            JoinSelectFacts(pathNarrowings, SameDirectReferencePath,
                key => DeclaredSelect(key.Type.DeclaredType), current.Paths, path.Paths);
            IntersectCollectionFacts(determinateLexicals, static (left, right) => ReferenceEquals(left, right),
                current.Determinate, path.Determinate);
            IntersectCollectionFacts(safeIndexPaths,
                static (left, right) => ReferenceEquals(left.Key, right.Key) && ReferenceEquals(left.Value, right.Value),
                current.IndexPaths, path.IndexPaths);
            hasCurrentPath = true;
        }

        return hasCurrentPath;

        ExpressBoundSymbol? DeclaredSelect(ExpressBoundType? type)
        {
            return type is ExpressBoundNamedType { Declaration.Kind: not ExpressDeclarationKind.Entity, } named
                && plan.Resolver.GetDefinedType(named.Declaration).UnderlyingType is ExpressBoundSelectType
                    ? named.Declaration
                    : null;
        }
    }

    /// <summary>
    /// Owns one shared generated value comparer per named SELECT used by entity equality.
    /// </summary>
    private sealed class SelectValueEqualityHelpers
    {
        private readonly ExpressReachableRulePlan _plan;

        private readonly ExpressGeneratedTypeResolver _resolver;

        private readonly ExpressDescriptorShards _shards;

        private readonly List<Method> _methods;

        private readonly Dictionary<ExpressBoundSymbol, string> _calls = [];

        /// <summary>
        /// Initializes a shared SELECT comparer owner for one generated descriptor.
        /// </summary>
        /// <param name="plan">The validated reachability plan.</param>
        /// <param name="resolver">The generated value-type resolver.</param>
        /// <param name="shards">The structural descriptor partitions.</param>
        /// <param name="methods">The unsharded descriptor methods.</param>
        internal SelectValueEqualityHelpers(
            ExpressReachableRulePlan plan,
            ExpressGeneratedTypeResolver resolver,
            ExpressDescriptorShards shards,
            List<Method> methods)
        {
            _plan = plan;
            _resolver = resolver;
            _shards = shards;
            _methods = methods;
        }

        /// <summary>
        /// Creates a shallow call to the shared comparer for one named SELECT.
        /// </summary>
        /// <param name="named">The named SELECT type.</param>
        /// <param name="select">The resolved SELECT domain.</param>
        /// <param name="left">The left generated value expression.</param>
        /// <param name="right">The right generated value expression.</param>
        /// <param name="activePairs">The active entity-pair expression.</param>
        /// <returns>The generated comparer call.</returns>
        internal string CreateCall(
            ExpressBoundNamedType named,
            ExpressBoundSelectType select,
            string left,
            string right,
            string activePairs)
        {
            if (!_calls.TryGetValue(named.Declaration, out var callName))
            {
                var ordinal = _calls.Count;
                var suffix = ExpressEntityProjection.ToPascalCase(named.Declaration.Name)
                    + ordinal.ToString(CultureInfo.InvariantCulture);
                var methodName = "__ExpressSelectValueEquals" + suffix;
                var shardName = "__ExpressSelectValueEqualityShard"
                    + (ordinal / SELECT_VALUE_EQUALITY_METHODS_PER_SHARD)
                        .ToString(CultureInfo.InvariantCulture);
                callName = _shards.Qualify(methodName, shardName);
                _calls.Add(named.Declaration, callName);

                var method = CreateMethod(
                    methodName,
                    new DataType("global::TedToolkit.Step21.LogicalValue"));
                var typeName = ExpressExpressionEmitter.BoundTypeName(named);
                method.AddParameter(SourceComposer.Parameter(new DataType(typeName), "left"));
                method.AddParameter(SourceComposer.Parameter(new DataType(typeName), "right"));
                method.AddParameter(SourceComposer.Parameter(
                    new DataType(
                        "global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<"
                        + "global::TedToolkit.Step21.Entity, global::TedToolkit.Step21.Entity>>"),
                    "activePairs"));
                method.AddStatement(new IfStatement(new CustomExpression("left.Kind != right.Kind"))
                    .AddStatement(new CustomExpression(
                        "global::TedToolkit.Step21.LogicalValue.False").Return));

                var alternatives = _plan.Resolver.GetSelectAlternatives(select);
                for (var index = 0; index < alternatives.Count; index++)
                {
                    var alternative = alternatives[index];
                    var indexText = index.ToString(CultureInfo.InvariantCulture);
                    var alternativeName = ExpressEntityProjection.ToPascalCase(alternative.Name);
                    var leftName = "selectedLeft" + indexText;
                    var rightName = "selectedRight" + indexText;
                    var equality = CreateBoundValueEquality(
                        _plan,
                        _resolver,
                        new ExpressBoundNamedType(alternative, select.Span),
                        leftName,
                        rightName + "!",
                        "activePairs",
                        selectValueEqualityHelpers: this);
                    method.AddStatement(new IfStatement(new CustomExpression(
                            $"left.TryGet{alternativeName}(out var {leftName}) "
                            + $"&& right.TryGet{alternativeName}(out var {rightName})"))
                        .AddStatement(new CustomExpression(equality).Return));
                }

                method.AddStatement(new CustomExpression(
                    "global::TedToolkit.Step21.LogicalValue.False").Return);
                AddSummary(method, "Compares one named SELECT value through its selected typed alternative.");
                if (_shards.IsEnabled)
                {
                    _shards.Add(method, shardName);
                }
                else
                {
                    _methods.Add(method);
                }
            }

            return $"{callName}(({left}), ({right}), {activePairs})";
        }
    }

    /// <summary>
    /// Owns transfers and their incoming facts within one generated whole-loop fragment.
    /// </summary>
    /// <param name="skipLabel">The nearest loop's post-body control target.</param>
    private sealed class LoopTransfers(string skipLabel)
    {
        internal string SkipLabel { get; } = skipLabel;

        internal List<LoopFlowFacts> SkipPaths { get; } = [];

        internal List<LoopFlowFacts> ExitPaths { get; } = [];
    }

    /// <summary>
    /// Captures branch facts before a transfer bypasses the normal statement-flow joins.
    /// </summary>
    private sealed class LoopFlowFacts
    {
        internal LoopFlowFacts(
            List<KeyValuePair<ExpressBoundName, ExpressBoundName>>? indices,
            IDictionary<ExpressBoundName, ExpressBoundName>? aliases,
            Dictionary<ExpressBoundName, ExpressBoundSymbol>? selects,
            List<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>>? paths,
            ISet<ExpressBoundName>? determinate,
            List<KeyValuePair<ExpressBoundExpression, ExpressBoundName>>? indexPaths,
            Dictionary<string, (string StorageCode, string Code, ExpressBoundType Type)>? scalars)
        {
            Indices = indices?.ToList() ?? [];
            Aliases = aliases?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [];
            Selects = selects?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [];
            Paths = paths?.ToList() ?? [];
            Determinate = determinate?.ToArray() ?? Array.Empty<ExpressBoundName>();
            IndexPaths = indexPaths?.ToList() ?? [];
            Scalars = scalars?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
                ?? new(StringComparer.OrdinalIgnoreCase);
        }

        internal List<KeyValuePair<ExpressBoundName, ExpressBoundName>> Indices { get; }

        internal Dictionary<ExpressBoundName, ExpressBoundName> Aliases { get; }

        internal Dictionary<ExpressBoundName, ExpressBoundSymbol> Selects { get; }

        internal List<KeyValuePair<ExpressBoundExpression, ExpressBoundSymbol>> Paths { get; }

        internal ExpressBoundName[] Determinate { get; }

        internal List<KeyValuePair<ExpressBoundExpression, ExpressBoundName>> IndexPaths { get; }

        internal Dictionary<string, (string StorageCode, string Code, ExpressBoundType Type)> Scalars { get; }
    }

    private static void AddSummary(IRootDescription target, string text)
    {
        target.AddRootDescription(new DescriptionSummary(
            new IDescriptionItem[] { new DescriptionText(text), }));
    }
}