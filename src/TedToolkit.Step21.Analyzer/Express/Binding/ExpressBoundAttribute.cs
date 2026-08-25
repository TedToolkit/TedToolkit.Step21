// -----------------------------------------------------------------------
// <copyright file="ExpressBoundAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents one source-located entity attribute with its resolved type.
/// </summary>
internal sealed class ExpressBoundAttribute
{
    /// <summary>
    /// Initializes a bound attribute.
    /// </summary>
    /// <param name="name">The local attribute name.</param>
    /// <param name="kind">The attribute family.</param>
    /// <param name="type">The resolved attribute type.</param>
    /// <param name="isOptional">Whether the explicit attribute is optional.</param>
    /// <param name="span">The attribute declaration span.</param>
    /// <param name="inverseEntity">The entity that declares the forward role for an inverse attribute.</param>
    /// <param name="inverseAttributeName">The forward role name for an inverse attribute.</param>
    internal ExpressBoundAttribute(
        string name,
        ExpressAttributeKind kind,
        ExpressBoundType type,
        bool isOptional,
        ExpressSourceSpan span,
        ExpressBoundSymbol? inverseEntity = null,
        string? inverseAttributeName = null)
    {
        Name = name;
        Kind = kind;
        Type = type;
        IsOptional = isOptional;
        Span = span;
        InverseEntity = inverseEntity;
        InverseAttributeName = inverseAttributeName;
    }

    /// <summary>
    /// Gets the local attribute name.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Gets the attribute family.
    /// </summary>
    internal ExpressAttributeKind Kind { get; }

    /// <summary>
    /// Gets the resolved attribute type.
    /// </summary>
    internal ExpressBoundType Type { get; }

    /// <summary>
    /// Gets a value indicating whether the explicit attribute is optional.
    /// </summary>
    internal bool IsOptional { get; }

    /// <summary>
    /// Gets the attribute declaration span.
    /// </summary>
    internal ExpressSourceSpan Span { get; }

    /// <summary>
    /// Gets the entity that declares the forward role for an inverse attribute.
    /// </summary>
    internal ExpressBoundSymbol? InverseEntity { get; }

    /// <summary>
    /// Gets the forward role name for an inverse attribute.
    /// </summary>
    internal string? InverseAttributeName { get; }
}