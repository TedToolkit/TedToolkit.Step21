// -----------------------------------------------------------------------
// <copyright file="ExpressBoundScalarType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents one EXPRESS simple type and its retained width or precision expression.
/// </summary>
internal sealed class ExpressBoundScalarType : ExpressBoundType
{
    /// <summary>
    /// Initializes a bound simple type.
    /// </summary>
    /// <param name="kind">The simple type category.</param>
    /// <param name="constraintText">The optional width or precision source text.</param>
    /// <param name="isFixed">Whether a BINARY or STRING width is FIXED.</param>
    /// <param name="span">The complete type span.</param>
    internal ExpressBoundScalarType(
        ExpressScalarKind kind,
        string? constraintText,
        bool isFixed,
        ExpressSourceSpan span)
        : base(span)
    {
        Kind = kind;
        ConstraintText = constraintText;
        IsFixed = isFixed;
    }

    /// <summary>
    /// Gets the simple type category.
    /// </summary>
    internal ExpressScalarKind Kind { get; }

    /// <summary>
    /// Gets the optional width or precision source text.
    /// </summary>
    internal string? ConstraintText { get; }

    /// <summary>
    /// Gets a value indicating whether a BINARY or STRING width is FIXED.
    /// </summary>
    internal bool IsFixed { get; }
}