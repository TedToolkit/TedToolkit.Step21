using System.Collections;

namespace TedToolkit.Step21;

/// <summary>
/// Represents a mutable schema-neutral EXPRESS <c>SET</c> candidate without silently discarding duplicates.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// Enumeration is deterministic but has no EXPRESS ordering meaning. <see cref="Add(T)"/> retains duplicate edit
/// candidates so <see cref="Validate(string)"/> can report the violated set uniqueness rule without mutation.
/// </remarks>
public sealed class ExpressSet<T> : ICollection<T>, IExpressSet<T>
{
    private readonly List<T> _items = [];
    private readonly IEqualityComparer<T> _comparer;

    /// <summary>Creates an empty mutable EXPRESS <c>SET</c> candidate.</summary>
    /// <param name="lowerBound">The inclusive minimum permitted cardinality.</param>
    /// <param name="upperBound">The inclusive maximum permitted cardinality, or <see langword="null"/> when unbounded.</param>
    /// <param name="comparer">The equality comparer used for uniqueness validation, or <see langword="null"/> for the default comparer.</param>
    /// <exception cref="ArgumentOutOfRangeException">The declared bounds are inconsistent.</exception>
    public ExpressSet(
        int lowerBound = 0,
        int? upperBound = null,
        IEqualityComparer<T>? comparer = null)
    {
        ExpressAggregateValidation.ValidateVariableBounds(lowerBound, upperBound);
        LowerBound = lowerBound;
        UpperBound = upperBound;
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    /// <summary>Gets the inclusive minimum permitted cardinality.</summary>
    public int LowerBound { get; }

    /// <summary>Gets the inclusive maximum permitted cardinality, or <see langword="null"/> when unbounded.</summary>
    public int? UpperBound { get; }

    /// <summary>Gets the current candidate cardinality, including duplicate candidates.</summary>
    public int Count => _items.Count;

    /// <summary>Gets <see langword="false"/> because this candidate is mutable.</summary>
    public bool IsReadOnly => false;

    /// <summary>Adds an element candidate without deduplication or validation.</summary>
    /// <param name="item">The candidate to add.</param>
    public void Add(T item) => _items.Add(item);

    /// <summary>Removes every candidate without running validation.</summary>
    public void Clear() => _items.Clear();

    /// <summary>Determines whether an equal candidate is present.</summary>
    /// <param name="item">The candidate to locate.</param>
    /// <returns><see langword="true"/> when present.</returns>
    public bool Contains(T item) => _items.Any(candidate => _comparer.Equals(candidate, item));

    /// <summary>Copies candidates to an array in deterministic enumeration order.</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based destination offset.</param>
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <summary>Returns an enumerator that includes duplicate candidates.</summary>
    /// <returns>A candidate enumerator.</returns>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <summary>Removes the first equal candidate without running validation.</summary>
    /// <param name="item">The candidate to remove.</param>
    /// <returns><see langword="true"/> when one candidate was removed.</returns>
    public bool Remove(T item)
    {
        var index = _items.FindIndex(candidate => _comparer.Equals(candidate, item));
        if (index < 0)
            return false;
        _items.RemoveAt(index);
        return true;
    }

    /// <summary>Validates current bounds and mandatory set uniqueness without mutation.</summary>
    /// <param name="path">The deterministic caller path assigned to produced failures.</param>
    /// <returns>Every detected aggregate failure in deterministic order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public ValidationResult Validate(string path = "$")
    {
        ArgumentNullException.ThrowIfNull(path);
        var failures = ExpressAggregateValidation.ValidateBounds(Count, LowerBound, UpperBound, path);
        ExpressAggregateValidation.AddUniquenessFailure(_items, _comparer, path, failures);
        return new ValidationResult(failures);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}