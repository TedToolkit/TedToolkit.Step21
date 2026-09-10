namespace TedToolkit.Step21;

/// <summary>
/// Composes the three required strongly typed ISO 10303-21 header entities.
/// </summary>
public sealed class HeaderSection
{
    /// <summary>Initializes a header section from its required ISO entities.</summary>
    /// <param name="fileDescription">The required file-description entity.</param>
    /// <param name="fileName">The required file-name entity.</param>
    /// <param name="fileSchema">The required file-schema entity.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public HeaderSection(FileDescription fileDescription, FileName fileName, FileSchema fileSchema)
    {
        ArgumentNullException.ThrowIfNull(fileDescription);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(fileSchema);

        FileDescription = fileDescription;
        FileName = fileName;
        FileSchema = fileSchema;
    }

    /// <summary>Gets the required file-description entity.</summary>
    public FileDescription FileDescription { get; }

    /// <summary>Gets the required file-name entity.</summary>
    public FileName FileName { get; }

    /// <summary>Gets the required file-schema entity.</summary>
    public FileSchema FileSchema { get; }

}
