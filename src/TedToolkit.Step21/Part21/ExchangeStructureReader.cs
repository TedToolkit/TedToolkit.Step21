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

        var names = new HashSet<SchemaName>(ExchangeStructure.DescriptorNameComparer);
        foreach (var descriptor in snapshot)
        {
            var name = descriptor.Name;
            if (name.IsDefault)
                throw new ArgumentException("Schema descriptors must expose valid names.", nameof(schemaDescriptors));
            if (!names.Add(name))
            {
                throw new ArgumentException(
                    $"Schema descriptor name '{name}' conflicts with another nominal binding identifier.",
                    nameof(schemaDescriptors));
            }
        }

        return Array.AsReadOnly(snapshot);
    }

    internal static ExchangeStructure Read(string source, IReadOnlyCollection<SchemaDescriptor> descriptors)
    {
        var syntax = ExchangeStructureSyntaxParser.Parse(source, SOURCE_NAME);
        syntax.ThrowIfUnsupportedOperationsRequired(retainExternalReferenceEvidence: true);
        ThrowIfReadCapabilityIsExceeded(syntax);

        var bindingDiagnostics = new List<Step21Diagnostic>();
        var referenceFailures = new List<ValidationFailure>();
        var header = BindHeader(syntax.Header, bindingDiagnostics);
        if (header is null)
            throw new ExchangeStructureBindingException(bindingDiagnostics);

        var structure = new ExchangeStructure(header, descriptors);
        foreach (var identifier in header.FileSchema.SchemaIdentifiers)
        {
            var schemaName = new SchemaName(identifier);
            if (!structure.TryGetSchemaDescriptor(schemaName, out _))
            {
                bindingDiagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-SCHEMA",
                    Step21DiagnosticSeverity.Error,
                    $"No supplied schema descriptor matches '{schemaName}'.",
                    syntax.Header.FileSchema.Span.Start));
            }
        }

        var sectionBindings = BindDataSections(
            syntax.DataSections,
            header.FileSchema.SchemaIdentifiers,
            structure,
            bindingDiagnostics);
        structure.SetSchemaPopulations(BindSchemaPopulations(
            syntax.Header.AdditionalEntities,
            header.FileSchema.SchemaIdentifiers,
            structure.DataSections.ToArray(),
            bindingDiagnostics));
        var occurrenceNames = new HashSet<EntityInstanceName>();
        var allocations = sectionBindings.SelectMany(binding => AllocateEntities(
                binding.Syntax,
                binding.DataSection,
                binding.Index,
                binding.Descriptor,
                occurrenceNames,
                bindingDiagnostics))
            .ToArray();
        var entitiesByName = allocations.ToDictionary(allocation => allocation.Name, allocation => allocation.Entity);
        var externalNames = BindExternalReferenceNames(syntax.Reference, structure, bindingDiagnostics);
        foreach (var externalName in externalNames.Entities.Keys.Where(entitiesByName.ContainsKey))
        {
            bindingDiagnostics.Add(new Step21Diagnostic(
                "P21-BIND-OCCURRENCE",
                Step21DiagnosticSeverity.Error,
                $"Entity occurrence '{externalName}' is defined both externally and in the data section.",
                externalNames.Entities[externalName]));
        }

        foreach (var externalName in externalNames.Values.Keys.Where(name =>
                     entitiesByName.ContainsKey(new EntityInstanceName(name.CanonicalDigits))))
        {
            bindingDiagnostics.Add(new Step21Diagnostic(
                "P21-BIND-OCCURRENCE",
                Step21DiagnosticSeverity.Error,
                $"Value occurrence '{externalName}' overlaps an entity occurrence with the same integer.",
                externalNames.Values[externalName]));
        }

        foreach (var allocation in allocations)
            structure.Add(allocation.DataSection, allocation.Name, allocation.Entity);

        _ = structure.TryGetSchemaDescriptor(
            new SchemaName(header.FileSchema.SchemaIdentifiers[0]),
            out var firstSchemaDescriptor);
        BindAnchors(
            syntax.Anchor,
            structure,
            entitiesByName,
            externalNames,
            firstSchemaDescriptor,
            bindingDiagnostics);

        foreach (var allocation in allocations)
        {
            var components = new List<KeyValuePair<string, IReadOnlyList<ParameterValue>>>(
                allocation.Syntax.Records.Count);
            var unresolvedParameters = new HashSet<(string EntityName, int ParameterIndex)>();
            for (var componentIndex = 0; componentIndex < allocation.Syntax.Records.Count; componentIndex++)
            {
                var record = allocation.Syntax.Records[componentIndex];
                var parameters = new List<ParameterValue>(record.Parameters.Count);
                for (var parameterIndex = 0; parameterIndex < record.Parameters.Count; parameterIndex++)
                {
                    var parameter = record.Parameters[parameterIndex];
                    var mappingPath = allocation.Syntax.Records.Count == 1
                        ? string.Empty
                        : $"Components[{componentIndex.ToString(CultureInfo.InvariantCulture)}].";
                    var parameterPath = $"DataSections[{allocation.SectionIndex.ToString(CultureInfo.InvariantCulture)}]."
                        + $"{allocation.Name}.{mappingPath}Parameters[{parameterIndex.ToString(CultureInfo.InvariantCulture)}]";
                    var convertedSuccessfully = TryConvertParameter(
                        parameter,
                        parameterPath,
                        entitiesByName,
                        externalNames.Entities,
                        referenceFailures,
                        bindingDiagnostics,
                        out var converted);
                    parameters.Add(converted);
                    if (!convertedSuccessfully)
                        unresolvedParameters.Add((record.Name, parameterIndex));
                }

                components.Add(new(record.Name, parameters.AsReadOnly()));
            }

            foreach (var diagnostic in allocation.Descriptor.HydrateEntity(
                         structure,
                         allocation.Entity,
                         components.AsReadOnly()))
            {
                var diagnosticRecord = FindDiagnosticRecord(allocation.Syntax, diagnostic.Message);
                if (diagnostic.Code == "P21-BIND-PARAMETER"
                    && TryGetParameterIndex(diagnostic.Message, out var unresolvedIndex)
                    && diagnosticRecord is not null
                    && unresolvedParameters.Contains((diagnosticRecord.Name, unresolvedIndex)))
                {
                    continue;
                }

                if (TryTranslateIncompatibleReference(
                        diagnostic,
                        allocation,
                        diagnosticRecord ?? allocation.Syntax.Records[0],
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
                            (diagnosticRecord ?? allocation.Syntax.Records[0]).Span.Start)
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

    private static void ThrowIfReadCapabilityIsExceeded(ExchangeStructureSyntax syntax)
    {
        var diagnostics = new List<Step21Diagnostic>();
        foreach (var additionalHeader in syntax.Header.AdditionalEntities)
        {
            if (string.Equals(additionalHeader.Name, "FILE_POPULATION", StringComparison.OrdinalIgnoreCase))
            {
                if (additionalHeader.Parameters.Count > 1
                    && additionalHeader.Parameters[1].Kind == Part21ValueKind.String
                    && TryDecodeString(additionalHeader.Parameters[1], out var method)
                    && method is not ("SECTION_BOUNDARY" or "INCLUDE_ALL_COMPATIBLE" or "INCLUDE_REFERENCED"))
                {
                    diagnostics.Add(new Step21Diagnostic(
                        "P21-CAP-SCHEMA-POPULATION",
                        Step21DiagnosticSeverity.Error,
                        $"Schema-population determination method '{method}' is not supported.",
                        additionalHeader.Parameters[1].Span.Start));
                }

                continue;
            }

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

        foreach (var section in syntax.DataSections)
        {
            foreach (var instance in section.EntityInstances)
            {
                foreach (var value in instance.Records.SelectMany(record => record.Parameters))
                    CollectUnsupportedValueDiagnostics(value, diagnostics);
            }
        }

        if (diagnostics.Count > 0)
            throw new ExchangeStructureCapabilityException(diagnostics);
    }

    private static bool TryDecodeString(ValueSyntax value, out string decoded)
    {
        try
        {
            decoded = Part21LexicalValueDecoder.DecodeString(value.Text);
            return true;
        }
        catch (Exception exception) when (exception is FormatException
            or ArgumentOutOfRangeException
            or OverflowException)
        {
            decoded = string.Empty;
            return false;
        }
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
                "Occurrence-reference binding is not implemented by typed reading.",
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
                "FILE_SCHEMA must contain at least one schema identifier."));
        }
        else if (schemaIdentifiers is not null)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var bindingNames = new HashSet<SchemaName>(ExchangeStructure.DescriptorNameComparer);
            foreach (var identifier in schemaIdentifiers)
            {
                if (!names.Add(identifier))
                {
                    diagnostics.Add(HeaderDiagnostic(
                        syntax.FileSchema,
                        $"FILE_SCHEMA repeats schema identifier '{identifier}'."));
                }
                else if (!bindingNames.Add(new SchemaName(identifier)))
                {
                    diagnostics.Add(new Step21Diagnostic(
                        "P21-BIND-SCHEMA",
                        Step21DiagnosticSeverity.Error,
                        $"FILE_SCHEMA identifier '{identifier}' conflicts with another nominal schema identifier.",
                        syntax.FileSchema.Span.Start));
                }
            }
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
            || schemaIdentifiers.Count == 0)
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

    private static IReadOnlyList<SchemaPopulationDefinition> BindSchemaPopulations(
        IReadOnlyList<HeaderEntitySyntax> additionalEntities,
        IReadOnlyList<string> headerSchemaNames,
        IReadOnlyList<DataSection> dataSections,
        ICollection<Step21Diagnostic> diagnostics)
    {
        var definitions = new List<SchemaPopulationDefinition>();
        var sectionsByName = dataSections
            .Where(section => section.Name is not null)
            .ToDictionary(section => section.Name!, StringComparer.Ordinal);
        foreach (var syntax in additionalEntities.Where(entity => string.Equals(
                     entity.Name,
                     "FILE_POPULATION",
                     StringComparison.OrdinalIgnoreCase)))
        {
            var valid = true;
            if (syntax.Parameters.Count != 3)
            {
                diagnostics.Add(PopulationDiagnostic(
                    syntax,
                    $"FILE_POPULATION requires 3 parameters, but received {syntax.Parameters.Count.ToString(CultureInfo.InvariantCulture)}."));
                continue;
            }

            var schemaText = BindPopulationString(syntax.Parameters[0], 0, diagnostics);
            SchemaName schemaName = default;
            if (schemaText is null)
            {
                valid = false;
            }
            else if (!headerSchemaNames.Any(headerSchemaName => ExchangeStructure.SchemaIdentifiersAssociate(
                         new SchemaName(headerSchemaName),
                         new SchemaName(schemaText))))
            {
                diagnostics.Add(PopulationDiagnostic(
                    syntax.Parameters[0],
                    $"FILE_POPULATION governing schema '{schemaText}' does not occur in FILE_SCHEMA."));
                valid = false;
            }
            else
            {
                schemaName = new SchemaName(schemaText);
            }

            var methodText = BindPopulationString(syntax.Parameters[1], 1, diagnostics);
            var method = methodText switch
            {
                "SECTION_BOUNDARY" => SchemaPopulationDetermination.SectionBoundary,
                "INCLUDE_ALL_COMPATIBLE" => SchemaPopulationDetermination.IncludeAllCompatible,
                "INCLUDE_REFERENCED" => SchemaPopulationDetermination.IncludeReferenced,
                _ => default,
            };
            if (methodText is null)
            {
                valid = false;
            }
            else if (methodText is not ("SECTION_BOUNDARY" or "INCLUDE_ALL_COMPATIBLE" or "INCLUDE_REFERENCED"))
            {
                // A well-formed unknown method is rejected by the capability preflight.
                valid = false;
            }

            var inputs = new List<DataSection>();
            var inputNames = new List<string>();
            var sectionParameter = syntax.Parameters[2];
            if (sectionParameter.Kind == Part21ValueKind.Omitted)
            {
                inputs.AddRange(dataSections);
            }
            else if (sectionParameter.Kind != Part21ValueKind.List)
            {
                diagnostics.Add(PopulationDiagnostic(
                    sectionParameter,
                    "FILE_POPULATION parameter 2 must be $ or a list of STRING data-section names."));
                valid = false;
            }
            else
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var value in sectionParameter.Values)
                {
                    if (value.Kind != Part21ValueKind.String || !TryDecodeString(value, out var sectionName))
                    {
                        diagnostics.Add(PopulationDiagnostic(
                            value,
                            "FILE_POPULATION parameter 2 must contain only valid STRING data-section names."));
                        valid = false;
                        continue;
                    }

                    if (!seen.Add(sectionName))
                    {
                        diagnostics.Add(PopulationDiagnostic(
                            value,
                            $"FILE_POPULATION repeats data-section name '{sectionName}'."));
                        valid = false;
                        continue;
                    }

                    inputNames.Add(sectionName);
                    if (!sectionsByName.TryGetValue(sectionName, out var section))
                    {
                        diagnostics.Add(PopulationDiagnostic(
                            value,
                            $"FILE_POPULATION names absent data section '{sectionName}'."));
                        valid = false;
                        continue;
                    }

                    inputs.Add(section);
                }
            }

            if (valid)
            {
                definitions.Add(new SchemaPopulationDefinition(
                    schemaName,
                    method,
                    inputs,
                    sectionParameter.Kind == Part21ValueKind.Omitted ? null : inputNames));
            }
        }

        return definitions.AsReadOnly();
    }

    private static string? BindPopulationString(
        ValueSyntax value,
        int parameterIndex,
        ICollection<Step21Diagnostic> diagnostics)
    {
        if (value.Kind == Part21ValueKind.String && TryDecodeString(value, out var decoded))
            return decoded;

        diagnostics.Add(PopulationDiagnostic(
            value,
            $"FILE_POPULATION parameter {parameterIndex.ToString(CultureInfo.InvariantCulture)} must be a valid STRING."));
        return null;
    }

    private static Step21Diagnostic PopulationDiagnostic(Part21SyntaxNode syntax, string message) =>
        new("P21-BIND-SCHEMA-POPULATION", Step21DiagnosticSeverity.Error, message, syntax.Span.Start);

    private static IReadOnlyList<DataSectionBinding> BindDataSections(
        IReadOnlyList<DataSectionSyntax> sections,
        IReadOnlyList<string> headerSchemaNames,
        ExchangeStructure structure,
        ICollection<Step21Diagnostic> diagnostics)
    {
        var bindings = new List<DataSectionBinding>(sections.Count);
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < sections.Count; index++)
        {
            var syntax = sections[index];
            if (sections.Count == 1 && syntax.Parameters.Count == 0)
            {
                if (headerSchemaNames.Count != 1)
                {
                    diagnostics.Add(DataSectionDiagnostic(
                        syntax,
                        "An unnamed data section requires exactly one FILE_SCHEMA identifier."));
                    continue;
                }

                var unnamedSchemaName = new SchemaName(headerSchemaNames[0]);
                var unnamed = new DataSection(unnamedSchemaName);
                structure.DataSections.Add(unnamed);
                _ = structure.TryGetSchemaDescriptor(unnamedSchemaName, out var unnamedDescriptor);
                bindings.Add(new DataSectionBinding(index, syntax, unnamed, unnamedDescriptor));
                continue;
            }

            if (syntax.Parameters.Count != 2)
            {
                diagnostics.Add(DataSectionDiagnostic(
                    syntax,
                    $"DataSections[{index.ToString(CultureInfo.InvariantCulture)}] requires a section name "
                    + "and one governing schema name."));
                continue;
            }

            var valid = true;
            var name = BindDataSectionName(syntax.Parameters[0], index, diagnostics);
            if (name is null)
            {
                valid = false;
            }
            else if (!names.Add(name))
            {
                diagnostics.Add(DataSectionDiagnostic(
                    syntax.Parameters[0],
                    $"DataSections[{index.ToString(CultureInfo.InvariantCulture)}] repeats section name '{name}'."));
                valid = false;
            }

            var schemaName = BindDataSectionSchemaName(syntax.Parameters[1], index, diagnostics);
            if (schemaName is null)
            {
                valid = false;
            }
            else if (!headerSchemaNames.Any(headerSchemaName => ExchangeStructure.SchemaIdentifiersAssociate(
                         new SchemaName(headerSchemaName),
                         new SchemaName(schemaName))))
            {
                diagnostics.Add(DataSectionDiagnostic(
                    syntax.Parameters[1],
                    $"DataSections[{index.ToString(CultureInfo.InvariantCulture)}] schema '{schemaName}' "
                    + "does not occur in FILE_SCHEMA."));
                valid = false;
            }

            if (!valid)
                continue;

            var dataSection = new DataSection(new SchemaName(schemaName!), name);
            structure.DataSections.Add(dataSection);
            _ = structure.TryGetSchemaDescriptor(dataSection.SchemaName, out var descriptor);
            bindings.Add(new DataSectionBinding(index, syntax, dataSection, descriptor));
        }

        return bindings;
    }

    private static string? BindDataSectionName(
        ValueSyntax value,
        int sectionIndex,
        ICollection<Step21Diagnostic> diagnostics)
    {
        if (value.Kind != Part21ValueKind.String)
        {
            diagnostics.Add(DataSectionDiagnostic(
                value,
                $"DataSections[{sectionIndex.ToString(CultureInfo.InvariantCulture)}] parameter 0 "
                + "must be a STRING section name."));
            return null;
        }

        return DecodeDataSectionString(value, sectionIndex, 0, "section name", diagnostics);
    }

    private static string? BindDataSectionSchemaName(
        ValueSyntax value,
        int sectionIndex,
        ICollection<Step21Diagnostic> diagnostics)
    {
        if (value.Kind != Part21ValueKind.List
            || value.Values.Count != 1
            || value.Values[0].Kind != Part21ValueKind.String)
        {
            diagnostics.Add(DataSectionDiagnostic(
                value,
                $"DataSections[{sectionIndex.ToString(CultureInfo.InvariantCulture)}] parameter 1 must be a list "
                + "containing exactly one STRING schema name."));
            return null;
        }

        return DecodeDataSectionString(value.Values[0], sectionIndex, 1, "schema name", diagnostics);
    }

    private static string? DecodeDataSectionString(
        ValueSyntax value,
        int sectionIndex,
        int parameterIndex,
        string description,
        ICollection<Step21Diagnostic> diagnostics)
    {
        try
        {
            return Part21LexicalValueDecoder.DecodeString(value.Text);
        }
        catch (Exception exception) when (exception is FormatException
            or ArgumentOutOfRangeException
            or OverflowException)
        {
            diagnostics.Add(DataSectionDiagnostic(
                value,
                $"DataSections[{sectionIndex.ToString(CultureInfo.InvariantCulture)}] parameter "
                + $"{parameterIndex.ToString(CultureInfo.InvariantCulture)} contains an invalid {description}."));
            return null;
        }
    }

    private static Step21Diagnostic DataSectionDiagnostic(Part21SyntaxNode syntax, string message) =>
        new("P21-BIND-DATA-SECTION", Step21DiagnosticSeverity.Error, message, syntax.Span.Start);

    private static IReadOnlyList<EntityAllocation> AllocateEntities(
        DataSectionSyntax section,
        DataSection dataSection,
        int sectionIndex,
        SchemaDescriptor? descriptor,
        ISet<EntityInstanceName> names,
        ICollection<Step21Diagnostic> diagnostics)
    {
        var allocations = new List<EntityAllocation>();
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

            if (descriptor is null)
                continue;

            var entityNames = instance.Records.Select(record => record.Name).ToArray();
            var entity = descriptor.AllocateEntity(entityNames);
            if (entity is null)
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-ENTITY",
                    Step21DiagnosticSeverity.Error,
                    $"Schema '{descriptor.Name}' does not support entity mapping "
                        + $"'{string.Join("|", entityNames)}'.",
                    instance.Records[0].Span.Start));
                continue;
            }

            allocations.Add(new EntityAllocation(name, entity, instance, dataSection, sectionIndex, descriptor));
        }

        return allocations;
    }

    private static ExternalOccurrenceNames BindExternalReferenceNames(
        ReferenceSectionSyntax? referenceSection,
        ExchangeStructure structure,
        ICollection<Step21Diagnostic> diagnostics)
    {
        var entityNames = new Dictionary<EntityInstanceName, SourceLocation>();
        var valueNames = new Dictionary<ValueInstanceName, SourceLocation>();
        if (referenceSection is null)
            return new ExternalOccurrenceNames(entityNames, valueNames);

        foreach (var reference in referenceSection.References)
        {
            try
            {
                if (reference.Name.Kind == Part21ValueKind.EntityInstanceName)
                {
                    var name = new EntityInstanceName(reference.Name.Text[1..]);
                    if (!entityNames.TryAdd(name, reference.Name.Span.Start))
                    {
                        diagnostics.Add(new Step21Diagnostic(
                            "P21-BIND-OCCURRENCE",
                            Step21DiagnosticSeverity.Error,
                            $"External entity occurrence '{name}' is declared more than once.",
                            reference.Name.Span.Start));
                    }
                    else
                    {
                        structure.References.Add(new Part21Reference(
                            name,
                            new Part21Resource(reference.Resource.Text[1..^1])));
                    }
                }
                else
                {
                    var name = new ValueInstanceName(reference.Name.Text[1..]);
                    if (!valueNames.TryAdd(name, reference.Name.Span.Start))
                    {
                        diagnostics.Add(new Step21Diagnostic(
                            "P21-BIND-OCCURRENCE",
                            Step21DiagnosticSeverity.Error,
                            $"External value occurrence '{name}' is declared more than once.",
                            reference.Name.Span.Start));
                    }
                    else
                    {
                        structure.References.Add(new Part21Reference(
                            name,
                            new Part21Resource(reference.Resource.Text[1..^1])));
                    }
                }
            }
            catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-OCCURRENCE",
                    Step21DiagnosticSeverity.Error,
                    $"External occurrence '{reference.Name.Text}' is not a positive occurrence name.",
                    reference.Name.Span.Start));
            }
        }

        var valueDigits = valueNames.Keys.Select(name => name.CanonicalDigits).ToHashSet(StringComparer.Ordinal);
        foreach (var name in entityNames.Keys.Where(name => valueDigits.Contains(name.CanonicalDigits)))
        {
            diagnostics.Add(new Step21Diagnostic(
                "P21-BIND-OCCURRENCE",
                Step21DiagnosticSeverity.Error,
                $"Entity occurrence '{name}' overlaps a value occurrence with the same integer.",
                entityNames[name]));
        }

        return new ExternalOccurrenceNames(entityNames, valueNames);
    }

    private static void BindAnchors(
        AnchorSectionSyntax? section,
        ExchangeStructure structure,
        IReadOnlyDictionary<EntityInstanceName, Entity> entitiesByName,
        ExternalOccurrenceNames externalNames,
        SchemaDescriptor? firstSchemaDescriptor,
        ICollection<Step21Diagnostic> diagnostics)
    {
        if (section is null)
            return;

        var names = new HashSet<AnchorName>();
        foreach (var syntax in section.Anchors)
        {
            try
            {
                var name = new AnchorName(syntax.Name.Text[1..^1]);
                if (!names.Add(name))
                {
                    diagnostics.Add(new Step21Diagnostic(
                        "P21-BIND-ANCHOR-DUPLICATE",
                        Step21DiagnosticSeverity.Error,
                        $"Anchor name '{name}' occurs more than once.",
                        syntax.Name.Span.Start));
                    continue;
                }

                var item = BindAnchorItem(
                    syntax.Item,
                    entitiesByName,
                    externalNames,
                    firstSchemaDescriptor,
                    diagnostics);
                var tags = syntax.Tags.Select(tag => new Part21AnchorTag(
                    tag.Name,
                    BindAnchorItem(
                        tag.Item,
                        entitiesByName,
                        externalNames,
                        firstSchemaDescriptor,
                        diagnostics)));
                structure.Anchors.Add(new Part21Anchor(name, item, tags));
            }
            catch (Exception exception) when (exception is FormatException
                or ArgumentException
                or OverflowException)
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-ANCHOR",
                    Step21DiagnosticSeverity.Error,
                    $"The anchor does not denote a valid Part 21 value: {exception.Message}",
                    syntax.Span.Start));
            }
        }
    }

    private static ParameterValue BindAnchorItem(
        ValueSyntax value,
        IReadOnlyDictionary<EntityInstanceName, Entity> entitiesByName,
        ExternalOccurrenceNames externalNames,
        SchemaDescriptor? firstSchemaDescriptor,
        ICollection<Step21Diagnostic> diagnostics)
    {
        if (value.Kind == Part21ValueKind.List)
        {
            return ParameterValue.FromAggregate(value.Values.Select(child =>
                BindAnchorItem(child, entitiesByName, externalNames, firstSchemaDescriptor, diagnostics)));
        }

        if (value.Kind == Part21ValueKind.Resource)
            return ParameterValue.FromResource(new Part21Resource(value.Text[1..^1]));

        if (value.Kind == Part21ValueKind.EntityInstanceName)
        {
            var name = new EntityInstanceName(value.Text[1..]);
            if (entitiesByName.TryGetValue(name, out var entity))
                return ParameterValue.FromEntity(entity);
            if (externalNames.Entities.ContainsKey(name))
                return ParameterValue.FromEntityInstance(name);

            diagnostics.Add(new Step21Diagnostic(
                "P21-BIND-ANCHOR-OCCURRENCE",
                Step21DiagnosticSeverity.Error,
                $"Anchor entity occurrence '{name}' is not defined in this exchange structure.",
                value.Span.Start));
            return ParameterValue.FromEntityInstance(name);
        }

        if (value.Kind == Part21ValueKind.ValueInstanceName)
        {
            var name = new ValueInstanceName(value.Text[1..]);
            if (!externalNames.Values.ContainsKey(name))
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-ANCHOR-OCCURRENCE",
                    Step21DiagnosticSeverity.Error,
                    $"Anchor value occurrence '{name}' is not defined in the reference section.",
                    value.Span.Start));
            }

            return ParameterValue.FromValueInstance(name);
        }

        if (value.Kind == Part21ValueKind.ConstantEntityName)
        {
            var name = new ConstantEntityName(value.Text[1..]);
            if (firstSchemaDescriptor is null || !firstSchemaDescriptor.ContainsConstantEntity(name.Value))
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-ANCHOR-CONSTANT",
                    Step21DiagnosticSeverity.Error,
                    $"Entity constant '{name}' is not defined by the first FILE_SCHEMA schema.",
                    value.Span.Start));
            }

            return ParameterValue.FromConstantEntity(name);
        }
        if (value.Kind == Part21ValueKind.ConstantValueName)
        {
            var name = new ConstantValueName(value.Text[1..]);
            if (firstSchemaDescriptor is null || !firstSchemaDescriptor.ContainsConstantValue(name.Value))
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-BIND-ANCHOR-CONSTANT",
                    Step21DiagnosticSeverity.Error,
                    $"Value constant '{name}' is not defined by the first FILE_SCHEMA schema.",
                    value.Span.Start));
            }

            return ParameterValue.FromConstantValue(name);
        }

        return ConvertNonReferenceParameter(value);
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
            var componentIndex = allocation.Syntax.Records
                .Select((candidate, index) => (candidate, index))
                .Single(item => ReferenceEquals(item.candidate, record))
                .index;
            var mappingPath = allocation.Syntax.Records.Count == 1
                ? string.Empty
                : $"Components[{componentIndex.ToString(CultureInfo.InvariantCulture)}].";
            failure = new ValidationFailure(
                "P21.READ.REFERENCE.TYPE",
                $"DataSections[{allocation.SectionIndex.ToString(CultureInfo.InvariantCulture)}]."
                    + $"{allocation.Name}.{mappingPath}Parameters[{parameterIndex.ToString(CultureInfo.InvariantCulture)}]",
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

    private static EntityRecordSyntax? FindDiagnosticRecord(EntityInstanceSyntax instance, string message)
    {
        return instance.Records.FirstOrDefault(record =>
            message.StartsWith(record.Name + " ", StringComparison.Ordinal)
            || message.Contains("for " + record.Name + ".", StringComparison.Ordinal));
    }

    private static bool ContainsEntityReference(ValueSyntax value) =>
        value.Kind == Part21ValueKind.EntityInstanceName || value.Values.Any(ContainsEntityReference);

    private static RealValue ParseReal(string text)
    {
        var isNegative = text.Length > 0 && text[0] == '-';
        var unsignedText = (text.Length > 0 && text[0] == '+') || isNegative
            ? text[1..]
            : text;
        if (!NumberValue.TryParse(unsignedText, out var number) || !number.TryGetReal(out var value))
            throw new InvalidOperationException($"The parser published invalid REAL text '{text}'.");
        return isNegative ? -value : value;
    }

    private sealed class EntityAllocation(
        EntityInstanceName name,
        Entity entity,
        EntityInstanceSyntax syntax,
        DataSection dataSection,
        int sectionIndex,
        SchemaDescriptor descriptor)
    {
        internal EntityInstanceName Name { get; } = name;

        internal Entity Entity { get; } = entity;

        internal EntityInstanceSyntax Syntax { get; } = syntax;

        internal DataSection DataSection { get; } = dataSection;

        internal int SectionIndex { get; } = sectionIndex;

        internal SchemaDescriptor Descriptor { get; } = descriptor;
    }

    private sealed class DataSectionBinding(
        int index,
        DataSectionSyntax syntax,
        DataSection dataSection,
        SchemaDescriptor? descriptor)
    {
        internal int Index { get; } = index;

        internal DataSectionSyntax Syntax { get; } = syntax;

        internal DataSection DataSection { get; } = dataSection;

        internal SchemaDescriptor? Descriptor { get; } = descriptor;
    }

    private sealed record ExternalOccurrenceNames(
        IReadOnlyDictionary<EntityInstanceName, SourceLocation> Entities,
        IReadOnlyDictionary<ValueInstanceName, SourceLocation> Values);
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