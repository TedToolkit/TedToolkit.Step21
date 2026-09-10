namespace TedToolkit.Step21;

/// <summary>
/// Represents one nominal ISO 10303-21 governing schema name.
/// </summary>
/// <remarks>
/// The spelling is retained exactly and compared ordinally. The default struct value is not a valid schema name;
/// lexical and schema-level validity is checked by later binding and validation operations.
/// </remarks>
public readonly struct SchemaName : IEquatable<SchemaName>
{
    private readonly string? _value;

    /// <summary>Initializes a schema name while retaining its supplied spelling.</summary>
    /// <param name="value">The schema-name spelling.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public SchemaName(string value)
    {
        Guard.NotNull(value);
        _value = value;
    }

    /// <summary>Gets the retained schema-name spelling.</summary>
    /// <exception cref="InvalidOperationException">This is the invalid default struct value.</exception>
    public string Value => _value
        ?? throw new InvalidOperationException("The default SchemaName value is invalid.");

    /// <summary>Determines whether another schema name has the same ordinal spelling.</summary>
    /// <param name="other">The other schema name.</param>
    /// <returns><see langword="true"/> when the names are equal.</returns>
    public bool Equals(SchemaName other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Determines whether an object is an equal schema name.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal schema name.</returns>
    public override bool Equals(object? obj) => obj is SchemaName other && Equals(other);

    /// <summary>Returns the hash code of the retained ordinal spelling.</summary>
    /// <returns>A hash code consistent with <see cref="Equals(SchemaName)"/>.</returns>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_value ?? string.Empty);

    /// <summary>Returns the retained schema-name spelling.</summary>
    /// <returns>The schema-name spelling.</returns>
    /// <exception cref="InvalidOperationException">This is the invalid default struct value.</exception>
    public override string ToString() => Value;

    internal bool IsDefault => _value is null;
}
