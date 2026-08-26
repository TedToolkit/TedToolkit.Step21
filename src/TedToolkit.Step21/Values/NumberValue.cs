using System.Globalization;
using System.Numerics;

namespace TedToolkit.Step21;

/// <summary>Represents an EXPRESS NUMBER without conflating its INTEGER and REAL alternatives.</summary>
public readonly struct NumberValue : IEquatable<NumberValue>, IComparable<NumberValue>
{
    private readonly BigInteger _integer;
    private readonly RealValue _real;

    private NumberValue(NumberValueKind kind, BigInteger integer, RealValue real)
    {
        Kind = kind;
        _integer = integer;
        _real = real;
    }

    /// <summary>Gets the retained exact numeric alternative.</summary>
    public NumberValueKind Kind { get; }

    /// <summary>Creates a NUMBER that retains an arbitrary-precision INTEGER.</summary>
    /// <param name="value">The exact integer.</param>
    /// <returns>A NUMBER containing the integer alternative.</returns>
    /// <example><code>var number = NumberValue.FromInteger(BigInteger.Parse("18446744073709551616"));</code></example>
    public static NumberValue FromInteger(BigInteger value) => new(NumberValueKind.Integer, value, default);

    /// <summary>Creates a NUMBER that retains an exact finite REAL.</summary>
    /// <param name="value">The exact decimal value.</param>
    /// <returns>A NUMBER containing the real alternative.</returns>
    /// <example><code>var number = NumberValue.FromReal(new RealValue(125, -2));</code></example>
    public static NumberValue FromReal(RealValue value) => new(NumberValueKind.Real, default, value);

    /// <summary>Attempts to parse one exact EXPRESS INTEGER or REAL literal spelling.</summary>
    /// <param name="text">The candidate literal without an EXPRESS string delimiter.</param>
    /// <param name="value">Receives the exact retained number, or zero when parsing fails.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is an INTEGER or REAL literal.</returns>
    public static bool TryParse(string? text, out NumberValue value)
    {
        value = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.All(IsAsciiDigit))
        {
            value = FromInteger(BigInteger.Parse(text, CultureInfo.InvariantCulture));
            return true;
        }

        var exponentMarker = text.IndexOfAny(['E', 'e']);
        var significandText = exponentMarker < 0 ? text : text[..exponentMarker];
        var exponentText = exponentMarker < 0 ? null : text[(exponentMarker + 1)..];
        var point = significandText.IndexOf('.');
        if (point <= 0
            || point != significandText.LastIndexOf('.')
            || !significandText[..point].All(IsAsciiDigit)
            || !significandText[(point + 1)..].All(IsAsciiDigit)
            || exponentText is not null && !IsExponent(exponentText))
        {
            return false;
        }

        var digits = significandText.Remove(point, 1);
        var exponent = exponentText is null
            ? BigInteger.Zero
            : BigInteger.Parse(exponentText, CultureInfo.InvariantCulture);
        exponent -= significandText.Length - point - 1;
        value = FromReal(new RealValue(BigInteger.Parse(digits, CultureInfo.InvariantCulture), exponent));
        return true;
    }

    /// <summary>Attempts to obtain the INTEGER alternative.</summary>
    /// <param name="value">Receives the exact integer, or zero when this is the REAL alternative.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="NumberValueKind.Integer"/>.</returns>
    public bool TryGetInteger(out BigInteger value)
    {
        value = Kind == NumberValueKind.Integer ? _integer : default;
        return Kind == NumberValueKind.Integer;
    }

    /// <summary>Attempts to obtain the REAL alternative.</summary>
    /// <param name="value">Receives the exact real, or zero when this is the INTEGER alternative.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="NumberValueKind.Real"/>.</returns>
    public bool TryGetReal(out RealValue value)
    {
        value = Kind == NumberValueKind.Real ? _real : default;
        return Kind == NumberValueKind.Real;
    }

    /// <summary>Converts this NUMBER to an exact finite REAL without changing its mathematical value.</summary>
    /// <returns>The retained REAL or an exact exponent-zero representation of the INTEGER.</returns>
    public RealValue ToReal() => Kind switch
    {
        NumberValueKind.Integer => new RealValue(_integer, BigInteger.Zero),
        NumberValueKind.Real => _real,
        _ => throw new InvalidOperationException("The NUMBER alternative is invalid."),
    };

    /// <summary>Converts this NUMBER to the nearest available CLR binary floating-point approximation.</summary>
    /// <returns>The binary floating-point approximation, which can overflow to an infinity.</returns>
    public double ToDouble() => Kind == NumberValueKind.Integer ? (double)_integer : _real.ToDouble();

    /// <summary>Formats this NUMBER according to the EXPRESS FORMAT function.</summary>
    /// <param name="format">The symbolic format, picture format, or an empty string for the standard format.</param>
    /// <returns>The formatted EXPRESS string.</returns>
    /// <exception cref="ArgumentException"><paramref name="format"/> is not a valid EXPRESS format.</exception>
    public string Format(string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (format.Length == 0)
        {
            format = Kind == NumberValueKind.Integer ? "7I" : "10.1E";
        }

        return IsSymbolicFormat(format)
            ? FormatSymbolic(format)
            : FormatPicture(format);
    }

    /// <summary>Truncates this NUMBER toward zero to an arbitrary-precision INTEGER.</summary>
    /// <returns>The retained INTEGER or the REAL with its fractional portion removed.</returns>
    /// <exception cref="OverflowException">The decimal exponent magnitude exceeds the CLR-addressable power range.</exception>
    public BigInteger ToIntegerTruncated()
    {
        if (Kind == NumberValueKind.Integer)
        {
            return _integer;
        }

        var exponent = _real.Exponent;
        if (BigInteger.Abs(exponent) > int.MaxValue)
        {
            throw new OverflowException("The decimal exponent magnitude exceeds the CLR-addressable power range.");
        }

        var power = BigInteger.Pow(10, (int)BigInteger.Abs(exponent));
        return exponent.Sign >= 0 ? _real.Significand * power : _real.Significand / power;
    }

    /// <summary>Compares two NUMBER values according to their mathematical values.</summary>
    /// <param name="other">The other number.</param>
    /// <returns>A negative, zero, or positive value according to numeric ordering.</returns>
    public int CompareTo(NumberValue other) => ToReal().CompareTo(other.ToReal());

    /// <summary>Determines whether another NUMBER retains the same alternative and exact value.</summary>
    /// <param name="other">The other NUMBER.</param>
    /// <returns><see langword="true"/> when both values are equal.</returns>
    public bool Equals(NumberValue other) => Kind == other.Kind && Kind switch
    {
        NumberValueKind.Integer => _integer == other._integer,
        NumberValueKind.Real => _real == other._real,
        _ => false,
    };

    /// <summary>Determines whether an object is an equal NUMBER.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal NUMBER.</returns>
    public override bool Equals(object? obj) => obj is NumberValue other && Equals(other);

    /// <summary>Returns a hash code for the retained alternative and exact value.</summary>
    /// <returns>A hash code consistent with <see cref="Equals(NumberValue)"/>.</returns>
    public override int GetHashCode() => Kind switch
    {
        NumberValueKind.Integer => HashCode.Combine(Kind, _integer),
        NumberValueKind.Real => HashCode.Combine(Kind, _real),
        _ => Kind.GetHashCode(),
    };

    /// <summary>Determines whether two NUMBER values are equal.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the values are equal.</returns>
    public static bool operator ==(NumberValue left, NumberValue right) => left.Equals(right);

    /// <summary>Determines whether two NUMBER values differ.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the values differ.</returns>
    public static bool operator !=(NumberValue left, NumberValue right) => !left.Equals(right);

    /// <summary>Adds two NUMBER values, retaining INTEGER only when both operands are INTEGER.</summary>
    public static NumberValue operator +(NumberValue left, NumberValue right)
    {
        return left.Kind == NumberValueKind.Integer && right.Kind == NumberValueKind.Integer
            ? FromInteger(left._integer + right._integer)
            : FromReal(left.ToReal() + right.ToReal());
    }

    /// <summary>Returns one NUMBER unchanged.</summary>
    public static NumberValue operator +(NumberValue value) => value;

    /// <summary>Subtracts two NUMBER values, retaining INTEGER only when both operands are INTEGER.</summary>
    public static NumberValue operator -(NumberValue left, NumberValue right) => left + (-right);

    /// <summary>Negates one NUMBER while retaining its alternative.</summary>
    public static NumberValue operator -(NumberValue value) => value.Kind switch
    {
        NumberValueKind.Integer => FromInteger(-value._integer),
        NumberValueKind.Real => FromReal(-value._real),
        _ => throw new InvalidOperationException("The NUMBER alternative is invalid."),
    };

    /// <summary>Multiplies two NUMBER values, retaining INTEGER only when both operands are INTEGER.</summary>
    public static NumberValue operator *(NumberValue left, NumberValue right)
    {
        return left.Kind == NumberValueKind.Integer && right.Kind == NumberValueKind.Integer
            ? FromInteger(left._integer * right._integer)
            : FromReal(left.ToReal() * right.ToReal());
    }

    /// <summary>Divides two NUMBER values and returns the required REAL result.</summary>
    public static RealValue operator /(NumberValue left, NumberValue right) => left.ToReal() / right.ToReal();

    /// <summary>Determines whether the first NUMBER is less than the second.</summary>
    public static bool operator <(NumberValue left, NumberValue right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the first NUMBER is greater than the second.</summary>
    public static bool operator >(NumberValue left, NumberValue right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the first NUMBER is less than or equal to the second.</summary>
    public static bool operator <=(NumberValue left, NumberValue right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the first NUMBER is greater than or equal to the second.</summary>
    public static bool operator >=(NumberValue left, NumberValue right) => left.CompareTo(right) >= 0;

    private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';

    private static bool IsSymbolicFormat(string format)
    {
        return format.Length > 1 && char.ToUpperInvariant(format[^1]) is 'I' or 'F' or 'E';
    }

    private string FormatSymbolic(string format)
    {
        var position = 0;
        var signControl = format[position] is '+' or '-' ? format[position++] : '-';
        var zeroPad = position < format.Length && format[position] == '0';
        var widthStart = position;
        while (position < format.Length && IsAsciiDigit(format[position]))
        {
            position++;
        }

        if (widthStart == position
            || !int.TryParse(format[widthStart..position], NumberStyles.None, CultureInfo.InvariantCulture, out var width))
        {
            throw new ArgumentException("The EXPRESS symbolic format requires a decimal width.", nameof(format));
        }

        int? decimals = null;
        if (position < format.Length && format[position] == '.')
        {
            var decimalStart = ++position;
            while (position < format.Length && IsAsciiDigit(format[position]))
            {
                position++;
            }

            if (decimalStart == position
                || !int.TryParse(
                    format[decimalStart..position],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var parsedDecimals))
            {
                throw new ArgumentException("The EXPRESS symbolic format has an invalid decimal count.", nameof(format));
            }

            decimals = parsedDecimals;
        }

        if (position != format.Length - 1)
        {
            throw new ArgumentException("The EXPRESS symbolic format contains trailing characters.", nameof(format));
        }

        var type = char.ToUpperInvariant(format[position]);
        if (width <= 2
            || type == 'I' && decimals.HasValue
            || type == 'F' && width < 4
            || type == 'E' && (width < 7 || decimals is null or < 1)
            || decimals < 1)
        {
            throw new ArgumentException("The EXPRESS symbolic format violates its width or decimal constraints.", nameof(format));
        }

        decimals ??= type == 'F' ? 2 : 0;
        var negative = ToReal().Significand.Sign < 0;
        var zero = ToReal().Significand.IsZero;
        var magnitude = type switch
        {
            'I' => FormatFixedMagnitude(0),
            'F' => FormatFixedMagnitude(decimals.Value),
            'E' => FormatScientificMagnitude(decimals.Value, zeroPad),
            _ => throw new ArgumentException("The EXPRESS symbolic format has an unknown type.", nameof(format)),
        };
        var sign = negative ? '-' : signControl == '+' && !zero ? '+' : ' ';
        var padding = Math.Max(0, width - magnitude.Length - 1);
        return zeroPad
            ? string.Concat(sign, new string('0', padding), magnitude)
            : string.Concat(new string(' ', padding), sign, magnitude);
    }

    private string FormatPicture(string format)
    {
        if (!format.Contains('#'))
        {
            throw new ArgumentException("The EXPRESS picture format requires at least one digit position.", nameof(format));
        }

        var point = format.IndexOf('.');
        var comma = format.IndexOf(',');
        var decimalCharacter = point < 0
            ? comma >= 0 ? ',' : '\0'
            : comma < 0 || comma < point ? '.' : ',';
        var groupingCharacter = point >= 0 && comma >= 0
            ? decimalCharacter == '.' ? ',' : '.'
            : '\0';
        var decimalIndex = decimalCharacter == '\0' ? format.Length : format.IndexOf(decimalCharacter);
        var decimals = format[(decimalIndex == format.Length ? format.Length : decimalIndex + 1)..]
            .Count(character => character == '#');
        var fixedMagnitude = FormatFixedMagnitude(decimals);
        var fixedPoint = fixedMagnitude.IndexOf('.');
        var integer = fixedPoint < 0 ? fixedMagnitude : fixedMagnitude[..fixedPoint];
        var fraction = fixedPoint < 0 ? string.Empty : fixedMagnitude[(fixedPoint + 1)..];
        var integerPositions = format[..decimalIndex].Count(character => character == '#');
        var extra = integer.Length > integerPositions ? integer[..(integer.Length - integerPositions)] : string.Empty;
        var integerOffset = Math.Max(0, integerPositions - integer.Length);
        var integerPosition = 0;
        var fractionPosition = 0;
        var negative = ToReal().Significand.Sign < 0;
        var builder = new System.Text.StringBuilder(format.Length + extra.Length).Append(extra);
        for (var index = 0; index < format.Length; index++)
        {
            var character = format[index];
            if (character == '#')
            {
                if (index < decimalIndex)
                {
                    builder.Append(integerPosition < integerOffset
                        ? ' '
                        : integer[integerPosition - integerOffset]);
                    integerPosition++;
                }
                else
                {
                    builder.Append(fractionPosition < fraction.Length ? fraction[fractionPosition] : '0');
                    fractionPosition++;
                }

                continue;
            }

            if (character == groupingCharacter)
            {
                builder.Append(integerPosition > integerOffset && integerPosition < integerPositions ? character : ' ');
            }
            else if (character == decimalCharacter)
            {
                builder.Append(character);
            }
            else if (character == '+')
            {
                builder.Append(negative ? '-' : '+');
            }
            else if (character == '-')
            {
                builder.Append(negative ? '-' : ' ');
            }
            else if (character is '(' or ')')
            {
                builder.Append(negative ? character : ' ');
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private string FormatFixedMagnitude(int decimals)
    {
        var value = ToReal();
        var significand = BigInteger.Abs(value.Significand);
        var scale = value.Exponent + decimals;
        BigInteger rounded;
        if (scale.Sign >= 0)
        {
            rounded = significand * DecimalPower(scale);
        }
        else
        {
            var divisor = DecimalPower(BigInteger.Abs(scale));
            rounded = BigInteger.DivRem(significand, divisor, out var remainder);
            if (remainder * 2 >= divisor)
            {
                rounded++;
            }
        }

        var digits = rounded.ToString(CultureInfo.InvariantCulture);
        if (decimals == 0)
        {
            return digits;
        }

        digits = digits.PadLeft(decimals + 1, '0');
        return string.Concat(digits[..^decimals], ".", digits[^decimals..]);
    }

    private string FormatScientificMagnitude(int decimals, bool leadingZero)
    {
        var value = ToReal();
        if (value.Significand.IsZero)
        {
            return string.Concat(leadingZero ? "0." : "0.", new string('0', decimals), "E+00");
        }

        var sourceDigits = BigInteger.Abs(value.Significand).ToString(CultureInfo.InvariantCulture);
        var exponent = value.Exponent + sourceDigits.Length - 1;
        var requiredDigits = decimals + 1;
        BigInteger rounded;
        if (sourceDigits.Length <= requiredDigits)
        {
            rounded = BigInteger.Parse(sourceDigits.PadRight(requiredDigits, '0'), CultureInfo.InvariantCulture);
        }
        else
        {
            var divisor = DecimalPower(sourceDigits.Length - requiredDigits);
            var significand = BigInteger.Parse(sourceDigits, CultureInfo.InvariantCulture);
            rounded = BigInteger.DivRem(significand, divisor, out var remainder);
            if (remainder * 2 >= divisor)
            {
                rounded++;
            }
        }

        var digits = rounded.ToString(CultureInfo.InvariantCulture);
        if (digits.Length > requiredDigits)
        {
            exponent++;
            digits = digits[..requiredDigits];
        }

        digits = digits.PadLeft(requiredDigits, '0');
        var mantissa = leadingZero
            ? string.Concat("0.", digits[..decimals])
            : string.Concat(digits[..1], ".", digits[1..]);
        if (leadingZero)
        {
            exponent++;
        }

        var exponentSign = exponent.Sign < 0 ? '-' : '+';
        var exponentDigits = BigInteger.Abs(exponent).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
        return string.Concat(mantissa, "E", exponentSign, exponentDigits);
    }

    private static BigInteger DecimalPower(BigInteger exponent)
    {
        if (exponent > int.MaxValue)
        {
            throw new OverflowException("The decimal exponent exceeds the CLR-addressable power range.");
        }

        return BigInteger.Pow(10, (int)exponent);
    }

    private static bool IsExponent(string text)
    {
        var digits = text.Length > 0 && text[0] is '+' or '-' ? text[1..] : text;
        return digits.Length > 0 && digits.All(IsAsciiDigit);
    }
}
