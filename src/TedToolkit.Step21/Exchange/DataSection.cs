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
    {
        _ = schemaName.Value;
        SchemaName = schemaName;
    }

    /// <summary>Initializes a named data section with its governing schema name.</summary>
    /// <param name="schemaName">The governing schema name; later validation determines whether it is usable.</param>
    /// <param name="name">The exchange-structure-unique data-section name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="schemaName"/> is the invalid default value.</exception>
    public DataSection(SchemaName schemaName, string name)
    {
        Guard.NotNull(name);
        _ = schemaName.Value;
        SchemaName = schemaName;
        Name = name;
    }

    /// <summary>Gets the governing schema name.</summary>
    public SchemaName SchemaName { get; }

    /// <summary>Gets the section name, or <see langword="null"/> for an unnamed single data section.</summary>
    public string? Name { get; }
}
