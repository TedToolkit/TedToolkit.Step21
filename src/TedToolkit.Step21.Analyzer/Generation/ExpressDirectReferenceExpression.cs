// -----------------------------------------------------------------------
// <copyright file="ExpressDirectReferenceExpression.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Composes reflection-free generated expressions for one-level physical entity occurrences.
/// </summary>
internal static class ExpressDirectReferenceExpression
{
    private const string EMPTY =
        "global::System.Array.Empty<global::TedToolkit.Step21.Entity>()";

    /// <summary>
    /// Creates a deferred expression that re-reads every reference-bearing physical attribute on each enumeration.
    /// </summary>
    /// <param name="projection">The generated entity projection.</param>
    /// <param name="resolver">The closed-set generated value resolver.</param>
    /// <returns>The generated expression.</returns>
    internal static string Create(
        ExpressEntityProjection projection,
        ExpressGeneratedTypeResolver resolver)
    {
        return Create(projection.EffectiveAttributes, resolver);
    }

    /// <summary>
    /// Creates a deferred expression for an explicit ordered physical attribute set.
    /// </summary>
    /// <param name="attributes">The physical attributes.</param>
    /// <param name="resolver">The closed-set generated value resolver.</param>
    /// <returns>The generated expression.</returns>
    internal static string Create(
        IEnumerable<ExpressEntityAttributeProjection> attributes,
        ExpressGeneratedTypeResolver resolver)
    {
        var nextName = 0;
        var attributeExpressions = new List<string>();
        foreach (var attribute in attributes.Where(
                     attribute => ContainsEntityReference(attribute.Type, resolver)))
        {
            attributeExpressions.Add(CreateValue(
                attribute.Type,
                attribute.StorageMemberName,
                resolver,
                ref nextName));
        }

        if (attributeExpressions.Count == 0)
        {
            return EMPTY;
        }

        var combined = attributeExpressions.Aggregate(
            EMPTY,
            (current, next) =>
                $"global::System.Linq.Enumerable.Concat({current}, {next})");
        return "global::System.Linq.Enumerable.SelectMany("
            + "global::System.Linq.Enumerable.Repeat(0, 1), "
            + $"_ => {combined})";
    }

    /// <summary>
    /// Creates a deferred expression for one reference-bearing value with a caller-supplied generated spelling.
    /// </summary>
    /// <param name="type">The bound value type.</param>
    /// <param name="valueExpression">The generated expression that reads the value.</param>
    /// <param name="resolver">The closed-set generated value resolver.</param>
    /// <returns>The generated one-level entity-reference sequence.</returns>
    internal static string Create(
        ExpressBoundType type,
        string valueExpression,
        ExpressGeneratedTypeResolver resolver)
    {
        if (!ContainsEntityReference(type, resolver))
        {
            return EMPTY;
        }

        var nextName = 0;
        return CreateValue(
            type,
            valueExpression,
            resolver,
            ref nextName,
            new Dictionary<ExpressBoundSymbol, string>());
    }

    private static bool ContainsEntityReference(
        ExpressBoundType type,
        ExpressGeneratedTypeResolver resolver)
    {
        return ContainsEntityReference(type, resolver, new HashSet<ExpressBoundSymbol>());
    }

    private static bool ContainsEntityReference(
        ExpressBoundType type,
        ExpressGeneratedTypeResolver resolver,
        ISet<ExpressBoundSymbol> activeNamedTypes)
    {
        switch (type)
        {
            case ExpressBoundNamedType { Declaration.Kind: ExpressDeclarationKind.Entity, }:
                return true;

            case ExpressBoundNamedType named:
                if (!activeNamedTypes.Add(named.Declaration))
                {
                    return false;
                }

                var containsNamedReference = ContainsEntityReference(
                    resolver.GetDefinedType(named.Declaration).UnderlyingType,
                    resolver,
                    activeNamedTypes);
                activeNamedTypes.Remove(named.Declaration);
                return containsNamedReference;

            case ExpressBoundAggregateType aggregate:
                return ContainsEntityReference(aggregate.ElementType, resolver, activeNamedTypes);

            case ExpressBoundSelectType select:
                return resolver.GetSelectAlternatives(select).Any(alternative =>
                    alternative.Kind == ExpressDeclarationKind.Entity
                    || ContainsEntityReference(
                        new ExpressBoundNamedType(alternative, alternative.Span),
                        resolver,
                        activeNamedTypes));

            default:
                return false;
        }
    }

    private static string CreateValue(
        ExpressBoundType type,
        string value,
        ExpressGeneratedTypeResolver resolver,
        ref int nextName,
        IDictionary<ExpressBoundSymbol, string>? recursiveFunctions = null)
    {
        recursiveFunctions ??= new Dictionary<ExpressBoundSymbol, string>();
        switch (type)
        {
            case ExpressBoundNamedType { Declaration.Kind: ExpressDeclarationKind.Entity, }:
                return "global::System.Linq.Enumerable.OfType<global::TedToolkit.Step21.Entity>("
                    + $"new global::TedToolkit.Step21.Entity?[] {{ {value} as global::TedToolkit.Step21.Entity }})";

            case ExpressBoundNamedType named:
                if (recursiveFunctions.TryGetValue(named.Declaration, out var recursiveFunction))
                {
                    return $"{recursiveFunction}({value})";
                }

                var underlying = resolver.GetDefinedType(named.Declaration).UnderlyingType;
                if (IsRecursive(named, resolver))
                {
                    var rootName = $"directReferenceRoot{nextName.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                    nextName++;
                    var currentName = $"directReferenceCurrent{nextName.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                    nextName++;
                    var functionName = $"DirectReferenceWalk{nextName.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                    nextName++;
                    recursiveFunctions.Add(named.Declaration, functionName);
                    var recursiveBody = underlying is ExpressBoundSelectType recursiveSelect
                        ? CreateSelect(
                            recursiveSelect,
                            currentName,
                            resolver,
                            ref nextName,
                            recursiveFunctions)
                        : CreateValue(
                            underlying,
                            $"{currentName}.Value",
                            resolver,
                            ref nextName,
                            recursiveFunctions);
                    recursiveFunctions.Remove(named.Declaration);
                    var namedType = ExpressExpressionEmitter.BoundTypeName(named);
                    return $"((global::System.Func<{namedType}, global::System.Collections.Generic.IEnumerable<"
                        + $"global::TedToolkit.Step21.Entity>>)(({rootName}) => {{ "
                        + "global::System.Collections.Generic.IEnumerable<global::TedToolkit.Step21.Entity> "
                        + $"{functionName}({namedType} {currentName}) {{ return {recursiveBody}; }} "
                        + $"return {functionName}({rootName}); }}))({value})";
                }

                return underlying is ExpressBoundSelectType select
                    ? CreateSelect(select, value, resolver, ref nextName, recursiveFunctions)
                    : CreateDefinedValue(
                        named,
                        underlying,
                        value,
                        resolver,
                        ref nextName,
                        recursiveFunctions);

            case ExpressBoundAggregateType aggregate:
                var itemName = $"directReferenceValue{nextName.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                nextName++;
                var items = CreateValue(
                    aggregate.ElementType,
                    itemName,
                    resolver,
                    ref nextName,
                    recursiveFunctions);
                return $"{value} is null ? {EMPTY} : global::System.Linq.Enumerable.SelectMany({value}, {itemName} => {items})";

            default:
                return EMPTY;
        }
    }

    private static string CreateDefinedValue(
        ExpressBoundNamedType named,
        ExpressBoundType underlying,
        string value,
        ExpressGeneratedTypeResolver resolver,
        ref int nextName,
        IDictionary<ExpressBoundSymbol, string> recursiveFunctions)
    {
        var presentName =
            $"directReferenceValue{nextName.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        nextName++;
        var presentValue = CreateValue(
            underlying,
            $"{presentName}.Value",
            resolver,
            ref nextName,
            recursiveFunctions);
        var namedType = ExpressExpressionEmitter.BoundTypeName(named);
        return $"(({namedType}?)({value})) switch {{ {{ }} {presentName} => {presentValue}, _ => {EMPTY} }}";
    }

    private static string CreateSelect(
        ExpressBoundSelectType select,
        string value,
        ExpressGeneratedTypeResolver resolver,
        ref int nextName,
        IDictionary<ExpressBoundSymbol, string> recursiveFunctions)
    {
        var handlers = new List<string>();
        foreach (var alternative in resolver.GetSelectAlternatives(select))
        {
            var selectedName =
                $"directReferenceValue{nextName.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            nextName++;
            var selectedType = new ExpressBoundNamedType(alternative, alternative.Span);
            handlers.Add($"{selectedName} => {CreateValue(
                selectedType,
                selectedName,
                resolver,
                ref nextName,
                recursiveFunctions)}");
        }

        return $"{value} is null ? {EMPTY} : {value}.Match({string.Join(", ", handlers)})";
    }

    private static bool IsRecursive(
        ExpressBoundNamedType named,
        ExpressGeneratedTypeResolver resolver)
    {
        return References(
            resolver.GetDefinedType(named.Declaration).UnderlyingType,
            named.Declaration,
            resolver,
            new HashSet<ExpressBoundSymbol>());
    }

    private static bool References(
        ExpressBoundType type,
        ExpressBoundSymbol target,
        ExpressGeneratedTypeResolver resolver,
        ISet<ExpressBoundSymbol> visited)
    {
        switch (type)
        {
            case ExpressBoundAggregateType aggregate:
                return References(aggregate.ElementType, target, resolver, visited);

            case ExpressBoundNamedType { Declaration.Kind: ExpressDeclarationKind.Entity, }:
                return false;

            case ExpressBoundNamedType named when ReferenceEquals(named.Declaration, target):
                return true;

            case ExpressBoundNamedType named when visited.Add(named.Declaration):
                return References(
                    resolver.GetDefinedType(named.Declaration).UnderlyingType,
                    target,
                    resolver,
                    visited);

            case ExpressBoundSelectType select:
                return resolver.GetSelectAlternatives(select).Any(alternative => References(
                    new ExpressBoundNamedType(alternative, alternative.Span),
                    target,
                    resolver,
                    visited));

            default:
                return false;
        }
    }
}