// -----------------------------------------------------------------------
// <copyright file="ExpressSyntaxVisitor.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using TedToolkit.Step21.Analyzer.Grammar;

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Transforms every generated grammar production and terminal without reflection or grammar actions.
/// </summary>
internal sealed class ExpressSyntaxVisitor : ExpressBaseVisitor<ExpressSyntaxElement>
{
    private readonly string _filePath;

    /// <summary>
    /// Initializes a complete EXPRESS syntax visitor.
    /// </summary>
    /// <param name="filePath">The logical input path retained by every span.</param>
    internal ExpressSyntaxVisitor(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Creates immutable IR from the normative syntax root.
    /// </summary>
    /// <param name="context">The complete generated parse-tree root.</param>
    /// <returns>The immutable complete-file IR.</returns>
    internal ExpressRuleSyntax Create(ExpressParser.SyntaxContext context)
    {
        return (ExpressRuleSyntax)Visit(context);
    }

    /// <inheritdoc/>
    public override ExpressSyntaxElement VisitChildren(IRuleNode node)
    {
        var context = (ParserRuleContext)node;
        var children = new List<ExpressSyntaxElement>(context.ChildCount);
        for (var index = 0; index < context.ChildCount; index++)
        {
            var child = context.GetChild(index);
            if (child is ParserRuleContext childRule)
            {
                children.Add(Visit(childRule));
            }
            else if (child is ITerminalNode terminal)
            {
                if (terminal.Symbol.Type != TokenConstants.EOF)
                {
                    children.Add(CreateToken(terminal.Symbol));
                }
            }
        }

        var span = children.Count == 0
            ? new ExpressSourceSpan(CreateStart(context.Start), CreateStart(context.Start))
            : new ExpressSourceSpan(children[0].Span.Start, children[children.Count - 1].Span.End);
        return new ExpressRuleSyntax(
            ExpressParser.ruleNames[context.RuleIndex],
            children,
            span);
    }

    private ExpressTokenSyntax CreateToken(IToken token)
    {
        var name = ExpressLexer.DefaultVocabulary.GetSymbolicName(token.Type)
            ?? ExpressLexer.DefaultVocabulary.GetLiteralName(token.Type)
            ?? token.Type.ToString(CultureInfo.InvariantCulture);
        return new(token.Type, name, token.Text ?? "", CreateSpan(token, token));
    }

    private ExpressSourceSpan CreateSpan(IToken start, IToken stop)
    {
        return new(CreateStart(start), CreateEnd(stop));
    }

    private ExpressSourceLocation CreateStart(IToken token)
    {
        return new(
            _filePath,
            Math.Max(1, token.Line),
            Math.Max(1, token.Column + 1));
    }

    private ExpressSourceLocation CreateEnd(IToken token)
    {
        var line = Math.Max(1, token.Line);
        var column = Math.Max(1, token.Column + 1);
        var text = token.Text ?? "";
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                line++;
                column = 1;
            }
            else if (text[index] == '\n' || text[index] == '\f')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return new(_filePath, line, column);
    }
}