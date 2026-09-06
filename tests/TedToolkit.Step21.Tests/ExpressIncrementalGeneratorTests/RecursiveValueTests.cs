// -----------------------------------------------------------------------
// <copyright file="RecursiveValueTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Proves finite recursive EXPRESS values retain their generated nominal carriers.</summary>
internal sealed class RecursiveValueTests
{
    /// <summary>Recursive value containers must retain entity occurrences at every finite nesting depth.</summary>
    [Test]
    public async Task Should_enumerate_entity_references_inside_recursive_values()
    {
        var result = GeneratorHostTests.Run("""
            using System.Linq;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.RecursiveReferences;
            internal static class RecursiveReferenceConsumer
            {
                internal static bool FindsNestedTarget()
                {
                    var target = new Target();
                    var inner = new NodeList(new ExpressList<NodeValue>(0)
                    {
                        NodeValue.FromTarget(target)
                    });
                    var outer = new NodeList(new ExpressList<NodeValue>(0)
                    {
                        NodeValue.FromNodeList(inner)
                    });
                    var holder = new Holder(NodeValue.FromNodeList(outer));
                    return ReferenceEquals(holder.DirectReferences.Single(), target);
                }
            }
            """, ("recursive-references.exp", """
                SCHEMA recursive_references;
                ENTITY target;
                END_ENTITY;
                TYPE node_list = LIST [0:?] OF node_value;
                END_TYPE;
                TYPE node_value = SELECT (target, node_list);
                END_TYPE;
                TYPE outer_list = LIST [0:?] OF outer_value;
                END_TYPE;
                TYPE outer_value = SELECT (outer_list, node_value);
                END_TYPE;
                ENTITY holder;
                  root : node_value;
                END_ENTITY;
                ENTITY outer_holder;
                  root : outer_value;
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
        var found = (bool)assembly.GetType("RecursiveReferenceConsumer", throwOnError: true)!
            .GetMethod("FindsNestedTarget", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        await Assert.That(found).IsTrue();
    }

    /// <summary>The resolver must not omit an otherwise supported aggregate-contained type cycle.</summary>
    [Test]
    public async Task Should_generate_and_execute_recursive_value_carriers()
    {
        var result = GeneratorHostTests.Run("""
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.RecursiveValues;
            internal static class RecursiveConsumer
            {
                internal static int NestedCount()
                {
                    var inner = new Tuple(new ExpressList<AtomOrTuple>(0)
                    {
                        AtomOrTuple.FromAtom(new Atom(new RealValue(3, 0)))
                    });
                    var outer = AtomOrTuple.FromTuple(new Tuple(new ExpressList<AtomOrTuple>(0)
                    {
                        AtomOrTuple.FromTuple(inner)
                    }));
                    return outer.Match(atom => -1, tuple => tuple.Value[0].Match(
                        atom => -2, nested => nested.Value.Count));
                }
            }
            """, ("recursive-values.exp", """
                SCHEMA recursive_values;
                TYPE atom = REAL;
                END_TYPE;
                TYPE tuple = LIST [0:?] OF atom_or_tuple;
                END_TYPE;
                TYPE atom_or_tuple = SELECT (atom, tuple);
                WHERE
                  valid_recursive_value : SELF = SELF;
                END_TYPE;
                ENTITY recursive_holder;
                  selected_value : atom_or_tuple;
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
        var count = (int)assembly.GetType("RecursiveConsumer", throwOnError: true)!
            .GetMethod("NestedCount", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        await Assert.That(count).IsEqualTo(1);
    }
}