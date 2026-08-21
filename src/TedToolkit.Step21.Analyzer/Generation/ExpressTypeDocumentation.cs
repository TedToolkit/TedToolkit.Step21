// -----------------------------------------------------------------------
// <copyright file="ExpressTypeDocumentation.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Formats retained bound EXPRESS types for deterministic generated documentation.
/// </summary>
internal static class ExpressTypeDocumentation
{
    /// <summary>
    /// Formats one retained type expression without changing its semantics.
    /// </summary>
    /// <param name="type">The bound type to format.</param>
    /// <returns>The normalized EXPRESS type expression.</returns>
    /// <exception cref="ArgumentException"><paramref name="type"/> is not a supported generated value type.</exception>
    internal static string Format(ExpressBoundType type)
    {
        return type switch
        {
            ExpressBoundAggregateType aggregate => FormatAggregate(aggregate),
            ExpressBoundNamedType named => named.Declaration.Name,
            ExpressBoundScalarType scalar => FormatScalar(scalar),
            _ => throw new ArgumentException("The bound type cannot be represented in generated documentation.", nameof(type)),
        };
    }

    private static string FormatAggregate(ExpressBoundAggregateType aggregate)
    {
        var category = aggregate.Kind.ToString().ToUpperInvariant();
        var label = aggregate.Kind == ExpressAggregateKind.Aggregate && aggregate.TypeLabel is not null
            ? $" : {aggregate.TypeLabel}"
            : "";
        var bounds = aggregate.LowerBoundText is not null && aggregate.UpperBoundText is not null
            ? $" [{aggregate.LowerBoundText}:{aggregate.UpperBoundText}]"
            : "";
        var optional = aggregate.IsOptional ? "OPTIONAL " : "";
        var unique = aggregate.IsUnique ? "UNIQUE " : "";
        return $"{category}{label}{bounds} OF {optional}{unique}{Format(aggregate.ElementType)}";
    }

    private static string FormatScalar(ExpressBoundScalarType scalar)
    {
        var category = scalar.Kind.ToString().ToUpperInvariant();
        var constraint = scalar.ConstraintText is null ? "" : $"({scalar.ConstraintText})";
        var fixedWidth = scalar.IsFixed ? " FIXED" : "";
        return category + constraint + fixedWidth;
    }
}