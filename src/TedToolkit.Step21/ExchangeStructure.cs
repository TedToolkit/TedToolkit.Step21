using System.Collections.ObjectModel;

namespace TedToolkit.Step21;

/// <summary>
/// Owns an editable ISO 10303-21 structure and structure-local entity occurrence identity.
/// </summary>
/// <remarks>
/// Registration uses CLR reference identity, never modifies entities, and never runs schema validation. Each Add
/// traverses the current live one-level <see cref="Entity.DirectReferences"/> graph root-first and depth-first.
/// </remarks>
public sealed class ExchangeStructure
{
    private readonly Dictionary<EntityInstanceName, EntityRegistration> _registrationsByName = [];
    private readonly Dictionary<Entity, EntityRegistration> _registrationsByEntity = new(ReferenceEqualityComparer.Instance);
    private readonly List<EntityRegistration> _registrations = [];
    private readonly ReadOnlyCollection<EntityRegistration> _registrationView;
    private readonly ReadOnlyCollection<SchemaDescriptor> _schemaDescriptors;
    private readonly ReadOnlyDictionary<SchemaName, SchemaDescriptor> _schemaDescriptorsByName;

    /// <summary>Initializes a temporarily unbound exchange structure.</summary>
    /// <param name="header">The required ISO header.</param>
    /// <exception cref="ArgumentNullException"><paramref name="header"/> is <see langword="null"/>.</exception>
    public ExchangeStructure(HeaderSection header)
        : this(header, Array.Empty<SchemaDescriptor>())
    {
    }

    /// <summary>Initializes an exchange structure with a snapshot of uniquely named schema descriptors.</summary>
    /// <param name="header">The required ISO header.</param>
    /// <param name="schemaDescriptors">The descriptors to snapshot in supplied order.</param>
    /// <exception cref="ArgumentNullException">An argument or descriptor is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Two descriptors have the same ordinal <see cref="SchemaDescriptor.Name"/>.</exception>
    public ExchangeStructure(HeaderSection header, IReadOnlyCollection<SchemaDescriptor> schemaDescriptors)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(schemaDescriptors);

        var descriptorSnapshot = schemaDescriptors.ToArray();
        if (descriptorSnapshot.Any(descriptor => descriptor is null))
            throw new ArgumentException("Schema descriptor collections cannot contain null values.", nameof(schemaDescriptors));
        var descriptorBindings = new Dictionary<SchemaName, SchemaDescriptor>();
        foreach (var descriptor in descriptorSnapshot)
        {
            var name = descriptor.Name;
            if (name.IsDefault)
                throw new ArgumentException("Schema descriptors must expose valid names.", nameof(schemaDescriptors));
            if (!descriptorBindings.TryAdd(name, descriptor))
            {
                throw new ArgumentException(
                    $"Schema descriptor name '{name}' occurs more than once.",
                    nameof(schemaDescriptors));
            }
        }

        Header = header;
        DataSections = new List<DataSection>();
        _schemaDescriptors = Array.AsReadOnly(descriptorSnapshot);
        _schemaDescriptorsByName = new ReadOnlyDictionary<SchemaName, SchemaDescriptor>(descriptorBindings);
        _registrationView = _registrations.AsReadOnly();
    }

    /// <summary>Gets the required ISO header retained by identity.</summary>
    public HeaderSection Header { get; }

    /// <summary>
    /// Gets the mutable ISO data-section collection. Editing this list performs no validation or registration repair.
    /// </summary>
    public IList<DataSection> DataSections { get; }

    /// <summary>
    /// Registers an entity graph in an owned data section using the smallest unused positive root name.
    /// </summary>
    /// <param name="dataSection">An existing member of <see cref="DataSections"/>.</param>
    /// <param name="entity">The root entity.</param>
    /// <returns>The root's structure-local entity instance name.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="dataSection"/> is not owned by this structure.</exception>
    public EntityInstanceName Add(DataSection dataSection, Entity entity)
    {
        PrepareAdd(dataSection, entity);
        var graph = CaptureGraph(entity);
        var rootName = _registrationsByEntity.TryGetValue(entity, out var existing)
            ? existing.Name
            : default;
        var additions = PlanAdditions(graph, explicitRootName: null);
        if (existing is null)
            rootName = additions[0].Name;

        Commit(additions, dataSection);
        return rootName;
    }

    /// <summary>
    /// Registers an entity graph in an owned data section with an explicit root name.
    /// </summary>
    /// <param name="dataSection">An existing member of <see cref="DataSections"/>.</param>
    /// <param name="name">The explicit structure-local root name.</param>
    /// <param name="entity">The root entity.</param>
    /// <exception cref="ArgumentNullException">A reference argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="dataSection"/> is not owned by this structure.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="name"/> is the invalid default value, the name identifies another entity, or the entity has
    /// another name in this structure.
    /// </exception>
    public void Add(DataSection dataSection, EntityInstanceName name, Entity entity)
    {
        PrepareAdd(dataSection, entity);
        _ = name.CanonicalDigits;
        EnsureExplicitPairAvailable(name, entity);
        var graph = CaptureGraph(entity);
        var additions = PlanAdditions(graph, name);
        Commit(additions, dataSection);
    }

    /// <summary>Removes one entity registration without cascading to referenced entities or running validation.</summary>
    /// <param name="entity">The entity to remove by CLR reference identity.</param>
    /// <returns><see langword="true"/> when a registration was removed; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
    public bool Remove(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return _registrationsByEntity.TryGetValue(entity, out var registration) && Remove(registration);
    }

    /// <summary>Removes one named registration without cascading to referenced entities or running validation.</summary>
    /// <param name="name">The structure-local entity instance name.</param>
    /// <returns><see langword="true"/> when a registration was removed; otherwise <see langword="false"/>.</returns>
    public bool Remove(EntityInstanceName name) =>
        _registrationsByName.TryGetValue(name, out var registration) && Remove(registration);

    internal IReadOnlyList<SchemaDescriptor> SchemaDescriptors => _schemaDescriptors;

    internal IReadOnlyList<EntityRegistration> Registrations => _registrationView;

    internal bool TryGetSchemaDescriptor(SchemaName name, out SchemaDescriptor? descriptor) =>
        _schemaDescriptorsByName.TryGetValue(name, out descriptor);

    internal bool TryGetEntity(EntityInstanceName name, out Entity? entity)
    {
        if (_registrationsByName.TryGetValue(name, out var registration))
        {
            entity = registration.Entity;
            return true;
        }

        entity = null;
        return false;
    }

    internal bool TryGetName(Entity entity, out EntityInstanceName name)
    {
        if (_registrationsByEntity.TryGetValue(entity, out var registration))
        {
            name = registration.Name;
            return true;
        }

        name = default;
        return false;
    }

    internal bool TryGetSection(Entity entity, out DataSection? dataSection)
    {
        if (_registrationsByEntity.TryGetValue(entity, out var registration))
        {
            dataSection = registration.DataSection;
            return true;
        }

        dataSection = null;
        return false;
    }

    private void PrepareAdd(DataSection dataSection, Entity entity)
    {
        ArgumentNullException.ThrowIfNull(dataSection);
        ArgumentNullException.ThrowIfNull(entity);
        if (!DataSections.Any(candidate => ReferenceEquals(candidate, dataSection)))
        {
            throw new ArgumentException(
                "The data section must already belong to this exchange structure.",
                nameof(dataSection));
        }
    }

    private void EnsureExplicitPairAvailable(EntityInstanceName name, Entity entity)
    {
        if (_registrationsByName.TryGetValue(name, out var byName) && !ReferenceEquals(byName.Entity, entity))
            throw new InvalidOperationException($"Entity instance name {name} is already registered.");
        if (_registrationsByEntity.TryGetValue(entity, out var byEntity) && !byEntity.Name.Equals(name))
            throw new InvalidOperationException($"The entity is already registered as {byEntity.Name}.");
    }

    private static IReadOnlyList<Entity> CaptureGraph(Entity root)
    {
        var graph = new List<Entity>();
        var visited = new HashSet<Entity>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<Entity>();
        pending.Push(root);

        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current))
                continue;

            graph.Add(current);
            var directReferences = current.DirectReferences?.ToArray()
                ?? throw new InvalidOperationException("Entity.DirectReferences cannot be null.");
            if (directReferences.Any(reference => reference is null))
                throw new InvalidOperationException("Entity.DirectReferences cannot contain null values.");

            for (var index = directReferences.Length - 1; index >= 0; index--)
                pending.Push(directReferences[index]);
        }

        return graph;
    }

    private IReadOnlyList<PlannedRegistration> PlanAdditions(
        IReadOnlyList<Entity> graph,
        EntityInstanceName? explicitRootName)
    {
        var usedNames = _registrationsByName.Keys.ToHashSet();
        var additions = new List<PlannedRegistration>();
        for (var index = 0; index < graph.Count; index++)
        {
            var entity = graph[index];
            if (_registrationsByEntity.ContainsKey(entity))
                continue;

            var name = index == 0 && explicitRootName is { } suppliedName
                ? suppliedName
                : FindSmallestUnusedName(usedNames);
            usedNames.Add(name);
            additions.Add(new PlannedRegistration(name, entity));
        }

        return additions;
    }

    private static EntityInstanceName FindSmallestUnusedName(IReadOnlySet<EntityInstanceName> usedNames)
    {
        var digits = "1";
        while (true)
        {
            var candidate = new EntityInstanceName(digits);
            if (!usedNames.Contains(candidate))
                return candidate;
            digits = Increment(digits);
        }
    }

    private static string Increment(string digits)
    {
        var characters = digits.ToCharArray();
        for (var index = characters.Length - 1; index >= 0; index--)
        {
            if (characters[index] != '9')
            {
                characters[index]++;
                return new string(characters);
            }

            characters[index] = '0';
        }

        return $"1{new string(characters)}";
    }

    private void Commit(IReadOnlyList<PlannedRegistration> additions, DataSection dataSection)
    {
        foreach (var addition in additions)
        {
            var registration = new EntityRegistration(addition.Name, addition.Entity, dataSection);
            _registrationsByName.Add(registration.Name, registration);
            _registrationsByEntity.Add(registration.Entity, registration);
            _registrations.Add(registration);
        }
    }

    private bool Remove(EntityRegistration registration)
    {
        _ = _registrationsByName.Remove(registration.Name);
        _ = _registrationsByEntity.Remove(registration.Entity);
        _ = _registrations.Remove(registration);
        return true;
    }

    private sealed record PlannedRegistration(EntityInstanceName Name, Entity Entity);
}

internal sealed class EntityRegistration(EntityInstanceName name, Entity entity, DataSection dataSection)
{
    internal EntityInstanceName Name { get; } = name;

    internal Entity Entity { get; } = entity;

    internal DataSection DataSection { get; } = dataSection;
}
