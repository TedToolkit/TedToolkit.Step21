using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Encodings.Web;
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
    private readonly Part21ProcessingLimits _limits;
    private Part21Resource _uri;

    /// <summary>Creates a bridge over one existing exchange structure and its caller-owned address.</summary>
    public AnnexFModelBridge(ExchangeStructure structure, Part21Resource uri)
        : this(structure, uri, Part21ProcessingLimits.Default)
    {
    }

    /// <summary>Creates a bridge with explicit shared processing limits.</summary>
    public AnnexFModelBridge(
        ExchangeStructure structure,
        Part21Resource uri,
        Part21ProcessingLimits processingLimits)
    {
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(processingLimits);
        _ = uri.Value;
        EnsureUri(uri.Value, processingLimits);
        _structure = structure;
        _uri = uri;
        _limits = processingLimits;
    }

    /// <summary>Gets the single exchange structure projected by this bridge.</summary>
    public ExchangeStructure Structure => _structure;

    /// <summary>Gets the current caller-owned address of the exchange structure.</summary>
    public Part21Resource Uri => _uri;

    /// <summary>Gets the shared limits applied at this bridge boundary.</summary>
    public Part21ProcessingLimits ProcessingLimits => _limits;

    /// <summary>Exports a deterministic JSON snapshot accepted by <see cref="AnnexFEcmaScriptModule"/>.</summary>
    public string ExportState()
    {
        EnsureUri(_uri.Value, _limits);
        var budget = new BridgeBudget(_limits);
        var output = new Part21TextBuilder(
            _limits.MaximumOutputCharacters,
            static () => throw new ExchangeStructureCapabilityException([
                new Step21Diagnostic(
                    "P21-PROCESSING-LIMIT-OUTPUT",
                    Step21DiagnosticSeverity.Error,
                    "The Annex F bridge output-character limit was exceeded."),
            ]));
        output.Append("{\"formatVersion\":");
        output.AppendFormattable(AnnexFEcmaScriptModule.BridgeFormatVersion);
        output.Append(",\"uri\":");
        AppendJsonString(output, _uri.Value);
        output.Append(",\"name\":");
        AppendJsonString(output, _structure.Header.FileName.Name);
        output.Append(",\"anchors\":[");
        for (var index = 0; index < _structure.Anchors.Count; index++)
        {
            if (index > 0)
                output.Append(',');
            WriteAnchor(output, _structure.Anchors[index], budget);
        }
        output.Append("],\"schemaPopulation\":[");
        for (var index = 0; index < _structure.SchemaPopulation.Count; index++)
        {
            if (index > 0)
                output.Append(',');
            WritePopulation(output, _structure.SchemaPopulation[index], budget);
        }
        output.Append("]}");
        return output.ToString();
    }

    /// <summary>
    /// Validates and atomically applies one complete bridge snapshot produced by the Annex F module.
    /// </summary>
    public void ApplyState(string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Length > _limits.MaximumInputCharacters)
            throw new JsonException("The Annex F bridge input-character limit was exceeded.");
        using var document = JsonDocument.Parse(state, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = _limits.MaximumNestingDepth,
        });

        var budget = new BridgeBudget(_limits);
        var root = RequireObject(document.RootElement, "state");
        var version = RequireInt32(root, "formatVersion");
        if (version != AnnexFEcmaScriptModule.BridgeFormatVersion)
            throw new JsonException($"Unsupported Annex F bridge format version '{version}'.");

        var uriValue = RequireString(root, "uri");
        EnsureUri(uriValue, _limits);
        var uri = new Part21Resource(uriValue);
        var name = RequireString(root, "name");
        var anchors = ReadAnchors(RequireArray(root, "anchors"), budget);
        var populations = ReadPopulations(RequireArray(root, "schemaPopulation"), budget);
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

    private void WriteAnchor(Part21TextBuilder writer, Part21Anchor anchor, BridgeBudget budget)
    {
        budget.CountItem();
        writer.Append("{\"name\":");
        AppendJsonString(writer, anchor.Name.Value);
        writer.Append(",\"value\":");
        WriteValue(writer, anchor.Item, budget, depth: 1);
        writer.Append(",\"tags\":[");
        for (var index = 0; index < anchor.Tags.Count; index++)
        {
            if (index > 0)
                writer.Append(',');
            var tag = anchor.Tags[index];
            budget.CountItem();
            writer.Append("{\"name\":");
            AppendJsonString(writer, tag.Name);
            writer.Append(",\"value\":");
            WriteValue(writer, tag.Item, budget, depth: 1);
            writer.Append('}');
        }
        writer.Append("]}");
    }

    private void WriteValue(Part21TextBuilder writer, ParameterValue value, BridgeBudget budget, int depth)
    {
        budget.CountValue(depth);
        writer.Append("{\"kind\":");
        switch (value.Kind)
        {
            case ParameterValueKind.Omitted:
                writer.Append("\"null\"");
                break;
            case ParameterValueKind.Integer:
                if (!value.TryGetInteger(out var integer))
                    throw new InvalidOperationException("An Annex F integer has no retained value.");
                writer.Append("\"integer\",\"p21\":\"");
                writer.AppendFormattable(integer);
                writer.Append('\"');
                break;
            case ParameterValueKind.Real:
                if (!value.TryGetReal(out var real))
                    throw new InvalidOperationException("An Annex F real has no retained value.");
                writer.Append("\"real\",\"p21\":\"");
                if (real.Significand.IsZero)
                {
                    writer.Append("0.");
                }
                else
                {
                    writer.AppendFormattable(real.Significand);
                    writer.Append(".E");
                    writer.AppendFormattable(real.Exponent);
                }
                writer.Append('\"');
                break;
            case ParameterValueKind.String:
                _ = value.TryGetString(out var text);
                writer.Append("\"string\",\"value\":");
                AppendJsonString(writer, text!);
                break;
            case ParameterValueKind.Enumeration:
                _ = value.TryGetEnumeration(out var symbol);
                writer.Append("\"enumeration\",\"value\":");
                AppendJsonString(writer, symbol!);
                break;
            case ParameterValueKind.Binary:
                writer.Append("\"binary\",\"value\":\"");
                AppendBinary(writer, value);
                writer.Append('\"');
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
                writer.Append("\"list\",\"values\":[");
                for (var index = 0; index < items.Count; index++)
                {
                    if (index > 0)
                        writer.Append(',');
                    WriteValue(writer, items[index], budget, depth + 1);
                }
                writer.Append(']');
                break;
            case ParameterValueKind.Resource:
                if (!value.TryGetResource(out var resource))
                    throw new InvalidOperationException("An Annex F URI has no retained resource.");
                EnsureUri(resource.Value, _limits);
                writer.Append("\"uri\",\"value\":");
                AppendJsonString(writer, resource.Value);
                break;
            default:
                throw new InvalidOperationException(
                    $"Parameter kind '{value.Kind}' is not an ISO 10303-21 Annex F anchor value.");
        }
        writer.Append('}');
    }

    private static void WriteOccurrence(Part21TextBuilder writer, string kind, string name)
    {
        AppendJsonString(writer, kind);
        writer.Append(",\"name\":");
        AppendJsonString(writer, name);
    }

    private void WritePopulation(
        Part21TextBuilder writer,
        SchemaPopulationExternalFile population,
        BridgeBudget budget)
    {
        budget.CountItem();
        EnsureUri(population.Location.OriginalString, _limits);
        writer.Append("{\"uri\":");
        AppendJsonString(writer, population.Location.OriginalString);
        writer.Append(",\"stamp\":");
        if (population.TimeStamp is null)
            writer.Append("null");
        else
            AppendJsonString(writer, population.TimeStamp);
        writer.Append(",\"messageDigest\":");
        if (population.MessageDigest is null)
            writer.Append("null");
        else
            AppendJsonString(writer, population.MessageDigest);
        writer.Append(",\"verification\":")
            .Append(population.DigestStatus == SchemaPopulationDigestStatus.Verified ? "true" : "false")
            .Append('}');
    }

    private static void AppendBinary(Part21TextBuilder writer, ParameterValue value)
    {
        if (!value.TryGetBinary(out var binary))
            throw new InvalidOperationException("An Annex F binary has no retained value.");
        var unusedBits = (4 - binary.Length % 4) % 4;
        writer.Append((char)('0' + unusedBits));
        var encodedLength = binary.Length + unusedBits;
        for (var encodedIndex = 0; encodedIndex < encodedLength; encodedIndex += 4)
        {
            var nibble = 0;
            for (var bit = 0; bit < 4; bit++)
            {
                var sourceIndex = encodedIndex + bit - unusedBits;
                nibble = nibble << 1 | (sourceIndex >= 0 && binary[sourceIndex] ? 1 : 0);
            }
            writer.Append("0123456789ABCDEF"[nibble]);
        }
    }

    private static void AppendJsonString(Part21TextBuilder writer, string value)
    {
        writer.Append('\"');
        using var destination = new BuilderTextWriter(writer);
        JavaScriptEncoder.Default.Encode(destination, value);
        writer.Append('\"');
    }

    private sealed class BuilderTextWriter(Part21TextBuilder builder) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => builder.Append(value);

        public override void Write(char[] buffer, int index, int count) =>
            builder.Append(buffer.AsSpan(index, count));

        public override void Write(string? value)
        {
            if (value is not null)
                builder.Append(value);
        }

        public override void Write(ReadOnlySpan<char> buffer) => builder.Append(buffer);
    }

    private List<Part21Anchor> ReadAnchors(JsonElement array, BridgeBudget budget)
    {
        var anchors = new List<Part21Anchor>();
        var names = new HashSet<AnchorName>();
        foreach (var element in array.EnumerateArray())
        {
            budget.CountItem();
            var item = RequireObject(element, "anchor");
            var name = new AnchorName(RequireString(item, "name"));
            if (!names.Add(name))
                throw new JsonException($"Duplicate Annex F anchor '{name.Value}'.");
            var value = ReadValue(RequireProperty(item, "value"), budget, depth: 1);
            var tags = new List<Part21AnchorTag>();
            var tagNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tagElement in RequireArray(item, "tags").EnumerateArray())
            {
                budget.CountItem();
                var tag = RequireObject(tagElement, "tag");
                var tagName = RequireString(tag, "name");
                if (!tagNames.Add(tagName))
                    throw new JsonException($"Duplicate Annex F tag '{tagName}' on anchor '{name.Value}'.");
                tags.Add(new Part21AnchorTag(
                    tagName,
                    ReadValue(RequireProperty(tag, "value"), budget, depth: 1)));
            }
            anchors.Add(new Part21Anchor(name, value, tags));
        }
        return anchors;
    }

    private List<SchemaPopulationExternalFile> ReadPopulations(JsonElement array, BridgeBudget budget)
    {
        var populations = new List<SchemaPopulationExternalFile>();
        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            budget.CountItem();
            var item = RequireObject(element, "schema population");
            var uriValue = RequireString(item, "uri");
            EnsureUri(uriValue, _limits);
            var uri = new Uri(uriValue, UriKind.RelativeOrAbsolute);
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

    private ParameterValue ReadValue(JsonElement element, BridgeBudget budget, int depth)
    {
        budget.CountValue(depth);
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
                RequireArray(item, "values").EnumerateArray().Select(value =>
                    ReadValue(value, budget, depth + 1))),
            "uri" => ReadResource(item),
            var kind => throw new JsonException($"Unknown Annex F value kind '{kind}'."),
        };
    }

    private ParameterValue ReadResource(JsonElement item)
    {
        var value = RequireString(item, "value");
        EnsureUri(value, _limits);
        return ParameterValue.FromResource(new Part21Resource(value));
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
        var encodedBitIndex = 0;
        foreach (var character in value.AsSpan(1))
        {
            var nibble = character is >= '0' and <= '9' ? character - '0' : character - 'A' + 10;
            for (var bit = 3; bit >= 0; bit--)
            {
                var isSet = (nibble & 1 << bit) != 0;
                if (encodedBitIndex < unusedBits)
                {
                    if (isSet)
                        throw new JsonException("The Annex F binary left-fill bits must be zero.");
                }
                else
                {
                    _ = bits.Append(isSet ? '1' : '0');
                }
                encodedBitIndex++;
            }
        }
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

    private static void EnsureUri(string value, Part21ProcessingLimits limits)
    {
        if (value.Length > limits.MaximumUriCharacters)
            throw new JsonException("The Annex F bridge URI-character limit was exceeded.");
    }

    private sealed class BridgeBudget(Part21ProcessingLimits limits)
    {
        private int _itemCount;

        internal void CountItem()
        {
            if (_itemCount >= limits.MaximumItemCount)
                throw new JsonException("The Annex F bridge item-count limit was exceeded.");
            _itemCount++;
        }

        internal void CountValue(int depth)
        {
            if (depth > limits.MaximumNestingDepth)
                throw new JsonException("The Annex F bridge nesting-depth limit was exceeded.");
            CountItem();
        }
    }

    [GeneratedRegex("^-?(?:0|[1-9][0-9]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex IntegerPattern();

    [GeneratedRegex("^-?[0-9]+\\.[0-9]*(?:E[+-]?[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex RealPattern();

    [GeneratedRegex("^[0-3][0-9A-F]*$", RegexOptions.CultureInvariant)]
    private static partial Regex BinaryPattern();
}
