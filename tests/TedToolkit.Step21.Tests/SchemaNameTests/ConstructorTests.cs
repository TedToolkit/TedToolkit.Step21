namespace TedToolkit.Step21.Tests.SchemaNameTests;

internal sealed class ConstructorTests
{
    /// <summary>
    /// Verifies exact ordinal schema-name value semantics without premature lexical validation.
    /// </summary>
    [Test]
    public async Task Should_retain_exact_spelling_and_compare_ordinally()
    {
        var first = new SchemaName("TEST_SCHEMA");
        var same = new SchemaName("TEST_SCHEMA");
        var differentCase = new SchemaName("test_schema");
        var temporarilyInvalid = new SchemaName(string.Empty);

        using (Assert.Multiple())
        {
            await Assert.That(first).IsEqualTo(same);
            await Assert.That(first.GetHashCode()).IsEqualTo(same.GetHashCode());
            await Assert.That(first).IsNotEqualTo(differentCase);
            await Assert.That(first.ToString()).IsEqualTo("TEST_SCHEMA");
            await Assert.That(temporarilyInvalid.Value).IsEqualTo(string.Empty);
        }
    }

    /// <summary>
    /// Verifies that null and the default struct value cannot represent a schema name.
    /// </summary>
    [Test]
    public async Task Should_reject_missing_schema_name_values()
    {
        static void CreateNull() => _ = new SchemaName(null!);
        static void ReadDefault() => _ = default(SchemaName).Value;

        using (Assert.Multiple())
        {
            await Assert.That((Action)CreateNull).Throws<ArgumentNullException>();
            await Assert.That((Action)ReadDefault).Throws<InvalidOperationException>();
        }
    }
}
