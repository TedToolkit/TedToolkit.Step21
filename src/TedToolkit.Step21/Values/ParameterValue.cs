using System.Collections.ObjectModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace TedToolkit.Step21;

/// <summary>
/// Represents one schema-neutral ISO 10303-21 parameter without exposing parser syntax or an untyped payload.
/// </summary>
/// <remarks>
/// Aggregate values recursively contain <see cref="ParameterValue"/> instances. Entity values retain the resolved
/// runtime <see cref="Entity"/> by reference identity. Omitted and derived markers remain distinct singleton values.
/// </remarks>
public sealed class ParameterValue : IEquatable<ParameterValue>
{
    private static readonly ParameterValue _omitted = new(ParameterValueKind.Omitted);
    private static readonly ParameterValue _derived = new(ParameterValueKind.Derived);

    private readonly BigInteger _integer;
    private readonly RealValue _real;
    private readonly string? _text;
    private readonly BinaryValue? _binary;
    private readonly bool _boolean;
    private readonly LogicalValue _logical;
    private readonly Entity? _entity;
    private readonly ReadOnlyCollection<ParameterValue>? _aggregate;
    private readonly ParameterValue? _inner;
    private readonly EntityInstanceName _entityInstanceName;
    private readonly ValueInstanceName _valueInstanceName;
    private readonly ConstantEntityName _constantEntityName;
    private readonly ConstantValueName _constantValueName;
    private readonly Part21Resource _resource;

    private ParameterValue(
        ParameterValueKind kind,
        BigInteger integer = default,
        RealValue real = default,
        string? text = null,
        BinaryValue? binary = null,
        bool boolean = false,
        LogicalValue logical = default,
        Entity? entity = null,
        ReadOnlyCollection<ParameterValue>? aggregate = null,
        ParameterValue? inner = null,
        EntityInstanceName entityInstanceName = default,
        ValueInstanceName valueInstanceName = default,
        ConstantEntityName constantEntityName = default,
        ConstantValueName constantValueName = default,
        Part21Resource resource = default)
    {
        Kind = kind;
        _integer = integer;
        _real = real;
        _text = text;
        _binary = binary;
        _boolean = boolean;
        _logical = logical;
        _entity = entity;
        _aggregate = aggregate;
        _inner = inner;
        _entityInstanceName = entityInstanceName;
        _valueInstanceName = valueInstanceName;
        _constantEntityName = constantEntityName;
        _constantValueName = constantValueName;
        _resource = resource;
    }

    /// <summary>Gets the explicit OPTIONAL absence marker.</summary>
    public static ParameterValue Omitted => _omitted;

    /// <summary>Gets the explicit derived attribute marker.</summary>
    public static ParameterValue Derived => _derived;

    /// <summary>Gets this parameter's strongly identified alternative.</summary>
    public ParameterValueKind Kind { get; }

    /// <summary>Creates an arbitrary-precision INTEGER parameter.</summary>
    /// <param name="value">The exact integer.</param>
    /// <returns>An INTEGER parameter.</returns>
    public static ParameterValue FromInteger(BigInteger value) => new(ParameterValueKind.Integer, integer: value);

    /// <summary>Creates an exact finite REAL parameter.</summary>
    /// <param name="value">The exact decimal.</param>
    /// <returns>A REAL parameter.</returns>
    public static ParameterValue FromReal(RealValue value) => new(ParameterValueKind.Real, real: value);

    /// <summary>Creates a decoded STRING parameter.</summary>
    /// <param name="value">The decoded string, which may be empty.</param>
    /// <returns>A STRING parameter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static ParameterValue FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(ParameterValueKind.String, text: value);
    }

    /// <summary>Creates a bit-accurate BINARY parameter.</summary>
    /// <param name="value">The immutable binary value.</param>
    /// <returns>A BINARY parameter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static ParameterValue FromBinary(BinaryValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(ParameterValueKind.Binary, binary: value);
    }

    /// <summary>Creates a two-state BOOLEAN parameter.</summary>
    /// <param name="value">The Boolean state.</param>
    /// <returns>A BOOLEAN parameter.</returns>
    public static ParameterValue FromBoolean(bool value) => new(ParameterValueKind.Boolean, boolean: value);

    /// <summary>Creates a three-state LOGICAL parameter.</summary>
    /// <param name="value">The logical state.</param>
    /// <returns>A LOGICAL parameter.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not a declared logical state.</exception>
    public static ParameterValue FromLogical(LogicalValue value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "The value must be a declared LOGICAL state.");

        return new(ParameterValueKind.Logical, logical: value);
    }

    /// <summary>Creates an untyped enumeration-symbol parameter.</summary>
    /// <param name="symbol">The canonical upper-case EXPRESS symbol without surrounding periods.</param>
    /// <returns>An enumeration parameter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="symbol"/> is not a legal canonical symbol.</exception>
    public static ParameterValue FromEnumeration(string symbol)
    {
        ValidateName(symbol, nameof(symbol));
        return new(ParameterValueKind.Enumeration, text: symbol);
    }

    /// <summary>Creates a parameter that retains a resolved runtime entity occurrence.</summary>
    /// <param name="value">The resolved entity.</param>
    /// <returns>An entity parameter retaining <paramref name="value"/> by reference identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static ParameterValue FromEntity(Entity value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(ParameterValueKind.Entity, entity: value);
    }

    /// <summary>Creates an unresolved entity instance occurrence.</summary>
    public static ParameterValue FromEntityInstance(EntityInstanceName name)
    {
        _ = name.CanonicalDigits;
        return new(ParameterValueKind.EntityInstance, entityInstanceName: name);
    }

    /// <summary>Creates a value instance occurrence.</summary>
    public static ParameterValue FromValueInstance(ValueInstanceName name)
    {
        _ = name.CanonicalDigits;
        return new(ParameterValueKind.ValueInstance, valueInstanceName: name);
    }

    /// <summary>Creates an EXPRESS constant entity occurrence.</summary>
    public static ParameterValue FromConstantEntity(ConstantEntityName name)
    {
        _ = name.Value;
        return new(ParameterValueKind.ConstantEntity, constantEntityName: name);
    }

    /// <summary>Creates an EXPRESS constant value occurrence.</summary>
    public static ParameterValue FromConstantValue(ConstantValueName name)
    {
        _ = name.Value;
        return new(ParameterValueKind.ConstantValue, constantValueName: name);
    }

    /// <summary>Creates a URI-addressed Part 21 resource value.</summary>
    public static ParameterValue FromResource(Part21Resource resource)
    {
        _ = resource.Value;
        return new(ParameterValueKind.Resource, resource: resource);
    }

    /// <summary>Creates a recursive aggregate parameter from a stable snapshot.</summary>
    /// <param name="values">The parameter elements in physical order.</param>
    /// <returns>An aggregate parameter owning a snapshot of the supplied sequence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="values"/> contains <see langword="null"/>.</exception>
    public static ParameterValue FromAggregate(IEnumerable<ParameterValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var snapshot = values.ToArray();
        if (snapshot.Any(value => value is null))
            throw new ArgumentException("An aggregate parameter cannot contain null elements.", nameof(values));

        return new(ParameterValueKind.Aggregate, aggregate: Array.AsReadOnly(snapshot));
    }

    /// <summary>Creates a keyword-qualified typed parameter.</summary>
    /// <param name="typeName">The canonical upper-case EXPRESS type keyword.</param>
    /// <param name="value">The nested parameter.</param>
    /// <returns>A typed parameter.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="typeName"/> is not a legal canonical name.</exception>
    public static ParameterValue FromTyped(string typeName, ParameterValue value)
    {
        ValidateName(typeName, nameof(typeName));
        ArgumentNullException.ThrowIfNull(value);
        return new(ParameterValueKind.Typed, text: typeName, inner: value);
    }

    /// <summary>Attempts to obtain the INTEGER alternative.</summary>
    /// <param name="value">Receives the exact integer, or zero for another alternative.</param>
    /// <returns><see langword="true"/> when this is an INTEGER parameter.</returns>
    public bool TryGetInteger(out BigInteger value) => TryGet(ParameterValueKind.Integer, _integer, out value);

    /// <summary>Attempts to obtain the REAL alternative.</summary>
    /// <param name="value">Receives the exact real, or zero for another alternative.</param>
    /// <returns><see langword="true"/> when this is a REAL parameter.</returns>
    public bool TryGetReal(out RealValue value) => TryGet(ParameterValueKind.Real, _real, out value);

    /// <summary>Attempts to obtain the STRING alternative.</summary>
    /// <param name="value">Receives the decoded string, or <see langword="null"/> for another alternative.</param>
    /// <returns><see langword="true"/> when this is a STRING parameter.</returns>
    public bool TryGetString([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value) =>
        TryGetReference(ParameterValueKind.String, _text, out value);

    /// <summary>Attempts to obtain the BINARY alternative.</summary>
    /// <param name="value">Receives the binary value, or <see langword="null"/> for another alternative.</param>
    /// <returns><see langword="true"/> when this is a BINARY parameter.</returns>
    public bool TryGetBinary([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BinaryValue? value) =>
        TryGetReference(ParameterValueKind.Binary, _binary, out value);

    /// <summary>Attempts to obtain the BOOLEAN alternative.</summary>
    /// <param name="value">Receives the Boolean state, or <see langword="false"/> for another alternative.</param>
    /// <returns><see langword="true"/> when this is a BOOLEAN parameter.</returns>
    public bool TryGetBoolean(out bool value) => TryGet(ParameterValueKind.Boolean, _boolean, out value);

    /// <summary>Attempts to obtain the LOGICAL alternative.</summary>
    /// <param name="value">Receives the logical state, or <see cref="LogicalValue.False"/> for another alternative.</param>
    /// <returns><see langword="true"/> when this is a LOGICAL parameter.</returns>
    public bool TryGetLogical(out LogicalValue value) => TryGet(ParameterValueKind.Logical, _logical, out value);

    /// <summary>Attempts to obtain the enumeration-symbol alternative.</summary>
    /// <param name="symbol">Receives the canonical symbol, or <see langword="null"/> for another alternative.</param>
    /// <returns><see langword="true"/> when this is an enumeration parameter.</returns>
    public bool TryGetEnumeration([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? symbol) =>
        TryGetReference(ParameterValueKind.Enumeration, _text, out symbol);

    /// <summary>Attempts to obtain the resolved entity alternative.</summary>
    /// <param name="value">Receives the resolved entity, or <see langword="null"/> for another alternative.</param>
    /// <returns><see langword="true"/> when this is an entity parameter.</returns>
    public bool TryGetEntity([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Entity? value) =>
        TryGetReference(ParameterValueKind.Entity, _entity, out value);

    /// <summary>Attempts to obtain an unresolved entity instance occurrence.</summary>
    public bool TryGetEntityInstance(out EntityInstanceName name) =>
        TryGet(ParameterValueKind.EntityInstance, _entityInstanceName, out name);

    /// <summary>Attempts to obtain a value instance occurrence.</summary>
    public bool TryGetValueInstance(out ValueInstanceName name) =>
        TryGet(ParameterValueKind.ValueInstance, _valueInstanceName, out name);

    /// <summary>Attempts to obtain an EXPRESS constant entity occurrence.</summary>
    public bool TryGetConstantEntity(out ConstantEntityName name) =>
        TryGet(ParameterValueKind.ConstantEntity, _constantEntityName, out name);

    /// <summary>Attempts to obtain an EXPRESS constant value occurrence.</summary>
    public bool TryGetConstantValue(out ConstantValueName name) =>
        TryGet(ParameterValueKind.ConstantValue, _constantValueName, out name);

    /// <summary>Attempts to obtain a resource URI reference.</summary>
    public bool TryGetResource(out Part21Resource resource) =>
        TryGet(ParameterValueKind.Resource, _resource, out resource);

    /// <summary>Attempts to obtain the recursive aggregate alternative.</summary>
    /// <param name="values">Receives the stable element snapshot, or <see langword="null"/> for another alternative.</param>
    /// <returns><see langword="true"/> when this is an aggregate parameter.</returns>
    public bool TryGetAggregate(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IReadOnlyList<ParameterValue>? values) =>
        TryGetReference(ParameterValueKind.Aggregate, _aggregate, out values);

    /// <summary>Attempts to obtain the typed-parameter alternative.</summary>
    /// <param name="typeName">Receives the canonical type name, or <see langword="null"/> for another alternative.</param>
    /// <param name="value">Receives the nested parameter, or <see langword="null"/> for another alternative.</param>
    /// <returns><see langword="true"/> when this is a typed parameter.</returns>
    public bool TryGetTyped(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? typeName,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ParameterValue? value)
    {
        typeName = Kind == ParameterValueKind.Typed ? _text : null;
        value = Kind == ParameterValueKind.Typed ? _inner : null;
        return Kind == ParameterValueKind.Typed;
    }

    /// <summary>Determines whether another parameter retains the same alternative and value.</summary>
    /// <param name="other">The other parameter.</param>
    /// <returns><see langword="true"/> when both parameter values are equal.</returns>
    public bool Equals(ParameterValue? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null || Kind != other.Kind)
            return false;

        return Kind switch
        {
            ParameterValueKind.Omitted or ParameterValueKind.Derived => true,
            ParameterValueKind.Integer => _integer == other._integer,
            ParameterValueKind.Real => _real == other._real,
            ParameterValueKind.String or ParameterValueKind.Enumeration =>
                string.Equals(_text, other._text, StringComparison.Ordinal),
            ParameterValueKind.Binary => Equals(_binary, other._binary),
            ParameterValueKind.Boolean => _boolean == other._boolean,
            ParameterValueKind.Logical => _logical == other._logical,
            ParameterValueKind.Entity => ReferenceEquals(_entity, other._entity),
            ParameterValueKind.EntityInstance => _entityInstanceName.Equals(other._entityInstanceName),
            ParameterValueKind.ValueInstance => _valueInstanceName.Equals(other._valueInstanceName),
            ParameterValueKind.ConstantEntity => _constantEntityName.Equals(other._constantEntityName),
            ParameterValueKind.ConstantValue => _constantValueName.Equals(other._constantValueName),
            ParameterValueKind.Resource => _resource.Equals(other._resource),
            ParameterValueKind.Aggregate => _aggregate!.SequenceEqual(other._aggregate!),
            ParameterValueKind.Typed => string.Equals(_text, other._text, StringComparison.Ordinal)
                && Equals(_inner, other._inner),
            _ => false,
        };
    }

    /// <summary>Determines whether an object is an equal parameter value.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal parameter value.</returns>
    public override bool Equals(object? obj) => Equals(obj as ParameterValue);

    /// <summary>Returns a hash code for the retained alternative and value.</summary>
    /// <returns>A hash code consistent with <see cref="Equals(ParameterValue)"/>.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        switch (Kind)
        {
            case ParameterValueKind.Integer:
                hash.Add(_integer);
                break;
            case ParameterValueKind.Real:
                hash.Add(_real);
                break;
            case ParameterValueKind.String:
            case ParameterValueKind.Enumeration:
                hash.Add(_text, StringComparer.Ordinal);
                break;
            case ParameterValueKind.Binary:
                hash.Add(_binary);
                break;
            case ParameterValueKind.Boolean:
                hash.Add(_boolean);
                break;
            case ParameterValueKind.Logical:
                hash.Add(_logical);
                break;
            case ParameterValueKind.Entity:
                hash.Add(_entity is null ? 0 : RuntimeHelpers.GetHashCode(_entity));
                break;
            case ParameterValueKind.EntityInstance:
                hash.Add(_entityInstanceName);
                break;
            case ParameterValueKind.ValueInstance:
                hash.Add(_valueInstanceName);
                break;
            case ParameterValueKind.ConstantEntity:
                hash.Add(_constantEntityName);
                break;
            case ParameterValueKind.ConstantValue:
                hash.Add(_constantValueName);
                break;
            case ParameterValueKind.Resource:
                hash.Add(_resource);
                break;
            case ParameterValueKind.Aggregate:
                foreach (var value in _aggregate!)
                    hash.Add(value);
                break;
            case ParameterValueKind.Typed:
                hash.Add(_text, StringComparer.Ordinal);
                hash.Add(_inner);
                break;
        }

        return hash.ToHashCode();
    }

    private bool TryGet<T>(ParameterValueKind expected, T retained, out T value)
        where T : struct
    {
        value = Kind == expected ? retained : default;
        return Kind == expected;
    }

    private bool TryGetReference<T>(
        ParameterValueKind expected,
        T? retained,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? value)
        where T : class
    {
        value = Kind == expected ? retained : null;
        return Kind == expected;
    }

    private static void ValidateName(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length == 0
            || value[0] is < 'A' or > 'Z'
            || value.Skip(1).Any(character => character is not (>= 'A' and <= 'Z')
                && character is not (>= '0' and <= '9')
                && character != '_'))
        {
            throw new FormatException("A canonical EXPRESS name must start with A-Z and contain only A-Z, 0-9, or '_'.");
        }
    }
}