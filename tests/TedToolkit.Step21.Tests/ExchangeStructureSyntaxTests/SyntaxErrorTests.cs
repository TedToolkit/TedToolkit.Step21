using TedToolkit.Step21.Syntax;

namespace TedToolkit.Step21.Tests.ExchangeStructureSyntaxTests;

internal sealed class SyntaxErrorTests
{
    /// <summary>
    /// Verifies that malformed input produces complete source-located syntax diagnostics and no graph.
    /// </summary>
    [Test]
    public async Task Should_throw_complete_diagnostics_when_exchange_file_is_invalid()
    {
        const string relativePath = "Part21/Invalid/missing-data-endsec.p21";
        ExchangeStructureSyntaxException? exception = null;

        try
        {
            _ = ExchangeStructureSyntaxParser.Parse(File.ReadAllText(GetPath(relativePath)), relativePath);
        }
        catch (ExchangeStructureSyntaxException caught)
        {
            exception = caught;
        }

        using (Assert.Multiple())
        {
            await Assert.That(exception is not null).IsTrue();
            await Assert.That(exception?.Diagnostics.Count > 0).IsTrue();
            await Assert.That(exception?.Diagnostics.All(value => value.Severity == Step21DiagnosticSeverity.Error) == true)
                .IsTrue();
            await Assert.That(exception?.Diagnostics.All(value => value.SourceLocation?.FilePath == relativePath) == true)
                .IsTrue();
            await Assert.That(exception?.Diagnostics.All(value => value.SourceLocation?.Line > 0) == true).IsTrue();
            await Assert.That(exception?.Diagnostics.All(value => value.SourceLocation?.Column > 0) == true).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that lexical failures remain distinguishable inside complete ordered syntax evidence.
    /// </summary>
    [Test]
    public async Task Should_include_lexer_diagnostics_when_a_token_is_invalid()
    {
        const string relativePath = "Part21/Invalid/resource-escape.p21";
        ExchangeStructureSyntaxException? exception = null;

        try
        {
            _ = ExchangeStructureSyntaxParser.Parse(File.ReadAllText(GetPath(relativePath)), relativePath);
        }
        catch (ExchangeStructureSyntaxException caught)
        {
            exception = caught;
        }

        using (Assert.Multiple())
        {
            await Assert.That(exception is not null).IsTrue();
            await Assert.That(exception?.Diagnostics.Any(value => value.Code == "P21-SYNTAX-LEXER") == true).IsTrue();
            await Assert.That(exception?.Diagnostics.All(value => value.SourceLocation?.FilePath == relativePath) == true)
                .IsTrue();
        }
    }

    private static string GetPath(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", relativePath);
}
