namespace TedToolkit.Step21;

/// <summary>Bounds untrusted ISO 10303-21 parsing, signing, writing, and language-binding work.</summary>
public sealed class Part21ProcessingLimits
{
    /// <summary>Gets the shared finite defaults used when a caller does not supply limits.</summary>
    public static Part21ProcessingLimits Default { get; } = new();

    /// <summary>Creates positive processing limits for one read, write, or Annex F bridge.</summary>
    public Part21ProcessingLimits(
        int maximumInputCharacters = 67108864,
        int maximumOutputCharacters = 67108864,
        int maximumUriCharacters = 8192,
        int maximumSignatureCount = 64,
        int maximumSignatureBytes = 16777216,
        int maximumTotalSignatureBytes = 67108864,
        int maximumCmsSignerCount = 64,
        int maximumNestingDepth = 128,
        int maximumItemCount = 1048576,
        long maximumArchiveEntryBytes = 67108864)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumInputCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOutputCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumUriCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSignatureCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSignatureBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTotalSignatureBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCmsSignerCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumNestingDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItemCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumArchiveEntryBytes);

        MaximumInputCharacters = maximumInputCharacters;
        MaximumOutputCharacters = maximumOutputCharacters;
        MaximumUriCharacters = maximumUriCharacters;
        MaximumSignatureCount = maximumSignatureCount;
        MaximumSignatureBytes = maximumSignatureBytes;
        MaximumTotalSignatureBytes = maximumTotalSignatureBytes;
        MaximumCmsSignerCount = maximumCmsSignerCount;
        MaximumNestingDepth = maximumNestingDepth;
        MaximumItemCount = maximumItemCount;
        MaximumArchiveEntryBytes = maximumArchiveEntryBytes;
    }

    /// <summary>Gets the maximum characters accepted from a root source or bridge state.</summary>
    public int MaximumInputCharacters { get; }

    /// <summary>Gets the maximum characters staged before one atomic output publication.</summary>
    public int MaximumOutputCharacters { get; }

    /// <summary>Gets the maximum characters in one URI presented to a provider or bridge.</summary>
    public int MaximumUriCharacters { get; }

    /// <summary>Gets the maximum signature sections or signing callbacks in one operation.</summary>
    public int MaximumSignatureCount { get; }

    /// <summary>Gets the maximum DER-encoded bytes in one CMS signature.</summary>
    public int MaximumSignatureBytes { get; }

    /// <summary>Gets the maximum aggregate DER-encoded CMS bytes in one operation.</summary>
    public int MaximumTotalSignatureBytes { get; }

    /// <summary>Gets the maximum CMS signer records in one signature section.</summary>
    public int MaximumCmsSignerCount { get; }

    /// <summary>Gets the maximum nested value depth at a language-binding boundary.</summary>
    public int MaximumNestingDepth { get; }

    /// <summary>Gets the maximum values, anchors, tags, or populations at a language-binding boundary.</summary>
    public int MaximumItemCount { get; }

    /// <summary>Gets the maximum uncompressed bytes retained for one archive or directory entry.</summary>
    public long MaximumArchiveEntryBytes { get; }
}