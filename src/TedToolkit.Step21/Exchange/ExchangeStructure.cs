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
    private readonly List<EntityRegistration> _registrations = [];
    private readonly ReadOnlyCollection<EntityRegistration> _registrationView;
    private readonly ReadOnlyCollection<SchemaDescriptor> _schemaDescriptors;
    private readonly ReadOnlyDictionary<SchemaName, SchemaDescriptor> _schemaDescriptorsByName;
    private IReadOnlyList<SchemaPopulationDefinition> _schemaPopulations = Array.Empty<SchemaPopulationDefinition>();
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

    /// <summary>
    /// Gets the mutable ISO data-section collection. Editing this list performs no validation or registration repair.
    /// </summary>
    public IList<DataSection> DataSections { get; }

    /// <summary>
    /// Gets a live read-only enumeration of registered entities in deterministic registration order.
    /// </summary>
    /// <remarks>
    /// The view exposes model values for navigation without exposing the structure's occurrence-name or section
    /// registration indexes. Adding or removing registrations changes subsequent enumerations.
    /// </remarks>
    public IEnumerable<Entity> Entities => _registrations.Select(registration => registration.Entity);

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
                failures.Add(sectionFailure);
        }

        failures.AddRange(ValidateSchemaPopulations(
            dataSections,
            sectionIndexes,
            descriptorsBySection,
            entitiesBySection));

        failures.AddRange(relationshipFailures);
        return new ValidationResult(failures);
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

    internal IReadOnlyList<SchemaPopulationDefinition> SchemaPopulations => _schemaPopulations;

    internal IReadOnlyList<EntityRegistration> Registrations => _registrationView;

    internal bool TryGetSchemaDescriptor(SchemaName name, out SchemaDescriptor? descriptor) =>
        _schemaDescriptorsByName.TryGetValue(name, out descriptor);

    internal void SetSchemaPopulations(IReadOnlyList<SchemaPopulationDefinition> populations)
    {
        ArgumentNullException.ThrowIfNull(populations);
        _schemaPopulations = Array.AsReadOnly(populations.ToArray());
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
        IReadOnlyDictionary<DataSection, List<KeyValuePair<string, Entity>>> entitiesBySection)
    {
        var failures = new List<ValidationFailure>();
        var validSections = dataSections
            .Select((section, index) => new { Section = section, Index = index, })
            .Where(item => item.Section is not null
                && sectionIndexes.TryGetValue(item.Section, out var firstIndex)
                && firstIndex == item.Index
                && descriptorsBySection.ContainsKey(item.Section))
            .Select(item => item.Section!)
            .ToArray();
        var allEntries = validSections
            .SelectMany(section => entitiesBySection[section]
                .Select(entry => new PopulationEntry(entry.Key, entry.Value, section)))
            .ToArray();
        var claimedSections = new HashSet<DataSection>(ReferenceEqualityComparer.Instance);
        foreach (var definition in _schemaPopulations)
        {
            var inputSections = definition.InputSections
                .Where(section => sectionIndexes.ContainsKey(section) && descriptorsBySection.ContainsKey(section))
                .ToArray();
            foreach (var section in definition.InputSections.Where(section => !sectionIndexes.ContainsKey(section)))
            {
                failures.Add(new ValidationFailure(
                    "P21.STRUCTURE.SCHEMA_POPULATION.SECTION",
                    $"SchemaPopulations[{definition.SchemaName}]",
                    $"Input data section '{section.Name}' is no longer present in the structure."));
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

            var selected = SelectPopulationEntries(definition.Determination, inputSections, allEntries, descriptor);
            failures.AddRange(ValidatePopulation(descriptor, selected, descriptorsBySection));
        }

        foreach (var section in validSections.Where(section => !claimedSections.Contains(section)))
        {
            failures.AddRange(ValidatePopulation(
                descriptorsBySection[section],
                entitiesBySection[section]
                    .Select(entry => new PopulationEntry(entry.Key, entry.Value, section))
                    .ToArray(),
                descriptorsBySection));
        }

        return failures;
    }

    private PopulationEntry[] SelectPopulationEntries(
        SchemaPopulationDetermination determination,
        IReadOnlyCollection<DataSection> inputSections,
        IReadOnlyList<PopulationEntry> allEntries,
        SchemaDescriptor descriptor)
    {
        var inputSet = new HashSet<DataSection>(inputSections, ReferenceEqualityComparer.Instance);
        var selected = new HashSet<Entity>(ReferenceEqualityComparer.Instance);
        foreach (var entry in allEntries.Where(entry => inputSet.Contains(entry.DataSection)))
            selected.Add(entry.Entity);

        if (determination == SchemaPopulationDetermination.IncludeReferenced)
        {
            foreach (var entry in allEntries.Where(entry => inputSet.Contains(entry.DataSection)))
            {
                foreach (var reference in entry.Entity.DirectReferences)
                {
                    if (reference is not null && _registrationsByEntity.ContainsKey(reference))
                        selected.Add(reference);
                }
            }
        }
        else if (determination == SchemaPopulationDetermination.IncludeAllCompatible)
        {
            foreach (var entry in allEntries.Where(entry => !inputSet.Contains(entry.DataSection)
                         && descriptor.IsEntityReferenceCompatible(entry.Entity)))
            {
                selected.Add(entry.Entity);
            }
        }

        return allEntries.Where(entry => selected.Contains(entry.Entity)).ToArray();
    }

    private IReadOnlyList<ValidationFailure> ValidatePopulation(
        SchemaDescriptor governingDescriptor,
        IReadOnlyList<PopulationEntry> entries,
        IReadOnlyDictionary<DataSection, SchemaDescriptor> descriptorsBySection)
    {
        var selected = new HashSet<Entity>(entries.Select(entry => entry.Entity), ReferenceEqualityComparer.Instance);
        var hasOutsideReference = entries.Any(entry => entry.Entity.DirectReferences.Any(reference =>
            reference is not null
            && _registrationsByEntity.ContainsKey(reference)
            && !selected.Contains(reference)));
        if (!hasOutsideReference)
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
                detached.Add(
                    detachedSections[entry.DataSection],
                    _registrationsByEntity[entry.Entity].Name,
                    clone);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                failures.Add(new ValidationFailure(
                    "P21.POPULATION.PROJECTION",
                    entry.Path,
                    $"The entity cannot be projected into a schema-population view: {exception.Message}"));
            }
        }

        foreach (var entry in entries.Where(entry => clones.ContainsKey(entry.Entity)))
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

        failures.AddRange(governingDescriptor.Validate(
            detached,
            entries.Where(entry => clones.ContainsKey(entry.Entity))
                .Select(entry => new KeyValuePair<string, Entity>(entry.Path, clones[entry.Entity]))
                .ToArray()).Failures);
        failures.AddRange(ValidateImportedEntityConstraints(
            detached,
            governingDescriptor,
            entries.Where(entry => clones.ContainsKey(entry.Entity)).ToArray(),
            descriptorsBySection,
            entry => clones[entry.Entity]));
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

    private static ParameterValue RewritePopulationParameter(
        ParameterValue value,
        IReadOnlyDictionary<Entity, Entity> clones,
        string path)
    {
        if (value.TryGetEntity(out var entity))
        {
            if (clones.TryGetValue(entity, out var clone))
                return ParameterValue.FromEntity(clone);

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

    private sealed record PopulationEntry(string Path, Entity Entity, DataSection DataSection);
}

internal sealed class EntityRegistration(EntityInstanceName name, Entity entity, DataSection dataSection)
{
    internal EntityInstanceName Name { get; } = name;

    internal Entity Entity { get; } = entity;

    internal DataSection DataSection { get; } = dataSection;
}