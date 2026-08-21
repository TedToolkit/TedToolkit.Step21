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
public readonly struct RealValue : IEquatable<RealValue>
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
}