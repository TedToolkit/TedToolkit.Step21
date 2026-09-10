namespace TedToolkit.Step21;

/// <summary>
/// Represents the required ISO 10303-21 <c>FILE_DESCRIPTION</c> header entity.
/// </summary>
/// <remarks>
/// The description list is snapshotted at construction. Lexical and schema-level validity is checked by later
/// validation and writing operations rather than by this editable-structure value.
/// </remarks>
public sealed class FileDescription
{
    /// <summary>Initializes a file description from its ISO parameter values.</summary>
    /// <param name="description">The descriptive strings in physical parameter order.</param>
    /// <param name="implementationLevel">The implementation-level spelling.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="description"/> contains a null element.</exception>
    public FileDescription(IEnumerable<string> description, string implementationLevel)
    {
        Description = IsoValueSnapshot.Create(description, nameof(description));
        Guard.NotNull(implementationLevel);
        ImplementationLevel = implementationLevel;
    }

    /// <summary>Gets the snapshotted descriptive strings in physical parameter order.</summary>
    public IReadOnlyList<string> Description { get; }

    /// <summary>Gets the implementation-level spelling.</summary>
    public string ImplementationLevel { get; }
}
