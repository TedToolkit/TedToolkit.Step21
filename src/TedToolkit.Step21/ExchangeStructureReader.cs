using System.Globalization;
using System.Numerics;
using System.Text;

using TedToolkit.Step21.Syntax;

namespace TedToolkit.Step21;

// Keeps parse, bind, hydrate, and validation state private until one complete structure can be returned.
internal static class ExchangeStructureReader
{
    private const string SOURCE_NAME = "<reader>";

    internal static IReadOnlyCollection<SchemaDescriptor> SnapshotDescriptors(
        IReadOnlyCollection<SchemaDescriptor> schemaDescriptors)
    {
        ArgumentNullException.ThrowIfNull(schemaDescriptors);
        var snapshot = schemaDescriptors.ToArray();
        if (snapshot.Any(descriptor => descriptor is null))
            throw new ArgumentException("Schema descriptor collections cannot contain null values.", nameof(schemaDescriptors));

        var names = new HashSet<SchemaName>();
        foreach (var descriptor in snapshot)
        {
            var name = descriptor.Name;
            if (name.IsDefault)
                throw new ArgumentException("Schema descriptors must expose valid names.", nameof(schemaDescriptors));
            if (!names.Add(name))
            {
                throw new ArgumentException(
                    $"Schema descriptor name '{name}' occurs more than once.",
                    nameof(schemaDescriptors));
            }
        }

        return Array.AsReadOnly(snapshot);
    }

    internal static ExchangeStructure Read(string source, IReadOnlyCollection<SchemaDescriptor> descriptors)
    {
        var syntax = ExchangeStructureSyntaxParser.Parse(source, SOURCE_NAME);
        syntax.ThrowIfUnsupportedOperationsRequired(retainExternalReferenceEvidence: true);
        ThrowIfSimpleReadCapabilityIsExceeded(syntax);

        var bindingDiagnostics = new List<Step21Diagnostic>();
        var referenceFailures = new List<ValidationFailure>();
        var header = BindHeader(syntax.Header, bindingDiagnostics);
        if (header is null)
            throw new ExchangeStructureBindingException(bindingDiagnostics);

        var schemaName = new SchemaName(header.FileSchema.SchemaIdentifiers[0]);
        var structure = new ExchangeStructure(header, descriptors);
        if (!structure.TryGetSchemaDescriptor(schemaName, out var descriptor) || descriptor is null)
        {
            bindingDiagnostics.Add(new Step21Diagnostic(
                "P21-BIND-SCHEMA",
                Step21DiagnosticSeverity.Error,
                $"No supplied schema descriptor matches '{schemaName}'.",
                syntax.Header.FileSchema.Span.Start));
            throw new ExchangeStructureBindingException(bindingDiagnostics);
        }

        var sectionSyntax = syntax.DataSections[0];
        var section = new DataSection(schemaName);
        structure.DataSections.Add(section);
        var allocations = AllocateEntities(sectionSyntax, descriptor, bindingDiagnostics);
        var entitiesByName = allocations.ToDictionary(allocation => allocation.Name, allocation => allocation.Entity);
        var externalNames = BindExternalReferenceNames(syntax.Reference, bindingDiagnostics);
        foreach (var externalName in externalNames.Keys.Where(entitiesByName.ContainsKey))
        {
            bindingDiagnostics.Add(new Step21Diagnostic(
                "P21-BIND-OCCURRENCE",
                Step21DiagnosticSeverity.Error,
                $"Entity occurrence '{externalName}' is defined both externally and in the data section.",
                externalNames[externalName]));
        }

        foreach (var allocation in allocations)
            structure.Add(section, allocation.Name, allocation.Entity);

        foreach (var allocation in allocations)
        {
            var record = allocation.Syntax.Records[0];
            var parameters = new List<ParameterValue>(record.Parameters.Count);
            var unresolvedParameters = new HashSet<int>();
            for (var parameterIndex = 0; parameterIndex < record.Parameters.Count; parameterIndex++)
            {
                var parameter = record.Parameters[parameterIndex];
                var parameterPath = $"DataSections[0].{allocation.Name}.Parameters[{parameterIndex.ToString(CultureInfo.InvariantCulture)}]";
                var convertedSuccessfully = TryConvertParameter(
                        parameter,
                        parameterPath,
                        entitiesByName,
                        externalNames,
                        referenceFailures,
                        bindingDiagnostics,
                        out var converted);
                parameters.Add(converted);
                if (!convertedSuccessfully)
                    unresolvedParameters.Add(parameterIndex);
            }

            var components = new KeyValuePair<string, IReadOnlyList<ParameterValue>>[]
            {
                new(record.Name, parameters.AsReadOnly()),
            };
            foreach (var diagnostic in descriptor.HydrateEntity(structure, allocation.Entity, components))
            {
                if (diagnostic.Code == "P21-BIND-PARAMETER"
                    && TryGetParameterIndex(diagnostic.Message, out var unresolvedIndex)
                    && unresolvedParameters.Contains(unresolvedIndex))
                {
                    continue;
                }

                if (TryTranslateIncompatibleReference(
                        diagnostic,
                        allocation,
                        record,
                        out var failure))
                {
                    referenceFailures.Add(failure);
                }
                else
                {
                    var bindingDiagnostic = diagnostic.Code.StartsWith(
                        "P21-BIND-REFERENCE-TYPE-",
                        StringComparison.Ordinal)
                        ? new Step21Diagnostic(
                            "P21-BIND-PARAMETER",
                            diagnostic.Severity,
                            diagnostic.Message,
                            diagnostic.SourceLocation)
                        : diagnostic;
                    bindingDiagnostics.Add(bindingDiagnostic.SourceLocation is null
                        ? new Step21Diagnostic(
                            bindingDiagnostic.Code,
                            bindingDiagnostic.Severity,
                            bindingDiagnostic.Message,
                            record.Span.Start)
                        : bindingDiagnostic);
                }
            }
        }

        if (bindingDiagnostics.Count > 0)
            throw new ExchangeStructureBindingException(bindingDiagnostics);
        if (referenceFailures.Count > 0)
        {
            throw new ExchangeStructureReadValidationException(new ValidationResult(
                referenceFailures
                    .OrderBy(failure => failure.SourceLocation?.Line)
                    .ThenBy(failure => failure.SourceLocation?.Column)
                    .ThenBy(failure => failure.Path, StringComparer.Ordinal)
                    .ThenBy(failure => failure.Code, StringComparer.Ordinal)));
        }

        var validationResult = structure.Validate();
        if (!validationResult.IsValid)
            throw new ExchangeStructureReadValidationException(validationResult);

        return structure;
    }

    private static void ThrowIfSimpleReadCapabilityIsExceeded(ExchangeStructureSyntax syntax)
    {
        var diagnostics = new List<Step21Diagnostic>();
        foreach (var additionalHeader in syntax.Header.AdditionalEntities)
        {
            diagnostics.Add(new Step21Diagnostic(
                "P21-CAP-HEADER-ENTITY",
                Step21DiagnosticSeverity.Error,
                $"Operational retention of additional header entity '{additionalHeader.Name}' is not implemented.",
                additionalHeader.Span.Start));
        }

        foreach (var headerValue in new[]
                 {
                     syntax.Header.FileDescription,
                     syntax.Header.FileName,
                     syntax.Header.FileSchema,
                 }.SelectMany(entity => entity.Parameters))
        {
            CollectUnsupportedValueDiagnostics(headerValue, diagnostics);
        }

        if (syntax.Reference is not null)
        {
            foreach (var reference in syntax.Reference.References.Where(reference =>
                         reference.Name.Kind != Part21ValueKind.EntityInstanceName))
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-CAP-VALUE-REFERENCE",
                    Step21DiagnosticSeverity.Error,
                    "External value-occurrence resolution is not implemented.",
                    reference.Name.Span.Start));
            }
        }

        if (syntax.DataSections.Count != 1)
        {
            diagnostics.Add(new Step21Diagnostic(
                "P21-CAP-DATA-SECTION",
                Step21DiagnosticSeverity.Error,
                "Simple typed reading requires exactly one data section.",
                syntax.Span.Start));
        }

        foreach (var section in syntax.DataSections)
        {
            if (section.Parameters.Count > 0)
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-CAP-DATA-SECTION",
                    Step21DiagnosticSeverity.Error,
                    "Named or schema-qualified data-section parameters are not implemented by simple typed reading.",
                    section.Span.Start));
            }

            foreach (var instance in section.EntityInstances)
            {
                if (instance.Kind == EntityInstanceSyntaxKind.Complex)
                {
                    diagnostics.Add(new Step21Diagnostic(
                        "P21-CAP-COMPLEX-ENTITY",
                        Step21DiagnosticSeverity.Error,
                        "Complex entity-instance binding is not implemented by simple typed reading.",
                        instance.Span.Start));
                }

                foreach (var value in instance.Records.SelectMany(record => record.Parameters))
                    CollectUnsupportedValueDiagnostics(value, diagnostics);
            }
        }

        var schemaParameters = syntax.Header.FileSchema.Parameters;
        if (schemaParameters.Count == 1
            && schemaParameters[0].Kind == Part21ValueKind.List
            && schemaParameters[0].Values.Count > 1)
        {
            diagnostics.Add(new Step21Diagnostic(
                "P21-CAP-SCHEMA-POPULATION",
                Step21DiagnosticSeverity.Error,
                "Simple typed reading requires exactly one FILE_SCHEMA identifier.",
                syntax.Header.FileSchema.Span.Start));
        }

        if (diagnostics.Count > 0)
            throw new ExchangeStructureCapabilityException(diagnostics);
    }

    private static void CollectUnsupportedValueDiagnostics(
        ValueSyntax value,
        ICollection<Step21Diagnostic> diagnostics)
    {
        if (value.Kind is Part21ValueKind.ValueInstanceName
            or Part21ValueKind.ConstantEntityName
            or Part21ValueKind.ConstantValueName
            or Part21ValueKind.AnchorName
            or Part21ValueKind.Resource
            or Part21ValueKind.Signature)
        {
            diagnostics.Add(new Step21Diagnostic(
                "P21-CAP-OCCURRENCE-REFERENCE",
                Step21DiagnosticSeverity.Error,
                "Occurrence-reference binding is not implemented by simple typed reading.",
                value.Span.Start));
        }

        foreach (var child in value.Values)
            CollectUnsupportedValueDiagnostics(child, diagnostics);
    }

    private static HeaderSection? BindHeader(
        HeaderSectionSyntax syntax,
        ICollection<Step21Diagnostic> diagnostics)
    {
        var description = BindStringList(syntax.FileDescription, 0, diagnostics);
        var implementationLevel = BindString(syntax.FileDescription, 1, diagnostics);
        RequireParameterCount(syntax.FileDescription, 2, diagnostics);

        var name = BindString(syntax.FileName, 0, diagnostics);
        var timeStamp = BindString(syntax.FileName, 1, diagnostics);
        var author = BindStringList(syntax.FileName, 2, diagnostics);
        var organization = BindStringList(syntax.FileName, 3, diagnostics);
        var preprocessorVersion = BindString(syntax.FileName, 4, diagnostics);
        var originatingSystem = BindString(syntax.FileName, 5, diagnostics);
        var authorization = BindString(syntax.FileName, 6, diagnostics);
        RequireParameterCount(syntax.FileName, 7, diagnostics);

        var schemaIdentifiers = BindStringList(syntax.FileSchema, 0, diagnostics);
        RequireParameterCount(syntax.FileSchema, 1, diagnostics);
        if (schemaIdentifiers is { Count: 0, })
        {
            diagnostics.Add(HeaderDiagnostic(
                syntax.FileSchema,
                "FILE_SCHEMA must contain exactly one schema identifier."));
        }

        if (diagnostics.Count > 0
            || description is null
            || implementationLevel is null
            || name is null
            || timeStamp is null
            || author is null
            || organization is null
            || preprocessorVersion is null
            || originatingSystem is null
            || authorization is null
            || schemaIdentifiers is null
            || schemaIdentifiers.Count != 1)
        {
            return null;
        }

        return new HeaderSection(
            new FileDescription(description, implementationLevel),
            new FileName(
                name,
                timeStamp,
                author,
                organization,
                preprocessorVersion,
                originatingSystem,
                authorization),
            new FileSchema(schemaIdentifiers));
    }

    private static void RequireParameterCount(
        HeaderEntitySyntax entity,
        int expected,
        ICollection<Step21Diagnostic> diagnostics)
    {
        if (entity.Parameters.Count != expected)
        {
            diagnostics.Add(HeaderDiagnostic(
                entity,
                $"{entity.Name} requires {expected.ToString(CultureInfo.InvariantCulture)} parameters, but received "
                + $"{entity.Parameters.Count.ToString(CultureInfo.InvariantCulture)}."));
        }
    }

    private static string? BindString(
        HeaderEntitySyntax entity,
        int index,
        ICollection<Step21Diagnostic> diagnostics)
    {
        if (index < entity.Parameters.Count && entity.Parameters[index].Kind == Part21ValueKind.String)
        {
            try
            {
                return Part21LexicalValueDecoder.DecodeString(entity.Parameters[index].Text);
            }
            catch (Exception exception) when (exception is FormatException
                or ArgumentOutOfRangeException
                or OverflowException)
            {
                diagnostics.Add(HeaderDiagnostic(entity, $"{entity.Name} parameter {index} contains an invalid character value."));
                return null;
            }
        }

        diagnostics.Add(HeaderDiagnostic(entity, $"{entity.Name} parameter {index} must be a STRING."));
        return null;
    }

    private static IReadOnlyList<string>? BindStringList(
        HeaderEntitySyntax entity,
        int index,
        ICollection<Step21Diagnostic> diagnostics)
    {
        if (index >= entity.Parameters.Count || entity.Parameters[index].Kind != Part21ValueKind.List)
        {
            diagnostics.Add(HeaderDiagnostic(entity, $"{entity.Name} parameter {index} must be a list of STRING values."));
            return null;
        }

        var list = entity.Parameters[index];
        if (list.Values.Any(value => value.Kind != Part21ValueKind.String))
        {
            diagnostics.Add(new Step21Diagnostic(
                "P21-BIND-HEADER",
                Step21DiagnosticSeverity.Error,
                $"{entity.Name} parameter {index} must contain only STRING values.",
                list.Span.Start));
            return null;
        }

        var values = new List<string>(list.Values.Count);
        foreach (var value in list.Values)
        {
            try
            {
                values.Add(Part21LexicalValueDecoder.DecodeString(value.Text));
            }
            catch (Exception exception) when (exception is FormatException
                or ArgumentOutOfRangeException
                or OverflowException)
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-HEADER",
                    Step21DiagnosticSeverity.Error,
                    $"{entity.Name} parameter {index} contains an invalid character value.",
                    value.Span.Start));
            }
        }

        return values.AsReadOnly();
    }

    private static Step21Diagnostic HeaderDiagnostic(HeaderEntitySyntax entity, string message) =>
        new("P21-BIND-HEADER", Step21DiagnosticSeverity.Error, message, entity.Span.Start);

    private static IReadOnlyList<EntityAllocation> AllocateEntities(
        DataSectionSyntax section,
        SchemaDescriptor descriptor,
        ICollection<Step21Diagnostic> diagnostics)
    {
        var allocations = new List<EntityAllocation>();
        var names = new HashSet<EntityInstanceName>();
        foreach (var instance in section.EntityInstances)
        {
            EntityInstanceName name;
            try
            {
                name = new EntityInstanceName(instance.Name.Text[1..]);
            }
            catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-OCCURRENCE",
                    Step21DiagnosticSeverity.Error,
                    $"Entity instance name '{instance.Name.Text}' is not a positive occurrence name.",
                    instance.Name.Span.Start));
                continue;
            }

            if (!names.Add(name))
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-OCCURRENCE",
                    Step21DiagnosticSeverity.Error,
                    $"Entity instance name '{name}' occurs more than once.",
                    instance.Name.Span.Start));
                continue;
            }

            var record = instance.Records[0];
            var entity = descriptor.AllocateEntity([record.Name]);
            if (entity is null)
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-ENTITY",
                    Step21DiagnosticSeverity.Error,
                    $"Schema '{descriptor.Name}' does not define simple entity '{record.Name}'.",
                    record.Span.Start));
                continue;
            }

            allocations.Add(new EntityAllocation(name, entity, instance));
        }

        return allocations;
    }

    private static IReadOnlyDictionary<EntityInstanceName, SourceLocation> BindExternalReferenceNames(
        ReferenceSectionSyntax? referenceSection,
        ICollection<Step21Diagnostic> diagnostics)
    {
        var names = new Dictionary<EntityInstanceName, SourceLocation>();
        if (referenceSection is null)
            return names;

        foreach (var reference in referenceSection.References)
        {
            if (reference.Name.Kind != Part21ValueKind.EntityInstanceName)
                continue;

            EntityInstanceName name;
            try
            {
                name = new EntityInstanceName(reference.Name.Text[1..]);
            }
            catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-OCCURRENCE",
                    Step21DiagnosticSeverity.Error,
                    $"External entity occurrence '{reference.Name.Text}' is not a positive occurrence name.",
                    reference.Name.Span.Start));
                continue;
            }

            if (!names.TryAdd(name, reference.Name.Span.Start))
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-OCCURRENCE",
                    Step21DiagnosticSeverity.Error,
                    $"External entity occurrence '{name}' is declared more than once.",
                    reference.Name.Span.Start));
            }
        }

        return names;
    }

    private static bool TryConvertParameter(
        ValueSyntax value,
        string path,
        IReadOnlyDictionary<EntityInstanceName, Entity> entitiesByName,
        IReadOnlyDictionary<EntityInstanceName, SourceLocation> externalNames,
        ICollection<ValidationFailure> referenceFailures,
        ICollection<Step21Diagnostic> diagnostics,
        out ParameterValue converted)
    {
        if (value.Kind == Part21ValueKind.EntityInstanceName)
        {
            var parsed = TryParseEntityInstanceName(value, out var name);
            if (parsed && entitiesByName.TryGetValue(name, out var entity))
            {
                converted = ParameterValue.FromEntity(entity);
                return true;
            }

            var isExternal = parsed && externalNames.ContainsKey(name);
            referenceFailures.Add(new ValidationFailure(
                isExternal ? "P21.READ.REFERENCE.EXTERNAL" : "P21.READ.REFERENCE.MISSING",
                path,
                isExternal
                    ? $"Entity reference '{value.Text}' denotes an external resource that is not resolved."
                    : $"Entity reference '{value.Text}' is not defined in this exchange structure.",
                value.Span.Start));
            converted = ParameterValue.Omitted;
            return false;
        }

        if (value.Kind is Part21ValueKind.List or Part21ValueKind.Typed)
        {
            var values = new List<ParameterValue>(value.Values.Count);
            var valid = true;
            for (var index = 0; index < value.Values.Count; index++)
            {
                var childPath = value.Kind == Part21ValueKind.List
                    ? $"{path}[{index.ToString(CultureInfo.InvariantCulture)}]"
                    : $"{path}.Value";
                if (TryConvertParameter(
                        value.Values[index],
                        childPath,
                        entitiesByName,
                        externalNames,
                        referenceFailures,
                        diagnostics,
                        out var child))
                {
                    values.Add(child);
                }
                else
                {
                    values.Add(child);
                    valid = false;
                }
            }

            converted = value.Kind == Part21ValueKind.List
                ? ParameterValue.FromAggregate(values)
                : ParameterValue.FromTyped(value.TypeName!, values[0]);
            return valid;
        }

        try
        {
            converted = ConvertNonReferenceParameter(value);
            return true;
        }
        catch (Exception exception) when (exception is FormatException
            or ArgumentOutOfRangeException
            or OverflowException)
        {
            diagnostics.Add(new Step21Diagnostic(
                "P21-BIND-VALUE",
                Step21DiagnosticSeverity.Error,
                "The physical parameter does not denote a valid runtime value.",
                value.Span.Start));
            converted = ParameterValue.Omitted;
            return false;
        }
    }

    private static ParameterValue ConvertNonReferenceParameter(ValueSyntax value) => value.Kind switch
    {
        Part21ValueKind.Omitted => ParameterValue.Omitted,
        Part21ValueKind.Derived => ParameterValue.Derived,
        Part21ValueKind.Integer => ParameterValue.FromInteger(BigInteger.Parse(value.Text, CultureInfo.InvariantCulture)),
        Part21ValueKind.Real => ParameterValue.FromReal(ParseReal(value.Text)),
        Part21ValueKind.String => ParameterValue.FromString(Part21LexicalValueDecoder.DecodeString(value.Text)),
        Part21ValueKind.Enumeration => ParameterValue.FromEnumeration(value.Text[1..^1]),
        Part21ValueKind.Binary => ParameterValue.FromBinary(Part21LexicalValueDecoder.DecodeBinary(value.Text)),
        _ => throw new InvalidOperationException($"Unsupported simple parameter kind '{value.Kind}'."),
    };

    private static bool TryParseEntityInstanceName(ValueSyntax value, out EntityInstanceName name)
    {
        try
        {
            name = new EntityInstanceName(value.Text[1..]);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            name = default;
            return false;
        }
    }

    private static bool TryTranslateIncompatibleReference(
        Step21Diagnostic diagnostic,
        EntityAllocation allocation,
        EntityRecordSyntax record,
        out ValidationFailure failure)
    {
        if (TryGetReferenceParameterIndex(diagnostic.Code, out var parameterIndex)
            && parameterIndex >= 0
            && parameterIndex < record.Parameters.Count
            && ContainsEntityReference(record.Parameters[parameterIndex]))
        {
            failure = new ValidationFailure(
                "P21.READ.REFERENCE.TYPE",
                $"DataSections[0].{allocation.Name}.Parameters[{parameterIndex.ToString(CultureInfo.InvariantCulture)}]",
                $"The resolved reference target is not assignable to the generated parameter type. {diagnostic.Message}",
                record.Parameters[parameterIndex].Span.Start);
            return true;
        }

        failure = null!;
        return false;
    }

    private static bool TryGetReferenceParameterIndex(string code, out int parameterIndex)
    {
        const string prefix = "P21-BIND-REFERENCE-TYPE-";
        if (!code.StartsWith(prefix, StringComparison.Ordinal))
        {
            parameterIndex = -1;
            return false;
        }

        return int.TryParse(
            code.AsSpan(prefix.Length),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out parameterIndex);
    }

    private static bool TryGetParameterIndex(string message, out int parameterIndex)
    {
        const string marker = " parameter ";
        var start = message.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            parameterIndex = -1;
            return false;
        }

        start += marker.Length;
        var end = message.IndexOf(' ', start);
        parameterIndex = -1;
        return end > start
            && int.TryParse(
                message.AsSpan(start, end - start),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out parameterIndex);
    }

    private static bool ContainsEntityReference(ValueSyntax value) =>
        value.Kind == Part21ValueKind.EntityInstanceName || value.Values.Any(ContainsEntityReference);

    private static RealValue ParseReal(string text)
    {
        if (!NumberValue.TryParse(text, out var number) || !number.TryGetReal(out var value))
            throw new InvalidOperationException($"The parser published invalid REAL text '{text}'.");
        return value;
    }

    private sealed class EntityAllocation(
        EntityInstanceName name,
        Entity entity,
        EntityInstanceSyntax syntax)
    {
        internal EntityInstanceName Name { get; } = name;

        internal Entity Entity { get; } = entity;

        internal EntityInstanceSyntax Syntax { get; } = syntax;
    }
}

internal static class Part21LexicalValueDecoder
{
    internal static string DecodeString(string text)
    {
        var source = text.AsSpan(1, text.Length - 2);
        var result = new StringBuilder(source.Length);
        var alphabet = 'A';
        for (var index = 0; index < source.Length;)
        {
            if (source[index] == '\'' && index + 1 < source.Length && source[index + 1] == '\'')
            {
                _ = result.Append('\'');
                index += 2;
            }
            else if (source[index] == '\\' && index + 1 < source.Length && source[index + 1] == '\\')
            {
                _ = result.Append('\\');
                index += 2;
            }
            else if (Matches(source, index, "\\N\\"))
            {
                _ = result.Append('\n');
                index += 3;
            }
            else if (Matches(source, index, "\\F\\"))
            {
                _ = result.Append('\f');
                index += 3;
            }
            else if (index + 3 < source.Length && source[index] == '\\' && source[index + 1] == 'P'
                     && source[index + 3] == '\\')
            {
                alphabet = source[index + 2];
                index += 4;
            }
            else if (Matches(source, index, "\\S\\"))
            {
                var code = source[index + 3] + 128;
                _ = result.Append(alphabet == 'A' ? (char)code : DecodeAlphabetCharacter(alphabet, code));
                index += 4;
            }
            else if (Matches(source, index, "\\X2\\"))
            {
                index = DecodeExtended(source, index + 4, 4, result);
            }
            else if (Matches(source, index, "\\X4\\"))
            {
                index = DecodeExtended(source, index + 4, 8, result);
            }
            else if (Matches(source, index, "\\X\\"))
            {
                _ = result.Append((char)ParseHex(source.Slice(index + 3, 2)));
                index += 5;
            }
            else
            {
                _ = result.Append(source[index]);
                index++;
            }
        }

        return result.ToString();
    }

    internal static BinaryValue DecodeBinary(string text)
    {
        var normalized = text[1..^1]
            .Replace("\\N\\", string.Empty, StringComparison.Ordinal)
            .Replace("\\F\\", string.Empty, StringComparison.Ordinal);
        var unusedBits = normalized[0] - '0';
        if (unusedBits > (normalized.Length - 1) * 4)
            throw new FormatException("The BINARY unused-bit count exceeds the encoded bit count.");
        var builder = new StringBuilder(Math.Max(0, (normalized.Length - 1) * 4 - unusedBits));
        foreach (var character in normalized.AsSpan(1))
        {
            var value = HexValue(character);
            for (var bit = 3; bit >= 0; bit--)
                _ = builder.Append((value & 1 << bit) == 0 ? '0' : '1');
        }

        if (unusedBits > 0)
            builder.Length -= unusedBits;
        return new BinaryValue(builder.ToString());
    }

    private static int DecodeExtended(
        ReadOnlySpan<char> source,
        int index,
        int width,
        StringBuilder result)
    {
        while (!Matches(source, index, "\\X0\\"))
        {
            var codePoint = ParseHex(source.Slice(index, width));
            if (width == 4)
            {
                var character = (char)codePoint;
                if (char.IsHighSurrogate(character))
                {
                    var nextIndex = index + width;
                    if (Matches(source, nextIndex, "\\X0\\"))
                        throw new FormatException("An X2 high surrogate must be followed by a low surrogate.");
                    var low = (char)ParseHex(source.Slice(nextIndex, width));
                    if (!char.IsLowSurrogate(low))
                        throw new FormatException("An X2 high surrogate must be followed by a low surrogate.");
                    _ = result.Append(character).Append(low);
                    index += width;
                }
                else if (char.IsLowSurrogate(character))
                {
                    throw new FormatException("An X2 low surrogate must follow a high surrogate.");
                }
                else
                {
                    _ = result.Append(character);
                }
            }
            else
                _ = result.Append(char.ConvertFromUtf32(codePoint));
            index += width;
        }

        return index + 4;
    }

    private static char DecodeAlphabetCharacter(char alphabet, int code)
    {
        if (alphabet is < 'A' or > 'I')
            throw new FormatException($"ISO 10303-21 alphabet page '{alphabet}' is not defined.");
        var encoding = CodePagesEncodingProvider.Instance.GetEncoding(28591 + alphabet - 'A')
            ?? throw new FormatException("The required ISO 8859 encoding is unavailable.");
        var decoded = encoding.GetString([(byte)code]);
        if (decoded.Length != 1 || decoded[0] == '\uFFFD')
            throw new FormatException("The selected ISO 8859 page does not define the encoded character.");
        return decoded[0];
    }

    private static bool Matches(ReadOnlySpan<char> source, int index, string value) =>
        index + value.Length <= source.Length && source.Slice(index, value.Length).SequenceEqual(value);

    private static int ParseHex(ReadOnlySpan<char> text)
    {
        var value = 0;
        foreach (var character in text)
            value = checked(value * 16 + HexValue(character));

        return value;
    }

    private static int HexValue(char character) => character switch
    {
        >= '0' and <= '9' => character - '0',
        >= 'A' and <= 'F' => character - 'A' + 10,
        _ => throw new InvalidOperationException("The parser published an invalid hexadecimal value."),
    };
}
