namespace TedToolkit.Step21;

/// <summary>
/// Represents the required ISO 10303-21 <c>FILE_NAME</c> header entity.
/// </summary>
/// <remarks>
/// Author and organization lists are snapshotted. The timestamp remains its ISO source spelling so this value does
/// not invent a CLR date-time interpretation before binding and validation.
/// </remarks>
public sealed class FileName
{
    /// <summary>Initializes a file name from its complete ISO parameter set.</summary>
    /// <param name="name">The exchange-file name.</param>
    /// <param name="timeStamp">The ISO timestamp spelling.</param>
    /// <param name="author">The author strings in physical parameter order.</param>
    /// <param name="organization">The organization strings in physical parameter order.</param>
    /// <param name="preprocessorVersion">The originating preprocessor version.</param>
    /// <param name="originatingSystem">The originating system.</param>
    /// <param name="authorization">The authorization text.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A supplied collection contains a null element.</exception>
    public FileName(
        string name,
        string timeStamp,
        IEnumerable<string> author,
        IEnumerable<string> organization,
        string preprocessorVersion,
        string originatingSystem,
        string authorization)
    {
        Guard.NotNull(name);
        Guard.NotNull(timeStamp);
        Guard.NotNull(preprocessorVersion);
        Guard.NotNull(originatingSystem);
        Guard.NotNull(authorization);

        Name = name;
        TimeStamp = timeStamp;
        Author = IsoValueSnapshot.Create(author, nameof(author));
        Organization = IsoValueSnapshot.Create(organization, nameof(organization));
        PreprocessorVersion = preprocessorVersion;
        OriginatingSystem = originatingSystem;
        Authorization = authorization;
    }

    /// <summary>Gets the exchange-file name.</summary>
    public string Name { get; }

    /// <summary>Gets the ISO timestamp spelling.</summary>
    public string TimeStamp { get; }

    /// <summary>Gets the snapshotted author strings in physical parameter order.</summary>
    public IReadOnlyList<string> Author { get; }

    /// <summary>Gets the snapshotted organization strings in physical parameter order.</summary>
    public IReadOnlyList<string> Organization { get; }

    /// <summary>Gets the originating preprocessor version.</summary>
    public string PreprocessorVersion { get; }

    /// <summary>Gets the originating system.</summary>
    public string OriginatingSystem { get; }

    /// <summary>Gets the authorization text.</summary>
    public string Authorization { get; }
}
