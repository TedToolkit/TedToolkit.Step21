namespace TedToolkit.Step21;

/// <summary>
/// Represents one positive ISO 10303-21 entity instance name without imposing a CLR integer limit.
/// </summary>
/// <remarks>
/// Construction accepts decimal digits without the leading <c>#</c>. Leading zeros are removed for equality,
/// hashing, and formatting. The default struct value is not a valid entity instance name.
/// </remarks>
public readonly struct EntityInstanceName : IEquatable<EntityInstanceName>
{
    private readonly string? _digits;

    /// <summary>
    /// Creates a positive entity instance name from an arbitrary-length decimal digit sequence.
    /// </summary>
    /// <param name="digits">The decimal digits without a leading <c>#</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="digits"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="digits"/> is empty or contains a non-decimal character.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="digits"/> represents zero.</exception>
    public EntityInstanceName(string digits)
    {
        Guard.NotNull(digits);
        if (digits.Length == 0)
            throw new FormatException("An entity instance name must contain decimal digits.");

        foreach (var character in digits)
        {
            if (character is < '0' or > '9')
                throw new FormatException("An entity instance name must contain only decimal digits.");
        }

        var firstSignificantDigit = 0;
        while (firstSignificantDigit < digits.Length && digits[firstSignificantDigit] == '0')
            firstSignificantDigit++;
        if (firstSignificantDigit == digits.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(digits),
                digits,
                "An entity instance name must be greater than zero.");
        }

        _digits = digits[firstSignificantDigit..];
    }

    /// <summary>Determines whether another name has the same canonical positive value.</summary>
    /// <param name="other">The other entity instance name.</param>
    /// <returns><see langword="true"/> when the names are equal.</returns>
    public bool Equals(EntityInstanceName other) => string.Equals(_digits, other._digits, StringComparison.Ordinal);

    /// <summary>Determines whether an object is an equal entity instance name.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal name.</returns>
    public override bool Equals(object? obj) => obj is EntityInstanceName other && Equals(other);

    /// <summary>Returns the hash code of the canonical positive value.</summary>
    /// <returns>A hash code consistent with <see cref="Equals(EntityInstanceName)"/>.</returns>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_digits ?? string.Empty);

    /// <summary>Formats the canonical entity instance name with its required <c>#</c> prefix.</summary>
    /// <returns>The canonical ISO 10303-21 spelling.</returns>
    /// <exception cref="InvalidOperationException">This is the invalid default struct value.</exception>
    public override string ToString() => _digits is null
        ? throw new InvalidOperationException("The default EntityInstanceName value is invalid.")
        : $"#{_digits}";

    internal string CanonicalDigits => _digits
        ?? throw new InvalidOperationException("The default EntityInstanceName value is invalid.");
}
