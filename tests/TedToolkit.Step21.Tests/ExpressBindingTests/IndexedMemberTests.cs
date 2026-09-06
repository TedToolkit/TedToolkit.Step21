// -----------------------------------------------------------------------
// <copyright file="IndexedMemberTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Tests.ExpressBindingTests;

/// <summary>Retains aggregate element identity through aliases and SELECT alternatives.</summary>
internal sealed class IndexedMemberTests
{
    /// <summary>A following member belongs to the selected element, not the aggregate wrapper.</summary>
    [Test]
    [Arguments("node_list")]
    [Arguments("SELECT (node_list, node_set)")]
    public async Task Should_bind_indexed_members_through_named_aggregate_types(string aggregateDefinition)
    {
        var source = $$"""
            SCHEMA indexed_members;
            ENTITY node;
              amount : REAL;
            END_ENTITY;
            TYPE node_list = LIST [1:?] OF node;
            END_TYPE;
            TYPE node_set = SET [1:?] OF node;
            END_TYPE;
            TYPE nodes = {{aggregateDefinition}};
            END_TYPE;
            FUNCTION first_amount(candidate : nodes) : REAL;
              RETURN(candidate[1].amount);
            END_FUNCTION;
            END_SCHEMA;
            """;
        var result = ExpressSchemaCompiler.Compile([new ExpressSchemaSource("indexed-members.exp", source)]);
        var invalid = ExpressSchemaCompiler.Compile([new ExpressSchemaSource(
            "missing-member.exp", source.Replace("[1].amount", "[1].absent", StringComparison.Ordinal))]);

        using (Assert.Multiple())
        {
            await Assert.That(result.SyntaxDiagnostics).IsEmpty();
            await Assert.That(result.BindingDiagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, result.BindingDiagnostics.Select(diagnostic => diagnostic.Message)));
            await Assert.That(invalid.BindingDiagnostics.Select(diagnostic => diagnostic.Code))
                .Contains("EXPRESS-BIND-UNRESOLVED-MEMBER");
        }

        var member = result.Schemas.Single().NameReferences.Single(reference => reference.Target.Name == "amount");
        await Assert.That(member.Target.Attribute!.DeclaringEntity.Name).IsEqualTo("node");
    }
}