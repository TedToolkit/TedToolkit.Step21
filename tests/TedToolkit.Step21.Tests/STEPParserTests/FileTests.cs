using TedToolkit.Step21.Tests.Parsing;

namespace TedToolkit.Step21.Tests.STEPParserTests;

internal sealed class FileTests
{
    /// <summary>
    /// Verifies that classic Part 21 files from mechanical STEP and IFC domains parse completely.
    /// </summary>
    [Test]
    [Arguments("Part21/Valid/minimal-ap242.step")]
    [Arguments("Part21/Valid/minimal-ifc4.ifc")]
    public async Task Should_parse_complete_file_when_classic_part21_syntax_is_valid(string relativePath)
    {
        var result = ParserFixture.ParseStep(relativePath);
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
    /// Verifies that malformed Part 21 input reports a syntax error.
    /// </summary>
    [Test]
    public async Task Should_report_error_when_data_section_is_not_terminated()
    {
        var result = ParserFixture.ParseStep("Part21/Invalid/missing-data-endsec.p21");

        await Assert.That(result.Errors.Count).IsGreaterThan(0);
    }
}
