using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace TedToolkit.Step21;

/// <summary>
/// Represents a mutable schema-neutral EXPRESS <c>ARRAY</c> with a fixed declared index domain.
/// </summary>
/// <typeparam name="T">The slot value type.</typeparam>
/// <remarks>
/// Assignment and <see cref="Unset(int)"/> never run validation. Unset slots remain observable through
/// <see cref="IsSet(int)"/> and are valid only when <see cref="IsOptional"/> is <see langword="true"/>.
/// </remarks>
public sealed class ExpressArray<T> : IExpressArray<T>
{
    private readonly T?[] _values;
    private readonly bool[] _isSet;
    private readonly IEqualityComparer<T> _comparer;

    /// <summary>Creates an empty mutable EXPRESS <c>ARRAY</c> candidate over a fixed index domain.</summary>
    /// <param name="lowerIndex">The inclusive lower EXPRESS index.</param>
    /// <param name="upperIndex">The inclusive upper EXPRESS index.</param>
    /// <param name="isOptional">Whether unset slots are permitted by the declaration.</param>
    /// <param name="isUnique">Whether assigned slot values must be distinct.</param>
    /// <param name="comparer">The equality comparer used for <c>UNIQUE</c> validation, or <see langword="null"/> for the default comparer.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index domain is empty or too large for a CLR array.</exception>
    public ExpressArray(
        int lowerIndex,
        int upperIndex,
        bool isOptional = false,
        bool isUnique = false,
        IEqualityComparer<T>? comparer = null)
    {
        var length = (long)upperIndex - lowerIndex + 1;
        if (length is <= 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(upperIndex),
                upperIndex,
                "The upper index must define a non-empty CLR-addressable domain.");
        }

        LowerIndex = lowerIndex;
        UpperIndex = upperIndex;
        IsOptional = isOptional;
        IsUnique = isUnique;
        _values = new T?[length];
        _isSet = new bool[length];
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    /// <summary>Creates an independent mutable copy of an existing EXPRESS <c>ARRAY</c> value.</summary>
    /// <param name="source">The source value, including its declared index domain and assigned-slot state.</param>
    /// <param name="comparer">The equality comparer for <c>UNIQUE</c> validation, or <see langword="null"/> to retain a concrete source comparer when available.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public ExpressArray(IExpressArray<T> source, IEqualityComparer<T>? comparer = null)
        : this(
            source?.LowerIndex ?? throw new ArgumentNullException(nameof(source)),
            source.UpperIndex,
            source.IsOptional,
            source.IsUnique,
            comparer ?? (source as ExpressArray<T>)?._comparer)
    {
        for (var offset = 0; offset < Count; offset++)
        {
            var index = checked(LowerIndex + offset);
            if (source.IsSet(index))
            {
                this[index] = source[index];
            }
        }
    }

    /// <summary>Gets the inclusive lower EXPRESS index.</summary>
    public int LowerIndex { get; }

    /// <summary>Gets the inclusive upper EXPRESS index.</summary>
    public int UpperIndex { get; }

    /// <summary>Gets the fixed number of slots in the declared index domain.</summary>
    public int Count => _values.Length;

    /// <summary>Gets whether unset slots are permitted by the declaration.</summary>
    public bool IsOptional { get; }

    /// <summary>Gets whether assigned slot values must be distinct.</summary>
    public bool IsUnique { get; }

    /// <summary>Gets or assigns one declared EXPRESS slot without running validation.</summary>
    /// <param name="index">The declared EXPRESS index.</param>
    /// <returns>The assigned value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the declared domain.</exception>
    /// <exception cref="InvalidOperationException">The requested slot is currently unset.</exception>
    public T this[int index]
    {
        get
        {
            var offset = GetOffset(index);
            if (!_isSet[offset])
                throw new InvalidOperationException($"The EXPRESS ARRAY slot at index {index} is unset.");
            return _values[offset]!;
        }
        set
        {
            var offset = GetOffset(index);
            _values[offset] = value;
            _isSet[offset] = true;
        }
    }

    /// <summary>Determines whether a declared slot currently has an assigned value.</summary>
    /// <param name="index">The declared EXPRESS index.</param>
    /// <returns><see langword="true"/> when the slot is assigned.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the declared domain.</exception>
    public bool IsSet(int index) => _isSet[GetOffset(index)];

    /// <summary>Marks a declared slot as unset without running validation.</summary>
    /// <param name="index">The declared EXPRESS index.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the declared domain.</exception>
    public void Unset(int index)
    {
        var offset = GetOffset(index);
        _values[offset] = default;
        _isSet[offset] = false;
    }

    /// <summary>Attempts to read an assigned slot without treating an unset slot as an exception.</summary>
    /// <param name="index">The declared EXPRESS index.</param>
    /// <param name="value">The assigned value when present, otherwise the default value.</param>
    /// <returns><see langword="true"/> when the slot is assigned.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the declared domain.</exception>
    public bool TryGetValue(int index, [MaybeNullWhen(false)] out T value)
    {
        var offset = GetOffset(index);
        value = _values[offset];
        return _isSet[offset];
    }

    /// <summary>Returns assigned slot values in ascending declared-index order.</summary>
    /// <returns>An enumerator that omits unset slots.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (var offset = 0; offset < _values.Length; offset++)
        {
            if (_isSet[offset])
                yield return _values[offset]!;
        }
    }

    /// <summary>Validates required slots and optional <c>UNIQUE</c> semantics without mutation.</summary>
    /// <param name="path">The deterministic caller path assigned to produced failures.</param>
    /// <returns>Every detected slot and uniqueness failure in deterministic index order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public ValidationResult Validate(string path = "$")
    {
        ArgumentNullException.ThrowIfNull(path);
        var failures = new List<ValidationFailure>();
        var assigned = new List<T>();
        for (var offset = 0; offset < _values.Length; offset++)
        {
            if (_isSet[offset])
            {
                assigned.Add(_values[offset]!);
            }
            else if (!IsOptional)
            {
                var index = LowerIndex + offset;
                failures.Add(
                    new ValidationFailure(
                        ExpressAggregateValidation.RequiredSlotCode,
                        $"{path}[{index}]",
                        $"The required EXPRESS ARRAY slot at index {index} is unset."));
            }
        }

        if (IsUnique)
            ExpressAggregateValidation.AddUniquenessFailure(assigned, _comparer, path, failures);
        return new ValidationResult(failures);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private int GetOffset(int index)
    {
        if (index < LowerIndex || index > UpperIndex)
            throw new ArgumentOutOfRangeException(nameof(index), index, "The index is outside the EXPRESS ARRAY domain.");
        return index - LowerIndex;
    }
}