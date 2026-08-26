namespace TedToolkit.Step21;

/// <summary>
/// Represents one immutable failure detected while validating an exchange structure.
/// </summary>
/// <remarks>
/// Every failure invalidates its containing <see cref="ValidationResult"/>. Non-invalidating
/// information and warnings are represented by <see cref="Step21Diagnostic"/> instead.
/// </remarks>
public sealed class ValidationFailure
{
    /// <summary>
    /// Creates a validation failure.
    /// </summary>
    /// <param name="code">The stable failure code.</param>
    /// <param name="path">The deterministic path to the invalid value.</param>
    /// <param name="message">The caller-facing failure message.</param>
    /// <param name="sourceLocation">The optional EXPRESS constraint source position.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="code"/>, <paramref name="path"/>, or <paramref name="message"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// var failure = new ValidationFailure(
    ///     "EXAMPLE.WHERE.WR1",
    ///     "DataSections[0].#42.Length",
    ///     "Length must be positive.");
    /// </code>
    /// </example>
    public ValidationFailure(
        string code,
        string path,
        string message,
        SourceLocation? sourceLocation = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(message);
        Code = code;
        Path = path;
        Message = message;
        SourceLocation = sourceLocation;
    }

    /// <summary>
    /// Gets the stable failure code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the deterministic path to the invalid value.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the caller-facing failure message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional EXPRESS constraint source position.
    /// </summary>
    public SourceLocation? SourceLocation { get; }
}