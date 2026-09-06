using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace TedToolkit.Step21;

// Owns all resource bytes, caches, recursion state, and quotas for exactly one public Read call.
internal sealed class Part21ResourceResolutionContext
{
    private static readonly AsyncLocal<IPart21ResourceProvider?> ActiveProvider = new();
    private readonly IReadOnlyCollection<SchemaDescriptor> _descriptors;
    private readonly ExchangeStructureReadOptions _options;
    private readonly Dictionary<string, LoadedDocument?> _documents = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeTargets = new(StringComparer.Ordinal);
    private int _resourceCount;
    private int _archiveEntryCount;
    private long _totalBytes;
    private long _archiveBytes;

    internal Part21ResourceResolutionContext(
        IReadOnlyCollection<SchemaDescriptor> descriptors,
        ExchangeStructureReadOptions options)
    {
        _descriptors = descriptors;
        _options = options;
    }

    internal DocumentAddress RootAddress => new(
        _options.BaseUri?.AbsoluteUri ?? "<reader>",
        _options.BaseUri,
        Container: null,
        EntryPath: null);

    internal void ResolveReferences(ExchangeStructure structure, DocumentAddress address, int depth)
    {
        EnsureDepth(depth);
        foreach (var reference in structure.ReferenceEntries)
            ResolveReference(structure, address, reference, depth);
    }

    private void ResolveReference(
        ExchangeStructure structure,
        DocumentAddress address,
        Part21Reference reference,
        int depth)
    {
        if (reference.ResolutionStatus != Part21ReferenceResolutionStatus.Unresolved)
            return;
        EnsureDepth(depth);
        var targetKey = address.Key + "|" + reference.Resource.Value;
        ParameterValue? value;
        if (!_activeTargets.Add(targetKey))
        {
            value = ParameterValue.Omitted;
        }
        else
        {
            try
            {
                value = ResolveResource(structure, address, reference.Resource.Value, depth + 1);
            }
            finally
            {
                _ = _activeTargets.Remove(targetKey);
            }
        }

        if (value is not null && !MatchesReferenceKind(reference.Kind, value))
            value = ParameterValue.Omitted;
        structure.SetResolvedReference(reference, value ?? ParameterValue.Omitted);
    }

    internal void RegisterDocument(ExchangeStructure structure, DocumentAddress address)
    {
        _documents[address.Key] = new LoadedDocument(structure, address);
    }

    private ParameterValue? ResolveResource(
        ExchangeStructure owner,
        DocumentAddress address,
        string resource,
        int depth)
    {
        EnsureDepth(depth);
        var hash = resource.IndexOf('#');
        if (hash < 0 || hash == resource.Length - 1)
            return ParameterValue.Omitted;

        var path = resource[..hash];
        var fragment = resource[(hash + 1)..];
        if (path.Length == 0 && !Guid.TryParse(fragment, out _))
            return ResolveFragment(owner, address, fragment, depth);

        LoadedDocument? document;
        if (path.Length == 0)
        {
            document = AcquireProviderDocument(new Uri(resource, UriKind.Relative), depth);
        }
        else if (address.Container is not null && !Uri.TryCreate(path, UriKind.Absolute, out _))
        {
            document = LoadContainerEntry(address, path, depth);
        }
        else
        {
            var identity = ResolveExternalIdentity(address.BaseUri, path);
            document = AcquireProviderDocument(identity, depth);
        }

        return document is null
            ? ParameterValue.Omitted
            : ResolveFragment(document.Structure, document.Address, fragment, depth);
    }

    private ParameterValue ResolveFragment(
        ExchangeStructure structure,
        DocumentAddress address,
        string fragment,
        int depth)
    {
        EnsureDepth(depth);
        var key = address.Key + "#" + fragment;
        if (!_activeTargets.Add(key))
            return ParameterValue.Omitted;

        try
        {
            if (fragment.Length > 0 && fragment.All(static character => character is >= '0' and <= '9'))
            {
                try
                {
                    return structure.TryGetEntity(new EntityInstanceName(fragment), out var entity)
                        ? ParameterValue.FromEntity(entity!)
                        : ParameterValue.Omitted;
                }
                catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
                {
                    return ParameterValue.Omitted;
                }
            }

            Part21Anchor? anchor;
            try
            {
                var name = new AnchorName(fragment);
                anchor = structure.AnchorEntries.SingleOrDefault(candidate => candidate.Name.Equals(name));
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                return ParameterValue.Omitted;
            }

            return anchor is null
                ? ParameterValue.Omitted
                : ResolveAnchorItem(structure, address, anchor.Item, depth);
        }
        finally
        {
            _ = _activeTargets.Remove(key);
        }
    }

    private ParameterValue ResolveAnchorItem(
        ExchangeStructure structure,
        DocumentAddress address,
        ParameterValue item,
        int depth)
    {
        EnsureDepth(depth);
        if (item.TryGetEntity(out _))
            return item;
        if (item.TryGetEntityInstance(out var entityName))
        {
            if (structure.TryGetEntity(entityName, out var entity))
                return ParameterValue.FromEntity(entity!);
            var reference = structure.ReferenceEntries.SingleOrDefault(candidate =>
                candidate.TryGetEntityInstance(out var candidateName) && candidateName.Equals(entityName));
            if (reference is not null)
            {
                ResolveReference(structure, address, reference, depth + 1);
                if (structure.TryGetEntity(entityName, out entity))
                    return ParameterValue.FromEntity(entity!);
            }
            return ParameterValue.Omitted;
        }
        if (item.TryGetValueInstance(out var valueName))
        {
            var reference = structure.ReferenceEntries.SingleOrDefault(candidate =>
                candidate.TryGetValueInstance(out var candidateName) && candidateName.Equals(valueName));
            if (reference is not null)
                ResolveReference(structure, address, reference, depth + 1);
            return reference is not null && reference.TryGetResolvedValue(out var value) && value is not null
                ? value
                : ParameterValue.Omitted;
        }
        if (item.TryGetResource(out var nested))
            return ResolveResource(structure, address, nested.Value, depth + 1) ?? ParameterValue.Omitted;
        if (item.TryGetAggregate(out var values))
        {
            return ParameterValue.FromAggregate(values.Select(value =>
                ResolveAnchorItem(structure, address, value, depth)));
        }

        return item;
    }

    private LoadedDocument? AcquireProviderDocument(Uri identity, int depth)
    {
        EnsureDepth(depth);
        var key = identity.IsAbsoluteUri ? identity.AbsoluteUri : identity.OriginalString;
        if (_documents.TryGetValue(key, out var cached))
            return cached;
        var provider = _options.ResourceProvider;
        if (provider is null)
            ThrowCapability("P21-CAP-RESOURCE-PROVIDER", $"Resource '{identity}' requires an explicit provider.");
        if (_resourceCount >= _options.ResourceLimits.MaximumResourceCount)
            ThrowLimit("resource count");

        if (ReferenceEquals(ActiveProvider.Value, provider))
            ThrowCapability("P21-RESOURCE-PROVIDER-REENTRY", "The resource provider re-entered the same read operation.");

        _documents.Add(key, null);
        _resourceCount++;
        Part21ResourceContent? content;
        var previous = ActiveProvider.Value;
        try
        {
            ActiveProvider.Value = provider;
            content = provider!.GetResource(identity);
        }
        finally
        {
            ActiveProvider.Value = previous;
        }

        if (content is null)
            return null;
        var loaded = LoadContent(content, depth, archiveDepth: 0);
        _documents[key] = loaded;
        if (loaded is not null)
            _documents[loaded.Address.Key] = loaded;
        return loaded;
    }

    private LoadedDocument? LoadContent(Part21ResourceContent content, int depth, int archiveDepth)
    {
        EnsureDepth(depth);
        if (content.Kind == Part21ResourceContentKind.Other)
        {
            var converter = _options.ResourceConverter;
            if (converter is null)
                ThrowCapability("P21-CAP-RESOURCE-CONVERTER", $"Resource '{content.Identity}' requires an explicit converter.");
            content = converter!.Convert(content)
                ?? throw Capability("P21-RESOURCE-CONVERSION", $"Resource '{content.Identity}' could not be converted.");
            if (content.Kind == Part21ResourceContentKind.Other)
                ThrowCapability("P21-RESOURCE-CONVERSION", "A converter must return clear text, ZIP, or directory content.");
        }

        if (content.Kind == Part21ResourceContentKind.ClearText)
        {
            CountBytes(content.Bytes.Length);
            return ReadClearText(content.Identity, content.Bytes, container: null, entryPath: null, depth);
        }

        var entries = content.Kind == Part21ResourceContentKind.ZipArchive
            ? ReadZip(content.Bytes, archiveDepth + 1)
            : SnapshotDirectory(content.Entries);
        var container = new ResourceContainer(content.Identity, entries, archiveDepth + 1);
        if (!entries.TryGetValue("ISO-10303.p21", out var root))
            ThrowCapability("P21-RESOURCE-ARCHIVE-ROOT", $"Resource '{content.Identity}' has no ISO-10303.p21 root.");
        return ReadClearText(content.Identity, root, container, "ISO-10303.p21", depth);
    }

    private LoadedDocument ReadClearText(
        Uri identity,
        ReadOnlyMemory<byte> bytes,
        ResourceContainer? container,
        string? entryPath,
        int depth)
    {
        EnsureDepth(depth);
        string source;
        try
        {
            source = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes.Span);
        }
        catch (DecoderFallbackException)
        {
            throw Capability("P21-RESOURCE-ENCODING", $"Resource '{identity}' is not valid UTF-8 clear text.");
        }

        var key = container is null ? identity.ToString() : identity + "!/" + entryPath;
        var baseUri = identity.IsAbsoluteUri ? identity : null;
        var address = new DocumentAddress(key, baseUri, container, entryPath);
        var structure = ExchangeStructureReader.Read(source, _descriptors, this, address, depth);
        return new LoadedDocument(structure, address);
    }

    private LoadedDocument? LoadContainerEntry(DocumentAddress address, string relativePath, int depth)
    {
        EnsureDepth(depth);
        var entryPath = ResolveEntryPath(address.EntryPath!, relativePath);
        var container = address.Container!;
        var key = container.Identity + "!/" + entryPath;
        if (_documents.TryGetValue(key, out var cached))
            return cached;
        if (!container.Entries.TryGetValue(entryPath, out var bytes))
            return null;

        _documents.Add(key, null);
        LoadedDocument loaded;
        if (LooksLikeZip(bytes.Span))
        {
            if (container.ArchiveDepth >= _options.ResourceLimits.MaximumArchiveDepth)
                ThrowCapability("P21-RESOURCE-ARCHIVE-RECURSION", "Nested archive depth exceeds the configured limit.");
            var nestedIdentity = new Uri(key, UriKind.RelativeOrAbsolute);
            loaded = LoadContent(
                new Part21ResourceContent(nestedIdentity, Part21ResourceContentKind.ZipArchive, bytes),
                depth,
                container.ArchiveDepth)!;
        }
        else
        {
            loaded = ReadClearText(container.Identity, bytes, container, entryPath, depth);
        }

        _documents[key] = loaded;
        return loaded;
    }

    private IReadOnlyDictionary<string, ReadOnlyMemory<byte>> ReadZip(ReadOnlyMemory<byte> bytes, int archiveDepth)
    {
        if (archiveDepth > _options.ResourceLimits.MaximumArchiveDepth)
            ThrowCapability("P21-RESOURCE-ARCHIVE-RECURSION", "Nested archive depth exceeds the configured limit.");
        CountBytes(bytes.Length);
        var result = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        try
        {
            using var stream = CreateReadStream(bytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    continue;
                if (entry.FullName.Any(static character => character > 0x7f))
                    ThrowCapability("P21-RESOURCE-ARCHIVE-ENTRY", "PKZip 2.04g entry names must use ASCII characters.");
                CountArchiveEntry(entry.FullName, entry.CompressedLength, entry.Length);
                var name = NormalizeEntryPath(entry.FullName);
                using var input = entry.Open();
                using var output = new MemoryStream(entry.Length > int.MaxValue ? 0 : (int)entry.Length);
                input.CopyTo(output);
                if (!result.TryAdd(name, output.ToArray()))
                    ThrowCapability("P21-RESOURCE-ARCHIVE-ENTRY", $"Archive entry '{name}' occurs more than once.");
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            throw Capability("P21-RESOURCE-ARCHIVE", $"The ZIP archive is invalid: {exception.Message}");
        }

        return result;
    }

    private IReadOnlyDictionary<string, ReadOnlyMemory<byte>> SnapshotDirectory(
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> entries)
    {
        var result = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            CountArchiveEntry(entry.Key, compressedLength: entry.Value.Length, uncompressedLength: entry.Value.Length);
            var name = NormalizeEntryPath(entry.Key);
            CountBytes(entry.Value.Length);
            if (!result.TryAdd(name, entry.Value))
                ThrowCapability("P21-RESOURCE-ARCHIVE-ENTRY", $"Directory entry '{name}' occurs more than once.");
        }
        return result;
    }

    private static Uri ResolveExternalIdentity(Uri? baseUri, string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            return absolute;
        if (baseUri is null)
            throw Capability("P21-CAP-RESOURCE-BASE-URI", $"Relative resource '{path}' requires an explicit base URI.");
        return new Uri(baseUri, path);
    }

    private static string ResolveEntryPath(string currentEntry, string relativePath)
    {
        if (relativePath.Contains('\\') || relativePath.StartsWith("/", StringComparison.Ordinal))
            ThrowCapability("P21-RESOURCE-ARCHIVE-PATH", "A relative archive reference cannot be rooted or contain a backslash.");
        var slash = currentEntry.LastIndexOf('/');
        var prefix = slash < 0 ? string.Empty : currentEntry[..(slash + 1)];
        return NormalizeEntryPath(prefix + Uri.UnescapeDataString(relativePath));
    }

    private static string NormalizeEntryPath(string path)
    {
        if (path.Contains('\\') || path.StartsWith("/", StringComparison.Ordinal))
            ThrowCapability("P21-RESOURCE-ARCHIVE-PATH", $"Archive entry '{path}' is not a scoped relative path.");
        var segments = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;
            if (segment == "..")
            {
                if (segments.Count == 0)
                    ThrowCapability("P21-RESOURCE-ARCHIVE-PATH", $"Archive entry '{path}' escapes the archive.");
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            if (segment.Contains(':'))
                ThrowCapability("P21-RESOURCE-ARCHIVE-PATH", $"Archive entry '{path}' contains a drive or scheme separator.");
            segments.Add(segment);
        }
        if (segments.Count == 0)
            ThrowCapability("P21-RESOURCE-ARCHIVE-PATH", "An archive entry path cannot be empty.");
        return string.Join('/', segments);
    }

    private void CountBytes(long length)
    {
        if (length < 0 || length > _options.ResourceLimits.MaximumTotalBytes - _totalBytes)
            ThrowLimit("total resource bytes");
        _totalBytes += length;
    }

    private void CountArchiveEntry(string name, long compressedLength, long uncompressedLength)
    {
        if (_archiveEntryCount >= _options.ResourceLimits.MaximumArchiveEntryCount)
            ThrowLimit("archive entry count");
        _archiveEntryCount++;
        if (uncompressedLength < 0
            || uncompressedLength > int.MaxValue
            || uncompressedLength > _options.ResourceLimits.MaximumArchiveUncompressedBytes - _archiveBytes)
            ThrowLimit("archive uncompressed bytes");
        _archiveBytes += uncompressedLength;
        if (compressedLength == 0 ? uncompressedLength > 0 : uncompressedLength / (double)compressedLength
            > _options.ResourceLimits.MaximumCompressionRatio)
        {
            ThrowCapability("P21-RESOURCE-LIMIT-COMPRESSION-RATIO", $"Archive entry '{name}' exceeds the compression-ratio limit.");
        }
    }

    private void EnsureDepth(int depth)
    {
        if (depth > _options.ResourceLimits.MaximumReferenceDepth)
            ThrowLimit("reference depth");
    }

    private static bool MatchesReferenceKind(Part21ReferenceKind kind, ParameterValue value) => kind switch
    {
        Part21ReferenceKind.EntityInstance => value.Kind is ParameterValueKind.Entity or ParameterValueKind.Omitted,
        Part21ReferenceKind.ValueInstance => value.Kind is not ParameterValueKind.Entity
            and not ParameterValueKind.EntityInstance
            and not ParameterValueKind.Omitted,
        _ => false,
    };

    private static bool LooksLikeZip(ReadOnlySpan<byte> bytes) => bytes.Length >= 4
        && bytes[0] == (byte)'P'
        && bytes[1] == (byte)'K'
        && bytes[2] is 3 or 5 or 7
        && bytes[3] is 4 or 6 or 8;

    private static MemoryStream CreateReadStream(ReadOnlyMemory<byte> bytes)
    {
        if (MemoryMarshal.TryGetArray(bytes, out var segment))
            return new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false, publiclyVisible: true);
        return new MemoryStream(bytes.ToArray(), writable: false);
    }

    private static void ThrowLimit(string limit) =>
        ThrowCapability("P21-RESOURCE-LIMIT", $"The configured {limit} limit was exceeded.");

    private static void ThrowCapability(string code, string message) => throw Capability(code, message);

    private static ExchangeStructureCapabilityException Capability(string code, string message) => new(
        [new Step21Diagnostic(code, Step21DiagnosticSeverity.Error, message)]);

    internal sealed record DocumentAddress(
        string Key,
        Uri? BaseUri,
        ResourceContainer? Container,
        string? EntryPath);

    internal sealed record ResourceContainer(
        Uri Identity,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Entries,
        int ArchiveDepth);

    private sealed record LoadedDocument(ExchangeStructure Structure, DocumentAddress Address);
}