using System.Text;

namespace TedToolkit.Step21;

// Buffers every domain-controlled character before touching the caller's destination.
internal static class ExchangeStructureWriter
{
    internal static void Write(ExchangeStructure structure, TextWriter destination)
    {
        PreflightValidation(structure, additionalFailures: null);
        var projected = Project(structure, registration: null);
        ThrowIfProjectedValuesAreInvalid(structure, projected);
        var builder = new StringBuilder();
        AppendHeader(builder, structure);
        AppendAnchors(builder, structure);
        AppendReferences(builder, structure);
        AppendDataSections(builder, structure, projected);
        _ = builder.Append("END-ISO-10303-21;");
        destination.Write(builder.ToString());
    }

    internal static void WriteEntity(ExchangeStructure structure, TextWriter destination, Entity entity)
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
        destination.Write(FormatEntity(structure, registration, projected[registration]));
    }

    private static void PreflightValidation(
        ExchangeStructure structure,
        IEnumerable<ValidationFailure>? additionalFailures)
    {
        var validation = structure.Validate();
        if (validation.IsValid && additionalFailures is null)
        {
            return;
        }

        var failures = validation.Failures.Concat(additionalFailures ?? []).ToArray();
        if (failures.Length > 0)
        {
            throw new ExchangeStructureWriteValidationException(new ValidationResult(failures));
        }
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