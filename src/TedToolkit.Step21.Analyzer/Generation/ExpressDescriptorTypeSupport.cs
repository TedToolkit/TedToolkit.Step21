// -----------------------------------------------------------------------
// <copyright file="ExpressDescriptorTypeSupport.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Computes descriptor mapping support without depending on source-emission representation.
/// </summary>
internal static class ExpressDescriptorTypeSupport
{
    /// <summary>
    /// Determines whether a bound type can participate in a generated schema descriptor.
    /// </summary>
    /// <param name="type">The bound type.</param>
    /// <param name="resolver">The generated type resolver.</param>
    /// <returns><see langword="true" /> when the type can be mapped; otherwise, <see langword="false" />.</returns>
    internal static bool CanMap(ExpressBoundType type, ExpressGeneratedTypeResolver resolver)
    {
        if (type is ExpressBoundNamedType namedSelect
            && namedSelect.Declaration.Kind != ExpressDeclarationKind.Entity
            && resolver.GetDefinedType(namedSelect.Declaration).UnderlyingType is ExpressBoundSelectType select)
        {
            return resolver.GetSelectAlternatives(select).All(alternative =>
                alternative.Kind == ExpressDeclarationKind.Entity
                || CanMapSelectAlternative(alternative, resolver));
        }

        var terminal = GetTerminalType(type, resolver);
        return terminal is ExpressBoundScalarType or ExpressBoundEnumerationType
            || (terminal is ExpressBoundNamedType named
                && named.Declaration.Kind == ExpressDeclarationKind.Entity)
            || (terminal is ExpressBoundAggregateType aggregate
                && CanMapAggregate(aggregate, resolver));
    }

    /// <summary>
    /// Resolves and validates static generated aggregate bounds.
    /// </summary>
    /// <param name="aggregate">The aggregate type.</param>
    /// <param name="lowerBound">The resolved lower bound.</param>
    /// <param name="upperBound">The resolved optional upper bound.</param>
    /// <returns><see langword="true" /> when the bounds can be represented by generated runtime aggregates.</returns>
    internal static bool TryGetAggregateBounds(
        ExpressBoundAggregateType aggregate,
        out int lowerBound,
        out int? upperBound)
    {
        lowerBound = 0;
        upperBound = null;
        var lowerText = aggregate.ResolvedLowerBoundText ?? aggregate.LowerBoundText;
        var upperText = aggregate.ResolvedUpperBoundText ?? aggregate.UpperBoundText;
        if (lowerText is not null
            && !int.TryParse(
                lowerText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out lowerBound))
        {
            return false;
        }

        if (upperText is not null && upperText != "?")
        {
            if (!int.TryParse(
                upperText,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedUpperBound))
            {
                return false;
            }

            upperBound = parsedUpperBound;
        }

        if (aggregate.Kind == ExpressAggregateKind.Array)
        {
            if (!upperBound.HasValue)
            {
                return false;
            }

            var length = (long)upperBound.Value - lowerBound + 1;
            return length is > 0 and <= int.MaxValue;
        }

        return lowerBound >= 0 && (!upperBound.HasValue || upperBound.Value >= lowerBound);
    }

    /// <summary>
    /// Resolves aliases until a descriptor mapping terminal type is reached.
    /// </summary>
    /// <param name="type">The bound source type.</param>
    /// <param name="resolver">The generated type resolver.</param>
    /// <returns>The mapping terminal type.</returns>
    internal static ExpressBoundType GetTerminalType(
        ExpressBoundType type,
        ExpressGeneratedTypeResolver resolver)
    {
        while (type is ExpressBoundNamedType named
            && named.Declaration.Kind != ExpressDeclarationKind.Entity)
        {
            var underlying = resolver.GetDefinedType(named.Declaration).UnderlyingType;
            if (underlying is ExpressBoundEnumerationType)
            {
                return underlying;
            }

            type = underlying;
        }

        return type;
    }

    private static bool CanMapAggregate(
        ExpressBoundAggregateType aggregate,
        ExpressGeneratedTypeResolver resolver)
    {
        return aggregate.Kind is ExpressAggregateKind.Array
                or ExpressAggregateKind.Bag
                or ExpressAggregateKind.List
                or ExpressAggregateKind.Set
            && TryGetAggregateBounds(aggregate, out _, out _)
            && aggregate.ElementType is not ExpressBoundAggregateType
            && CanMapAggregateElement(aggregate.ElementType, resolver);
    }

    private static bool CanMapAggregateElement(
        ExpressBoundType type,
        ExpressGeneratedTypeResolver resolver)
    {
        if (type is ExpressBoundNamedType named
            && named.Declaration.Kind == ExpressDeclarationKind.Entity)
        {
            return true;
        }

        if (type is ExpressBoundNamedType namedSelect
            && resolver.GetDefinedType(namedSelect.Declaration).UnderlyingType is ExpressBoundSelectType)
        {
            return CanMap(type, resolver);
        }

        var terminal = GetTerminalType(type, resolver);
        return terminal is ExpressBoundScalarType or ExpressBoundEnumerationType;
    }

    private static bool CanMapSelectAlternative(
        ExpressBoundSymbol alternative,
        ExpressGeneratedTypeResolver resolver)
    {
        var underlying = resolver.GetDefinedType(alternative).UnderlyingType;
        if (underlying is ExpressBoundSelectType nestedSelect)
        {
            return resolver.GetSelectAlternatives(nestedSelect)
                .All(nested => nested.Kind == ExpressDeclarationKind.Entity
                    || CanMapSelectAlternative(nested, resolver));
        }

        var terminal = GetTerminalType(underlying, resolver);
        return terminal is ExpressBoundScalarType or ExpressBoundEnumerationType;
    }
}