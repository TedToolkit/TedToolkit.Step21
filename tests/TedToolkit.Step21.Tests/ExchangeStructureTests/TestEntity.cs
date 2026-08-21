namespace TedToolkit.Step21.Tests.ExchangeStructureTests;

internal sealed class TestEntity(params Entity[] references) : Entity
{
    internal List<Entity> References { get; } = [.. references];

    public override IEnumerable<Entity> DirectReferences => References;
}


internal sealed class TestSchemaDescriptor(string name) : SchemaDescriptor
{
    public override SchemaName Name { get; } = new(name);
}

internal static class TestHeader
{
    internal static HeaderSection Create() => new(
        new FileDescription(["test"], "3;1"),
        new FileName("test.step", "2026-08-21T00:00:00+08:00", [], [], "tests", "tests", string.Empty),
        new FileSchema(["TEST_SCHEMA"]));
}
