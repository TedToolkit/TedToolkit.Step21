// -----------------------------------------------------------------------
// <copyright file="ExpressSchemaCompilation.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents the deterministic valid schemas and complete failures from one closed compilation set.
/// </summary>
internal sealed class ExpressSchemaCompilation
{
    /// <summary>
    /// Initializes a closed-set compilation result.
    /// </summary>
    /// <param name="schemas">The independently valid bound schemas.</param>
    /// <param name="syntaxDiagnostics">All syntax diagnostics.</param>
    /// <param name="bindingDiagnostics">All binding diagnostics.</param>
    internal ExpressSchemaCompilation(
        IEnumerable<ExpressBoundSchema> schemas,
        IEnumerable<ExpressSyntaxDiagnostic> syntaxDiagnostics,
        IEnumerable<ExpressBindingDiagnostic> bindingDiagnostics)
    {
        Schemas = new ReadOnlyCollection<ExpressBoundSchema>(schemas.ToArray());
        SyntaxDiagnostics = new ReadOnlyCollection<ExpressSyntaxDiagnostic>(syntaxDiagnostics.ToArray());
        BindingDiagnostics = new ReadOnlyCollection<ExpressBindingDiagnostic>(bindingDiagnostics.ToArray());
    }

    /// <summary>
    /// Gets the independently valid bound schemas in deterministic order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundSchema> Schemas { get; }

    /// <summary>
    /// Gets all syntax diagnostics in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressSyntaxDiagnostic> SyntaxDiagnostics { get; }

    /// <summary>
    /// Gets all binding diagnostics in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressBindingDiagnostic> BindingDiagnostics { get; }
}