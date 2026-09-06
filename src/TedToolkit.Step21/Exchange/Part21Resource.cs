namespace TedToolkit.Step21;

/// <summary>Represents one ISO 10303-21 resource URI reference without its angle-bracket delimiters.</summary>
public readonly struct Part21Resource : IEquatable<Part21Resource>
{
    private readonly string? _value;

    /// <summary>Creates a resource from its exact URI-reference characters.</summary>
    public Part21Resource(string value)
    {
        Part21NameValidation.ValidateResource(value, nameof(value));
        _value = value;
    }

    /// <summary>Gets the exact URI-reference characters without angle brackets.</summary>
    public string Value => _value ?? throw new InvalidOperationException("The default Part21Resource value is invalid.");

    /// <inheritdoc />
    public bool Equals(Part21Resource other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Part21Resource other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_value ?? string.Empty);

    /// <inheritdoc />
    public override string ToString() => $"<{Value}>";
}

internal static class Part21NameValidation
{
    internal static string CanonicalDigits(string digits, string parameterName, string displayName)
    {
        ArgumentNullException.ThrowIfNull(digits, parameterName);
        if (digits.Length == 0 || digits.Any(character => character is < '0' or > '9'))
            throw new FormatException($"A {displayName} must contain only decimal digits.");

        var first = 0;
        while (first < digits.Length && digits[first] == '0')
            first++;
        if (first == digits.Length)
            throw new ArgumentOutOfRangeException(parameterName, digits, $"A {displayName} must be greater than zero.");

        return digits[first..];
    }

    internal static void ValidateConstant(string value, string parameterName, string displayName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length == 0
            || value[0] is not (>= 'A' and <= 'Z') and not '_'
            || value.Skip(1).Any(character => character is not (>= 'A' and <= 'Z')
                && character is not (>= '0' and <= '9')
                && character is not '_'))
        {
            throw new FormatException($"A {displayName} must contain only Part 21 UPPER or DIGIT characters.");
        }
    }

    internal static void ValidateAnchorName(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length == 0
            || value.Contains('#')
            || !ValidateUriCharacters(value, out var hasNonDigit)
            || !hasNonDigit)
            throw new FormatException("The anchor name is not a valid Part 21 URI fragment identifier.");
    }

    internal static void ValidateResource(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length == 0 || !ValidateUriCharacters(value, out _))
            throw new FormatException("The resource is not a valid Part 21 URI-reference spelling.");

        var fragment = value.IndexOf('#');
        if (fragment >= 0)
        {
            if (value.IndexOf('#', fragment + 1) >= 0)
                throw new FormatException("A Part 21 resource can contain at most one URI fragment delimiter.");
            if (fragment == 0)
                return;
        }

        var resourceLength = fragment >= 0 ? fragment : value.Length;
        var query = value.IndexOf('?', 0, resourceLength);
        var pathLength = query >= 0 ? query : resourceLength;
        if (pathLength == 0)
            throw new FormatException("A Part 21 resource must contain a URI or a fragment identifier.");

        var colon = value.IndexOf(':', 0, pathLength);
        var slash = value.IndexOf('/', 0, pathLength);
        if (colon > 0 && (slash < 0 || colon < slash) && IsScheme(value.AsSpan(0, colon)))
        {
            if (colon + 1 == pathLength)
                throw new FormatException("An absolute Part 21 resource must contain a hierarchical or opaque URI part.");
            return;
        }

        var firstSegmentLength = slash >= 0 ? slash : pathLength;
        if (firstSegmentLength == 0 && value[0] != '/')
            throw new FormatException("The resource is not a valid relative URI reference.");
        if (value.AsSpan(0, firstSegmentLength).Contains(':'))
            throw new FormatException("A relative URI first path segment cannot contain a colon.");
    }

    private static bool ValidateUriCharacters(string value, out bool hasNonDigit)
    {
        hasNonDigit = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is >= '0' and <= '9')
                continue;
            hasNonDigit = true;
            if (character == '%')
            {
                if (index + 2 >= value.Length || !IsHex(value[index + 1]) || !IsHex(value[index + 2]))
                    return false;
                index += 2;
                continue;
            }
            if (!IsUriCharacter(character))
                return false;
        }
        return true;
    }

    private static bool IsScheme(ReadOnlySpan<char> value)
    {
        if (value.Length == 0 || !IsAlpha(value[0]))
            return false;
        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!IsAlpha(character)
                && character is not (>= '0' and <= '9')
                && character is not '+' and not '-' and not '.')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsUriCharacter(char value) => IsAlpha(value)
        || value is >= '0' and <= '9'
        || value is '-' or '_' or '.' or '!' or '~' or '*' or '\'' or '(' or ')'
        or ';' or '/' or '?' or ':' or '@' or '&' or '=' or '+' or '$' or ',' or '#' or '%';

    private static bool IsAlpha(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsHex(char value) => value is >= '0' and <= '9'
        or >= 'A' and <= 'F'
        or >= 'a' and <= 'f';
}