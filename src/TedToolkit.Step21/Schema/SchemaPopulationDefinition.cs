namespace TedToolkit.Step21;

/// <summary>Identifies one ISO 10303-21 Annex E schema-population determination method.</summary>
public enum SchemaPopulationDetermination
{
    /// <summary>Includes only instances in the governed sections.</summary>
    SectionBoundary,

    /// <summary>Also includes instances whose types may be referenced by the governing schema.</summary>
    IncludeAllCompatible,

    /// <summary>Also includes instances directly referenced from the governed sections.</summary>
    IncludeReferenced,
}

/// <summary>Represents one standard <c>FILE_POPULATION</c> header declaration.</summary>
public sealed class SchemaPopulationDefinition
{
    /// <summary>Creates a population declaration, using every data section when names are omitted.</summary>
    public SchemaPopulationDefinition(
        SchemaName schemaName,
        SchemaPopulationDetermination determination,
        IEnumerable<string>? governedSectionNames = null)
    {
        _ = schemaName.Value;
        if (!Enum.IsDefined(determination))
            throw new ArgumentOutOfRangeException(nameof(determination));

        SchemaName = schemaName;
        Determination = determination;
        GovernedSectionNames = governedSectionNames is null
            ? null
            : IsoValueSnapshot.Create(governedSectionNames, nameof(governedSectionNames));
        if (GovernedSectionNames is { Count: 0, })
            throw new ArgumentException("Explicit governed-section names cannot be empty.", nameof(governedSectionNames));
        if (GovernedSectionNames is not null
            && GovernedSectionNames.Distinct(StringComparer.Ordinal).Count() != GovernedSectionNames.Count)
        {
            throw new ArgumentException("Governed-section names must be unique.", nameof(governedSectionNames));
        }
    }

    internal SchemaPopulationDefinition(
        SchemaName schemaName,
        SchemaPopulationDetermination determination,
        IEnumerable<DataSection> inputSections,
        IEnumerable<string>? explicitSectionNames)
        : this(schemaName, determination, explicitSectionNames)
    {
    }

    /// <summary>Gets the governing schema name retained from the header.</summary>
    public SchemaName SchemaName { get; }

    /// <summary>Gets the standard population determination method.</summary>
    public SchemaPopulationDetermination Determination { get; }

    /// <summary>Gets explicit governed data-section names, or <see langword="null"/> for every section.</summary>
    public IReadOnlyList<string>? GovernedSectionNames { get; }

    internal IReadOnlyList<string>? ExplicitSectionNames => GovernedSectionNames;
}