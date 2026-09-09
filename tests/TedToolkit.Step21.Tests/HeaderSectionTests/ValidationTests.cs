using TedToolkit.Step21.Tests.ExchangeStructureTests;

namespace TedToolkit.Step21.Tests.HeaderSectionTests;

/// <summary>Proves the standard header EXPRESS cardinalities, bounds, and timestamp form.</summary>
internal sealed class ValidationTests
{
    [Test]
    public async Task Should_accept_exact_header_bounds_and_extended_timestamps()
    {
        var bounded = new string('A', 256);
        var schema = "S" + new string('A', 1023);
        var structure = Structure(
            new FileDescription([bounded], bounded),
            new FileName(bounded, "2024-02-29T24:00:00Z", [bounded], [bounded], bounded, bounded, bounded),
            new FileSchema([schema]));

        var validation = structure.Validate();

        await Assert.That(validation.IsValid).IsTrue();
    }

    [Test]
    public async Task Should_report_every_invalid_required_header_constraint()
    {
        var over = new string('A', 257);
        var structure = Structure(
            new FileDescription([], over),
            new FileName(over, "2026/09/09 12:00:00", [], [], over, over, over),
            new FileSchema([]));

        var validation = structure.Validate();

        using (Assert.Multiple())
        {
            await Assert.That(validation.Failures.Select(item => item.Code)).IsEquivalentTo([
                "P21.HEADER.FILE_DESCRIPTION.DESCRIPTION.CARDINALITY",
                "P21.HEADER.FILE_DESCRIPTION.IMPLEMENTATION_LEVEL.LENGTH",
                "P21.HEADER.FILE_NAME.NAME.LENGTH",
                "P21.HEADER.FILE_NAME.TIME_STAMP.FORMAT",
                "P21.HEADER.FILE_NAME.AUTHOR.CARDINALITY",
                "P21.HEADER.FILE_NAME.ORGANIZATION.CARDINALITY",
                "P21.HEADER.FILE_NAME.PREPROCESSOR_VERSION.LENGTH",
                "P21.HEADER.FILE_NAME.ORIGINATING_SYSTEM.LENGTH",
                "P21.HEADER.FILE_NAME.AUTHORIZATION.LENGTH",
                "P21.HEADER.FILE_SCHEMA.CARDINALITY",
            ]);
            await Assert.That(validation.Failures.All(item => item.Path.StartsWith("Header.", StringComparison.Ordinal)))
                .IsTrue();
        }
    }

    [Test]
    public async Task Should_count_unicode_scalars_and_validate_schema_identifiers()
    {
        var accepted = Structure(
            new FileDescription([string.Concat(Enumerable.Repeat("😀", 256))], "4;1"),
            ValidFileName(),
            new FileSchema(["model_name { 1 0 10303 999 }"]));
        var invalid = Structure(
            new FileDescription([string.Concat(Enumerable.Repeat("😀", 257))], "4;1"),
            ValidFileName(),
            new FileSchema(["BAD-NAME", "MODEL { 1 40 }"]));
        var duplicateAfterCanonicalization = Structure(
            new FileDescription(["duplicate"], "4;1"),
            ValidFileName(),
            new FileSchema(["model_name", "MODEL_NAME"]));
        var distinctObjectIdentifiers = Structure(
            new FileDescription(["versions"], "4;1"),
            ValidFileName(),
            new FileSchema(["MODEL { 1 0 }", "MODEL { 1 1 }"]));

        using (Assert.Multiple())
        {
            await Assert.That(accepted.Validate().IsValid).IsTrue();
            await Assert.That(distinctObjectIdentifiers.Validate().IsValid).IsTrue();
            await Assert.That(invalid.Validate().Failures.Select(item => item.Code)).IsEquivalentTo([
                "P21.HEADER.FILE_DESCRIPTION.DESCRIPTION.LENGTH",
                "P21.HEADER.FILE_SCHEMA.IDENTIFIER.FORMAT",
                "P21.HEADER.FILE_SCHEMA.IDENTIFIER.FORMAT",
            ]);
            await Assert.That(duplicateAfterCanonicalization.Validate().Failures.Select(item => item.Code))
                .IsEquivalentTo(["P21.HEADER.FILE_SCHEMA.IDENTIFIER.DUPLICATE"]);
        }
    }

    [Test]
    public async Task Should_reject_invalid_read_and_write_headers_atomically()
    {
        var descriptor = new TestSchemaDescriptor("TEST_SCHEMA");
        var read = Assert.Throws<ExchangeStructureReadValidationException>(() => ExchangeStructure.Read(
            new StringReader("""
                ISO-10303-21;
                HEADER;
                FILE_DESCRIPTION((),'4;1');
                FILE_NAME('x','not-a-time',(),(),'p','s','a');
                FILE_SCHEMA(('TEST_SCHEMA'));
                ENDSEC;
                DATA;
                ENDSEC;
                END-ISO-10303-21;
                """),
            [descriptor]));
        var writable = Structure(
            new FileDescription(["valid"], "4;1"),
            new FileName("x", "not-a-time", ["author"], ["organization"], "p", "s", "a"),
            new FileSchema(["TEST_SCHEMA"]));
        var destination = new StringWriter();
        var write = Assert.Throws<ExchangeStructureWriteValidationException>(() => writable.Write(destination));

        using (Assert.Multiple())
        {
            await Assert.That(read.ValidationResult.Failures.Select(item => item.Code)).IsEquivalentTo([
                "P21.HEADER.FILE_DESCRIPTION.DESCRIPTION.CARDINALITY",
                "P21.HEADER.FILE_NAME.TIME_STAMP.FORMAT",
                "P21.HEADER.FILE_NAME.AUTHOR.CARDINALITY",
                "P21.HEADER.FILE_NAME.ORGANIZATION.CARDINALITY",
            ]);
            await Assert.That(write.ValidationResult.Failures.Select(item => item.Code))
                .Contains("P21.HEADER.FILE_NAME.TIME_STAMP.FORMAT");
            await Assert.That(destination.ToString()).IsEmpty();
        }
    }

    private static ExchangeStructure Structure(
        FileDescription description,
        FileName fileName,
        FileSchema schema) => new(
            new HeaderSection(description, fileName, schema),
            [new TestSchemaDescriptor(schema.SchemaIdentifiers.FirstOrDefault() ?? "TEST_SCHEMA")]);

    private static FileName ValidFileName() => new(
        "model.p21",
        "2026-09-09T12:00:00+08:00",
        ["author"],
        ["organization"],
        "preprocessor",
        "system",
        "authorization");
}
