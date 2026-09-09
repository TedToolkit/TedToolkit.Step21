// -----------------------------------------------------------------------
// <copyright file="PhysicalNameMappingTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>
/// Proves explicit Part 21 physical-name inputs without heuristic abbreviation.
/// </summary>
public sealed class PhysicalNameMappingTests
{
    private const string SCHEMA = """
        SCHEMA physical_names;
        TYPE state = ENUMERATION OF (active, inactive);
        END_TYPE;
        TYPE distance = REAL;
        END_TYPE;
        TYPE payload = SELECT (distance, state);
        END_TYPE;
        TYPE distances = LIST [1:?] OF distance;
        END_TYPE;
        ENTITY marker;
          selected : payload;
          status : state;
        END_ENTITY;
        ENTITY marker_two;
          selected : payload;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string MAP = """
        # ISO 10303-21 physical names supplied by the schema-defining document.
        SCHEMA physical_names
        ENTITY marker mrk
        TYPE distance dst
        TYPE state sta
        ENUMERATION state active act
        ENUMERATION state inactive ina
        END_SCHEMA
        """;

    private const string CONSUMER = """
        using System.IO;
        using System.Linq;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.PhysicalNames;
        internal static class PhysicalNameConsumer
        {
            internal static bool Check(bool shortInput, bool enumerationSelected)
            {
                var selected = enumerationSelected
                    ? shortInput ? "STA(.ACT.)" : "STATE(.ACTIVE.)"
                    : shortInput ? "DST(1.0)" : "DISTANCE(1.0)";
                var record = shortInput
                    ? "#1=MRK(" + selected + ",.ACT.);"
                    : "#1=MARKER(" + selected + ",.ACTIVE.);";
                var input = "ISO-10303-21;HEADER;FILE_DESCRIPTION(('physical names'),'3;1');" +
                    "FILE_NAME('physical','2026-09-09T00:00:00',('A'),('O'),'P','S','');" +
                    "FILE_SCHEMA(('physical_names'));ENDSEC;DATA;" + record +
                    "ENDSEC;END-ISO-10303-21;";
                var structure = ExchangeStructure.Read(new StringReader(input),
                    [TedToolkit.Step21.Generated.PhysicalNames.SchemaDescriptor.Instance]);
                var writer = new StringWriter();
                structure.Write(writer);
                var output = writer.ToString();
                var reread = ExchangeStructure.Read(new StringReader(output),
                    [TedToolkit.Step21.Generated.PhysicalNames.SchemaDescriptor.Instance]);
                var marker = reread.Entities.OfType<Marker>().Single();
                if (enumerationSelected)
                {
                    return output.Contains("=MRK(STA(.ACT.),.ACT.)") &&
                        marker.Selected.TryGetState(out var state) && state.Value == "ACTIVE" &&
                        marker.Status.Value == "ACTIVE";
                }

                return output.Contains("=MRK(DST(") && output.Contains("),.ACT.);") &&
                    marker.Selected.TryGetDistance(out var distance) && distance.Value == new RealValue(1, 0) &&
                    marker.Status.Value == "ACTIVE";
            }
        }
        """;

    /// <summary>
    /// Long and explicitly supplied short forms are both readable while projection is canonical-short.
    /// </summary>
    [Test]
    public async Task Should_generate_bidirectional_physical_name_mapping()
    {
        var result = GeneratorHostTests.Run(
            CONSUMER,
            ("schemas/physical.exp", SCHEMA),
            ("schemas/physical.p21map", MAP));
        var descriptor = result.GeneratedSources.Single(source =>
            source.HintName == "ExpressSchema_PHYSICAL_NAMES.g.cs").SourceText.ToString();

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(item => item.Severity == DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(descriptor).Contains("entityNames[0] is \"MARKER\" or \"MRK\"");
            await Assert.That(descriptor).Contains("\"MRK\", [");
            await Assert.That(descriptor).Contains("Type is \"DISTANCE\" or \"DST\"");
            await Assert.That(descriptor).Contains("FromTyped(\"DST\"");
            await Assert.That(descriptor).Contains("Type is \"STATE\" or \"STA\"");
            await Assert.That(descriptor).Contains("FromTyped(\"STA\"");
            await Assert.That(descriptor).Contains("\"ACTIVE\" or \"ACT\"");
            await Assert.That(descriptor).Contains("\"ACTIVE\" => \"ACT\"");
            await Assert.That(descriptor.Split("__ExpressProjectSelectPayload0(").Length - 1).IsEqualTo(3);
        }

        using var stream = new MemoryStream();
        var emitted = result.OutputCompilation.Emit(stream);
        await Assert.That(emitted.Success).IsTrue().Because(string.Join(Environment.NewLine, emitted.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var check = assembly.GetType("PhysicalNameConsumer", throwOnError: true)!
            .GetMethod("Check", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        await Assert.That((bool)check.Invoke(null, [false, false])!).IsTrue();
        await Assert.That((bool)check.Invoke(null, [true, false])!).IsTrue();
        await Assert.That((bool)check.Invoke(null, [false, true])!).IsTrue();
        await Assert.That((bool)check.Invoke(null, [true, true])!).IsTrue();
    }

    /// <summary>
    /// Omitting metadata preserves long-name-only generation and never invents abbreviations.
    /// </summary>
    [Test]
    public async Task Should_preserve_long_names_when_mapping_is_absent()
    {
        var result = GeneratorHostTests.Run(("schemas/physical.exp", SCHEMA));
        var descriptor = result.GeneratedSources.Single(source =>
            source.HintName == "ExpressSchema_PHYSICAL_NAMES.g.cs").SourceText.ToString();

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(descriptor).DoesNotContain("entityNames[0] is \"MARKER\" or");
            await Assert.That(descriptor).DoesNotContain("FromTyped(\"DST\"");
            await Assert.That(descriptor).DoesNotContain("\"ACTIVE\" => \"ACT\"");
        }
    }

    /// <summary>
    /// Malformed, unknown, duplicated, and colliding declarations fail at their map source.
    /// </summary>
    [Test]
    public async Task Should_reject_invalid_mapping_rows_deterministically()
    {
        var cases = new[]
        {
            (Map: "SCHEMA absent\nENTITY marker m\nEND_SCHEMA", Line: 1, Message: "not present"),
            (Map: "SCHEMA physical_names\nENTITY absent m\nEND_SCHEMA", Line: 2, Message: "not declared"),
            (Map: "SCHEMA physical_names\nENTITY marker distance\nEND_SCHEMA", Line: 2, Message: "collides"),
            (Map: "SCHEMA physical_names\nENTITY marker m\nTYPE distance M\nEND_SCHEMA", Line: 3, Message: "collides"),
            (Map: "SCHEMA physical_names\nENUMERATION distance active a\nEND_SCHEMA", Line: 2, Message: "not an enumeration"),
            (Map: "SCHEMA physical_names\nENUMERATION state absent a\nEND_SCHEMA", Line: 2, Message: "not declared"),
            (Map: "SCHEMA physical_names\nENUMERATION state active inactive\nEND_SCHEMA", Line: 2, Message: "ambiguous"),
            (Map: "SCHEMA physical_names\nENUMERATION state active a\nENUMERATION state active b\nEND_SCHEMA", Line: 3, Message: "already has"),
            (Map: "SCHEMA physical_names\nTYPE payload pay\nEND_SCHEMA", Line: 2, Message: "not a simple defined"),
            (Map: "SCHEMA physical_names\nTYPE distances dists\nEND_SCHEMA", Line: 2, Message: "not a simple defined"),
            (Map: "SCHEMA physical_names\nENTITY marker 名称\nEND_SCHEMA", Line: 2, Message: "Expected 'ENTITY"),
            (Map: "SCHEMA physical_names\nENTITY marker\nEND_SCHEMA", Line: 2, Message: "Expected 'ENTITY"),
        };

        foreach (var item in cases)
        {
            var result = GeneratorHostTests.Run(
                ("schemas/physical.exp", SCHEMA),
                ("schemas/invalid.p21map", item.Map));
            var diagnostic = result.Diagnostics.Single(item => item.Id == "STEP21EXP007");
            var span = diagnostic.Location.GetLineSpan();

            using (Assert.Multiple())
            {
                await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
                await Assert.That(span.Path).IsEqualTo("schemas/invalid.p21map");
                await Assert.That(span.StartLinePosition.Line + 1).IsEqualTo(item.Line);
                await Assert.That(span.StartLinePosition.Character + 1).IsEqualTo(1);
                await Assert.That(diagnostic.GetMessage()).Contains(item.Message);
                await Assert.That(result.GeneratedSources).IsEmpty();
            }
        }

        var ordered = GeneratorHostTests.Run(
            ("schemas/physical.exp", SCHEMA),
            ("schemas/ordered.p21map",
                "SCHEMA physical_names\nENTITY absent m\nTYPE absent_type t\nEND_SCHEMA"));
        var orderedDiagnostics = ordered.Diagnostics.Where(item => item.Id == "STEP21EXP007").ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(orderedDiagnostics).Count().IsEqualTo(2);
            await Assert.That(orderedDiagnostics[0].Location.GetLineSpan().StartLinePosition.Line + 1).IsEqualTo(2);
            await Assert.That(orderedDiagnostics[1].Location.GetLineSpan().StartLinePosition.Line + 1).IsEqualTo(3);
            await Assert.That(ordered.GeneratedSources).IsEmpty();
        }
    }

    /// <summary>
    /// A schema may have exactly one map and physical output is independent of machine paths and input order.
    /// </summary>
    [Test]
    public async Task Should_require_one_reproducible_map_per_schema()
    {
        var first = GeneratorHostTests.Run(
            ("C:/agent-a/physical.exp", SCHEMA),
            ("C:/agent-a/physical.p21map", MAP));
        var second = GeneratorHostTests.Run(
            ("D:/agent-b/physical.p21map", MAP),
            ("D:/agent-b/physical.exp", SCHEMA));
        var duplicate = GeneratorHostTests.Run(
            ("schemas/physical.exp", SCHEMA),
            ("schemas/first.p21map", MAP),
            ("schemas/second.p21map", MAP));

        using (Assert.Multiple())
        {
            await Assert.That(first.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(second.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(first.GeneratedSources.Select(source => source.SourceText.ToString()))
                .IsEquivalentTo(second.GeneratedSources.Select(source => source.SourceText.ToString()));
            await Assert.That(duplicate.Diagnostics.Count(item => item.Id == "STEP21EXP007")).IsEqualTo(1);
            await Assert.That(duplicate.GeneratedSources).IsEmpty();
        }
    }
}
