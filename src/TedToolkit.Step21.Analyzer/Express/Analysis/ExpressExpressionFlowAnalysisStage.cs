// -----------------------------------------------------------------------
// <copyright file="ExpressExpressionFlowAnalysisStage.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Express.Analysis;

/// <summary>
/// Produces immutable typed expressions and indeterminate-flow facts from closed-set binding output.
/// </summary>
internal static class ExpressExpressionFlowAnalysisStage
{
    /// <summary>
    /// Analyzes every independently bound schema without mutating the binding-stage output.
    /// </summary>
    /// <param name="compilation">The closed-set binding output.</param>
    /// <returns>The analyzed schemas with the original deterministic diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="compilation"/> is null.</exception>
    internal static ExpressAnalyzedCompilation Analyze(ExpressSchemaCompilation compilation)
    {
        if (compilation is null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }

        var schemas = compilation.Schemas.Select(AnalyzeSchema).ToArray();
        return new(compilation, schemas, schemas.Select(schema => new ExpressSchemaAnalysis(schema)));
    }

    private static ExpressBoundSchema AnalyzeSchema(ExpressBoundSchema schema)
    {
        var facts = ExpressExpressionBinder.Bind(schema.Declarations, schema.NameReferences);
        return new(
            schema.Identity,
            schema.Imports,
            schema.Declarations,
            schema.NestedDeclarations,
            schema.NameReferences,
            facts.Expressions,
            facts.IndeterminateFunctions,
            facts.IndeterminateLocals);
    }
}