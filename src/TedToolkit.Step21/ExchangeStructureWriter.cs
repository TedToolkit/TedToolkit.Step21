using System.Text;

namespace TedToolkit.Step21;

// Buffers every domain-controlled character before touching the caller's destination.
internal static class ExchangeStructureWriter
{
    internal static void Write(ExchangeStructure structure, TextWriter destination)
    {
        PreflightValidation(structure);
        var projected = Project(structure, registration: null);
        var builder = new StringBuilder();
        AppendHeader(builder, structure);
        AppendDataSections(builder, structure, projected);
        _ = builder.Append("END-ISO-10303-21;");
        destination.Write(builder.ToString());
    }

    internal static void WriteEntity(ExchangeStructure structure, TextWriter destination, Entity entity)
    {
        PreflightValidation(structure);
        var registration = structure.Registrations.SingleOrDefault(candidate => ReferenceEquals(candidate.Entity, entity));
        if (registration is null)
        {
            throw new ExchangeStructureWriteValidationException(new ValidationResult(
            [
                new ValidationFailure(
                    "P21.WRITE.ENTITY.REGISTRATION",
                    "Entity",
                    "The entity is not registered in this exchange structure."),
            ]));
        }

        var projected = Project(structure, registration);
        destination.Write(FormatEntity(structure, registration, projected[registration]));
    }

    private static void PreflightValidation(ExchangeStructure structure)
    {
        var validation = structure.Validate();
        if (!validation.IsValid)
            throw new ExchangeStructureWriteValidationException(validation);
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
            if (components.Count != 1)
            {
                capabilityDiagnostics.Add(new Step21Diagnostic(
                    "P21-CAP-COMPLEX-ENTITY",
                    Step21DiagnosticSeverity.Error,
                    $"Entity '{item.Name}' does not have exactly one writable simple physical component."));
                continue;
            }

            result.Add(item, new ProjectedEntity(components[0].Key, components[0].Value));
        }

        if (capabilityDiagnostics.Count > 0)
            throw new ExchangeStructureCapabilityException(capabilityDiagnostics);
        return result;
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

    private static string FormatEntity(
        ExchangeStructure structure,
        EntityRegistration registration,
        ProjectedEntity projected)
    {
        var parameters = string.Join(',', projected.Parameters.Select(parameter =>
            ParameterValueFormatter.Format(parameter, entity => ResolveName(structure, entity))));
        return $"{registration.Name}={projected.ComponentName}({parameters});";
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

    private sealed record ProjectedEntity(string ComponentName, IReadOnlyList<ParameterValue> Parameters);
}