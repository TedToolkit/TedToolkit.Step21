namespace TedToolkit.Step21;

/// <summary>
/// Represents a position in an EXPRESS schema or ISO 10303-21 source.
/// </summary>
/// <remarks>
/// Line and column numbers are 1-based. The location identifies one position and intentionally has
/// no end-span coordinates.
/// </remarks>
public sealed class SourceLocation
{
    /// <summary>
    /// Creates a source location.
    /// </summary>
    /// <param name="filePath">The path that identifies the source.</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="column">The 1-based column number.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="filePath"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="line"/> or <paramref name="column"/> is less than one.
    /// </exception>
    /// <example>
    /// <code>
    /// var location = new SourceLocation("schemas/example.exp", 12, 5);
    /// </code>
    /// </example>
    public SourceLocation(string filePath, int line, int column)
    {
        Guard.NotNull(filePath);
        Guard.LessThan(line, 1);
        Guard.LessThan(column, 1);
        FilePath = filePath;
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Gets the path that identifies the source.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the 1-based line number.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the 1-based column number.
    /// </summary>
    public int Column { get; }
}