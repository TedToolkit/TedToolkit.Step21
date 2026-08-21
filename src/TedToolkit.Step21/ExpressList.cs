using System.Collections;

namespace TedToolkit.Step21;

/// <summary>
/// Represents a mutable schema-neutral EXPRESS <c>LIST</c> that retains order and multiplicity.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// Mutations accept temporary bound and <c>UNIQUE</c> violations and never run validation.
/// Call <see cref="Validate(string)"/> explicitly or rely on a later model boundary to inspect the candidate.
/// </remarks>
public sealed class ExpressList<T> : IList<T>, IReadOnlyList<T>
{
    private readonly List<T> _items = [];
    private readonly IEqualityComparer<T> _comparer;

    /// <summary>
    /// Creates an empty mutable EXPRESS <c>LIST</c> candidate.
    /// </summary>
    /// <param name="lowerBound">The inclusive minimum permitted cardinality.</param>
    /// <param name="upperBound">The inclusive maximum permitted cardinality, or <see langword="null"/> when unbounded.</param>
    /// <param name="isUnique">Whether explicit validation requires distinct element values.</param>
    /// <param name="comparer">The equality comparer used for <c>UNIQUE</c> validation, or <see langword="null"/> for the default comparer.</param>
    /// <exception cref="ArgumentOutOfRangeException">The declared bounds are inconsistent.</exception>
    public ExpressList(
        int lowerBound = 0,
        int? upperBound = null,
        bool isUnique = false,
        IEqualityComparer<T>? comparer = null)
    {
        ExpressAggregateValidation.ValidateVariableBounds(lowerBound, upperBound);
        LowerBound = lowerBound;
        UpperBound = upperBound;
        IsUnique = isUnique;
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    /// <summary>Gets the inclusive minimum permitted cardinality.</summary>
    public int LowerBound { get; }

    /// <summary>Gets the inclusive maximum permitted cardinality, or <see langword="null"/> when unbounded.</summary>
    public int? UpperBound { get; }

    /// <summary>Gets whether explicit validation requires distinct element values.</summary>
    public bool IsUnique { get; }

    /// <summary>Gets or sets the element at the zero-based list position without running validation.</summary>
    /// <param name="index">The zero-based list position.</param>
    /// <returns>The element at <paramref name="index"/>.</returns>
    public T this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    /// <summary>Gets the current candidate cardinality.</summary>
    public int Count => _items.Count;

    /// <summary>Gets <see langword="false"/> because this candidate is mutable.</summary>
    public bool IsReadOnly => false;

    /// <summary>Adds an element without running validation.</summary>
    /// <param name="item">The element to add.</param>
    public void Add(T item) => _items.Add(item);

    /// <summary>Removes every candidate element without running validation.</summary>
    public void Clear() => _items.Clear();

    /// <summary>Determines whether the current candidate contains an element.</summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><see langword="true"/> when the element is present.</returns>
    public bool Contains(T item) => _items.Contains(item);

    /// <summary>Copies candidate elements to an array in list order.</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based destination offset.</param>
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <summary>Returns an enumerator over candidate elements in list order.</summary>
    /// <returns>An element enumerator.</returns>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <summary>Returns the zero-based position of an element.</summary>
    /// <param name="item">The element to locate.</param>
    /// <returns>The zero-based position, or <c>-1</c> when absent.</returns>
    public int IndexOf(T item) => _items.IndexOf(item);

    /// <summary>Inserts an element at a zero-based position without running validation.</summary>
    /// <param name="index">The insertion position.</param>
    /// <param name="item">The element to insert.</param>
    public void Insert(int index, T item) => _items.Insert(index, item);

    /// <summary>Removes the first matching element without running validation.</summary>
    /// <param name="item">The element to remove.</param>
    /// <returns><see langword="true"/> when an element was removed.</returns>
    public bool Remove(T item) => _items.Remove(item);

    /// <summary>Removes the element at a zero-based position without running validation.</summary>
    /// <param name="index">The removal position.</param>
    public void RemoveAt(int index) => _items.RemoveAt(index);

    /// <summary>Validates current bounds and optional <c>UNIQUE</c> semantics without mutation.</summary>
    /// <param name="path">The deterministic caller path assigned to produced failures.</param>
    /// <returns>Every detected aggregate failure in deterministic order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public ValidationResult Validate(string path = "$")
    {
        ArgumentNullException.ThrowIfNull(path);
        var failures = ExpressAggregateValidation.ValidateBounds(Count, LowerBound, UpperBound, path);
        if (IsUnique)
            ExpressAggregateValidation.AddUniquenessFailure(_items, _comparer, path, failures);
        return new ValidationResult(failures);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}