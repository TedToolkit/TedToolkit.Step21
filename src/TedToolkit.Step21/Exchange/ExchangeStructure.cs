using System.Collections.ObjectModel;
using System.Globalization;

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
    private static readonly IReadOnlySet<string> EmptyOccurrenceNames =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly Dictionary<EntityInstanceName, EntityRegistration> _registrationsByName = [];
    private readonly Dictionary<Entity, EntityRegistration> _registrationsByEntity = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<EntityInstanceName, Entity> _resolvedExternalEntitiesByName = [];
    private readonly Dictionary<Entity, EntityInstanceName> _resolvedExternalNamesByEntity =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Entity, Entity> _domainProjectionSources = new(ReferenceEqualityComparer.Instance);
    private readonly List<EntityRegistration> _registrations = [];
    private readonly ReadOnlyCollection<EntityRegistration> _registrationView;
    private readonly ReadOnlyCollection<SchemaDescriptor> _schemaDescriptors;
    private readonly ReadOnlyDictionary<SchemaName, SchemaDescriptor> _schemaDescriptorsByName;
    private IReadOnlyList<Part21Signature> _signatures = Array.Empty<Part21Signature>();
    private IReadOnlyList<Part21ResourceSignatureReport> _signatureReports =
        Array.Empty<Part21ResourceSignatureReport>();
    private List<SchemaPopulationDefinition>? _schemaPopulations;
    private List<SchemaPopulationExternalFile>? _schemaPopulationExternalFiles;
    private IReadOnlyList<ExchangeStructure> _includedPopulationStructures = Array.Empty<ExchangeStructure>();
    private IReadOnlyList<SchemaDomainEquivalence> _domainEquivalences = Array.Empty<SchemaDomainEquivalence>();
    private ISchemaDomainEquivalenceProvider? _domainEquivalenceProvider;
    private List<Part21Anchor>? _anchors;
    private List<Part21Reference>? _references;

    /// <summary>Initializes a temporarily unbound exchange structure.</summary>
    /// <param name="header">The required ISO header.</param>
    /// <exception cref="ArgumentNullException"><paramref name="header"/> is <see langword="null"/>.</exception>
    public ExchangeStructure(HeaderSection header)
        : this(header, Array.Empty<SchemaDescriptor>())
    {
    }

    /// <summary>Initializes an exchange structure with a snapshot of uniquely bound schema descriptors.</summary>
    /// <param name="header">The required ISO header.</param>
    /// <param name="schemaDescriptors">The descriptors to snapshot in supplied order.</param>
    /// <exception cref="ArgumentNullException">An argument or descriptor is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Two descriptors have names that resolve to the same nominal binding identifier.
    /// </exception>
    public ExchangeStructure(HeaderSection header, IReadOnlyCollection<SchemaDescriptor> schemaDescriptors)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(schemaDescriptors);

        var descriptorSnapshot = schemaDescriptors.ToArray();
        if (descriptorSnapshot.Any(descriptor => descriptor is null))
            throw new ArgumentException("Schema descriptor collections cannot contain null values.", nameof(schemaDescriptors));
        var descriptorBindings = new Dictionary<SchemaName, SchemaDescriptor>(DescriptorNameComparer);
        foreach (var descriptor in descriptorSnapshot)
        {
            var name = descriptor.Name;
            if (name.IsDefault)
                throw new ArgumentException("Schema descriptors must expose valid names.", nameof(schemaDescriptors));
            if (!descriptorBindings.TryAdd(name, descriptor))
            {
                throw new ArgumentException(
                    $"Schema descriptor name '{name}' conflicts with another nominal binding identifier.",
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

    /// <summary>Gets the mutable anchor collection in physical order.</summary>
    public IList<Part21Anchor> Anchors => _anchors ??= [];

    /// <summary>Gets the mutable external-reference association collection in physical order.</summary>
    public IList<Part21Reference> References => _references ??= [];

    /// <summary>Gets the mutable <c>SCHEMA_POPULATION</c> external-file declarations in physical order.</summary>
    public IList<SchemaPopulationExternalFile> SchemaPopulation => _schemaPopulationExternalFiles ??= [];

    /// <summary>Gets the mutable <c>FILE_POPULATION</c> declarations in physical order.</summary>
    public IList<SchemaPopulationDefinition> FilePopulations => _schemaPopulations ??= [];

    /// <summary>
    /// Gets the mutable ISO data-section collection. Editing this list performs no validation or registration repair.
    /// </summary>
    public IList<DataSection> DataSections { get; }

    /// <summary>Gets signature sections and their CMS signer results in physical order.</summary>
    public IReadOnlyList<Part21Signature> Signatures => _signatures;

    /// <summary>Gets complete signature results for every signed resource in resolution order.</summary>
    public IReadOnlyList<Part21ResourceSignatureReport> SignatureReports => _signatureReports;

    /// <summary>
    /// Gets a live read-only enumeration of registered entities in deterministic registration order.
    /// </summary>
    /// <remarks>
    /// The view exposes model values for navigation without exposing the structure's occurrence-name or section
    /// registration indexes. Adding or removing registrations changes subsequent enumerations.
    /// </remarks>
    public IEnumerable<Entity> Entities => _registrations.Select(registration => registration.Entity);

    /// <summary>Gets this structure's complete transitive, identity-distinct schema population.</summary>
    public IEnumerable<Entity> SchemaPopulationEntities
    {
        get
        {
            var visited = new HashSet<Entity>(ReferenceEqualityComparer.Instance);
            foreach (var structure in new[] { this }.Concat(_includedPopulationStructures))
            {
                foreach (var registration in structure._registrations)
                {
                    if (visited.Add(registration.Entity))
                        yield return registration.Entity;
                }
            }
        }
    }

    /// <summary>
    /// Reads, schema-binds, validates, and atomically publishes simple ISO 10303-21 data sections under one or more
    /// explicitly supplied generated schemas, including standard schema populations and cross-section references.
    /// </summary>
    /// <param name="source">The character source. Diagnostics identify it by the stable logical name <c>&lt;reader&gt;</c>.</param>
    /// <param name="schemaDescriptors">The generated schema descriptors available to the closed read operation.</param>
    /// <returns>A complete validated mutable exchange structure.</returns>
    /// <exception cref="ArgumentNullException">An argument or descriptor is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A descriptor name is invalid or resolves to the same nominal binding identifier as another descriptor. This is
    /// detected before <paramref name="source"/> is consumed.
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
    /// Reads and atomically resolves a distributed ISO 10303-21 structure using only explicitly supplied resources.
    /// </summary>
    /// <param name="source">The root character source.</param>
    /// <param name="schemaDescriptors">The generated schema descriptors shared by the resource graph.</param>
    /// <param name="options">The per-read base identity, resource capabilities, and limits.</param>
    /// <returns>A complete validated mutable exchange structure.</returns>
    /// <exception cref="ArgumentNullException">An argument or descriptor is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A descriptor name is invalid or resolves to the same nominal binding identifier as another descriptor. This is
    /// detected before <paramref name="source"/> is consumed.
    /// </exception>
    /// <exception cref="ExchangeStructureCapabilityException">
    /// A required provider or converter is absent, re-enters reading, or exceeds a resource limit.
    /// </exception>
    /// <exception cref="ExchangeStructureSyntaxException">The root source is not valid ISO 10303-21 syntax.</exception>
    /// <exception cref="ExchangeStructureBindingException">
    /// The root structure cannot be bound completely to the closed descriptor set.
    /// </exception>
    /// <exception cref="ExchangeStructureReadValidationException">
    /// The bound root structure fails structural or reachable EXPRESS validation.
    /// </exception>
    /// <remarks>
    /// A delivered external exchange structure that cannot be parsed, bound, or validated resolves its reference
    /// to the ISO 10303-21 null result; capability, quota, and archive-integrity failures remain atomic exceptions.
    /// Exceptions raised by the underlying <see cref="TextReader"/> propagate unchanged.
    /// </remarks>
    public static ExchangeStructure Read(
        TextReader source,
        IReadOnlyCollection<SchemaDescriptor> schemaDescriptors,
        ExchangeStructureReadOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        var descriptors = ExchangeStructureReader.SnapshotDescriptors(schemaDescriptors);
        return ExchangeStructureReader.Read(source.ReadToEnd(), descriptors, options);
    }

    /// <summary>Writes this complete structure as deterministic ISO 10303-21 clear text.</summary>
    /// <param name="destination">The destination that receives the complete canonical structure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ExchangeStructureWriteValidationException">
    /// The current registered graph is invalid. No output is produced.
    /// </exception>
    /// <exception cref="ExchangeStructureCapabilityException">
    /// The current structure requires an unsupported write operation. No output is produced.
    /// </exception>
    /// <remarks>
    /// The final current graph is validated once before projection or output. Property, aggregate, registration, and
    /// removal edits remain unchecked until this boundary. Do not mutate the structure concurrently with writing.
    /// Exceptions raised by <paramref name="destination"/> are not caught or translated.
    /// </remarks>
    public void Write(TextWriter destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ExchangeStructureWriter.Write(this, destination);
    }

    /// <summary>Writes this structure and creates signature sections with explicit signing capabilities.</summary>
    /// <param name="destination">The destination that receives the complete canonical structure.</param>
    /// <param name="options">The signing capabilities used in signature-section order.</param>
    public void Write(TextWriter destination, ExchangeStructureWriteOptions options)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        ExchangeStructureWriter.Write(this, destination, options);
    }

    /// <summary>Writes one registered entity-instance record using this structure's occurrence-name context.</summary>
    /// <param name="destination">The destination that receives the canonical entity-instance record.</param>
    /// <param name="entity">The registered entity to write.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ExchangeStructureWriteValidationException">
    /// The current graph is invalid or <paramref name="entity"/> is not registered. No output is produced.
    /// </exception>
    /// <exception cref="ExchangeStructureCapabilityException">
    /// The entity requires an unsupported write operation. No output is produced.
    /// </exception>
    /// <remarks>
    /// The final current graph and target registration are validated together before projection or output. Property,
    /// aggregate, registration, and removal edits remain unchecked until this boundary. Do not mutate the structure
    /// concurrently with writing. Exceptions raised by <paramref name="destination"/> are not caught or translated.
    /// </remarks>
    public void WriteEntity(TextWriter destination, Entity entity)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(entity);
        ExchangeStructureWriter.WriteEntity(this, destination, entity);
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
        IReadOnlySet<string> externalEntityNames = EmptyOccurrenceNames;
        IReadOnlySet<string> externalValueNames = EmptyOccurrenceNames;
        List<ValidationFailure>? referenceFailures = null;
        if (_references is { Count: > 0, })
        {
            var entityNames = new HashSet<string>(StringComparer.Ordinal);
            var valueNames = new HashSet<string>(StringComparer.Ordinal);
            referenceFailures = [];
            ValidateReferences(referenceFailures, entityNames, valueNames);
            externalEntityNames = entityNames;
            externalValueNames = valueNames;
        }

        ValidateAnchors(failures, externalEntityNames, externalValueNames);
        if (referenceFailures is not null)
            failures.AddRange(referenceFailures);
        var schemaPopulations = ValidatePopulationDeclarations(failures);
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
                else if (!TryGetName(reference, out _))
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
                failures.Add(sectionFailure);
        }

        failures.AddRange(ValidateSchemaPopulations(
            dataSections,
            sectionIndexes,
            descriptorsBySection,
            entitiesBySection,
            schemaPopulations));

        failures.AddRange(relationshipFailures);
        return new ValidationResult(failures);
    }

    private IReadOnlyList<SchemaPopulationDefinition> ValidatePopulationDeclarations(
        ICollection<ValidationFailure> failures)
    {
        if (_schemaPopulationExternalFiles is not null)
        {
            for (var index = 0; index < _schemaPopulationExternalFiles.Count; index++)
            {
                if (_schemaPopulationExternalFiles[index] is null)
                {
                    failures.Add(new ValidationFailure(
                        "P21.STRUCTURE.SCHEMA_POPULATION.EXTERNAL_FILE.REQUIRED",
                        $"SchemaPopulation[{index.ToString(CultureInfo.InvariantCulture)}]",
                        "The external schema-population entry is null."));
                    continue;
                }

                var externalFile = _schemaPopulationExternalFiles[index];
                var path = $"SchemaPopulation[{index.ToString(CultureInfo.InvariantCulture)}].MessageDigest";
                if (externalFile.MessageDigest is not null && Signatures.Count == 0)
                {
                    failures.Add(new ValidationFailure(
                        "P21.STRUCTURE.SCHEMA_POPULATION.DIGEST.SIGNATURE_REQUIRED",
                        path,
                        "A schema-population message digest requires at least one signature section."));
                }
                if (externalFile.DigestStatus == SchemaPopulationDigestStatus.Mismatch)
                {
                    failures.Add(new ValidationFailure(
                        "P21.STRUCTURE.SCHEMA_POPULATION.DIGEST.MISMATCH",
                        path,
                        "The schema-population message digest does not match the referenced file."));
                }
            }
        }

        if (_schemaPopulations is null)
            return Array.Empty<SchemaPopulationDefinition>();

        var valid = new List<SchemaPopulationDefinition>(_schemaPopulations.Count);
        for (var index = 0; index < _schemaPopulations.Count; index++)
        {
            var population = _schemaPopulations[index];
            if (population is null)
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.FILE_POPULATION.REQUIRED",
                    $"FilePopulations[{index.ToString(CultureInfo.InvariantCulture)}]",
                    "The file-population entry is null."));
                continue;
            }

            valid.Add(population);
        }

        return valid;
    }

    private void ValidateAnchors(
        ICollection<ValidationFailure> failures,
        IReadOnlySet<string> externalEntityNames,
        IReadOnlySet<string> externalValueNames)
    {
        if (_anchors is null)
            return;

        var names = new HashSet<AnchorName>();
        for (var index = 0; index < _anchors.Count; index++)
        {
            var anchor = _anchors[index];
            var path = $"Anchors[{index.ToString(CultureInfo.InvariantCulture)}]";
            if (anchor is null)
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.ANCHOR.REQUIRED",
                    path,
                    "The anchor entry is null."));
                continue;
            }

            if (!names.Add(anchor.Name))
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.ANCHOR.DUPLICATE",
                    $"{path}.Name",
                    $"Anchor name '{anchor.Name}' occurs more than once."));
            }

            ValidateAnchorItem(
                anchor.Item,
                $"{path}.Item",
                failures,
                externalEntityNames,
                externalValueNames);
            for (var tagIndex = 0; tagIndex < anchor.Tags.Count; tagIndex++)
            {
                ValidateAnchorItem(
                    anchor.Tags[tagIndex].Item,
                    $"{path}.Tags[{tagIndex.ToString(CultureInfo.InvariantCulture)}].Item",
                    failures,
                    externalEntityNames,
                    externalValueNames);
            }
        }
    }

    private void ValidateAnchorItem(
        ParameterValue item,
        string path,
        ICollection<ValidationFailure> failures,
        IReadOnlySet<string> externalEntityNames,
        IReadOnlySet<string> externalValueNames)
    {
        if (item.TryGetEntity(out var entity))
        {
            if (!TryGetName(entity, out _))
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.ANCHOR.ENTITY_REGISTRATION",
                    path,
                    "The anchored entity is not registered in this exchange structure."));
            }
            return;
        }

        if (item.TryGetEntityInstance(out var entityName))
        {
            if (_registrationsByName.ContainsKey(entityName))
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.ANCHOR.ENTITY_IDENTITY",
                    path,
                    $"Local entity occurrence '{entityName}' must be retained by object identity."));
            }
            else if (!externalEntityNames.Contains(entityName.CanonicalDigits))
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.ANCHOR.ENTITY_OCCURRENCE",
                    path,
                    $"Entity occurrence '{entityName}' is not defined."));
            }
            return;
        }

        if (item.TryGetValueInstance(out var valueName))
        {
            if (!externalValueNames.Contains(valueName.CanonicalDigits))
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.ANCHOR.VALUE_OCCURRENCE",
                    path,
                    $"Value occurrence '{valueName}' is not defined in the reference section."));
            }
            return;
        }

        var firstDescriptor = Header.FileSchema.SchemaIdentifiers.Count == 0
            ? null
            : _schemaDescriptorsByName.GetValueOrDefault(new SchemaName(Header.FileSchema.SchemaIdentifiers[0]));
        if (item.TryGetConstantEntity(out var entityConstant)
            && (firstDescriptor is null || !firstDescriptor.ContainsConstantEntity(entityConstant.Value)))
        {
            failures.Add(new ValidationFailure(
                "P21.STRUCTURE.ANCHOR.ENTITY_CONSTANT",
                path,
                $"Entity constant '{entityConstant}' is not defined by the first FILE_SCHEMA schema."));
            return;
        }

        if (item.TryGetConstantValue(out var valueConstant)
            && (firstDescriptor is null || !firstDescriptor.ContainsConstantValue(valueConstant.Value)))
        {
            failures.Add(new ValidationFailure(
                "P21.STRUCTURE.ANCHOR.VALUE_CONSTANT",
                path,
                $"Value constant '{valueConstant}' is not defined by the first FILE_SCHEMA schema."));
            return;
        }

        if (item.TryGetAggregate(out var values))
        {
            for (var index = 0; index < values.Count; index++)
            {
                ValidateAnchorItem(
                    values[index],
                    $"{path}[{index.ToString(CultureInfo.InvariantCulture)}]",
                    failures,
                    externalEntityNames,
                    externalValueNames);
            }
        }
    }

    private void ValidateReferences(
        ICollection<ValidationFailure> failures,
        ISet<string> externalEntityNames,
        ISet<string> externalValueNames)
    {
        if (_references is null)
            return;

        var names = new HashSet<(Part21ReferenceKind Kind, string Digits)>();
        var numericNames = new Dictionary<string, Part21ReferenceKind>(StringComparer.Ordinal);
        for (var index = 0; index < _references.Count; index++)
        {
            var reference = _references[index];
            var path = $"References[{index.ToString(CultureInfo.InvariantCulture)}]";
            if (reference is null)
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.EXTERNAL_REFERENCE.REQUIRED",
                    path,
                    "The external-reference entry is null."));
                continue;
            }

            var key = (reference.Kind, reference.CanonicalDigits);
            _ = (reference.Kind == Part21ReferenceKind.EntityInstance
                ? externalEntityNames
                : externalValueNames).Add(reference.CanonicalDigits);
            if (!names.Add(key))
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.EXTERNAL_REFERENCE.DUPLICATE",
                    path,
                    $"Occurrence '{reference.FormatName()}' has more than one resource association."));
            }

            if (numericNames.TryGetValue(reference.CanonicalDigits, out var otherKind)
                && otherKind != reference.Kind)
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.OCCURRENCE.OVERLAP",
                    path,
                    "Entity and value instance names shall not use the same integer."));
            }
            else
            {
                numericNames[reference.CanonicalDigits] = reference.Kind;
            }

            if (reference.Kind == Part21ReferenceKind.EntityInstance
                && _registrationsByName.ContainsKey(new EntityInstanceName(reference.CanonicalDigits)))
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.EXTERNAL_REFERENCE.DATA_DUPLICATE",
                    path,
                    $"Entity occurrence '{reference.FormatName()}' is also defined in a data section."));
            }
            else if (reference.Kind == Part21ReferenceKind.ValueInstance
                && _registrationsByName.ContainsKey(new EntityInstanceName(reference.CanonicalDigits)))
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.OCCURRENCE.OVERLAP",
                    path,
                    "A local entity and external value instance name shall not use the same integer."));
            }
        }
    }

    internal IReadOnlyList<SchemaDescriptor> SchemaDescriptors => _schemaDescriptors;

    internal IReadOnlyList<Part21Anchor> AnchorEntries =>
        _anchors is null ? Array.Empty<Part21Anchor>() : _anchors;

    internal IReadOnlyList<Part21Reference> ReferenceEntries =>
        _references is null ? Array.Empty<Part21Reference>() : _references;

    internal static IEqualityComparer<SchemaName> DescriptorNameComparer { get; } = new SchemaIdentifierComparer();

    internal static bool SchemaIdentifiersAssociate(SchemaName left, SchemaName right) =>
        DescriptorNameComparer.Equals(left, right);

    internal IReadOnlyList<SchemaPopulationDefinition> SchemaPopulations =>
        _schemaPopulations ?? (IReadOnlyList<SchemaPopulationDefinition>)Array.Empty<SchemaPopulationDefinition>();

    internal IReadOnlyList<SchemaPopulationExternalFile> SchemaPopulationExternalFiles =>
        _schemaPopulationExternalFiles
        ?? (IReadOnlyList<SchemaPopulationExternalFile>)Array.Empty<SchemaPopulationExternalFile>();

    internal IReadOnlyList<EntityRegistration> Registrations => _registrationView;

    internal bool TryGetSchemaDescriptor(SchemaName name, out SchemaDescriptor? descriptor) =>
        _schemaDescriptorsByName.TryGetValue(name, out descriptor);

    internal void SetSchemaPopulations(IReadOnlyList<SchemaPopulationDefinition> populations)
    {
        ArgumentNullException.ThrowIfNull(populations);
        _schemaPopulations = [.. populations];
    }

    internal void SetSchemaPopulationExternalFiles(IReadOnlyList<SchemaPopulationExternalFile> externalFiles)
    {
        ArgumentNullException.ThrowIfNull(externalFiles);
        _schemaPopulationExternalFiles = [.. externalFiles];
    }

    internal void SetIncludedPopulationStructures(IReadOnlyList<ExchangeStructure> structures)
    {
        ArgumentNullException.ThrowIfNull(structures);
        _includedPopulationStructures = Array.AsReadOnly(structures.ToArray());
    }

    internal void SetDomainEquivalenceProvider(
        ISchemaDomainEquivalenceProvider? provider,
        IReadOnlyList<SchemaDomainEquivalence> equivalences)
    {
        ArgumentNullException.ThrowIfNull(equivalences);
        foreach (var endpoint in equivalences.SelectMany(item => new[] { item.Source, item.Target }).Distinct())
        {
            var descriptor = _schemaDescriptors.FirstOrDefault(candidate => SchemaIdentifiersAssociate(
                candidate.Name,
                endpoint.SchemaName));
            if (descriptor is null || descriptor.AllocateEntity([endpoint.EntityName.ToUpperInvariant()]) is null)
            {
                throw new ArgumentException(
                    $"Domain-equivalence endpoint '{endpoint}' is not defined by the supplied descriptors.",
                    nameof(equivalences));
            }
        }
        _domainEquivalenceProvider = provider;
        _domainEquivalences = Array.AsReadOnly(equivalences.ToArray());
    }

    internal bool TryProjectDomainParameters(
        SchemaDomainEquivalence equivalence,
        IReadOnlyList<ParameterValue> sourceParameters,
        out IReadOnlyList<ParameterValue> projectedParameters,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(equivalence);
        ArgumentNullException.ThrowIfNull(sourceParameters);
        projectedParameters = Array.Empty<ParameterValue>();
        error = null;
        if (_domainEquivalenceProvider is null)
        {
            error = "No domain-equivalence parameter-projection provider was supplied.";
            return false;
        }

        IReadOnlyList<ParameterValue>? supplied;
        try
        {
            supplied = _domainEquivalenceProvider.ProjectParameters(
                equivalence.Target,
                equivalence.Source,
                Array.AsReadOnly(sourceParameters.ToArray()));
        }
        catch (Exception exception) when (exception is not (
            OperationCanceledException
            or OutOfMemoryException
            or StackOverflowException
            or AccessViolationException))
        {
            error = $"The domain-equivalence provider failed: {exception.Message}";
            return false;
        }

        if (supplied is null)
        {
            error = "The domain-equivalence provider returned a null parameter list.";
            return false;
        }
        var snapshot = supplied.ToArray();
        if (snapshot.Any(parameter => parameter is null))
        {
            error = "The domain-equivalence provider returned a null parameter.";
            return false;
        }

        var allowedEntities = new HashSet<Entity>(ReferenceEqualityComparer.Instance);
        foreach (var parameter in sourceParameters)
            CollectParameterEntities(parameter, allowedEntities);
        var projectedEntities = new HashSet<Entity>(ReferenceEqualityComparer.Instance);
        foreach (var parameter in snapshot)
            CollectParameterEntities(parameter, projectedEntities);
        if (!projectedEntities.IsSubsetOf(allowedEntities))
        {
            error = "The domain-equivalence provider introduced an entity outside the source parameters.";
            return false;
        }

        projectedParameters = Array.AsReadOnly(snapshot);
        return true;
    }

    private static void CollectParameterEntities(ParameterValue value, ISet<Entity> entities)
    {
        if (value.TryGetEntity(out var entity))
        {
            _ = entities.Add(entity!);
            return;
        }
        if (value.TryGetAggregate(out var values))
        {
            foreach (var item in values)
                CollectParameterEntities(item, entities);
            return;
        }
        if (value.TryGetTyped(out _, out var inner))
            CollectParameterEntities(inner, entities);
    }

    internal bool TryAllocateDomainProjection(
        SchemaDescriptor receivingDescriptor,
        Entity source,
        out Entity projection,
        out SchemaDescriptor sourceDescriptor,
        out SchemaDomainEquivalence equivalence)
    {
        ArgumentNullException.ThrowIfNull(receivingDescriptor);
        ArgumentNullException.ThrowIfNull(source);
        projection = null!;
        var owningDescriptor = FindOwningDescriptor(source);
        sourceDescriptor = owningDescriptor!;
        equivalence = null!;
        if (owningDescriptor is null)
            return false;

        var sourceComponentNames = owningDescriptor.ProjectEntity(source)
            .Select(component => component.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = _domainEquivalences.Where(candidate =>
                SchemaIdentifiersAssociate(candidate.Source.SchemaName, receivingDescriptor.Name)
                && SchemaIdentifiersAssociate(candidate.Target.SchemaName, owningDescriptor.Name)
                && sourceComponentNames.Contains(candidate.Target.EntityName))
            .ToArray();
        if (candidates.Length == 0)
            return false;
        if (candidates.Length > 1)
        {
            throw new ArgumentException(
                $"Domain equivalence is contradictory for '{receivingDescriptor.Name}' and "
                    + $"'{owningDescriptor.Name}.{string.Join("/", sourceComponentNames)}'.",
                nameof(_domainEquivalences));
        }

        equivalence = candidates[0];
        projection = receivingDescriptor.AllocateEntity([equivalence.Source.EntityName.ToUpperInvariant()])
            ?? throw new ArgumentException(
                $"Domain-equivalence endpoint '{equivalence.Source}' cannot be allocated by its descriptor.",
                nameof(_domainEquivalences));
        RegisterDomainProjection(source, projection);
        return true;
    }

    private SchemaDescriptor? FindOwningDescriptor(Entity entity)
    {
        if (_registrationsByEntity.TryGetValue(entity, out var registration)
            && _schemaDescriptorsByName.TryGetValue(registration.DataSection.SchemaName, out var localDescriptor))
        {
            return localDescriptor;
        }

        SchemaDescriptor? match = null;
        foreach (var descriptor in _schemaDescriptors)
        {
            if (descriptor.ProjectEntity(entity).Count == 0)
                continue;
            if (match is not null)
                throw new ArgumentException("An entity is projected by more than one supplied schema descriptor.", nameof(entity));
            match = descriptor;
        }

        return match;
    }

    private void RegisterDomainProjection(Entity source, Entity projection)
    {
        if (_registrationsByEntity.TryGetValue(source, out var registration))
        {
            _registrationsByEntity.Add(projection, registration);
            _domainProjectionSources.Add(projection, source);
            return;
        }

        if (!TryGetName(source, out var name))
            throw new ArgumentException("A domain-equivalence source must have a canonical occurrence identity.", nameof(source));
        _resolvedExternalNamesByEntity.Add(projection, name);
        _domainProjectionSources.Add(projection, source);
    }

    internal bool IsDomainEquivalentTo(ExchangeStructure targetStructure, Entity target)
    {
        if (_domainEquivalences.Count == 0
            || !targetStructure._registrationsByEntity.TryGetValue(target, out var registration)
            || !targetStructure._schemaDescriptorsByName.TryGetValue(registration.DataSection.SchemaName, out var targetDescriptor))
        {
            return false;
        }

        return _schemaDescriptors.Any(governing => IsDomainEquivalent(governing, targetDescriptor, target));
    }

    private bool IsDomainEquivalent(
        SchemaDescriptor governingDescriptor,
        SchemaDescriptor targetDescriptor,
        Entity target)
    {
        if (_domainEquivalences.Count == 0)
            return false;
        var targetTypes = targetDescriptor.ProjectEntity(target)
            .Select(component => component.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _domainEquivalences.Any(equivalence =>
            SchemaIdentifiersAssociate(equivalence.Source.SchemaName, governingDescriptor.Name)
            && SchemaIdentifiersAssociate(equivalence.Target.SchemaName, targetDescriptor.Name)
            && targetTypes.Contains(equivalence.Target.EntityName));
    }

    internal void SetSignatures(IReadOnlyList<Part21Signature> signatures)
    {
        ArgumentNullException.ThrowIfNull(signatures);
        _signatures = Array.AsReadOnly(signatures.ToArray());
        _signatureReports = _signatures.Count == 0
            ? Array.Empty<Part21ResourceSignatureReport>()
            : Array.AsReadOnly<Part21ResourceSignatureReport>(
                [new Part21ResourceSignatureReport("<reader>", _signatures)]);
    }

    internal void SetSignatureReports(IReadOnlyList<Part21ResourceSignatureReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        _signatureReports = Array.AsReadOnly(reports.ToArray());
    }

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

        if (_resolvedExternalEntitiesByName.TryGetValue(name, out entity)
            && IsCurrentResolvedReference(name, entity))
            return true;

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

        if (_resolvedExternalNamesByEntity.TryGetValue(entity, out name)
            && IsCurrentResolvedReference(name, entity))
            return true;

        if (_references is not null)
        {
            foreach (var reference in _references)
            {
                if (reference is not null
                    && reference.TryGetEntityInstance(out name)
                    && reference.TryGetResolvedValue(out var value)
                    && value is not null
                    && value.TryGetEntity(out var candidate)
                    && ReferenceEquals(candidate, entity))
                {
                    _resolvedExternalNamesByEntity[entity] = name;
                    return true;
                }
            }
        }

        _ = _resolvedExternalNamesByEntity.Remove(entity);
        name = default;
        return false;
    }

    internal void SetResolvedReference(Part21Reference reference, ParameterValue? value)
    {
        ArgumentNullException.ThrowIfNull(reference);
        reference.SetResolution(value);
        if (reference.Kind != Part21ReferenceKind.EntityInstance
            || value is null
            || !value.TryGetEntity(out var entity)
            || !reference.TryGetEntityInstance(out var name))
        {
            return;
        }

        _resolvedExternalEntitiesByName[name] = entity;
        _resolvedExternalNamesByEntity.TryAdd(entity, name);
    }

    private bool IsCurrentResolvedReference(EntityInstanceName name, Entity entity) =>
        _references?.Any(reference => reference is not null
            && reference.TryGetEntityInstance(out var candidateName)
            && candidateName.Equals(name)
            && reference.TryGetResolvedValue(out var value)
            && value is not null
            && value.TryGetEntity(out var candidateEntity)
            && ReferenceEquals(
                candidateEntity,
                _domainProjectionSources.TryGetValue(entity, out var source) ? source : entity)) == true;

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

    private sealed class SchemaIdentifierComparer : IEqualityComparer<SchemaName>
    {
        public bool Equals(SchemaName left, SchemaName right)
        {
            if (left.Equals(right))
                return true;

            return TryGetNominalName(left, out var leftNominal)
                && TryGetNominalName(right, out var rightNominal)
                && string.Equals(leftNominal, rightNominal, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(SchemaName value) => TryGetNominalName(value, out var nominal)
            ? StringComparer.OrdinalIgnoreCase.GetHashCode(nominal)
            : value.GetHashCode();

        private static bool TryGetNominalName(SchemaName name, out string nominal)
        {
            var identifier = name.Value;
            var openBrace = identifier.IndexOf('{');
            var nominalLength = openBrace < 0 ? identifier.Length : openBrace;
            if (openBrace >= 0)
            {
                while (nominalLength > 0 && identifier[nominalLength - 1] == ' ')
                    nominalLength--;
                if (nominalLength == openBrace || identifier[^1] != '}')
                {
                    nominal = string.Empty;
                    return false;
                }
            }

            if (nominalLength == 0
                || identifier[0] is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z'))
            {
                nominal = string.Empty;
                return false;
            }

            for (var index = 1; index < nominalLength; index++)
            {
                var character = identifier[index];
                if (character is not (>= 'A' and <= 'Z')
                    and not (>= 'a' and <= 'z')
                    and not (>= '0' and <= '9')
                    and not '_')
                {
                    nominal = string.Empty;
                    return false;
                }
            }

            nominal = identifier[..nominalLength];
            if (openBrace < 0)
                return true;

            var suffix = identifier.AsSpan(openBrace + 1, identifier.Length - openBrace - 2);
            var suffixIndex = 0;
            while (suffixIndex < suffix.Length && suffix[suffixIndex] == ' ')
                suffixIndex++;

            var arcCount = 0;
            var rootArc = -1;
            while (suffixIndex < suffix.Length)
            {
                var arcStart = suffixIndex;
                while (suffixIndex < suffix.Length && suffix[suffixIndex] is >= '0' and <= '9')
                    suffixIndex++;
                if (suffixIndex == arcStart)
                    return false;

                var arcLength = suffixIndex - arcStart;
                if (arcLength > 1 && suffix[arcStart] == '0')
                    return false;
                if (arcCount == 0)
                {
                    if (arcLength != 1 || suffix[arcStart] > '2')
                        return false;
                    rootArc = suffix[arcStart] - '0';
                }
                else if (arcCount == 1 && rootArc < 2)
                {
                    if (arcLength > 2)
                        return false;
                    var secondArc = suffix[arcStart] - '0';
                    if (arcLength == 2)
                        secondArc = (secondArc * 10) + suffix[arcStart + 1] - '0';
                    if (secondArc > 39)
                        return false;
                }

                arcCount++;
                var delimiterStart = suffixIndex;
                while (suffixIndex < suffix.Length && suffix[suffixIndex] == ' ')
                    suffixIndex++;
                if (suffixIndex < suffix.Length && suffixIndex == delimiterStart)
                    return false;
            }

            return arcCount >= 2;
        }
    }

    private IReadOnlyList<ValidationFailure> ValidateSchemaPopulations(
        IReadOnlyList<DataSection?> dataSections,
        IReadOnlyDictionary<DataSection, int> sectionIndexes,
        IReadOnlyDictionary<DataSection, SchemaDescriptor> descriptorsBySection,
        IReadOnlyDictionary<DataSection, List<KeyValuePair<string, Entity>>> entitiesBySection,
        IReadOnlyList<SchemaPopulationDefinition> schemaPopulations)
    {
        var failures = new List<ValidationFailure>();
        var validSections = dataSections
            .Select((section, index) => new { Section = section, Index = index, })
            .Where(item => item.Section is not null
                && sectionIndexes.TryGetValue(item.Section, out var firstIndex)
                && firstIndex == item.Index
                && descriptorsBySection.ContainsKey(item.Section))
            .Select(item => item.Section!)
            .ToList();
        var populationDescriptorsBySection = new Dictionary<DataSection, SchemaDescriptor>(
            descriptorsBySection,
            ReferenceEqualityComparer.Instance);
        var allEntries = validSections
            .SelectMany(section => entitiesBySection[section]
                .Select(entry => new PopulationEntry(entry.Key, entry.Value, section, this)))
            .ToList();
        for (var structureIndex = 0; structureIndex < _includedPopulationStructures.Count; structureIndex++)
        {
            var included = _includedPopulationStructures[structureIndex];
            var includedSections = new List<DataSection>();
            var seenIncludedSections = new HashSet<DataSection>(ReferenceEqualityComparer.Instance);
            foreach (var section in included.DataSections)
            {
                if (section is not null && seenIncludedSections.Add(section))
                    includedSections.Add(section);
            }
            foreach (var section in includedSections)
            {
                if (!included._schemaDescriptorsByName.TryGetValue(section.SchemaName, out var descriptor))
                    continue;
                validSections.Add(section);
                populationDescriptorsBySection.TryAdd(section, descriptor);
                var sectionIndex = included.DataSections.IndexOf(section);
                allEntries.AddRange(included._registrations
                    .Where(registration => ReferenceEquals(registration.DataSection, section))
                    .Select(registration => new PopulationEntry(
                        $"SchemaPopulation[{structureIndex}].DataSections[{sectionIndex}].{registration.Name}",
                        registration.Entity,
                        section,
                        included)));
            }
        }
        var populationEntities = new HashSet<Entity>(
            allEntries.Select(entry => entry.Entity),
            ReferenceEqualityComparer.Instance);
        var claimedSections = new HashSet<DataSection>(ReferenceEqualityComparer.Instance);
        foreach (var definition in schemaPopulations)
        {
            var governedNames = definition.GovernedSectionNames is null
                ? null
                : new HashSet<string>(definition.GovernedSectionNames, StringComparer.Ordinal);
            IReadOnlyList<DataSection> inputSections = governedNames is null
                ? validSections
                : validSections.Where(section => section.Name is not null && governedNames.Contains(section.Name)).ToArray();
            foreach (var sectionName in definition.GovernedSectionNames?.Where(name =>
                         !inputSections.Any(section => string.Equals(section.Name, name, StringComparison.Ordinal)))
                     ?? [])
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.SCHEMA_POPULATION.SECTION",
                    $"SchemaPopulations[{definition.SchemaName}]",
                    $"Input data section '{sectionName}' is not present in the schema population."));
            }

            foreach (var section in inputSections)
                claimedSections.Add(section);
            if (!_schemaDescriptorsByName.TryGetValue(definition.SchemaName, out var descriptor))
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.SCHEMA_POPULATION.DESCRIPTOR",
                    $"SchemaPopulations[{definition.SchemaName}]",
                    $"No supplied schema descriptor matches '{definition.SchemaName}'."));
                continue;
            }

            var selected = SelectPopulationEntries(
                definition.Determination,
                inputSections,
                allEntries,
                descriptor,
                populationDescriptorsBySection);
            failures.AddRange(ValidatePopulation(
                descriptor,
                selected,
                populationDescriptorsBySection,
                populationEntities));
        }

        foreach (var section in validSections.Where(section => !claimedSections.Contains(section)))
        {
            failures.AddRange(ValidatePopulation(
                populationDescriptorsBySection[section],
                allEntries.Where(entry => ReferenceEquals(entry.DataSection, section)).ToArray(),
                populationDescriptorsBySection,
                populationEntities));
        }

        return failures;
    }

    private PopulationEntry[] SelectPopulationEntries(
        SchemaPopulationDetermination determination,
        IReadOnlyCollection<DataSection> inputSections,
        IReadOnlyList<PopulationEntry> allEntries,
        SchemaDescriptor descriptor,
        IReadOnlyDictionary<DataSection, SchemaDescriptor> populationDescriptorsBySection)
    {
        var inputSet = new HashSet<DataSection>(inputSections, ReferenceEqualityComparer.Instance);
        var selected = new HashSet<Entity>(ReferenceEqualityComparer.Instance);
        foreach (var entry in allEntries.Where(entry => inputSet.Contains(entry.DataSection)))
            selected.Add(entry.Entity);

        if (determination == SchemaPopulationDetermination.IncludeReferenced)
        {
            var available = new HashSet<Entity>(
                allEntries.Select(entry => entry.Entity),
                ReferenceEqualityComparer.Instance);
            foreach (var entry in allEntries.Where(entry => inputSet.Contains(entry.DataSection)))
            {
                foreach (var reference in entry.Entity.DirectReferences)
                {
                    if (reference is null)
                        continue;
                    var canonical = entry.Owner.GetCanonicalPopulationEntity(reference);
                    if (available.Contains(canonical))
                        selected.Add(canonical);
                }
            }
        }
        else if (determination == SchemaPopulationDetermination.IncludeAllCompatible)
        {
            foreach (var entry in allEntries.Where(entry => !inputSet.Contains(entry.DataSection)
                         && (descriptor.IsEntityReferenceCompatible(entry.Entity)
                             || IsDomainEquivalent(
                                 descriptor,
                                 populationDescriptorsBySection[entry.DataSection],
                                 entry.Entity))))
            {
                selected.Add(entry.Entity);
            }
        }

        return allEntries.Where(entry => selected.Contains(entry.Entity)).ToArray();
    }

    private Entity GetCanonicalPopulationEntity(Entity entity)
    {
        var current = entity;
        while (_domainProjectionSources.TryGetValue(current, out var source))
            current = source;
        return current;
    }

    private IReadOnlyList<ValidationFailure> ValidatePopulation(
        SchemaDescriptor governingDescriptor,
        IReadOnlyList<PopulationEntry> entries,
        IReadOnlyDictionary<DataSection, SchemaDescriptor> descriptorsBySection,
        IReadOnlySet<Entity> populationEntities)
    {
        var selected = new HashSet<Entity>(entries.Select(entry => entry.Entity), ReferenceEqualityComparer.Instance);
        var hasOutsideReference = entries.Any(entry => entry.Entity.DirectReferences.Any(reference =>
            reference is not null
            && populationEntities.Contains(reference)
            && !selected.Contains(reference)));
        var allDirectlyGoverned = entries.All(entry =>
        {
            var owner = descriptorsBySection[entry.DataSection];
            return SchemaIdentifiersAssociate(owner.Name, governingDescriptor.Name)
                || governingDescriptor.IsEntityReferenceCompatible(entry.Entity);
        });
        if (!hasOutsideReference
            && entries.All(entry => ReferenceEquals(entry.Owner, this))
            && (_domainEquivalences.Count == 0 || allDirectlyGoverned))
        {
            var failures = governingDescriptor.Validate(
                this,
                entries.Select(entry => new KeyValuePair<string, Entity>(entry.Path, entry.Entity)).ToArray()).Failures.ToList();
            failures.AddRange(ValidateImportedEntityConstraints(
                this,
                governingDescriptor,
                entries,
                descriptorsBySection,
                entry => entry.Entity));
            return failures;
        }

        return ValidateDetachedPopulation(governingDescriptor, entries, descriptorsBySection);
    }

    private IReadOnlyList<ValidationFailure> ValidateDetachedPopulation(
        SchemaDescriptor governingDescriptor,
        IReadOnlyList<PopulationEntry> entries,
        IReadOnlyDictionary<DataSection, SchemaDescriptor> descriptorsBySection)
    {
        var failures = new List<ValidationFailure>();
        var detached = new ExchangeStructure(Header, _schemaDescriptors);
        var detachedSections = new Dictionary<DataSection, DataSection>(ReferenceEqualityComparer.Instance);
        foreach (var section in entries.Select(entry => entry.DataSection))
        {
            if (detachedSections.ContainsKey(section))
                continue;
            var clone = new DataSection(section.SchemaName, section.Name);
            detached.DataSections.Add(clone);
            detachedSections.Add(section, clone);
        }

        var clones = new Dictionary<Entity, Entity>(ReferenceEqualityComparer.Instance);
        var projections = new Dictionary<Entity, IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>>>(
            ReferenceEqualityComparer.Instance);
        foreach (var entry in entries)
        {
            try
            {
                var owner = descriptorsBySection[entry.DataSection];
                var components = owner.ProjectEntity(entry.Entity);
                var clone = owner.AllocateEntity(components.Select(component => component.Key).ToArray());
                if (components.Count == 0 || clone is null)
                {
                    failures.Add(new ValidationFailure(
                        "P21.POPULATION.PROJECTION",
                        entry.Path,
                        "The governing data-section descriptor cannot project and allocate the population entity."));
                    continue;
                }

                clones.Add(entry.Entity, clone);
                projections.Add(entry.Entity, components);
                _ = detached.Add(detachedSections[entry.DataSection], clone);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                failures.Add(new ValidationFailure(
                    "P21.POPULATION.PROJECTION",
                    entry.Path,
                    $"The entity cannot be projected into a schema-population view: {exception.Message}"));
            }
        }

        foreach (var entry in entries.Where(entry =>
                     clones.ContainsKey(entry.Entity)
                     && (!SchemaIdentifiersAssociate(
                             descriptorsBySection[entry.DataSection].Name,
                             governingDescriptor.Name)
                         || !projections[entry.Entity]
                             .SelectMany(component => component.Value)
                             .Any(ContainsDomainProjection))))
        {
            var owner = descriptorsBySection[entry.DataSection];
            var components = projections[entry.Entity]
                .Select(component => new KeyValuePair<string, IReadOnlyList<ParameterValue>>(
                    component.Key,
                    component.Value.Select((parameter, index) => RewritePopulationParameter(
                            parameter,
                            clones,
                            $"{entry.Path}.Parameters[{index}]"))
                        .ToArray()))
                .ToArray();
            foreach (var diagnostic in owner.HydrateEntity(detached, clones[entry.Entity], components))
            {
                if (diagnostic.Code == "P21-BIND-PARAMETER"
                    && TryGetHydrationParameterIndex(diagnostic.Message, out var parameterIndex))
                {
                    failures.Add(new ValidationFailure(
                        "P21.POPULATION.REFERENCE.UNSET",
                        $"{entry.Path}.Parameters[{parameterIndex}]",
                        "A required reference is outside the schema instance population and therefore behaves as unset."));
                }
                else
                {
                    failures.Add(new ValidationFailure(
                        "P21.POPULATION.HYDRATION",
                        entry.Path,
                        diagnostic.Message));
                }
            }
        }

        failures.AddRange(ValidateGoverningPopulationProjection(
            governingDescriptor,
            entries,
            descriptorsBySection));
        failures.AddRange(ValidateImportedEntityConstraints(
            detached,
            governingDescriptor,
            entries.Where(entry => clones.ContainsKey(entry.Entity)).ToArray(),
            descriptorsBySection,
            entry => clones[entry.Entity]));
        return failures;
    }

    private IReadOnlyList<ValidationFailure> ValidateGoverningPopulationProjection(
        SchemaDescriptor governingDescriptor,
        IReadOnlyList<PopulationEntry> entries,
        IReadOnlyDictionary<DataSection, SchemaDescriptor> descriptorsBySection)
    {
        var failures = new List<ValidationFailure>();
        var projected = new ExchangeStructure(Header, _schemaDescriptors);
        var projectedSections = new Dictionary<DataSection, DataSection>(ReferenceEqualityComparer.Instance);
        var projectedEntities = new Dictionary<Entity, Entity>(ReferenceEqualityComparer.Instance);
        var projectedComponents = new Dictionary<
            Entity,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>>>(ReferenceEqualityComparer.Instance);
        foreach (var entry in entries)
        {
            if (!projectedSections.TryGetValue(entry.DataSection, out var section))
            {
                section = new DataSection(governingDescriptor.Name, entry.DataSection.Name);
                projected.DataSections.Add(section);
                projectedSections.Add(entry.DataSection, section);
            }

            var owner = descriptorsBySection[entry.DataSection];
            var sourceComponents = owner.ProjectEntity(entry.Entity);
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components;
            if (SchemaIdentifiersAssociate(owner.Name, governingDescriptor.Name))
            {
                components = sourceComponents;
            }
            else
            {
                var candidates = _domainEquivalences.Where(equivalence =>
                        SchemaIdentifiersAssociate(equivalence.Source.SchemaName, governingDescriptor.Name)
                        && SchemaIdentifiersAssociate(equivalence.Target.SchemaName, owner.Name)
                        && sourceComponents.Any(component => string.Equals(
                            component.Key,
                            equivalence.Target.EntityName,
                            StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                if (candidates.Length != 1)
                {
                    failures.Add(new ValidationFailure(
                        "P21.POPULATION.DOMAIN_EQUIVALENCE",
                        entry.Path,
                        candidates.Length == 0
                            ? $"No explicit domain equivalence projects the entity into '{governingDescriptor.Name}'."
                            : $"More than one domain equivalence projects the entity into '{governingDescriptor.Name}'."));
                    continue;
                }

                var equivalence = candidates[0];
                var sourceComponent = sourceComponents.Single(component => string.Equals(
                    component.Key,
                    equivalence.Target.EntityName,
                    StringComparison.OrdinalIgnoreCase));
                if (!TryProjectDomainParameters(
                        equivalence,
                        sourceComponent.Value,
                        out var projectedParameters,
                        out var projectionError))
                {
                    failures.Add(new ValidationFailure(
                        "P21.POPULATION.DOMAIN_EQUIVALENCE",
                        entry.Path,
                        $"The domain-equivalence parameter projection failed: {projectionError}"));
                    continue;
                }
                components =
                [
                    new KeyValuePair<string, IReadOnlyList<ParameterValue>>(
                        equivalence.Source.EntityName.ToUpperInvariant(),
                        projectedParameters),
                ];
            }

            var clone = governingDescriptor.AllocateEntity(components.Select(component => component.Key).ToArray());
            if (components.Count == 0 || clone is null)
            {
                failures.Add(new ValidationFailure(
                    "P21.POPULATION.PROJECTION",
                    entry.Path,
                    $"The entity cannot be allocated in governing schema '{governingDescriptor.Name}'."));
                continue;
            }

            projectedEntities.Add(entry.Entity, clone);
            projectedComponents.Add(entry.Entity, components);
            _ = projected.Add(section, clone);
        }

        foreach (var entry in entries.Where(entry => projectedEntities.ContainsKey(entry.Entity)))
        {
            var components = projectedComponents[entry.Entity]
                .Select(component => new KeyValuePair<string, IReadOnlyList<ParameterValue>>(
                    component.Key,
                    component.Value.Select((parameter, index) => RewritePopulationParameter(
                            parameter,
                            projectedEntities,
                            $"{entry.Path}.Parameters[{index}]")).ToArray()))
                .ToArray();
            foreach (var diagnostic in governingDescriptor.HydrateEntity(
                         projected,
                         projectedEntities[entry.Entity],
                         components))
            {
                failures.Add(new ValidationFailure(
                    "P21.POPULATION.PROJECTION",
                    entry.Path,
                    diagnostic.Message));
            }
        }

        failures.AddRange(governingDescriptor.Validate(
            projected,
            entries.Where(entry => projectedEntities.ContainsKey(entry.Entity))
                .Select(entry => new KeyValuePair<string, Entity>(entry.Path, projectedEntities[entry.Entity]))
                .ToArray()).Failures);
        return failures;
    }

    private static IReadOnlyList<ValidationFailure> ValidateImportedEntityConstraints(
        ExchangeStructure populationStructure,
        SchemaDescriptor governingDescriptor,
        IReadOnlyList<PopulationEntry> entries,
        IReadOnlyDictionary<DataSection, SchemaDescriptor> descriptorsBySection,
        Func<PopulationEntry, Entity> selectEntity)
    {
        var failures = new List<ValidationFailure>();
        foreach (var group in entries
                     .Where(entry => !descriptorsBySection[entry.DataSection].Name.Equals(governingDescriptor.Name))
                     .GroupBy(entry => descriptorsBySection[entry.DataSection]))
        {
            failures.AddRange(group.Key.ValidateEntityPopulation(
                populationStructure,
                group.Select(entry => new KeyValuePair<string, Entity>(entry.Path, selectEntity(entry))).ToArray()).Failures);
        }

        return failures;
    }

    private ParameterValue RewritePopulationParameter(
        ParameterValue value,
        IReadOnlyDictionary<Entity, Entity> clones,
        string path)
    {
        if (value.TryGetEntity(out var entity))
        {
            if (clones.TryGetValue(entity, out var clone))
                return ParameterValue.FromEntity(clone);
            if (_domainProjectionSources.TryGetValue(entity!, out var source)
                && clones.TryGetValue(source, out clone))
            {
                return ParameterValue.FromEntity(clone);
            }

            return ParameterValue.Omitted;
        }

        if (value.TryGetAggregate(out var values))
        {
            return ParameterValue.FromAggregate(values.Select((item, index) => RewritePopulationParameter(
                item,
                clones,
                $"{path}[{index}]")));
        }

        if (value.TryGetTyped(out var typeName, out var inner))
            return ParameterValue.FromTyped(typeName, RewritePopulationParameter(inner, clones, path));
        return value;
    }

    private bool ContainsDomainProjection(ParameterValue value)
    {
        if (value.TryGetEntity(out var entity))
            return _domainProjectionSources.ContainsKey(entity!);
        if (value.TryGetAggregate(out var values))
            return values.Any(ContainsDomainProjection);
        return value.TryGetTyped(out _, out var inner) && ContainsDomainProjection(inner);
    }

    private static bool TryGetHydrationParameterIndex(string message, out int parameterIndex)
    {
        const string marker = " parameter ";
        var start = message.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            parameterIndex = -1;
            return false;
        }

        start += marker.Length;
        var end = message.IndexOf(' ', start);
        parameterIndex = -1;
        return end > start && int.TryParse(
            message.AsSpan(start, end - start),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out parameterIndex);
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

    private sealed record PopulationEntry(
        string Path,
        Entity Entity,
        DataSection DataSection,
        ExchangeStructure Owner);
}

internal sealed class EntityRegistration(EntityInstanceName name, Entity entity, DataSection dataSection)
{
    internal EntityInstanceName Name { get; } = name;

    internal Entity Entity { get; } = entity;

    internal DataSection DataSection { get; } = dataSection;
}