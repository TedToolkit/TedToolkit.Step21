using System.Collections;

namespace TedToolkit.Step21;

/// <summary>
/// Represents a mutable schema-neutral EXPRESS <c>BAG</c> that preserves multiplicity.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// Enumeration is deterministic but has no EXPRESS ordering meaning. Mutations accept temporary bound violations and
/// never run validation; call <see cref="Validate(string)"/> explicitly or rely on a later model boundary.
/// </remarks>
public sealed class ExpressBag<T> : ICollection<T>, IReadOnlyCollection<T>
{
    private readonly List<T> _items = [];

    /// <summary>Creates an empty mutable EXPRESS <c>BAG</c> candidate.</summary>
    /// <param name="lowerBound">The inclusive minimum permitted cardinality.</param>
    /// <param name="upperBound">The inclusive maximum permitted cardinality, or <see langword="null"/> when unbounded.</param>
    /// <exception cref="ArgumentOutOfRangeException">The declared bounds are inconsistent.</exception>
    public ExpressBag(int lowerBound = 0, int? upperBound = null)
    {
        ExpressAggregateValidation.ValidateVariableBounds(lowerBound, upperBound);
        LowerBound = lowerBound;
        UpperBound = upperBound;
    }

    /// <summary>Gets the inclusive minimum permitted cardinality.</summary>
    public int LowerBound { get; }

    /// <summary>Gets the inclusive maximum permitted cardinality, or <see langword="null"/> when unbounded.</summary>
    public int? UpperBound { get; }

    /// <summary>Gets the current candidate cardinality, including duplicate occurrences.</summary>
    public int Count => _items.Count;

    /// <summary>Gets <see langword="false"/> because this candidate is mutable.</summary>
    public bool IsReadOnly => false;

    /// <summary>Adds one occurrence without running validation.</summary>
    /// <param name="item">The occurrence to add.</param>
    public void Add(T item) => _items.Add(item);

    /// <summary>Removes every occurrence without running validation.</summary>
    public void Clear() => _items.Clear();

    /// <summary>Determines whether an equal occurrence is present.</summary>
    /// <param name="item">The occurrence to locate.</param>
    /// <returns><see langword="true"/> when present.</returns>
    public bool Contains(T item) => _items.Contains(item);

    /// <summary>Copies occurrences to an array in deterministic enumeration order.</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based destination offset.</param>
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <summary>Returns an enumerator that preserves every occurrence.</summary>
    /// <returns>An occurrence enumerator.</returns>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <summary>Removes the first equal occurrence without running validation.</summary>
    /// <param name="item">The occurrence to remove.</param>
    /// <returns><see langword="true"/> when one occurrence was removed.</returns>
    public bool Remove(T item) => _items.Remove(item);

    /// <summary>Validates current cardinality bounds without mutation.</summary>
    /// <param name="path">The deterministic caller path assigned to produced failures.</param>
    /// <returns>Every detected bound failure in deterministic order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public ValidationResult Validate(string path = "$")
    {
        ArgumentNullException.ThrowIfNull(path);
        return new ValidationResult(ExpressAggregateValidation.ValidateBounds(Count, LowerBound, UpperBound, path));
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}