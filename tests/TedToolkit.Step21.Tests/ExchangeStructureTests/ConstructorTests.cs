namespace TedToolkit.Step21.Tests.ExchangeStructureTests;

internal sealed class ConstructorTests
{
    /// <summary>
    /// Verifies descriptor-free construction and freely editable data-section composition.
    /// </summary>
    [Test]
    public async Task Should_construct_temporarily_unbound_structure_with_mutable_sections()
    {
        var header = TestHeader.Create();
        var structure = new ExchangeStructure(header);
        var section = new DataSection(new SchemaName("TEST_SCHEMA"));

        structure.DataSections.Add(section);
        _ = structure.DataSections.Remove(section);

        using (Assert.Multiple())
        {
            await Assert.That(structure.Header).IsSameReferenceAs(header);
            await Assert.That(structure.DataSections).IsEmpty();
            await Assert.That(structure.SchemaDescriptors).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies that descriptors are snapshotted by unique ordinal name before construction succeeds.
    /// </summary>
    [Test]
    public async Task Should_snapshot_unique_descriptors_and_reject_duplicate_names()
    {
        var first = new TestSchemaDescriptor("FIRST");
        var second = new TestSchemaDescriptor("SECOND");
        var source = new List<SchemaDescriptor> { first, second };
        var structure = new ExchangeStructure(TestHeader.Create(), source);

        source.Clear();
        static void CreateDuplicates() => _ = new ExchangeStructure(
            TestHeader.Create(),
            [new TestSchemaDescriptor("DUPLICATE"), new TestSchemaDescriptor("DUPLICATE")]);

        using (Assert.Multiple())
        {
            await Assert.That(structure.SchemaDescriptors.SequenceEqual([first, second])).IsTrue();
            await Assert.That(structure.TryGetSchemaDescriptor(new SchemaName("FIRST"), out var found)).IsTrue();
            await Assert.That(found).IsSameReferenceAs(first);
            await Assert.That(structure.TryGetSchemaDescriptor(new SchemaName("first"), out var foundByCase)).IsTrue();
            await Assert.That(foundByCase).IsSameReferenceAs(first);
            await Assert.That(structure.TryGetSchemaDescriptor(new SchemaName("MISSING"), out _)).IsFalse();
            await Assert.That(structure.GetSchemaDescriptorDiagnostics(new SchemaName("FIRST"))).IsEmpty();
            await Assert.That(structure.GetSchemaDescriptorDiagnostics(new SchemaName("first"))).IsEmpty();
            await Assert.That((Action)CreateDuplicates).Throws<ArgumentException>();
        }
    }
}
