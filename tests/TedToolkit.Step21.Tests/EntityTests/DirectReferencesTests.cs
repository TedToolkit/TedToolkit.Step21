namespace TedToolkit.Step21.Tests.EntityTests;

internal sealed class DirectReferencesTests
{
    /// <summary>
    /// Verifies that Entity exposes only a live one-level reference view and owns no serialization identity.
    /// </summary>
    [Test]
    public async Task Should_keep_entity_identity_outside_the_entity_object()
    {
        var first = new TestEntity();
        var second = new TestEntity();
        var root = new TestEntity(first, first);

        root.References.Add(second);
        var properties = typeof(Entity).GetProperties().Select(property => property.Name).ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(properties).IsEquivalentTo(["DirectReferences"]);
            await Assert.That(root.DirectReferences.SequenceEqual([first, first, second])).IsTrue();
            await Assert.That(root.DirectReferences.SelectMany(entity => entity.DirectReferences)).IsEmpty();
        }
    }

    private sealed class TestEntity(params Entity[] references) : Entity
    {
        internal List<Entity> References { get; } = [.. references];

        public override IEnumerable<Entity> DirectReferences => References;
    }
}
