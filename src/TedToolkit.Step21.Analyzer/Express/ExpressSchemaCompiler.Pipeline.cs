// -----------------------------------------------------------------------
// <copyright file="ExpressSchemaCompiler.Pipeline.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Analysis;

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Provides the compatibility entry points owned by compiler-pipeline orchestration.
/// </summary>
internal static class ExpressSchemaCompiler
{
    /// <summary>
    /// Compiles the supplied sources without reading their logical paths or consulting external state.
    /// </summary>
    /// <param name="sources">The complete closed source set.</param>
    /// <returns>The valid independent schemas and complete deterministic diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources" /> or one of its elements is null.</exception>
    internal static ExpressSchemaCompilation Compile(IEnumerable<ExpressSchemaSource> sources)
    {
        return Analyze(sources).Compilation;
    }

    /// <summary>
    /// Compiles the supplied sources and retains syntax-detached analysis output for generation.
    /// </summary>
    /// <param name="sources">The complete closed source set.</param>
    /// <returns>The analyzed compilation and semantic lowering input.</returns>
    internal static ExpressAnalyzedCompilation Analyze(IEnumerable<ExpressSchemaSource> sources)
    {
        return ExpressCompilerPipeline.Analyze(sources);
    }
}