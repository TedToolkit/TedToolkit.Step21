// -----------------------------------------------------------------------
// <copyright file="RecursiveAggregateTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Tests.ExpressBindingTests;

/// <summary>Distinguishes recursive aggregate values from circular type aliases.</summary>
internal sealed class RecursiveAggregateTests
{
    /// <summary>An aggregate consumer must not hide an independently invalid alias or SELECT cycle.</summary>
    [Test]
    [Arguments("second", "first")]
    [Arguments("SELECT (second)", "SELECT (first)")]
    public async Task Should_still_reject_non_aggregate_type_cycles(string firstType, string secondType)
    {
        var source = $$"""
            SCHEMA invalid_cycle;
            TYPE container = LIST [0:?] OF first;
            END_TYPE;
            TYPE first = {{firstType}};
            END_TYPE;
            TYPE second = {{secondType}};
            END_TYPE;
            END_SCHEMA;
            """;
        var result = ExpressSchemaCompiler.Compile([new ExpressSchemaSource("invalid-cycle.exp", source)]);
        using (Assert.Multiple())
        {
            await Assert.That(result.SyntaxDiagnostics).IsEmpty();
            await Assert.That(result.Schemas).IsEmpty();
            await Assert.That(result.BindingDiagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(["EXPRESS-BIND-TYPE-CYCLE", "EXPRESS-BIND-TYPE-CYCLE"]);
        }
    }

    /// <summary>ISO 10303-50 4.4.8–4.4.11 uses aggregate-contained SELECT recursion for finite tuples.</summary>
    [Test]
    public async Task Should_bind_recursive_aggregate_select_values_without_expanding_the_type_graph()
    {
        var result = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("recursive-values.exp", """
                SCHEMA recursive_values;
                TYPE atom = REAL;
                END_TYPE;
                TYPE tuple = LIST [0:?] OF atom_or_tuple;
                END_TYPE;
                TYPE atom_or_tuple = SELECT (atom, tuple);
                END_TYPE;
                ENTITY sample;
                  contents : atom_or_tuple;
                END_ENTITY;
                END_SCHEMA;
                """),
        ]);

        using (Assert.Multiple())
        {
            await Assert.That(result.SyntaxDiagnostics).IsEmpty();
            await Assert.That(result.BindingDiagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, result.BindingDiagnostics.Select(diagnostic => diagnostic.Message)));
        }

        var types = result.Schemas.Single().Declarations.OfType<ExpressBoundDefinedType>().ToArray();
        var tuple = types.Single(type => type.Name == "tuple");
        var choice = types.Single(type => type.Name == "atom_or_tuple");
        var element = (ExpressBoundNamedType)((ExpressBoundAggregateType)tuple.UnderlyingType).ElementType;
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(element.Declaration, choice.Symbol)).IsTrue();
            await Assert.That(((ExpressBoundSelectType)choice.UnderlyingType).Alternatives).Contains(tuple.Symbol);
        }
    }
}