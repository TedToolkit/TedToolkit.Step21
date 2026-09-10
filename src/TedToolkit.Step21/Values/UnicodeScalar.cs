using System.Buffers;

namespace TedToolkit.Step21;

// Keeps canonical string encoding independent of the target framework's Rune availability.
internal readonly struct UnicodeScalar
{
    private UnicodeScalar(int value) => Value = value;

    internal int Value { get; }

    internal int Utf16SequenceLength => Value <= 0xFFFF ? 1 : 2;

    internal int Utf8SequenceLength => Value switch
    {
        <= 0x7F => 1,
        <= 0x7FF => 2,
        <= 0xFFFF => 3,
        _ => 4,
    };

    internal int EncodeToUtf16(Span<char> destination)
    {
        if (Value <= 0xFFFF)
        {
            destination[0] = (char)Value;
            return 1;
        }

        var scalar = Value - 0x10000;
        destination[0] = (char)(0xD800 + (scalar >> 10));
        destination[1] = (char)(0xDC00 + (scalar & 0x3FF));
        return 2;
    }

    internal static OperationStatus DecodeFromUtf16(
        ReadOnlySpan<char> source,
        out UnicodeScalar scalar,
        out int consumed)
    {
        scalar = default;
        consumed = 0;
        if (source.IsEmpty)
            return OperationStatus.NeedMoreData;

        var first = source[0];
        if (!char.IsSurrogate(first))
        {
            scalar = new(first);
            consumed = 1;
            return OperationStatus.Done;
        }

        if (!char.IsHighSurrogate(first))
            return OperationStatus.InvalidData;
        if (source.Length < 2)
            return OperationStatus.NeedMoreData;

        var second = source[1];
        if (!char.IsLowSurrogate(second))
            return OperationStatus.InvalidData;

        scalar = new(0x10000 + ((first - 0xD800) << 10) + second - 0xDC00);
        consumed = 2;
        return OperationStatus.Done;
    }
}
