using System.Globalization;
using System.Numerics;

namespace TedToolkit.Step21;

/// <summary>
/// Represents an exact finite EXPRESS REAL value as an arbitrary-precision decimal significand and exponent.
/// </summary>
/// <remarks>
/// The represented value is <c>Significand × 10^Exponent</c>. Construction removes insignificant trailing
/// decimal zeroes, so numerically identical finite decimal values compare equal without binary floating-point loss.
/// </remarks>
public readonly struct RealValue : IEquatable<RealValue>, IComparable<RealValue>
{
    /// <summary>
    /// Creates an exact finite decimal value.
    /// </summary>
    /// <param name="significand">The arbitrary-precision decimal significand.</param>
    /// <param name="exponent">The arbitrary-precision power of ten.</param>
    /// <example><code>var value = new RealValue(125, -2); // exactly 1.25</code></example>
    public RealValue(BigInteger significand, BigInteger exponent)
    {
        if (significand.IsZero)
        {
            Significand = BigInteger.Zero;
            Exponent = BigInteger.Zero;
            return;
        }

        while (significand % 10 == 0)
        {
            significand /= 10;
            exponent += BigInteger.One;
        }

        Significand = significand;
        Exponent = exponent;
    }

    /// <summary>Gets the normalized arbitrary-precision decimal significand.</summary>
    public BigInteger Significand { get; }

    /// <summary>Gets the normalized arbitrary-precision power of ten.</summary>
    public BigInteger Exponent { get; }

    /// <summary>Creates an exact decimal from the invariant round-trip spelling of a finite CLR approximation.</summary>
    /// <param name="value">The finite binary floating-point value.</param>
    /// <returns>The exact decimal denoted by the CLR round-trip spelling.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not finite.</exception>
    public static RealValue FromDouble(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "An EXPRESS REAL value must be finite.");
        }

        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var exponentMarker = text.IndexOfAny(['E', 'e']);
        var significandText = exponentMarker < 0 ? text : text[..exponentMarker];
        var exponent = exponentMarker < 0
            ? BigInteger.Zero
            : BigInteger.Parse(text[(exponentMarker + 1)..], CultureInfo.InvariantCulture);
        var point = significandText.IndexOf('.');
        if (point >= 0)
        {
            exponent -= significandText.Length - point - 1;
            significandText = significandText.Remove(point, 1);
        }

        return new(BigInteger.Parse(significandText, CultureInfo.InvariantCulture), exponent);
    }

    /// <summary>Converts this finite decimal to the nearest available CLR binary floating-point approximation.</summary>
    /// <returns>The binary floating-point approximation, which can overflow to an infinity.</returns>
    public double ToDouble() => (double)Significand * Math.Pow(10, (double)Exponent);

    /// <summary>Compares two exact finite decimal values.</summary>
    /// <param name="other">The other exact decimal.</param>
    /// <returns>A negative, zero, or positive value according to numeric ordering.</returns>
    public int CompareTo(RealValue other)
    {
        var (left, right) = Align(this, other);
        return left.CompareTo(right);
    }

    /// <summary>Determines whether another exact decimal represents the same value.</summary>
    /// <param name="other">The other exact decimal.</param>
    /// <returns><see langword="true"/> when both normalized components are equal.</returns>
    public bool Equals(RealValue other) => Significand == other.Significand && Exponent == other.Exponent;

    /// <summary>Determines whether an object is an equal exact decimal.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal <see cref="RealValue"/>.</returns>
    public override bool Equals(object? obj) => obj is RealValue other && Equals(other);

    /// <summary>Returns a hash code for the normalized exact decimal.</summary>
    /// <returns>A hash code consistent with <see cref="Equals(RealValue)"/>.</returns>
    public override int GetHashCode() => HashCode.Combine(Significand, Exponent);

    /// <summary>Formats the normalized components for bounded diagnostics.</summary>
    /// <returns>The invariant <c>significandEexponent</c> diagnostic spelling.</returns>
    /// <remarks>This diagnostic representation is not an ISO 10303-21 serialization contract.</remarks>
    public override string ToString() => string.Concat(
        Significand.ToString(CultureInfo.InvariantCulture),
        "E",
        Exponent.ToString(CultureInfo.InvariantCulture));

    /// <summary>Determines whether two exact decimal values are equal.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the values are equal.</returns>
    public static bool operator ==(RealValue left, RealValue right) => left.Equals(right);

    /// <summary>Determines whether two exact decimal values differ.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the values differ.</returns>
    public static bool operator !=(RealValue left, RealValue right) => !left.Equals(right);

    /// <summary>Adds two exact finite decimal values.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The exact finite decimal sum.</returns>
    /// <exception cref="OverflowException">The exponent gap cannot be represented by a CLR-addressable power.</exception>
    public static RealValue operator +(RealValue left, RealValue right)
    {
        var (leftSignificand, rightSignificand) = Align(left, right);
        return new(leftSignificand + rightSignificand, BigInteger.Min(left.Exponent, right.Exponent));
    }

    /// <summary>Returns one exact finite decimal unchanged.</summary>
    /// <param name="value">The value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static RealValue operator +(RealValue value) => value;

    /// <summary>Subtracts two exact finite decimal values.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The exact finite decimal difference.</returns>
    /// <exception cref="OverflowException">The exponent gap cannot be represented by a CLR-addressable power.</exception>
    public static RealValue operator -(RealValue left, RealValue right) => left + (-right);

    /// <summary>Negates one exact finite decimal value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The exact additive inverse.</returns>
    public static RealValue operator -(RealValue value) => new(-value.Significand, value.Exponent);

    /// <summary>Multiplies two exact finite decimal values.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The exact finite decimal product.</returns>
    public static RealValue operator *(RealValue left, RealValue right) =>
        new(left.Significand * right.Significand, left.Exponent + right.Exponent);

    /// <summary>Divides two finite decimal values using the CLR round-trip approximation as the result precision.</summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>The finite decimal denoted by the binary result's invariant round-trip spelling.</returns>
    /// <exception cref="DivideByZeroException"><paramref name="right"/> is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The binary approximation is not finite.</exception>
    public static RealValue operator /(RealValue left, RealValue right)
    {
        if (right.Significand.IsZero)
        {
            throw new DivideByZeroException();
        }

        return FromDouble(left.ToDouble() / right.ToDouble());
    }

    /// <summary>Determines whether the first value is less than the second.</summary>
    public static bool operator <(RealValue left, RealValue right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the first value is greater than the second.</summary>
    public static bool operator >(RealValue left, RealValue right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the first value is less than or equal to the second.</summary>
    public static bool operator <=(RealValue left, RealValue right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the first value is greater than or equal to the second.</summary>
    public static bool operator >=(RealValue left, RealValue right) => left.CompareTo(right) >= 0;

    private static (BigInteger Left, BigInteger Right) Align(RealValue left, RealValue right)
    {
        if (left.Exponent == right.Exponent)
        {
            return (left.Significand, right.Significand);
        }

        var commonExponent = BigInteger.Min(left.Exponent, right.Exponent);
        return (
            left.Significand * PowerOfTen(left.Exponent - commonExponent),
            right.Significand * PowerOfTen(right.Exponent - commonExponent));
    }

    private static BigInteger PowerOfTen(BigInteger exponent)
    {
        if (exponent > int.MaxValue)
        {
            throw new OverflowException("The decimal exponent gap exceeds the CLR-addressable power range.");
        }

        return BigInteger.Pow(10, (int)exponent);
    }
}
