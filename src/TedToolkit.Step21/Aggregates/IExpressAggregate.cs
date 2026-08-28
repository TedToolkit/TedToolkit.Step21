namespace TedToolkit.Step21;

/// <summary>
/// Represents the schema-neutral view accepted by an EXPRESS <c>AGGREGATE OF</c> formal parameter.
/// </summary>
/// <typeparam name="T">The element type exposed by the view.</typeparam>
/// <remarks>
/// The operator properties retain the actual ARRAY, BAG, LIST, or SET semantics of the supplied value.
/// </remarks>
public interface IExpressAggregate<out T> : IReadOnlyCollection<T>
{
    /// <summary>Gets the value produced by the EXPRESS <c>LOBOUND</c> operator.</summary>
    int LowBound { get; }

    /// <summary>Gets the value produced by <c>HIBOUND</c>, or <see langword="null"/> when unbounded.</summary>
    int? HighBound { get; }

    /// <summary>Gets the value produced by the EXPRESS <c>LOINDEX</c> operator.</summary>
    int LowIndex { get; }

    /// <summary>Gets the value produced by the EXPRESS <c>HIINDEX</c> operator.</summary>
    int HighIndex { get; }

    /// <summary>Gets whether the actual aggregate requires distinct assigned values.</summary>
    bool IsUnique { get; }
}
