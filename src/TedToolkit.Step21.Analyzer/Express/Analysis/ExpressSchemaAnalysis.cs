// -----------------------------------------------------------------------
// <copyright file="ExpressSchemaAnalysis.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Express.Analysis;

/// <summary>
/// Provides immutable semantic rule structure for one analyzed schema.
/// </summary>
internal sealed class ExpressSchemaAnalysis
{
    private readonly ReadOnlyDictionary<ExpressBoundDeclaration, ExpressSemanticRule> _declarations;

    private readonly ReadOnlyDictionary<
        (string Path, int StartLine, int StartColumn, int EndLine, int EndColumn),
        ExpressBoundExpression> _expressions;

    /// <summary>
    /// Initializes one syntax-detached schema analysis.
    /// </summary>
    /// <param name="schema">The analyzed bound schema.</param>
    /// <param name="syntaxOf">The binding-owned syntax lookup consumed during construction.</param>
    internal ExpressSchemaAnalysis(
        ExpressBoundSchema schema,
        Func<ExpressBoundDeclaration, ExpressRuleSyntax> syntaxOf)
    {
        Schema = schema;
        _expressions = new(schema.Expressions.ToDictionary(expression => ExpressionKey(expression.Span)));
        _declarations = new(
            schema.Declarations
                .Concat(schema.NestedDeclarations)
                .ToDictionary(
                    declaration => declaration,
                    declaration => ExpressSemanticRule.Create(syntaxOf(declaration))));
    }

    /// <summary>
    /// Gets the analyzed bound schema.
    /// </summary>
    internal ExpressBoundSchema Schema { get; }

    /// <summary>
    /// Gets the existing bound expression by its exact source range.
    /// </summary>
    /// <param name="span">The retained expression range.</param>
    /// <returns>The shared expression tree, without a new projection or copy.</returns>
    internal ExpressBoundExpression GetExpression(ExpressSourceSpan span)
    {
        return _expressions[ExpressionKey(span)];
    }

    /// <summary>
    /// Gets the semantic rule model rooted at one declaration.
    /// </summary>
    /// <param name="declaration">The analyzed declaration.</param>
    /// <returns>The declaration's immutable semantic rule model.</returns>
    internal ExpressSemanticRule GetDeclaration(ExpressBoundDeclaration declaration)
    {
        return _declarations[declaration];
    }

    private static (string Path, int StartLine, int StartColumn, int EndLine, int EndColumn) ExpressionKey(
        ExpressSourceSpan span)
    {
        return (span.Start.FilePath, span.Start.Line, span.Start.Column, span.End.Line, span.End.Column);
    }
}