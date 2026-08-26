// -----------------------------------------------------------------------
// <copyright file="ExpressAnalyzedCompilation.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Express.Analysis;

/// <summary>
/// Represents the immutable expression and flow analysis output for one closed compilation.
/// </summary>
internal sealed class ExpressAnalyzedCompilation
{
    private readonly ReadOnlyDictionary<ExpressBoundSchema, ExpressSchemaAnalysis> _analyses;

    /// <summary>
    /// Initializes analyzed schemas and their syntax-detached semantic rule models.
    /// </summary>
    /// <param name="binding">The closed-set binding output.</param>
    /// <param name="schemas">The analyzed schemas.</param>
    /// <param name="analyses">The semantic rule analysis for every schema.</param>
    internal ExpressAnalyzedCompilation(
        ExpressSchemaCompilation binding,
        IEnumerable<ExpressBoundSchema> schemas,
        IEnumerable<ExpressSchemaAnalysis> analyses)
    {
        Binding = binding;
        Schemas = new ReadOnlyCollection<ExpressBoundSchema>(schemas.ToArray());
        Compilation = new(
            Schemas,
            binding.SyntaxDiagnostics,
            binding.BindingDiagnostics);
        _analyses = new(
            analyses.ToDictionary(analysis => analysis.Schema));
    }

    /// <summary>
    /// Gets the closed-set binding output retained as analysis evidence.
    /// </summary>
    internal ExpressSchemaCompilation Binding { get; }

    /// <summary>
    /// Gets the compatibility compilation containing analyzed bound schemas.
    /// </summary>
    internal ExpressSchemaCompilation Compilation { get; }

    /// <summary>
    /// Gets independently valid analyzed schemas in deterministic order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundSchema> Schemas { get; }

    /// <summary>
    /// Gets all syntax diagnostics in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressSyntaxDiagnostic> SyntaxDiagnostics
    {
        get
        {
            return Binding.SyntaxDiagnostics;
        }
    }

    /// <summary>
    /// Gets all binding diagnostics in deterministic source order.
    /// </summary>
    internal IReadOnlyList<ExpressBindingDiagnostic> BindingDiagnostics
    {
        get
        {
            return Binding.BindingDiagnostics;
        }
    }

    /// <summary>
    /// Gets the syntax-detached semantic analysis for one analyzed schema.
    /// </summary>
    /// <param name="schema">The analyzed schema identity.</param>
    /// <returns>The schema analysis.</returns>
    internal ExpressSchemaAnalysis GetAnalysis(ExpressBoundSchema schema)
    {
        return _analyses[schema];
    }
}