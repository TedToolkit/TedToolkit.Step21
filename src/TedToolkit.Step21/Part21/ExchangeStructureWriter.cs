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
        Guard.NotNull(options);
        WriteCore(structure, destination, options);
    }

    private static void WriteCore(
        ExchangeStructure structure,
        TextWriter destination,
        ExchangeStructureWriteOptions? options)
    {
        var limits = options?.ProcessingLimits ?? Part21ProcessingLimits.Default;
        var stringEncoding = options?.StringEncoding ?? Part21StringEncoding.Canonical;
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
        var graphFeatures = Part21WriteLimitValidator.Validate(
            structure,
            projected.Count,
            projected.Values.Select(entity => entity.Components),
            limits,
            detectsHighStrings: (structure.Header.FileDescription.ImplementationLevel is "3;1" or "2;1")
                && stringEncoding == Part21StringEncoding.Utf8);
        if (graphFeatures.ContainsInvalidParameterValue)
            ThrowIfProjectedValuesAreInvalid(structure, projected);
        Part21ImplementationLevelValidator.ValidateForWrite(
            structure,
            graphFeatures,
            writesSignature: options is { Signers.Count: > 0 },
            stringEncoding);
        var builder = CreateOutputBuilder(limits);
        AppendHeader(builder, structure, stringEncoding);
        AppendAnchors(builder, structure, stringEncoding);
        AppendReferences(builder, structure);
        AppendDataSections(builder, structure, projected, stringEncoding);
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
        var graphFeatures = Part21WriteLimitValidator.ValidateEntity(
            structure,
            projected.Values.Select(entity => entity.Components),
            limits);
        if (graphFeatures.ContainsInvalidParameterValue)
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
        if (additionalFailures is null)
        {
            if (validation.IsValid)
                return;
            throw new ExchangeStructureWriteValidationException(validation);
        }

        var additions = additionalFailures.ToArray();
        if (validation.IsValid && additions.Length == 0)
            return;

        throw new ExchangeStructureWriteValidationException(new ValidationResult(
            validation.Failures.Concat(additions)));
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
        Part21WriteOccurrenceIndex? occurrenceIndex = null;
        for (var headerIndex = 0; headerIndex < structure.UserDefinedHeaderEntityEntries.Count; headerIndex++)
        {
            var header = structure.UserDefinedHeaderEntityEntries[headerIndex];
            for (var parameterIndex = 0; parameterIndex < header.Parameters.Count; parameterIndex++)
            {
                CollectProjectedValueFailures(
                    structure,
                    header.Parameters[parameterIndex],
                    $"UserDefinedHeaderEntities[{headerIndex}].Parameters[{parameterIndex}]",
                    failures,
                    ref occurrenceIndex);
            }
        }

        var sectionIndexes = new Dictionary<DataSection, int>(Step21ReferenceEqualityComparer.Instance);
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
                        failures,
                        ref occurrenceIndex);
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
        ICollection<ValidationFailure> failures,
        ref Part21WriteOccurrenceIndex? occurrenceIndex)
    {
        if (value is null)
        {
            failures.Add(new ValidationFailure(
                "P21.WRITE.PARAMETER.REQUIRED",
                path,
                "The projected parameter value is null."));
            return;
        }

        if (value.Kind == ParameterValueKind.Resource)
        {
            failures.Add(new ValidationFailure(
                "P21.WRITE.PARAMETER.KIND",
                path,
                "A DATA parameter cannot contain an anchor-only resource value."));
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

        if (value.TryGetEntityInstance(out var entityName))
        {
            if (!(occurrenceIndex ??= new Part21WriteOccurrenceIndex(structure)).DefinesEntity(entityName))
            {
                failures.Add(new ValidationFailure(
                    "P21.WRITE.REFERENCE.OCCURRENCE",
                    path,
                    $"Projected entity occurrence '{entityName}' is not defined in this exchange structure."));
            }

            return;
        }

        if (value.TryGetValueInstance(out var valueName))
        {
            if (!(occurrenceIndex ??= new Part21WriteOccurrenceIndex(structure)).DefinesValue(valueName))
            {
                failures.Add(new ValidationFailure(
                    "P21.WRITE.REFERENCE.OCCURRENCE",
                    path,
                    $"Projected value occurrence '{valueName}' is not defined in the reference section."));
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
                    failures,
                    ref occurrenceIndex);
            }

            return;
        }

        if (value.TryGetTyped(out _, out var inner))
        {
            CollectProjectedValueFailures(
                structure,
                inner,
                path + ".Value",
                failures,
                ref occurrenceIndex);
        }
    }

    private static void AppendHeader(
        Part21TextBuilder builder,
        ExchangeStructure structure,
        Part21StringEncoding stringEncoding)
    {
        var header = structure.Header;
        builder.Append("ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION(");
        AppendStringList(builder, header.FileDescription.Description, stringEncoding);
        builder.Append(',');
        AppendString(builder, header.FileDescription.ImplementationLevel, stringEncoding);
        builder.Append(");\nFILE_NAME(");
        AppendString(builder, header.FileName.Name, stringEncoding);
        builder.Append(',');
        AppendString(builder, header.FileName.TimeStamp, stringEncoding);
        builder.Append(',');
        AppendStringList(builder, header.FileName.Author, stringEncoding);
        builder.Append(',');
        AppendStringList(builder, header.FileName.Organization, stringEncoding);
        builder.Append(',');
        AppendString(builder, header.FileName.PreprocessorVersion, stringEncoding);
        builder.Append(',');
        AppendString(builder, header.FileName.OriginatingSystem, stringEncoding);
        builder.Append(',');
        AppendString(builder, header.FileName.Authorization, stringEncoding);
        builder.Append(");\nFILE_SCHEMA(");
        AppendSchemaIdentifierList(builder, header.FileSchema.SchemaIdentifiers, stringEncoding);
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
                AppendString(builder, externalFile.Location.OriginalString, stringEncoding);
                builder.Append(',');
                AppendOptionalString(builder, externalFile.TimeStamp, stringEncoding);
                builder.Append(',');
                AppendOptionalString(builder, externalFile.MessageDigest, stringEncoding);
                builder.Append(')');
            }
            builder.Append("));\n");
        }
        foreach (var population in structure.SchemaPopulations)
        {
            builder.Append("FILE_POPULATION(");
            AppendSchemaIdentifier(builder, population.SchemaName.Value, stringEncoding);
            builder.Append(',');
            AppendString(builder, FormatDetermination(population.Determination), stringEncoding);
            builder.Append(',');
            if (population.ExplicitSectionNames is null)
                builder.Append('$');
            else
                AppendStringList(builder, population.ExplicitSectionNames, stringEncoding);
            builder.Append(");\n");
        }
        foreach (var declaration in structure.SectionLanguageEntries)
        {
            builder.Append("SECTION_LANGUAGE(");
            AppendOptionalString(builder, declaration.SectionName, stringEncoding);
            builder.Append(',');
            AppendString(builder, declaration.LanguageCode, stringEncoding);
            builder.Append(");\n");
        }
        foreach (var declaration in structure.SectionContextEntries)
        {
            builder.Append("SECTION_CONTEXT(");
            AppendOptionalString(builder, declaration.SectionName, stringEncoding);
            builder.Append(',');
            AppendStringList(builder, declaration.ContextIdentifiers, stringEncoding);
            builder.Append(");\n");
        }
        foreach (var entity in structure.UserDefinedHeaderEntityEntries)
        {
            builder.Append(entity.Keyword).Append('(');
            for (var index = 0; index < entity.Parameters.Count; index++)
            {
                if (index > 0)
                    builder.Append(',');
                ParameterValueFormatter.Append(
                    builder,
                    entity.Parameters[index],
                    referencedEntity => ResolveName(structure, referencedEntity),
                    stringEncoding);
            }
            builder.Append(");\n");
        }

        builder.Append("ENDSEC;\n");
    }

    private static void AppendDataSections(
        Part21TextBuilder builder,
        ExchangeStructure structure,
        IReadOnlyDictionary<EntityRegistration, ProjectedEntity> projected,
        Part21StringEncoding stringEncoding)
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
                AppendString(builder, section.Name, stringEncoding);
                builder.Append(",(");
                AppendSchemaIdentifier(builder, section.SchemaName.Value, stringEncoding);
                builder.Append("));\n");
            }

            foreach (var registration in structure.Registrations.Where(item => ReferenceEquals(item.DataSection, section)))
            {
                AppendEntity(builder, structure, registration, projected[registration], stringEncoding);
                builder.Append('\n');
            }

            builder.Append("ENDSEC;\n");
        }
    }

    private static void AppendAnchors(
        Part21TextBuilder builder,
        ExchangeStructure structure,
        Part21StringEncoding stringEncoding)
    {
        if (structure.AnchorEntries.Count == 0)
            return;

        builder.Append("ANCHOR;\n");
        foreach (var anchor in structure.AnchorEntries)
        {
            builder.Append('<').Append(anchor.Name.Value).Append(">=");
            ParameterValueFormatter.Append(
                builder,
                anchor.Item,
                entity => ResolveName(structure, entity),
                stringEncoding);
            foreach (var tag in anchor.Tags)
            {
                builder.Append('{')
                    .Append(tag.Name)
                    .Append(':');
                ParameterValueFormatter.Append(
                    builder,
                    tag.Item,
                    entity => ResolveName(structure, entity),
                    stringEncoding);
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
        ProjectedEntity projected,
        Part21StringEncoding stringEncoding = Part21StringEncoding.Canonical)
    {
        builder.Append('#').Append(registration.Name.CanonicalDigits).Append('=');
        if (projected.Components.Count > 1)
            builder.Append('(');
        foreach (var component in projected.Components)
            AppendComponent(builder, structure, component, stringEncoding);
        if (projected.Components.Count > 1)
            builder.Append(')');
        builder.Append(';');
    }

    private static void AppendComponent(
        Part21TextBuilder builder,
        ExchangeStructure structure,
        KeyValuePair<string, IReadOnlyList<ParameterValue>> component,
        Part21StringEncoding stringEncoding)
    {
        builder.Append(component.Key).Append('(');
        for (var index = 0; index < component.Value.Count; index++)
        {
            if (index > 0)
                builder.Append(',');
            ParameterValueFormatter.Append(
                builder,
                component.Value[index],
                entity => ResolveName(structure, entity),
                stringEncoding);
        }
        builder.Append(')');
    }

    private static EntityInstanceName ResolveName(ExchangeStructure structure, Entity entity)
    {
        return structure.TryGetName(entity, out var name)
            ? name
            : throw new InvalidOperationException("A projected entity reference is not registered in this structure.");
    }

    private static void AppendString(
        Part21TextBuilder builder,
        string value,
        Part21StringEncoding stringEncoding) =>
        ParameterValueFormatter.AppendString(builder, value, stringEncoding);

    private static void AppendOptionalString(
        Part21TextBuilder builder,
        string? value,
        Part21StringEncoding stringEncoding)
    {
        if (value is null)
            builder.Append('$');
        else
            AppendString(builder, value, stringEncoding);
    }

    private static void AppendStringList(
        Part21TextBuilder builder,
        IEnumerable<string> values,
        Part21StringEncoding stringEncoding)
    {
        builder.Append('(');
        var first = true;
        foreach (var value in values)
        {
            if (!first)
                builder.Append(',');
            AppendString(builder, value, stringEncoding);
            first = false;
        }
        builder.Append(')');
    }

    private static void AppendSchemaIdentifierList(
        Part21TextBuilder builder,
        IReadOnlyList<string> values,
        Part21StringEncoding stringEncoding)
    {
        builder.Append('(');
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0)
                builder.Append(',');
            AppendSchemaIdentifier(builder, values[index], stringEncoding);
        }
        builder.Append(')');
    }

    private static void AppendSchemaIdentifier(
        Part21TextBuilder builder,
        string value,
        Part21StringEncoding stringEncoding) =>
        AppendString(builder, value.ToUpperInvariant(), stringEncoding);

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
