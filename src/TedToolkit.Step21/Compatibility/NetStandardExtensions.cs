#if NETSTANDARD2_0
using System.Diagnostics.CodeAnalysis;

namespace TedToolkit.Step21;

internal static class NetStandardExtensions
{
    internal static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        where TKey : notnull
    {
        if (dictionary.ContainsKey(key))
            return false;
        dictionary.Add(key, value);
        return true;
    }

    internal static TValue? GetValueOrDefault<TKey, TValue>(
        this IReadOnlyDictionary<TKey, TValue> dictionary,
        TKey key)
        where TKey : notnull => dictionary.TryGetValue(key, out var value) ? value : default;

    internal static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source) => new(source);

    internal static HashSet<TSource> ToHashSet<TSource>(
        this IEnumerable<TSource> source,
        IEqualityComparer<TSource>? comparer) => new(source, comparer);

    internal static IEnumerable<TSource> DistinctBy<TSource, TKey>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        IEqualityComparer<TKey>? comparer = null)
    {
        var seen = new HashSet<TKey>(comparer);
        foreach (var item in source)
        {
            if (seen.Add(keySelector(item)))
                yield return item;
        }
    }

    internal static bool TryPop<T>(this Stack<T> stack, [MaybeNullWhen(false)] out T result)
    {
        if (stack.Count == 0)
        {
            result = default;
            return false;
        }
        result = stack.Pop();
        return true;
    }

    internal static bool TryPeek<T>(this Stack<T> stack, [MaybeNullWhen(false)] out T result)
    {
        if (stack.Count == 0)
        {
            result = default;
            return false;
        }
        result = stack.Peek();
        return true;
    }

    internal static bool Contains(this ReadOnlySpan<char> span, char value) => span.IndexOf(value) >= 0;

    internal static bool ContainsAnyExcept(this ReadOnlySpan<char> span, char allowed)
    {
        foreach (var value in span)
        {
            if (value != allowed)
                return true;
        }
        return false;
    }

    internal static bool ContainsAnyExcept(this ReadOnlySpan<char> span, string allowedValues)
    {
        foreach (var value in span)
        {
            if (allowedValues.IndexOf(value) < 0)
                return true;
        }
        return false;
    }

    internal static bool ContainsAnyExceptInRange(this ReadOnlySpan<char> span, char lowInclusive, char highInclusive)
    {
        foreach (var value in span)
        {
            if (value < lowInclusive || value > highInclusive)
                return true;
        }
        return false;
    }
}
#endif
