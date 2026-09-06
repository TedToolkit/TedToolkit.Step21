using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace TedToolkit.Step21;

// Owns all resource bytes, caches, recursion state, and quotas for exactly one public Read call.
internal sealed class Part21ResourceResolutionContext
{
    private const string ArchiveRootName = "ISO-10303.p21";
    private static readonly AsyncLocal<CallbackFrame<IPart21ResourceConverter>?> ActiveConverters = new();
    private static readonly AsyncLocal<CallbackFrame<IPart21ResourceProvider>?> ActiveProviders = new();
    private static readonly uint[] Crc32Table = CreateCrc32Table();
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly IReadOnlyCollection<SchemaDescriptor> _descriptors;
    private readonly ExchangeStructureReadOptions _options;
    private readonly Dictionary<string, LoadedDocument?> _documents = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeTargets = new(StringComparer.Ordinal);
    private readonly Stack<DocumentCacheTransaction> _cacheTransactions = new();
    private readonly Stack<LoadingDocumentAliases> _loadingAliases = new();
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
        var document = new LoadedDocument(structure, address);
        SetDocument(address.Key, document);
        if (_loadingAliases.TryPeek(out var loading)
            && loading.AddressKey == address.Key)
        {
            foreach (var alias in loading.Aliases)
                SetDocument(alias, document);
        }
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
        if (path.Length == 0 && !Guid.TryParseExact(fragment, "D", out _))
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

        var previousProviders = ActiveProviders.Value;
        if (ContainsCallback(previousProviders, provider!))
            ThrowCapability("P21-RESOURCE-PROVIDER-REENTRY", "The resource provider re-entered the same read operation.");

        var transaction = BeginCacheTransaction();
        try
        {
            AddDocument(key, null);
            _resourceCount++;
            Part21ResourceContent? content;
            ActiveProviders.Value = new CallbackFrame<IPart21ResourceProvider>(provider!, previousProviders);
            try
            {
                content = provider!.GetResource(identity);
            }
            finally
            {
                ActiveProviders.Value = previousProviders;
            }

            if (content is null)
            {
                CommitCacheTransaction(transaction);
                return null;
            }
            CountSuppliedBytes(content);
            var aliases = new HashSet<string>(StringComparer.Ordinal) { key };
            var providerKey = GetIdentityKey(content.Identity);
            if (!aliases.Contains(providerKey) && _documents.TryGetValue(providerKey, out var providerCached))
            {
                SetDocument(key, providerCached);
                CommitCacheTransaction(transaction);
                return providerCached;
            }
            ReserveAlias(providerKey, aliases);

            var wasConverted = content.Kind == Part21ResourceContentKind.Other;
            content = ConvertContent(content);
            if (wasConverted)
                CountSuppliedBytes(content);
            var convertedKey = GetIdentityKey(content.Identity);
            if (!aliases.Contains(convertedKey) && _documents.TryGetValue(convertedKey, out var convertedCached))
            {
                foreach (var alias in aliases)
                    SetDocument(alias, convertedCached);
                CommitCacheTransaction(transaction);
                return convertedCached;
            }
            ReserveAlias(convertedKey, aliases);

            LoadedDocument? loaded;
            try
            {
                loaded = LoadContent(content, depth, archiveDepth: 0, aliases);
            }
            catch (Exception exception) when (IsExternalStructureFailure(exception))
            {
                RollbackCacheTransaction(transaction);
                foreach (var alias in aliases)
                    SetDocument(alias, null);
                return null;
            }
            foreach (var alias in aliases)
                SetDocument(alias, loaded);
            if (loaded is not null)
                SetDocument(loaded.Address.Key, loaded);
            CommitCacheTransaction(transaction);
            return loaded;
        }
        catch
        {
            RollbackCacheTransaction(transaction);
            throw;
        }
    }

    private static string GetIdentityKey(Uri identity) =>
        identity.IsAbsoluteUri ? identity.AbsoluteUri : identity.OriginalString;

    private void ReserveAlias(string alias, ISet<string> aliases)
    {
        if (aliases.Add(alias))
            SetDocument(alias, null);
    }

    private Part21ResourceContent ConvertContent(Part21ResourceContent content)
    {
        if (content.Kind != Part21ResourceContentKind.Other)
            return content;

        var converter = _options.ResourceConverter;
        if (converter is null)
            ThrowCapability("P21-CAP-RESOURCE-CONVERTER", $"Resource '{content.Identity}' requires an explicit converter.");
        var previousConverters = ActiveConverters.Value;
        if (ContainsCallback(previousConverters, converter!))
            ThrowCapability("P21-RESOURCE-CONVERTER-REENTRY", "The resource converter re-entered the same read operation.");

        ActiveConverters.Value = new CallbackFrame<IPart21ResourceConverter>(converter!, previousConverters);
        Part21ResourceContent? converted;
        try
        {
            converted = converter!.Convert(content);
        }
        finally
        {
            ActiveConverters.Value = previousConverters;
        }

        var convertedContent = converted
            ?? throw Capability("P21-RESOURCE-CONVERSION", $"Resource '{content.Identity}' could not be converted.");
        if (convertedContent.Kind == Part21ResourceContentKind.Other)
            ThrowCapability("P21-RESOURCE-CONVERSION", "A converter must return clear text, ZIP, or directory content.");
        return convertedContent;
    }

    private LoadedDocument? LoadContent(
        Part21ResourceContent content,
        int depth,
        int archiveDepth,
        IReadOnlyCollection<string> aliases)
    {
        EnsureDepth(depth);
        if (content.Kind == Part21ResourceContentKind.ClearText)
        {
            return ReadRoot(content.Identity, content.Bytes, container: null, entryPath: null, depth, aliases);
        }

        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> entries;
        var containerArchiveDepth = archiveDepth;
        if (content.Kind == Part21ResourceContentKind.ZipArchive)
        {
            containerArchiveDepth++;
            entries = ReadZip(content.Bytes, containerArchiveDepth);
        }
        else
        {
            entries = SnapshotDirectory(content.Entries);
        }
        var container = new ResourceContainer(content.Identity, entries, containerArchiveDepth);
        if (!entries.TryGetValue(ArchiveRootName, out var root))
            ThrowCapability("P21-RESOURCE-ARCHIVE-ROOT", $"Resource '{content.Identity}' has no {ArchiveRootName} root.");
        return ReadRoot(content.Identity, root, container, ArchiveRootName, depth, aliases);
    }

    private LoadedDocument ReadRoot(
        Uri identity,
        ReadOnlyMemory<byte> bytes,
        ResourceContainer? container,
        string? entryPath,
        int depth,
        IReadOnlyCollection<string> aliases)
    {
        _loadingAliases.Push(new LoadingDocumentAliases(
            GetDocumentKey(identity, container, entryPath),
            aliases));
        try
        {
            return ReadClearText(identity, bytes, container, entryPath, depth);
        }
        finally
        {
            _ = _loadingAliases.Pop();
        }
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
            source = StrictUtf8.GetString(bytes.Span);
        }
        catch (DecoderFallbackException)
        {
            throw Capability("P21-RESOURCE-ENCODING", $"Resource '{identity}' is not valid UTF-8 clear text.");
        }

        var key = GetDocumentKey(identity, container, entryPath);
        var baseUri = identity.IsAbsoluteUri ? identity : null;
        var address = new DocumentAddress(key, baseUri, container, entryPath);
        var structure = ExchangeStructureReader.Read(source, _descriptors, this, address, depth);
        return new LoadedDocument(structure, address);
    }

    private static string GetDocumentKey(Uri identity, ResourceContainer? container, string? entryPath) =>
        container is null ? GetIdentityKey(identity) : identity + "!/" + entryPath;

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

        var transaction = BeginCacheTransaction();
        try
        {
            AddDocument(key, null);
            LoadedDocument loaded;
            try
            {
                if (LooksLikeZip(bytes.Span))
                {
                    if (container.ArchiveDepth >= _options.ResourceLimits.MaximumArchiveDepth)
                        ThrowCapability("P21-RESOURCE-ARCHIVE-RECURSION", "Nested archive depth exceeds the configured limit.");
                    var nestedIdentity = new Uri(key, UriKind.RelativeOrAbsolute);
                    loaded = LoadContent(
                        new Part21ResourceContent(nestedIdentity, Part21ResourceContentKind.ZipArchive, bytes),
                        depth,
                        container.ArchiveDepth,
                        [key])!;
                }
                else
                {
                    loaded = ReadClearText(container.Identity, bytes, container, entryPath, depth);
                }
            }
            catch (Exception exception) when (IsExternalStructureFailure(exception))
            {
                RollbackCacheTransaction(transaction);
                SetDocument(key, null);
                return null;
            }

            SetDocument(key, loaded);
            CommitCacheTransaction(transaction);
            return loaded;
        }
        catch
        {
            RollbackCacheTransaction(transaction);
            throw;
        }
    }

    private IReadOnlyDictionary<string, ReadOnlyMemory<byte>> ReadZip(ReadOnlyMemory<byte> bytes, int archiveDepth)
    {
        if (archiveDepth > _options.ResourceLimits.MaximumArchiveDepth)
            ThrowCapability("P21-RESOURCE-ARCHIVE-RECURSION", "Nested archive depth exceeds the configured limit.");
        ValidatePkZip204(bytes.Span);
        var result = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        try
        {
            using var stream = CreateReadStream(bytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.Any(static character => character > 0x7f))
                    ThrowCapability("P21-RESOURCE-ARCHIVE-ENTRY", "PKZip 2.04g entry names must use ASCII characters.");
                CountArchiveEntry(entry.FullName, entry.CompressedLength, entry.Length);
                var name = NormalizeEntryPath(entry.FullName);
                RejectNormalizedRootAlias(entry.FullName, name);
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    continue;
                using var input = entry.Open();
                using var output = new MemoryStream(entry.Length > int.MaxValue ? 0 : (int)entry.Length);
                CopyAndValidateArchiveEntry(entry, input, output);
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
            CountContainerEntry(entry.Value.Length);
            var name = NormalizeEntryPath(entry.Key);
            RejectNormalizedRootAlias(entry.Key, name);
            CountArchiveBytes(entry.Value.Length);
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

    private static bool IsExternalStructureFailure(Exception exception) =>
        exception is ExchangeStructureSyntaxException
            or ExchangeStructureBindingException
            or ExchangeStructureReadValidationException;

    private static bool ContainsCallback<T>(CallbackFrame<T>? frame, T callback)
        where T : class
    {
        for (; frame is not null; frame = frame.Parent)
        {
            if (ReferenceEquals(frame.Callback, callback))
                return true;
        }
        return false;
    }

    private DocumentCacheTransaction BeginCacheTransaction()
    {
        var transaction = new DocumentCacheTransaction();
        _cacheTransactions.Push(transaction);
        return transaction;
    }

    private void AddDocument(string key, LoadedDocument? document)
    {
        RecordDocumentMutation(key);
        _documents.Add(key, document);
    }

    private void SetDocument(string key, LoadedDocument? document)
    {
        RecordDocumentMutation(key);
        _documents[key] = document;
    }

    private void RecordDocumentMutation(string key)
    {
        foreach (var transaction in _cacheTransactions)
        {
            if (transaction.OriginalDocuments.ContainsKey(key))
                continue;
            transaction.OriginalDocuments.Add(
                key,
                _documents.TryGetValue(key, out var document)
                    ? new DocumentCacheEntry(Exists: true, Document: document)
                    : new DocumentCacheEntry(Exists: false, Document: null));
        }
    }

    private void CommitCacheTransaction(DocumentCacheTransaction transaction)
    {
        if (!transaction.IsActive)
            return;
        if (!_cacheTransactions.TryPeek(out var current) || !ReferenceEquals(current, transaction))
            throw new InvalidOperationException("Resource cache transactions must complete in stack order.");
        _ = _cacheTransactions.Pop();
        transaction.IsActive = false;
    }

    private void RollbackCacheTransaction(DocumentCacheTransaction transaction)
    {
        if (!transaction.IsActive)
            return;
        if (!_cacheTransactions.TryPeek(out var current) || !ReferenceEquals(current, transaction))
            throw new InvalidOperationException("Resource cache transactions must complete in stack order.");
        _ = _cacheTransactions.Pop();
        transaction.IsActive = false;
        foreach (var original in transaction.OriginalDocuments)
        {
            if (original.Value.Exists)
                _documents[original.Key] = original.Value.Document;
            else
                _ = _documents.Remove(original.Key);
        }
    }

    private void CountSuppliedBytes(Part21ResourceContent content)
    {
        if (content.Kind == Part21ResourceContentKind.Directory)
        {
            foreach (var entry in content.Entries)
                CountBytes(entry.Value.Length);
            return;
        }
        CountBytes(content.Bytes.Length);
    }

    private void CountBytes(long length)
    {
        if (length < 0 || length > _options.ResourceLimits.MaximumTotalBytes - _totalBytes)
            ThrowLimit("total resource bytes");
        _totalBytes += length;
    }

    private void CountArchiveEntry(string name, long compressedLength, long uncompressedLength)
    {
        CountContainerEntry(uncompressedLength);
        if (compressedLength == 0 ? uncompressedLength > 0 : uncompressedLength / (double)compressedLength
            > _options.ResourceLimits.MaximumCompressionRatio)
        {
            ThrowCapability("P21-RESOURCE-LIMIT-COMPRESSION-RATIO", $"Archive entry '{name}' exceeds the compression-ratio limit.");
        }
    }

    private void CountContainerEntry(long uncompressedLength)
    {
        if (_archiveEntryCount >= _options.ResourceLimits.MaximumArchiveEntryCount)
            ThrowLimit("archive entry count");
        _archiveEntryCount++;
        if (uncompressedLength < 0
            || uncompressedLength > int.MaxValue
            || uncompressedLength > _options.ResourceLimits.MaximumArchiveUncompressedBytes - _archiveBytes)
            ThrowLimit("archive uncompressed bytes");
    }

    private void CountArchiveBytes(long length)
    {
        if (length < 0 || length > _options.ResourceLimits.MaximumArchiveUncompressedBytes - _archiveBytes)
            ThrowLimit("archive uncompressed bytes");
        _archiveBytes += length;
    }

    private void CopyAndValidateArchiveEntry(ZipArchiveEntry entry, Stream input, Stream output)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        var crc = uint.MaxValue;
        long actualLength = 0;
        try
        {
            while (true)
            {
                var read = input.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    break;
                if (read > entry.Length - actualLength)
                    ThrowArchiveFormat($"ZIP entry '{entry.FullName}' expands beyond its declared length.");
                CountArchiveBytes(read);
                actualLength += read;
                crc = UpdateCrc32(crc, buffer.AsSpan(0, read));
                output.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (actualLength != entry.Length)
            ThrowArchiveFormat($"ZIP entry '{entry.FullName}' does not match its declared length.");
        if (~crc != entry.Crc32)
            ThrowArchiveFormat($"ZIP entry '{entry.FullName}' does not match its declared CRC-32.");
    }

    private static uint UpdateCrc32(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
            crc = Crc32Table[(crc ^ value) & 0xff] ^ crc >> 8;
        return crc;
    }

    private static uint[] CreateCrc32Table()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) == 0 ? value >> 1 : 0xedb88320U ^ value >> 1;
            table[index] = value;
        }
        return table;
    }

    private static void RejectNormalizedRootAlias(string originalName, string normalizedName)
    {
        if (normalizedName == ArchiveRootName && originalName != ArchiveRootName)
        {
            ThrowCapability(
                "P21-RESOURCE-ARCHIVE-ROOT",
                $"Archive root '{originalName}' must have the exact name {ArchiveRootName}.");
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

    private static void ValidatePkZip204(ReadOnlySpan<byte> bytes)
    {
        const uint endSignature = 0x06054b50;
        var end = -1;
        var firstPossible = Math.Max(0, bytes.Length - 65557);
        for (var index = bytes.Length - 22; index >= firstPossible; index--)
        {
            if (ReadUInt32(bytes, index) == endSignature)
            {
                end = index;
                break;
            }
        }
        if (end < 0 || end + 22 + ReadUInt16(bytes, end + 20) != bytes.Length)
            ThrowArchiveFormat("The ZIP end-of-central-directory record is missing or invalid.");

        if (ReadUInt16(bytes, end + 4) != 0 || ReadUInt16(bytes, end + 6) != 0)
            ThrowArchiveFormat("Multi-disk ZIP archives are outside PKZip 2.04g transport.");
        var entriesOnDisk = ReadUInt16(bytes, end + 8);
        var entryCount = ReadUInt16(bytes, end + 10);
        var centralSize = ReadUInt32(bytes, end + 12);
        var centralOffset = ReadUInt32(bytes, end + 16);
        if (entriesOnDisk == ushort.MaxValue
            || entryCount == ushort.MaxValue
            || centralSize == uint.MaxValue
            || centralOffset == uint.MaxValue
            || entriesOnDisk != entryCount)
        {
            ThrowArchiveFormat("ZIP64 transport is outside PKZip 2.04g.");
        }
        if ((ulong)centralOffset + centralSize != (ulong)end)
            ThrowArchiveFormat("The ZIP central-directory bounds are invalid.");

        var position = (int)centralOffset;
        var localRanges = new List<(long Start, long End)>();
        for (var index = 0; index < entryCount; index++)
        {
            if (ReadUInt32(bytes, position) != 0x02014b50 || position + 46 > end)
                ThrowArchiveFormat("A ZIP central-directory entry is invalid.");
            var versionNeeded = ReadUInt16(bytes, position + 6);
            var flags = ReadUInt16(bytes, position + 8);
            var method = ReadUInt16(bytes, position + 10);
            var centralCrc = ReadUInt32(bytes, position + 16);
            var compressedSize = ReadUInt32(bytes, position + 20);
            var uncompressedSize = ReadUInt32(bytes, position + 24);
            var nameLength = ReadUInt16(bytes, position + 28);
            var extraLength = ReadUInt16(bytes, position + 30);
            var commentLength = ReadUInt16(bytes, position + 32);
            var diskStart = ReadUInt16(bytes, position + 34);
            var localOffset = ReadUInt32(bytes, position + 42);
            var entryEnd = (long)position + 46 + nameLength + extraLength + commentLength;
            if (entryEnd > end)
                ThrowArchiveFormat("A ZIP central-directory entry exceeds its declared bounds.");
            ValidatePkZipEntry(
                versionNeeded,
                flags,
                method,
                compressedSize,
                uncompressedSize,
                diskStart,
                localOffset);
            ValidateExtraFields(bytes.Slice(position + 46 + nameLength, extraLength));

            if (localOffset > bytes.Length - 30)
                ThrowArchiveFormat("A ZIP local-file offset is outside the archive.");
            var local = (int)localOffset;
            if (ReadUInt32(bytes, local) != 0x04034b50)
                ThrowArchiveFormat("A ZIP local-file header is missing or invalid.");
            var localNameLength = ReadUInt16(bytes, local + 26);
            var localExtraLength = ReadUInt16(bytes, local + 28);
            var dataStart = (long)local + 30 + localNameLength + localExtraLength;
            var dataEnd = dataStart + compressedSize;
            if (ReadUInt16(bytes, local + 4) != versionNeeded
                || ReadUInt16(bytes, local + 6) != flags
                || ReadUInt16(bytes, local + 8) != method
                || localNameLength != nameLength
                || (long)local + 30 + localNameLength + localExtraLength > bytes.Length
                || !bytes.Slice(local + 30, localNameLength).SequenceEqual(bytes.Slice(position + 46, nameLength)))
            {
                ThrowArchiveFormat("ZIP local and central entry metadata do not agree.");
            }
            if (dataEnd > centralOffset)
                ThrowArchiveFormat("A ZIP entry payload overlaps the central directory.");
            var localRangeEnd = dataEnd;
            if ((flags & 0x0008) != 0)
            {
                var descriptor = (int)dataEnd;
                var hasSignatureMarker = dataEnd + 4 <= centralOffset
                    && ReadUInt32(bytes, descriptor) == 0x08074b50;
                var signedValues = descriptor + 4;
                var signedMatches = hasSignatureMarker
                    && DataDescriptorMatches(
                        bytes,
                        signedValues,
                        centralOffset,
                        centralCrc,
                        compressedSize,
                        uncompressedSize);
                var unsignedMatches = DataDescriptorMatches(
                    bytes,
                    descriptor,
                    centralOffset,
                    centralCrc,
                    compressedSize,
                    uncompressedSize);
                if (!signedMatches && !unsignedMatches)
                {
                    ThrowArchiveFormat("A ZIP data descriptor is missing or inconsistent.");
                }
                var descriptorValues = signedMatches ? signedValues : descriptor;
                localRangeEnd = descriptorValues + 12L;
            }
            localRanges.Add((local, localRangeEnd));
            if ((flags & 0x0008) == 0
                && (ReadUInt32(bytes, local + 14) != centralCrc
                    || ReadUInt32(bytes, local + 18) != compressedSize
                    || ReadUInt32(bytes, local + 22) != uncompressedSize))
            {
                ThrowArchiveFormat("ZIP local and central entry integrity metadata do not agree.");
            }
            ValidateExtraFields(bytes.Slice(local + 30 + localNameLength, localExtraLength));
            position = checked((int)entryEnd);
        }
        if (position != end)
            ThrowArchiveFormat("The ZIP central-directory entry count is inconsistent.");
        localRanges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        for (var index = 1; index < localRanges.Count; index++)
        {
            if (localRanges[index].Start < localRanges[index - 1].End)
                ThrowArchiveFormat("ZIP local-file ranges overlap.");
        }
    }

    private static bool DataDescriptorMatches(
        ReadOnlySpan<byte> bytes,
        int values,
        uint centralOffset,
        uint crc,
        uint compressedSize,
        uint uncompressedSize) =>
        (long)values + 12 <= centralOffset
        && ReadUInt32(bytes, values) == crc
        && ReadUInt32(bytes, values + 4) == compressedSize
        && ReadUInt32(bytes, values + 8) == uncompressedSize;

    private static void ValidatePkZipEntry(
        ushort versionNeeded,
        ushort flags,
        ushort method,
        uint compressedSize,
        uint uncompressedSize,
        ushort diskStart,
        uint localOffset)
    {
        if (versionNeeded > 20)
            ThrowArchiveFormat("The ZIP entry requires a feature newer than PKZip 2.04g.");
        if ((flags & 0x0041) != 0)
            ThrowArchiveFormat("Encrypted ZIP entries are outside PKZip 2.04g transport.");
        if ((flags & 0x0800) != 0)
            ThrowArchiveFormat("Unicode ZIP entry-name flags are outside PKZip 2.04g transport.");
        if (method is not (0 or 8))
            ThrowArchiveFormat("Only stored or deflated PKZip 2.04g entries are supported.");
        if (compressedSize == uint.MaxValue
            || uncompressedSize == uint.MaxValue
            || diskStart != 0
            || localOffset == uint.MaxValue)
            ThrowArchiveFormat("Multi-disk or ZIP64 entries are outside PKZip 2.04g transport.");
    }

    private static void ValidateExtraFields(ReadOnlySpan<byte> fields)
    {
        var position = 0;
        while (position < fields.Length)
        {
            if (position + 4 > fields.Length)
                ThrowArchiveFormat("A ZIP extra field is truncated.");
            var identifier = BinaryPrimitives.ReadUInt16LittleEndian(fields[position..]);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(fields[(position + 2)..]);
            position += 4;
            if (position + length > fields.Length)
                ThrowArchiveFormat("A ZIP extra field exceeds its declared bounds.");
            if (identifier == 0x0001)
                ThrowArchiveFormat("ZIP64 extra fields are outside PKZip 2.04g.");
            if (identifier == 0x7075)
                ThrowArchiveFormat("Unicode ZIP entry-name fields are outside PKZip 2.04g transport.");
            position += length;
        }
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 2)
            ThrowArchiveFormat("The ZIP structure is truncated.");
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
            ThrowArchiveFormat("The ZIP structure is truncated.");
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    }

    private static void ThrowArchiveFormat(string message) =>
        ThrowCapability("P21-RESOURCE-ARCHIVE-FORMAT", message);

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

    private sealed record LoadingDocumentAliases(
        string AddressKey,
        IReadOnlyCollection<string> Aliases);

    private sealed class DocumentCacheTransaction
    {
        internal Dictionary<string, DocumentCacheEntry> OriginalDocuments { get; } = new(StringComparer.Ordinal);

        internal bool IsActive { get; set; } = true;
    }

    private readonly record struct DocumentCacheEntry(bool Exists, LoadedDocument? Document);

    private sealed record CallbackFrame<T>(T Callback, CallbackFrame<T>? Parent)
        where T : class;

    private sealed record LoadedDocument(ExchangeStructure Structure, DocumentAddress Address);
}
