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
    /// Reads, schema-binds, validates, and atomically publishes one or more simple same-schema ISO 10303-21 data
    /// sections, including structure-local entity references across section boundaries.
    /// </summary>
    /// <param name="source">The character source. Diagnostics identify it by the stable logical name <c>&lt;reader&gt;</c>.</param>
    /// <param name="schemaDescriptors">The generated schema descriptors available to the closed read operation.</param>
    /// <returns>A complete validated mutable exchange structure.</returns>
    /// <exception cref="ArgumentNullException">An argument or descriptor is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A descriptor name is invalid or occurs more than once. This is detected before <paramref name="source"/> is
    /// consumed.
    /// </exception>
    /// <exception cref="ExchangeStructureSyntaxException">The source is not valid ISO 10303-21 syntax.</exception>
    /// <exception cref="ExchangeStructureBindingException">
    /// The parsed simple population cannot be bound completely to the supplied descriptor.
    /// </exception>
    /// <exception cref="ExchangeStructureReadValidationException">
    /// The bound structure fails structural or reachable EXPRESS validation.
    /// </exception>
    /// <exception cref="ExchangeStructureCapabilityException">
    /// The valid source requires a Part 21 operation outside the simple-read capability.
    /// </exception>
    /// <remarks>
    /// Duplicate descriptor validation precedes the first source read. Exceptions raised by the underlying
    /// <see cref="TextReader"/> are not caught or translated.
    /// </remarks>
    public static ExchangeStructure Read(
        TextReader source,
        IReadOnlyCollection<SchemaDescriptor> schemaDescriptors)
    {
        ArgumentNullException.ThrowIfNull(source);
        var descriptors = ExchangeStructureReader.SnapshotDescriptors(schemaDescriptors);
        return ExchangeStructureReader.Read(source.ReadToEnd(), descriptors);
    }

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

    /// <summary>
    /// Validates the current registered graph and every applicable generated schema rule without changing the model.
    /// </summary>
    /// <returns>Every detected failure in deterministic data-section, entity, and rule order.</returns>
    /// <remarks>
    /// Invalidity is returned as evidence and does not throw. Property assignment, aggregate mutation, registration,
    /// and removal remain unchecked; call this method whenever explicit feedback is required.
    /// </remarks>
    public ValidationResult Validate()
    {
        var failures = new List<ValidationFailure>();
        var relationshipFailures = new List<ValidationFailure>();
        var dataSections = DataSections.ToArray();
        var sectionFailures = new ValidationFailure?[dataSections.Length];
        var sectionIndexes = new Dictionary<DataSection, int>(ReferenceEqualityComparer.Instance);
        var descriptorsBySection = new Dictionary<DataSection, SchemaDescriptor>(ReferenceEqualityComparer.Instance);
        var entitiesBySection = new Dictionary<DataSection, List<KeyValuePair<string, Entity>>>(
            ReferenceEqualityComparer.Instance);
        for (var index = 0; index < dataSections.Length; index++)
        {
            var sectionPath = $"DataSections[{index}]";
            var dataSection = dataSections[index];
            if (dataSection is null)
            {
                sectionFailures[index] = new ValidationFailure(
                    "P21.STRUCTURE.DATA_SECTION.REQUIRED",
                    sectionPath,
                    "The data-section entry is null.");
                continue;
            }

            if (!sectionIndexes.TryAdd(dataSection, index))
            {
                sectionFailures[index] = new ValidationFailure(
                    "P21.STRUCTURE.DATA_SECTION.DUPLICATE",
                    sectionPath,
                    "The same data-section object occurs more than once.");
                continue;
            }

            if (!_schemaDescriptorsByName.TryGetValue(dataSection.SchemaName, out var descriptor))
            {
                sectionFailures[index] = new ValidationFailure(
                    "P21.STRUCTURE.SCHEMA_DESCRIPTOR",
                    $"{sectionPath}.SchemaName",
                    $"No supplied schema descriptor matches '{dataSection.SchemaName}'.");
                continue;
            }

            descriptorsBySection.Add(dataSection, descriptor);
            entitiesBySection.Add(dataSection, []);
        }

        var visited = new HashSet<Entity>(ReferenceEqualityComparer.Instance);
        foreach (var registration in _registrations)
        {
            if (!visited.Add(registration.Entity))
                continue;

            var entityPath = sectionIndexes.TryGetValue(registration.DataSection, out var sectionIndex)
                ? $"DataSections[{sectionIndex}].{registration.Name}"
                : $"Registrations[{registration.Name}]";
            if (entitiesBySection.TryGetValue(registration.DataSection, out var entities))
            {
                entities.Add(new KeyValuePair<string, Entity>(entityPath, registration.Entity));
            }
            else if (!sectionIndexes.ContainsKey(registration.DataSection))
            {
                relationshipFailures.Add(new ValidationFailure(
                    "P21.STRUCTURE.DATA_SECTION.MEMBERSHIP",
                    $"{entityPath}.DataSection",
                    "The registered entity belongs to a data section that is no longer in the structure."));
            }

            var referenceIndex = 0;
            foreach (var reference in registration.Entity.DirectReferences)
            {
                var referencePath = $"{entityPath}.DirectReferences[{referenceIndex}]";
                if (reference is null)
                {
                    relationshipFailures.Add(new ValidationFailure(
                        "P21.STRUCTURE.REFERENCE.REQUIRED",
                        referencePath,
                        "A direct entity-reference occurrence is null."));
                }
                else if (!_registrationsByEntity.ContainsKey(reference))
                {
                    relationshipFailures.Add(new ValidationFailure(
                        "P21.STRUCTURE.REFERENCE.REGISTRATION",
                        referencePath,
                        "The referenced entity is not registered in this exchange structure."));
                }

                referenceIndex++;
            }
        }

        for (var index = 0; index < dataSections.Length; index++)
        {
            if (sectionFailures[index] is { } sectionFailure)
            {
                failures.Add(sectionFailure);
                continue;
            }

            var dataSection = dataSections[index]!;
            var descriptor = descriptorsBySection[dataSection];
            failures.AddRange(descriptor.Validate(this, entitiesBySection[dataSection]).Failures);
        }

        failures.AddRange(relationshipFailures);
        return new ValidationResult(failures);
    }

    internal IReadOnlyList<SchemaDescriptor> SchemaDescriptors => _schemaDescriptors;

    internal IReadOnlyList<EntityRegistration> Registrations => _registrationView;

    internal bool TryGetSchemaDescriptor(SchemaName name, out SchemaDescriptor? descriptor) =>
        _schemaDescriptorsByName.TryGetValue(name, out descriptor);

    internal IReadOnlyList<Step21Diagnostic> GetSchemaDescriptorDiagnostics(SchemaName name)
    {
        return _schemaDescriptorsByName.ContainsKey(name)
            ? []
            :
            [
                new Step21Diagnostic(
                    "P21-BIND-SCHEMA",
                    Step21DiagnosticSeverity.Error,
                    $"No supplied schema descriptor matches '{name}'."),
            ];
    }

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
