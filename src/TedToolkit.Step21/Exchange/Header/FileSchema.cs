namespace TedToolkit.Step21;

/// <summary>
/// Represents the required ISO 10303-21 <c>FILE_SCHEMA</c> header entity.
/// </summary>
public sealed class FileSchema
{
    /// <summary>Initializes a file-schema value by snapshotting its schema identifiers.</summary>
    /// <param name="schemaIdentifiers">The schema identifiers in physical parameter order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schemaIdentifiers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="schemaIdentifiers"/> contains a null element.</exception>
    public FileSchema(IEnumerable<string> schemaIdentifiers) =>
        SchemaIdentifiers = IsoValueSnapshot.Create(schemaIdentifiers, nameof(schemaIdentifiers));

    /// <summary>Gets the snapshotted schema identifiers in physical parameter order.</summary>
    public IReadOnlyList<string> SchemaIdentifiers { get; }
}
