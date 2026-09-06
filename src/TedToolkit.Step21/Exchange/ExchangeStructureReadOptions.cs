using System.Collections.ObjectModel;

namespace TedToolkit.Step21;

/// <summary>Identifies the representation supplied for one explicitly requested Part 21 resource.</summary>
public enum Part21ResourceContentKind
{
    /// <summary>UTF-8 encoded ISO 10303-21 clear text.</summary>
    ClearText = 0,

    /// <summary>A PKZip-compatible archive whose root entry is named <c>ISO-10303.p21</c>.</summary>
    ZipArchive = 1,

    /// <summary>An in-memory directory whose root entry is named <c>ISO-10303.p21</c>.</summary>
    Directory = 2,

    /// <summary>Content that requires the caller-supplied conversion capability.</summary>
    Other = 3,
}

/// <summary>Supplies schema-neutral bytes or directory entries for one resolved resource identity.</summary>
public sealed class Part21ResourceContent
{
    private static readonly IReadOnlyDictionary<string, ReadOnlyMemory<byte>> EmptyEntries =
        new ReadOnlyDictionary<string, ReadOnlyMemory<byte>>(
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal));

    /// <summary>Creates byte-oriented clear text, archive, or other-format content.</summary>
    public Part21ResourceContent(Uri identity, Part21ResourceContentKind kind, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (kind == Part21ResourceContentKind.Directory)
            throw new ArgumentException("Directory content requires an entry collection.", nameof(kind));
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        Identity = identity;
        Kind = kind;
        Bytes = bytes;
        Entries = EmptyEntries;
    }

    /// <summary>Creates an in-memory directory from a stable entry snapshot.</summary>
    public Part21ResourceContent(Uri identity, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> entries)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Keys.Any(static name => name is null))
            throw new ArgumentException("Directory entry names cannot be null.", nameof(entries));

        Identity = identity;
        Kind = Part21ResourceContentKind.Directory;
        Bytes = ReadOnlyMemory<byte>.Empty;
        Entries = new ReadOnlyDictionary<string, ReadOnlyMemory<byte>>(
            new Dictionary<string, ReadOnlyMemory<byte>>(entries, StringComparer.Ordinal));
    }

    /// <summary>Gets the stable identity used by the per-read cache.</summary>
    public Uri Identity { get; }

    /// <summary>Gets the supplied representation.</summary>
    public Part21ResourceContentKind Kind { get; }

    /// <summary>Gets the supplied bytes; empty for directory content.</summary>
    public ReadOnlyMemory<byte> Bytes { get; }

    /// <summary>Gets the snapshotted directory entries; empty for byte content.</summary>
    public IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Entries { get; }
}

/// <summary>Explicitly supplies raw resources without granting implicit network or file-system access.</summary>
public interface IPart21ResourceProvider
{
    /// <summary>Gets one resource, or <see langword="null"/> when the identity cannot be delivered.</summary>
    Part21ResourceContent? GetResource(Uri resourceIdentity);
}

/// <summary>Explicitly converts another representation into clear text, archive, or directory content.</summary>
public interface IPart21ResourceConverter
{
    /// <summary>Converts caller-supplied other-format content, or returns <see langword="null"/>.</summary>
    Part21ResourceContent? Convert(Part21ResourceContent content);
}

/// <summary>Bounds untrusted resource and archive work for one read operation.</summary>
public sealed class Part21ResourceLimits
{
    /// <summary>Creates positive per-read resource limits.</summary>
    public Part21ResourceLimits(
        int maximumResourceCount = 64,
        int maximumReferenceDepth = 16,
        int maximumArchiveDepth = 8,
        long maximumTotalBytes = 268435456,
        int maximumArchiveEntryCount = 4096,
        long maximumArchiveUncompressedBytes = 268435456,
        double maximumCompressionRatio = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResourceCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumReferenceDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumArchiveDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTotalBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumArchiveEntryCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumArchiveUncompressedBytes);
        if (!double.IsFinite(maximumCompressionRatio) || maximumCompressionRatio <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumCompressionRatio));

        MaximumResourceCount = maximumResourceCount;
        MaximumReferenceDepth = maximumReferenceDepth;
        MaximumArchiveDepth = maximumArchiveDepth;
        MaximumTotalBytes = maximumTotalBytes;
        MaximumArchiveEntryCount = maximumArchiveEntryCount;
        MaximumArchiveUncompressedBytes = maximumArchiveUncompressedBytes;
        MaximumCompressionRatio = maximumCompressionRatio;
    }

    /// <summary>Gets the maximum provider acquisitions during one read.</summary>
    public int MaximumResourceCount { get; }

    /// <summary>Gets the maximum reference or nested-archive depth.</summary>
    public int MaximumReferenceDepth { get; }

    /// <summary>Gets the maximum nested ZIP archive depth.</summary>
    public int MaximumArchiveDepth { get; }

    /// <summary>Gets the maximum total supplied byte count.</summary>
    public long MaximumTotalBytes { get; }

    /// <summary>Gets the maximum entry count across all opened archives or directories.</summary>
    public int MaximumArchiveEntryCount { get; }

    /// <summary>Gets the maximum uncompressed byte count across all opened archives or directories.</summary>
    public long MaximumArchiveUncompressedBytes { get; }

    /// <summary>Gets the maximum allowed uncompressed-to-compressed ratio for each ZIP entry.</summary>
    public double MaximumCompressionRatio { get; }
}

/// <summary>Binds explicit resource capabilities and quotas to one exchange-structure read.</summary>
public sealed class ExchangeStructureReadOptions
{
    /// <summary>Creates an immutable per-read capability snapshot.</summary>
    public ExchangeStructureReadOptions(
        Uri? baseUri = null,
        IPart21ResourceProvider? resourceProvider = null,
        IPart21ResourceConverter? resourceConverter = null,
        Part21ResourceLimits? resourceLimits = null)
    {
        if (baseUri is not null && !baseUri.IsAbsoluteUri)
            throw new ArgumentException("A Part 21 base URI must be absolute.", nameof(baseUri));

        BaseUri = baseUri;
        ResourceProvider = resourceProvider;
        ResourceConverter = resourceConverter;
        ResourceLimits = resourceLimits ?? new Part21ResourceLimits();
    }

    /// <summary>Gets the optional absolute identity of the character source.</summary>
    public Uri? BaseUri { get; }

    /// <summary>Gets the only capability allowed to acquire external bytes.</summary>
    public IPart21ResourceProvider? ResourceProvider { get; }

    /// <summary>Gets the optional other-format conversion capability.</summary>
    public IPart21ResourceConverter? ResourceConverter { get; }

    /// <summary>Gets the per-read resource limits.</summary>
    public Part21ResourceLimits ResourceLimits { get; }
}
