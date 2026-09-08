using System.Text;

namespace TedToolkit.Step21;

// Buffers every domain-controlled character before touching the caller's destination.
internal static class ExchangeStructureWriter
{
    private static readonly AsyncLocal<SignerFrame?> ActiveSigners = new();

    internal static void Write(ExchangeStructure structure, TextWriter destination)
    {
        WriteCore(structure, destination, options: null);
    }

    internal static void Write(
        ExchangeStructure structure,
        TextWriter destination,
        ExchangeStructureWriteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        WriteCore(structure, destination, options);
    }

    private static void WriteCore(
        ExchangeStructure structure,
        TextWriter destination,
        ExchangeStructureWriteOptions? options)
    {
        var limits = options?.ProcessingLimits ?? Part21ProcessingLimits.Default;
        PreflightValidation(structure, additionalFailures: null);
        if (structure.Signatures.Count > 0 && (options is null || options.Signers.Count == 0))
        {
            throw new ExchangeStructureCapabilityException([
                new Step21Diagnostic(
                    "P21-CAP-SIGNATURE-SIGNER",
                    Step21DiagnosticSeverity.Error,
                    "Writing a previously signed structure requires an explicit signing capability."),
            ]);
        }
        if (options is not null && options.Signers.Count > limits.MaximumSignatureCount)
            ThrowSignatureLimit("signature count");
        var projected = Project(structure, registration: null);
        ThrowIfProjectedValuesAreInvalid(structure, projected);
        var builder = new StringBuilder();
        AppendHeader(builder, structure);
        EnsureOutputLimit(builder, limits);
        AppendAnchors(builder, structure);
        EnsureOutputLimit(builder, limits);
        AppendReferences(builder, structure);
        EnsureOutputLimit(builder, limits);
        AppendDataSections(builder, structure, projected);
        EnsureOutputLimit(builder, limits);
        _ = builder.Append("END-ISO-10303-21;");
        EnsureOutputLimit(builder, limits);
        AppendSignatures(builder, structure, options, limits);
        EnsureOutputLimit(builder, limits);
        destination.Write(builder.ToString());
    }

    private static void AppendSignatures(
        StringBuilder builder,
        ExchangeStructure structure,
        ExchangeStructureWriteOptions? options,
        Part21ProcessingLimits limits)
    {
        if (options is null)
            return;

        long totalSignatureBytes = 0;
        for (var signerIndex = 0; signerIndex < options.Signers.Count; signerIndex++)
        {
            var signer = options.Signers[signerIndex];
            if (ContainsSigner(ActiveSigners.Value, signer))
            {
                throw new ExchangeStructureCapabilityException([
                    new Step21Diagnostic(
                        "P21-SIGNATURE-SIGNER-REENTRY",
                        Step21DiagnosticSeverity.Error,
                        "A signature signer re-entered the same write operation."),
                ]);
            }
            _ = builder.Append('\n');
            EnsureOutputLimit(builder, limits);
            var content = Part21SignatureEngine.EncodeCoveredCharacters(builder);
            ReadOnlyMemory<byte> supplied;
            var previousSigners = ActiveSigners.Value;
            ActiveSigners.Value = new SignerFrame(signer, previousSigners);
            try
            {
                supplied = signer.Sign(content);
            }
            catch (Exception exception) when (IsRecoverableSignerFailure(exception))
            {
                throw new ExchangeStructureCapabilityException([
                    new Step21Diagnostic(
                        "P21-CAP-SIGNATURE-SIGNER",
                        Step21DiagnosticSeverity.Error,
                        "A signature signer failed to produce CMS."),
                ]);
            }
            finally
            {
                ActiveSigners.Value = previousSigners;
            }

            if (supplied.Length > limits.MaximumTotalSignatureBytes - totalSignatureBytes)
                ThrowSignatureLimit("total CMS byte");
            totalSignatureBytes += supplied.Length;

            // The callback received array-backed memory and is therefore outside our trust boundary.
            // Re-create the canonical content from the private builder before validating its result.
            var verificationContent = Part21SignatureEngine.EncodeCoveredCharacters(builder);
            var encodedCms = Part21SignatureEngine.ValidateSignerOutput(
                supplied,
                verificationContent,
                limits,
                out var digestAlgorithm);
            if (signerIndex == 0)
                ThrowIfPopulationDigestAlgorithmChanges(structure, digestAlgorithm);
            _ = builder.Append("SIGNATURE ")
                .Append(Convert.ToBase64String(encodedCms))
                .Append(" ENDSEC;");
            EnsureOutputLimit(builder, limits);
        }
    }

    private static bool ContainsSigner(SignerFrame? frame, IPart21SignatureSigner signer)
    {
        while (frame is not null)
        {
            if (ReferenceEquals(frame.Signer, signer))
                return true;
            frame = frame.Previous;
        }
        return false;
    }

    private static void EnsureOutputLimit(StringBuilder builder, Part21ProcessingLimits limits)
    {
        if (builder.Length > limits.MaximumOutputCharacters)
            ThrowLimit("output-character");
    }

    private static void ThrowLimit(string name) => throw new ExchangeStructureCapabilityException([
        new Step21Diagnostic(
            "P21-PROCESSING-LIMIT-OUTPUT",
            Step21DiagnosticSeverity.Error,
            $"The configured {name} limit was exceeded."),
    ]);

    private static void ThrowSignatureLimit(string name) => throw new ExchangeStructureCapabilityException([
        new Step21Diagnostic(
            "P21-PROCESSING-LIMIT-SIGNATURE",
            Step21DiagnosticSeverity.Error,
            $"The configured {name} limit was exceeded."),
    ]);

    private sealed record SignerFrame(IPart21SignatureSigner Signer, SignerFrame? Previous);

    private static void ThrowIfPopulationDigestAlgorithmChanges(
        ExchangeStructure structure,
        string outgoingDigestAlgorithm)
    {
        if (!structure.SchemaPopulationExternalFiles.Any(static externalFile => externalFile.MessageDigest is not null)
            || string.Equals(
                structure.Signatures[0].DigestAlgorithm,
                outgoingDigestAlgorithm,
                StringComparison.Ordinal))
        {
            return;
        }

        throw new ExchangeStructureWriteValidationException(new ValidationResult(
            structure.SchemaPopulationExternalFiles
                .Select((externalFile, index) => (externalFile, index))
                .Where(item => item.externalFile.MessageDigest is not null)
                .Select(item => new ValidationFailure(
                    "P21.WRITE.SCHEMA_POPULATION.DIGEST.ALGORITHM",
                    $"SchemaPopulation[{item.index}].MessageDigest",
                    "The first outgoing signature digest algorithm differs from the algorithm that verified "
                    + "the schema-population message digest."))));
    }

    private static bool IsRecoverableSignerFailure(Exception exception) => exception is not (
        ExchangeStructureCapabilityException
        or OperationCanceledException
        or OutOfMemoryException
        or StackOverflowException
        or AccessViolationException);

    internal static void WriteEntity(ExchangeStructure structure, TextWriter destination, Entity entity)
    {
        WriteEntity(structure, destination, entity, Part21ProcessingLimits.Default);
    }

    internal static void WriteEntity(
        ExchangeStructure structure,
        TextWriter destination,
        Entity entity,
        Part21ProcessingLimits limits)
    {
        var registration = structure.Registrations.SingleOrDefault(candidate => ReferenceEquals(candidate.Entity, entity));
        var additionalFailures = registration is null
            ? new ValidationFailure[]
            {
                new(
                    "P21.WRITE.ENTITY.REGISTRATION",
                    "Entity",
                    "The entity is not registered in this exchange structure."),
            }
            : null;
        PreflightValidation(structure, additionalFailures);
        if (registration is null)
        {
            throw new InvalidOperationException("Successful write preflight requires a registered entity.");
        }

        var projected = Project(structure, registration);
        ThrowIfProjectedValuesAreInvalid(structure, projected);
        var output = FormatEntity(structure, registration, projected[registration]);
        if (output.Length > limits.MaximumOutputCharacters)
            ThrowLimit("output-character");
        destination.Write(output);
    }

    private static void PreflightValidation(
        ExchangeStructure structure,
        IEnumerable<ValidationFailure>? additionalFailures)
    {
        var validation = structure.Validate();
        var failures = validation.Failures.Concat(additionalFailures ?? []).ToArray();
        if (failures.Length == 0)
        {
            return;
        }

        throw new ExchangeStructureWriteValidationException(new ValidationResult(failures));
    }

    private static IReadOnlyDictionary<EntityRegistration, ProjectedEntity> Project(
        ExchangeStructure structure,
        EntityRegistration? registration)
    {
        var registrations = registration is null ? structure.Registrations : [registration];
        var schemaNames = registration is null
            ? structure.Header.FileSchema.SchemaIdentifiers.Select(identifier => new SchemaName(identifier))
            : [registration.DataSection.SchemaName];
        var capabilityDiagnostics = new List<Step21Diagnostic>();
        var descriptors = new List<SchemaDescriptor>();
        foreach (var name in schemaNames.Distinct())
        {
            if (structure.TryGetSchemaDescriptor(name, out var descriptor))
            {
                descriptors.Add(descriptor!);
            }
            else
            {
                capabilityDiagnostics.Add(new Step21Diagnostic(
                    "P21-CAP-SCHEMA-DESCRIPTOR",
                    Step21DiagnosticSeverity.Error,
                    $"No supplied schema descriptor matches '{name}' for writing."));
            }
        }

        capabilityDiagnostics.AddRange(descriptors.SelectMany(descriptor =>
            descriptor.GetCapabilityDiagnostics(structure)));
        if (registration is null)
            CollectSectionCapabilityDiagnostics(structure, capabilityDiagnostics);
        if (capabilityDiagnostics.Count > 0)
            throw new ExchangeStructureCapabilityException(capabilityDiagnostics);

        var result = new Dictionary<EntityRegistration, ProjectedEntity>();
        foreach (var item in registrations)
        {
            _ = structure.TryGetSchemaDescriptor(item.DataSection.SchemaName, out var descriptor);
            var components = descriptor!.ProjectEntity(item.Entity);
            if (components.Count == 0)
            {
                capabilityDiagnostics.Add(new Step21Diagnostic(
                    "P21-CAP-COMPLEX-ENTITY",
                    Step21DiagnosticSeverity.Error,
                    $"Entity '{item.Name}' does not have a writable physical component."));
                continue;
            }

            if (components.Count > 1 && components.Zip(components.Skip(1), (left, right) =>
                    StringComparer.Ordinal.Compare(left.Key, right.Key) < 0).Any(ascending => !ascending))
            {
                capabilityDiagnostics.Add(new Step21Diagnostic(
                    "P21-CAP-COMPLEX-ENTITY",
                    Step21DiagnosticSeverity.Error,
                    $"Entity '{item.Name}' does not have a strictly ascending complex component sequence."));
                continue;
            }

            result.Add(item, new ProjectedEntity(components));
        }

        if (capabilityDiagnostics.Count > 0)
            throw new ExchangeStructureCapabilityException(capabilityDiagnostics);
        return result;
    }

    private static void ThrowIfProjectedValuesAreInvalid(
        ExchangeStructure structure,
        IReadOnlyDictionary<EntityRegistration, ProjectedEntity> projected)
    {
        var failures = new List<ValidationFailure>();
        var sectionIndexes = new Dictionary<DataSection, int>(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < structure.DataSections.Count; index++)
        {
            sectionIndexes.Add(structure.DataSections[index], index);
        }

        foreach (var entry in projected)
        {
            var registration = entry.Key;
            var entityPath = $"DataSections[{sectionIndexes[registration.DataSection]}].{registration.Name}";
            var components = entry.Value.Components;
            for (var componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                var component = components[componentIndex];
                var componentPath = components.Count == 1
                    ? entityPath
                    : $"{entityPath}.Components[{componentIndex}]";
                for (var parameterIndex = 0; parameterIndex < component.Value.Count; parameterIndex++)
                {
                    CollectProjectedValueFailures(
                        structure,
                        component.Value[parameterIndex],
                        $"{componentPath}.Parameters[{parameterIndex}]",
                        failures);
                }
            }
        }

        if (failures.Count > 0)
        {
            throw new ExchangeStructureWriteValidationException(new ValidationResult(failures));
        }
    }

    private static void CollectProjectedValueFailures(
        ExchangeStructure structure,
        ParameterValue? value,
        string path,
        ICollection<ValidationFailure> failures)
    {
        if (value is null)
        {
            failures.Add(new ValidationFailure(
                "P21.WRITE.PARAMETER.REQUIRED",
                path,
                "The projected parameter value is null."));
            return;
        }

        if (value.TryGetEntity(out var entity))
        {
            if (!structure.TryGetName(entity, out _))
            {
                failures.Add(new ValidationFailure(
                    "P21.WRITE.REFERENCE.REGISTRATION",
                    path,
                    "The projected entity reference is not registered in this exchange structure."));
            }

            return;
        }

        if (value.TryGetAggregate(out var aggregate))
        {
            for (var index = 0; index < aggregate.Count; index++)
            {
                CollectProjectedValueFailures(
                    structure,
                    aggregate[index],
                    $"{path}[{index}]",
                    failures);
            }

            return;
        }

        if (value.TryGetTyped(out _, out var inner))
        {
            CollectProjectedValueFailures(structure, inner, path + ".Value", failures);
        }
    }

    private static void CollectSectionCapabilityDiagnostics(
        ExchangeStructure structure,
        ICollection<Step21Diagnostic> diagnostics)
    {
        var sections = structure.DataSections;
        for (var index = 0; index < sections.Count; index++)
        {
            var section = sections[index];
            if (section.Name is null && (sections.Count != 1 || structure.Header.FileSchema.SchemaIdentifiers.Count != 1))
            {
                diagnostics.Add(new Step21Diagnostic(
                    "P21-CAP-DATA-SECTION",
                    Step21DiagnosticSeverity.Error,
                    $"DataSections[{index}] requires a name for canonical writing."));
            }
        }
    }

    private static void AppendHeader(StringBuilder builder, ExchangeStructure structure)
    {
        var header = structure.Header;
        _ = builder.Append("ISO-10303-21;\nHEADER;\n")
            .Append("FILE_DESCRIPTION(")
            .Append(FormatStringList(header.FileDescription.Description))
            .Append(',')
            .Append(FormatString(header.FileDescription.ImplementationLevel))
            .Append(");\nFILE_NAME(")
            .Append(FormatString(header.FileName.Name))
            .Append(',')
            .Append(FormatString(header.FileName.TimeStamp))
            .Append(',')
            .Append(FormatStringList(header.FileName.Author))
            .Append(',')
            .Append(FormatStringList(header.FileName.Organization))
            .Append(',')
            .Append(FormatString(header.FileName.PreprocessorVersion))
            .Append(',')
            .Append(FormatString(header.FileName.OriginatingSystem))
            .Append(',')
            .Append(FormatString(header.FileName.Authorization))
            .Append(");\nFILE_SCHEMA(")
            .Append(FormatStringList(header.FileSchema.SchemaIdentifiers))
            .Append(");\n");
        if (structure.SchemaPopulationExternalFiles.Count > 0)
        {
            _ = builder.Append("SCHEMA_POPULATION((")
                .Append(string.Join(",", structure.SchemaPopulationExternalFiles.Select(externalFile =>
                    $"({FormatString(externalFile.Location.OriginalString)},"
                    + $"{FormatOptionalString(externalFile.TimeStamp)},"
                    + $"{FormatOptionalString(externalFile.MessageDigest)})")))
                .Append("));\n");
        }
        foreach (var population in structure.SchemaPopulations)
        {
            _ = builder.Append("FILE_POPULATION(")
                .Append(FormatString(population.SchemaName.Value))
                .Append(',')
                .Append(FormatString(FormatDetermination(population.Determination)))
                .Append(',')
                .Append(population.ExplicitSectionNames is null
                    ? "$"
                    : FormatStringList(population.ExplicitSectionNames))
                .Append(");\n");
        }

        _ = builder.Append("ENDSEC;\n");
    }

    private static void AppendDataSections(
        StringBuilder builder,
        ExchangeStructure structure,
        IReadOnlyDictionary<EntityRegistration, ProjectedEntity> projected)
    {
        foreach (var section in structure.DataSections)
        {
            if (section.Name is null)
            {
                _ = builder.Append("DATA;\n");
            }
            else
            {
                _ = builder.Append("DATA(")
                    .Append(FormatString(section.Name))
                    .Append(",(")
                    .Append(FormatString(section.SchemaName.Value))
                    .Append("));\n");
            }

            foreach (var registration in structure.Registrations.Where(item => ReferenceEquals(item.DataSection, section)))
            {
                _ = builder.Append(FormatEntity(structure, registration, projected[registration]))
                    .Append('\n');
            }

            _ = builder.Append("ENDSEC;\n");
        }
    }

    private static void AppendAnchors(StringBuilder builder, ExchangeStructure structure)
    {
        if (structure.AnchorEntries.Count == 0)
            return;

        _ = builder.Append("ANCHOR;\n");
        foreach (var anchor in structure.AnchorEntries)
        {
            _ = builder.Append(anchor.Name)
                .Append('=')
                .Append(ParameterValueFormatter.Format(anchor.Item, entity => ResolveName(structure, entity)));
            foreach (var tag in anchor.Tags)
            {
                _ = builder.Append('{')
                    .Append(tag.Name)
                    .Append(':')
                    .Append(ParameterValueFormatter.Format(tag.Item, entity => ResolveName(structure, entity)))
                    .Append('}');
            }

            _ = builder.Append(";\n");
        }

        _ = builder.Append("ENDSEC;\n");
    }

    private static void AppendReferences(StringBuilder builder, ExchangeStructure structure)
    {
        if (structure.ReferenceEntries.Count == 0)
            return;

        _ = builder.Append("REFERENCE;\n");
        foreach (var reference in structure.ReferenceEntries)
        {
            _ = builder.Append(reference.FormatName())
                .Append('=')
                .Append(reference.Resource)
                .Append(";\n");
        }

        _ = builder.Append("ENDSEC;\n");
    }

    private static string FormatEntity(
        ExchangeStructure structure,
        EntityRegistration registration,
        ProjectedEntity projected)
    {
        var records = projected.Components.Select(component => FormatComponent(structure, component));
        var mappedValue = projected.Components.Count == 1
            ? records.Single()
            : $"({string.Concat(records)})";
        return $"{registration.Name}={mappedValue};";
    }

    private static string FormatComponent(
        ExchangeStructure structure,
        KeyValuePair<string, IReadOnlyList<ParameterValue>> component)
    {
        var parameters = string.Join(',', component.Value.Select(parameter =>
            ParameterValueFormatter.Format(parameter, entity => ResolveName(structure, entity))));
        return $"{component.Key}({parameters})";
    }

    private static EntityInstanceName ResolveName(ExchangeStructure structure, Entity entity)
    {
        return structure.TryGetName(entity, out var name)
            ? name
            : throw new InvalidOperationException("A projected entity reference is not registered in this structure.");
    }

    private static string FormatString(string value) =>
        ParameterValueFormatter.Format(ParameterValue.FromString(value), _ => default);

    private static string FormatOptionalString(string? value) => value is null ? "$" : FormatString(value);

    private static string FormatStringList(IEnumerable<string> values) =>
        $"({string.Join(',', values.Select(FormatString))})";

    private static string FormatDetermination(SchemaPopulationDetermination determination) => determination switch
    {
        SchemaPopulationDetermination.SectionBoundary => "SECTION_BOUNDARY",
        SchemaPopulationDetermination.IncludeAllCompatible => "INCLUDE_ALL_COMPATIBLE",
        SchemaPopulationDetermination.IncludeReferenced => "INCLUDE_REFERENCED",
        _ => throw new InvalidOperationException($"Unsupported schema-population determination '{determination}'."),
    };

    private sealed record ProjectedEntity(
        IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> Components);
}