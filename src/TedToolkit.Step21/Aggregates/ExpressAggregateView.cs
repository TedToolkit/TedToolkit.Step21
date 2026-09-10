using System.Collections;
using System.ComponentModel;

namespace TedToolkit.Step21;

/// <summary>Creates live read-only projections over EXPRESS aggregate storage.</summary>
/// <remarks>
/// This type supports generated schema code. Each projection retains the source aggregate's metadata and observes
/// subsequent source mutations without copying its elements.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ExpressAggregateView
{
    /// <summary>Projects an EXPRESS <c>ARRAY</c> without copying its storage.</summary>
    /// <typeparam name="TSource">The stored slot type.</typeparam>
    /// <typeparam name="TResult">The projected slot type.</typeparam>
    /// <param name="source">The live source view.</param>
    /// <param name="projector">The lossless element projection.</param>
    /// <returns>A live read-only projected view.</returns>
    public static IExpressArray<TResult> Project<TSource, TResult>(
        IExpressArray<TSource> source,
        Func<TSource, TResult> projector)
    {
        Guard.NotNull(source);
        Guard.NotNull(projector);
        return new ArrayProjection<TSource, TResult>(source, projector);
    }

    /// <summary>Projects an EXPRESS <c>LIST</c> without copying its storage.</summary>
    /// <typeparam name="TSource">The stored element type.</typeparam>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="source">The live source view.</param>
    /// <param name="projector">The lossless element projection.</param>
    /// <returns>A live read-only projected view.</returns>
    public static IExpressList<TResult> Project<TSource, TResult>(
        IExpressList<TSource> source,
        Func<TSource, TResult> projector)
    {
        Guard.NotNull(source);
        Guard.NotNull(projector);
        return new ListProjection<TSource, TResult>(source, projector);
    }

    /// <summary>Projects an EXPRESS <c>BAG</c> without copying its storage.</summary>
    /// <typeparam name="TSource">The stored element type.</typeparam>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="source">The live source view.</param>
    /// <param name="projector">The lossless element projection.</param>
    /// <returns>A live read-only projected view.</returns>
    public static IExpressBag<TResult> Project<TSource, TResult>(
        IExpressBag<TSource> source,
        Func<TSource, TResult> projector)
    {
        Guard.NotNull(source);
        Guard.NotNull(projector);
        return new BagProjection<TSource, TResult>(source, projector);
    }

    /// <summary>Projects an EXPRESS <c>SET</c> without copying its storage.</summary>
    /// <typeparam name="TSource">The stored element type.</typeparam>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="source">The live source view.</param>
    /// <param name="projector">The lossless element projection.</param>
    /// <returns>A live read-only projected view.</returns>
    public static IExpressSet<TResult> Project<TSource, TResult>(
        IExpressSet<TSource> source,
        Func<TSource, TResult> projector)
    {
        Guard.NotNull(source);
        Guard.NotNull(projector);
        return new SetProjection<TSource, TResult>(source, projector);
    }

    private sealed class ArrayProjection<TSource, TResult>(
        IExpressArray<TSource> source,
        Func<TSource, TResult> projector) : IExpressArray<TResult>
    {
        public int LowerIndex => source.LowerIndex;

        public int UpperIndex => source.UpperIndex;

        public int Count => source.Count;

        public bool IsOptional => source.IsOptional;

        public bool IsUnique => source.IsUnique;

        public TResult this[int index] => projector(source[index]);

        public bool IsSet(int index) => source.IsSet(index);

        public ValidationResult Validate(string path = "$") => source.Validate(path);

        public IEnumerator<TResult> GetEnumerator() => Enumerate(source, projector).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ListProjection<TSource, TResult>(
        IExpressList<TSource> source,
        Func<TSource, TResult> projector) : IExpressList<TResult>
    {
        public int LowerBound => source.LowerBound;

        public int? UpperBound => source.UpperBound;

        public bool IsUnique => source.IsUnique;

        public int Count => source.Count;

        public TResult this[int index] => projector(source[index]);

        public ValidationResult Validate(string path = "$") => source.Validate(path);

        public IEnumerator<TResult> GetEnumerator() => Enumerate(source, projector).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class BagProjection<TSource, TResult>(
        IExpressBag<TSource> source,
        Func<TSource, TResult> projector) : IExpressBag<TResult>
    {
        public int LowerBound => source.LowerBound;

        public int? UpperBound => source.UpperBound;

        public int Count => source.Count;

        public ValidationResult Validate(string path = "$") => source.Validate(path);

        public IEnumerator<TResult> GetEnumerator() => Enumerate(source, projector).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class SetProjection<TSource, TResult>(
        IExpressSet<TSource> source,
        Func<TSource, TResult> projector) : IExpressSet<TResult>
    {
        public int LowerBound => source.LowerBound;

        public int? UpperBound => source.UpperBound;

        public int Count => source.Count;

        public ValidationResult Validate(string path = "$") => source.Validate(path);

        public IEnumerator<TResult> GetEnumerator() => Enumerate(source, projector).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static IEnumerable<TResult> Enumerate<TSource, TResult>(
        IEnumerable<TSource> source,
        Func<TSource, TResult> projector)
    {
        foreach (var item in source)
            yield return projector(item);
    }
}
