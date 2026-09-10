namespace TedToolkit.Step21;

/// <summary>Identifies one entity type in one EXPRESS schema for domain-equivalence declarations.</summary>
public readonly struct SchemaEntityType : IEquatable<SchemaEntityType>
{
    private readonly string? _entityName;

    /// <summary>Creates a schema-qualified entity-type identity.</summary>
    public SchemaEntityType(SchemaName schemaName, string entityName)
    {
        _ = schemaName.Value;
        Guard.NullOrWhiteSpace(entityName);
        SchemaName = schemaName;
        _entityName = entityName;
    }

    /// <summary>Gets the schema name.</summary>
    public SchemaName SchemaName { get; }

    /// <summary>Gets the retained entity-type spelling.</summary>
    public string EntityName => _entityName
        ?? throw new InvalidOperationException("The default SchemaEntityType value is invalid.");

    /// <summary>Determines whether both schema and entity names are equal without case significance.</summary>
    public bool Equals(SchemaEntityType other) =>
        string.Equals(SchemaName.Value, other.SchemaName.Value, StringComparison.OrdinalIgnoreCase)
        && string.Equals(_entityName, other._entityName, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SchemaEntityType other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(
        StringComparer.OrdinalIgnoreCase.GetHashCode(SchemaName.Value),
        StringComparer.OrdinalIgnoreCase.GetHashCode(_entityName ?? string.Empty));

    /// <inheritdoc/>
    public override string ToString() => $"{SchemaName.Value}.{EntityName}";
}

/// <summary>Declares one directed SDAI domain-equivalence relation supplied by the caller.</summary>
public sealed class SchemaDomainEquivalence
{
    /// <summary>Creates one directed relation; callers must also supply its symmetric and transitive closure.</summary>
    public SchemaDomainEquivalence(SchemaEntityType source, SchemaEntityType target)
    {
        _ = source.EntityName;
        _ = target.EntityName;
        Source = source;
        Target = target;
    }

    /// <summary>Gets the source entity type.</summary>
    public SchemaEntityType Source { get; }

    /// <summary>Gets the target entity type.</summary>
    public SchemaEntityType Target { get; }
}

/// <summary>
/// Supplies an explicit SDAI domain-equivalence relation and schema-neutral physical-parameter projections.
/// </summary>
public interface ISchemaDomainEquivalenceProvider
{
    /// <summary>Gets the complete symmetric and transitive domain-equivalence relation.</summary>
    IReadOnlyCollection<SchemaDomainEquivalence> GetEquivalences();

    /// <summary>
    /// Projects one source entity component's physical parameters into its declared equivalent target component.
    /// </summary>
    /// <remarks>
    /// Returned entity references must originate in <paramref name="sourceParameters"/>. The runtime retains
    /// ownership of target entity allocation, canonical occurrence identity, and recursive entity projection.
    /// </remarks>
    IReadOnlyList<ParameterValue> ProjectParameters(
        SchemaEntityType source,
        SchemaEntityType target,
        IReadOnlyList<ParameterValue> sourceParameters);
}
