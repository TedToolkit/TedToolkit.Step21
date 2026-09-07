using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using TedToolkit.Step21.Syntax;

namespace TedToolkit.Step21;

internal static class Part21SignatureEngine
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal static IReadOnlyList<Part21Signature> Evaluate(
        string source,
        IReadOnlyList<SignatureSectionSyntax> signatures,
        Part21SignatureVerificationOptions? options)
    {
        if (signatures.Count == 0)
            return Array.Empty<Part21Signature>();

        using var verification = options is null ? null : new VerificationContext(options);
        var result = new List<Part21Signature>(signatures.Count);
        for (var signatureIndex = 0; signatureIndex < signatures.Count; signatureIndex++)
        {
            var syntax = signatures[signatureIndex];
            if (!TryDecodeCanonicalBase64(syntax.Content.Text, out var encodedCms))
            {
                throw Malformed(
                    "P21-SIGNATURE-BASE64",
                    "A signature section must contain canonical RFC 4648 Base64.",
                    syntax.Content.Span.Start);
            }

            byte[] content;
            try
            {
                content = EncodeCoveredCharacters(source.AsSpan(0, syntax.StartIndex));
            }
            catch (EncoderFallbackException)
            {
                throw Malformed(
                    "P21-SIGNATURE-ALPHABET",
                    "The content preceding a signature contains an invalid Unicode scalar value.",
                    syntax.Span.Start);
            }

            var cms = new SignedCms(new ContentInfo(content), detached: true);
            try
            {
                var encodedEnvelope = new SignedCms();
                encodedEnvelope.Decode(encodedCms);
                if (HasEmbeddedContent(encodedCms))
                {
                    throw Malformed(
                        "P21-SIGNATURE-CMS-CONTENT",
                        "A signature section CMS value must use external content.",
                        syntax.Content.Span.Start);
                }
                cms.Decode(encodedCms);
            }
            catch (Exception exception) when (exception is CryptographicException or AsnContentException)
            {
                throw Malformed(
                    "P21-SIGNATURE-CMS",
                    "The signature section does not contain valid detached CMS SignedData.",
                    syntax.Content.Span.Start);
            }

            if (cms.SignerInfos.Count == 0)
            {
                throw Malformed(
                    "P21-SIGNATURE-CMS-SIGNER",
                    "A CMS signature section must contain at least one signer.",
                    syntax.Content.Span.Start);
            }

            var signerResults = options is null
                ? CreateNotEvaluatedResults(cms)
                : Verify(cms, verification!);
            var digestAlgorithm = cms.SignerInfos[0].DigestAlgorithm.Value
                ?? throw Malformed(
                    "P21-SIGNATURE-CMS-DIGEST",
                    "The first CMS signer does not identify a digest algorithm.",
                    syntax.Content.Span.Start);
            var signature = new Part21Signature(
                syntax.Content.Text,
                digestAlgorithm,
                TryComputeDigest(digestAlgorithm, content),
                signerResults);
            ThrowIfRejected(signature, signatureIndex, options);
            result.Add(signature);
        }

        return result.AsReadOnly();
    }

    internal static byte[] EncodeCoveredCharacters(ReadOnlySpan<char> content)
    {
        var byteCount = GetCoveredByteCount(content);
        var result = new byte[byteCount];
        EncodeCoveredCharacters(content, result);
        return result;
    }

    internal static byte[] EncodeCoveredCharacters(StringBuilder content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var byteCount = 0;
        var isAscii = true;
        foreach (var chunk in content.GetChunks())
        {
            foreach (var character in chunk.Span)
            {
                if (character >= '\u0080')
                    isAscii = false;
                if (IsCovered(character))
                    byteCount++;
            }
        }

        if (!isAscii)
            return EncodeCoveredCharacters(content.ToString().AsSpan());

        var result = new byte[byteCount];
        var offset = 0;
        foreach (var chunk in content.GetChunks())
        {
            foreach (var character in chunk.Span)
            {
                if (IsCovered(character))
                    result[offset++] = (byte)character;
            }
        }
        return result;
    }

    private static int GetCoveredByteCount(ReadOnlySpan<char> content)
    {
        var result = 0;
        var segmentStart = 0;
        for (var index = 0; index < content.Length; index++)
        {
            if (IsCovered(content[index]))
                continue;

            result = checked(result + StrictUtf8.GetByteCount(content[segmentStart..index]));
            segmentStart = index + 1;
        }
        return checked(result + StrictUtf8.GetByteCount(content[segmentStart..]));
    }

    private static void EncodeCoveredCharacters(ReadOnlySpan<char> content, Span<byte> destination)
    {
        var destinationOffset = 0;
        var segmentStart = 0;
        for (var index = 0; index < content.Length; index++)
        {
            if (IsCovered(content[index]))
                continue;

            destinationOffset += StrictUtf8.GetBytes(
                content[segmentStart..index],
                destination[destinationOffset..]);
            segmentStart = index + 1;
        }
        _ = StrictUtf8.GetBytes(content[segmentStart..], destination[destinationOffset..]);
    }

    private static bool IsCovered(char character) =>
        character is >= '\u0020' and <= '\u007e' or >= '\u0080';

    internal static byte[] ValidateSignerOutput(
        ReadOnlyMemory<byte> encodedCms,
        ReadOnlyMemory<byte> content,
        out string digestAlgorithm)
    {
        if (encodedCms.IsEmpty)
        {
            throw Capability(
                "P21-CAP-SIGNATURE-SIGNER",
                "A signature signer returned an empty CMS value.");
        }

        var copy = encodedCms.ToArray();
        var cms = new SignedCms(new ContentInfo(content.ToArray()), detached: true);
        try
        {
            var encodedEnvelope = new SignedCms();
            encodedEnvelope.Decode(copy);
            if (HasEmbeddedContent(copy))
                throw new CryptographicException("CMS content is embedded.");
            cms.Decode(copy);
            if (cms.SignerInfos.Count == 0)
                throw new CryptographicException("CMS SignedData contains no signer.");
            cms.CheckSignature(verifySignatureOnly: true);
        }
        catch (Exception exception) when (exception is CryptographicException or AsnContentException)
        {
            throw Capability(
                "P21-CAP-SIGNATURE-SIGNER",
                "A signature signer did not return valid detached CMS for the supplied content.");
        }
        digestAlgorithm = cms.SignerInfos[0].DigestAlgorithm.Value
            ?? throw Capability(
                "P21-CAP-SIGNATURE-SIGNER",
                "A signature signer did not identify its first CMS digest algorithm.");
        return copy;
    }

    private static bool HasEmbeddedContent(ReadOnlyMemory<byte> encodedCms)
    {
        var contentInfo = new AsnReader(encodedCms, AsnEncodingRules.BER).ReadSequence();
        _ = contentInfo.ReadObjectIdentifier();
        var explicitContent = contentInfo.ReadSequence(
            new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
        var signedData = explicitContent.ReadSequence();
        _ = signedData.ReadInteger();
        _ = signedData.ReadSetOf();
        var encapsulatedContent = signedData.ReadSequence();
        _ = encapsulatedContent.ReadObjectIdentifier();
        return encapsulatedContent.HasData;
    }

    private static IReadOnlyList<Part21SignatureSignerResult> CreateNotEvaluatedResults(SignedCms cms) =>
        cms.SignerInfos.Cast<SignerInfo>().Select(signer => new Part21SignatureSignerResult(
            GetFingerprint(signer.Certificate),
            Part21SignatureCryptographicStatus.NotEvaluated,
            Part21SignatureTrustStatus.NotEvaluated)).ToArray();

    private static IReadOnlyList<Part21SignatureSignerResult> Verify(SignedCms cms, VerificationContext verification)
    {
        var result = new List<Part21SignatureSignerResult>(cms.SignerInfos.Count);
        foreach (SignerInfo signer in cms.SignerInfos)
        {
            var certificate = signer.Certificate;
            if (certificate is null)
            {
                certificate = verification.Loaded.FirstOrDefault(candidate => VerifiesWith(signer, candidate));
                if (certificate is null)
                {
                    result.Add(new Part21SignatureSignerResult(
                        certificateFingerprint: null,
                        Part21SignatureCryptographicStatus.NotEvaluated,
                        Part21SignatureTrustStatus.UnknownSigner));
                    continue;
                }
            }
            else
            {
                try
                {
                    signer.CheckSignature(verification.ExtraStore, verifySignatureOnly: true);
                }
                catch (CryptographicException)
                {
                    result.Add(new Part21SignatureSignerResult(
                        GetFingerprint(certificate),
                        Part21SignatureCryptographicStatus.Invalid,
                        Part21SignatureTrustStatus.NotEvaluated));
                    continue;
                }
            }

            var fingerprint = GetFingerprint(certificate);
            var trust = EvaluateTrust(certificate, fingerprint!, verification, cms.Certificates);
            result.Add(new Part21SignatureSignerResult(
                fingerprint,
                Part21SignatureCryptographicStatus.Valid,
                trust));
        }
        return result.AsReadOnly();
    }

    private static bool VerifiesWith(SignerInfo signer, X509Certificate2 certificate)
    {
        try
        {
            signer.CheckSignature(new X509Certificate2Collection(certificate), verifySignatureOnly: true);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static Part21SignatureTrustStatus EvaluateTrust(
        X509Certificate2 certificate,
        string fingerprint,
        VerificationContext verification,
        X509Certificate2Collection embeddedCertificates)
    {
        var options = verification.Options;
        var time = options.VerificationTime.UtcDateTime;
        if (time < certificate.NotBefore.ToUniversalTime() || time > certificate.NotAfter.ToUniversalTime())
            return Part21SignatureTrustStatus.Expired;
        if (options.IsRevoked(fingerprint))
            return Part21SignatureTrustStatus.Revoked;

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.ChainPolicy.VerificationTime = time;
        foreach (var root in verification.Loaded.Take(options.TrustedRoots.Count))
            chain.ChainPolicy.CustomTrustStore.Add(root);
        foreach (var additional in verification.Loaded.Skip(options.TrustedRoots.Count))
            chain.ChainPolicy.ExtraStore.Add(additional);
        chain.ChainPolicy.ExtraStore.AddRange(embeddedCertificates);
        if (!chain.Build(certificate) || chain.ChainElements.Count == 0)
            return Part21SignatureTrustStatus.Untrusted;

        var explicitFingerprints = new HashSet<string>(
            verification.Loaded.Select(GetFingerprint).OfType<string>(),
            StringComparer.Ordinal);
        explicitFingerprints.UnionWith(embeddedCertificates.Cast<X509Certificate2>()
            .Select(GetFingerprint)
            .OfType<string>());
        if (chain.ChainElements.Cast<X509ChainElement>().Any(element =>
                !explicitFingerprints.Contains(GetFingerprint(element.Certificate)!)))
        {
            return Part21SignatureTrustStatus.Untrusted;
        }

        var chainRoot = chain.ChainElements[^1].Certificate;
        return verification.IsTrustedRoot(GetFingerprint(chainRoot)!)
            ? Part21SignatureTrustStatus.Trusted
            : Part21SignatureTrustStatus.Untrusted;
    }

    private static void ThrowIfRejected(
        Part21Signature signature,
        int signatureIndex,
        Part21SignatureVerificationOptions? options)
    {
        if (options is null)
            return;

        var failures = signature.Signers.Select((signer, signerIndex) => (signer, signerIndex))
            .Where(item => !options.AcceptancePolicy.Accepts(item.signer))
            .Select(item => new ValidationFailure(
                item.signer.CryptographicStatus == Part21SignatureCryptographicStatus.Invalid
                    ? "P21.SIGNATURE.CRYPTOGRAPHICALLY_INVALID"
                    : $"P21.SIGNATURE.{item.signer.TrustStatus.ToString().ToUpperInvariant()}",
                $"Signatures[{signatureIndex}].Signers[{item.signerIndex}]",
                "The evaluated CMS signer is rejected by the explicit signature acceptance policy."))
            .ToArray();
        if (failures.Length > 0)
            throw new ExchangeStructureReadValidationException(new ValidationResult(failures));
    }

    private static bool TryDecodeCanonicalBase64(string value, out byte[] result)
    {
        result = Array.Empty<byte>();
        if (value.Length == 0 || value.Length % 4 != 0)
            return false;
        try
        {
            result = Convert.FromBase64String(value);
            return Convert.ToBase64String(result).Equals(value, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal static byte[]? TryComputeDigest(string algorithm, ReadOnlySpan<byte> content)
    {
        var name = algorithm switch
        {
            "1.2.840.113549.2.5" => HashAlgorithmName.MD5,
            "1.3.14.3.2.26" => HashAlgorithmName.SHA1,
            "2.16.840.1.101.3.4.2.1" => HashAlgorithmName.SHA256,
            "2.16.840.1.101.3.4.2.2" => HashAlgorithmName.SHA384,
            "2.16.840.1.101.3.4.2.3" => HashAlgorithmName.SHA512,
            _ => new HashAlgorithmName(algorithm),
        };
        try
        {
            using var hash = IncrementalHash.CreateHash(name);
            hash.AppendData(content);
            return hash.GetHashAndReset();
        }
        catch (Exception exception) when (exception is CryptographicException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    private static string? GetFingerprint(X509Certificate2? certificate) => certificate?.GetCertHashString(
        HashAlgorithmName.SHA256);

    private static ExchangeStructureBindingException Malformed(
        string code,
        string message,
        SourceLocation location) => new([
            new Step21Diagnostic(code, Step21DiagnosticSeverity.Error, message, location),
        ]);

    private static ExchangeStructureCapabilityException Capability(string code, string message) => new([
        new Step21Diagnostic(code, Step21DiagnosticSeverity.Error, message),
    ]);

    private sealed class VerificationContext : IDisposable
    {
        internal VerificationContext(Part21SignatureVerificationOptions options)
        {
            Options = options;
            Loaded = options.TrustedRoots.Concat(options.AdditionalCertificates)
                .Select(value => value.Load())
                .ToArray();
            TrustedRootFingerprints = options.TrustedRoots
                .Select(static value => value.Fingerprint)
                .ToHashSet(StringComparer.Ordinal);
            ExtraStore = new X509Certificate2Collection(Loaded);
        }

        internal Part21SignatureVerificationOptions Options { get; }

        internal X509Certificate2[] Loaded { get; }

        internal IReadOnlySet<string> TrustedRootFingerprints { get; }

        internal X509Certificate2Collection ExtraStore { get; }

        internal bool IsTrustedRoot(string fingerprint) => TrustedRootFingerprints.Contains(fingerprint);

        public void Dispose()
        {
            foreach (var certificate in Loaded)
                certificate.Dispose();
        }
    }
}