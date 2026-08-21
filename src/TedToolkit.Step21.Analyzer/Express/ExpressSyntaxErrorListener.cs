// -----------------------------------------------------------------------
// <copyright file="ExpressSyntaxErrorListener.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Antlr4.Runtime;

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Converts ANTLR lexer and parser errors to stable internal EXPRESS diagnostics.
/// </summary>
/// <typeparam name="TSymbol">The lexer or parser offending-symbol type.</typeparam>
internal sealed class ExpressSyntaxErrorListener<TSymbol> : IAntlrErrorListener<TSymbol>
{
    private readonly string _filePath;

    private readonly ICollection<ExpressSyntaxDiagnostic> _diagnostics;

    /// <summary>
    /// Initializes an EXPRESS syntax error listener.
    /// </summary>
    /// <param name="filePath">The logical input path.</param>
    /// <param name="diagnostics">The destination diagnostic collection.</param>
    internal ExpressSyntaxErrorListener(
        string filePath,
        ICollection<ExpressSyntaxDiagnostic> diagnostics)
    {
        _filePath = filePath;
        _diagnostics = diagnostics;
    }

    /// <inheritdoc/>
    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        TSymbol offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        _diagnostics.Add(new ExpressSyntaxDiagnostic(
            "EXPRESS-SYNTAX",
            msg,
            new ExpressSourceLocation(
                _filePath,
                Math.Max(1, line),
                Math.Max(1, charPositionInLine + 1))));
    }
}