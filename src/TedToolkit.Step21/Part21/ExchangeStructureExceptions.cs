namespace TedToolkit.Step21;

/// <summary>
/// Represents an ISO 10303-21 syntax-stage failure with complete diagnostic evidence.
/// </summary>
public sealed class ExchangeStructureSyntaxException : Exception
{
    /// <summary>
    /// Creates a syntax-stage exception.
    /// </summary>
    /// <param name="diagnostics">The complete ordered syntax diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="diagnostics"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// var exception = new ExchangeStructureSyntaxException(
    ///     [new Step21Diagnostic("P21-SYNTAX-001", Step21DiagnosticSeverity.Error, "Invalid syntax.")]);
    /// </code>
    /// </example>
    public ExchangeStructureSyntaxException(IEnumerable<Step21Diagnostic> diagnostics)
        : base("The ISO 10303-21 exchange structure contains syntax errors.")
    {
        Guard.NotNull(diagnostics);
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>
    /// Gets the complete ordered syntax diagnostics.
    /// </summary>
    public IReadOnlyList<Step21Diagnostic> Diagnostics { get; }
}

/// <summary>
/// Represents a schema-binding-stage failure with complete diagnostic evidence.
/// </summary>
public sealed class ExchangeStructureBindingException : Exception
{
    /// <summary>
    /// Creates a binding-stage exception.
    /// </summary>
    /// <param name="diagnostics">The complete ordered binding diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="diagnostics"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// var exception = new ExchangeStructureBindingException(
    ///     [new Step21Diagnostic("P21-BIND-001", Step21DiagnosticSeverity.Error, "Unknown schema.")]);
    /// </code>
    /// </example>
    public ExchangeStructureBindingException(IEnumerable<Step21Diagnostic> diagnostics)
        : base("The ISO 10303-21 exchange structure could not be bound to the supplied schemas.")
    {
        Guard.NotNull(diagnostics);
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>
    /// Gets the complete ordered binding diagnostics.
    /// </summary>
    public IReadOnlyList<Step21Diagnostic> Diagnostics { get; }
}

/// <summary>
/// Represents a failed read-boundary validation with the complete validation result.
/// </summary>
public sealed class ExchangeStructureReadValidationException : Exception
{
    /// <summary>
    /// Creates a read-validation exception.
    /// </summary>
    /// <param name="validationResult">The complete invalid result produced before publication.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="validationResult"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// var exception = new ExchangeStructureReadValidationException(
    ///     new ValidationResult(
    ///     [
    ///         new ValidationFailure("P21-READ-001", "DataSections[0].#1", "Invalid entity."),
    ///     ]));
    /// </code>
    /// </example>
    public ExchangeStructureReadValidationException(ValidationResult validationResult)
        : base("The ISO 10303-21 exchange structure is invalid and cannot be published.")
    {
        Guard.NotNull(validationResult);
        ValidationResult = validationResult;
    }

    /// <summary>
    /// Gets the complete validation result produced before publication.
    /// </summary>
    public ValidationResult ValidationResult { get; }
}

/// <summary>
/// Represents a failed write-boundary validation with the complete validation result.
/// </summary>
public sealed class ExchangeStructureWriteValidationException : Exception
{
    /// <summary>
    /// Creates a write-validation exception.
    /// </summary>
    /// <param name="validationResult">The complete invalid result produced before output.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="validationResult"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// var exception = new ExchangeStructureWriteValidationException(
    ///     new ValidationResult(
    ///     [
    ///         new ValidationFailure("P21-WRITE-001", "DataSections[0].#1", "Invalid entity."),
    ///     ]));
    /// </code>
    /// </example>
    public ExchangeStructureWriteValidationException(ValidationResult validationResult)
        : base("The ISO 10303-21 exchange structure is invalid and cannot be written.")
    {
        Guard.NotNull(validationResult);
        ValidationResult = validationResult;
    }

    /// <summary>
    /// Gets the complete validation result produced before output.
    /// </summary>
    public ValidationResult ValidationResult { get; }
}

/// <summary>
/// Represents an unavailable exchange-structure operation with complete diagnostic evidence.
/// </summary>
public sealed class ExchangeStructureCapabilityException : Exception
{
    /// <summary>
    /// Creates a capability exception.
    /// </summary>
    /// <param name="diagnostics">The complete ordered capability diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="diagnostics"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// var exception = new ExchangeStructureCapabilityException(
    ///     [new Step21Diagnostic("P21-CAP-001", Step21DiagnosticSeverity.Error, "Unsupported operation.")]);
    /// </code>
    /// </example>
    public ExchangeStructureCapabilityException(IEnumerable<Step21Diagnostic> diagnostics)
        : base("The ISO 10303-21 exchange structure requires an unsupported capability.")
    {
        Guard.NotNull(diagnostics);
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>
    /// Gets the complete ordered capability diagnostics.
    /// </summary>
    public IReadOnlyList<Step21Diagnostic> Diagnostics { get; }
}
