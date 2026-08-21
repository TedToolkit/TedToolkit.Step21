using TedToolkit.Step21.Analyzer.Express;

namespace TedToolkit.Step21.Tests.ExpressSyntaxTests;

internal sealed class SyntaxErrorTests
{
    /// <summary>
    /// Verifies that syntax errors produce deterministic source evidence and never publish a partial IR.
    /// </summary>
    [Test]
    public async Task Should_report_source_location_and_withhold_invalid_ir()
    {
        var relativePath = Path.Combine("Express", "Invalid", "invalid-attribute-type.exp");
        var fullPath = Path.Combine(AppContext.BaseDirectory, "TestData", relativePath);

        var result = ExpressSyntaxParser.Parse(relativePath, File.ReadAllText(fullPath));

        using (Assert.Multiple())
        {
            await Assert.That(result.Root).IsNull();
            await Assert.That(result.Diagnostics).IsNotEmpty();
            await Assert.That(result.Diagnostics.All(diagnostic => diagnostic.Code == "EXPRESS-SYNTAX")).IsTrue();
            await Assert.That(result.Diagnostics.All(diagnostic => diagnostic.SourceLocation.FilePath == relativePath)).IsTrue();
            await Assert.That(result.Diagnostics[0].SourceLocation.Line).IsEqualTo(3);
            await Assert.That(result.Diagnostics[0].SourceLocation.Column).IsGreaterThan(0);
        }
    }

    /// <summary>
    /// Verifies that valid schema text followed by unrelated input is rejected rather than partially accepted.
    /// </summary>
    [Test]
    public async Task Should_reject_trailing_input_after_schema()
    {
        const string text = "SCHEMA trailing; END_SCHEMA; unexpected";

        var result = ExpressSyntaxParser.Parse("trailing.exp", text);

        using (Assert.Multiple())
        {
            await Assert.That(result.Root).IsNull();
            await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("EXPRESS-SYNTAX");
            await Assert.That(result.Diagnostics.Single().Message).Contains("unexpected");
        }
    }
}
