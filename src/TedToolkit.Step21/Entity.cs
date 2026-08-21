namespace TedToolkit.Step21;

/// <summary>
/// Provides the schema-neutral base for generated mutable EXPRESS entity classes.
/// </summary>
/// <remarks>
/// An entity owns neither an ISO 10303-21 instance name nor an exchange-structure or data-section reference.
/// Those associations belong exclusively to each receiving <c>ExchangeStructure</c>.
/// </remarks>
public abstract class Entity
{
    /// <summary>Initializes the schema-neutral base of a generated mutable entity.</summary>
    protected Entity()
    {
    }

    /// <summary>
    /// Gets a live, non-recursive sequence of physical entity-reference occurrences in write order.
    /// </summary>
    /// <remarks>
    /// Generated implementations preserve repeated occurrences and inspect current mutable state without reflection.
    /// Consumers that need a transitive graph must compose this one-level sequence with reference-identity cycle detection.
    /// </remarks>
    public abstract IEnumerable<Entity> DirectReferences { get; }
}
