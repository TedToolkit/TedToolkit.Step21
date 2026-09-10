// -----------------------------------------------------------------------
// <copyright file="AggregateSelectSpecializationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Protects live aggregate projections from entity specializations into SELECT domains.</summary>
internal sealed class AggregateSelectSpecializationTests
{
    /// <summary>Aggregate elements reuse numeric and exact named-value specialization with their constraints intact.</summary>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Should_share_scalar_and_named_value_element_projections(bool invalid)
    {
        var result = GeneratorHostTests.Run("""
            using System.IO;
            using System.Linq;
            using System.Numerics;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Schemas.AggregateValues;
            internal static class AggregateValueConsumer
            {
                internal static bool Check(bool invalid)
                {
                    var input = "ISO-10303-21;HEADER;FILE_DESCRIPTION(('values'),'3;1');" +
                        "FILE_NAME('values','2026-09-05T00:00:00',('A'),('O'),'P','S','');" +
                        "FILE_SCHEMA(('aggregate_values'));ENDSEC;DATA;#1=CHILD((7),('" +
                        (invalid ? "" : "ok") + "'));ENDSEC;END-ISO-10303-21;";
                    var descriptor = TedToolkit.Step21.Schemas.AggregateValues.SchemaDescriptor.Instance;
                    try
                    {
                        var structure = ExchangeStructure.Read(new StringReader(input), [descriptor]);
                        var child = structure.Entities.OfType<Child>().Single();
                        IRoot root = child;
                        var numbers = root.Numbers;
                        var codes = root.Codes;
                        if (!numbers.Single().Equals(NumberValue.FromInteger(7)) ||
                            !codes.Single().TryGetCode(out var code) || code.Value != "ok") return false;
                        child.Numbers.Add(new BigInteger(8));
                        child.Codes.Add(new Code("next"));
                        if (numbers.Count != 2 || codes.Count != 2 ||
                            !numbers.Last().Equals(NumberValue.FromInteger(8)) ||
                            !codes.Last().TryGetCode(out code) || code.Value != "next") return false;
                        var output = new StringWriter();
                        structure.Write(output);
                        var reread = ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]);
                        root = reread.Entities.OfType<Child>().Single();
                        return !invalid && reread.Validate().IsValid && root.Numbers.Count == 2 && root.Codes.Count == 2;
                    }
                    catch (ExchangeStructureReadValidationException failure)
                    {
                        return invalid && failure.ValidationResult.Failures.Count > 0;
                    }
                }
            }
            """, ("aggregate-values.exp", """
                SCHEMA aggregate_values;
                TYPE code = STRING;
                WHERE nonempty : LENGTH(SELF) > 0;
                END_TYPE;
                TYPE other = INTEGER; END_TYPE;
                TYPE choice = SELECT (code, other); END_TYPE;
                ENTITY root ABSTRACT;
                  numbers : LIST [1:?] OF NUMBER;
                  codes : LIST [1:?] OF choice;
                END_ENTITY;
                ENTITY child SUBTYPE OF (root);
                  SELF\root.numbers : LIST [1:2] OF INTEGER;
                  SELF\root.codes : LIST [1:2] OF code;
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
        var check = assembly.GetType("AggregateValueConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        await Assert.That((bool)check.Invoke(null, [invalid])!).IsTrue();
    }

    /// <summary>Every aggregate kind retains entity identity and observes edits through an existing parent view.</summary>
    [Test]
    [Arguments("LIST")]
    [Arguments("BAG")]
    [Arguments("SET")]
    [Arguments("ARRAY")]
    public async Task Should_project_entity_elements_into_selects_without_copying_storage(string kind)
    {
        var mutation = kind == "ARRAY"
            ? "child.Items[1] = replacement; child.MixedItems[1] = replacement;"
            : "child.Items.Clear(); child.Items.Add(replacement); child.MixedItems.Clear(); child.MixedItems.Add(replacement);";
        var result = GeneratorHostTests.Run($$"""
            using System.IO;
            using System.Linq;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Schemas.AggregateSelectSpecialization;
            internal static class AggregateConsumer
            {
                internal static bool Check()
                {
                    var input = "ISO-10303-21;HEADER;FILE_DESCRIPTION(('aggregate'),'3;1');" +
                        "FILE_NAME('aggregate','2026-09-04T00:00:00',('A'),('O'),'P','S','');" +
                        "FILE_SCHEMA(('aggregate_select_specialization'));ENDSEC;DATA;" +
                        "#1=LEAF();#2=LEAF();#3=CHILD((#1),(#1));ENDSEC;END-ISO-10303-21;";
                    var descriptor = TedToolkit.Step21.Schemas.AggregateSelectSpecialization.SchemaDescriptor.Instance;
                    var structure = ExchangeStructure.Read(new StringReader(input), [descriptor]);
                    var child = structure.Entities.OfType<Child>().Single();
                    IRoot root = child;
                    var view = root.Items;
                    var mixedView = root.MixedItems;
                    var first = child.Items.Single();
                    if (!view.Single().TryGetItem(out var selected) || !object.ReferenceEquals(selected, first) ||
                        !mixedView.Single().TryGetItem(out var mixed) || !object.ReferenceEquals(mixed, first)) return false;
                    var replacement = structure.Entities.OfType<Leaf>().Single(entity => !object.ReferenceEquals(entity, first));
                    {{mutation}}
                    if (!view.Single().TryGetItem(out selected) || !object.ReferenceEquals(selected, replacement) ||
                        !mixedView.Single().TryGetItem(out mixed) || !object.ReferenceEquals(mixed, replacement)) return false;
                    var output = new StringWriter();
                    structure.Write(output);
                    var reread = ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]);
                    var readChild = reread.Entities.OfType<Child>().Single();
                    IRoot readRoot = readChild;
                    return reread.Validate().IsValid && readRoot.Items.Single().TryGetItem(out selected) &&
                        object.ReferenceEquals(selected, readChild.Items.Single()) &&
                        readRoot.MixedItems.Single().TryGetItem(out mixed) && object.ReferenceEquals(mixed, selected);
                }
            }
            """, ("aggregate-select.exp", $$"""
                SCHEMA aggregate_select_specialization;
                ENTITY item; END_ENTITY;
                ENTITY leaf SUBTYPE OF (item); END_ENTITY;
                ENTITY other; END_ENTITY;
                TYPE items = SET [1:?] OF item; END_TYPE;
                TYPE broad = SELECT (item, other); END_TYPE;
                TYPE mixed = SELECT (item, items); END_TYPE;
                ENTITY root ABSTRACT;
                  items : {{kind}} [1:1] OF broad;
                  mixed_items : {{kind}} [1:1] OF mixed;
                END_ENTITY;
                ENTITY child SUBTYPE OF (root);
                  SELF\root.items : {{kind}} [1:1] OF leaf;
                  SELF\root.mixed_items : {{kind}} [1:1] OF leaf;
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
        var check = assembly.GetType("AggregateConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        await Assert.That((bool)check.Invoke(null, null)!).IsTrue();
    }
}
