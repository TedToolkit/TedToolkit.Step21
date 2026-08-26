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
            .Where(static input => string.Equals(
                Path.GetExtension(input.Path),
                ".exp",
                StringComparison.OrdinalIgnoreCase))
            .Select(static (input, cancellationToken) => new ExpressGeneratorInput(
                input.Path,
                input.GetText(cancellationToken)?.ToString()))
            .Collect()
            .Select(static (inputs, _) => Compile(inputs));

        context.RegisterSourceOutput(expressInputs, static (productionContext, result) =>
        {
            ExpressGeneratorDiagnostics.Report(productionContext, result);
            var valueResolver = ExpressGeneratedTypeResolver.Create(result.Compilation);
            var valueProjections = ExpressValueProjection.Create(result.Compilation, valueResolver);
            var plan = ExpressEntityGenerationPlan.Create(result.Compilation, valueResolver);
            var complexProjections = ExpressComplexEntityProjection.Create(plan.Projections, valueResolver);
            var rulePlans = result.Compilation.Schemas.ToDictionary(
                schema => schema,
                schema => ExpressReachableRulePlan.Create(
                    schema,
                    valueResolver,
                    plan.Projections.Where(projection => ReferenceEquals(projection.Schema, schema)).ToArray(),
                    complexProjections.Where(projection => ReferenceEquals(projection.Schema, schema)).ToArray()));
            var ruleFailures = rulePlans.Values.SelectMany(rulePlan => rulePlan.Failures).ToArray();
            var invalidSchemas = new HashSet<ExpressBoundSchema>(plan.InvalidSchemas);
            invalidSchemas.UnionWith(ruleFailures.Select(failure => failure.Schema));
            ExpressGeneratorDiagnostics.ReportNameCollisions(productionContext, result, plan.Collisions);
            ExpressGeneratorDiagnostics.ReportGenerationFailures(productionContext, result, plan.Failures);
            ExpressGeneratorDiagnostics.ReportReachableRuleFailures(productionContext, result, ruleFailures);
            foreach (var projection in valueProjections
                         .Where(projection => !invalidSchemas.Contains(projection.Schema)))
            {
                ExpressValueEmitter.Emit(productionContext, projection);
            }

            foreach (var projection in plan.Projections
                         .Where(projection => !invalidSchemas.Contains(projection.Schema)))
            {
                ExpressEntityEmitter.Emit(productionContext, projection, valueResolver);
            }

            foreach (var projection in complexProjections
                         .Where(projection => !invalidSchemas.Contains(projection.Schema)))
            {
                ExpressComplexEntityEmitter.Emit(productionContext, projection, valueResolver);
            }

            foreach (var schema in result.Compilation.Schemas.Where(schema => !invalidSchemas.Contains(schema)))
            {
                ExpressSchemaDescriptorEmitter.Emit(
                    productionContext,
                    schema,
                    plan.Projections.Where(projection => ReferenceEquals(projection.Schema, schema)).ToArray(),
                    complexProjections.Where(projection => ReferenceEquals(projection.Schema, schema)).ToArray(),
                    valueResolver,
                    rulePlans[schema]);
            }
        });
    }

    private static ExpressGeneratorCompilation Compile(in ImmutableArray<ExpressGeneratorInput> inputs)
    {
        var readableInputs = inputs
            .Where(input => input.Text is not null)
            .Select(input => new ExpressSchemaSource(input.Path, input.Text!));
        return new(inputs, ExpressSchemaCompiler.Compile(readableInputs));
    }
}