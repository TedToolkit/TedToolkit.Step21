namespace TedToolkit.Step21;

/// <summary>Represents one ISO 10303-21 constant value occurrence name.</summary>
public readonly struct ConstantValueName : IEquatable<ConstantValueName>
{
    private readonly string? _value;

    /// <summary>Creates a constant value name without its leading <c>@</c>.</summary>
    public ConstantValueName(string value)
    {
        Part21NameValidation.ValidateConstant(value, nameof(value), "constant value name");
        _value = value;
    }

    /// <summary>Gets the canonical EXPRESS constant name.</summary>
    public string Value => _value ?? throw new InvalidOperationException("The default ConstantValueName value is invalid.");

    /// <inheritdoc />
    public bool Equals(ConstantValueName other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ConstantValueName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_value ?? string.Empty);

    /// <inheritdoc />
    public override string ToString() => $"@{Value}";
}