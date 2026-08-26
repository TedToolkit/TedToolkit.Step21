// -----------------------------------------------------------------------
// <copyright file="ExpressCompilerPipeline.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Analysis;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Composes the one-way syntax, closed-set binding, and expression/flow analysis stages.
/// </summary>
internal static class ExpressCompilerPipeline
{
    /// <summary>
    /// Compiles the complete closed source set through the semantic stages.
    /// </summary>
    /// <param name="sources">The complete closed source set.</param>
    /// <returns>The analyzed valid schemas and complete diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> or one of its elements is null.</exception>
    internal static ExpressSchemaCompilation Compile(IEnumerable<ExpressSchemaSource> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        var logicalSources = sources
            .Select(source => source ?? throw new ArgumentNullException(nameof(sources)))
            .Select(source => (source.FilePath, source.Text));
        var syntax = ExpressSyntaxStage.Parse(logicalSources);
        var binding = ExpressClosedSetBindingStage.Bind(syntax);
        return ExpressExpressionFlowAnalysisStage.Analyze(binding);
    }
}