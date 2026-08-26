// -----------------------------------------------------------------------
// <copyright file="ExpressComplexEntityProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.Step21.Analyzer.Express.Analysis;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Describes one generated CLR representation of a supported multi-leaf evaluated-set member.
/// </summary>
internal sealed class ExpressComplexEntityProjection
{
    private const int MAX_FLAT_ANDOR_FACTORS = 8;

    private const int MAX_COMPLEX_COMBINATIONS_PER_ROOT = 256;

    private ExpressComplexEntityProjection(
        ExpressBoundSchema schema,
        string name,
        IEnumerable<ExpressEntityProjection> leaves,
        IEnumerable<ExpressEntityProjection> components,
        IEnumerable<ExpressEntityAttributeProjection> properties)
    {
        Schema = schema;
        Name = name;
        Leaves = new ReadOnlyCollection<ExpressEntityProjection>(leaves.ToArray());
        Components = new ReadOnlyCollection<ExpressEntityProjection>(components.ToArray());
        Properties = new ReadOnlyCollection<ExpressEntityAttributeProjection>(properties.ToArray());
    }

    /// <summary>
    /// Gets the owning schema.
    /// </summary>
    internal ExpressBoundSchema Schema { get; }

    /// <summary>
    /// Gets the deterministic internal generated class name.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Gets the multiple leaf entity projections.
    /// </summary>
    internal IReadOnlyList<ExpressEntityProjection> Leaves { get; }

    /// <summary>
    /// Gets every partial entity value in external-mapping order.
    /// </summary>
    internal IReadOnlyList<ExpressEntityProjection> Components { get; }

    /// <summary>
    /// Gets the combined generated property surface.
    /// </summary>
    internal IReadOnlyList<ExpressEntityAttributeProjection> Properties { get; }

    /// <summary>
    /// Gets a value indicating whether any selected leaf derives the physical explicit slot.
    /// </summary>
    /// <param name="attribute">The physical explicit slot.</param>
    /// <returns><see langword="true" /> when external mapping must use <c>*</c>.</returns>
    internal bool IsDerivedRedeclared(ExpressEntityAttributeProjection attribute)
    {
        return Leaves.SelectMany(leaf => leaf.DerivedRedeclaredAttributes)
            .Any(derived => ReferenceEquals(derived.StorageEntity.Symbol, attribute.StorageEntity.Symbol)
                && StringComparer.OrdinalIgnoreCase.Equals(
                    derived.StorageAttributeName,
                    attribute.StorageAttributeName));
    }

    /// <summary>
    /// Creates supported flat-ANDOR multi-leaf projections.
    /// </summary>
    /// <param name="projections">The generated entity projections.</param>
    /// <param name="resolver">The generated type resolver.</param>
    /// <returns>The supported synthetic complex entity projections.</returns>
    internal static IReadOnlyList<ExpressComplexEntityProjection> Create(
        IReadOnlyList<ExpressEntityProjection> projections,
        ExpressGeneratedTypeResolver resolver)
    {
        var bySymbol = projections.ToDictionary(projection => projection.Entity.Symbol);
        var result = new List<ExpressComplexEntityProjection>();
        foreach (var root in projections)
        {
            var expression = root.Analysis.GetDeclaration(root.Entity).DescendantsAndSelf()
                .FirstOrDefault(rule => rule.Role == "supertypeExpression");
            var leafSets = expression is null
                ? CreateUnconstrainedSiblingSets(root, projections)
                : EvaluateSupertypeExpression(expression);
            if (leafSets.Count > MAX_COMPLEX_COMBINATIONS_PER_ROOT)
            {
                continue;
            }

            foreach (var leafSet in leafSets.Where(set => set.Count >= 2))
            {
                var selected = leafSet.Select(name => projections.SingleOrDefault(candidate =>
                        ReferenceEquals(candidate.Schema, root.Schema)
                        && StringComparer.OrdinalIgnoreCase.Equals(candidate.Entity.Name, name)))
                    .ToArray();
                if (selected.Any(candidate => candidate is null)
                    || selected.Length > MAX_FLAT_ANDOR_FACTORS
                    || selected.Any(candidate => candidate!.Entity.IsAbstract))
                {
                    continue;
                }

                var concrete = selected.Select(candidate => candidate!).ToArray();

                var componentSymbols = concrete
                    .SelectMany(candidate => candidate.PhysicalComponents.Select(component => component.Symbol))
                    .Distinct()
                    .ToArray();
                var components = componentSymbols
                    .Select(symbol => bySymbol[symbol])
                    .OrderBy(candidate => candidate.Entity.Name.ToUpperInvariant(), StringComparer.Ordinal)
                    .ToArray();
                var properties = concrete.SelectMany(candidate => candidate.FlattenedAttributes)
                    .GroupBy(attribute => attribute.Name, StringComparer.Ordinal)
                    .Where(group => group.Select(attribute =>
                            (
                                attribute.StorageEntity.Symbol,
                                StorageAttribute: attribute.StorageAttributeName.ToUpperInvariant(),
                                Type: ExpressTypeDocumentation.Format(attribute.Type),
                                attribute.Attribute.IsOptional))
                        .Distinct()
                        .Count() == 1)
                    .Select(group => group.First())
                    .ToArray();
                if (properties.Select(attribute => attribute.Name).Distinct(StringComparer.Ordinal).Count()
                    != concrete.SelectMany(candidate => candidate.FlattenedAttributes)
                        .Select(attribute => attribute.Name)
                        .Distinct(StringComparer.Ordinal)
                        .Count()
                    || properties.Any(attribute =>
                        !ExpressDescriptorTypeSupport.CanMap(attribute.Type, resolver)))
                {
                    continue;
                }

                var name = "__Complex_" + string.Join("_", concrete
                    .Select(candidate => candidate.Entity.Name.ToUpperInvariant())
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Select(ExpressEntityProjection.ToPascalCase));
                result.Add(new(root.Schema, name, concrete, components, properties));
            }
        }

        return new ReadOnlyCollection<ExpressComplexEntityProjection>(result
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray());
    }

    private static List<IReadOnlyList<string>> CreateUnconstrainedSiblingSets(
        ExpressEntityProjection root,
        IReadOnlyList<ExpressEntityProjection> projections)
    {
        var siblings = projections.Where(candidate =>
                ReferenceEquals(candidate.Schema, root.Schema)
                && candidate.Entity.DirectSupertypes.Contains(root.Entity.Symbol)
                && !candidate.Entity.IsAbstract)
            .Select(candidate => candidate.Entity.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (siblings.Length is < 2 or > MAX_FLAT_ANDOR_FACTORS)
        {
            return new();
        }

        var result = new List<IReadOnlyList<string>>();
        for (var mask = 1; mask < 1 << siblings.Length; mask++)
        {
            var selected = siblings.Where((_, index) => (mask & 1 << index) != 0).ToArray();
            if (selected.Length >= 2)
            {
                result.Add(selected);
            }
        }

        return result;
    }

    private static List<IReadOnlyList<string>> EvaluateSupertypeExpression(ExpressSemanticRule expression)
    {
        var factors = expression.ChildRules("supertypeFactor")
            .Select(EvaluateSupertypeFactor)
            .ToArray();
        var result = new List<IReadOnlyList<string>>();
        foreach (var factor in factors)
        {
            var previous = result.ToArray();
            result.AddRange(factor);
            result.AddRange(previous.SelectMany(left => factor.Select(right => Merge(left, right))));
            result = DistinctSets(result);
            if (result.Count > MAX_COMPLEX_COMBINATIONS_PER_ROOT)
            {
                return result;
            }
        }

        return result;
    }

    private static List<IReadOnlyList<string>> EvaluateSupertypeFactor(ExpressSemanticRule factor)
    {
        var result = new List<IReadOnlyList<string>>() { Array.Empty<string>(), };
        foreach (var term in factor.ChildRules("supertypeTerm"))
        {
            var alternatives = EvaluateSupertypeTerm(term);
            result = DistinctSets(result.SelectMany(left => alternatives.Select(right => Merge(left, right))));
        }

        return result;
    }

    private static List<IReadOnlyList<string>> EvaluateSupertypeTerm(ExpressSemanticRule term)
    {
        var reference = term.ChildRules("entityRef").SingleOrDefault();
        if (reference is not null)
        {
            return new() { new[] { reference.Identifier!, }, };
        }

        var oneOf = term.ChildRules("oneOf").SingleOrDefault();
        if (oneOf is not null)
        {
            return DistinctSets(oneOf.ChildRules("supertypeExpression")
                .SelectMany(EvaluateSupertypeExpression));
        }

        var nested = term.ChildRules("supertypeExpression").Single();
        return EvaluateSupertypeExpression(nested);
    }

    private static string[] Merge(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        return left.Concat(right)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static List<IReadOnlyList<string>> DistinctSets(IEnumerable<IReadOnlyList<string>> sets)
    {
        return sets.GroupBy(set => string.Join("\0", set.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    /// <summary>
    /// Gets non-redeclared explicit parameters declared by one component.
    /// </summary>
    /// <param name="component">The physical component projection.</param>
    /// <returns>The component's physical explicit parameters.</returns>
    internal static IReadOnlyList<ExpressEntityAttributeProjection> GetComponentAttributes(
        ExpressEntityProjection component)
    {
        return component.OwnAttributes
            .Where(attribute => attribute.RedeclaredEntityName is null)
            .ToArray();
    }
}