// -----------------------------------------------------------------------
// <copyright file="ExpressSyntaxParser.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Antlr4.Runtime;

using TedToolkit.Step21.Analyzer.Grammar;

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Owns the atomic EXPRESS boundary that publishes either complete immutable IR or diagnostics.
/// </summary>
internal static class ExpressSyntaxParser
{
    /// <summary>
    /// Parses one or more complete EXPRESS schema declarations.
    /// </summary>
    /// <param name="filePath">The logical path retained by all source evidence.</param>
    /// <param name="source">The complete EXPRESS source text.</param>
    /// <returns>The atomic parse result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> or <paramref name="source"/> is null.</exception>
    internal static ExpressSyntaxParseResult Parse(string filePath, string source)
    {
        if (filePath is null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var diagnostics = new List<ExpressSyntaxDiagnostic>();
        var input = new AntlrInputStream(source);
        var lexer = new ExpressLexer(input);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(new ExpressSyntaxErrorListener<int>(filePath, diagnostics));

        var parser = new ExpressParser(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new ExpressSyntaxErrorListener<IToken>(filePath, diagnostics));
        var tree = parser.syntax();

        var orderedDiagnostics = diagnostics
            .OrderBy(diagnostic => diagnostic.SourceLocation.Line)
            .ThenBy(diagnostic => diagnostic.SourceLocation.Column)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();
        if (orderedDiagnostics.Length > 0)
        {
            return new(root: null, orderedDiagnostics);
        }

        var root = new ExpressSyntaxVisitor(filePath).Create(tree);
        return new(root, Array.Empty<ExpressSyntaxDiagnostic>());
    }
}