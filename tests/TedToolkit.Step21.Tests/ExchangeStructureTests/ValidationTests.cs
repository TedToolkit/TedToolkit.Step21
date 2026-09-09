namespace TedToolkit.Step21.Tests.ExchangeStructureTests;

internal sealed class ValidationTests
{
    /// <summary>
    /// Verifies descriptor dispatch receives each registered object once with paths in data-section and registration order.
    /// </summary>
    [Test]
    public async Task Should_dispatch_each_registered_entity_once_with_deterministic_complete_paths()
    {
        var descriptor = new CapturingDescriptor("TEST_SCHEMA");
        var structure = new ExchangeStructure(TestHeader.Create(), [descriptor]);
        var firstSection = new DataSection(new SchemaName("TEST_SCHEMA"), "FIRST");
        var secondSection = new DataSection(new SchemaName("TEST_SCHEMA"), "SECOND");
        structure.DataSections.Add(firstSection);
        structure.DataSections.Add(secondSection);
        var shared = new TestEntity();
        var root = new TestEntity(shared, shared);
        shared.References.Add(root);
        structure.Add(firstSection, new EntityInstanceName("8"), root);
        structure.Add(secondSection, new EntityInstanceName("3"), new TestEntity());

        var first = structure.Validate();
        var second = structure.Validate();
        var expectedPaths = new[]
        {
            "DataSections[0].#8",
            "DataSections[0].#1",
            "DataSections[1].#3",
        };

        using (Assert.Multiple())
        {
            await Assert.That(first.Failures.Select(failure => failure.Path).SequenceEqual(expectedPaths)).IsTrue();
            await Assert.That(second.Failures.Select(failure => failure.Path).SequenceEqual(expectedPaths)).IsTrue();
            await Assert.That(descriptor.Batches.Count).IsEqualTo(4);
            await Assert.That(descriptor.Batches[0].SequenceEqual(expectedPaths[..2])).IsTrue();
            await Assert.That(descriptor.Batches[1].SequenceEqual(expectedPaths[2..])).IsTrue();
        }
    }

    /// <summary>
    /// Verifies editable section/reference inconsistencies aggregate as evidence instead of throwing or mutating state.
    /// </summary>
    [Test]
    public async Task Should_report_every_schema_neutral_structure_failure_without_mutation()
    {
        var descriptor = new CapturingDescriptor("TEST_SCHEMA", emitEntityFailures: false);
        var structure = new ExchangeStructure(TestHeader.Create(), [descriptor]);
        var known = new DataSection(new SchemaName("TEST_SCHEMA"), "KNOWN");
        var missing = new DataSection(new SchemaName("MISSING_SCHEMA"), "MISSING");
        structure.DataSections.Add(known);
        structure.DataSections.Add(known);
        structure.DataSections.Add(missing);
        structure.DataSections.Add(null!);
        var child = new TestEntity();
        var root = new TestEntity(child);
        structure.Add(known, root);
        _ = structure.Remove(child);
        root.References.Add(null!);

        var beforeSections = structure.DataSections.ToArray();
        var beforeRegistrations = structure.Registrations.ToArray();
        var result = structure.Validate();

        using (Assert.Multiple())
        {
            await Assert.That(result.Failures.Select(failure => failure.Code).SequenceEqual(new[]
            {
                "P21.STRUCTURE.DATA_SECTION.NAME.DUPLICATE",
                "P21.STRUCTURE.DATA_SECTION.DUPLICATE",
                "P21.STRUCTURE.SCHEMA_DESCRIPTOR",
                "P21.STRUCTURE.DATA_SECTION.REQUIRED",
                "P21.STRUCTURE.REFERENCE.REGISTRATION",
                "P21.STRUCTURE.REFERENCE.REQUIRED",
            })).IsTrue();
            await Assert.That(result.Failures.Select(failure => failure.Path).SequenceEqual(new[]
            {
                "DataSections[1].Name",
                "DataSections[1]",
                "DataSections[2].SchemaName",
                "DataSections[3]",
                "DataSections[0].#1.DirectReferences[0]",
                "DataSections[0].#1.DirectReferences[1]",
            })).IsTrue();
            await Assert.That(structure.DataSections.SequenceEqual(beforeSections)).IsTrue();
            await Assert.That(structure.Registrations.SequenceEqual(beforeRegistrations)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies a registration whose mutable section was removed remains inspectable as one deterministic failure.
    /// </summary>
    [Test]
    public async Task Should_report_registration_membership_when_its_section_was_removed()
    {
        var descriptor = new CapturingDescriptor("TEST_SCHEMA", emitEntityFailures: false);
        var structure = new ExchangeStructure(TestHeader.Create(), [descriptor]);
        var section = new DataSection(new SchemaName("TEST_SCHEMA"));
        structure.DataSections.Add(section);
        structure.Add(section, new EntityInstanceName("42"), new TestEntity());
        _ = structure.DataSections.Remove(section);

        var failure = structure.Validate().Failures.Single();

        using (Assert.Multiple())
        {
            await Assert.That(failure.Code).IsEqualTo("P21.STRUCTURE.DATA_SECTION.MEMBERSHIP");
            await Assert.That(failure.Path).IsEqualTo("Registrations[#42].DataSection");
        }
    }

    private sealed class CapturingDescriptor(string name, bool emitEntityFailures = true) : SchemaDescriptor
    {
        internal List<string[]> Batches { get; } = [];

        public override SchemaName Name { get; } = new(name);

        protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;

        protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
            ExchangeStructure structure,
            Entity value,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];

        protected override ValidationResult ValidateCore(
            ExchangeStructure structure,
            IReadOnlyList<KeyValuePair<string, Entity>> entities)
        {
            Batches.Add(entities.Select(entity => entity.Key).ToArray());
            return emitEntityFailures
                ? new ValidationResult(entities.Select(entity => new ValidationFailure(
                    "TEST.ENTITY",
                    entity.Key,
                    "The entity was visited.")))
                : new ValidationResult([]);
        }

        protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
            ExchangeStructure structure) => [];

        protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
            Entity value) => [];
    }
}
