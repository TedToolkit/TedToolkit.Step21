using TedToolkit.Step21.Tests.Parsing;

namespace TedToolkit.Step21.Tests.STEPParserTests;

internal sealed class FileTests
{
    /// <summary>
    /// Verifies that classic and Edition 3 Part 21 exchange structures parse completely.
    /// </summary>
    [Test]
    [Arguments("Part21/Valid/minimal-ap242.step")]
    [Arguments("Part21/Valid/minimal-ifc4.ifc")]
    [Arguments("Part21/Valid/edition3-all-sections.p21")]
    [Arguments("Part21/Valid/edition3-no-data-signatures.p21")]
    [Arguments("Part21/Valid/ignored-controls-inside-tokens.p21")]
    public async Task Should_parse_complete_file_when_part21_syntax_is_valid(string relativePath)
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

    /// <summary>
    /// Verifies that the normative start rule rejects trailing input and misordered sections.
    /// </summary>
    [Test]
    [Arguments("Part21/Invalid/trailing-input.p21")]
    [Arguments("Part21/Invalid/header-entity-order.p21")]
    [Arguments("Part21/Invalid/section-order.p21")]
    [Arguments("Part21/Invalid/lowercase-exponent.p21")]
    [Arguments("Part21/Invalid/lowercase-enumeration.p21")]
    [Arguments("Part21/Invalid/binary-prefix.p21")]
    [Arguments("Part21/Invalid/numeric-anchor.p21")]
    [Arguments("Part21/Invalid/resource-escape.p21")]
    [Arguments("Part21/Invalid/resource-structure.p21")]
    [Arguments("Part21/Invalid/value-instance-data-lhs.p21")]
    [Arguments("Part21/Invalid/tag-name-low-line.p21")]
    [Arguments("Part21/Invalid/signature-base64.p21")]
    [Arguments("Part21/Invalid/signature-semicolon.p21")]
    public async Task Should_report_error_when_exchange_structure_order_or_boundary_is_invalid(string relativePath)
    {
        var result = ParserFixture.ParseStep(relativePath);

        await Assert.That(result.Errors.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// Verifies that non-graphic control octets are ignored inside and between tokens.
    /// </summary>
    [Test]
    public async Task Should_ignore_non_graphic_control_octets()
    {
        const string input = "I\u0000SO-10303-21;HEADER;"
            + "FILE_\u001FDESCRIPTION(('Controls'),'4;1');"
            + "FILE_NAME('controls.p21','2026-08-21T17:00:00+08:00',('Author'),('Organization'),'TedToolkit','Step21','');"
            + "FILE_SCHEMA(('EXAMPLE_SCHEMA'));ENDSEC;D\u000BATA;ENDSEC;"
            + "END-I\u007FSO-10303-21;\u0007";
        var result = ParserFixture.ParseStepText(input);

        using (Assert.Multiple())
        {
            await Assert.That(result.Errors.Count).IsEqualTo(0);
            await Assert.That(result.ReachedEndOfFile).IsTrue();
        }
    }
}
