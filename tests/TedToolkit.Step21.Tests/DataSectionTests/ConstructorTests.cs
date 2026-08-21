namespace TedToolkit.Step21.Tests.DataSectionTests;

internal sealed class ConstructorTests
{
    /// <summary>
    /// Verifies that a data section retains its governing schema name as an editable-structure ISO value.
    /// </summary>
    [Test]
    public async Task Should_retain_schema_name_without_running_schema_validation()
    {
        var section = new DataSection(new SchemaName(string.Empty));

        await Assert.That(section.SchemaName.Value).IsEqualTo(string.Empty);
    }
}
