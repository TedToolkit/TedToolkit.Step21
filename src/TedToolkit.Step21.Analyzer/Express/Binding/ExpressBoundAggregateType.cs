// -----------------------------------------------------------------------
// <copyright file="ExpressBoundAggregateType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents an EXPRESS aggregate while retaining category and structural modifiers.
/// </summary>
internal sealed class ExpressBoundAggregateType : ExpressBoundType
{
    /// <summary>
    /// Initializes a bound aggregate type.
    /// </summary>
    /// <param name="kind">The aggregate category.</param>
    /// <param name="elementType">The recursively bound element type.</param>
    /// <param name="lowerBoundText">The optional lower-bound source text.</param>
    /// <param name="upperBoundText">The optional upper-bound source text.</param>
    /// <param name="isOptional">Whether ARRAY elements are optional.</param>
    /// <param name="isUnique">Whether ARRAY or LIST elements are unique.</param>
    /// <param name="typeLabel">The optional general AGGREGATE type label.</param>
    /// <param name="span">The complete aggregate span.</param>
    /// <param name="resolvedLowerBoundText">The statically evaluated lower bound, when available.</param>
    /// <param name="resolvedUpperBoundText">The statically evaluated upper bound, when available.</param>
    internal ExpressBoundAggregateType(
        ExpressAggregateKind kind,
        ExpressBoundType elementType,
        string? lowerBoundText,
        string? upperBoundText,
        bool isOptional,
        bool isUnique,
        string? typeLabel,
        ExpressSourceSpan span,
        string? resolvedLowerBoundText = null,
        string? resolvedUpperBoundText = null)
        : base(span)
    {
        Kind = kind;
        ElementType = elementType;
        LowerBoundText = lowerBoundText;
        UpperBoundText = upperBoundText;
        IsOptional = isOptional;
        IsUnique = isUnique;
        TypeLabel = typeLabel;
        ResolvedLowerBoundText = resolvedLowerBoundText;
        ResolvedUpperBoundText = resolvedUpperBoundText;
    }

    /// <summary>
    /// Gets the aggregate category.
    /// </summary>
    internal ExpressAggregateKind Kind { get; }

    /// <summary>
    /// Gets the recursively bound element type.
    /// </summary>
    internal ExpressBoundType ElementType { get; }

    /// <summary>
    /// Gets the optional lower-bound source text.
    /// </summary>
    internal string? LowerBoundText { get; }

    /// <summary>
    /// Gets the optional upper-bound source text.
    /// </summary>
    internal string? UpperBoundText { get; }

    /// <summary>
    /// Gets the statically evaluated lower-bound text used by generated execution, when available.
    /// </summary>
    internal string? ResolvedLowerBoundText { get; }

    /// <summary>
    /// Gets the statically evaluated upper-bound text used by generated execution, when available.
    /// </summary>
    internal string? ResolvedUpperBoundText { get; }

    /// <summary>
    /// Gets a value indicating whether ARRAY elements are optional.
    /// </summary>
    internal bool IsOptional { get; }

    /// <summary>
    /// Gets a value indicating whether ARRAY or LIST elements are unique.
    /// </summary>
    internal bool IsUnique { get; }

    /// <summary>
    /// Gets the optional general AGGREGATE type label.
    /// </summary>
    internal string? TypeLabel { get; }
}