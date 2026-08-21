using Antlr4.Runtime;
using TedToolkit.Step21.Analyzer.Grammar;
using TedToolkit.Step21.Grammar;

namespace TedToolkit.Step21.Tests.Parsing;

internal static class ParserFixture
{
    public static ParseResult ParseStep(string relativePath)
    {
        var input = new AntlrInputStream(File.ReadAllText(GetTestDataPath(relativePath)));
        var lexer = new STEPLexer(input);
        var lexerErrors = new CollectingErrorListener<int>();
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(lexerErrors);

        var parser = new STEPParser(new CommonTokenStream(lexer));
        var parserErrors = new CollectingErrorListener<IToken>();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(parserErrors);
        parser.file();

        return CreateResult(lexerErrors.Errors, parserErrors.Errors, parser.CurrentToken.Type);
    }

    public static ParseResult ParseExpress(string relativePath)
    {
        var input = new AntlrInputStream(File.ReadAllText(GetTestDataPath(relativePath)));
        var lexer = new ExpressLexer(input);
        var lexerErrors = new CollectingErrorListener<int>();
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(lexerErrors);

        var parser = new ExpressParser(new CommonTokenStream(lexer));
        var parserErrors = new CollectingErrorListener<IToken>();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(parserErrors);
        parser.schemaDecl();

        return CreateResult(lexerErrors.Errors, parserErrors.Errors, parser.CurrentToken.Type);
    }

    private static ParseResult CreateResult(
        IReadOnlyCollection<string> lexerErrors,
        IReadOnlyCollection<string> parserErrors,
        int currentTokenType)
    {
        var errors = lexerErrors.Concat(parserErrors).ToArray();
        return new ParseResult(errors, currentTokenType == TokenConstants.EOF);
    }

    private static string GetTestDataPath(string relativePath)
    {
        return Path.Combine(AppContext.BaseDirectory, "TestData", relativePath);
    }
}
