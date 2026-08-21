// -----------------------------------------------------------------------
// <copyright file="ExpressBoundType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Provides source evidence shared by immutable bound EXPRESS types.
/// </summary>
internal abstract class ExpressBoundType
{
    /// <summary>
    /// Initializes a bound type.
    /// </summary>
    /// <param name="span">The complete type-expression span.</param>
    protected ExpressBoundType(ExpressSourceSpan span)
    {
        Span = span;
    }

    /// <summary>
    /// Gets the complete type-expression span.
    /// </summary>
    internal ExpressSourceSpan Span { get; }
}