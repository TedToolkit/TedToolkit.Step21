// -----------------------------------------------------------------------
// <copyright file="RulePopulationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Tests.ExpressBindingTests;

/// <summary>Separates RULE populations from entity data types in qualified expressions.</summary>
internal sealed class RulePopulationTests
{
    /// <summary>A RULE's population name must not hide the entity used after a group reference.</summary>
    [Test]
    public async Task Should_resolve_group_qualifiers_inside_a_same_named_rule_population()
    {
        var result = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("population.exp", """
                SCHEMA population;
                ENTITY item;
                  amount : REAL;
                END_ENTITY;
                RULE positive_items FOR (item);
                WHERE
                  valid : SIZEOF(QUERY(candidate <* item | candidate\item.amount <= 0.0)) = 0;
                END_RULE;
                END_SCHEMA;
                """),
        ]);

        using (Assert.Multiple())
        {
            await Assert.That(result.SyntaxDiagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, result.SyntaxDiagnostics.Select(diagnostic => diagnostic.Message)));
            await Assert.That(result.BindingDiagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, result.BindingDiagnostics.Select(diagnostic => diagnostic.Message)));
            await Assert.That(result.Schemas.Select(schema => schema.Name)).IsEquivalentTo(["population"]);
        }

        var itemReferences = result.Schemas.Single().NameReferences
            .Where(reference => reference.Target.Name == "item").Select(reference => reference.Target).ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(itemReferences.Select(target => target.Kind))
                .Contains(ExpressBoundNameKind.Population);
            await Assert.That(itemReferences.Select(target => target.Kind))
                .Contains(ExpressBoundNameKind.Entity);
            await Assert.That(itemReferences.Where(target => target.Kind == ExpressBoundNameKind.Population)
                .All(target => target.Type is ExpressBoundAggregateType)).IsTrue();
        }
    }
}