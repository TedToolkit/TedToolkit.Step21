#if NETSTANDARD2_0
namespace System
{
    internal readonly struct Index : IEquatable<Index>
    {
        private readonly int _value;

        internal Index(int value, bool fromEnd = false)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _value = fromEnd ? ~value : value;
        }

        internal static Index Start => new(0);

        internal static Index End => new(0, fromEnd: true);

        internal int Value => _value < 0 ? ~_value : _value;

        internal bool IsFromEnd => _value < 0;

        internal int GetOffset(int length)
        {
            var offset = _value;
            if (IsFromEnd)
                offset += length + 1;
            return offset;
        }

        public override bool Equals(object? value) => value is Index index && Equals(index);

        public bool Equals(Index other) => _value == other._value;

        public override int GetHashCode() => _value;

        public override string ToString() => IsFromEnd ? $"^{Value}" : Value.ToString();

        public static implicit operator Index(int value) => new(value);
    }

    internal readonly struct Range : IEquatable<Range>
    {
        internal Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        internal Index Start { get; }

        internal Index End { get; }

        internal static Range All => new(Index.Start, Index.End);

        internal static Range StartAt(Index start) => new(start, Index.End);

        internal static Range EndAt(Index end) => new(Index.Start, end);

        internal (int Offset, int Length) GetOffsetAndLength(int length)
        {
            var start = Start.GetOffset(length);
            var end = End.GetOffset(length);
            if ((uint)end > (uint)length || (uint)start > (uint)end)
                throw new ArgumentOutOfRangeException(nameof(length));
            return (start, end - start);
        }

        public override bool Equals(object? value) => value is Range range && Equals(range);

        public bool Equals(Range other) => Start.Equals(other.Start) && End.Equals(other.End);

        public override int GetHashCode() => unchecked((Start.GetHashCode() * 397) ^ End.GetHashCode());

        public override string ToString() => $"{Start}..{End}";
    }
}

#endif
