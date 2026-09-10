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
        Guard.NotNull(identity);
        if (kind == Part21ResourceContentKind.Directory)
            throw new ArgumentException("Directory content requires an entry collection.", nameof(kind));
        if (!Enum.IsDefined(typeof(Part21ResourceContentKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        Identity = identity;
        Kind = kind;
        Bytes = bytes;
        Entries = EmptyEntries;
    }

    /// <summary>Creates an in-memory directory from a stable entry snapshot.</summary>
    public Part21ResourceContent(Uri identity, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> entries)
    {
        Guard.NotNull(identity);
        Guard.NotNull(entries);
        if (entries.Keys.Any(static name => name is null))
            throw new ArgumentException("Directory entry names cannot be null.", nameof(entries));

        Identity = identity;
        Kind = Part21ResourceContentKind.Directory;
        Bytes = ReadOnlyMemory<byte>.Empty;
        Entries = new ReadOnlyDictionary<string, ReadOnlyMemory<byte>>(
            entries.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal));
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
        Guard.NegativeOrZero(maximumResourceCount);
        Guard.NegativeOrZero(maximumReferenceDepth);
        Guard.NegativeOrZero(maximumArchiveDepth);
        Guard.NegativeOrZero(maximumTotalBytes);
        Guard.NegativeOrZero(maximumArchiveEntryCount);
        Guard.NegativeOrZero(maximumArchiveUncompressedBytes);
        if (double.IsNaN(maximumCompressionRatio)
            || double.IsInfinity(maximumCompressionRatio)
            || maximumCompressionRatio <= 0)
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
    /// <summary>Creates an immutable per-read resource capability snapshot.</summary>
    public ExchangeStructureReadOptions(
        Uri? baseUri = null,
        IPart21ResourceProvider? resourceProvider = null,
        IPart21ResourceConverter? resourceConverter = null,
        Part21ResourceLimits? resourceLimits = null)
        : this(
            baseUri,
            resourceProvider,
            resourceConverter,
            resourceLimits,
            Part21ProcessingLimits.Default,
            signatureVerification: null,
            domainEquivalenceProvider: null)
    {
    }

    /// <summary>Creates an immutable per-read capability snapshot with explicit shared processing limits.</summary>
    public static ExchangeStructureReadOptions WithProcessingLimits(
        Part21ProcessingLimits processingLimits,
        Uri? baseUri = null,
        IPart21ResourceProvider? resourceProvider = null,
        IPart21ResourceConverter? resourceConverter = null,
        Part21ResourceLimits? resourceLimits = null,
        Part21SignatureVerificationOptions? signatureVerification = null,
        ISchemaDomainEquivalenceProvider? domainEquivalenceProvider = null) => new(
            baseUri,
            resourceProvider,
            resourceConverter,
            resourceLimits,
            processingLimits ?? throw new ArgumentNullException(nameof(processingLimits)),
            signatureVerification,
            domainEquivalenceProvider);

    /// <summary>Creates an immutable per-read signature and resource capability snapshot.</summary>
    public static ExchangeStructureReadOptions WithSignatureVerification(
        Part21SignatureVerificationOptions signatureVerification,
        Uri? baseUri = null,
        IPart21ResourceProvider? resourceProvider = null,
        IPart21ResourceConverter? resourceConverter = null,
        Part21ResourceLimits? resourceLimits = null) => new(
            baseUri,
            resourceProvider,
            resourceConverter,
            resourceLimits,
            Part21ProcessingLimits.Default,
            signatureVerification ?? throw new ArgumentNullException(nameof(signatureVerification)),
            domainEquivalenceProvider: null);

    /// <summary>Creates an immutable per-read domain-equivalence and resource capability snapshot.</summary>
    public static ExchangeStructureReadOptions WithDomainEquivalenceProvider(
        ISchemaDomainEquivalenceProvider domainEquivalenceProvider,
        Uri? baseUri = null,
        IPart21ResourceProvider? resourceProvider = null,
        IPart21ResourceConverter? resourceConverter = null,
        Part21ResourceLimits? resourceLimits = null,
        Part21SignatureVerificationOptions? signatureVerification = null) => new(
            baseUri,
            resourceProvider,
            resourceConverter,
            resourceLimits,
            Part21ProcessingLimits.Default,
            signatureVerification,
            domainEquivalenceProvider ?? throw new ArgumentNullException(nameof(domainEquivalenceProvider)));

    private ExchangeStructureReadOptions(
        Uri? baseUri,
        IPart21ResourceProvider? resourceProvider,
        IPart21ResourceConverter? resourceConverter,
        Part21ResourceLimits? resourceLimits,
        Part21ProcessingLimits processingLimits,
        Part21SignatureVerificationOptions? signatureVerification,
        ISchemaDomainEquivalenceProvider? domainEquivalenceProvider)
    {
        Guard.NotNull(processingLimits);
        if (baseUri is not null && !baseUri.IsAbsoluteUri)
            throw new ArgumentException("A Part 21 base URI must be absolute.", nameof(baseUri));
        if (baseUri is not null)
        {
            if (baseUri.OriginalString.Length > processingLimits.MaximumUriCharacters)
                throw new ArgumentException("A Part 21 base URI exceeds the configured URI limit.", nameof(baseUri));
            if (baseUri.AbsoluteUri.Length > processingLimits.MaximumUriCharacters)
                throw new ArgumentException("A Part 21 base URI exceeds the configured URI limit.", nameof(baseUri));
        }

        BaseUri = baseUri;
        ResourceProvider = resourceProvider;
        ResourceConverter = resourceConverter;
        ResourceLimits = resourceLimits ?? new Part21ResourceLimits();
        ProcessingLimits = processingLimits;
        SignatureVerification = signatureVerification;
        DomainEquivalenceProvider = domainEquivalenceProvider;
        DomainEquivalences = SnapshotDomainEquivalences(domainEquivalenceProvider?.GetEquivalences());
    }

    /// <summary>Gets the optional absolute identity of the character source.</summary>
    public Uri? BaseUri { get; }

    /// <summary>Gets the only capability allowed to acquire external bytes.</summary>
    public IPart21ResourceProvider? ResourceProvider { get; }

    /// <summary>Gets the optional other-format conversion capability.</summary>
    public IPart21ResourceConverter? ResourceConverter { get; }

    /// <summary>Gets the per-read resource limits.</summary>
    public Part21ResourceLimits ResourceLimits { get; }

    /// <summary>Gets the shared read, CMS, URI, archive-entry, and binding limits.</summary>
    public Part21ProcessingLimits ProcessingLimits { get; }

    /// <summary>Gets the optional explicit CMS certificate, time, revocation, and acceptance inputs.</summary>
    public Part21SignatureVerificationOptions? SignatureVerification { get; }

    /// <summary>Gets the optional caller-owned domain-equivalence and parameter-projection capability.</summary>
    public ISchemaDomainEquivalenceProvider? DomainEquivalenceProvider { get; }

    /// <summary>Gets the validated caller-supplied SDAI domain-equivalence relation.</summary>
    public IReadOnlyList<SchemaDomainEquivalence> DomainEquivalences { get; }

    private static IReadOnlyList<SchemaDomainEquivalence> SnapshotDomainEquivalences(
        IReadOnlyCollection<SchemaDomainEquivalence>? equivalences)
    {
        if (equivalences is null)
            return Array.Empty<SchemaDomainEquivalence>();
        var snapshot = equivalences.ToArray();
        if (snapshot.Any(equivalence => equivalence is null))
            throw new ArgumentException("Domain-equivalence collections cannot contain null values.", nameof(equivalences));

        var edges = new HashSet<(SchemaEntityType Source, SchemaEntityType Target)>();
        foreach (var equivalence in snapshot)
        {
            if (!edges.Add((equivalence.Source, equivalence.Target)))
                throw new ArgumentException("Domain-equivalence declarations cannot contain duplicate edges.", nameof(equivalences));
        }

        var nodes = edges.SelectMany(edge => new[] { edge.Source, edge.Target }).Distinct().ToArray();
        foreach (var node in nodes)
            edges.Add((node, node));
        foreach (var edge in edges.Where(edge => !edge.Source.Equals(edge.Target)).ToArray())
        {
            if (!edges.Contains((edge.Target, edge.Source)))
                throw new ArgumentException($"Domain equivalence is asymmetric at '{edge.Source}' and '{edge.Target}'.", nameof(equivalences));
        }
        foreach (var left in edges.ToArray())
        {
            foreach (var right in edges.Where(edge => edge.Source.Equals(left.Target)).ToArray())
            {
                if (!edges.Contains((left.Source, right.Target)))
                {
                    throw new ArgumentException(
                        $"Domain equivalence is not transitively closed at '{left.Source}', '{left.Target}', and '{right.Target}'.",
                        nameof(equivalences));
                }
            }
        }
        foreach (var node in nodes)
        {
            var contradictorySchema = edges
                .Where(edge => edge.Source.Equals(node))
                .Select(edge => edge.Target)
                .GroupBy(target => target.SchemaName.Value, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group
                    .Select(target => target.EntityName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Skip(1)
                    .Any());
            if (contradictorySchema is not null)
            {
                throw new ArgumentException(
                    $"Domain equivalence is contradictory because '{node}' maps to more than one entity in "
                        + $"schema '{contradictorySchema.Key}'.",
                    nameof(equivalences));
            }
        }

        return Array.AsReadOnly(snapshot);
    }
}
