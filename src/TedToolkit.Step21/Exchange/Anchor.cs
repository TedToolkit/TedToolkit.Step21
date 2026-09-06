using System.Collections.ObjectModel;

namespace TedToolkit.Step21;

/// <summary>Associates one external anchor name with a schema-neutral Part 21 value and ordered tags.</summary>
public sealed class Part21Anchor : IEquatable<Part21Anchor>
{
    /// <summary>Creates an immutable anchor from a stable tag snapshot.</summary>
    public Part21Anchor(AnchorName name, ParameterValue item, IEnumerable<Part21AnchorTag>? tags = null)
    {
        _ = name.Value;
        ArgumentNullException.ThrowIfNull(item);
        ValidateItem(item, nameof(item));
        var snapshot = (tags ?? []).ToArray();
        if (snapshot.Any(tag => tag is null))
            throw new ArgumentException("Anchor tags cannot contain null values.", nameof(tags));

        Name = name;
        Item = item;
        Tags = new ReadOnlyCollection<Part21AnchorTag>(snapshot);
    }

    /// <summary>Gets the external name.</summary>
    public AnchorName Name { get; }

    /// <summary>Gets the anchor item.</summary>
    public ParameterValue Item { get; }

    /// <summary>Gets the ordered tag snapshot.</summary>
    public IReadOnlyList<Part21AnchorTag> Tags { get; }

    /// <inheritdoc />
    public bool Equals(Part21Anchor? other) => other is not null
        && Name.Equals(other.Name)
        && Item.Equals(other.Item)
        && Tags.SequenceEqual(other.Tags);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Part21Anchor);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name);
        hash.Add(Item);
        foreach (var tag in Tags)
            hash.Add(tag);
        return hash.ToHashCode();
    }

    internal static void ValidateItem(ParameterValue item, string parameterName)
    {
        if (item.Kind is ParameterValueKind.Derived or ParameterValueKind.Typed)
            throw new ArgumentException("Derived and typed parameters are not valid anchor items.", parameterName);
        if (item.TryGetAggregate(out var values))
        {
            foreach (var value in values)
                ValidateItem(value, parameterName);
        }
    }
}

/// <summary>Associates schema-external tag information with an anchor.</summary>
public sealed class Part21AnchorTag : IEquatable<Part21AnchorTag>
{
    /// <summary>Creates an anchor tag.</summary>
    public Part21AnchorTag(string name, ParameterValue item)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0
            || name[0] is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z')
            || name.Skip(1).Any(character => character is not (>= 'A' and <= 'Z')
                && character is not (>= 'a' and <= 'z')
                && character is not (>= '0' and <= '9')))
        {
            throw new FormatException("A tag name must start with a Latin letter and contain only Latin letters or digits.");
        }

        ArgumentNullException.ThrowIfNull(item);
        Part21Anchor.ValidateItem(item, nameof(item));
        Name = name;
        Item = item;
    }

    /// <summary>Gets the exact case-sensitive tag name.</summary>
    public string Name { get; }

    /// <summary>Gets the tag item.</summary>
    public ParameterValue Item { get; }

    /// <inheritdoc />
    public bool Equals(Part21AnchorTag? other) => other is not null
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && Item.Equals(other.Item);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Part21AnchorTag);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(StringComparer.Ordinal.GetHashCode(Name), Item);
}