// -----------------------------------------------------------------------
// <copyright file="ExpressSyntaxElement.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Provides source identity shared by immutable EXPRESS rule and token IR elements.
/// </summary>
internal abstract class ExpressSyntaxElement
{
    /// <summary>
    /// Initializes an EXPRESS syntax element.
    /// </summary>
    /// <param name="span">The element's half-open source span.</param>
    protected ExpressSyntaxElement(ExpressSourceSpan span)
    {
        Span = span;
    }

    /// <summary>
    /// Gets the element's half-open source span.
    /// </summary>
    internal ExpressSourceSpan Span { get; }
}