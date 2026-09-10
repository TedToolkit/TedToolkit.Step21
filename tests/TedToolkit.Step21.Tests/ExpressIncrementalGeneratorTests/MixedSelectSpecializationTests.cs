// -----------------------------------------------------------------------
// <copyright file="MixedSelectSpecializationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Projects entity-only specializations into SELECT domains that also admit aggregate values.</summary>
internal sealed class MixedSelectSpecializationTests
{
    /// <summary>An unused aggregate alternative must not invalidate a lossless entity projection.</summary>
    [Test]
    public async Task Should_project_entity_redeclarations_into_mixed_select_domains()
    {
        var result = GeneratorHostTests.Run("""
            using TedToolkit.Step21.Schemas.MixedSpecialization;
            internal static class MixedConsumer
            {
                internal static bool Check()
                {
                    var leaf = new Leaf();
                    var owner = new Child(leaf, Narrow.FromLeaf(leaf));
                    IRoot root = owner;
                    return root.Direct.TryGetItem(out var direct) && object.ReferenceEquals(direct, leaf) &&
                        root.Selected.TryGetItem(out var selected) && object.ReferenceEquals(selected, leaf);
                }
            }
            """, ("mixed.exp", """
                SCHEMA mixed_specialization;
                ENTITY item; END_ENTITY;
                ENTITY leaf SUBTYPE OF (item); END_ENTITY;
                TYPE item_set = SET [1:?] OF item; END_TYPE;
                TYPE broad = SELECT (item, item_set); END_TYPE;
                TYPE narrow = SELECT (leaf); END_TYPE;
                ENTITY root ABSTRACT;
                  direct : broad;
                  selected : broad;
                END_ENTITY;
                ENTITY child SUBTYPE OF (root);
                  SELF\root.direct : leaf;
                  SELF\root.selected : narrow;
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
        var check = assembly.GetType("MixedConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        await Assert.That((bool)check.Invoke(null, null)!).IsTrue();
    }
}
