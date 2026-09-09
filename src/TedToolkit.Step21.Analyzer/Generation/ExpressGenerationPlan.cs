// -----------------------------------------------------------------------
// <copyright file="ExpressGenerationPlan.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.Step21.Analyzer.Express.Analysis;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Carries the immutable output of generation planning into source emission.
/// </summary>
internal sealed class ExpressGenerationPlan
{
    private ExpressGenerationPlan(
        ExpressAnalyzedCompilation compilation,
        ExpressGeneratedTypeResolver valueResolver,
        ExpressPhysicalNameMap physicalNames,
        IEnumerable<ExpressValueProjection> valueProjections,
        ExpressEntityGenerationPlan entityPlan,
        IEnumerable<ExpressComplexEntityProjection> complexProjections,
        IReadOnlyDictionary<ExpressBoundSchema, ExpressReachableRulePlan> rulePlans,
        IEnumerable<ExpressEntityGenerationFailure> ruleFailures,
        IEnumerable<ExpressBoundSchema> invalidSchemas)
    {
        Compilation = compilation;
        ValueResolver = valueResolver;
        PhysicalNames = physicalNames;
        ValueProjections = new ReadOnlyCollection<ExpressValueProjection>(valueProjections.ToArray());
        EntityPlan = entityPlan;
        ComplexProjections = new ReadOnlyCollection<ExpressComplexEntityProjection>(complexProjections.ToArray());
        RulePlans = new ReadOnlyDictionary<ExpressBoundSchema, ExpressReachableRulePlan>(
            rulePlans.ToDictionary(pair => pair.Key, pair => pair.Value));
        RuleFailures = new ReadOnlyCollection<ExpressEntityGenerationFailure>(ruleFailures.ToArray());
        InvalidSchemas = new ReadOnlyCollection<ExpressBoundSchema>(invalidSchemas.ToArray());
    }

    /// <summary>
    /// Gets the analyzed closed schema compilation.
    /// </summary>
    internal ExpressAnalyzedCompilation Compilation { get; }

    /// <summary>
    /// Gets the closed generated-type resolver.
    /// </summary>
    internal ExpressGeneratedTypeResolver ValueResolver { get; }

    /// <summary>
    /// Gets the validated explicit Part 21 physical-name inventory.
    /// </summary>
    internal ExpressPhysicalNameMap PhysicalNames { get; }

    /// <summary>
    /// Gets value projections in deterministic declaration order.
    /// </summary>
    internal IReadOnlyList<ExpressValueProjection> ValueProjections { get; }

    /// <summary>
    /// Gets the validated entity plan.
    /// </summary>
    internal ExpressEntityGenerationPlan EntityPlan { get; }

    /// <summary>
    /// Gets complex-entity projections in deterministic declaration order.
    /// </summary>
    internal IReadOnlyList<ExpressComplexEntityProjection> ComplexProjections { get; }

    /// <summary>
    /// Gets reachable-rule plans by owning schema.
    /// </summary>
    internal IReadOnlyDictionary<ExpressBoundSchema, ExpressReachableRulePlan> RulePlans { get; }

    /// <summary>
    /// Gets source-located expression and flow planning failures.
    /// </summary>
    internal IReadOnlyList<ExpressEntityGenerationFailure> RuleFailures { get; }

    /// <summary>
    /// Gets schemas for which source emission must be withheld.
    /// </summary>
    internal IReadOnlyCollection<ExpressBoundSchema> InvalidSchemas { get; }

    /// <summary>
    /// Creates the complete deterministic generation plan.
    /// </summary>
    /// <param name="compilation">The analyzed closed schema compilation.</param>
    /// <returns>The immutable source-emission input without physical-name metadata.</returns>
    internal static ExpressGenerationPlan Create(ExpressAnalyzedCompilation compilation)
    {
        return Create(compilation, []);
    }

    /// <summary>
    /// Creates the complete deterministic generation plan.
    /// </summary>
    /// <param name="compilation">The analyzed closed schema compilation.</param>
    /// <param name="inputs">The AdditionalFiles used for schema and physical-name planning.</param>
    /// <returns>The immutable source-emission input.</returns>
    internal static ExpressGenerationPlan Create(
        ExpressAnalyzedCompilation compilation,
        IEnumerable<ExpressGeneratorInput> inputs)
    {
        var valueResolver = ExpressGeneratedTypeResolver.Create(compilation);
        var physicalNames = ExpressPhysicalNameMap.Create(inputs, compilation, valueResolver);
        var valueProjections = ExpressValueProjection.Create(compilation, valueResolver);
        var entityPlan = ExpressEntityGenerationPlan.Create(compilation, valueResolver);
        var complexProjections = ExpressComplexEntityProjection.Create(entityPlan.Projections, valueResolver);
        var rulePlans = compilation.Schemas.ToDictionary(
            schema => schema,
            schema => ExpressReachableRulePlan.Create(
                schema,
                compilation,
                valueResolver,
                entityPlan.Projections.Where(projection => ReferenceEquals(projection.Schema, schema)).ToArray(),
                complexProjections.Where(projection => ReferenceEquals(projection.Schema, schema)).ToArray()));
        var ruleFailures = rulePlans.Values.SelectMany(rulePlan => rulePlan.Failures).ToArray();
        var invalidSchemas = new HashSet<ExpressBoundSchema>(entityPlan.InvalidSchemas);
        invalidSchemas.UnionWith(ruleFailures.Select(failure => failure.Schema));
        return new(
            compilation,
            valueResolver,
            physicalNames,
            valueProjections,
            entityPlan,
            complexProjections,
            rulePlans,
            ruleFailures,
            invalidSchemas);
    }
}