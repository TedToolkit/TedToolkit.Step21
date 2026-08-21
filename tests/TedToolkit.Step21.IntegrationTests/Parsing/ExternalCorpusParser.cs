using Antlr4.Runtime;
using TedToolkit.Step21.Analyzer.Grammar;
using TedToolkit.Step21.Grammar;

namespace TedToolkit.Step21.IntegrationTests.Parsing;

internal static class ExternalCorpusParser
{
    public static IReadOnlyList<string> Parse(string path, string format)
    {
        return format switch
        {
            "part21" => ParseStep(path),
            "express" => ParseExpress(path),
            _ => throw new InvalidDataException($"Unsupported corpus format '{format}'."),
        };
    }

    private static IReadOnlyList<string> ParseStep(string path)
    {
        var input = new AntlrInputStream(File.ReadAllText(path));
        var lexer = new STEPLexer(input);
        var lexerErrors = new CollectingErrorListener<int>();
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(lexerErrors);

        var parser = new STEPParser(new CommonTokenStream(lexer));
        var parserErrors = new CollectingErrorListener<IToken>();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(parserErrors);
        parser.exchangeFile();

        return CompleteErrors(lexerErrors.Errors, parserErrors.Errors, parser.CurrentToken.Type);
    }

    private static IReadOnlyList<string> ParseExpress(string path)
    {
        var input = new AntlrInputStream(File.ReadAllText(path));
        var lexer = new ExpressLexer(input);
        var lexerErrors = new CollectingErrorListener<int>();
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(lexerErrors);

        var parser = new ExpressParser(new CommonTokenStream(lexer));
        var parserErrors = new CollectingErrorListener<IToken>();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(parserErrors);
        parser.syntax();

        return CompleteErrors(lexerErrors.Errors, parserErrors.Errors, parser.CurrentToken.Type);
    }

    private static IReadOnlyList<string> CompleteErrors(
        IReadOnlyCollection<string> lexerErrors,
        IReadOnlyCollection<string> parserErrors,
        int currentTokenType)
    {
        var errors = lexerErrors.Concat(parserErrors).ToList();
        if (currentTokenType != TokenConstants.EOF)
        {
            errors.Add("The parser did not consume the complete file.");
        }

        return errors;
    }
}
