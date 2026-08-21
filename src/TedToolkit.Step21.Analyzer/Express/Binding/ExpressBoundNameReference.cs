// -----------------------------------------------------------------------
// <copyright file="ExpressBoundNameReference.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents one neutral syntax name resolved to its immutable declaration identity.
/// </summary>
internal sealed class ExpressBoundNameReference
{
    /// <summary>
    /// Initializes a bound name reference.
    /// </summary>
    /// <param name="target">The selected declaration.</param>
    /// <param name="isApplication">Whether the name uses application syntax.</param>
    /// <param name="span">The reference span.</param>
    internal ExpressBoundNameReference(
        ExpressBoundName target,
        bool isApplication,
        ExpressSourceSpan span)
    {
        Target = target;
        IsApplication = isApplication;
        Span = span;
    }

    /// <summary>
    /// Gets the selected declaration.
    /// </summary>
    internal ExpressBoundName Target { get; }

    /// <summary>
    /// Gets a value indicating whether the name uses application syntax.
    /// </summary>
    internal bool IsApplication { get; }

    /// <summary>
    /// Gets the reference span.
    /// </summary>
    internal ExpressSourceSpan Span { get; }
}