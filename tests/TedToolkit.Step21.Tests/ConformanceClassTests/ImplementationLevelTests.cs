using System.IO.Compression;
using System.Text;

using TedToolkit.Step21.Tests.ExchangeStructureTests;

namespace TedToolkit.Step21.Tests.ConformanceClassTests;

/// <summary>Proves ISO 10303-21:2016 clauses 4.3 and 8.2.2 implementation-level boundaries.</summary>
internal sealed class ImplementationLevelTests
{
    [Test]
    [Arguments("4;1")]
    [Arguments("4;2")]
    [Arguments("4;3")]
    [Arguments("3;1")]
    [Arguments("2;1")]
    public async Task Should_read_write_and_reread_each_declared_level_when_its_restrictions_hold(string level)
    {
        var descriptor = new TestSchemaDescriptor("TEST_SCHEMA");
        var structure = ExchangeStructure.Read(new StringReader(Exchange(level)), [descriptor]);
        var destination = new StringWriter();

        structure.Write(destination);
        var reread = ExchangeStructure.Read(new StringReader(destination.ToString()), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(structure.Header.FileDescription.ImplementationLevel).IsEqualTo(level);
            await Assert.That(reread.Header.FileDescription.ImplementationLevel).IsEqualTo(level);
            await Assert.That(destination.ToString()).Contains($"FILE_DESCRIPTION(('levels'),'{level}');");
        }
    }

    [Test]
    public async Task Should_round_trip_the_distinguishing_class_2_and_class_3_facilities()
    {
        var descriptor = new TestSchemaDescriptor("TEST_SCHEMA");
        var class2 = ExchangeStructure.Read(new StringReader(Exchange(
            "4;2",
            "REFERENCE;#1=<external.p21#root>;ENDSEC;")), [descriptor]);
        var class3 = ExchangeStructure.Read(new StringReader(Exchange(
            "4;3",
            "ANCHOR;<value>=@2;ENDSEC;REFERENCE;@2=<external.p21#value>;ENDSEC;")), [descriptor]);
        var class2Output = new StringWriter();
        var class3Output = new StringWriter();

        class2.Write(class2Output);
        class3.Write(class3Output);
        var class2Reread = ExchangeStructure.Read(new StringReader(class2Output.ToString()), [descriptor]);
        var class3Reread = ExchangeStructure.Read(new StringReader(class3Output.ToString()), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(class2Reread.References.Single().Kind)
                .IsEqualTo(Part21ReferenceKind.EntityInstance);
            await Assert.That(class3Reread.References.Single().Kind)
                .IsEqualTo(Part21ReferenceKind.ValueInstance);
            await Assert.That(class3Reread.Anchors.Single().Item.Kind)
                .IsEqualTo(ParameterValueKind.ValueInstance);
            await Assert.That(class2Output.ToString()).Contains("#1=<external.p21#root>;");
            await Assert.That(class3Output.ToString()).Contains("<value>=@2;");
        }
    }

    [Test]
    public async Task Should_round_trip_user_defined_data_entities_through_an_agreed_descriptor()
    {
        var descriptor = new UserDefinedDescriptor();
        var structure = ExchangeStructure.Read(
            new StringReader(Exchange("4;1", data: "DATA;#1=!CUSTOM('agreed');ENDSEC;")),
            [descriptor]);
        var destination = new StringWriter();

        structure.Write(destination);
        var reread = ExchangeStructure.Read(new StringReader(destination.ToString()), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(structure.Entities.OfType<UserDefinedDataEntity>().Single().Text)
                .IsEqualTo("agreed");
            await Assert.That(destination.ToString()).Contains("#1=!CUSTOM('agreed');");
            await Assert.That(reread.Entities.OfType<UserDefinedDataEntity>().Single().Text)
                .IsEqualTo("agreed");
        }
    }

    [Test]
    public async Task Should_create_named_data_sections_through_the_public_model()
    {
        var descriptor = new UserDefinedDescriptor();
        var structure = Structure("4;1", descriptor);
        structure.DataSections.Clear();
        var first = new DataSection(descriptor.Name, "FIRST");
        var second = new DataSection(descriptor.Name, "SECOND");
        structure.DataSections.Add(first);
        structure.DataSections.Add(second);
        structure.Add(first, new UserDefinedDataEntity { Text = "one" });
        structure.Add(second, new UserDefinedDataEntity { Text = "two" });
        var destination = new StringWriter();

        structure.Write(destination);
        var reread = ExchangeStructure.Read(new StringReader(destination.ToString()), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(reread.DataSections.Select(section => section.Name!))
                .IsEquivalentTo(["FIRST", "SECOND"]);
            await Assert.That(reread.Entities.OfType<UserDefinedDataEntity>().Select(entity => entity.Text))
                .IsEquivalentTo(["one", "two"]);
            await Assert.That(destination.ToString()).Contains("DATA('FIRST',('TEST_SCHEMA'));");
            await Assert.That(destination.ToString()).Contains("DATA('SECOND',('TEST_SCHEMA'));");
        }
    }

    [Test]
    [Arguments("1;1")]
    [Arguments("2;2")]
    [Arguments("3;2")]
    [Arguments("4;4")]
    [Arguments("edition3")]
    public async Task Should_reject_an_unknown_or_obsolete_level_before_model_publication(string level)
    {
        var failure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange(level)),
            [new TestSchemaDescriptor("TEST_SCHEMA")]));

        using (Assert.Multiple())
        {
            await Assert.That(failure.Diagnostics).HasSingleItem();
            await Assert.That(failure.Diagnostics[0].Code).IsEqualTo("P21-CONFORMANCE-IMPLEMENTATION-LEVEL");
            await Assert.That(failure.Diagnostics[0].SourceLocation).IsNotNull();
        }
    }

    [Test]
    public async Task Should_reject_class_2_and_3_facilities_declared_as_a_lower_class()
    {
        var class2Failure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("4;1", "REFERENCE;#1=<external.p21#root>;ENDSEC;")),
            [new TestSchemaDescriptor("TEST_SCHEMA")]));
        var class3Failure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange(
                "4;2",
                "ANCHOR;<constant>=@PI;ENDSEC;")),
            [new ConstantDescriptor()]));
        var class3ReferenceFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("4;2", "REFERENCE;@1=<external.p21#value>;ENDSEC;")),
            [new TestSchemaDescriptor("TEST_SCHEMA")]));

        using (Assert.Multiple())
        {
            await Assert.That(class2Failure.Diagnostics.Select(item => item.Code))
                .IsEquivalentTo(["P21-CONFORMANCE-IMPLEMENTATION-LEVEL"]);
            await Assert.That(class3Failure.Diagnostics.Select(item => item.Code))
                .IsEquivalentTo(["P21-CONFORMANCE-IMPLEMENTATION-LEVEL"]);
            await Assert.That(class3ReferenceFailure.Diagnostics.Select(item => item.Code))
                .IsEquivalentTo(["P21-CONFORMANCE-IMPLEMENTATION-LEVEL"]);
        }
    }

    [Test]
    public async Task Should_require_class_2_for_a_multi_file_zip_structure()
    {
        const string identity = "https://example.test/package.zip";
        var invalidProvider = new StaticResourceProvider(new Part21ResourceContent(
            new Uri(identity),
            Part21ResourceContentKind.ZipArchive,
            CreateZip(Exchange("4;1", "ANCHOR;<root>=$;ENDSEC;"))));
        var validProvider = new StaticResourceProvider(new Part21ResourceContent(
            new Uri(identity),
            Part21ResourceContentKind.ZipArchive,
            CreateZip(Exchange("4;2", "ANCHOR;<root>=$;ENDSEC;"))));
        var singleFileProvider = new StaticResourceProvider(new Part21ResourceContent(
            new Uri(identity),
            Part21ResourceContentKind.ZipArchive,
            CreateZip(Exchange("4;1", "ANCHOR;<root>=$;ENDSEC;"), includePayload: false)));
        var source = Exchange("4;2", $"REFERENCE;#1=<{identity}#root>;ENDSEC;");

        var failure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(source),
            [new TestSchemaDescriptor("TEST_SCHEMA")],
            new ExchangeStructureReadOptions(resourceProvider: invalidProvider)));
        var valid = ExchangeStructure.Read(
            new StringReader(source),
            [new TestSchemaDescriptor("TEST_SCHEMA")],
            new ExchangeStructureReadOptions(resourceProvider: validProvider));
        var singleFile = ExchangeStructure.Read(
            new StringReader(source),
            [new TestSchemaDescriptor("TEST_SCHEMA")],
            new ExchangeStructureReadOptions(resourceProvider: singleFileProvider));

        using (Assert.Multiple())
        {
            await Assert.That(invalidProvider.CallCount).IsEqualTo(1);
            await Assert.That(failure.Diagnostics).HasSingleItem();
            await Assert.That(failure.Diagnostics[0].Code)
                .IsEqualTo("P21-CONFORMANCE-IMPLEMENTATION-LEVEL");
            await Assert.That(failure.Diagnostics[0].Message).Contains("multi-file ZIP");
            await Assert.That(valid.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Null);
            await Assert.That(singleFile.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Null);
        }
    }

    [Test]
    public async Task Should_enforce_edition_2_and_edition_1_compatibility_restrictions()
    {
        var edition2Anchor = Capture("3;1", "ANCHOR;<root>=1;ENDSEC;");
        var edition2Utf8 = Capture("3;1", string.Empty, description: "等级");
        var edition2DataUtf8 = Capture(
            "3;1",
            string.Empty,
            data: "DATA;#1=THING('等级');ENDSEC;");
        var edition1NamedData = Capture(
            "2;1",
            string.Empty,
            data: "DATA('main',('TEST_SCHEMA'));ENDSEC;");
        var edition1Population = Capture(
            "2;1",
            string.Empty,
            additionalHeader: "FILE_POPULATION('TEST_SCHEMA','SECTION_BOUNDARY',$);");
        var edition1Language = Capture(
            "2;1",
            string.Empty,
            additionalHeader: "SECTION_LANGUAGE($,'eng');");
        var edition1Context = Capture(
            "2;1",
            string.Empty,
            additionalHeader: "SECTION_CONTEXT($,('design'));");

        using (Assert.Multiple())
        {
            await Assert.That(edition2Anchor.Diagnostics).HasSingleItem();
            await Assert.That(edition2Utf8.Diagnostics).HasSingleItem();
            await Assert.That(edition2DataUtf8.Diagnostics).HasSingleItem();
            await Assert.That(edition1NamedData.Diagnostics).HasSingleItem();
            await Assert.That(edition1Population.Diagnostics).HasSingleItem();
            await Assert.That(edition1Language.Diagnostics).HasSingleItem();
            await Assert.That(edition1Context.Diagnostics).HasSingleItem();
            await Assert.That(new[]
            {
                edition2Anchor,
                edition2Utf8,
                edition2DataUtf8,
                edition1NamedData,
                edition1Population,
                edition1Language,
                edition1Context,
            }.SelectMany(failure => failure.Diagnostics).All(diagnostic =>
                diagnostic.Code == "P21-CONFORMANCE-IMPLEMENTATION-LEVEL")).IsTrue();
        }
    }

    [Test]
    public async Task Should_reject_invalid_writer_level_combinations_before_destination_publication()
    {
        var descriptor = new TestSchemaDescriptor("TEST_SCHEMA");
        var class1 = Structure("4;1", descriptor);
        class1.References.Add(new Part21Reference(
            new EntityInstanceName("1"),
            new Part21Resource("external.p21#root")));
        var edition1 = Structure("2;1", descriptor);
        edition1.FilePopulations.Add(new SchemaPopulationDefinition(
            descriptor.Name,
            SchemaPopulationDetermination.SectionBoundary));
        edition1.SectionLanguages.Add(new SectionLanguage(null, "eng"));
        edition1.SectionContexts.Add(new SectionContext(null, ["design"]));
        var first = new ProbeWriter();
        var second = new ProbeWriter();
        var edition2Descriptor = new ProjectionDescriptor(ParameterValue.FromString("等级"));
        var edition2 = Structure("3;1", edition2Descriptor);
        edition2.Add(edition2.DataSections[0], new ProjectionEntity());
        var third = new ProbeWriter();

        var class1Failure = Assert.Throws<ExchangeStructureCapabilityException>(() => class1.Write(first));
        var edition1Failure = Assert.Throws<ExchangeStructureCapabilityException>(() => edition1.Write(second));
        var edition2Utf8Failure = Assert.Throws<ExchangeStructureCapabilityException>(() => edition2.Write(
            third,
            new ExchangeStructureWriteOptions(
                [],
                Part21ProcessingLimits.Default,
                Part21StringEncoding.Utf8)));

        using (Assert.Multiple())
        {
            await Assert.That(class1Failure.Diagnostics.Select(item => item.Code))
                .IsEquivalentTo(["P21-CONFORMANCE-IMPLEMENTATION-LEVEL"]);
            await Assert.That(edition1Failure.Diagnostics.Select(item => item.Code))
                .IsEquivalentTo([
                    "P21-CONFORMANCE-IMPLEMENTATION-LEVEL",
                    "P21-CONFORMANCE-IMPLEMENTATION-LEVEL",
                    "P21-CONFORMANCE-IMPLEMENTATION-LEVEL",
                ]);
            await Assert.That(edition2Utf8Failure.Diagnostics.Select(item => item.Code))
                .IsEquivalentTo(["P21-CONFORMANCE-IMPLEMENTATION-LEVEL"]);
            await Assert.That(first.WriteCount).IsEqualTo(0);
            await Assert.That(second.WriteCount).IsEqualTo(0);
            await Assert.That(third.WriteCount).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments(Part21StringEncoding.Canonical, "éπ😀", "'\\X2\\00E903C0\\X0\\\\X4\\0001F600\\X0\\'")]
    [Arguments(Part21StringEncoding.Utf8, "éπ😀", "'éπ😀'")]
    [Arguments(Part21StringEncoding.X, "é", "'\\X\\E9'")]
    [Arguments(Part21StringEncoding.Iso8859, "éĄ", "'\\S\\i\\PB\\\\S\\!'")]
    [Arguments(Part21StringEncoding.X2, "éπ", "'\\X2\\00E903C0\\X0\\'")]
    [Arguments(Part21StringEncoding.X4, "é😀", "'\\X4\\000000E90001F600\\X0\\'")]
    public async Task Should_write_and_reread_every_declared_string_encoding(
        Part21StringEncoding encoding,
        string description,
        string expectedToken)
    {
        var descriptor = new TestSchemaDescriptor("TEST_SCHEMA");
        var structure = Structure("4;1", descriptor, description);
        var destination = new StringWriter();

        structure.Write(destination, new ExchangeStructureWriteOptions(encoding));
        var reread = ExchangeStructure.Read(new StringReader(destination.ToString()), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(destination.ToString()).Contains($"FILE_DESCRIPTION(({expectedToken}),'4;1');");
            await Assert.That(reread.Header.FileDescription.Description.Single()).IsEqualTo(description);
        }
    }

    [Test]
    [Arguments(Part21StringEncoding.Canonical)]
    [Arguments(Part21StringEncoding.Utf8)]
    [Arguments(Part21StringEncoding.X)]
    [Arguments(Part21StringEncoding.Iso8859)]
    [Arguments(Part21StringEncoding.X2)]
    [Arguments(Part21StringEncoding.X4)]
    public async Task Should_use_the_mandatory_x_encoding_for_control_characters(
        Part21StringEncoding encoding)
    {
        var descriptor = new TestSchemaDescriptor("TEST_SCHEMA");
        var structure = Structure("4;1", descriptor, "line\nend\u007f");
        var destination = new StringWriter();

        structure.Write(destination, new ExchangeStructureWriteOptions(encoding));
        var output = destination.ToString();
        var reread = ExchangeStructure.Read(new StringReader(output), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(output).Contains("'line\\X\\0Aend\\X\\7F'");
            await Assert.That(reread.Header.FileDescription.Description.Single()).IsEqualTo("line\nend\u007f");
        }
    }

    [Test]
    public async Task Should_ignore_print_and_raw_control_characters_inside_string_tokens()
    {
        var descriptor = new TestSchemaDescriptor("TEST_SCHEMA");
        var source = Exchange("4;1", description: "a\\N\\b\\F\\c\r\n\td");

        var structure = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        var destination = new StringWriter();
        structure.Write(destination);
        var reread = ExchangeStructure.Read(new StringReader(destination.ToString()), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(structure.Header.FileDescription.Description.Single()).IsEqualTo("abcd");
            await Assert.That(destination.ToString()).Contains("FILE_DESCRIPTION(('abcd'),'4;1');");
            await Assert.That(reread.Header.FileDescription.Description.Single()).IsEqualTo("abcd");
        }
    }

    [Test]
    public async Task Should_reject_print_controls_anywhere_in_anchor_or_reference_sections()
    {
        var descriptor = new TestSchemaDescriptor("TEST_SCHEMA");
        var invalidSources = new[]
        {
            Exchange("4;1", optionalSection: "ANCHOR;\\N\\<a>=1;ENDSEC;"),
            Exchange("4;1", optionalSection: "ANCHOR;<a>='x\\F\\y';ENDSEC;"),
            Exchange("4;1", optionalSection: "ANCHOR;<a>=\"0\\N\\\";ENDSEC;"),
            Exchange("4;2", optionalSection: "REFERENCE;\\N\\#1=<child.p21#item>;ENDSEC;"),
        };

        var failures = invalidSources.Select(source =>
            Assert.Throws<ExchangeStructureSyntaxException>(() =>
                ExchangeStructure.Read(new StringReader(source), [descriptor]))).ToArray();
        var escapedLiteral = ExchangeStructure.Read(
            new StringReader(Exchange("4;1", optionalSection: "ANCHOR;<literal>='\\\\N\\\\';ENDSEC;")),
            [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(failures.SelectMany(failure => failure.Diagnostics).Select(item => item.Code))
                .IsEquivalentTo(Enumerable.Repeat("P21-SYNTAX-PRINT-CONTROL-CONTEXT", invalidSources.Length));
            await Assert.That(escapedLiteral.Anchors.Single().Item.TryGetString(out var literal)).IsTrue();
            await Assert.That(literal).IsEqualTo("\\N\\");
        }
    }

    [Test]
    [Arguments("a'\r'b", "a'b")]
    [Arguments("a\\\r\\b", "a\\b")]
    [Arguments("a\\\rN\t\\b\\F\n\\c", "abc")]
    [Arguments("\\\rX\t\\E\n9", "é")]
    [Arguments("\\P\rB\t\\\\S\n\\!", "Ą")]
    [Arguments("\\X\r2\t\\03\nC0\\X\r0\\", "π")]
    [Arguments("\\X\r4\t\\0001\nF600\\X\r0\\", "😀")]
    public async Task Should_ignore_raw_controls_within_every_string_directive(
        string storedContents,
        string expected)
    {
        var descriptor = new TestSchemaDescriptor("TEST_SCHEMA");
        var structure = ExchangeStructure.Read(
            new StringReader(Exchange("4;1", description: storedContents)),
            [descriptor]);
        var destination = new StringWriter();

        structure.Write(destination);
        var reread = ExchangeStructure.Read(new StringReader(destination.ToString()), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(structure.Header.FileDescription.Description.Single()).IsEqualTo(expected);
            await Assert.That(reread.Header.FileDescription.Description.Single()).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task Should_apply_the_selected_string_encoding_inside_nested_projected_values()
    {
        var descriptor = new ProjectionDescriptor(ParameterValue.FromTyped(
            "WRAPPED",
            ParameterValue.FromAggregate([ParameterValue.FromString("é😀")])));
        var structure = Structure("4;1", descriptor);
        structure.Add(structure.DataSections[0], new ProjectionEntity());
        var destination = new StringWriter();

        structure.Write(destination, new ExchangeStructureWriteOptions(Part21StringEncoding.X4));

        await Assert.That(destination.ToString()).Contains(
            "WRAPPED(('\\X4\\000000E90001F600\\X0\\'))");
    }

    [Test]
    public async Task Should_reject_unrepresentable_selected_string_encodings_atomically()
    {
        var descriptor = new TestSchemaDescriptor("TEST_SCHEMA");
        var xDestination = new ProbeWriter();
        var isoDestination = new ProbeWriter();
        var x2Destination = new ProbeWriter();
        var invalidScalarDestination = new ProbeWriter();

        var xFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => Structure(
            "4;1",
            descriptor,
            "π").Write(xDestination, new ExchangeStructureWriteOptions(Part21StringEncoding.X)));
        var isoFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => Structure(
            "4;1",
            descriptor,
            "😀").Write(isoDestination, new ExchangeStructureWriteOptions(Part21StringEncoding.Iso8859)));
        var x2Failure = Assert.Throws<ExchangeStructureCapabilityException>(() => Structure(
            "4;1",
            descriptor,
            "😀").Write(x2Destination, new ExchangeStructureWriteOptions(Part21StringEncoding.X2)));
        var x2ReadFailure = Assert.Throws<FormatException>(() =>
            Part21LexicalValueDecoder.DecodeString("'\\X2\\D83DDE00\\X0\\'"));
        var invalidScalarFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => Structure(
            "4;1",
            descriptor,
            "\uD800").Write(invalidScalarDestination));
        var invalidScalarReadFailure = Assert.Throws<FormatException>(() =>
            Part21LexicalValueDecoder.DecodeString("'\uD800'"));

        using (Assert.Multiple())
        {
            await Assert.That(xFailure.Diagnostics.Single().Code).IsEqualTo("P21-CAP-STRING-ENCODING");
            await Assert.That(isoFailure.Diagnostics.Single().Code).IsEqualTo("P21-CAP-STRING-ENCODING");
            await Assert.That(x2Failure.Diagnostics.Single().Code).IsEqualTo("P21-CAP-STRING-ENCODING");
            await Assert.That(x2ReadFailure.Message).Contains("BMP Unicode scalar");
            await Assert.That(invalidScalarFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-STRING-UNICODE-SCALAR");
            await Assert.That(invalidScalarReadFailure.Message).Contains("UCS scalar");
            await Assert.That(xDestination.WriteCount).IsEqualTo(0);
            await Assert.That(isoDestination.WriteCount).IsEqualTo(0);
            await Assert.That(x2Destination.WriteCount).IsEqualTo(0);
            await Assert.That(invalidScalarDestination.WriteCount).IsEqualTo(0);
            await Assert.That(() => new ExchangeStructureWriteOptions(
                    [],
                    Part21ProcessingLimits.Default,
                    (Part21StringEncoding)99))
                .Throws<ArgumentOutOfRangeException>();
        }
    }

    [Test]
    public async Task Should_apply_shared_item_and_nesting_limits_before_model_binding()
    {
        var itemFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("4;1", "ANCHOR;<items>=(1,2);ENDSEC;")),
            [new TestSchemaDescriptor("TEST_SCHEMA")],
            ExchangeStructureReadOptions.WithProcessingLimits(new Part21ProcessingLimits(maximumItemCount: 1))));
        var depthFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("4;1", "ANCHOR;<nested>=((1));ENDSEC;")),
            [new TestSchemaDescriptor("TEST_SCHEMA")],
            ExchangeStructureReadOptions.WithProcessingLimits(new Part21ProcessingLimits(maximumNestingDepth: 1))));

        using (Assert.Multiple())
        {
            await Assert.That(itemFailure.Diagnostics.Single().Code).IsEqualTo("P21-PROCESSING-LIMIT-ITEM");
            await Assert.That(depthFailure.Diagnostics.Single().Code).IsEqualTo("P21-PROCESSING-LIMIT-NESTING");
            await Assert.That(itemFailure.Diagnostics.Single().SourceLocation).IsNotNull();
            await Assert.That(depthFailure.Diagnostics.Single().SourceLocation).IsNotNull();
        }
    }

    [Test]
    public async Task Should_enforce_declared_schema_section_and_instance_limits_on_read_and_write()
    {
        var limits = new Part21ProcessingLimits(maximumItemCount: 1);
        var descriptor = new TestSchemaDescriptor("TEST_SCHEMA");
        var readOptions = ExchangeStructureReadOptions.WithProcessingLimits(limits);
        var schemaReadFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("4;1").Replace(
                "FILE_SCHEMA(('TEST_SCHEMA'));",
                "FILE_SCHEMA(('TEST_SCHEMA','SECOND_SCHEMA'));",
                StringComparison.Ordinal)),
            [descriptor],
            readOptions));
        var sectionReadFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("4;1", data: "DATA('A',('TEST_SCHEMA'));ENDSEC;DATA('B',('TEST_SCHEMA'));ENDSEC;")),
            [descriptor],
            readOptions));
        var instanceReadFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("4;1", data: "DATA;#1=THING();#2=THING();ENDSEC;")),
            [descriptor],
            readOptions));

        var secondDescriptor = new TestSchemaDescriptor("SECOND_SCHEMA");
        var schemaStructure = new ExchangeStructure(
            new HeaderSection(
                new FileDescription(["limits"], "4;1"),
                new FileName("limits.p21", "2026-09-08T00:00:00+08:00", [""], [""], "tests", "tests", ""),
                new FileSchema([descriptor.Name.Value, secondDescriptor.Name.Value])),
            [descriptor, secondDescriptor]);
        schemaStructure.DataSections.Add(new DataSection(descriptor.Name, "primary"));
        var sectionStructure = Structure("4;1", descriptor);
        sectionStructure.DataSections.Clear();
        sectionStructure.DataSections.Add(new DataSection(descriptor.Name, "first"));
        sectionStructure.DataSections.Add(new DataSection(descriptor.Name, "second"));
        var instanceDescriptor = new ProjectionDescriptor(ParameterValue.FromInteger(1));
        var instanceStructure = Structure("4;1", instanceDescriptor);
        instanceStructure.Add(instanceStructure.DataSections[0], new ProjectionEntity());
        instanceStructure.Add(instanceStructure.DataSections[0], new ProjectionEntity());
        var schemaDestination = new ProbeWriter();
        var sectionDestination = new ProbeWriter();
        var instanceDestination = new ProbeWriter();

        var schemaWriteFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => schemaStructure.Write(
            schemaDestination,
            new ExchangeStructureWriteOptions(limits, Part21StringEncoding.Canonical)));
        var sectionWriteFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => sectionStructure.Write(
            sectionDestination,
            new ExchangeStructureWriteOptions(limits, Part21StringEncoding.Canonical)));
        var instanceWriteFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => instanceStructure.Write(
            instanceDestination,
            new ExchangeStructureWriteOptions(limits, Part21StringEncoding.Canonical)));

        using (Assert.Multiple())
        {
            await Assert.That(schemaReadFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-ITEM");
            await Assert.That(sectionReadFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-ITEM");
            await Assert.That(instanceReadFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-ITEM");
            await Assert.That(schemaWriteFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-ITEM");
            await Assert.That(sectionWriteFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-ITEM");
            await Assert.That(instanceWriteFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-ITEM");
            await Assert.That(schemaDestination.WriteCount).IsEqualTo(0);
            await Assert.That(sectionDestination.WriteCount).IsEqualTo(0);
            await Assert.That(instanceDestination.WriteCount).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Should_apply_shared_item_and_nesting_limits_before_write_formatting()
    {
        var itemDescriptor = new ProjectionDescriptor(ParameterValue.FromInteger(1));
        var itemStructure = Structure("4;1", itemDescriptor);
        itemStructure.Anchors.Add(new Part21Anchor(
            new AnchorName("items"),
            ParameterValue.FromAggregate([
                ParameterValue.FromInteger(1),
                ParameterValue.FromInteger(2),
            ])));
        var depthDescriptor = new ProjectionDescriptor(ParameterValue.FromTyped(
            "OUTER",
            ParameterValue.FromAggregate([ParameterValue.FromInteger(1)])));
        var depthStructure = Structure("4;1", depthDescriptor);
        depthStructure.Add(depthStructure.DataSections[0], new ProjectionEntity());
        var itemDestination = new ProbeWriter();
        var depthDestination = new ProbeWriter();

        var itemFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => itemStructure.Write(
            itemDestination,
            new ExchangeStructureWriteOptions(
                [],
                new Part21ProcessingLimits(maximumItemCount: 1),
                Part21StringEncoding.Canonical)));
        var depthFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => depthStructure.Write(
            depthDestination,
            new ExchangeStructureWriteOptions(
                [],
                new Part21ProcessingLimits(maximumNestingDepth: 1),
                Part21StringEncoding.Canonical)));

        using (Assert.Multiple())
        {
            await Assert.That(itemFailure.Diagnostics.Single().Code).IsEqualTo("P21-PROCESSING-LIMIT-ITEM");
            await Assert.That(depthFailure.Diagnostics.Single().Code).IsEqualTo("P21-PROCESSING-LIMIT-NESTING");
            await Assert.That(itemDestination.WriteCount).IsEqualTo(0);
            await Assert.That(depthDestination.WriteCount).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Should_apply_shared_nesting_limits_to_single_entity_writes()
    {
        var descriptor = new ProjectionDescriptor(ParameterValue.FromTyped(
            "OUTER",
            ParameterValue.FromAggregate([ParameterValue.FromInteger(1)])));
        var structure = Structure("4;1", descriptor);
        var entity = new ProjectionEntity();
        structure.Add(structure.DataSections[0], entity);
        var destination = new ProbeWriter();

        var failure = Assert.Throws<ExchangeStructureCapabilityException>(() => structure.WriteEntity(
            destination,
            entity,
            new Part21ProcessingLimits(maximumNestingDepth: 1)));

        using (Assert.Multiple())
        {
            await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-PROCESSING-LIMIT-NESTING");
            await Assert.That(destination.WriteCount).IsEqualTo(0);
        }
    }

    private static ExchangeStructureCapabilityException Capture(
        string level,
        string optionalSection,
        string description = "levels",
        string additionalHeader = "",
        string data = "DATA;ENDSEC;") => Assert.Throws<ExchangeStructureCapabilityException>(() =>
        ExchangeStructure.Read(
            new StringReader(Exchange(level, optionalSection, description, additionalHeader, data)),
            [new TestSchemaDescriptor("TEST_SCHEMA")]));

    private static ExchangeStructure Structure(
        string level,
        SchemaDescriptor descriptor,
        string description = "levels")
    {
        var structure = new ExchangeStructure(
            new HeaderSection(
                new FileDescription([description], level),
                new FileName("levels.p21", "2026-09-08T00:00:00+08:00", [""], [""], "tests", "tests", ""),
                new FileSchema([descriptor.Name.Value])),
            [descriptor]);
        structure.DataSections.Add(new DataSection(descriptor.Name));
        return structure;
    }

    private static string Exchange(
        string level,
        string optionalSection = "",
        string description = "levels",
        string additionalHeader = "",
        string data = "DATA;ENDSEC;") => $$"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('{{description}}'),'{{level}}');
        FILE_NAME('levels.p21','2026-09-08T00:00:00+08:00',(''),(''),'tests','tests','');
        FILE_SCHEMA(('TEST_SCHEMA'));
        {{additionalHeader}}
        ENDSEC;
        {{optionalSection}}
        {{data}}
        END-ISO-10303-21;
        """;

    private static byte[] CreateZip(string root, bool includePayload = true)
    {
        using var destination = new MemoryStream();
        using (var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(
                       archive.CreateEntry("ISO-10303.p21").Open(),
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(root);
            }

            if (includePayload)
            {
                using var payload = archive.CreateEntry("payload.bin").Open();
                payload.WriteByte(1);
            }
        }
        return destination.ToArray();
    }

    private sealed class ConstantDescriptor : TestSchemaDescriptorBase
    {
        protected override bool ContainsConstantValueCore(string name) => name == "PI";
    }

    private sealed class ProjectionDescriptor(ParameterValue value) : TestSchemaDescriptorBase
    {
        protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
            Entity entity) => [new("PROJECTION_ENTITY", [value])];
    }

    private sealed class ProjectionEntity : Entity
    {
        public override IEnumerable<Entity> DirectReferences => [];
    }

    private sealed class UserDefinedDataEntity : Entity
    {
        internal string Text { get; set; } = string.Empty;

        public override IEnumerable<Entity> DirectReferences => [];
    }

    private sealed class UserDefinedDescriptor : TestSchemaDescriptorBase
    {
        protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) =>
            entityNames.Count == 1 && entityNames[0] == "!CUSTOM"
                ? new UserDefinedDataEntity()
                : null;

        protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
            ExchangeStructure structure,
            Entity value,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components)
        {
            if (value is UserDefinedDataEntity entity
                && components.Count == 1
                && components[0].Value.Count == 1
                && components[0].Value[0].TryGetString(out var text))
            {
                entity.Text = text;
                return [];
            }

            return [new Step21Diagnostic(
                "P21-TEST-USER-DEFINED-DATA",
                Step21DiagnosticSeverity.Error,
                "The agreed user-defined entity shape is invalid.")];
        }

        protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
            Entity value) => value is UserDefinedDataEntity entity
                ? [new("!CUSTOM", [ParameterValue.FromString(entity.Text)])]
                : [];
    }

    private abstract class TestSchemaDescriptorBase : SchemaDescriptor
    {
        public override SchemaName Name { get; } = new("TEST_SCHEMA");

        protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;

        protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
            ExchangeStructure structure,
            Entity value,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];

        protected override ValidationResult ValidateCore(
            ExchangeStructure structure,
            IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);

        protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
            ExchangeStructure structure) => [];

        protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
            Entity value) => [];
    }

    private sealed class ProbeWriter : StringWriter
    {
        internal int WriteCount { get; private set; }

        public override void Write(string? value)
        {
            WriteCount++;
            base.Write(value);
        }
    }

    private sealed class StaticResourceProvider(Part21ResourceContent content) : IPart21ResourceProvider
    {
        internal int CallCount { get; private set; }

        public Part21ResourceContent? GetResource(Uri resourceIdentity)
        {
            CallCount++;
            return content;
        }
    }
}