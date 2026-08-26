namespace TedToolkit.Step21;

/// <summary>Represents an immutable, bit-accurate EXPRESS BINARY value.</summary>
/// <remarks>Leading zeroes and an empty bit sequence are significant and preserved.</remarks>
public sealed class BinaryValue : IEquatable<BinaryValue>
{
    private readonly string _bits;

    /// <summary>Creates a binary value from its exact bit sequence.</summary>
    /// <param name="bits">A sequence containing only <c>0</c> and <c>1</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bits"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="bits"/> contains a character other than <c>0</c> or <c>1</c>.</exception>
    /// <example><code>var value = new BinaryValue("00101");</code></example>
    public BinaryValue(string bits)
    {
        ArgumentNullException.ThrowIfNull(bits);
        if (bits.Any(bit => bit is not ('0' or '1')))
            throw new FormatException("An EXPRESS BINARY value may contain only '0' and '1'.");

        _bits = bits;
    }

    /// <summary>Gets the exact number of retained bits.</summary>
    public int Length => _bits.Length;

    /// <summary>Gets the bit at a zero-based position.</summary>
    /// <param name="index">The zero-based bit index.</param>
    /// <returns><see langword="true"/> for <c>1</c>; otherwise <see langword="false"/>.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the retained sequence.</exception>
    public bool this[int index] => _bits[index] == '1';

    /// <summary>Determines whether another binary value has the same length and bits.</summary>
    /// <param name="other">The other binary value.</param>
    /// <returns><see langword="true"/> when both bit sequences are equal.</returns>
    public bool Equals(BinaryValue? other) => other is not null
        && string.Equals(_bits, other._bits, StringComparison.Ordinal);

    /// <summary>Determines whether an object is an equal binary value.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal binary value.</returns>
    public override bool Equals(object? obj) => Equals(obj as BinaryValue);

    /// <summary>Returns a hash code for the exact bit sequence.</summary>
    /// <returns>A hash code consistent with <see cref="Equals(BinaryValue)"/>.</returns>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_bits);

    /// <summary>Returns the exact retained bit sequence.</summary>
    /// <returns>The bits without ISO 10303-21 quote or unused-bit encoding.</returns>
    public override string ToString() => _bits;
}