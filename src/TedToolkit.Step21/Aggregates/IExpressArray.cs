namespace TedToolkit.Step21;

/// <summary>
/// Represents a covariant, read-only view of an EXPRESS <c>ARRAY</c> and its declared metadata.
/// </summary>
/// <typeparam name="T">The slot value type exposed by the view.</typeparam>
/// <remarks>The view does not copy storage; a compatible <see cref="ExpressArray{T}"/> implements it directly.</remarks>
public interface IExpressArray<out T> : IExpressAggregate<T>
{
    /// <summary>Gets the inclusive lower EXPRESS index.</summary>
    int LowerIndex { get; }

    /// <summary>Gets the inclusive upper EXPRESS index.</summary>
    int UpperIndex { get; }

    /// <summary>Gets whether unset slots are permitted by the declaration.</summary>
    bool IsOptional { get; }

    int IExpressAggregate<T>.LowBound => LowerIndex;

    int? IExpressAggregate<T>.HighBound => UpperIndex;

    int IExpressAggregate<T>.LowIndex => LowerIndex;

    int IExpressAggregate<T>.HighIndex => UpperIndex;

    /// <summary>Gets one assigned value by its declared EXPRESS index.</summary>
    /// <param name="index">The declared EXPRESS index.</param>
    /// <returns>The assigned value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the declared domain.</exception>
    /// <exception cref="InvalidOperationException">The requested slot is currently unset.</exception>
    T this[int index] { get; }

    /// <summary>Determines whether a declared slot currently has an assigned value.</summary>
    /// <param name="index">The declared EXPRESS index.</param>
    /// <returns><see langword="true"/> when the slot is assigned.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the declared domain.</exception>
    bool IsSet(int index);

    /// <summary>Validates the visible candidate state without mutation.</summary>
    /// <param name="path">The deterministic caller path assigned to produced failures.</param>
    /// <returns>Every detected slot and uniqueness failure in deterministic index order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <example><code>ValidationResult result = view.Validate("ENTITY.Items");</code></example>
    ValidationResult Validate(string path = "$");
}
