// -----------------------------------------------------------------------
// <copyright file="ExpressIncrementalGenerator.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Generates deterministic C# artifacts from the closed set of supplied EXPRESS additional files.
/// </summary>
[Generator]
[CLSCompliant(false)]
public sealed class ExpressIncrementalGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Registers the EXPRESS AdditionalFiles compilation and source-emission pipeline.
    /// </summary>
    /// <param name="context">The incremental generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var expressInputs = context.AdditionalTextsProvider
            .Where(static input => IsGeneratorInput(input.Path))
            .Select(static (input, cancellationToken) => new ExpressGeneratorInput(
                input.Path,
                input.GetText(cancellationToken)?.ToString()))
            .Collect()
            .Select(static (inputs, _) => Compile(inputs));

        context.RegisterSourceOutput(expressInputs, static (productionContext, result) =>
        {
            var plan = ExpressGenerationPlan.Create(result.Compilation, result.Inputs);
            ExpressSourceEmissionStage.Emit(productionContext, result, plan);
        });
    }

    private static ExpressGeneratorCompilation Compile(in ImmutableArray<ExpressGeneratorInput> inputs)
    {
        var readableInputs = inputs
            .Where(input => input.Text is not null
                && string.Equals(Path.GetExtension(input.Path), ".exp", StringComparison.OrdinalIgnoreCase))
            .Select(input => new ExpressSchemaSource(input.Path, input.Text!));
        return new(inputs, ExpressSchemaCompiler.Analyze(readableInputs));
    }

    private static bool IsGeneratorInput(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".exp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".p21map", StringComparison.OrdinalIgnoreCase);
    }
}