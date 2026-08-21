// -----------------------------------------------------------------------
// <copyright file="ExpressBoundGenericType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents a GENERIC or GENERIC_ENTITY parameter type.
/// </summary>
internal sealed class ExpressBoundGenericType : ExpressBoundType
{
    /// <summary>
    /// Initializes a bound generic type.
    /// </summary>
    /// <param name="isEntity">Whether the source type is GENERIC_ENTITY.</param>
    /// <param name="typeLabel">The optional type label.</param>
    /// <param name="span">The complete generic type span.</param>
    internal ExpressBoundGenericType(bool isEntity, string? typeLabel, ExpressSourceSpan span)
        : base(span)
    {
        IsEntity = isEntity;
        TypeLabel = typeLabel;
    }

    /// <summary>
    /// Gets a value indicating whether the source type is GENERIC_ENTITY.
    /// </summary>
    internal bool IsEntity { get; }

    /// <summary>
    /// Gets the optional type label.
    /// </summary>
    internal string? TypeLabel { get; }
}