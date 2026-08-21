using TedToolkit.Step21.Tests.Parsing;

namespace TedToolkit.Step21.Tests.ExpressParserTests;

internal sealed class SchemaDeclTests
{
    /// <summary>
    /// Verifies that a complete EXPRESS schema declaration parses to the end of the file.
    /// </summary>
    [Test]
    public async Task Should_parse_complete_schema_when_express_is_valid()
    {
        var result = ParserFixture.ParseExpress("Express/Valid/minimal-schema.exp");
        foreach (var error in result.Errors)
        {
            Console.WriteLine(error);
        }

        using (Assert.Multiple())
        {
            await Assert.That(result.Errors.Count).IsEqualTo(0);
            await Assert.That(result.ReachedEndOfFile).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that an unterminated EXPRESS schema reports a syntax error.
    /// </summary>
    [Test]
    public async Task Should_report_error_when_end_schema_is_missing()
    {
        var result = ParserFixture.ParseExpress("Express/Invalid/missing-end-schema.exp");

        await Assert.That(result.Errors.Count).IsGreaterThan(0);
    }
}
