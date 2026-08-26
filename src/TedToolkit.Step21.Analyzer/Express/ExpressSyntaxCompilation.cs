// -----------------------------------------------------------------------
// <copyright file="ExpressSyntaxCompilation.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Carries the immutable output of the syntax stage.
/// </summary>
internal sealed class ExpressSyntaxCompilation
{
    /// <summary>
    /// Initializes a syntax-stage result.
    /// </summary>
    /// <param name="schemas">The parsed schemas.</param>
    /// <param name="diagnostics">The ordered syntax diagnostics.</param>
    internal ExpressSyntaxCompilation(
        IEnumerable<ExpressParsedSchema> schemas,
        IEnumerable<ExpressSyntaxDiagnostic> diagnostics)
    {
        Schemas = new ReadOnlyCollection<ExpressParsedSchema>(schemas.ToArray());
        Diagnostics = new ReadOnlyCollection<ExpressSyntaxDiagnostic>(diagnostics.ToArray());
    }

    /// <summary>
    /// Gets parsed schemas in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressParsedSchema> Schemas { get; }

    /// <summary>
    /// Gets syntax diagnostics in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressSyntaxDiagnostic> Diagnostics { get; }
}