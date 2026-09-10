using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace TedToolkit.Step21;

/// <summary>Controls which evaluated signature failures may still publish a complete exchange structure.</summary>
public sealed class Part21SignatureAcceptancePolicy
{
    /// <summary>Creates an explicit evaluated-signature acceptance policy.</summary>
    public Part21SignatureAcceptancePolicy(
        bool acceptCryptographicallyInvalid = false,
        bool acceptUnknownSigner = false,
        bool acceptExpired = false,
        bool acceptRevoked = false,
        bool acceptUntrusted = false)
    {
        AcceptCryptographicallyInvalid = acceptCryptographicallyInvalid;
        AcceptUnknownSigner = acceptUnknownSigner;
        AcceptExpired = acceptExpired;
        AcceptRevoked = acceptRevoked;
        AcceptUntrusted = acceptUntrusted;
    }

    /// <summary>Gets whether a cryptographically invalid signer is accepted.</summary>
    public bool AcceptCryptographicallyInvalid { get; }

    /// <summary>Gets whether a signer without a resolvable certificate is accepted.</summary>
    public bool AcceptUnknownSigner { get; }

    /// <summary>Gets whether an expired or not-yet-valid signer is accepted.</summary>
    public bool AcceptExpired { get; }

    /// <summary>Gets whether a caller-declared revoked signer is accepted.</summary>
    public bool AcceptRevoked { get; }

    /// <summary>Gets whether a signer outside the caller's trust roots is accepted.</summary>
    public bool AcceptUntrusted { get; }

    internal bool Accepts(Part21SignatureSignerResult result) => result.CryptographicStatus switch
    {
        Part21SignatureCryptographicStatus.Invalid => AcceptCryptographicallyInvalid,
        Part21SignatureCryptographicStatus.Valid => result.TrustStatus switch
        {
            Part21SignatureTrustStatus.Trusted => true,
            Part21SignatureTrustStatus.UnknownSigner => AcceptUnknownSigner,
            Part21SignatureTrustStatus.Expired => AcceptExpired,
            Part21SignatureTrustStatus.Revoked => AcceptRevoked,
            Part21SignatureTrustStatus.Untrusted => AcceptUntrusted,
            _ => false,
        },
        Part21SignatureCryptographicStatus.NotEvaluated
            when result.TrustStatus == Part21SignatureTrustStatus.UnknownSigner => AcceptUnknownSigner,
        _ => true,
    };
}

/// <summary>Supplies all deterministic certificate, time, revocation, and acceptance inputs for one read.</summary>
public sealed class Part21SignatureVerificationOptions
{
    private readonly ReadOnlyCollection<Part21Certificate> _trustedRoots;
    private readonly ReadOnlyCollection<Part21Certificate> _additionalCertificates;
    private readonly ISet<string> _revokedFingerprints;

    /// <summary>
    /// Creates an immutable verification input snapshot. Revocation entries identify signer
    /// certificates; chain revocation remains entirely caller-supplied and does not use network state.
    /// </summary>
    public Part21SignatureVerificationOptions(
        DateTimeOffset verificationTime,
        IEnumerable<Part21Certificate> trustedRoots,
        IEnumerable<Part21Certificate>? additionalCertificates = null,
        IEnumerable<Part21Certificate>? revokedCertificates = null,
        Part21SignatureAcceptancePolicy? acceptancePolicy = null)
    {
        VerificationTime = verificationTime;
        _trustedRoots = Snapshot(trustedRoots, nameof(trustedRoots));
        _additionalCertificates = Snapshot(additionalCertificates ?? [], nameof(additionalCertificates));
        _revokedFingerprints = new HashSet<string>(
            Snapshot(revokedCertificates ?? [], nameof(revokedCertificates)).Select(value => value.Fingerprint),
            StringComparer.Ordinal);
        AcceptancePolicy = acceptancePolicy ?? new Part21SignatureAcceptancePolicy();
    }

    /// <summary>Gets the caller-supplied verification instant.</summary>
    public DateTimeOffset VerificationTime { get; }

    /// <summary>Gets the caller-supplied custom trust roots.</summary>
    public IReadOnlyList<Part21Certificate> TrustedRoots => _trustedRoots;

    /// <summary>Gets caller-supplied intermediate or signer certificates.</summary>
    public IReadOnlyList<Part21Certificate> AdditionalCertificates => _additionalCertificates;

    /// <summary>Gets the evaluated-state acceptance policy.</summary>
    public Part21SignatureAcceptancePolicy AcceptancePolicy { get; }

    internal bool IsRevoked(string fingerprint) => _revokedFingerprints.Contains(fingerprint);

    private static ReadOnlyCollection<Part21Certificate> Snapshot(
        IEnumerable<Part21Certificate> certificates,
        string parameterName)
    {
        Guard.NotNull(certificates, parameterName);
        var result = certificates.ToArray();
        if (result.Any(static certificate => certificate is null))
            throw new ArgumentException("Certificate collections cannot contain null values.", parameterName);
        return Array.AsReadOnly(result);
    }
}

/// <summary>Creates one detached CMS signature over the supplied standard Part 21 content bytes.</summary>
public interface IPart21SignatureSigner
{
    /// <summary>
    /// Returns one complete DER-encoded detached CMS SignedData value. The input is valid only for
    /// this call and must not be retained or mutated.
    /// </summary>
    ReadOnlyMemory<byte> Sign(ReadOnlyMemory<byte> content);
}

/// <summary>Selects the standard physical encoding used for non-ASCII characters in written STRING tokens.</summary>
public enum Part21StringEncoding
{
    /// <summary>Uses X2 for BMP characters and X4 for supplementary characters.</summary>
    Canonical = 0,

    /// <summary>Writes ISO 10646 characters directly for an enclosing UTF-8 stream.</summary>
    Utf8 = 1,

    /// <summary>Uses the two-hex-digit X directive and rejects characters above U+00FF.</summary>
    X = 2,

    /// <summary>Uses P and S directives for ISO 8859 pages 1 through 9.</summary>
    Iso8859 = 3,

    /// <summary>Uses X2 UTF-16 code units for every non-ASCII character.</summary>
    X2 = 4,

    /// <summary>Uses X4 Unicode scalar values for every non-ASCII character.</summary>
    X4 = 5,
}

/// <summary>Creates detached CMS SignedData with one caller-owned X.509 private key.</summary>
public sealed class Part21CmsSigner : IPart21SignatureSigner
{
    private readonly X509Certificate2 _certificate;
    private readonly Oid _digestAlgorithm;

    /// <summary>Creates a signer that retains, but does not own, the supplied certificate and private key.</summary>
    public Part21CmsSigner(X509Certificate2 certificate, Oid? digestAlgorithm = null)
    {
        Guard.NotNull(certificate);
        if (!certificate.HasPrivateKey)
            throw new ArgumentException("A CMS signer certificate must have a private key.", nameof(certificate));
        _certificate = certificate;
        _digestAlgorithm = digestAlgorithm ?? new Oid(Oids.Sha256);
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> Sign(ReadOnlyMemory<byte> content)
    {
        var cms = new SignedCms(new ContentInfo(content.ToArray()), detached: true);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, _certificate)
        {
            DigestAlgorithm = _digestAlgorithm,
            IncludeOption = X509IncludeOption.EndCertOnly,
        };
        cms.ComputeSignature(signer, silent: true);
        return cms.Encode();
    }

    private static class Oids
    {
        internal const string Sha256 = "2.16.840.1.101.3.4.2.1";
    }
}

/// <summary>Supplies explicit encoding, limits, and optional signing capabilities for one atomic write.</summary>
public sealed class ExchangeStructureWriteOptions
{
    private readonly IReadOnlyList<IPart21SignatureSigner> _signers;

    /// <summary>Creates unsigned write options with the selected standard string encoding.</summary>
    public ExchangeStructureWriteOptions(Part21StringEncoding stringEncoding)
        : this([], Part21ProcessingLimits.Default, stringEncoding)
    {
    }

    /// <summary>Creates unsigned write options with explicit limits and standard string encoding.</summary>
    public ExchangeStructureWriteOptions(
        Part21ProcessingLimits processingLimits,
        Part21StringEncoding stringEncoding)
        : this([], processingLimits, stringEncoding)
    {
    }

    /// <summary>Creates an immutable signer snapshot in signature-section order.</summary>
    public ExchangeStructureWriteOptions(IEnumerable<IPart21SignatureSigner> signers)
        : this(signers, Part21ProcessingLimits.Default, Part21StringEncoding.Canonical)
    {
    }

    /// <summary>Creates an immutable signer snapshot with explicit shared processing limits.</summary>
    public ExchangeStructureWriteOptions(
        IEnumerable<IPart21SignatureSigner> signers,
        Part21ProcessingLimits processingLimits)
        : this(signers, processingLimits, Part21StringEncoding.Canonical)
    {
    }

    /// <summary>Creates an immutable signer snapshot with explicit limits and standard string encoding.</summary>
    public ExchangeStructureWriteOptions(
        IEnumerable<IPart21SignatureSigner> signers,
        Part21ProcessingLimits processingLimits,
        Part21StringEncoding stringEncoding)
    {
        Guard.NotNull(signers);
        Guard.NotNull(processingLimits);
        if (!Enum.IsDefined(typeof(Part21StringEncoding), stringEncoding))
            throw new ArgumentOutOfRangeException(nameof(stringEncoding));
        var result = signers.ToArray();
        if (result.Any(static signer => signer is null))
            throw new ArgumentException("Signer collections cannot contain null values.", nameof(signers));
        _signers = result.Length == 0
            ? Array.Empty<IPart21SignatureSigner>()
            : Array.AsReadOnly(result);
        ProcessingLimits = processingLimits;
        StringEncoding = stringEncoding;
    }

    /// <summary>Gets signing capabilities in signature-section order.</summary>
    public IReadOnlyList<IPart21SignatureSigner> Signers => _signers;

    /// <summary>Gets the shared output, callback, and CMS limits.</summary>
    public Part21ProcessingLimits ProcessingLimits { get; }

    /// <summary>Gets the standard STRING encoding selected for this write.</summary>
    public Part21StringEncoding StringEncoding { get; }
}
