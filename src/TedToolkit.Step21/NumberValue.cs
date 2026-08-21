using System.Numerics;

namespace TedToolkit.Step21;

/// <summary>Represents an EXPRESS NUMBER without conflating its INTEGER and REAL alternatives.</summary>
public readonly struct NumberValue : IEquatable<NumberValue>
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
}