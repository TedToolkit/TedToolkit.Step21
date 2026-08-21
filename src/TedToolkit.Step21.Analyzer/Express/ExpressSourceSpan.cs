// -----------------------------------------------------------------------
// <copyright file="ExpressSourceSpan.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Retains one half-open source span in an EXPRESS input.
/// </summary>
internal sealed class ExpressSourceSpan
{
    /// <summary>
    /// Initializes a source span.
    /// </summary>
    /// <param name="start">The inclusive start.</param>
    /// <param name="end">The exclusive end.</param>
    internal ExpressSourceSpan(ExpressSourceLocation start, ExpressSourceLocation end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Gets the inclusive start.
    /// </summary>
    internal ExpressSourceLocation Start { get; }

    /// <summary>
    /// Gets the exclusive end.
    /// </summary>
    internal ExpressSourceLocation End { get; }
}