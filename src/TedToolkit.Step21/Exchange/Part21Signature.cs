using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;

namespace TedToolkit.Step21;

/// <summary>Describes cryptographic verification of one CMS signer.</summary>
public enum Part21SignatureCryptographicStatus
{
    /// <summary>The CMS structure was decoded but cryptographic verification was not requested.</summary>
    NotEvaluated = 0,

    /// <summary>The CMS signature does not verify the preceding Part 21 characters.</summary>
    Invalid = 1,

    /// <summary>The CMS signature verifies the preceding Part 21 characters.</summary>
    Valid = 2,
}

/// <summary>Describes caller-bounded certificate trust for one CMS signer.</summary>
public enum Part21SignatureTrustStatus
{
    /// <summary>Trust was not evaluated.</summary>
    NotEvaluated = 0,

    /// <summary>No signer certificate could be resolved from the CMS or supplied certificates.</summary>
    UnknownSigner = 1,

    /// <summary>The signer certificate is outside its validity interval at the supplied verification time.</summary>
    Expired = 2,

    /// <summary>The signer certificate appears in the caller-supplied revoked-certificate set.</summary>
    Revoked = 3,

    /// <summary>The signer does not chain to a caller-supplied trust root.</summary>
    Untrusted = 4,

    /// <summary>The signer chains to a caller-supplied trust root.</summary>
    Trusted = 5,
}

/// <summary>Retains one immutable DER-encoded X.509 certificate for explicit signature trust input.</summary>
public sealed class Part21Certificate
{
    private readonly byte[] _encoded;

    /// <summary>Creates a certificate snapshot from one complete DER encoding.</summary>
    public Part21Certificate(ReadOnlySpan<byte> encoded)
    {
        if (encoded.IsEmpty)
            throw new ArgumentException("A certificate encoding cannot be empty.", nameof(encoded));

        _encoded = encoded.ToArray();
        try
        {
            using var certificate = X509CertificateLoader.LoadCertificate(_encoded);
            Fingerprint = certificate.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256);
        }
        catch (System.Security.Cryptography.CryptographicException exception)
        {
            throw new ArgumentException("The value is not a valid DER-encoded X.509 certificate.", nameof(encoded), exception);
        }
    }

    /// <summary>Gets the upper-case SHA-256 certificate fingerprint.</summary>
    public string Fingerprint { get; }

    /// <summary>Gets the immutable encoded certificate bytes.</summary>
    public ReadOnlyMemory<byte> Encoded => _encoded.ToArray();

    internal X509Certificate2 Load() => X509CertificateLoader.LoadCertificate(_encoded);
}

/// <summary>Reports cryptographic and trust results for one CMS signer.</summary>
public sealed class Part21SignatureSignerResult
{
    internal Part21SignatureSignerResult(
        string? certificateFingerprint,
        Part21SignatureCryptographicStatus cryptographicStatus,
        Part21SignatureTrustStatus trustStatus)
    {
        CertificateFingerprint = certificateFingerprint;
        CryptographicStatus = cryptographicStatus;
        TrustStatus = trustStatus;
    }

    /// <summary>Gets the SHA-256 signer-certificate fingerprint, or <see langword="null"/>.</summary>
    public string? CertificateFingerprint { get; }

    /// <summary>Gets the detached-content cryptographic result.</summary>
    public Part21SignatureCryptographicStatus CryptographicStatus { get; }

    /// <summary>Gets the explicit trust/time/revocation result.</summary>
    public Part21SignatureTrustStatus TrustStatus { get; }
}

/// <summary>Represents one standard signature section and all of its CMS signer results.</summary>
public sealed class Part21Signature
{
    private readonly ReadOnlyCollection<Part21SignatureSignerResult> _signers;
    private readonly byte[]? _messageDigest;

    internal Part21Signature(
        string content,
        string digestAlgorithm,
        byte[]? messageDigest,
        IEnumerable<Part21SignatureSignerResult> signers)
    {
        Content = content;
        DigestAlgorithm = digestAlgorithm;
        _messageDigest = messageDigest;
        _signers = Array.AsReadOnly(signers.ToArray());
    }

    /// <summary>Gets the canonical Base64 CMS spelling retained from the source.</summary>
    public string Content { get; }

    /// <summary>Gets the first CMS signer's digest-algorithm object identifier.</summary>
    public string DigestAlgorithm { get; }

    /// <summary>Gets the digest of the preceding standard characters, or <see langword="null"/> if unavailable.</summary>
    public ReadOnlyMemory<byte>? MessageDigest => _messageDigest?.ToArray();

    /// <summary>Gets every CMS signer result in encoded order.</summary>
    public IReadOnlyList<Part21SignatureSignerResult> Signers => _signers;
}
