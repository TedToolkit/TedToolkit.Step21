using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;
using System.Text;

namespace TedToolkit.Step21.Tests.CmsSignatureConformanceTests;

/// <summary>Proves the clause 14 detached CMS boundary and explicit trust policy.</summary>
public sealed class SignatureTests
{
    private static readonly SchemaDescriptor[] Descriptors = [EmptySchemaDescriptor.Instance];

    /// <summary>Retains structurally valid CMS without consulting hidden certificate or time state.</summary>
    [Test]
    public async Task Should_publish_decoded_signatures_as_not_evaluated_without_trust_input()
    {
        using var certificate = CreateCertificate("CN=Part21 Signer");
        var signed = Sign(certificate);

        var structure = ExchangeStructure.Read(new StringReader(signed), Descriptors);

        using (Assert.Multiple())
        {
            await Assert.That(structure.Signatures.Count).IsEqualTo(1);
            await Assert.That(structure.Signatures[0].Signers.Count).IsEqualTo(1);
            await Assert.That(structure.Signatures[0].Signers[0].CryptographicStatus)
                .IsEqualTo(Part21SignatureCryptographicStatus.NotEvaluated);
            await Assert.That(structure.Signatures[0].Signers[0].TrustStatus)
                .IsEqualTo(Part21SignatureTrustStatus.NotEvaluated);
        }
    }

    /// <summary>Includes every prior section, including an earlier signature, in each later CMS value.</summary>
    [Test]
    public async Task Should_verify_multiple_signatures_over_every_preceding_standard_character()
    {
        using var certificate = CreateCertificate("CN=Original Signer");
        using var replacementCertificate = CreateCertificate("CN=Replacement Signer");
        var signed = Sign(certificate, certificate);
        var firstToken = signed.IndexOf("SIGNATURE ", StringComparison.Ordinal);
        var payloadStart = firstToken + "SIGNATURE ".Length;
        var payloadEnd = signed.IndexOf(" ENDSEC;", payloadStart, StringComparison.Ordinal);
        var replacementSigner = new Part21CmsSigner(replacementCertificate);
        var replacementCms = replacementSigner.Sign(
            Part21SignatureEngine.EncodeCoveredCharacters(signed.AsSpan(0, firstToken)));
        var replaced = string.Concat(
            signed.AsSpan(0, payloadStart),
            Convert.ToBase64String(replacementCms.Span),
            signed.AsSpan(payloadEnd));
        var verification = Verification(
            [certificate, replacementCertificate],
            new Part21SignatureAcceptancePolicy(acceptCryptographicallyInvalid: true));

        var rejected = Assert.Throws<ExchangeStructureReadValidationException>(() => ReadWith(
            replaced,
            Verification([certificate, replacementCertificate], new Part21SignatureAcceptancePolicy())));

        var structure = ExchangeStructure.Read(
            new StringReader(replaced),
            Descriptors,
            ExchangeStructureReadOptions.WithSignatureVerification(verification));

        using (Assert.Multiple())
        {
            await Assert.That(structure.Signatures[0].Signers[0].CryptographicStatus)
                .IsEqualTo(Part21SignatureCryptographicStatus.Valid);
            await Assert.That(structure.Signatures[0].Signers[0].TrustStatus)
                .IsEqualTo(Part21SignatureTrustStatus.Trusted);
            await Assert.That(structure.Signatures[1].Signers[0].CryptographicStatus)
                .IsEqualTo(Part21SignatureCryptographicStatus.Invalid);
            await Assert.That(rejected.ValidationResult.Failures.Single().Code)
                .IsEqualTo("P21.SIGNATURE.CRYPTOGRAPHICALLY_INVALID");
        }
    }

    /// <summary>Signs exactly the preceding canonical Part 21 basic-alphabet characters.</summary>
    [Test]
    public async Task Should_exclude_controls_but_retain_basic_alphabet_characters_in_covered_content()
    {
        using var certificate = CreateCertificate("CN=Coverage Signer");
        var source = Unsigned.Replace("'Author'", "'作者'", StringComparison.Ordinal);
        var structure = ExchangeStructure.Read(new StringReader(source), Descriptors);
        var signer = new RecordingSigner(new Part21CmsSigner(certificate));
        var destination = new StringWriter();

        structure.Write(destination, new ExchangeStructureWriteOptions([signer]));
        var reread = ExchangeStructure.Read(new StringReader(destination.ToString()), Descriptors);

        const string expected = "ISO-10303-21;HEADER;"
            + "FILE_DESCRIPTION(('CMS signature test'),'4;3');"
            + "FILE_NAME('signature.p21','2026-09-07T12:00:00Z',('\\X2\\4F5C\\X0\\\\X2\\8005\\X0\\'),('Org'),'Pre','System','Auth');"
            + "FILE_SCHEMA(('empty_schema'));ENDSEC;END-ISO-10303-21;";
        using (Assert.Multiple())
        {
            await Assert.That(Encoding.UTF8.GetString(signer.Content!)).IsEqualTo(expected);
            await Assert.That(reread.Signatures[0].DigestAlgorithm)
                .IsEqualTo("2.16.840.1.101.3.4.2.1");
            await Assert.That(reread.Signatures[0].MessageDigest!.Value.Span.SequenceEqual(
                    SHA256.HashData(signer.Content!)))
                .IsTrue();
            await Assert.That(Encoding.UTF8.GetString(
                    Part21SignatureEngine.EncodeCoveredCharacters("甲\r\n".AsSpan())))
                .IsEqualTo("甲");
        }
    }

    /// <summary>Keeps cryptographic validity distinct from deterministic caller-supplied trust states.</summary>
    [Test]
    public async Task Should_report_explicit_untrusted_expired_and_revoked_states()
    {
        using var signerCertificate = CreateCertificate("CN=State Signer");
        using var otherRoot = CreateCertificate("CN=Other Root");
        var signed = Sign(signerCertificate);
        var untrusted = ReadWith(signed, Verification(
            [otherRoot],
            new Part21SignatureAcceptancePolicy(acceptUntrusted: true)));
        var expired = ReadWith(signed, Verification(
            [signerCertificate],
            new Part21SignatureAcceptancePolicy(acceptExpired: true),
            new DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        var notYetValid = ReadWith(signed, Verification(
            [signerCertificate],
            new Part21SignatureAcceptancePolicy(acceptExpired: true),
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        var revoked = ReadWith(signed, new Part21SignatureVerificationOptions(
            VerificationTime,
            [new Part21Certificate(signerCertificate.RawData)],
            revokedCertificates: [new Part21Certificate(signerCertificate.RawData)],
            acceptancePolicy: new Part21SignatureAcceptancePolicy(acceptRevoked: true)));

        using (Assert.Multiple())
        {
            await Assert.That(untrusted.Signatures[0].Signers[0].TrustStatus)
                .IsEqualTo(Part21SignatureTrustStatus.Untrusted);
            await Assert.That(expired.Signatures[0].Signers[0].TrustStatus)
                .IsEqualTo(Part21SignatureTrustStatus.Expired);
            await Assert.That(notYetValid.Signatures[0].Signers[0].TrustStatus)
                .IsEqualTo(Part21SignatureTrustStatus.Expired);
            await Assert.That(revoked.Signatures[0].Signers[0].TrustStatus)
                .IsEqualTo(Part21SignatureTrustStatus.Revoked);
        }
    }

    /// <summary>Reports an absent CMS signer certificate separately from a bad cryptographic signature.</summary>
    [Test]
    public async Task Should_report_unknown_signer_when_no_certificate_can_be_resolved()
    {
        using var certificate = CreateCertificate("CN=Unresolved Signer");
        var prefix = Unsigned + "\n";
        var content = Part21SignatureEngine.EncodeCoveredCharacters(prefix.AsSpan());
        var detached = new SignedCms(new ContentInfo(content), detached: true);
        detached.ComputeSignature(new CmsSigner(certificate) { IncludeOption = X509IncludeOption.None }, silent: true);
        var source = prefix + $"SIGNATURE {Convert.ToBase64String(detached.Encode())} ENDSEC;";
        var verification = new Part21SignatureVerificationOptions(
            VerificationTime,
            [],
            acceptancePolicy: new Part21SignatureAcceptancePolicy(acceptUnknownSigner: true));
        var resolvedVerification = new Part21SignatureVerificationOptions(
            VerificationTime,
            [new Part21Certificate(certificate.RawData)],
            additionalCertificates: [new Part21Certificate(certificate.RawData)]);

        var read = ReadWith(source, verification);
        var resolved = ReadWith(source, resolvedVerification);

        using (Assert.Multiple())
        {
            await Assert.That(read.Signatures[0].Signers[0].CryptographicStatus)
                .IsEqualTo(Part21SignatureCryptographicStatus.NotEvaluated);
            await Assert.That(read.Signatures[0].Signers[0].TrustStatus)
                .IsEqualTo(Part21SignatureTrustStatus.UnknownSigner);
            await Assert.That(resolved.Signatures[0].Signers[0].CryptographicStatus)
                .IsEqualTo(Part21SignatureCryptographicStatus.Valid);
            await Assert.That(resolved.Signatures[0].Signers[0].TrustStatus)
                .IsEqualTo(Part21SignatureTrustStatus.Trusted);
        }
    }

    /// <summary>Does not expose mutable certificate or digest storage through read-only memory views.</summary>
    [Test]
    public async Task Should_snapshot_public_certificate_and_digest_bytes()
    {
        using var certificate = CreateCertificate("CN=Snapshot Signer");
        var certificateSnapshot = new Part21Certificate(certificate.RawData);
        var originalCertificate = certificateSnapshot.Encoded.ToArray();
        var exposedCertificate = certificateSnapshot.Encoded;
        _ = MemoryMarshal.TryGetArray(exposedCertificate, out var exposedCertificateArray);
        exposedCertificateArray.Array![exposedCertificateArray.Offset]++;

        var signed = ExchangeStructure.Read(new StringReader(Sign(certificate)), Descriptors);
        var originalDigest = signed.Signatures[0].MessageDigest!.Value.ToArray();
        var exposedDigest = signed.Signatures[0].MessageDigest!.Value;
        _ = MemoryMarshal.TryGetArray(exposedDigest, out var exposedDigestArray);
        exposedDigestArray.Array![exposedDigestArray.Offset]++;

        using (Assert.Multiple())
        {
            await Assert.That(certificateSnapshot.Encoded.Span.SequenceEqual(originalCertificate)).IsTrue();
            await Assert.That(signed.Signatures[0].MessageDigest!.Value.Span.SequenceEqual(originalDigest)).IsTrue();
        }
    }

    /// <summary>Rejects structurally malformed CMS before publishing even when verification is omitted.</summary>
    [Test]
    public async Task Should_reject_malformed_cms_even_without_verification_input()
    {
        var malformed = Unsigned + "\nSIGNATURE QUJD ENDSEC;";

        var failure = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(new StringReader(malformed), Descriptors));

        await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-SIGNATURE-CMS");
    }

    /// <summary>Rejects valid but non-canonical Base64 spelling before CMS decoding.</summary>
    [Test]
    public async Task Should_reject_noncanonical_base64()
    {
        var failure = Assert.Throws<ExchangeStructureBindingException>(() => ExchangeStructure.Read(
            new StringReader(Unsigned + "\nSIGNATURE AB== ENDSEC;"),
            Descriptors));

        await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-SIGNATURE-BASE64");
    }

    /// <summary>Rejects CMS with embedded content because clause 14 requires external content.</summary>
    [Test]
    public async Task Should_reject_cms_with_embedded_content()
    {
        using var certificate = CreateCertificate("CN=Embedded Content Signer");
        var prefix = Unsigned + "\n";
        var cms = new SignedCms(
            new ContentInfo(Part21SignatureEngine.EncodeCoveredCharacters(prefix.AsSpan())),
            detached: false);
        cms.ComputeSignature(new CmsSigner(certificate), silent: true);
        var source = prefix + $"SIGNATURE {Convert.ToBase64String(cms.Encode())} ENDSEC;";

        var failure = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(new StringReader(source), Descriptors));

        await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-SIGNATURE-CMS-CONTENT");
    }

    /// <summary>Rejects attached CMS even when its embedded content has zero length.</summary>
    [Test]
    public async Task Should_reject_zero_length_embedded_cms_content()
    {
        var source = Unsigned + $"\nSIGNATURE {Convert.ToBase64String(CreateEmptyAttachedCms())} ENDSEC;";

        var failure = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(new StringReader(source), Descriptors));

        await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-SIGNATURE-CMS-CONTENT");
    }

    /// <summary>Rejects structurally valid detached CMS SignedData with no signer information.</summary>
    [Test]
    public async Task Should_reject_detached_cms_without_a_signer()
    {
        var source = Unsigned + $"\nSIGNATURE {Convert.ToBase64String(CreateEmptyDetachedCms())} ENDSEC;";

        var failure = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(new StringReader(source), Descriptors));

        await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-SIGNATURE-CMS-SIGNER");
    }

    /// <summary>Propagates malformed CMS from an external structure instead of treating it as a null reference.</summary>
    [Test]
    public async Task Should_reject_malformed_cms_atomically_across_the_resource_graph()
    {
        const string childIdentity = "https://example.test/signatures/child.p21";
        var root = Unsigned.Replace(
            "ENDSEC;\nEND-ISO-10303-21;",
            "ENDSEC;\nREFERENCE;\n#1=<child.p21#1>;\nENDSEC;\nEND-ISO-10303-21;",
            StringComparison.Ordinal);
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [childIdentity] = new(
                new Uri(childIdentity),
                Part21ResourceContentKind.ClearText,
                Encoding.UTF8.GetBytes(Unsigned + "\nSIGNATURE QUJD ENDSEC;")),
        });

        var failure = Assert.Throws<ExchangeStructureBindingException>(() => ExchangeStructure.Read(
            new StringReader(root),
            Descriptors,
            new ExchangeStructureReadOptions(
                new Uri("https://example.test/signatures/root.p21"),
                provider)));

        await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-SIGNATURE-CMS");
    }

    /// <summary>Propagates malformed signature-section syntax from every external structure.</summary>
    [Test]
    public async Task Should_reject_malformed_signature_syntax_atomically_across_the_resource_graph()
    {
        const string childIdentity = "https://example.test/signatures/malformed-child.p21";
        var root = Unsigned.Replace(
            "ENDSEC;\nEND-ISO-10303-21;",
            "ENDSEC;\nREFERENCE;\n#1=<malformed-child.p21#1>;\nENDSEC;\nEND-ISO-10303-21;",
            StringComparison.Ordinal);
        var provider = new DictionaryProvider(new Dictionary<string, Part21ResourceContent>
        {
            [childIdentity] = new(
                new Uri(childIdentity),
                Part21ResourceContentKind.ClearText,
                Encoding.UTF8.GetBytes(Unsigned + "\nSIGNATURE ENDSEC;")),
        });

        var failure = Assert.Throws<ExchangeStructureSyntaxException>(() => ExchangeStructure.Read(
            new StringReader(root),
            Descriptors,
            new ExchangeStructureReadOptions(
                new Uri("https://example.test/signatures/root.p21"),
                provider)));

        await Assert.That(failure.Diagnostics.Any(value => value.Code == "P21-SIGNATURE-SYNTAX")).IsTrue();
    }

    /// <summary>Applies the explicit evaluated-state acceptance policy before model publication.</summary>
    [Test]
    public async Task Should_apply_rejection_policy_before_publishing_a_structure()
    {
        using var certificate = CreateCertificate("CN=Rejected Signer");
        using var otherRoot = CreateCertificate("CN=Other Root");
        var signed = Sign(certificate);
        var verification = Verification([otherRoot], new Part21SignatureAcceptancePolicy());

        var failure = Assert.Throws<ExchangeStructureReadValidationException>(() =>
            ReadWith(signed, verification));

        await Assert.That(failure.ValidationResult.Failures.Single().Code)
            .IsEqualTo("P21.SIGNATURE.UNTRUSTED");
    }

    /// <summary>Rejects every non-trusted evaluated state unless its individual switch is enabled.</summary>
    [Test]
    public async Task Should_default_every_signature_acceptance_switch_to_reject()
    {
        using var certificate = CreateCertificate("CN=Policy Signer");
        var signed = Sign(certificate);
        var unknownCms = CreateCertificateOmittedSource(certificate);
        var unknown = Assert.Throws<ExchangeStructureReadValidationException>(() => ReadWith(
            unknownCms,
            new Part21SignatureVerificationOptions(VerificationTime, [])));
        var expired = Assert.Throws<ExchangeStructureReadValidationException>(() => ReadWith(
            signed,
            Verification(
                [certificate],
                new Part21SignatureAcceptancePolicy(),
                new DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero))));
        var revoked = Assert.Throws<ExchangeStructureReadValidationException>(() => ReadWith(
            signed,
            new Part21SignatureVerificationOptions(
                VerificationTime,
                [new Part21Certificate(certificate.RawData)],
                revokedCertificates: [new Part21Certificate(certificate.RawData)])));

        using (Assert.Multiple())
        {
            await Assert.That(unknown.ValidationResult.Failures.Single().Code)
                .IsEqualTo("P21.SIGNATURE.UNKNOWNSIGNER");
            await Assert.That(expired.ValidationResult.Failures.Single().Code)
                .IsEqualTo("P21.SIGNATURE.EXPIRED");
            await Assert.That(revoked.ValidationResult.Failures.Single().Code)
                .IsEqualTo("P21.SIGNATURE.REVOKED");
        }
    }

    /// <summary>Never exposes partial output when signing fails or the required signer is absent.</summary>
    [Test]
    public async Task Should_fail_signing_atomically_and_require_a_signer_for_signed_models()
    {
        var unsigned = ExchangeStructure.Read(new StringReader(Unsigned), Descriptors);
        var failedDestination = new StringWriter();
        var signerFailure = Assert.Throws<ExchangeStructureCapabilityException>(() => unsigned.Write(
            failedDestination,
            new ExchangeStructureWriteOptions([new EmptySigner()])));

        using var certificate = CreateCertificate("CN=Existing Signer");
        var signed = ExchangeStructure.Read(new StringReader(Sign(certificate)), Descriptors);
        var unsignedDestination = new StringWriter();
        var missingSigner = Assert.Throws<ExchangeStructureCapabilityException>(() =>
            signed.Write(unsignedDestination));

        using (Assert.Multiple())
        {
            await Assert.That(signerFailure.Diagnostics.Single().Code).IsEqualTo("P21-CAP-SIGNATURE-SIGNER");
            await Assert.That(failedDestination.ToString()).IsEmpty();
            await Assert.That(missingSigner.Diagnostics.Single().Code).IsEqualTo("P21-CAP-SIGNATURE-SIGNER");
            await Assert.That(unsignedDestination.ToString()).IsEmpty();
        }
    }

    /// <summary>Validates signer output against a private canonical snapshot after the callback returns.</summary>
    [Test]
    public async Task Should_reject_a_signer_that_mutates_its_input_buffer()
    {
        using var certificate = CreateCertificate("CN=Mutating Signer");
        var structure = ExchangeStructure.Read(new StringReader(Unsigned), Descriptors);
        var destination = new StringWriter();

        var failure = Assert.Throws<ExchangeStructureCapabilityException>(() => structure.Write(
            destination,
            new ExchangeStructureWriteOptions([new MutatingSigner(new Part21CmsSigner(certificate))])));

        using (Assert.Multiple())
        {
            await Assert.That(failure.Diagnostics.Single().Code).IsEqualTo("P21-CAP-SIGNATURE-SIGNER");
            await Assert.That(destination.ToString()).IsEmpty();
        }
    }

    private static DateTimeOffset VerificationTime =>
        new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static string Sign(params X509Certificate2[] certificates)
    {
        var structure = ExchangeStructure.Read(new StringReader(Unsigned), Descriptors);
        var destination = new StringWriter();
        structure.Write(
            destination,
            new ExchangeStructureWriteOptions(certificates.Select(value => new Part21CmsSigner(value))));
        return destination.ToString();
    }

    private static string CreateCertificateOmittedSource(X509Certificate2 certificate)
    {
        var prefix = Unsigned + "\n";
        var cms = new SignedCms(
            new ContentInfo(Part21SignatureEngine.EncodeCoveredCharacters(prefix.AsSpan())),
            detached: true);
        cms.ComputeSignature(new CmsSigner(certificate) { IncludeOption = X509IncludeOption.None }, silent: true);
        return prefix + $"SIGNATURE {Convert.ToBase64String(cms.Encode())} ENDSEC;";
    }

    private static byte[] CreateEmptyAttachedCms()
        => CreateEmptyCms(includeContent: true);

    private static byte[] CreateEmptyDetachedCms()
        => CreateEmptyCms(includeContent: false);

    private static byte[] CreateEmptyCms(bool includeContent)
    {
        var explicitTag = new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true);
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence();
        writer.WriteObjectIdentifier("1.2.840.113549.1.7.2");
        writer.PushSequence(explicitTag);
        writer.PushSequence();
        writer.WriteInteger(1);
        writer.PushSetOf();
        writer.PopSetOf();
        writer.PushSequence();
        writer.WriteObjectIdentifier("1.2.840.113549.1.7.1");
        if (includeContent)
        {
            writer.PushSequence(explicitTag);
            writer.WriteOctetString([]);
            writer.PopSequence(explicitTag);
        }
        writer.PopSequence();
        writer.PushSetOf();
        writer.PopSetOf();
        writer.PopSequence();
        writer.PopSequence(explicitTag);
        writer.PopSequence();
        return writer.Encode();
    }

    private static ExchangeStructure ReadWith(string source, Part21SignatureVerificationOptions verification) =>
        ExchangeStructure.Read(
            new StringReader(source),
            Descriptors,
            ExchangeStructureReadOptions.WithSignatureVerification(verification));

    private static Part21SignatureVerificationOptions Verification(
        IEnumerable<X509Certificate2> roots,
        Part21SignatureAcceptancePolicy policy,
        DateTimeOffset? time = null) => new(
            time ?? VerificationTime,
            roots.Select(value => new Part21Certificate(value.RawData)),
            acceptancePolicy: policy);

    private static X509Certificate2 CreateCertificate(string subject)
    {
        var fixtureName = subject.Contains("Replacement", StringComparison.Ordinal)
            || subject.Contains("Other Root", StringComparison.Ordinal)
                ? "alternate"
                : "primary";
        var directory = Path.Combine(AppContext.BaseDirectory, "TestData", "Cms");
        using var certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(
            File.ReadAllText(Path.Combine(directory, fixtureName + "-certificate.der.b64"))));
        using var privateKey = RSA.Create();
        privateKey.ImportPkcs8PrivateKey(Convert.FromBase64String(
            File.ReadAllText(Path.Combine(directory, fixtureName + "-private-key.pkcs8.b64"))), out _);
        return certificate.CopyWithPrivateKey(privateKey);
    }

    private const string Unsigned = """
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('CMS signature test'),'4;3');
        FILE_NAME('signature.p21','2026-09-07T12:00:00Z',('Author'),('Org'),'Pre','System','Auth');
        FILE_SCHEMA(('empty_schema'));
        ENDSEC;
        END-ISO-10303-21;
        """;

    private sealed class EmptySigner : IPart21SignatureSigner
    {
        public ReadOnlyMemory<byte> Sign(ReadOnlyMemory<byte> content) => ReadOnlyMemory<byte>.Empty;
    }

    private sealed class RecordingSigner(IPart21SignatureSigner inner) : IPart21SignatureSigner
    {
        internal byte[]? Content { get; private set; }

        public ReadOnlyMemory<byte> Sign(ReadOnlyMemory<byte> content)
        {
            Content = content.ToArray();
            return inner.Sign(content);
        }
    }

    private sealed class MutatingSigner(IPart21SignatureSigner inner) : IPart21SignatureSigner
    {
        public ReadOnlyMemory<byte> Sign(ReadOnlyMemory<byte> content)
        {
            if (!MemoryMarshal.TryGetArray(content, out var segment) || segment.Array is null)
                throw new InvalidOperationException("The writer did not expose the expected callback buffer.");
            segment.Array[segment.Offset + segment.Count - 1] ^= 1;
            return inner.Sign(content);
        }
    }

    private sealed class DictionaryProvider(IReadOnlyDictionary<string, Part21ResourceContent> resources)
        : IPart21ResourceProvider
    {
        public Part21ResourceContent? GetResource(Uri resourceIdentity) =>
            resources.GetValueOrDefault(resourceIdentity.AbsoluteUri);
    }

    private sealed class EmptySchemaDescriptor : SchemaDescriptor
    {
        internal static EmptySchemaDescriptor Instance { get; } = new();

        public override SchemaName Name => new("empty_schema");

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