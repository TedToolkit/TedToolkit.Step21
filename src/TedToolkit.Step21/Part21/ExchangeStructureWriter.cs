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
        var builder = CreateOutputBuilder(limits);
        AppendHeader(builder, structure);
        AppendAnchors(builder, structure);
        AppendReferences(builder, structure);
        AppendDataSections(builder, structure, projected);
        builder.Append("END-ISO-10303-21;");
        AppendSignatures(builder, structure, options, limits);
        destination.Write(builder.ToString());
    }

    private static void AppendSignatures(
        Part21TextBuilder builder,
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
            builder.Append('\n');
            var content = Part21SignatureEngine.EncodeCoveredCharacters(builder.Builder);
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
            var verificationContent = Part21SignatureEngine.EncodeCoveredCharacters(builder.Builder);
            var encodedCms = Part21SignatureEngine.ValidateSignerOutput(
                supplied,
                verificationContent,
                limits,
                out var digestAlgorithm);
            if (signerIndex == 0)
                ThrowIfPopulationDigestAlgorithmChanges(structure, digestAlgorithm);
            var base64Length = ((long)encodedCms.Length + 2) / 3 * 4;
            builder.EnsureCanAppend("SIGNATURE ".Length + base64Length + " ENDSEC;".Length);
            builder.Append("SIGNATURE ")
                .Append(Convert.ToBase64String(encodedCms))
                .Append(" ENDSEC;");
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

    private static Part21TextBuilder CreateOutputBuilder(Part21ProcessingLimits limits) =>
        new(limits.MaximumOutputCharacters, static () => ThrowLimit("output-character"));

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
        var output = CreateOutputBuilder(limits);
        AppendEntity(output, structure, registration, projected[registration]);
        destination.Write(output.ToString());
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

    private static void AppendHeader(Part21TextBuilder builder, ExchangeStructure structure)
    {
        var header = structure.Header;
        builder.Append("ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION(");
        AppendStringList(builder, header.FileDescription.Description);
        builder.Append(',');
        AppendString(builder, header.FileDescription.ImplementationLevel);
        builder.Append(");\nFILE_NAME(");
        AppendString(builder, header.FileName.Name);
        builder.Append(',');
        AppendString(builder, header.FileName.TimeStamp);
        builder.Append(',');
        AppendStringList(builder, header.FileName.Author);
        builder.Append(',');
        AppendStringList(builder, header.FileName.Organization);
        builder.Append(',');
        AppendString(builder, header.FileName.PreprocessorVersion);
        builder.Append(',');
        AppendString(builder, header.FileName.OriginatingSystem);
        builder.Append(',');
        AppendString(builder, header.FileName.Authorization);
        builder.Append(");\nFILE_SCHEMA(");
        AppendStringList(builder, header.FileSchema.SchemaIdentifiers);
        builder.Append(");\n");
        if (structure.SchemaPopulationExternalFiles.Count > 0)
        {
            builder.Append("SCHEMA_POPULATION((");
            for (var index = 0; index < structure.SchemaPopulationExternalFiles.Count; index++)
            {
                if (index > 0)
                    builder.Append(',');
                var externalFile = structure.SchemaPopulationExternalFiles[index];
                builder.Append('(');
                AppendString(builder, externalFile.Location.OriginalString);
                builder.Append(',');
                AppendOptionalString(builder, externalFile.TimeStamp);
                builder.Append(',');
                AppendOptionalString(builder, externalFile.MessageDigest);
                builder.Append(')');
            }
            builder.Append("));\n");
        }
        foreach (var population in structure.SchemaPopulations)
        {
            builder.Append("FILE_POPULATION(");
            AppendString(builder, population.SchemaName.Value);
            builder.Append(',');
            AppendString(builder, FormatDetermination(population.Determination));
            builder.Append(',');
            if (population.ExplicitSectionNames is null)
                builder.Append('$');
            else
                AppendStringList(builder, population.ExplicitSectionNames);
            builder.Append(");\n");
        }

        builder.Append("ENDSEC;\n");
    }

    private static void AppendDataSections(
        Part21TextBuilder builder,
        ExchangeStructure structure,
        IReadOnlyDictionary<EntityRegistration, ProjectedEntity> projected)
    {
        foreach (var section in structure.DataSections)
        {
            if (section.Name is null)
            {
                builder.Append("DATA;\n");
            }
            else
            {
                builder.Append("DATA(");
                AppendString(builder, section.Name);
                builder.Append(",(");
                AppendString(builder, section.SchemaName.Value);
                builder.Append("));\n");
            }

            foreach (var registration in structure.Registrations.Where(item => ReferenceEquals(item.DataSection, section)))
            {
                AppendEntity(builder, structure, registration, projected[registration]);
                builder.Append('\n');
            }

            builder.Append("ENDSEC;\n");
        }
    }

    private static void AppendAnchors(Part21TextBuilder builder, ExchangeStructure structure)
    {
        if (structure.AnchorEntries.Count == 0)
            return;

        builder.Append("ANCHOR;\n");
        foreach (var anchor in structure.AnchorEntries)
        {
            builder.Append('<').Append(anchor.Name.Value).Append(">=");
            ParameterValueFormatter.Append(builder, anchor.Item, entity => ResolveName(structure, entity));
            foreach (var tag in anchor.Tags)
            {
                builder.Append('{')
                    .Append(tag.Name)
                    .Append(':');
                ParameterValueFormatter.Append(builder, tag.Item, entity => ResolveName(structure, entity));
                builder.Append('}');
            }

            builder.Append(";\n");
        }

        builder.Append("ENDSEC;\n");
    }

    private static void AppendReferences(Part21TextBuilder builder, ExchangeStructure structure)
    {
        if (structure.ReferenceEntries.Count == 0)
            return;

        builder.Append("REFERENCE;\n");
        foreach (var reference in structure.ReferenceEntries)
        {
            builder.Append(reference.Kind == Part21ReferenceKind.EntityInstance ? '#' : '@')
                .Append(reference.CanonicalDigits)
                .Append("=<")
                .Append(reference.Resource.Value)
                .Append('>')
                .Append(";\n");
        }

        builder.Append("ENDSEC;\n");
    }

    private static void AppendEntity(
        Part21TextBuilder builder,
        ExchangeStructure structure,
        EntityRegistration registration,
        ProjectedEntity projected)
    {
        builder.Append('#').Append(registration.Name.CanonicalDigits).Append('=');
        if (projected.Components.Count > 1)
            builder.Append('(');
        foreach (var component in projected.Components)
            AppendComponent(builder, structure, component);
        if (projected.Components.Count > 1)
            builder.Append(')');
        builder.Append(';');
    }

    private static void AppendComponent(
        Part21TextBuilder builder,
        ExchangeStructure structure,
        KeyValuePair<string, IReadOnlyList<ParameterValue>> component)
    {
        builder.Append(component.Key).Append('(');
        for (var index = 0; index < component.Value.Count; index++)
        {
            if (index > 0)
                builder.Append(',');
            ParameterValueFormatter.Append(builder, component.Value[index], entity => ResolveName(structure, entity));
        }
        builder.Append(')');
    }

    private static EntityInstanceName ResolveName(ExchangeStructure structure, Entity entity)
    {
        return structure.TryGetName(entity, out var name)
            ? name
            : throw new InvalidOperationException("A projected entity reference is not registered in this structure.");
    }

    private static void AppendString(Part21TextBuilder builder, string value) =>
        ParameterValueFormatter.Append(builder, ParameterValue.FromString(value), _ => default);

    private static void AppendOptionalString(Part21TextBuilder builder, string? value)
    {
        if (value is null)
            builder.Append('$');
        else
            AppendString(builder, value);
    }

    private static void AppendStringList(Part21TextBuilder builder, IEnumerable<string> values)
    {
        builder.Append('(');
        var first = true;
        foreach (var value in values)
        {
            if (!first)
                builder.Append(',');
            AppendString(builder, value);
            first = false;
        }
        builder.Append(')');
    }

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
