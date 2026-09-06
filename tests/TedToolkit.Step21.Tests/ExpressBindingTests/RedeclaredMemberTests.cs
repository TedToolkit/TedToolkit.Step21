// -----------------------------------------------------------------------
// <copyright file="RedeclaredMemberTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Tests.ExpressBindingTests;

/// <summary>Protects effective attribute identity across repeated ancestor-slot redeclarations.</summary>
internal sealed class RedeclaredMemberTests
{
    /// <summary>The nearest subtype wins even when each declaration names the original slot owner.</summary>
    [Test]
    public async Task Should_bind_the_most_specific_redeclaration_of_an_ancestor_slot()
    {
        var result = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("redeclarations.exp", """
                SCHEMA redeclarations;
                ENTITY item; END_ENTITY;
                ENTITY narrow_item SUBTYPE OF (item); END_ENTITY;
                ENTITY base_owner;
                  members : SET [1:?] OF item;
                END_ENTITY;
                ENTITY middle_owner SUBTYPE OF (base_owner);
                  SELF\base_owner.members : SET [1:?] OF narrow_item;
                END_ENTITY;
                ENTITY leaf_owner SUBTYPE OF (middle_owner);
                  SELF\base_owner.members : SET [1:1] OF narrow_item;
                END_ENTITY;
                FUNCTION count_members(candidate : leaf_owner) : INTEGER;
                  RETURN(SIZEOF(candidate.members));
                END_FUNCTION;
                END_SCHEMA;
                """),
        ]);

        using (Assert.Multiple())
        {
            await Assert.That(result.SyntaxDiagnostics).IsEmpty();
            await Assert.That(result.BindingDiagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, result.BindingDiagnostics.Select(diagnostic => diagnostic.Message)));
            await Assert.That(result.Schemas.Select(schema => schema.Name)).IsEquivalentTo(["redeclarations"]);
        }

        var member = result.Schemas.Single().NameReferences.Last(reference => reference.Target.Name == "members").Target;
        using (Assert.Multiple())
        {
            await Assert.That(member.Attribute!.DeclaringEntity.Name).IsEqualTo("leaf_owner");
            await Assert.That(member.AttributeCandidates.Select(attribute => attribute.DeclaringEntity.Name))
                .IsEquivalentTo(["base_owner", "middle_owner", "leaf_owner"]);
        }
    }
}