namespace TedToolkit.Step21;

/// <summary>
/// Defines the minimal identity shared by generated EXPRESS schema descriptors.
/// </summary>
/// <remarks>
/// Descriptor identity is ordinal and is snapshotted by <see cref="ExchangeStructure"/> construction. Runtime
/// operations dispatch through internal non-virtual methods to statically generated protected overrides without
/// reflection or public processing contexts.
/// </remarks>
public abstract class SchemaDescriptor
{
    /// <summary>Initializes the base of a generated schema descriptor.</summary>
    protected SchemaDescriptor()
    {
    }

    /// <summary>Gets the descriptor's stable schema name.</summary>
    public abstract SchemaName Name { get; }

    internal Entity? AllocateEntity(IReadOnlyList<string> entityNames) => AllocateEntityCore(entityNames);

    internal IReadOnlyList<Step21Diagnostic> HydrateEntity(
        ExchangeStructure structure,
        Entity value,
        IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) =>
        HydrateEntityCore(structure, value, components);

    internal ValidationResult Validate(
        ExchangeStructure structure,
        IReadOnlyList<KeyValuePair<string, Entity>> entities) =>
        ValidateCore(structure, entities);

    internal ValidationResult ValidateEntityPopulation(
        ExchangeStructure structure,
        IReadOnlyList<KeyValuePair<string, Entity>> entities) =>
        ValidateEntityPopulationCore(structure, entities);

    internal IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnostics(ExchangeStructure structure) =>
        GetCapabilityDiagnosticsCore(structure);

    internal IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntity(Entity value) =>
        ProjectEntityCore(value);

    internal bool IsEntityReferenceCompatible(Entity value) => IsEntityReferenceCompatibleCore(value);

    /// <summary>Allocates a generated entity for one ordered physical entity-name group.</summary>
    /// <param name="entityNames">The physical entity names in component order.</param>
    /// <returns>The allocated generated entity, or <see langword="null"/> when the names are not supported.</returns>
    protected abstract Entity? AllocateEntityCore(IReadOnlyList<string> entityNames);

    /// <summary>Assigns strong physical component parameters to an allocated generated entity.</summary>
    /// <param name="structure">The structure owning resolved entity references.</param>
    /// <param name="value">The allocated generated entity.</param>
    /// <param name="components">The ordered physical component names and strong parameter lists.</param>
    /// <returns>Every detected hydration diagnostic in deterministic order.</returns>
    protected abstract IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
        ExchangeStructure structure,
        Entity value,
        IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components);

    /// <summary>Validates one ordered schema-owned entity set in an exchange structure.</summary>
    /// <param name="structure">The exchange structure to validate.</param>
    /// <param name="entities">The complete structure paths and registered entities to validate in path order.</param>
    /// <returns>Every detected schema validation failure in deterministic order.</returns>
    protected abstract ValidationResult ValidateCore(
        ExchangeStructure structure,
        IReadOnlyList<KeyValuePair<string, Entity>> entities);

    /// <summary>Validates entity-local and UNIQUE rules without executing this schema's global RULE declarations.</summary>
    /// <param name="structure">The exchange structure containing the schema instance population.</param>
    /// <param name="entities">The schema-owned entities selected into the population.</param>
    /// <returns>Every detected entity-local validation failure in deterministic order.</returns>
    protected virtual ValidationResult ValidateEntityPopulationCore(
        ExchangeStructure structure,
        IReadOnlyList<KeyValuePair<string, Entity>> entities) => ValidateCore(structure, entities);

    /// <summary>Reports unsupported schema operations before a public boundary starts producing output.</summary>
    /// <param name="structure">The exchange structure to inspect.</param>
    /// <returns>Every applicable capability diagnostic in deterministic order.</returns>
    protected abstract IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(ExchangeStructure structure);

    /// <summary>Projects a generated entity to ordered strong physical component parameters.</summary>
    /// <param name="value">The generated entity to project.</param>
    /// <returns>The physical component names and strong parameter lists in standard order.</returns>
    protected abstract IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
        Entity value);

    /// <summary>
    /// Determines whether this schema can reference an entity type through a local declaration or EXPRESS interface.
    /// </summary>
    /// <param name="value">The candidate generated entity.</param>
    /// <returns><see langword="true"/> when the entity type is reference-compatible with this schema.</returns>
    protected virtual bool IsEntityReferenceCompatibleCore(Entity value) => false;
}