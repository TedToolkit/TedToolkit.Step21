using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TedToolkit.Step21.AnnexF;

/// <summary>
/// Projects one authoritative <see cref="ExchangeStructure"/> to the engine-neutral Annex F bridge format and
/// applies complete ECMAScript mutations atomically.
/// </summary>
public sealed partial class AnnexFModelBridge
{
    private readonly ExchangeStructure _structure;
    private Part21Resource _uri;

    /// <summary>Creates a bridge over one existing exchange structure and its caller-owned address.</summary>
    public AnnexFModelBridge(ExchangeStructure structure, Part21Resource uri)
    {
        ArgumentNullException.ThrowIfNull(structure);
        _ = uri.Value;
        _structure = structure;
        _uri = uri;
    }

    /// <summary>Gets the single exchange structure projected by this bridge.</summary>
    public ExchangeStructure Structure => _structure;

    /// <summary>Gets the current caller-owned address of the exchange structure.</summary>
    public Part21Resource Uri => _uri;

    /// <summary>Exports a deterministic JSON snapshot accepted by <see cref="AnnexFEcmaScriptModule"/>.</summary>
    public string ExportState()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", AnnexFEcmaScriptModule.BridgeFormatVersion);
            writer.WriteString("uri", _uri.Value);
            writer.WriteString("name", _structure.Header.FileName.Name);
            writer.WriteStartArray("anchors");
            foreach (var anchor in _structure.Anchors)
                WriteAnchor(writer, anchor);
            writer.WriteEndArray();
            writer.WriteStartArray("schemaPopulation");
            foreach (var population in _structure.SchemaPopulation)
                WritePopulation(writer, population);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Validates and atomically applies one complete bridge snapshot produced by the Annex F module.
    /// </summary>
    public void ApplyState(string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var document = JsonDocument.Parse(state, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 128,
        });

        var root = RequireObject(document.RootElement, "state");
        var version = RequireInt32(root, "formatVersion");
        if (version != AnnexFEcmaScriptModule.BridgeFormatVersion)
            throw new JsonException($"Unsupported Annex F bridge format version '{version}'.");

        var uri = new Part21Resource(RequireString(root, "uri"));
        var name = RequireString(root, "name");
        var anchors = ReadAnchors(RequireArray(root, "anchors"));
        var populations = ReadPopulations(RequireArray(root, "schemaPopulation"));
        EnsureAnchorIdentityIsPreserved(anchors);

        var current = _structure.Header.FileName;
        var fileName = new FileName(
            name,
            current.TimeStamp,
            current.Author,
            current.Organization,
            current.PreprocessorVersion,
            current.OriginatingSystem,
            current.Authorization);

        _structure.Anchors.Clear();
        foreach (var anchor in anchors)
            _structure.Anchors.Add(anchor);
        _structure.SchemaPopulation.Clear();
        foreach (var population in populations)
            _structure.SchemaPopulation.Add(population);
        _structure.ReplaceFileName(fileName);
        _uri = uri;
    }

    private void WriteAnchor(Utf8JsonWriter writer, Part21Anchor anchor)
    {
        writer.WriteStartObject();
        writer.WriteString("name", anchor.Name.Value);
        writer.WritePropertyName("value");
        WriteValue(writer, anchor.Item);
        writer.WriteStartArray("tags");
        foreach (var tag in anchor.Tags)
        {
            writer.WriteStartObject();
            writer.WriteString("name", tag.Name);
            writer.WritePropertyName("value");
            WriteValue(writer, tag.Item);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private void WriteValue(Utf8JsonWriter writer, ParameterValue value)
    {
        writer.WriteStartObject();
        switch (value.Kind)
        {
            case ParameterValueKind.Omitted:
                writer.WriteString("kind", "null");
                break;
            case ParameterValueKind.Integer:
                writer.WriteString("kind", "integer");
                writer.WriteString("p21", Format(value));
                break;
            case ParameterValueKind.Real:
                writer.WriteString("kind", "real");
                writer.WriteString("p21", Format(value));
                break;
            case ParameterValueKind.String:
                _ = value.TryGetString(out var text);
                writer.WriteString("kind", "string");
                writer.WriteString("value", text);
                break;
            case ParameterValueKind.Enumeration:
                _ = value.TryGetEnumeration(out var symbol);
                writer.WriteString("kind", "enumeration");
                writer.WriteString("value", symbol);
                break;
            case ParameterValueKind.Binary:
                var binary = Format(value);
                writer.WriteString("kind", "binary");
                writer.WriteString("value", binary[1..^1]);
                break;
            case ParameterValueKind.Entity:
                if (!value.TryGetEntity(out var entity) || !_structure.TryGetName(entity, out var entityName))
                    throw new InvalidOperationException("An Annex F EID must be registered in the bridged structure.");
                WriteOccurrence(writer, "eid", entityName.CanonicalDigits);
                break;
            case ParameterValueKind.EntityInstance:
                _ = value.TryGetEntityInstance(out var unresolvedEntityName);
                WriteOccurrence(writer, "eid", unresolvedEntityName.CanonicalDigits);
                break;
            case ParameterValueKind.ValueInstance:
                _ = value.TryGetValueInstance(out var valueName);
                WriteOccurrence(writer, "vid", valueName.CanonicalDigits);
                break;
            case ParameterValueKind.ConstantEntity:
                _ = value.TryGetConstantEntity(out var constantEntity);
                WriteOccurrence(writer, "cin", constantEntity.Value);
                break;
            case ParameterValueKind.ConstantValue:
                _ = value.TryGetConstantValue(out var constantValue);
                WriteOccurrence(writer, "cvn", constantValue.Value);
                break;
            case ParameterValueKind.Aggregate:
                if (!value.TryGetAggregate(out var items))
                    throw new InvalidOperationException("An Annex F list has no retained members.");
                writer.WriteString("kind", "list");
                writer.WriteStartArray("values");
                foreach (var item in items)
                    WriteValue(writer, item);
                writer.WriteEndArray();
                break;
            case ParameterValueKind.Resource:
                _ = value.TryGetResource(out var resource);
                writer.WriteString("kind", "uri");
                writer.WriteString("value", resource.Value);
                break;
            default:
                throw new InvalidOperationException(
                    $"Parameter kind '{value.Kind}' is not an ISO 10303-21 Annex F anchor value.");
        }
        writer.WriteEndObject();
    }

    private static void WriteOccurrence(Utf8JsonWriter writer, string kind, string name)
    {
        writer.WriteString("kind", kind);
        writer.WriteString("name", name);
    }

    private static void WritePopulation(Utf8JsonWriter writer, SchemaPopulationExternalFile population)
    {
        writer.WriteStartObject();
        writer.WriteString("uri", population.Location.OriginalString);
        if (population.TimeStamp is null)
            writer.WriteNull("stamp");
        else
            writer.WriteString("stamp", population.TimeStamp);
        if (population.MessageDigest is null)
            writer.WriteNull("messageDigest");
        else
            writer.WriteString("messageDigest", population.MessageDigest);
        writer.WriteBoolean("verification", population.DigestStatus == SchemaPopulationDigestStatus.Verified);
        writer.WriteEndObject();
    }

    private List<Part21Anchor> ReadAnchors(JsonElement array)
    {
        var anchors = new List<Part21Anchor>();
        var names = new HashSet<AnchorName>();
        foreach (var element in array.EnumerateArray())
        {
            var item = RequireObject(element, "anchor");
            var name = new AnchorName(RequireString(item, "name"));
            if (!names.Add(name))
                throw new JsonException($"Duplicate Annex F anchor '{name.Value}'.");
            var value = ReadValue(RequireProperty(item, "value"));
            var tags = new List<Part21AnchorTag>();
            var tagNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tagElement in RequireArray(item, "tags").EnumerateArray())
            {
                var tag = RequireObject(tagElement, "tag");
                var tagName = RequireString(tag, "name");
                if (!tagNames.Add(tagName))
                    throw new JsonException($"Duplicate Annex F tag '{tagName}' on anchor '{name.Value}'.");
                tags.Add(new Part21AnchorTag(tagName, ReadValue(RequireProperty(tag, "value"))));
            }
            anchors.Add(new Part21Anchor(name, value, tags));
        }
        return anchors;
    }

    private List<SchemaPopulationExternalFile> ReadPopulations(JsonElement array)
    {
        var populations = new List<SchemaPopulationExternalFile>();
        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            var item = RequireObject(element, "schema population");
            var uri = new Uri(RequireString(item, "uri"), UriKind.RelativeOrAbsolute);
            var stamp = ReadNullableString(item, "stamp");
            var digest = ReadNullableString(item, "messageDigest");
            var verification = RequireBoolean(item, "verification");
            if (verification && digest is null)
                throw new JsonException("A verified Annex F schema population requires a message digest.");
            var current = index < _structure.SchemaPopulation.Count
                ? _structure.SchemaPopulation[index]
                : null;
            if (current is not null
                && current.Location.Equals(uri)
                && string.Equals(current.TimeStamp, stamp, StringComparison.Ordinal)
                && string.Equals(current.MessageDigest, digest, StringComparison.Ordinal)
                && (current.DigestStatus == SchemaPopulationDigestStatus.Verified) == verification)
            {
                populations.Add(current);
            }
            else
            {
                var population = new SchemaPopulationExternalFile(uri, stamp, digest);
                if (verification)
                    population.DigestStatus = SchemaPopulationDigestStatus.Verified;
                populations.Add(population);
            }
            index++;
        }
        return populations;
    }

    private ParameterValue ReadValue(JsonElement element)
    {
        var item = RequireObject(element, "value");
        return RequireString(item, "kind") switch
        {
            "null" => ParameterValue.Omitted,
            "integer" => ParameterValue.FromInteger(ParseInteger(RequireString(item, "p21"))),
            "real" => ParameterValue.FromReal(ParseReal(RequireString(item, "p21"))),
            "string" => ParameterValue.FromString(RequireString(item, "value")),
            "enumeration" => ParameterValue.FromEnumeration(RequireString(item, "value")),
            "binary" => ParameterValue.FromBinary(DecodeBinary(RequireString(item, "value"))),
            "eid" => ReadEntity(RequireString(item, "name")),
            "vid" => ParameterValue.FromValueInstance(new ValueInstanceName(RequireString(item, "name"))),
            "cin" => ParameterValue.FromConstantEntity(new ConstantEntityName(RequireString(item, "name"))),
            "cvn" => ParameterValue.FromConstantValue(new ConstantValueName(RequireString(item, "name"))),
            "list" => ParameterValue.FromAggregate(
                RequireArray(item, "values").EnumerateArray().Select(ReadValue)),
            "uri" => ParameterValue.FromResource(new Part21Resource(RequireString(item, "value"))),
            var kind => throw new JsonException($"Unknown Annex F value kind '{kind}'."),
        };
    }

    private ParameterValue ReadEntity(string name)
    {
        var entityName = new EntityInstanceName(name);
        return _structure.TryGetEntity(entityName, out var entity)
            ? ParameterValue.FromEntity(entity!)
            : ParameterValue.FromEntityInstance(entityName);
    }

    private static BinaryValue DecodeBinary(string value)
    {
        if (!BinaryPattern().IsMatch(value))
            throw new JsonException($"Invalid Annex F binary spelling '{value}'.");
        var unusedBits = value[0] - '0';
        if (unusedBits > (value.Length - 1) * 4)
            throw new JsonException("The Annex F binary unused-bit count exceeds the encoded bit count.");
        var bits = new StringBuilder(Math.Max(0, (value.Length - 1) * 4 - unusedBits));
        foreach (var character in value.AsSpan(1))
        {
            var nibble = character is >= '0' and <= '9' ? character - '0' : character - 'A' + 10;
            for (var bit = 3; bit >= 0; bit--)
                _ = bits.Append((nibble & 1 << bit) == 0 ? '0' : '1');
        }
        if (unusedBits > 0)
            bits.Length -= unusedBits;
        return new BinaryValue(bits.ToString());
    }

    private void EnsureAnchorIdentityIsPreserved(IReadOnlyList<Part21Anchor> anchors)
    {
        if (anchors.Count != _structure.Anchors.Count)
            throw new JsonException("Annex F mutation cannot add or remove anchors.");
        for (var index = 0; index < anchors.Count; index++)
        {
            if (!anchors[index].Name.Equals(_structure.Anchors[index].Name))
                throw new JsonException("Annex F mutation cannot add, remove, rename, or reorder anchors.");
            var currentTags = _structure.Anchors[index].Tags;
            var candidateTags = anchors[index].Tags;
            if (candidateTags.Count != currentTags.Count)
                throw new JsonException("Annex F mutation cannot add or remove anchor tags.");
            for (var tagIndex = 0; tagIndex < candidateTags.Count; tagIndex++)
            {
                if (!string.Equals(candidateTags[tagIndex].Name, currentTags[tagIndex].Name, StringComparison.Ordinal))
                    throw new JsonException("Annex F mutation cannot add, remove, rename, or reorder anchor tags.");
            }
        }
    }

    private string Format(ParameterValue value) => ParameterValueFormatter.Format(value, entity =>
        _structure.TryGetName(entity, out var name)
            ? name
            : throw new InvalidOperationException("The entity is not registered in the bridged structure."));

    private static BigInteger ParseInteger(string value)
    {
        if (!IntegerPattern().IsMatch(value))
            throw new JsonException($"Invalid Annex F integer spelling '{value}'.");
        return BigInteger.Parse(value, CultureInfo.InvariantCulture);
    }

    private static RealValue ParseReal(string value)
    {
        if (!RealPattern().IsMatch(value))
            throw new JsonException($"Invalid Annex F real spelling '{value}'.");
        var exponentMarker = value.IndexOf('E');
        var significand = exponentMarker < 0 ? value : value[..exponentMarker];
        var exponent = exponentMarker < 0
            ? BigInteger.Zero
            : BigInteger.Parse(value[(exponentMarker + 1)..], CultureInfo.InvariantCulture);
        var point = significand.IndexOf('.');
        exponent -= significand.Length - point - 1;
        significand = significand.Remove(point, 1);
        return new RealValue(BigInteger.Parse(significand, CultureInfo.InvariantCulture), exponent);
    }

    private static JsonElement RequireObject(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new JsonException($"Annex F {name} must be an object.");
        return value;
    }

    private static JsonElement RequireArray(JsonElement value, string propertyName)
    {
        var property = RequireProperty(value, propertyName);
        if (property.ValueKind != JsonValueKind.Array)
            throw new JsonException($"Annex F property '{propertyName}' must be an array.");
        return property;
    }

    private static JsonElement RequireProperty(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property))
            throw new JsonException($"Annex F property '{propertyName}' is required.");
        return property;
    }

    private static string RequireString(JsonElement value, string propertyName)
    {
        var property = RequireProperty(value, propertyName);
        return property.ValueKind == JsonValueKind.String
            ? property.GetString()!
            : throw new JsonException($"Annex F property '{propertyName}' must be a string.");
    }

    private static int RequireInt32(JsonElement value, string propertyName)
    {
        var property = RequireProperty(value, propertyName);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var result)
            ? result
            : throw new JsonException($"Annex F property '{propertyName}' must be an integer.");
    }

    private static bool RequireBoolean(JsonElement value, string propertyName)
    {
        var property = RequireProperty(value, propertyName);
        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new JsonException($"Annex F property '{propertyName}' must be a Boolean."),
        };
    }

    private static string? ReadNullableString(JsonElement value, string propertyName)
    {
        var property = RequireProperty(value, propertyName);
        return property.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => property.GetString(),
            _ => throw new JsonException($"Annex F property '{propertyName}' must be a string or null."),
        };
    }

    [GeneratedRegex("^-?(?:0|[1-9][0-9]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex IntegerPattern();

    [GeneratedRegex("^-?[0-9]+\\.[0-9]*(?:E[+-]?[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex RealPattern();

    [GeneratedRegex("^[0-3][0-9A-F]*$", RegexOptions.CultureInvariant)]
    private static partial Regex BinaryPattern();
}