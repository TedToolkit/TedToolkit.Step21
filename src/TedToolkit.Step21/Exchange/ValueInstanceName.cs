namespace TedToolkit.Step21;

/// <summary>Represents one positive ISO 10303-21 value instance name.</summary>
public readonly struct ValueInstanceName : IEquatable<ValueInstanceName>
{
    private readonly string? _digits;

    /// <summary>Creates a value instance name from digits without the leading <c>@</c>.</summary>
    public ValueInstanceName(string digits) =>
        _digits = Part21NameValidation.CanonicalDigits(digits, nameof(digits), "value instance name");

    /// <inheritdoc />
    public bool Equals(ValueInstanceName other) => string.Equals(_digits, other._digits, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ValueInstanceName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_digits ?? string.Empty);

    /// <inheritdoc />
    public override string ToString() => _digits is null
        ? throw new InvalidOperationException("The default ValueInstanceName value is invalid.")
        : $"@{_digits}";

    internal string CanonicalDigits => _digits
        ?? throw new InvalidOperationException("The default ValueInstanceName value is invalid.");
}