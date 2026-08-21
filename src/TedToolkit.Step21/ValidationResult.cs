namespace TedToolkit.Step21;

/// <summary>
/// Represents the complete ordered result of one exchange-structure validation operation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Creates a validation result from every detected failure in deterministic order.
    /// </summary>
    /// <param name="failures">The complete ordered failures; an empty sequence represents success.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="failures"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// var result = new ValidationResult(
    /// [
    ///     new ValidationFailure("EXAMPLE.WR1", "DataSections[0].#1", "Rule WR1 failed."),
    /// ]);
    /// </code>
    /// </example>
    public ValidationResult(IEnumerable<ValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        Failures = Array.AsReadOnly(failures.ToArray());
    }

    /// <summary>
    /// Gets every detected failure in deterministic order.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Failures { get; }

    /// <summary>
    /// Gets a value indicating whether no validation failure was detected.
    /// </summary>
    public bool IsValid => Failures.Count == 0;
}