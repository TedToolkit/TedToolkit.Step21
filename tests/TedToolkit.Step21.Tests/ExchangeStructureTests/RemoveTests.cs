namespace TedToolkit.Step21.Tests.ExchangeStructureTests;

internal sealed class RemoveTests
{
    /// <summary>
    /// Verifies both removal overloads, non-cascading behavior, dangling references, and name reuse.
    /// </summary>
    [Test]
    public async Task Should_remove_only_selected_registration_and_reuse_its_name()
    {
        var structure = new ExchangeStructure(TestHeader.Create());
        var section = new DataSection(new SchemaName("TEST_SCHEMA"));
        structure.DataSections.Add(section);
        var child = new TestEntity();
        var root = new TestEntity(child);
        var rootName = structure.Add(section, root);
        _ = structure.Add(section, child);

        var removedRoot = structure.Remove(root);
        var removedRootAgain = structure.Remove(rootName);
        var childStillRegistered = structure.TryGetName(child, out var childName);
        var replacementName = structure.Add(section, new TestEntity());
        var removedChild = structure.Remove(childName);

        using (Assert.Multiple())
        {
            await Assert.That(removedRoot).IsTrue();
            await Assert.That(removedRootAgain).IsFalse();
            await Assert.That(childStillRegistered).IsTrue();
            await Assert.That(root.DirectReferences.Single()).IsSameReferenceAs(child);
            await Assert.That(replacementName).IsEqualTo(rootName);
            await Assert.That(removedChild).IsTrue();
            await Assert.That(structure.Remove(child)).IsFalse();
        }
    }
}
