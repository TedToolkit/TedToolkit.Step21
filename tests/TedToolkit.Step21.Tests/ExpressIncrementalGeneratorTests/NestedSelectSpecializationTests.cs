// -----------------------------------------------------------------------
// <copyright file="NestedSelectSpecializationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Preserves nominal nested SELECT branches when entity coverage overlaps.</summary>
internal sealed class NestedSelectSpecializationTests
{
    /// <summary>Two indistinguishable nested routes must not be chosen by traversal order.</summary>
    [Test]
    [Arguments("subset")]
    [Arguments("leaf")]
    public async Task Should_reject_ambiguous_nested_select_projections(string narrowed)
    {
        var result = GeneratorHostTests.Run(("ambiguous-nested.exp", $$"""
            SCHEMA ambiguous_nested;
            ENTITY root_item; END_ENTITY;
            ENTITY leaf SUBTYPE OF (root_item); END_ENTITY;
            ENTITY other; END_ENTITY;
            ENTITY third; END_ENTITY;
            TYPE subset = SELECT (leaf, other); END_TYPE;
            TYPE left_choice = SELECT (subset, third); END_TYPE;
            TYPE right_choice = SELECT (subset, root_item); END_TYPE;
            TYPE broad = SELECT (left_choice, right_choice); END_TYPE;
            ENTITY root ABSTRACT;
              choice : broad;
            END_ENTITY;
            ENTITY child SUBTYPE OF (root);
              SELF\root.choice : {{narrowed}};
            END_ENTITY;
            END_SCHEMA;
            """));
        var diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.Id).IsEqualTo("STEP21EXP005");
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    /// <summary>The stored nested branch, rather than its flattened leaves, determines the inherited projection.</summary>
    [Test]
    [Arguments("root_item, group_choice")]
    [Arguments("group_choice, root_item")]
    public async Task Should_preserve_nested_select_alternatives_independently_of_declaration_order(string alternatives)
    {
        var result = GeneratorHostTests.Run("""
            using System.IO;
            using System.Linq;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.NestedSpecialization;
            internal static class NestedConsumer
            {
                internal static bool Check()
                {
                    var leaf = new Leaf();
                    IRoot root = new Child(Narrow.FromSubset(Subset.FromLeaf(leaf)));
                    if (!root.Choice.TryGetGroupChoice(out var group) || !group.TryGetSubset(out var subset) ||
                        !subset.TryGetLeaf(out var projected) || !object.ReferenceEquals(projected, leaf)) return false;
                    var input = "ISO-10303-21;HEADER;FILE_DESCRIPTION(('nested'),'3;1');" +
                        "FILE_NAME('nested','2026-09-04T00:00:00',('A'),('O'),'P','S','');" +
                        "FILE_SCHEMA(('nested_specialization'));ENDSEC;DATA;" +
                        "#1=LEAF();#2=CHILD(#1);ENDSEC;END-ISO-10303-21;";
                    var descriptor = TedToolkit.Step21.Generated.NestedSpecialization.SchemaDescriptor.Instance;
                    var structure = ExchangeStructure.Read(new StringReader(input), [descriptor]);
                    var output = new StringWriter();
                    structure.Write(output);
                    var reread = ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]);
                    root = reread.Entities.OfType<Child>().Single();
                    return reread.Validate().IsValid && root.Choice.TryGetGroupChoice(out group) &&
                        group.TryGetSubset(out subset) && subset.TryGetLeaf(out projected) &&
                        object.ReferenceEquals(projected, reread.Entities.OfType<Leaf>().Single());
                }
            }
            """, ("nested-specialization.exp", $$"""
                SCHEMA nested_specialization;
                ENTITY root_item; END_ENTITY;
                ENTITY leaf SUBTYPE OF (root_item); END_ENTITY;
                ENTITY other; END_ENTITY;
                ENTITY third; END_ENTITY;
                TYPE subset = SELECT (leaf, other); END_TYPE;
                TYPE group_choice = SELECT (subset, third); END_TYPE;
                TYPE broad = SELECT ({{alternatives}}); END_TYPE;
                TYPE narrow = SELECT (subset); END_TYPE;
                ENTITY root ABSTRACT;
                  choice : broad;
                END_ENTITY;
                ENTITY child SUBTYPE OF (root);
                  SELF\root.choice : narrow;
                END_ENTITY;
                END_SCHEMA;
                """));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning).ToArray();
        await Assert.That(diagnostics).IsEmpty().Because(string.Join(Environment.NewLine, diagnostics));
        using var stream = new MemoryStream();
        var emitted = result.OutputCompilation.Emit(stream);
        await Assert.That(emitted.Success).IsTrue().Because(string.Join(Environment.NewLine, emitted.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var check = assembly.GetType("NestedConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        await Assert.That((bool)check.Invoke(null, null)!).IsTrue();
    }
}