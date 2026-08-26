using System.Collections.ObjectModel;

namespace TedToolkit.Step21;

internal enum SchemaPopulationDetermination
{
    SectionBoundary,
    IncludeAllCompatible,
    IncludeReferenced,
}

internal sealed class SchemaPopulationDefinition
{
    internal SchemaPopulationDefinition(
        SchemaName schemaName,
        SchemaPopulationDetermination determination,
        IEnumerable<DataSection> inputSections,
        IEnumerable<string>? explicitSectionNames)
    {
        SchemaName = schemaName;
        Determination = determination;
        InputSections = new ReadOnlyCollection<DataSection>(inputSections.ToArray());
        ExplicitSectionNames = explicitSectionNames is null
            ? null
            : new ReadOnlyCollection<string>(explicitSectionNames.ToArray());
    }

    internal SchemaName SchemaName { get; }

    internal SchemaPopulationDetermination Determination { get; }

    internal IReadOnlyList<DataSection> InputSections { get; }

    internal IReadOnlyList<string>? ExplicitSectionNames { get; }
}