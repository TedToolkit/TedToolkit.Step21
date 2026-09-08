using System.IO.Compression;
using System.Text;
using System.Text.Json;

using TedToolkit.Step21.AnnexF;
using TedToolkit.Step21.IntegrationTests.ExternalCorpus;

namespace TedToolkit.Step21.IntegrationTests.DistributedProcessorSecurityTests;

/// <summary>Proves the shared quota, callback, publication, and Annex F threat boundary.</summary>
internal sealed class SecurityBoundaryTests
{
    [Test]
    public async Task Should_bind_every_security_partition_to_public_defaults_and_evidence()
    {
        var path = Path.Combine(
            RepositoryPaths.FindRoot(),
            "docs",
            "conformance",
            "distributed-processor-security.json");
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var defaults = manifest.RootElement.GetProperty("defaults");
        var threats = manifest.RootElement.GetProperty("threats").EnumerateArray().ToArray();
        var processing = Part21ProcessingLimits.Default;
        var resources = new Part21ResourceLimits();

        using (Assert.Multiple())
        {
            await Assert.That(defaults.GetProperty("maximumInputCharacters").GetInt32())
                .IsEqualTo(processing.MaximumInputCharacters);
            await Assert.That(defaults.GetProperty("maximumOutputCharacters").GetInt32())
                .IsEqualTo(processing.MaximumOutputCharacters);
            await Assert.That(defaults.GetProperty("maximumUriCharacters").GetInt32())
                .IsEqualTo(processing.MaximumUriCharacters);
            await Assert.That(defaults.GetProperty("maximumSignatureCount").GetInt32())
                .IsEqualTo(processing.MaximumSignatureCount);
            await Assert.That(defaults.GetProperty("maximumSignatureBytes").GetInt32())
                .IsEqualTo(processing.MaximumSignatureBytes);
            await Assert.That(defaults.GetProperty("maximumTotalSignatureBytes").GetInt32())
                .IsEqualTo(processing.MaximumTotalSignatureBytes);
            await Assert.That(defaults.GetProperty("maximumCmsSignerCount").GetInt32())
                .IsEqualTo(processing.MaximumCmsSignerCount);
            await Assert.That(defaults.GetProperty("maximumNestingDepth").GetInt32())
                .IsEqualTo(processing.MaximumNestingDepth);
            await Assert.That(defaults.GetProperty("maximumItemCount").GetInt32())
                .IsEqualTo(processing.MaximumItemCount);
            await Assert.That(defaults.GetProperty("maximumArchiveEntryBytes").GetInt64())
                .IsEqualTo(processing.MaximumArchiveEntryBytes);
            await Assert.That(defaults.GetProperty("maximumResourceCount").GetInt32())
                .IsEqualTo(resources.MaximumResourceCount);
            await Assert.That(defaults.GetProperty("maximumReferenceDepth").GetInt32())
                .IsEqualTo(resources.MaximumReferenceDepth);
            await Assert.That(defaults.GetProperty("maximumArchiveDepth").GetInt32())
                .IsEqualTo(resources.MaximumArchiveDepth);
            await Assert.That(defaults.GetProperty("maximumTotalResourceBytes").GetInt64())
                .IsEqualTo(resources.MaximumTotalBytes);
            await Assert.That(defaults.GetProperty("maximumArchiveEntryCount").GetInt32())
                .IsEqualTo(resources.MaximumArchiveEntryCount);
            await Assert.That(defaults.GetProperty("maximumArchiveUncompressedBytes").GetInt64())
                .IsEqualTo(resources.MaximumArchiveUncompressedBytes);
            await Assert.That(defaults.GetProperty("maximumCompressionRatio").GetDouble())
                .IsEqualTo(resources.MaximumCompressionRatio);
            await Assert.That(threats.Length).IsEqualTo(16);
            await Assert.That(threats.Select(item => item.GetProperty("id").GetString()).Distinct().Count())
                .IsEqualTo(threats.Length);
            await Assert.That(threats.All(item => item.GetProperty("evidence").GetString()!.Length > 0)).IsTrue();
            await Assert.That(threats.All(item => item.GetProperty("atomic").GetString()!.Length > 0)).IsTrue();
        }
    }

    [Test]
    public async Task Should_expose_one_finite_shared_default_and_validate_custom_limits()
    {
        var defaults = Part21ProcessingLimits.Default;
        var read = new ExchangeStructureReadOptions();
        var write = new ExchangeStructureWriteOptions([]);
        var bridge = new AnnexFModelBridge(CreateStructure(), new Part21Resource("urn:security"));

        using (Assert.Multiple())
        {
            await Assert.That(read.ProcessingLimits).IsSameReferenceAs(defaults);
            await Assert.That(write.ProcessingLimits).IsSameReferenceAs(defaults);
            await Assert.That(bridge.ProcessingLimits).IsSameReferenceAs(defaults);
            await Assert.That(defaults.MaximumInputCharacters).IsGreaterThan(0);
            await Assert.That(defaults.MaximumOutputCharacters).IsGreaterThan(0);
            await Assert.That(defaults.MaximumSignatureBytes).IsGreaterThan(0);
            await Assert.That(() => new Part21ProcessingLimits(maximumItemCount: 0))
                .Throws<ArgumentOutOfRangeException>();
        }
    }

    [Test]
    public async Task Should_stop_root_input_before_parse_or_model_publication()
    {
        var source = new CountingReader(new string('X', 17));
        var limits = new Part21ProcessingLimits(maximumInputCharacters: 16);

        var failure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            source,
            [],
            ExchangeStructureReadOptions.WithProcessingLimits(limits)));

        using (Assert.Multiple())
        {
            await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-PROCESSING-LIMIT-INPUT");
            await Assert.That(source.CharactersRead).IsEqualTo(17);
        }
    }

    [Test]
    public async Task Should_reject_oversized_uri_before_provider_access()
    {
        var provider = new RecordingProvider();
        var limits = new Part21ProcessingLimits(maximumUriCharacters: 24);
        var source = Exchange("REFERENCE;#1=<https://example.test/path#target>;ENDSEC;");

        var failure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(source),
            [SecuritySchemaDescriptor.Instance],
            ExchangeStructureReadOptions.WithProcessingLimits(limits, resourceProvider: provider)));

        using (Assert.Multiple())
        {
            await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-PROCESSING-LIMIT-URI");
            await Assert.That(provider.Requests).IsEmpty();
        }
    }

    [Test]
    public async Task Should_require_an_explicit_provider_for_file_uri_without_implicit_access()
    {
        var source = Exchange("REFERENCE;#1=<file:///C:/private/model.p21#target>;ENDSEC;");

        var failure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(source),
            [SecuritySchemaDescriptor.Instance],
            new ExchangeStructureReadOptions()));

        await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-CAP-RESOURCE-PROVIDER");
    }

    [Test]
    public async Task Should_reject_provider_reentry_and_directory_traversal_before_publication()
    {
        var source = Exchange("REFERENCE;#1=<https://example.test/resource#target>;ENDSEC;");
        ExchangeStructureReadOptions? reentryOptions = null;
        var reentryProvider = new DelegateProvider(resourceIdentity =>
        {
            _ = resourceIdentity;
            _ = ExchangeStructure.Read(
                new StringReader(source),
                [SecuritySchemaDescriptor.Instance],
                reentryOptions!);
            return null;
        });
        reentryOptions = ExchangeStructureReadOptions.WithProcessingLimits(
            Part21ProcessingLimits.Default,
            resourceProvider: reentryProvider);
        var reentryFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(source),
            [SecuritySchemaDescriptor.Instance],
            reentryOptions));

        var directoryIdentity = new Uri("https://example.test/directory");
        var traversalProvider = new RecordingProvider(new Part21ResourceContent(
            directoryIdentity,
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
            {
                ["ISO-10303.p21"] = Encoding.UTF8.GetBytes(Exchange(string.Empty)),
                ["../escape.p21"] = Encoding.UTF8.GetBytes(Exchange(string.Empty)),
            }));
        var traversalFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(source),
            [SecuritySchemaDescriptor.Instance],
            new ExchangeStructureReadOptions(resourceProvider: traversalProvider)));

        using (Assert.Multiple())
        {
            await Assert.That(reentryFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-RESOURCE-PROVIDER-REENTRY");
            await Assert.That(reentryProvider.RequestCount).IsEqualTo(1);
            await Assert.That(traversalFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-RESOURCE-ARCHIVE-PATH");
        }
    }

    [Test]
    public async Task Should_bound_each_archive_entry_before_extraction()
    {
        var identity = new Uri("https://example.test/archive.zip");
        var provider = new RecordingProvider(new Part21ResourceContent(
            identity,
            Part21ResourceContentKind.ZipArchive,
            CreateZip(Exchange(string.Empty))));
        var limits = new Part21ProcessingLimits(maximumArchiveEntryBytes: 32);
        var source = Exchange("REFERENCE;#1=<https://example.test/archive.zip#missing>;ENDSEC;");

        var failure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(source),
            [SecuritySchemaDescriptor.Instance],
            ExchangeStructureReadOptions.WithProcessingLimits(limits, resourceProvider: provider)));

        await Assert.That(failure.Diagnostics.Single().Code)
            .IsEqualTo("P21-PROCESSING-LIMIT-ARCHIVE-ENTRY");
    }

    [Test]
    public async Task Should_bound_signature_count_and_bytes_before_publication()
    {
        var twoSignatures = Exchange(string.Empty)
            + "SIGNATURE AA== ENDSEC;SIGNATURE AA== ENDSEC;";
        var countLimits = new Part21ProcessingLimits(maximumSignatureCount: 1);
        var countFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(twoSignatures),
            [SecuritySchemaDescriptor.Instance],
            ExchangeStructureReadOptions.WithProcessingLimits(countLimits)));

        var largeCms = Convert.ToBase64String(new byte[33]);
        var largeSignature = Exchange(string.Empty) + $"SIGNATURE {largeCms} ENDSEC;";
        var byteLimits = new Part21ProcessingLimits(maximumSignatureBytes: 32);
        var byteFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(largeSignature),
            [SecuritySchemaDescriptor.Instance],
            ExchangeStructureReadOptions.WithProcessingLimits(byteLimits)));

        using (Assert.Multiple())
        {
            await Assert.That(countFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-SIGNATURE");
            await Assert.That(byteFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-SIGNATURE");
        }
    }

    [Test]
    public async Task Should_reject_output_and_signer_reentry_with_zero_destination_characters()
    {
        var structure = CreateStructure();
        var limitedDestination = new StringWriter();
        var outputFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => structure.Write(
            limitedDestination,
            new ExchangeStructureWriteOptions(
                [],
                new Part21ProcessingLimits(maximumOutputCharacters: 16))));

        var reentryDestination = new StringWriter();
        ExchangeStructureWriteOptions? reentryOptions = null;
        reentryOptions = new ExchangeStructureWriteOptions([
            new DelegateSigner(_ =>
            {
                structure.Write(new StringWriter(), reentryOptions!);
                return ReadOnlyMemory<byte>.Empty;
            }),
        ]);
        var reentryFailure = Assert.Throws<ExchangeStructureCapabilityException>(() =>
            structure.Write(reentryDestination, reentryOptions));

        using (Assert.Multiple())
        {
            await Assert.That(outputFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-OUTPUT");
            await Assert.That(limitedDestination.ToString()).IsEmpty();
            await Assert.That(reentryFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-SIGNATURE-SIGNER-REENTRY");
            await Assert.That(reentryDestination.ToString()).IsEmpty();
        }
    }

    [Test]
    public async Task Should_reject_oversized_signer_output_before_destination_publication()
    {
        var structure = CreateStructure();
        var destination = new StringWriter();
        var options = new ExchangeStructureWriteOptions(
            [new DelegateSigner(_ => new byte[33])],
            new Part21ProcessingLimits(maximumSignatureBytes: 32));

        var failure = Assert.Throws<ExchangeStructureCapabilityException>(() =>
            structure.Write(destination, options));

        using (Assert.Multiple())
        {
            await Assert.That(failure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-SIGNATURE");
            await Assert.That(destination.ToString()).IsEmpty();
        }
    }

    [Test]
    public async Task Should_apply_annex_f_input_item_and_depth_limits_without_mutation()
    {
        var structure = CreateStructure();
        structure.Anchors.Add(new Part21Anchor(
            new AnchorName("list"),
            ParameterValue.FromAggregate([ParameterValue.FromAggregate([ParameterValue.Omitted])])));
        var state = new AnnexFModelBridge(structure, new Part21Resource("urn:security")).ExportState();
        var originalName = structure.Header.FileName.Name;
        var originalAnchor = structure.Anchors.Single();

        var inputBridge = new AnnexFModelBridge(
            structure,
            new Part21Resource("urn:security"),
            new Part21ProcessingLimits(maximumInputCharacters: state.Length - 1));
        await Assert.That(() => inputBridge.ApplyState(state)).Throws<JsonException>();

        var itemBridge = new AnnexFModelBridge(
            structure,
            new Part21Resource("urn:security"),
            new Part21ProcessingLimits(maximumItemCount: 2));
        await Assert.That(() => itemBridge.ApplyState(state)).Throws<JsonException>();

        var depthBridge = new AnnexFModelBridge(
            structure,
            new Part21Resource("urn:security"),
            new Part21ProcessingLimits(maximumNestingDepth: 2));
        await Assert.That(() => depthBridge.ApplyState(state)).Throws<JsonException>();

        using (Assert.Multiple())
        {
            await Assert.That(structure.Header.FileName.Name).IsEqualTo(originalName);
            await Assert.That(structure.Anchors.Single()).IsSameReferenceAs(originalAnchor);
        }
    }

    [Test]
    public async Task Should_preserve_normative_reference_cycle_null_semantics()
    {
        var source = Exchange(
            "ANCHOR;<first>=<#second>;<second>=<#first>;ENDSEC;"
            + "REFERENCE;#1=<#first>;ENDSEC;");
        var structure = ExchangeStructure.Read(
            new StringReader(source),
            [SecuritySchemaDescriptor.Instance],
            new ExchangeStructureReadOptions());

        using (Assert.Multiple())
        {
            await Assert.That(structure.References.Single().ResolutionStatus)
                .IsEqualTo(Part21ReferenceResolutionStatus.Null);
            await Assert.That(structure.References.Single().TryGetResolvedValue(out _)).IsFalse();
        }
    }

    private static ExchangeStructure CreateStructure()
    {
        var header = new HeaderSection(
            new FileDescription(["Security boundary"], "4;3"),
            new FileName(
                "security.step",
                "2026-09-08T00:00:00Z",
                ["TedToolkit"],
                ["TedToolkit"],
                "TedToolkit.Step21",
                "integration-test",
                string.Empty),
            new FileSchema(["SECURITY_TEST"]));
        var structure = new ExchangeStructure(header, [SecuritySchemaDescriptor.Instance]);
        structure.DataSections.Add(new DataSection(new SchemaName("SECURITY_TEST")));
        return structure;
    }

    private static string Exchange(string sections) => $$"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('Security boundary'),'4;3');
        FILE_NAME('security.step','2026-09-08T00:00:00Z',('TedToolkit'),('TedToolkit'),'TedToolkit.Step21','integration-test','');
        FILE_SCHEMA(('SECURITY_TEST'));
        ENDSEC;
        {{sections}}
        DATA;
        ENDSEC;
        END-ISO-10303-21;
        """;

    private static ReadOnlyMemory<byte> CreateZip(string root)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(
                   archive.CreateEntry("ISO-10303.p21", CompressionLevel.Fastest).Open(),
                   new UTF8Encoding(false)))
        {
            writer.Write(root);
        }
        return stream.ToArray();
    }

    private sealed class CountingReader(string value) : StringReader(value)
    {
        internal int CharactersRead { get; private set; }

        public override int Read(char[] buffer, int index, int count)
        {
            var read = base.Read(buffer, index, count);
            CharactersRead += read;
            return read;
        }
    }

    private sealed class RecordingProvider(Part21ResourceContent? content = null) : IPart21ResourceProvider
    {
        internal List<string> Requests { get; } = [];

        public Part21ResourceContent? GetResource(Uri resourceIdentity)
        {
            Requests.Add(resourceIdentity.OriginalString);
            return content;
        }
    }

    private sealed class DelegateProvider(Func<Uri, Part21ResourceContent?> getResource)
        : IPart21ResourceProvider
    {
        internal int RequestCount { get; private set; }

        public Part21ResourceContent? GetResource(Uri resourceIdentity)
        {
            RequestCount++;
            return getResource(resourceIdentity);
        }
    }

    private sealed class DelegateSigner(Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> sign)
        : IPart21SignatureSigner
    {
        public ReadOnlyMemory<byte> Sign(ReadOnlyMemory<byte> content) => sign(content);
    }

    private sealed class SecuritySchemaDescriptor : SchemaDescriptor
    {
        internal static SecuritySchemaDescriptor Instance { get; } = new();

        public override SchemaName Name => new("SECURITY_TEST");

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
}