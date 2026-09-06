namespace TedToolkit.Step21;

/// <summary>Represents one ISO 10303-21 constant entity occurrence name.</summary>
public readonly struct ConstantEntityName : IEquatable<ConstantEntityName>
{
    private readonly string? _value;

    /// <summary>Creates a constant entity name without its leading <c>#</c>.</summary>
    public ConstantEntityName(string value)
    {
        Part21NameValidation.ValidateConstant(value, nameof(value), "constant entity name");
        _value = value;
    }

    /// <summary>Gets the canonical EXPRESS constant name.</summary>
    public string Value => _value ?? throw new InvalidOperationException("The default ConstantEntityName value is invalid.");

    /// <inheritdoc />
    public bool Equals(ConstantEntityName other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ConstantEntityName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_value ?? string.Empty);

    /// <inheritdoc />
    public override string ToString() => $"#{Value}";
}