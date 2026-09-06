namespace TedToolkit.Step21;

/// <summary>Represents one ISO 10303-21 anchor name without its angle-bracket delimiters.</summary>
public readonly struct AnchorName : IEquatable<AnchorName>
{
    private readonly string? _value;
    private readonly Guid _uuid;

    /// <summary>Creates an anchor name from its exact URI-reference characters.</summary>
    /// <param name="value">The name without surrounding angle brackets.</param>
    public AnchorName(string value)
    {
        Part21NameValidation.ValidateAnchorName(value, nameof(value));
        _value = value;
        IsUuid = Guid.TryParseExact(value, "D", out _uuid);
    }

    /// <summary>Gets the exact name characters without angle brackets.</summary>
    public string Value => _value ?? throw new InvalidOperationException("The default AnchorName value is invalid.");

    /// <summary>Gets whether this name is an RFC 4122 UUID spelling as defined by Annex G.</summary>
    public bool IsUuid { get; }

    /// <summary>Attempts to obtain the UUID represented by this anchor name.</summary>
    public bool TryGetUuid(out Guid value)
    {
        value = IsUuid ? _uuid : default;
        return IsUuid;
    }

    /// <inheritdoc />
    public bool Equals(AnchorName other) => IsUuid && other.IsUuid
        ? _uuid == other._uuid
        : string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AnchorName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => IsUuid
        ? _uuid.GetHashCode()
        : StringComparer.Ordinal.GetHashCode(_value ?? string.Empty);

    /// <inheritdoc />
    public override string ToString() => $"<{Value}>";
}