using Antlr4.Runtime;

using TedToolkit.Step21.Grammar;

namespace TedToolkit.Step21.Syntax;

// Owns the atomic parse boundary: diagnostics are aggregated before any syntax graph is published.
internal static class ExchangeStructureSyntaxParser
{
    internal static ExchangeStructureSyntax Parse(string source, string filePath)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filePath);

        var diagnostics = new List<Step21Diagnostic>();
        var input = new AntlrInputStream(source);
        var lexer = new STEPLexer(input);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(new Part21SyntaxErrorListener<int>(filePath, "P21-SYNTAX-LEXER", diagnostics));

        var tokens = new CommonTokenStream(lexer);
        var parser = new STEPParser(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new Part21SyntaxErrorListener<IToken>(filePath, "P21-SYNTAX-PARSER", diagnostics));
        var tree = parser.exchangeFile();

        if (diagnostics.Count > 0)
        {
            ReclassifySignatureDiagnostics(tokens, diagnostics);
            throw new ExchangeStructureSyntaxException(
                diagnostics
                    .OrderBy(value => value.SourceLocation?.Line)
                    .ThenBy(value => value.SourceLocation?.Column)
                    .ThenBy(value => value.Code, StringComparer.Ordinal));
        }

        return new ExchangeStructureSyntaxVisitor(filePath).Create(tree);
    }

    private static void ReclassifySignatureDiagnostics(
        CommonTokenStream tokens,
        IList<Step21Diagnostic> diagnostics)
    {
        tokens.Fill();
        IToken? signature = null;
        var sawTerminalDelimiter = false;
        for (var index = 0; index < tokens.Size; index++)
        {
            var token = tokens.Get(index);
            if (token.Type == STEPLexer.ISO_END)
            {
                sawTerminalDelimiter = true;
            }
            else if (sawTerminalDelimiter && token.Type == STEPLexer.SIGNATURE)
            {
                signature = token;
                break;
            }
        }

        if (signature is null)
            return;

        for (var index = 0; index < diagnostics.Count; index++)
        {
            var diagnostic = diagnostics[index];
            var location = diagnostic.SourceLocation;
            if (location is null
                || location.Line < signature.Line
                || location.Line == signature.Line && location.Column < signature.Column + 1)
            {
                continue;
            }

            diagnostics[index] = new Step21Diagnostic(
                "P21-SIGNATURE-SYNTAX",
                diagnostic.Severity,
                diagnostic.Message,
                location);
        }
    }
}

// Keeps lexer and parser diagnostics distinguishable while sharing deterministic source ordering.
internal sealed class Part21SyntaxErrorListener<TSymbol> : IAntlrErrorListener<TSymbol>
{
    private readonly string _filePath;
    private readonly string _code;
    private readonly ICollection<Step21Diagnostic> _diagnostics;

    internal Part21SyntaxErrorListener(
        string filePath,
        string code,
        ICollection<Step21Diagnostic> diagnostics)
    {
        _filePath = filePath;
        _code = code;
        _diagnostics = diagnostics;
    }

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        TSymbol offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        _diagnostics.Add(
            new Step21Diagnostic(
                _code,
                Step21DiagnosticSeverity.Error,
                msg,
                new SourceLocation(_filePath, Math.Max(1, line), Math.Max(1, charPositionInLine + 1))));
    }
}