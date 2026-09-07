using TedToolkit.Step21.Syntax;

namespace TedToolkit.Step21.Tests.ExchangeStructureSyntaxTests;

internal sealed class CapabilityTests
{
    /// <summary>
    /// Verifies that each advanced section remains parsed and reports its exact unavailable operation.
    /// </summary>
    [Test]
    [Arguments("Part21/Valid/advanced-reference.p21", "P21-CAP-REFERENCE")]
    public async Task Should_throw_exact_capability_when_advanced_operation_is_requested(
        string relativePath,
        string expectedCode)
    {
        var syntax = Parse(relativePath);
        ExchangeStructureCapabilityException? exception = null;

        try
        {
            syntax.ThrowIfUnsupportedOperationsRequired();
        }
        catch (ExchangeStructureCapabilityException caught)
        {
            exception = caught;
        }

        using (Assert.Multiple())
        {
            await Assert.That(exception is not null).IsTrue();
            await Assert.That(exception?.Diagnostics.Count).IsEqualTo(1);
            await Assert.That(exception?.Diagnostics[0].Code).IsEqualTo(expectedCode);
            await Assert.That(exception?.Diagnostics[0].Severity).IsEqualTo(Step21DiagnosticSeverity.Error);
            await Assert.That(exception?.Diagnostics[0].SourceLocation?.FilePath).IsEqualTo(relativePath);
        }
    }

    /// <summary>
    /// Verifies that syntactically valid but malformed CMS fails at the public semantic boundary.
    /// </summary>
    [Test]
    public async Task Should_report_malformed_cms_through_public_read()
    {
        const string relativePath = "Part21/Valid/advanced-signature.p21";
        var source = File.ReadAllText(GetPath(relativePath));
        var exception = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(new StringReader(source), []));

        using (Assert.Multiple())
        {
            await Assert.That(exception.Diagnostics.Count).IsEqualTo(1);
            await Assert.That(exception.Diagnostics[0].Code).IsEqualTo("P21-SIGNATURE-CMS");
            await Assert.That(exception.Diagnostics[0].Severity).IsEqualTo(Step21DiagnosticSeverity.Error);
        }
    }

    /// <summary>
    /// Verifies that a graph containing all advanced section families reports every capability in file order.
    /// </summary>
    [Test]
    public async Task Should_aggregate_capabilities_when_multiple_advanced_sections_are_preserved()
    {
        var syntax = Parse("Part21/Valid/edition3-all-sections.p21");
        ExchangeStructureCapabilityException? exception = null;

        try
        {
            syntax.ThrowIfUnsupportedOperationsRequired();
        }
        catch (ExchangeStructureCapabilityException caught)
        {
            exception = caught;
        }

        using (Assert.Multiple())
        {
            await Assert.That(exception is not null).IsTrue();
            await Assert.That(string.Join('|', exception!.Diagnostics.Select(value => value.Code)))
                .IsEqualTo("P21-CAP-REFERENCE");
            await Assert.That(exception.Diagnostics.Select(value => value.SourceLocation!.Line).SequenceEqual([16]))
                .IsTrue();
        }
    }

    /// <summary>
    /// Verifies that capability checking returns normally when no preserved syntax needs an advanced operation.
    /// </summary>
    [Test]
    public async Task Should_return_normally_when_no_advanced_operation_is_required()
    {
        var syntax = Parse("Part21/Valid/minimal-ap242.step");
        Exception? exception = null;

        try
        {
            syntax.ThrowIfUnsupportedOperationsRequired();
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is null).IsTrue();
    }

    private static ExchangeStructureSyntax Parse(string relativePath) =>
        ExchangeStructureSyntaxParser.Parse(File.ReadAllText(GetPath(relativePath)), relativePath);

    private static string GetPath(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", relativePath);
}
