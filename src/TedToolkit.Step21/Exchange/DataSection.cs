namespace TedToolkit.Step21;

/// <summary>
/// Represents an editable ISO 10303-21 data section and its governing schema name.
/// </summary>
/// <remarks>
/// Entity membership is owned by each <see cref="ExchangeStructure"/> registration rather than by this value.
/// </remarks>
public sealed class DataSection
{
    /// <summary>Initializes a data section with its governing schema name.</summary>
    /// <param name="schemaName">The governing schema name; later validation determines whether it is usable.</param>
    /// <exception cref="InvalidOperationException"><paramref name="schemaName"/> is the invalid default value.</exception>
    public DataSection(SchemaName schemaName)
        : this(schemaName, name: null)
    {
    }

    internal DataSection(SchemaName schemaName, string? name)
    {
        _ = schemaName.Value;
        SchemaName = schemaName;
        Name = name;
    }

    /// <summary>Gets the governing schema name.</summary>
    public SchemaName SchemaName { get; }

    internal string? Name { get; }
}
