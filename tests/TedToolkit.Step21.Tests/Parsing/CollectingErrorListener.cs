using Antlr4.Runtime;

namespace TedToolkit.Step21.Tests.Parsing;

internal sealed class CollectingErrorListener<TSymbol> : IAntlrErrorListener<TSymbol>
{
    public List<string> Errors { get; } = [];

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        TSymbol offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        Errors.Add($"{line}:{charPositionInLine} {msg}");
    }
}
