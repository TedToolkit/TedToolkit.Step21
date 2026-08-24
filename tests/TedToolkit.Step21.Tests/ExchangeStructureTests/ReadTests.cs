namespace TedToolkit.Step21.Tests.ExchangeStructureTests;

internal sealed class ReadTests
{
    /// <summary>
    /// Verifies case-insensitive nominal matching and the numeric object-identifier suffix while retaining header text.
    /// </summary>
    [Test]
    [Arguments("CONFIG_CONTROL_DESIGN")]
    [Arguments("config_control_design")]
    [Arguments("CoNfIg_CoNtRoL_DeSiGn")]
    [Arguments("CONFIG_CONTROL_DESIGN { 1 0 10303 203 1 1 1 }")]
    [Arguments("config_control_design {1 0 10303 203 1 1 1}")]
    [Arguments("CONFIG_CONTROL_DESIGN   { 1  0 10303 203 1 1 1 }")]
    public async Task Should_bind_supported_identifier_variants_and_preserve_exact_file_schema_text(
        string identifier)
    {
        var descriptor = new TestSchemaDescriptor("config_control_design");
        var structure = ExchangeStructure.Read(new StringReader(CreateExchange(identifier)), [descriptor]);
        var destination = new StringWriter();

        structure.Write(destination);

        using (Assert.Multiple())
        {
            await Assert.That(structure.Header.FileSchema.SchemaIdentifiers).HasSingleItem();
            await Assert.That(structure.Header.FileSchema.SchemaIdentifiers[0]).IsEqualTo(identifier);
            await Assert.That(structure.Validate().IsValid).IsTrue();
            await Assert.That(destination.ToString()).Contains($"FILE_SCHEMA(('{identifier}'));\n");
        }
    }

    /// <summary>
    /// Verifies other protocols, close names, and malformed object-identifier suffixes fail schema binding atomically.
    /// </summary>
    [Test]
    [Arguments("AUTOMOTIVE_DESIGN")]
    [Arguments("AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF")]
    [Arguments("UNKNOWN_SCHEMA")]
    [Arguments("CONFIG_CONTROL_DESIGNER")]
    [Arguments("CONFIG_CONTROL_DESIGN { }")]
    [Arguments("CONFIG_CONTROL_DESIGN { 1 0 AP203 }")]
    [Arguments("CONFIG_CONTROL_DESIGN { 1.0.10303.203 }")]
    [Arguments("CONFIG_CONTROL_DESIGN { 1 0 10303 203 1 1 1")]
    [Arguments("CONFIG_CONTROL_DESIGN { 1 0 10303 203 1 1 1 } trailing")]
    [Arguments("CONFIG_CONTROL_DESIGN{ 1 0 10303 203 1 1 1 }")]
    [Arguments("CONFIG_CONTROL_DESIGN\t{ 1 0 10303 203 1 1 1 }")]
    public async Task Should_reject_other_and_malformed_schema_identifiers_atomically(string identifier)
    {
        var descriptor = new TestSchemaDescriptor("config_control_design");

        var exception = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(new StringReader(CreateExchange(identifier)), [descriptor]));

        using (Assert.Multiple())
        {
            await Assert.That(exception.Diagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(["P21-BIND-SCHEMA"]);
            await Assert.That(exception.Diagnostics[0].SourceLocation).IsNotNull();
            await Assert.That(exception.Diagnostics[0].SourceLocation!.FilePath).IsEqualTo("<reader>");
        }
    }

    /// <summary>
    /// Verifies descriptors that normalize to one nominal name are rejected before the source is consumed.
    /// </summary>
    [Test]
    public async Task Should_reject_normalized_descriptor_collisions_before_consuming_source()
    {
        var source = new ProbeTextReader();

        var exception = Assert.Throws<ArgumentException>(() => ExchangeStructure.Read(
            source,
            [new TestSchemaDescriptor("CONFIG_CONTROL_DESIGN"), new TestSchemaDescriptor("config_control_design")]));

        using (Assert.Multiple())
        {
            await Assert.That(exception.ParamName).IsEqualTo("schemaDescriptors");
            await Assert.That(source.WasRead).IsFalse();
        }
    }

    private static string CreateExchange(string identifier) => $$"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('schema identifier test'),'3;1');
        FILE_NAME('identifier.p21','2026-08-24T00:00:00',('Author'),('Org'),'Pre','System','Auth');
        FILE_SCHEMA(('{{identifier}}'));
        ENDSEC;
        DATA;
        ENDSEC;
        END-ISO-10303-21;
        """;

    private sealed class ProbeTextReader : TextReader
    {
        internal bool WasRead { get; private set; }

        public override int Read(char[] buffer, int index, int count)
        {
            WasRead = true;
            return 0;
        }
    }
}
