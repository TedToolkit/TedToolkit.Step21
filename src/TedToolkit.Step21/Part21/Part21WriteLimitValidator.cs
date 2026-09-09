namespace TedToolkit.Step21;

/// <summary>Applies shared item and nesting limits to a projected write graph before recursive validation or formatting.</summary>
internal static class Part21WriteLimitValidator
{
    internal static Part21WriteGraphFeatures Validate(
        ExchangeStructure structure,
        int projectedEntityCount,
        IEnumerable<IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>>> projectedEntities,
        Part21ProcessingLimits limits,
        bool detectsHighStrings)
    {
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(projectedEntities);
        ArgumentNullException.ThrowIfNull(limits);

        var usesClass3Occurrence = false;
        var containsHighString = false;
        var containsInvalidParameterValue = false;
        var header = structure.Header;
        CheckStrings(
            header.FileDescription.Description,
            limits,
            "FILE_DESCRIPTION item",
            detectsHighStrings,
            ref containsHighString);
        CheckStrings(
            header.FileName.Author,
            limits,
            "FILE_NAME author",
            detectsHighStrings,
            ref containsHighString);
        CheckStrings(
            header.FileName.Organization,
            limits,
            "FILE_NAME organization",
            detectsHighStrings,
            ref containsHighString);
        CheckStrings(
            header.FileSchema.SchemaIdentifiers,
            limits,
            "FILE_SCHEMA identifier",
            detectsHighStrings,
            ref containsHighString);
        if (detectsHighStrings
            && (ContainsHighCodePoint(header.FileDescription.ImplementationLevel)
                || ContainsHighCodePoint(header.FileName.Name)
                || ContainsHighCodePoint(header.FileName.TimeStamp)
                || ContainsHighCodePoint(header.FileName.PreprocessorVersion)
                || ContainsHighCodePoint(header.FileName.OriginatingSystem)
                || ContainsHighCodePoint(header.FileName.Authorization)))
        {
            containsHighString = true;
        }
        CheckCount(structure.DataSections.Count, limits, "data section");
        CheckCount(structure.AnchorEntries.Count, limits, "anchor");
        CheckCount(structure.ReferenceEntries.Count, limits, "reference");
        CheckCount(structure.SchemaPopulations.Count, limits, "FILE_POPULATION declaration");
        CheckCount(structure.SchemaPopulationExternalFiles.Count, limits, "SCHEMA_POPULATION external file");
        CheckCount(structure.SectionLanguageEntries.Count, limits, "SECTION_LANGUAGE declaration");
        CheckCount(structure.SectionContextEntries.Count, limits, "SECTION_CONTEXT declaration");
        CheckCount(structure.UserDefinedHeaderEntityEntries.Count, limits, "user-defined header entity");
        CheckCount(
            (long)structure.SchemaPopulations.Count
            + structure.SchemaPopulationExternalFiles.Count
            + structure.SectionLanguageEntries.Count
            + structure.SectionContextEntries.Count
            + structure.UserDefinedHeaderEntityEntries.Count,
            limits,
            "additional header entity");

        if ((long)projectedEntityCount + structure.ReferenceEntries.Count > limits.MaximumItemCount)
            ThrowItemLimit("instance name");

        foreach (var section in structure.DataSections)
        {
            if (detectsHighStrings && section.Name is not null && ContainsHighCodePoint(section.Name))
                containsHighString = true;
        }

        foreach (var external in structure.SchemaPopulationExternalFiles)
        {
            if (detectsHighStrings
                && (ContainsHighCodePoint(external.Location.OriginalString)
                    || external.TimeStamp is not null && ContainsHighCodePoint(external.TimeStamp)
                    || external.MessageDigest is not null && ContainsHighCodePoint(external.MessageDigest)))
            {
                containsHighString = true;
            }
        }

        foreach (var population in structure.SchemaPopulations)
        {
            CheckCount(population.ExplicitSectionNames?.Count ?? 0, limits, "governed data-section name");
            if (detectsHighStrings
                && (ContainsHighCodePoint(population.SchemaName.Value)
                    || population.ExplicitSectionNames?.Any(ContainsHighCodePoint) == true))
            {
                containsHighString = true;
            }
        }

        foreach (var declaration in structure.SectionLanguageEntries)
        {
            if (detectsHighStrings
                && declaration.SectionName is not null
                && ContainsHighCodePoint(declaration.SectionName))
            {
                containsHighString = true;
            }
        }

        foreach (var declaration in structure.SectionContextEntries)
        {
            CheckStrings(
                declaration.ContextIdentifiers,
                limits,
                "SECTION_CONTEXT identifier",
                detectsHighStrings,
                ref containsHighString);
            if (detectsHighStrings
                && declaration.SectionName is not null
                && ContainsHighCodePoint(declaration.SectionName))
            {
                containsHighString = true;
            }
        }

        Stack<(ParameterValue Value, int Depth)>? pending = null;
        Part21WriteOccurrenceIndex? occurrenceIndex = null;
        foreach (var entity in structure.UserDefinedHeaderEntityEntries)
        {
            CheckCount(entity.Parameters.Count, limits, "user-defined header parameter");
            foreach (var value in entity.Parameters)
            {
                ValidateValue(
                    value,
                    limits,
                    detectsHighStrings,
                    structure,
                    ref usesClass3Occurrence,
                    ref containsHighString,
                    ref containsInvalidParameterValue,
                    ref pending,
                    ref occurrenceIndex);
            }
        }

        foreach (var anchor in structure.AnchorEntries)
        {
            CheckCount(anchor.Tags.Count, limits, "anchor tag");
            ValidateValue(
                anchor.Item,
                limits,
                detectsHighStrings,
                structure,
                ref usesClass3Occurrence,
                ref containsHighString,
                ref containsInvalidParameterValue,
                ref pending,
                ref occurrenceIndex);
            foreach (var tag in anchor.Tags)
            {
                ValidateValue(
                    tag.Item,
                    limits,
                    detectsHighStrings,
                    structure,
                    ref usesClass3Occurrence,
                    ref containsHighString,
                    ref containsInvalidParameterValue,
                    ref pending,
                    ref occurrenceIndex);
            }
        }

        ValidateProjectedEntities(
            projectedEntities,
            limits,
            detectsHighStrings,
            structure,
            ref usesClass3Occurrence,
            ref containsHighString,
            ref containsInvalidParameterValue,
            ref pending,
            ref occurrenceIndex);

        return new Part21WriteGraphFeatures(
            usesClass3Occurrence,
            containsHighString,
            containsInvalidParameterValue);
    }

    internal static Part21WriteGraphFeatures ValidateEntity(
        ExchangeStructure structure,
        IEnumerable<IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>>> projectedEntities,
        Part21ProcessingLimits limits)
    {
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(projectedEntities);
        ArgumentNullException.ThrowIfNull(limits);

        var usesClass3Occurrence = false;
        var containsHighString = false;
        var containsInvalidParameterValue = false;
        Stack<(ParameterValue Value, int Depth)>? pending = null;
        Part21WriteOccurrenceIndex? occurrenceIndex = null;
        ValidateProjectedEntities(
            projectedEntities,
            limits,
            detectsHighStrings: false,
            structure,
            ref usesClass3Occurrence,
            ref containsHighString,
            ref containsInvalidParameterValue,
            ref pending,
            ref occurrenceIndex);
        return new Part21WriteGraphFeatures(
            usesClass3Occurrence,
            containsHighString,
            containsInvalidParameterValue);
    }

    private static void ValidateProjectedEntities(
        IEnumerable<IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>>> projectedEntities,
        Part21ProcessingLimits limits,
        bool detectsHighStrings,
        ExchangeStructure structure,
        ref bool usesClass3Occurrence,
        ref bool containsHighString,
        ref bool containsInvalidParameterValue,
        ref Stack<(ParameterValue Value, int Depth)>? pending,
        ref Part21WriteOccurrenceIndex? occurrenceIndex)
    {
        foreach (var components in projectedEntities)
        {
            CheckCount(components.Count, limits, "entity component");
            foreach (var component in components)
            {
                CheckCount(component.Value.Count, limits, "entity parameter");
                foreach (var value in component.Value)
                {
                    ValidateValue(
                        value,
                        limits,
                        detectsHighStrings,
                        structure,
                        ref usesClass3Occurrence,
                        ref containsHighString,
                        ref containsInvalidParameterValue,
                        ref pending,
                        ref occurrenceIndex);
                }
            }
        }
    }

    private static void ValidateValue(
        ParameterValue? root,
        Part21ProcessingLimits limits,
        bool detectsHighStrings,
        ExchangeStructure structure,
        ref bool usesClass3Occurrence,
        ref bool containsHighString,
        ref bool containsInvalidParameterValue,
        ref Stack<(ParameterValue Value, int Depth)>? pending,
        ref Part21WriteOccurrenceIndex? occurrenceIndex)
    {
        if (root is null)
        {
            containsInvalidParameterValue = true;
            return;
        }

        if (root.Kind is not (ParameterValueKind.Aggregate or ParameterValueKind.Typed))
        {
            Inspect(
                root,
                detectsHighStrings,
                structure,
                ref usesClass3Occurrence,
                ref containsHighString,
                ref containsInvalidParameterValue,
                ref occurrenceIndex);
            return;
        }

        pending ??= new Stack<(ParameterValue Value, int Depth)>();
        pending.Push((root, 0));
        while (pending.TryPop(out var entry))
        {
            Inspect(
                entry.Value,
                detectsHighStrings,
                structure,
                ref usesClass3Occurrence,
                ref containsHighString,
                ref containsInvalidParameterValue,
                ref occurrenceIndex);
            var nextDepth = entry.Value.Kind is ParameterValueKind.Aggregate or ParameterValueKind.Typed
                ? entry.Depth + 1
                : entry.Depth;
            if (nextDepth > limits.MaximumNestingDepth)
            {
                throw new ExchangeStructureCapabilityException([
                    new Step21Diagnostic(
                        "P21-PROCESSING-LIMIT-NESTING",
                        Step21DiagnosticSeverity.Error,
                        "The configured nested-value depth limit was exceeded."),
                ]);
            }

            if (entry.Value.TryGetAggregate(out var aggregate))
            {
                CheckCount(aggregate.Count, limits, "aggregate element");
                for (var index = aggregate.Count - 1; index >= 0; index--)
                    pending.Push((aggregate[index], nextDepth));
            }
            else if (entry.Value.TryGetTyped(out _, out var inner))
            {
                pending.Push((inner, nextDepth));
            }
        }
    }

    private static void Inspect(
        ParameterValue value,
        bool detectsHighStrings,
        ExchangeStructure structure,
        ref bool usesClass3Occurrence,
        ref bool containsHighString,
        ref bool containsInvalidParameterValue,
        ref Part21WriteOccurrenceIndex? occurrenceIndex)
    {
        if (value.Kind is ParameterValueKind.ValueInstance
            or ParameterValueKind.ConstantEntity
            or ParameterValueKind.ConstantValue)
        {
            usesClass3Occurrence = true;
        }

        if (detectsHighStrings
            && value.TryGetString(out var text)
            && text.AsSpan().ContainsAnyExceptInRange('\0', '\x7f'))
        {
            containsHighString = true;
        }

        if (value.Kind == ParameterValueKind.Resource)
            containsInvalidParameterValue = true;
        else if (value.TryGetEntity(out var entity) && !structure.TryGetName(entity, out _))
            containsInvalidParameterValue = true;
        else if (value.TryGetEntityInstance(out var entityName)
            && !(occurrenceIndex ??= new Part21WriteOccurrenceIndex(structure)).DefinesEntity(entityName))
        {
            containsInvalidParameterValue = true;
        }
        else if (value.TryGetValueInstance(out var valueName)
            && !(occurrenceIndex ??= new Part21WriteOccurrenceIndex(structure)).DefinesValue(valueName))
        {
            containsInvalidParameterValue = true;
        }
    }

    private static void CheckCount(long count, Part21ProcessingLimits limits, string name)
    {
        if (count > limits.MaximumItemCount)
            ThrowItemLimit(name);
    }

    private static void CheckStrings(
        IReadOnlyList<string> values,
        Part21ProcessingLimits limits,
        string name,
        bool detectsHighStrings,
        ref bool containsHighString)
    {
        CheckCount(values.Count, limits, name);
        if (detectsHighStrings && !containsHighString && values.Any(ContainsHighCodePoint))
            containsHighString = true;
    }

    private static bool ContainsHighCodePoint(string value) =>
        value.AsSpan().ContainsAnyExceptInRange('\0', '\x7f');

    private static void ThrowItemLimit(string name) =>
        throw new ExchangeStructureCapabilityException([
            new Step21Diagnostic(
                "P21-PROCESSING-LIMIT-ITEM",
                Step21DiagnosticSeverity.Error,
                $"The configured {name} count limit was exceeded."),
        ]);
}

internal sealed class Part21WriteOccurrenceIndex
{
    private readonly ExchangeStructure _structure;
    private HashSet<EntityInstanceName>? _externalEntities;
    private HashSet<ValueInstanceName>? _externalValues;

    internal Part21WriteOccurrenceIndex(ExchangeStructure structure)
    {
        _structure = structure;
        foreach (var reference in structure.ReferenceEntries)
        {
            if (reference.TryGetEntityInstance(out var entityName))
                (_externalEntities ??= []).Add(entityName);
            else if (reference.TryGetValueInstance(out var valueName))
                (_externalValues ??= []).Add(valueName);
        }
    }

    internal bool DefinesEntity(EntityInstanceName name) =>
        _structure.TryGetEntity(name, out _) || _externalEntities?.Contains(name) == true;

    internal bool DefinesValue(ValueInstanceName name) => _externalValues?.Contains(name) == true;
}

internal readonly record struct Part21WriteGraphFeatures(
    bool UsesClass3Occurrence,
    bool ContainsHighString,
    bool ContainsInvalidParameterValue);
