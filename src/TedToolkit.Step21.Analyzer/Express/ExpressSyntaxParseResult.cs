// -----------------------------------------------------------------------
// <copyright file="ExpressSyntaxParseResult.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Represents the atomic result of parsing one EXPRESS input.
/// </summary>
internal sealed class ExpressSyntaxParseResult
{
    /// <summary>
    /// Initializes an atomic parse result.
    /// </summary>
    /// <param name="root">The complete root, or <see langword="null"/> when diagnostics exist.</param>
    /// <param name="diagnostics">The deterministic syntax diagnostics.</param>
    internal ExpressSyntaxParseResult(
        ExpressRuleSyntax? root,
        IEnumerable<ExpressSyntaxDiagnostic> diagnostics)
    {
        Root = root;
        Diagnostics = new ReadOnlyCollection<ExpressSyntaxDiagnostic>(diagnostics.ToArray());
    }

    /// <summary>
    /// Gets the complete root, or <see langword="null"/> when diagnostics exist.
    /// </summary>
    internal ExpressRuleSyntax? Root { get; }

    /// <summary>
    /// Gets the deterministic syntax diagnostics.
    /// </summary>
    internal IReadOnlyList<ExpressSyntaxDiagnostic> Diagnostics { get; }
}