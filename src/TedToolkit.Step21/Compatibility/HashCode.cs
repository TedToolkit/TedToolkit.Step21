#if NETSTANDARD2_0
namespace System;

internal struct HashCode
{
    private int _value;

    internal void Add<T>(T value) => Add(value, EqualityComparer<T>.Default);

    internal void Add<T>(T value, IEqualityComparer<T>? comparer)
    {
        comparer ??= EqualityComparer<T>.Default;
        _value = unchecked((_value * 31) + (value is null ? 0 : comparer.GetHashCode(value)));
    }

    internal int ToHashCode() => _value;

    internal static int Combine<T1, T2>(T1 value1, T2 value2)
    {
        var hash = new HashCode();
        hash.Add(value1);
        hash.Add(value2);
        return hash.ToHashCode();
    }

    internal static int Combine<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
    {
        var hash = new HashCode();
        hash.Add(value1);
        hash.Add(value2);
        hash.Add(value3);
        return hash.ToHashCode();
    }
}
#endif
