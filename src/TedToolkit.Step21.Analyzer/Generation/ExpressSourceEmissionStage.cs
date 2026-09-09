// -----------------------------------------------------------------------
// <copyright file="ExpressSourceEmissionStage.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Emits deterministic Roslyn sources and diagnostics from one immutable generation plan.
/// </summary>
internal static class ExpressSourceEmissionStage
{
    /// <summary>
    /// Emits all valid planned schemas and reports every withheld-schema diagnostic.
    /// </summary>
    /// <param name="context">The Roslyn source-production context.</param>
    /// <param name="result">The generator inputs and analyzed compilation.</param>
    /// <param name="plan">The immutable generation plan.</param>
    internal static void Emit(
        in SourceProductionContext context,
        in ExpressGeneratorCompilation result,
        ExpressGenerationPlan plan)
    {
        ExpressGeneratorDiagnostics.Report(context, result);
        ExpressGeneratorDiagnostics.ReportNameCollisions(context, result, plan.EntityPlan.Collisions);
        ExpressGeneratorDiagnostics.ReportGenerationFailures(context, result, plan.EntityPlan.Failures);
        ExpressGeneratorDiagnostics.ReportReachableRuleFailures(context, result, plan.RuleFailures);
        ExpressGeneratorDiagnostics.ReportPhysicalNameFailures(context, result, plan.PhysicalNames.Failures);
        if (plan.PhysicalNames.Failures.Count > 0)
        {
            return;
        }

        foreach (var projection in plan.ValueProjections
                     .Where(projection => !plan.InvalidSchemas.Contains(projection.Schema)))
        {
            ExpressValueEmitter.Emit(context, projection);
        }

        foreach (var projection in plan.EntityPlan.Projections
                     .Where(projection => !plan.InvalidSchemas.Contains(projection.Schema)))
        {
            ExpressEntityEmitter.Emit(context, projection, plan.ValueResolver);
        }

        foreach (var projection in plan.ComplexProjections
                     .Where(projection => !plan.InvalidSchemas.Contains(projection.Schema)))
        {
            ExpressComplexEntityEmitter.Emit(context, projection, plan.ValueResolver);
        }

        foreach (var schema in plan.Compilation.Schemas.Where(schema => !plan.InvalidSchemas.Contains(schema)))
        {
            ExpressSchemaDescriptorEmitter.Emit(
                context,
                schema,
                plan.EntityPlan.Projections
                    .Where(projection => ReferenceEquals(projection.Schema, schema))
                    .ToArray(),
                plan.ComplexProjections
                    .Where(projection => ReferenceEquals(projection.Schema, schema))
                    .ToArray(),
                plan.ValueResolver,
                plan.RulePlans[schema],
                plan.PhysicalNames);
        }
    }
}