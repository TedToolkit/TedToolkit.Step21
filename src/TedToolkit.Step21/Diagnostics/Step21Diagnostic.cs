namespace TedToolkit.Step21;

/// <summary>
/// Represents the severity of an ISO 10303-21 diagnostic.
/// </summary>
public enum Step21DiagnosticSeverity
{
    /// <summary>
    /// Indicates non-invalidating informational evidence.
    /// </summary>
    Information,

    /// <summary>
    /// Indicates non-invalidating warning evidence.
    /// </summary>
    Warning,

    /// <summary>
    /// Indicates an error that prevents the requested stage from completing.
    /// </summary>
    Error,
}

/// <summary>
/// Represents immutable diagnostic evidence from an ISO 10303-21 processing stage.
/// </summary>
public sealed class Step21Diagnostic
{
    /// <summary>
    /// Creates diagnostic evidence.
    /// </summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="severity">The diagnostic severity.</param>
    /// <param name="message">The caller-facing diagnostic message.</param>
    /// <param name="sourceLocation">The optional source position associated with the diagnostic.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="code"/> or <paramref name="message"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// var diagnostic = new Step21Diagnostic(
    ///     "P21-SYNTAX-001",
    ///     Step21DiagnosticSeverity.Error,
    ///     "Expected ENDSEC.",
    ///     new SourceLocation("model.p21", 8, 1));
    /// </code>
    /// </example>
    public Step21Diagnostic(
        string code,
        Step21DiagnosticSeverity severity,
        string message,
        SourceLocation? sourceLocation = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(message);
        Code = code;
        Severity = severity;
        Message = message;
        SourceLocation = sourceLocation;
    }

    /// <summary>
    /// Gets the stable diagnostic code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the diagnostic severity.
    /// </summary>
    public Step21DiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the caller-facing diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional source position associated with the diagnostic.
    /// </summary>
    public SourceLocation? SourceLocation { get; }
}