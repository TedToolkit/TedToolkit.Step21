namespace TedToolkit.Step21;

/// <summary>Identifies the local occurrence category defined by a Part 21 reference entry.</summary>
public enum Part21ReferenceKind
{
    /// <summary>An entity instance occurrence.</summary>
    EntityInstance = 0,

    /// <summary>A value instance occurrence.</summary>
    ValueInstance = 1,
}

/// <summary>Associates one local occurrence name with a caller-resolvable resource URI.</summary>
public sealed class Part21Reference : IEquatable<Part21Reference>
{
    private readonly EntityInstanceName _entityName;
    private readonly ValueInstanceName _valueName;

    /// <summary>Creates an external entity instance reference.</summary>
    public Part21Reference(EntityInstanceName name, Part21Resource resource)
    {
        _ = name.CanonicalDigits;
        _ = resource.Value;
        Kind = Part21ReferenceKind.EntityInstance;
        _entityName = name;
        Resource = resource;
    }

    /// <summary>Creates an external value instance reference.</summary>
    public Part21Reference(ValueInstanceName name, Part21Resource resource)
    {
        _ = name.CanonicalDigits;
        _ = resource.Value;
        Kind = Part21ReferenceKind.ValueInstance;
        _valueName = name;
        Resource = resource;
    }

    /// <summary>Gets the occurrence category.</summary>
    public Part21ReferenceKind Kind { get; }

    /// <summary>Gets the exact resource URI reference.</summary>
    public Part21Resource Resource { get; }

    /// <summary>Attempts to obtain the entity instance name.</summary>
    public bool TryGetEntityInstance(out EntityInstanceName name)
    {
        name = Kind == Part21ReferenceKind.EntityInstance ? _entityName : default;
        return Kind == Part21ReferenceKind.EntityInstance;
    }

    /// <summary>Attempts to obtain the value instance name.</summary>
    public bool TryGetValueInstance(out ValueInstanceName name)
    {
        name = Kind == Part21ReferenceKind.ValueInstance ? _valueName : default;
        return Kind == Part21ReferenceKind.ValueInstance;
    }

    /// <inheritdoc />
    public bool Equals(Part21Reference? other) => other is not null
        && Kind == other.Kind
        && Resource.Equals(other.Resource)
        && (Kind == Part21ReferenceKind.EntityInstance
            ? _entityName.Equals(other._entityName)
            : _valueName.Equals(other._valueName));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Part21Reference);

    /// <inheritdoc />
    public override int GetHashCode() => Kind == Part21ReferenceKind.EntityInstance
        ? HashCode.Combine(Kind, _entityName, Resource)
        : HashCode.Combine(Kind, _valueName, Resource);

    internal string CanonicalDigits => Kind == Part21ReferenceKind.EntityInstance
        ? _entityName.CanonicalDigits
        : _valueName.CanonicalDigits;

    internal string FormatName() => Kind == Part21ReferenceKind.EntityInstance
        ? _entityName.ToString()
        : _valueName.ToString();
}