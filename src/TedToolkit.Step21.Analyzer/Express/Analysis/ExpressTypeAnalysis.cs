// -----------------------------------------------------------------------
// <copyright file="ExpressTypeAnalysis.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Express.Analysis;

/// <summary>
/// Provides syntax-independent type facts shared by analysis and generation lowering.
/// </summary>
internal static class ExpressTypeAnalysis
{
    /// <summary>
    /// Determines whether an expression type requires generated schema-aware value equality.
    /// </summary>
    /// <param name="type">The bound expression type.</param>
    /// <returns><see langword="true" /> when entity values occur in the type.</returns>
    internal static bool RequiresSchemaValueEquality(ExpressExpressionType type)
    {
        if (type.Kind is ExpressExpressionTypeKind.Entity or ExpressExpressionTypeKind.Select)
        {
            return true;
        }

        return type.DeclaredType is ExpressBoundAggregateType aggregate
            && RequiresSchemaValueEquality(aggregate.ElementType);
    }

    /// <summary>
    /// Determines whether a declared type requires generated schema-aware value equality.
    /// </summary>
    /// <param name="type">The bound declared type.</param>
    /// <returns><see langword="true" /> when entity values occur in the type.</returns>
    internal static bool RequiresSchemaValueEquality(ExpressBoundType type)
    {
        return type switch
        {
            ExpressBoundAggregateType aggregate => RequiresSchemaValueEquality(aggregate.ElementType),
            ExpressBoundGenericType { IsEntity: true, } => true,
            ExpressBoundNamedType named => named.Declaration.Kind == ExpressDeclarationKind.Entity,
            ExpressBoundSelectType => true,
            _ => false,
        };
    }

    /// <summary>
    /// Collects labeled generic variables from bound aggregate trees in stable first-declaration order.
    /// </summary>
    /// <param name="types">The formal, result, or local bound types.</param>
    /// <returns>The distinct EXPRESS type labels.</returns>
    internal static IReadOnlyList<string> GenericTypeLabels(IEnumerable<ExpressBoundType> types)
    {
        var labels = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in types)
        {
            var type = root;
            while (true)
            {
                if (type is ExpressBoundAggregateType aggregate)
                {
                    if (aggregate.TypeLabel is { } aggregateLabel && seen.Add(aggregateLabel))
                    {
                        labels.Add(aggregateLabel);
                    }

                    type = aggregate.ElementType;
                    continue;
                }

                if (type is ExpressBoundGenericType { TypeLabel: { } genericLabel, }
                    && seen.Add(genericLabel))
                {
                    labels.Add(genericLabel);
                }

                break;
            }
        }

        return labels;
    }
}