namespace TedToolkit.Step21.Tests.HeaderSectionTests;

internal sealed class ConstructorTests
{
    /// <summary>
    /// Verifies that required ISO header entities retain every supplied value through immutable snapshots.
    /// </summary>
    [Test]
    public async Task Should_compose_complete_strongly_typed_header_values()
    {
        var descriptions = new List<string> { "View definition" };
        var authors = new List<string> { "Author" };
        var organizations = new List<string> { "Organization" };
        var schemas = new List<string> { "EXAMPLE_SCHEMA" };
        var description = new FileDescription(descriptions, "3;1");
        var fileName = new FileName(
            "example.step",
            "2026-08-21T10:11:12+08:00",
            authors,
            organizations,
            "TedToolkit.Step21",
            "Example System",
            "Approver");
        var fileSchema = new FileSchema(schemas);

        var header = new HeaderSection(description, fileName, fileSchema);
        descriptions.Add("late");
        authors.Add("late");
        organizations.Add("late");
        schemas.Add("LATE_SCHEMA");

        using (Assert.Multiple())
        {
            await Assert.That(header.FileDescription).IsSameReferenceAs(description);
            await Assert.That(header.FileName).IsSameReferenceAs(fileName);
            await Assert.That(header.FileSchema).IsSameReferenceAs(fileSchema);
            await Assert.That(description.Description.SequenceEqual(["View definition"])).IsTrue();
            await Assert.That(description.ImplementationLevel).IsEqualTo("3;1");
            await Assert.That(fileName.Name).IsEqualTo("example.step");
            await Assert.That(fileName.TimeStamp).IsEqualTo("2026-08-21T10:11:12+08:00");
            await Assert.That(fileName.Author.SequenceEqual(["Author"])).IsTrue();
            await Assert.That(fileName.Organization.SequenceEqual(["Organization"])).IsTrue();
            await Assert.That(fileName.PreprocessorVersion).IsEqualTo("TedToolkit.Step21");
            await Assert.That(fileName.OriginatingSystem).IsEqualTo("Example System");
            await Assert.That(fileName.Authorization).IsEqualTo("Approver");
            await Assert.That(fileSchema.SchemaIdentifiers.SequenceEqual(["EXAMPLE_SCHEMA"])).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that null components and null elements cannot escape into immutable ISO values.
    /// </summary>
    [Test]
    public async Task Should_reject_null_header_components_and_elements()
    {
        static void CreateNullDescription() => _ = new FileDescription(null!, "3;1");
        static void CreateNullDescriptionElement() => _ = new FileDescription([null!], "3;1");
        static void CreateNullAuthorElement() => _ = new FileName("name", "time", [null!], [], "p", "o", "a");
        static void CreateNullSchemaElement() => _ = new FileSchema([null!]);
        static void CreateNullHeaderComponent() => _ = new HeaderSection(
            null!,
            new FileName("name", "time", [], [], "p", "o", "a"),
            new FileSchema([]));

        using (Assert.Multiple())
        {
            await Assert.That((Action)CreateNullDescription).Throws<ArgumentNullException>();
            await Assert.That((Action)CreateNullDescriptionElement).Throws<ArgumentException>();
            await Assert.That((Action)CreateNullAuthorElement).Throws<ArgumentException>();
            await Assert.That((Action)CreateNullSchemaElement).Throws<ArgumentException>();
            await Assert.That((Action)CreateNullHeaderComponent).Throws<ArgumentNullException>();
        }
    }
}
