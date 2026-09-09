namespace TedToolkit.Step21.Tests.ExchangeStructureTests;

/// <summary>
/// Proves final-state validation timing, complete pre-write evidence, and zero-output failures.
/// </summary>
public sealed class AtomicPrewriteValidationTests
{
    /// <summary>Defers validation across repeated edits and observes only the final graph at each write boundary.</summary>
    [Test]
    public async Task Should_validate_once_only_after_edits_reach_the_write_boundary()
    {
        var descriptor = new ProbeDescriptor("prewrite");
        var structure = CreateStructure(descriptor);
        var section = new DataSection(descriptor.Name);
        structure.DataSections.Add(section);
        var entity = new ProbeEntity("ITEM", "temporary", [1]);

        structure.Add(section, entity);
        entity.Text = null;
        entity.Values.Clear();
        _ = structure.Remove(entity);
        structure.Add(section, entity);
        entity.Text = "final";
        entity.Values.Add(2);

        await Assert.That(descriptor.ValidationCount).IsEqualTo(0);
        var validDestination = new ProbeTextWriter();
        structure.Write(validDestination);

        entity.Text = null;
        entity.Values.Clear();
        var invalidDestination = new ProbeTextWriter();
        var exception = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.Write(invalidDestination));

        using (Assert.Multiple())
        {
            await Assert.That(descriptor.ValidationCount).IsEqualTo(2);
            await Assert.That(validDestination.ToString()).Contains("#1=ITEM('final',(2));");
            await Assert.That(exception.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(["TEST.TEXT.REQUIRED", "TEST.VALUES.LOWER_BOUND"]);
            await Assert.That(invalidDestination.WriteCount).IsEqualTo(0);
        }
    }

    /// <summary>Combines whole-graph failures with an unregistered WriteEntity target.</summary>
    [Test]
    public async Task Should_aggregate_unregistered_entity_and_graph_failures_before_write_entity_output()
    {
        var descriptor = new ProbeDescriptor("prewrite");
        var structure = CreateStructure(descriptor);
        var section = new DataSection(descriptor.Name);
        structure.DataSections.Add(section);
        structure.Add(section, new ProbeEntity("ITEM", null, []));
        var destination = new ProbeTextWriter();

        var exception = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.WriteEntity(destination, new ProbeEntity("ITEM", "outside", [1])));

        using (Assert.Multiple())
        {
            await Assert.That(exception.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo([
                    "TEST.TEXT.REQUIRED",
                    "TEST.VALUES.LOWER_BOUND",
                    "P21.WRITE.ENTITY.REGISTRATION",
                ]);
            await Assert.That(destination.WriteCount).IsEqualTo(0);
            await Assert.That(descriptor.ValidationCount).IsEqualTo(1);
        }
    }

    /// <summary>Returns foreign, aggregate, property, and dangling-reference failures in one zero-output result.</summary>
    [Test]
    public async Task Should_aggregate_multiple_final_graph_failure_categories_before_output()
    {
        var descriptor = new ProbeDescriptor("prewrite");
        var structure = CreateStructure(descriptor);
        var section = new DataSection(descriptor.Name);
        structure.DataSections.Add(section);
        var target = new ProbeEntity("TARGET", "target", [1]);
        var root = new ProbeEntity(
            "ITEM",
            null,
            [],
            directReferences: [target]);
        structure.Add(section, root);
        _ = structure.Remove(target);
        structure.Add(section, new ForeignEntity());
        var destination = new ProbeTextWriter();

        var exception = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.Write(destination));

        using (Assert.Multiple())
        {
            await Assert.That(exception.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo([
                    "TEST.TEXT.REQUIRED",
                    "TEST.VALUES.LOWER_BOUND",
                    "TEST.ENTITY.FOREIGN",
                    "P21.STRUCTURE.REFERENCE.REGISTRATION",
                ]);
            await Assert.That(destination.WriteCount).IsEqualTo(0);
            await Assert.That(descriptor.ValidationCount).IsEqualTo(1);
        }
    }

    /// <summary>Aggregates every unregistered reference exposed by physical projection before complete output.</summary>
    [Test]
    public async Task Should_aggregate_projected_reference_failures_before_complete_output()
    {
        var descriptor = new ProbeDescriptor("prewrite");
        var structure = CreateStructure(descriptor);
        var section = new DataSection(descriptor.Name);
        structure.DataSections.Add(section);
        var firstMissing = new ProbeEntity("TARGET", "missing-1", [1]);
        var secondMissing = new ProbeEntity("TARGET", "missing-2", [1]);
        var thirdMissing = new ProbeEntity("TARGET", "missing-3", [1]);
        var first = new ProbeEntity(
            "ITEM",
            "first",
            [1],
            [ParameterValue.FromAggregate([
                ParameterValue.FromEntity(firstMissing),
                ParameterValue.FromTyped("REF", ParameterValue.FromEntity(secondMissing)),
            ])]);
        structure.Add(section, first);
        structure.Add(section, new ProbeEntity(
            "ITEM",
            "second",
            [2],
            [ParameterValue.FromEntity(thirdMissing)]));
        var destination = new ProbeTextWriter();

        var exception = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.Write(destination));
        var entityDestination = new ProbeTextWriter();
        var entityException = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.WriteEntity(entityDestination, first));

        using (Assert.Multiple())
        {
            await Assert.That(exception.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo([
                    "P21.WRITE.REFERENCE.REGISTRATION",
                    "P21.WRITE.REFERENCE.REGISTRATION",
                    "P21.WRITE.REFERENCE.REGISTRATION",
                ]);
            await Assert.That(exception.ValidationResult.Failures.Select(failure => failure.Path)
                .SequenceEqual([
                    "DataSections[0].#1.Parameters[2][0]",
                    "DataSections[0].#1.Parameters[2][1].Value",
                    "DataSections[0].#2.Parameters[2]",
                ])).IsTrue();
            await Assert.That(entityException.ValidationResult.Failures.Select(failure => failure.Path)
                .SequenceEqual([
                    "DataSections[0].#1.Parameters[2][0]",
                    "DataSections[0].#1.Parameters[2][1].Value",
                ])).IsTrue();
            await Assert.That(destination.WriteCount).IsEqualTo(0);
            await Assert.That(entityDestination.WriteCount).IsEqualTo(0);
            await Assert.That(descriptor.ValidationCount).IsEqualTo(2);
        }
    }

    /// <summary>Preserves structured zero-output validation when a physical projection contains null.</summary>
    [Test]
    public async Task Should_report_a_null_projected_parameter_before_either_writer_outputs()
    {
        var descriptor = new ProbeDescriptor("prewrite");
        var structure = CreateStructure(descriptor);
        var section = new DataSection(descriptor.Name);
        structure.DataSections.Add(section);
        var entity = new ProbeEntity("ITEM", "valid", [1], [null!]);
        structure.Add(section, entity);
        var completeDestination = new ProbeTextWriter();
        var entityDestination = new ProbeTextWriter();

        var complete = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.Write(completeDestination));
        var record = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.WriteEntity(entityDestination, entity));

        using (Assert.Multiple())
        {
            await Assert.That(complete.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(["P21.WRITE.PARAMETER.REQUIRED"]);
            await Assert.That(record.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(["P21.WRITE.PARAMETER.REQUIRED"]);
            await Assert.That(complete.ValidationResult.Failures.Single().Path)
                .IsEqualTo("DataSections[0].#1.Parameters[2]");
            await Assert.That(record.ValidationResult.Failures.Single().Path)
                .IsEqualTo("DataSections[0].#1.Parameters[2]");
            await Assert.That(completeDestination.WriteCount).IsEqualTo(0);
            await Assert.That(entityDestination.WriteCount).IsEqualTo(0);
        }
    }

    /// <summary>Rejects ANCHOR-only resource tokens when a schema projects them into DATA parameters.</summary>
    [Test]
    public async Task Should_reject_projected_resources_before_either_writer_outputs()
    {
        var descriptor = new ProbeDescriptor("prewrite");
        var structure = CreateStructure(descriptor, "4;1");
        var section = new DataSection(descriptor.Name);
        structure.DataSections.Add(section);
        var entity = new ProbeEntity(
            "ITEM",
            "valid",
            [1],
            [ParameterValue.FromAggregate([
                ParameterValue.FromTyped(
                    "WRAPPED",
                    ParameterValue.FromResource(new Part21Resource("asset.p21"))),
            ])]);
        structure.Add(section, entity);
        var completeDestination = new ProbeTextWriter();
        var entityDestination = new ProbeTextWriter();

        var complete = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.Write(completeDestination));
        var record = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.WriteEntity(entityDestination, entity));

        using (Assert.Multiple())
        {
            await Assert.That(complete.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(["P21.WRITE.PARAMETER.KIND"]);
            await Assert.That(record.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(["P21.WRITE.PARAMETER.KIND"]);
            await Assert.That(complete.ValidationResult.Failures.Single().Path)
                .IsEqualTo("DataSections[0].#1.Parameters[2][0].Value");
            await Assert.That(record.ValidationResult.Failures.Single().Path)
                .IsEqualTo("DataSections[0].#1.Parameters[2][0].Value");
            await Assert.That(completeDestination.WriteCount).IsEqualTo(0);
            await Assert.That(entityDestination.WriteCount).IsEqualTo(0);
        }
    }

    /// <summary>Rejects projected occurrence names that have no local or REFERENCE definition.</summary>
    [Test]
    public async Task Should_report_undefined_projected_occurrences_before_output()
    {
        var descriptor = new ProbeDescriptor("prewrite");
        var structure = CreateStructure(descriptor, "4;3");
        var section = new DataSection(descriptor.Name);
        structure.DataSections.Add(section);
        var entity = new ProbeEntity(
            "ITEM",
            "valid",
            [1],
            [
                ParameterValue.FromEntityInstance(new EntityInstanceName("98")),
                ParameterValue.FromValueInstance(new ValueInstanceName("99")),
            ]);
        structure.Add(section, entity);
        var completeDestination = new ProbeTextWriter();
        var entityDestination = new ProbeTextWriter();

        var complete = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.Write(completeDestination));
        var record = Assert.Throws<ExchangeStructureWriteValidationException>(() =>
            structure.WriteEntity(entityDestination, entity));

        using (Assert.Multiple())
        {
            await Assert.That(complete.ValidationResult.Failures.Select(failure => failure.Code))
                .IsEquivalentTo([
                    "P21.WRITE.REFERENCE.OCCURRENCE",
                    "P21.WRITE.REFERENCE.OCCURRENCE",
                ]);
            await Assert.That(record.ValidationResult.Failures.Select(failure => failure.Path).SequenceEqual([
                "DataSections[0].#1.Parameters[2]",
                "DataSections[0].#1.Parameters[3]",
            ])).IsTrue();
            await Assert.That(completeDestination.WriteCount).IsEqualTo(0);
            await Assert.That(entityDestination.WriteCount).IsEqualTo(0);
        }
    }

    /// <summary>Accepts projected occurrence names backed by the structure's REFERENCE definitions.</summary>
    [Test]
    public async Task Should_accept_defined_projected_occurrences()
    {
        var descriptor = new ProbeDescriptor("prewrite");
        var structure = CreateStructure(descriptor, "4;3");
        var section = new DataSection(descriptor.Name);
        structure.DataSections.Add(section);
        var entity = new ProbeEntity(
            "ITEM",
            "valid",
            [1],
            [
                ParameterValue.FromEntityInstance(new EntityInstanceName("98")),
                ParameterValue.FromValueInstance(new ValueInstanceName("99")),
            ]);
        structure.Add(section, entity);
        structure.References.Add(new Part21Reference(
            new EntityInstanceName("98"),
            new Part21Resource("external.p21#entity")));
        structure.References.Add(new Part21Reference(
            new ValueInstanceName("99"),
            new Part21Resource("external.p21#value")));
        var completeDestination = new ProbeTextWriter();
        var entityDestination = new ProbeTextWriter();

        structure.Write(completeDestination);
        structure.WriteEntity(entityDestination, entity);

        using (Assert.Multiple())
        {
            await Assert.That(completeDestination.ToString()).Contains("#1=ITEM('valid',(1),#98,@99);");
            await Assert.That(entityDestination.ToString()).IsEqualTo("#1=ITEM('valid',(1),#98,@99);");
            await Assert.That(completeDestination.WriteCount).IsEqualTo(1);
            await Assert.That(entityDestination.WriteCount).IsEqualTo(1);
        }
    }

    /// <summary>Returns complete capability evidence before either public writer touches its destination.</summary>
    [Test]
    public async Task Should_preflight_all_capability_diagnostics_for_both_writer_entry_points()
    {
        var descriptor = new ProbeDescriptor("prewrite", emitCapabilities: true);
        var structure = CreateStructure(descriptor);
        var section = new DataSection(descriptor.Name);
        structure.DataSections.Add(section);
        var entity = new ProbeEntity("ITEM", "valid", [1]);
        structure.Add(section, entity);
        var completeDestination = new ProbeTextWriter();
        var entityDestination = new ProbeTextWriter();

        var complete = Assert.Throws<ExchangeStructureCapabilityException>(() =>
            structure.Write(completeDestination));
        var record = Assert.Throws<ExchangeStructureCapabilityException>(() =>
            structure.WriteEntity(entityDestination, entity));

        using (Assert.Multiple())
        {
            await Assert.That(complete.Diagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(["TEST-CAPABILITY-1", "TEST-CAPABILITY-2"]);
            await Assert.That(record.Diagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(["TEST-CAPABILITY-1", "TEST-CAPABILITY-2"]);
            await Assert.That(completeDestination.WriteCount).IsEqualTo(0);
            await Assert.That(entityDestination.WriteCount).IsEqualTo(0);
        }
    }

    private static ExchangeStructure CreateStructure(
        SchemaDescriptor descriptor,
        string implementationLevel = "3;1") => new(
        new HeaderSection(
            new FileDescription(["prewrite"], implementationLevel),
            new FileName("prewrite.p21", "2026-08-22T00:00:00", [string.Empty], [string.Empty], "Pre", "System", "Auth"),
            new FileSchema([descriptor.Name.Value])),
        [descriptor]);

    private sealed class ProbeEntity(
        string componentName,
        string? text,
        IEnumerable<int> values,
        IReadOnlyList<ParameterValue>? projectedParameters = null,
        IEnumerable<Entity>? directReferences = null) : Entity
    {
        internal string ComponentName { get; } = componentName;

        internal string? Text { get; set; } = text;

        internal List<int> Values { get; } = values.ToList();

        internal IReadOnlyList<ParameterValue>? ProjectedParameters { get; } = projectedParameters;

        public override IEnumerable<Entity> DirectReferences { get; } = directReferences?.ToArray() ?? [];
    }

    private sealed class ForeignEntity : Entity
    {
        public override IEnumerable<Entity> DirectReferences => [];
    }

    private sealed class ProbeDescriptor(string name, bool emitCapabilities = false) : SchemaDescriptor
    {
        public override SchemaName Name { get; } = new(name);

        internal int ValidationCount { get; private set; }

        protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;

        protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
            ExchangeStructure structure,
            Entity value,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];

        protected override ValidationResult ValidateCore(
            ExchangeStructure structure,
            IReadOnlyList<KeyValuePair<string, Entity>> entities)
        {
            ValidationCount++;
            var failures = new List<ValidationFailure>();
            foreach (var entry in entities)
            {
                if (entry.Value is not ProbeEntity entity)
                {
                    failures.Add(new ValidationFailure(
                        "TEST.ENTITY.FOREIGN",
                        entry.Key,
                        "The entity is foreign to this schema."));
                    continue;
                }

                if (entity.Text is null)
                {
                    failures.Add(new ValidationFailure(
                        "TEST.TEXT.REQUIRED",
                        entry.Key + ".Text",
                        "Text is required."));
                }

                if (entity.Values.Count == 0)
                {
                    failures.Add(new ValidationFailure(
                        "TEST.VALUES.LOWER_BOUND",
                        entry.Key + ".Values",
                        "Values requires at least one item."));
                }
            }

            return new(failures);
        }

        protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
            ExchangeStructure structure) => emitCapabilities
            ? [
                new("TEST-CAPABILITY-1", Step21DiagnosticSeverity.Error, "First unsupported operation."),
                new("TEST-CAPABILITY-2", Step21DiagnosticSeverity.Error, "Second unsupported operation."),
            ]
            : [];

        protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
            Entity value)
        {
            var entity = (ProbeEntity)value;
            var parameters = new List<ParameterValue>
            {
                ParameterValue.FromString(entity.Text!),
                ParameterValue.FromAggregate(entity.Values.Select(value => ParameterValue.FromInteger(value))),
            };
            if (entity.ProjectedParameters is not null)
            {
                parameters.AddRange(entity.ProjectedParameters);
            }

            return [new(entity.ComponentName, parameters)];
        }
    }

    private sealed class ProbeTextWriter : StringWriter
    {
        internal int WriteCount { get; private set; }

        public override void Write(string? value)
        {
            WriteCount++;
            base.Write(value);
        }
    }
}
