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
        var nextName = 0;
        return CreateValue(type, valueExpression, resolver, ref nextName);
    }

    private static bool ContainsEntityReference(
        ExpressBoundType type,
        ExpressGeneratedTypeResolver resolver)
    {
        switch (type)
        {
            case ExpressBoundNamedType { Declaration.Kind: ExpressDeclarationKind.Entity, }:
                return true;

            case ExpressBoundNamedType named:
                return ContainsEntityReference(
                    resolver.GetDefinedType(named.Declaration).UnderlyingType,
                    resolver);

            case ExpressBoundAggregateType aggregate:
                return ContainsEntityReference(aggregate.ElementType, resolver);

            case ExpressBoundSelectType select:
                return resolver.GetSelectAlternatives(select).Any(alternative =>
                    alternative.Kind == ExpressDeclarationKind.Entity
                    || ContainsEntityReference(
                        resolver.GetDefinedType(alternative).UnderlyingType,
                        resolver));

            default:
                return false;
        }
    }

    private static string CreateValue(
        ExpressBoundType type,
        string value,
        ExpressGeneratedTypeResolver resolver,
        ref int nextName)
    {
        switch (type)
        {
            case ExpressBoundNamedType { Declaration.Kind: ExpressDeclarationKind.Entity, }:
                return "global::System.Linq.Enumerable.OfType<global::TedToolkit.Step21.Entity>("
                    + $"new global::TedToolkit.Step21.Entity?[] {{ {value} as global::TedToolkit.Step21.Entity }})";

            case ExpressBoundNamedType named:
                var underlying = resolver.GetDefinedType(named.Declaration).UnderlyingType;
                return underlying is ExpressBoundSelectType select
                    ? CreateSelect(select, value, resolver, ref nextName)
                    : CreateValue(underlying, $"{value}.Value", resolver, ref nextName);

            case ExpressBoundAggregateType aggregate:
                var itemName = $"directReferenceValue{nextName.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                nextName++;
                var items = CreateValue(aggregate.ElementType, itemName, resolver, ref nextName);
                return $"{value} is null ? {EMPTY} : global::System.Linq.Enumerable.SelectMany({value}, {itemName} => {items})";

            default:
                return EMPTY;
        }
    }

    private static string CreateSelect(
        ExpressBoundSelectType select,
        string value,
        ExpressGeneratedTypeResolver resolver,
        ref int nextName)
    {
        var handlers = new List<string>();
        foreach (var alternative in resolver.GetSelectAlternatives(select))
        {
            var selectedName =
                $"directReferenceValue{nextName.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            nextName++;
            var selectedType = new ExpressBoundNamedType(alternative, alternative.Span);
            handlers.Add($"{selectedName} => {CreateValue(selectedType, selectedName, resolver, ref nextName)}");
        }

        return $"{value} is null ? {EMPTY} : {value}.Match({string.Join(", ", handlers)})";
    }
}