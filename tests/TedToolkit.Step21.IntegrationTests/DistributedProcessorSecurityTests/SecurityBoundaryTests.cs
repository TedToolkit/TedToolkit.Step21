using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

using TedToolkit.Step21.IntegrationTests.ExternalCorpus;

namespace TedToolkit.Step21.IntegrationTests.DistributedProcessorSecurityTests;

/// <summary>Proves the shared quota, callback, and publication threat boundary.</summary>
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
            var executableTests = typeof(SecurityBoundaryTests)
                .GetMethods()
                .Where(method => method.IsDefined(typeof(TestAttribute), inherit: true))
                .Select(method => $"SecurityBoundaryTests.{method.Name}")
                .ToHashSet(StringComparer.Ordinal);
            await Assert.That(threats.All(item =>
                    item.GetProperty("focusedEvidence").EnumerateArray().Any()
                    && item.GetProperty("focusedEvidence").EnumerateArray().All(evidence =>
                        executableTests.Contains(evidence.GetString()!))))
                .IsTrue();
            await Assert.That(threats.All(item => item.GetProperty("atomic").GetString()!.Length > 0)).IsTrue();
        }
    }

    [Test]
    public async Task Should_expose_one_finite_shared_default_and_validate_custom_limits()
    {
        var defaults = Part21ProcessingLimits.Default;
        var read = new ExchangeStructureReadOptions();
        var write = new ExchangeStructureWriteOptions([]);
        using (Assert.Multiple())
        {
            await Assert.That(read.ProcessingLimits).IsSameReferenceAs(defaults);
            await Assert.That(write.ProcessingLimits).IsSameReferenceAs(defaults);
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

        var exactStringReader = new StringReader(new string('X', 18));
        _ = exactStringReader.Read();
        var exactFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            exactStringReader,
            [],
            ExchangeStructureReadOptions.WithProcessingLimits(limits)));
        await Assert.That(exactFailure.Diagnostics.Single().Code)
            .IsEqualTo("P21-PROCESSING-LIMIT-INPUT");
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

        var returnedIdentity = new Uri("https://example.test/" + new string('a', 128));
        var returnedProvider = new RecordingProvider(new Part21ResourceContent(
            returnedIdentity,
            Part21ResourceContentKind.ClearText,
            Encoding.UTF8.GetBytes(Exchange(string.Empty))));
        var returnedFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("REFERENCE;#1=<https://x.test/a#target>;ENDSEC;")),
            [SecuritySchemaDescriptor.Instance],
            ExchangeStructureReadOptions.WithProcessingLimits(
                new Part21ProcessingLimits(maximumUriCharacters: 64),
                resourceProvider: returnedProvider)));
        var baseUriFailure = Assert.Throws<ArgumentException>(() =>
            ExchangeStructureReadOptions.WithProcessingLimits(
                new Part21ProcessingLimits(maximumUriCharacters: 16),
                baseUri: new Uri("https://example.test/" + new string('b', 128))));

        using (Assert.Multiple())
        {
            await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-PROCESSING-LIMIT-URI");
            await Assert.That(provider.Requests).IsEmpty();
            await Assert.That(returnedFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-URI");
            await Assert.That(returnedProvider.Requests.Count).IsEqualTo(1);
            await Assert.That(baseUriFailure.ParamName).IsEqualTo("baseUri");
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

        var other = new Part21ResourceContent(
            new Uri("https://example.test/resource"),
            Part21ResourceContentKind.Other,
            new byte[] { 1 });
        var converterProvider = new RecordingProvider(other);
        ExchangeStructureReadOptions? converterOptions = null;
        var converter = new DelegateConverter(content =>
        {
            _ = content;
            var ignored = ExchangeStructure.Read(
                new StringReader(source),
                [SecuritySchemaDescriptor.Instance],
                converterOptions!);
            _ = ignored;
            return null;
        });
        converterOptions = new ExchangeStructureReadOptions(
            resourceProvider: converterProvider,
            resourceConverter: converter);
        var converterFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(source),
            [SecuritySchemaDescriptor.Instance],
            converterOptions));

        using (Assert.Multiple())
        {
            await Assert.That(reentryFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-RESOURCE-PROVIDER-REENTRY");
            await Assert.That(reentryProvider.RequestCount).IsEqualTo(1);
            await Assert.That(traversalFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-RESOURCE-ARCHIVE-PATH");
            await Assert.That(converterFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-RESOURCE-CONVERTER-REENTRY");
            await Assert.That(converter.CallCount).IsEqualTo(1);
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
    public async Task Should_enforce_total_cms_signer_and_write_signature_limits_before_publication()
    {
        var totalSource = Exchange(string.Empty) + "SIGNATURE AQI= ENDSEC;";
        var totalFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(totalSource),
            [SecuritySchemaDescriptor.Instance],
            ExchangeStructureReadOptions.WithProcessingLimits(new Part21ProcessingLimits(
                maximumSignatureBytes: 8,
                maximumTotalSignatureBytes: 1))));

        var structure = CreateStructure();
        var writeCountCalls = 0;
        var writeCountDestination = new StringWriter();
        var writeCountFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => structure.Write(
            writeCountDestination,
            new ExchangeStructureWriteOptions(
                [
                    new DelegateSigner(_ => { writeCountCalls++; return ReadOnlyMemory<byte>.Empty; }),
                    new DelegateSigner(_ => { writeCountCalls++; return ReadOnlyMemory<byte>.Empty; }),
                ],
                new Part21ProcessingLimits(maximumSignatureCount: 1))));

        using var firstCertificate = CreateCertificate("first");
        using var secondCertificate = CreateCertificate("second");
        var sampleCmsLength = CreateCms(new byte[] { 1 }, firstCertificate).Length;
        var totalWriteCalls = 0;
        var totalWriteDestination = new StringWriter();
        var totalWriteFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => structure.Write(
            totalWriteDestination,
            new ExchangeStructureWriteOptions(
                [
                    new DelegateSigner(content =>
                    {
                        totalWriteCalls++;
                        return CreateCms(content, firstCertificate);
                    }),
                    new DelegateSigner(content =>
                    {
                        totalWriteCalls++;
                        return CreateCms(content, firstCertificate);
                    }),
                ],
                new Part21ProcessingLimits(maximumTotalSignatureBytes: sampleCmsLength * 2 - 1))));
        var signerCountDestination = new StringWriter();
        var signerCountFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => structure.Write(
            signerCountDestination,
            new ExchangeStructureWriteOptions(
                [new DelegateSigner(content => CreateCms(content, firstCertificate, secondCertificate))],
                new Part21ProcessingLimits(maximumCmsSignerCount: 1))));

        var multiSignerPrefix = Exchange(string.Empty) + "\n";
        var multiSignerCms = CreateCms(
            Part21SignatureEngine.EncodeCoveredCharacters(multiSignerPrefix.AsSpan()),
            firstCertificate,
            secondCertificate);
        var multiSignerSource = multiSignerPrefix
            + $"SIGNATURE {Convert.ToBase64String(multiSignerCms.Span)} ENDSEC;";
        var readSignerCountFailure = Assert.Throws<ExchangeStructureCapabilityException>(() =>
            ExchangeStructure.Read(
                new StringReader(multiSignerSource),
                [SecuritySchemaDescriptor.Instance],
                ExchangeStructureReadOptions.WithProcessingLimits(
                    new Part21ProcessingLimits(maximumCmsSignerCount: 1))));

        var malformedFailure = Assert.Throws<ExchangeStructureBindingException>(() => ExchangeStructure.Read(
            new StringReader(Exchange(string.Empty) + "SIGNATURE AQID ENDSEC;"),
            [SecuritySchemaDescriptor.Instance],
            new ExchangeStructureReadOptions()));

        var signedPrefix = Exchange(string.Empty) + "\n";
        var signedCms = CreateCms(
            Part21SignatureEngine.EncodeCoveredCharacters(signedPrefix.AsSpan()),
            firstCertificate);
        var signedSource = signedPrefix + $"SIGNATURE {Convert.ToBase64String(signedCms.Span)} ENDSEC;";
        var trustFailure = Assert.Throws<ExchangeStructureReadValidationException>(() => ExchangeStructure.Read(
            new StringReader(signedSource),
            [SecuritySchemaDescriptor.Instance],
            ExchangeStructureReadOptions.WithSignatureVerification(new Part21SignatureVerificationOptions(
                DateTimeOffset.UtcNow,
                trustedRoots: [],
                additionalCertificates: [new Part21Certificate(firstCertificate.RawData)]))));

        using (Assert.Multiple())
        {
            await Assert.That(totalFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-SIGNATURE");
            await Assert.That(writeCountFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-SIGNATURE");
            await Assert.That(writeCountCalls).IsEqualTo(0);
            await Assert.That(writeCountDestination.ToString()).IsEmpty();
            await Assert.That(signerCountFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-SIGNATURE");
            await Assert.That(signerCountDestination.ToString()).IsEmpty();
            await Assert.That(totalWriteFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-SIGNATURE");
            await Assert.That(totalWriteCalls).IsEqualTo(2);
            await Assert.That(totalWriteDestination.ToString()).IsEmpty();
            await Assert.That(readSignerCountFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-SIGNATURE");
            await Assert.That(malformedFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-SIGNATURE-CMS");
            await Assert.That(trustFailure.ValidationResult.Failures.Single().Code)
                .IsEqualTo("P21.SIGNATURE.UNTRUSTED");
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
    public async Task Should_bound_entity_and_structure_output_before_publication()
    {
        var structure = CreateStructure();
        var entity = new SecurityEntity(new string('A', 1024));
        structure.Add(structure.DataSections.Single(), entity);
        var entityDestination = new StringWriter();
        var entityFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => structure.WriteEntity(
            entityDestination,
            entity,
            new Part21ProcessingLimits(maximumOutputCharacters: 16)));

        var namedStructure = CreateStructure();
        var namedEntity = new SecurityEntity("value");
        namedStructure.Add(
            namedStructure.DataSections.Single(),
            new EntityInstanceName(new string('9', 1024)),
            namedEntity);
        var namedDestination = new StringWriter();
        var namedFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => namedStructure.WriteEntity(
            namedDestination,
            namedEntity,
            new Part21ProcessingLimits(maximumOutputCharacters: 16)));

        foreach (var payload in new ParameterValue[]
                 {
                     ParameterValue.FromEntityInstance(new EntityInstanceName(new string('8', 1024))),
                     ParameterValue.FromValueInstance(new ValueInstanceName(new string('7', 1024))),
                     ParameterValue.FromConstantEntity(new ConstantEntityName(new string('A', 1024))),
                     ParameterValue.FromConstantValue(new ConstantValueName(new string('B', 1024))),
                 })
        {
            var payloadStructure = CreateStructure();
            var payloadEntity = new SecurityEntity(payload);
            payloadStructure.Add(payloadStructure.DataSections.Single(), payloadEntity);
            if (payload.TryGetEntityInstance(out var entityName))
            {
                payloadStructure.References.Add(new Part21Reference(
                    entityName,
                    new Part21Resource("external.p21#entity")));
            }
            else if (payload.TryGetValueInstance(out var valueName))
            {
                payloadStructure.References.Add(new Part21Reference(
                    valueName,
                    new Part21Resource("external.p21#value")));
            }
            var payloadDestination = new StringWriter();
            _ = Assert.Throws<ExchangeStructureCapabilityException>(() => payloadStructure.WriteEntity(
                payloadDestination,
                payloadEntity,
                new Part21ProcessingLimits(maximumOutputCharacters: 64)));
            await Assert.That(payloadDestination.ToString()).IsEmpty();
        }

        var baselineDestination = new StringWriter();
        CreateStructure().Write(baselineDestination);
        var anchorStructure = CreateStructure();
        anchorStructure.Anchors.Add(new Part21Anchor(
            new AnchorName(new string('a', 1024)),
            ParameterValue.Omitted));
        var anchorDestination = new StringWriter();
        _ = Assert.Throws<ExchangeStructureCapabilityException>(() => anchorStructure.Write(
            anchorDestination,
            new ExchangeStructureWriteOptions(
                [],
                new Part21ProcessingLimits(
                    maximumOutputCharacters: baselineDestination.ToString().Length + 16))));
        var referenceStructure = ExchangeStructure.Read(
            new StringReader(Exchange(
                "REFERENCE;#1=<#" + new string('d', 1024) + ">;ENDSEC;")),
            [SecuritySchemaDescriptor.Instance],
            new ExchangeStructureReadOptions());
        var referenceDestination = new StringWriter();
        _ = Assert.Throws<ExchangeStructureCapabilityException>(() => referenceStructure.Write(
            referenceDestination,
            new ExchangeStructureWriteOptions(
                [],
                new Part21ProcessingLimits(
                    maximumOutputCharacters: baselineDestination.ToString().Length + 16))));

        using (Assert.Multiple())
        {
            await Assert.That(entityFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-OUTPUT");
            await Assert.That(entityDestination.ToString()).IsEmpty();
            await Assert.That(namedFailure.Diagnostics.Single().Code)
                .IsEqualTo("P21-PROCESSING-LIMIT-OUTPUT");
            await Assert.That(namedDestination.ToString()).IsEmpty();
            await Assert.That(anchorDestination.ToString()).IsEmpty();
            await Assert.That(referenceDestination.ToString()).IsEmpty();
        }
    }

    [Test]
    public async Task Should_bound_physical_value_items_and_depth_before_publication()
    {
        var itemRead = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("ANCHOR;<items>=(1,2);ENDSEC;")),
            [SecuritySchemaDescriptor.Instance],
            ExchangeStructureReadOptions.WithProcessingLimits(
                new Part21ProcessingLimits(maximumItemCount: 1))));
        var depthRead = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("ANCHOR;<nested>=((1));ENDSEC;")),
            [SecuritySchemaDescriptor.Instance],
            ExchangeStructureReadOptions.WithProcessingLimits(
                new Part21ProcessingLimits(maximumNestingDepth: 1))));

        var itemStructure = CreateStructure();
        itemStructure.Anchors.Add(new Part21Anchor(
            new AnchorName("items"),
            ParameterValue.FromAggregate([
                ParameterValue.FromInteger(1),
                ParameterValue.FromInteger(2),
            ])));
        var itemDestination = new StringWriter();
        var itemWrite = Assert.Throws<ExchangeStructureCapabilityException>(() => itemStructure.Write(
            itemDestination,
            new ExchangeStructureWriteOptions(
                [],
                new Part21ProcessingLimits(maximumItemCount: 1))));

        var depthStructure = CreateStructure();
        var depthEntity = new SecurityEntity(ParameterValue.FromTyped(
            "OUTER",
            ParameterValue.FromAggregate([ParameterValue.FromInteger(1)])));
        depthStructure.Add(depthStructure.DataSections.Single(), depthEntity);
        var depthDestination = new StringWriter();
        var depthWrite = Assert.Throws<ExchangeStructureCapabilityException>(() => depthStructure.Write(
            depthDestination,
            new ExchangeStructureWriteOptions(
                [],
                new Part21ProcessingLimits(maximumNestingDepth: 1))));

        using (Assert.Multiple())
        {
            await Assert.That(itemRead.Diagnostics.Single().Code).IsEqualTo("P21-PROCESSING-LIMIT-ITEM");
            await Assert.That(depthRead.Diagnostics.Single().Code).IsEqualTo("P21-PROCESSING-LIMIT-NESTING");
            await Assert.That(itemWrite.Diagnostics.Single().Code).IsEqualTo("P21-PROCESSING-LIMIT-ITEM");
            await Assert.That(depthWrite.Diagnostics.Single().Code).IsEqualTo("P21-PROCESSING-LIMIT-NESTING");
            await Assert.That(itemDestination.ToString()).IsEmpty();
            await Assert.That(depthDestination.ToString()).IsEmpty();
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

    [Test]
    public async Task Should_enforce_resource_archive_and_recursion_partitions()
    {
        var child = Exchange("ANCHOR;<target>=$;ENDSEC;");
        var provider = new DelegateProvider(identity => new Part21ResourceContent(
            identity,
            Part21ResourceContentKind.ClearText,
            Encoding.UTF8.GetBytes(child)));
        var twoResources = Exchange(
            "REFERENCE;#1=<https://example.test/a.p21#target>;"
            + "#2=<https://example.test/b.p21#target>;ENDSEC;");
        var resourceCount = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(twoResources),
            [SecuritySchemaDescriptor.Instance],
            new ExchangeStructureReadOptions(
                resourceProvider: provider,
                resourceLimits: new Part21ResourceLimits(maximumResourceCount: 1))));
        var resourceBytes = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("REFERENCE;#1=<https://example.test/a.p21#target>;ENDSEC;")),
            [SecuritySchemaDescriptor.Instance],
            new ExchangeStructureReadOptions(
                resourceProvider: provider,
                resourceLimits: new Part21ResourceLimits(maximumTotalBytes: 1))));
        var referenceDepth = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange(
                "ANCHOR;<first>=<#second>;<second>=$;ENDSEC;"
                + "REFERENCE;#1=<#first>;ENDSEC;")),
            [SecuritySchemaDescriptor.Instance],
            new ExchangeStructureReadOptions(
                resourceLimits: new Part21ResourceLimits(maximumReferenceDepth: 1))));

        var archiveIdentity = new Uri("https://example.test/archive.zip");
        var largeRoot = Exchange($"ANCHOR;<target>='{new string('A', 1024)}';ENDSEC;");
        var archiveBytes = CreateZip(new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["ISO-10303.p21"] = Encoding.UTF8.GetBytes(largeRoot),
            ["extra.txt"] = new byte[] { 1 },
        });
        var archiveProvider = new RecordingProvider(new Part21ResourceContent(
            archiveIdentity,
            Part21ResourceContentKind.ZipArchive,
            archiveBytes));
        var archiveSource = Exchange("REFERENCE;#1=<https://example.test/archive.zip#target>;ENDSEC;");
        var entryCount = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(archiveSource),
            [SecuritySchemaDescriptor.Instance],
            new ExchangeStructureReadOptions(
                resourceProvider: archiveProvider,
                resourceLimits: new Part21ResourceLimits(maximumArchiveEntryCount: 1))));
        var expanded = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(archiveSource),
            [SecuritySchemaDescriptor.Instance],
            new ExchangeStructureReadOptions(
                resourceProvider: archiveProvider,
                resourceLimits: new Part21ResourceLimits(maximumArchiveUncompressedBytes: 64))));
        var ratio = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(archiveSource),
            [SecuritySchemaDescriptor.Instance],
            new ExchangeStructureReadOptions(
                resourceProvider: archiveProvider,
                resourceLimits: new Part21ResourceLimits(maximumCompressionRatio: 1))));

        var nested = CreateZip(new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["ISO-10303.p21"] = Encoding.UTF8.GetBytes(child),
        });
        var outer = CreateZip(new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["ISO-10303.p21"] = Encoding.UTF8.GetBytes(
                Exchange("ANCHOR;<target>=<nested.zip#target>;ENDSEC;")),
            ["nested.zip"] = nested,
        });
        var recursionProvider = new RecordingProvider(new Part21ResourceContent(
            new Uri("https://example.test/outer.zip"),
            Part21ResourceContentKind.ZipArchive,
            outer));
        var recursion = Assert.Throws<ExchangeStructureCapabilityException>(() => ExchangeStructure.Read(
            new StringReader(Exchange("REFERENCE;#1=<https://example.test/outer.zip#target>;ENDSEC;")),
            [SecuritySchemaDescriptor.Instance],
            new ExchangeStructureReadOptions(
                resourceProvider: recursionProvider,
                resourceLimits: new Part21ResourceLimits(maximumArchiveDepth: 1))));

        using (Assert.Multiple())
        {
            await Assert.That(resourceCount.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(resourceBytes.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(referenceDepth.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(entryCount.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(expanded.Diagnostics.Single().Code).IsEqualTo("P21-RESOURCE-LIMIT");
            await Assert.That(ratio.Diagnostics.Single().Code)
                .IsEqualTo("P21-RESOURCE-LIMIT-COMPRESSION-RATIO");
            await Assert.That(recursion.Diagnostics.Single().Code)
                .IsEqualTo("P21-RESOURCE-ARCHIVE-RECURSION");
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

    private static ReadOnlyMemory<byte> CreateZip(
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                using var destination = archive.CreateEntry(entry.Key, CompressionLevel.SmallestSize).Open();
                destination.Write(entry.Value.Span);
            }
        }
        return stream.ToArray();
    }

    private static X509Certificate2 CreateCertificate(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={name}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
    }

    private static ReadOnlyMemory<byte> CreateCms(
        ReadOnlyMemory<byte> content,
        params X509Certificate2[] certificates)
    {
        var cms = new SignedCms(new ContentInfo(content.ToArray()), detached: true);
        foreach (var certificate in certificates)
            cms.ComputeSignature(new CmsSigner(certificate));
        return cms.Encode();
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

    private sealed class DelegateConverter(Func<Part21ResourceContent, Part21ResourceContent?> convert)
        : IPart21ResourceConverter
    {
        internal int CallCount { get; private set; }

        public Part21ResourceContent? Convert(Part21ResourceContent content)
        {
            CallCount++;
            return convert(content);
        }
    }

    private sealed class DelegateSigner(Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> sign)
        : IPart21SignatureSigner
    {
        public ReadOnlyMemory<byte> Sign(ReadOnlyMemory<byte> content) => sign(content);
    }

    private sealed class SecurityEntity : Entity
    {
        internal SecurityEntity(string value)
            : this(ParameterValue.FromString(value))
        {
        }

        internal SecurityEntity(ParameterValue value)
        {
            Value = value;
        }

        internal ParameterValue Value { get; }

        public override IEnumerable<Entity> DirectReferences => [];
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
            Entity value) => value is SecurityEntity security
                ? [new("SECURITY_ENTITY", [security.Value])]
                : [];
    }
}
