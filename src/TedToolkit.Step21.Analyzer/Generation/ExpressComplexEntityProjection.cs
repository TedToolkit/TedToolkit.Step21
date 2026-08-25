// -----------------------------------------------------------------------
// <copyright file="ExpressComplexEntityProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Describes one generated CLR representation of a supported multi-leaf evaluated-set member.
/// </summary>
internal sealed class ExpressComplexEntityProjection
{
    private const int MAX_FLAT_ANDOR_FACTORS = 8;

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
    /// Creates supported flat-ANDOR multi-leaf projections.
    /// </summary>
    /// <param name="projections">The generated entity projections.</param>
    /// <returns>The supported synthetic complex entity projections.</returns>
    internal static IReadOnlyList<ExpressComplexEntityProjection> Create(
        IReadOnlyList<ExpressEntityProjection> projections)
    {
        var bySymbol = projections.ToDictionary(projection => projection.Entity.Symbol);
        var result = new List<ExpressComplexEntityProjection>();
        foreach (var root in projections)
        {
            var expression = root.Entity.Syntax.DescendantsAndSelf()
                .FirstOrDefault(rule => rule.Production == "supertypeExpression");
            if (expression is null)
            {
                continue;
            }

            if (!expression.Children.OfType<ExpressTokenSyntax>()
                    .Any(token => token.TokenName == "ANDOR"))
            {
                continue;
            }

            var factors = expression.ChildRules("supertypeFactor").ToArray();
            var leaves = factors.Select(factor => factor.DescendantsAndSelf()
                    .Where(rule => rule.Production == "entityRef")
                    .Select(rule => rule.IdentifierToken().Text)
                    .ToArray())
                .ToArray();
            if (leaves.Any(names => names.Length != 1))
            {
                continue;
            }

            var candidates = leaves
                .Select(names => projections.SingleOrDefault(candidate =>
                    ReferenceEquals(candidate.Schema, root.Schema)
                    && StringComparer.OrdinalIgnoreCase.Equals(candidate.Entity.Name, names[0])
                    && candidate.Entity.DirectSupertypes.Contains(root.Entity.Symbol)))
                .ToArray();
            if (candidates.Any(candidate => candidate is null)
                || candidates.Length > MAX_FLAT_ANDOR_FACTORS)
            {
                continue;
            }

            var concrete = candidates.Select(candidate => candidate!).ToArray();
            for (var mask = 1; mask < 1 << concrete.Length; mask++)
            {
                var selected = concrete.Where((_, index) => (mask & 1 << index) != 0).ToArray();
                if (selected.Length < 2
                    || selected.Any(candidate =>
                        candidate.Entity.IsAbstract || candidate.HasDerivedRedeclaration))
                {
                    continue;
                }

                var componentSymbols = selected
                    .SelectMany(candidate => candidate.PhysicalComponents.Select(component => component.Symbol))
                    .Distinct()
                    .ToArray();
                var components = componentSymbols
                    .Select(symbol => bySymbol[symbol])
                    .OrderBy(candidate => candidate.Entity.Name.ToUpperInvariant(), StringComparer.Ordinal)
                    .ToArray();
                var properties = selected.SelectMany(candidate => candidate.FlattenedAttributes)
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
                    != selected.SelectMany(candidate => candidate.FlattenedAttributes)
                        .Select(attribute => attribute.Name)
                        .Distinct(StringComparer.Ordinal)
                        .Count())
                {
                    continue;
                }

                var name = "__Complex_" + string.Join("_", selected
                    .Select(candidate => candidate.Entity.Name.ToUpperInvariant())
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Select(ExpressEntityProjection.ToPascalCase));
                result.Add(new(root.Schema, name, selected, components, properties));
            }
        }

        return new ReadOnlyCollection<ExpressComplexEntityProjection>(result
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ToArray());
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