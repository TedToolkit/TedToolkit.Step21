using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.AnchorOccurrenceConformanceTests;

internal sealed class RoundTripTests
{
    /// <summary>
    /// ISO 10303-21:2016 clauses 9.1, 9.2 and Annex G: every local anchor value category, ordered tag and UUID
    /// classification remains available through the editable model and canonical write/read boundary.
    /// </summary>
    [Test]
    public async Task Should_round_trip_local_anchor_values_tags_and_uuid_identity()
    {
        const string uuid = "48a0de4c-3c6f-488f-843a-231e08125315";
        var source = $$"""
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('anchor conformance'),'4;3');
            FILE_NAME('anchor.p21','2026-09-06T00:00:00+08:00',(''),(''),'tests','tests','');
            FILE_SCHEMA(('TEST_SCHEMA'));
            ENDSEC;
            ANCHOR;
            <{{uuid}}>=(1,-2.5,'text',._STATE.,"30",$,<foo:?bar>,(),(1,())){_:'root'}{_TAG:2}{TAG_NAME:3}{_label:'primary'}{rank_name:1};
            ENDSEC;
            END-ISO-10303-21;
            """;

        var structure = ExchangeStructure.Read(
            new StringReader(source),
            [new ExchangeStructureTests.TestSchemaDescriptor("TEST_SCHEMA")]);
        var anchor = structure.Anchors.Single();
        var destination = new StringWriter();

        structure.Write(destination);
        var reread = ExchangeStructure.Read(
            new StringReader(destination.ToString()),
            [new ExchangeStructureTests.TestSchemaDescriptor("TEST_SCHEMA")]);

        using (Assert.Multiple())
        {
            await Assert.That(anchor.Name.Value).IsEqualTo(uuid);
            await Assert.That(anchor.Name.IsUuid).IsTrue();
            await Assert.That(anchor.Name.TryGetUuid(out var parsedUuid)).IsTrue();
            await Assert.That(parsedUuid).IsEqualTo(Guid.Parse(uuid));
            await Assert.That(anchor.Item.Kind).IsEqualTo(ParameterValueKind.Aggregate);
            await Assert.That(anchor.Tags.Select(tag => tag.Name).SequenceEqual([
                "_",
                "_TAG",
                "TAG_NAME",
                "_label",
                "rank_name",
            ])).IsTrue();
            await Assert.That(reread.Anchors).HasSingleItem();
            await Assert.That(reread.Anchors[0]).IsEqualTo(anchor);
            await Assert.That(destination.ToString()).Contains(
                $"ANCHOR;\n<{uuid}>=(1,-25.E-1,'text',._STATE.,\"30\",$,<foo:?bar>,(),(1,()))"
                + "{_:'root'}{_TAG:2}{TAG_NAME:3}{_label:'primary'}{rank_name:1};\nENDSEC;\n");
        }
    }

    /// <summary>
    /// ISO 10303-21:2016 clauses 6.4.4, 9.2.1, 9.2.2 and 9.2.7: all occurrence categories retain their
    /// distinct canonical identity while reference associations remain data, not implicit I/O.
    /// </summary>
    [Test]
    public async Task Should_round_trip_all_occurrence_categories_without_resolving_resources()
    {
        var descriptor = CreateConstantDescriptor();
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('occurrence conformance'),'4;3');
            FILE_NAME('occurrences.p21','2026-09-06T00:00:00+08:00',(''),(''),'tests','tests','');
            FILE_SCHEMA(('occurrence_schema'));
            ENDSEC;
            ANCHOR;
            <all>=(#001,@002,#ORIGIN,@CIRCLERATIO);
            ENDSEC;
            REFERENCE;
            #1=<entity.p21#root>;
            @2=<values.p21#pi>;
            ENDSEC;
            END-ISO-10303-21;
            """;

        ExchangeStructure structure;
        try
        {
            structure = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        }
        catch (ExchangeStructureBindingException exception)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                exception.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")), exception);
        }
        var destination = new StringWriter();
        structure.Write(destination);
        var reread = ExchangeStructure.Read(new StringReader(destination.ToString()), [descriptor]);
        _ = structure.Anchors[0].Item.TryGetAggregate(out var values);

        using (Assert.Multiple())
        {
            await Assert.That(values!.Select(value => value.Kind).SequenceEqual([
                ParameterValueKind.EntityInstance,
                ParameterValueKind.ValueInstance,
                ParameterValueKind.ConstantEntity,
                ParameterValueKind.ConstantValue,
            ])).IsTrue();
            await Assert.That(structure.References.Select(reference => reference.Kind).SequenceEqual([
                Part21ReferenceKind.EntityInstance,
                Part21ReferenceKind.ValueInstance,
            ])).IsTrue();
            await Assert.That(destination.ToString()).Contains("<all>=(#1,@2,#ORIGIN,@CIRCLERATIO);");
            await Assert.That(destination.ToString()).Contains(
                "REFERENCE;\n#1=<entity.p21#root>;\n@2=<values.p21#pi>;\nENDSEC;\n");
            await Assert.That(reread.Anchors[0]).IsEqualTo(structure.Anchors[0]);
            await Assert.That(reread.References.SequenceEqual(structure.References)).IsTrue();
        }
    }

    /// <summary>Annex G UUID identity is case-insensitive and cannot define two anchors in one structure.</summary>
    [Test]
    public async Task Should_reject_duplicate_uuid_anchor_names_atomically()
    {
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('duplicate anchor'),'4;3');
            FILE_NAME('duplicate.p21','2026-09-06T00:00:00+08:00',(''),(''),'tests','tests','');
            FILE_SCHEMA(('TEST_SCHEMA'));
            ENDSEC;
            ANCHOR;
            <48a0de4c-3c6f-488f-843a-231e08125315>=1;
            <48A0DE4C-3C6F-488F-843A-231E08125315>=2;
            ENDSEC;
            END-ISO-10303-21;
            """;

        var exception = Assert.Throws<ExchangeStructureBindingException>(() => ExchangeStructure.Read(
            new StringReader(source),
            [new ExchangeStructureTests.TestSchemaDescriptor("TEST_SCHEMA")]));

        using (Assert.Multiple())
        {
            await Assert.That(exception.Diagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(["P21-BIND-ANCHOR-DUPLICATE"]);
            await Assert.That(exception.Diagnostics[0].SourceLocation).IsNotNull();
        }
    }

    /// <summary>Clause 6.4.4 forbids entity and value instance names from sharing the same integer.</summary>
    [Test]
    public async Task Should_reject_overlapping_entity_and_value_occurrence_names_atomically()
    {
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('overlap'),'4;3');
            FILE_NAME('overlap.p21','2026-09-06T00:00:00+08:00',(''),(''),'tests','tests','');
            FILE_SCHEMA(('TEST_SCHEMA'));
            ENDSEC;
            REFERENCE;
            #1=<entities.p21#root>;
            @001=<values.p21#value>;
            ENDSEC;
            END-ISO-10303-21;
            """;

        var exception = Assert.Throws<ExchangeStructureBindingException>(() => ExchangeStructure.Read(
            new StringReader(source),
            [new ExchangeStructureTests.TestSchemaDescriptor("TEST_SCHEMA")]));

        await Assert.That(exception.Diagnostics.Select(diagnostic => diagnostic.Code))
            .Contains("P21-BIND-OCCURRENCE");
    }

    /// <summary>Clause 9.2.7 binds constant occurrence category and name against the first FILE_SCHEMA schema.</summary>
    [Test]
    public async Task Should_reject_wrong_or_unknown_constant_categories_atomically()
    {
        var descriptor = CreateConstantDescriptor();
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('constant mismatch'),'4;3');
            FILE_NAME('constant-mismatch.p21','2026-09-06T00:00:00+08:00',(''),(''),'tests','tests','');
            FILE_SCHEMA(('occurrence_schema'));
            ENDSEC;
            ANCHOR;
            <wrong-entity>=#CIRCLERATIO;
            <wrong-value>=@ORIGIN;
            <missing>=#MISSING;
            ENDSEC;
            END-ISO-10303-21;
            """;

        var exception = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(new StringReader(source), [descriptor]));

        await Assert.That(exception.Diagnostics.Count(diagnostic =>
            diagnostic.Code == "P21-BIND-ANCHOR-CONSTANT")).IsEqualTo(3);
    }

    /// <summary>Clause 9.2.1 resolves a local entity occurrence to the same runtime object owned by the structure.</summary>
    [Test]
    public async Task Should_resolve_and_round_trip_a_local_entity_anchor_by_identity()
    {
        var descriptor = CreateConstantDescriptor();
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('entity anchor'),'4;3');
            FILE_NAME('entity-anchor.p21','2026-09-06T00:00:00+08:00',(''),(''),'tests','tests','');
            FILE_SCHEMA(('occurrence_schema'));
            ENDSEC;
            ANCHOR;
            <root>=#1;
            ENDSEC;
            DATA;
            #1=POINT(1.);
            ENDSEC;
            END-ISO-10303-21;
            """;

        var structure = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        var destination = new StringWriter();
        structure.Write(destination);
        var reread = ExchangeStructure.Read(new StringReader(destination.ToString()), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(structure.Anchors[0].Item.TryGetEntity(out var entity)).IsTrue();
            await Assert.That(entity).IsSameReferenceAs(structure.Entities.Single());
            await Assert.That(reread.Anchors[0].Item.TryGetEntity(out var rereadEntity)).IsTrue();
            await Assert.That(rereadEntity).IsSameReferenceAs(reread.Entities.Single());
            await Assert.That(destination.ToString()).Contains("ANCHOR;\n<root>=#1;\nENDSEC;\nDATA;\n#1=POINT(1.E0);");
        }
    }

    /// <summary>Clause 9 enumeration tokens remain enumeration values when supplied through the public edit model.</summary>
    [Test]
    public async Task Should_round_trip_boolean_and_logical_spellings_as_untyped_anchor_enumerations()
    {
        var descriptor = new ExchangeStructureTests.TestSchemaDescriptor("TEST_SCHEMA");
        var structure = new ExchangeStructure(ExchangeStructureTests.TestHeader.Create("4;1"), [descriptor]);
        structure.Anchors.Add(new Part21Anchor(
            new AnchorName("states"),
            ParameterValue.FromAggregate([
                ParameterValue.FromEnumeration("T"),
                ParameterValue.FromEnumeration("F"),
                ParameterValue.FromEnumeration("U"),
            ])));
        var destination = new StringWriter();

        structure.Write(destination);
        var reread = ExchangeStructure.Read(new StringReader(destination.ToString()), [descriptor]);
        _ = reread.Anchors[0].Item.TryGetAggregate(out var values);

        using (Assert.Multiple())
        {
            await Assert.That(destination.ToString()).Contains("<states>=(.T.,.F.,.U.);");
            await Assert.That(values!.Select(value => value.Kind))
                .IsEquivalentTo([
                    ParameterValueKind.Enumeration,
                    ParameterValueKind.Enumeration,
                    ParameterValueKind.Enumeration,
                ]);
            await Assert.That(reread.Anchors[0]).IsEqualTo(structure.Anchors[0]);
        }
    }

    /// <summary>Table 2 tag spellings that overlap keyword tokens remain tags in the clause 9 context.</summary>
    [Test]
    public async Task Should_round_trip_uppercase_and_low_line_tags_created_by_public_editing()
    {
        var descriptor = new ExchangeStructureTests.TestSchemaDescriptor("TEST_SCHEMA");
        var structure = new ExchangeStructure(ExchangeStructureTests.TestHeader.Create("4;1"), [descriptor]);
        structure.Anchors.Add(new Part21Anchor(
            new AnchorName("tags"),
            ParameterValue.Omitted,
            [
                new Part21AnchorTag("_", ParameterValue.FromString("root")),
                new Part21AnchorTag("_TAG", ParameterValue.FromInteger(2)),
                new Part21AnchorTag("TAG_NAME", ParameterValue.FromInteger(3)),
            ]));
        var destination = new StringWriter();

        structure.Write(destination);
        var reread = ExchangeStructure.Read(new StringReader(destination.ToString()), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(destination.ToString()).Contains(
                "<tags>=${_:'root'}{_TAG:2}{TAG_NAME:3};");
            await Assert.That(reread.Anchors[0].Tags.Select(tag => tag.Name).SequenceEqual([
                "_",
                "_TAG",
                "TAG_NAME",
            ])).IsTrue();
            await Assert.That(reread.Anchors[0]).IsEqualTo(structure.Anchors[0]);
        }
    }

    private static SchemaDescriptor CreateConstantDescriptor()
    {
        const string schema = """
            SCHEMA occurrence_schema;
            CONSTANT
              origin : point := point(0.0);
              circleratio : REAL := 3.14159;
            END_CONSTANT;
            ENTITY point;
              x : REAL;
            END_ENTITY;
            END_SCHEMA;
            """;
        var result = GeneratorHostTests.Run(("schemas/occurrence.exp", schema));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        if (diagnostics.Length > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.AsEnumerable()));

        using var stream = new MemoryStream();
        var emit = result.OutputCompilation.Emit(stream);
        if (!emit.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, emit.Diagnostics));

        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        return (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Schemas.OccurrenceSchema.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
    }
}
