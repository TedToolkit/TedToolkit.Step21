// -----------------------------------------------------------------------
// <copyright file="ExpressSemanticRule.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Express.Analysis;

/// <summary>
/// Represents syntax-detached semantic rule structure retained for analysis and lowering.
/// </summary>
internal sealed class ExpressSemanticRule
{
    private ExpressSemanticRule(
        string role,
        ExpressSourceSpan span,
        string sourceText,
        IEnumerable<string> identifiers,
        IEnumerable<ExpressSemanticRule> children,
        IEnumerable<ExpressSemanticRule> thenStatements,
        IEnumerable<ExpressSemanticRule> elseStatements)
    {
        Role = role;
        Span = span;
        SourceText = sourceText;
        Identifiers = new ReadOnlyCollection<string>(identifiers.ToArray());
        Children = new ReadOnlyCollection<ExpressSemanticRule>(children.ToArray());
        ThenStatements = new ReadOnlyCollection<ExpressSemanticRule>(thenStatements.ToArray());
        ElseStatements = new ReadOnlyCollection<ExpressSemanticRule>(elseStatements.ToArray());
    }

    /// <summary>
    /// Gets the semantic role carried by this node.
    /// </summary>
    internal string Role { get; }

    /// <summary>
    /// Gets the original source span used for bound-fact correlation and diagnostics.
    /// </summary>
    internal ExpressSourceSpan Span { get; }

    /// <summary>
    /// Gets normalized source text needed by generated documentation.
    /// </summary>
    internal string SourceText { get; }

    /// <summary>
    /// Gets the declaration identifier when this semantic role carries one.
    /// </summary>
    internal string? Identifier
    {
        get
        {
            return Identifiers.Count == 0 ? null : Identifiers[0];
        }
    }

    /// <summary>
    /// Gets source identifiers carried by this semantic role in source order.
    /// </summary>
    internal IReadOnlyList<string> Identifiers { get; }

    /// <summary>
    /// Gets direct semantic children in source order.
    /// </summary>
    internal IReadOnlyList<ExpressSemanticRule> Children { get; }

    /// <summary>
    /// Gets statements in the true branch when this node represents an IF statement.
    /// </summary>
    internal IReadOnlyList<ExpressSemanticRule> ThenStatements { get; }

    /// <summary>
    /// Gets statements in the false branch when this node represents an IF statement.
    /// </summary>
    internal IReadOnlyList<ExpressSemanticRule> ElseStatements { get; }

    /// <summary>
    /// Gets direct children having one semantic role.
    /// </summary>
    /// <param name="role">The required semantic role.</param>
    /// <returns>Matching direct children in source order.</returns>
    internal IEnumerable<ExpressSemanticRule> ChildRules(string role)
    {
        return Children.Where(child => string.Equals(child.Role, role, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets all direct semantic children.
    /// </summary>
    /// <returns>Direct semantic children in source order.</returns>
    internal IEnumerable<ExpressSemanticRule> ChildRules()
    {
        return Children;
    }

    /// <summary>
    /// Gets the single required direct child having one semantic role.
    /// </summary>
    /// <param name="role">The required semantic role.</param>
    /// <returns>The required child.</returns>
    internal ExpressSemanticRule RequiredChild(string role)
    {
        return ChildRules(role).Single();
    }

    /// <summary>
    /// Enumerates this rule and all semantic descendants in source order.
    /// </summary>
    /// <returns>This rule followed by its descendants.</returns>
    internal IEnumerable<ExpressSemanticRule> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var descendant in child.DescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Creates a syntax-detached semantic rule tree during expression and flow analysis.
    /// </summary>
    /// <param name="syntax">The analyzed source syntax.</param>
    /// <returns>The immutable semantic rule model.</returns>
    internal static ExpressSemanticRule Create(ExpressRuleSyntax syntax)
    {
        var children = syntax.ChildRules().Select(Create).ToArray();
        var thenStatements = new List<ExpressSemanticRule>();
        var elseStatements = new List<ExpressSemanticRule>();
        var inElse = false;
        if (string.Equals(syntax.Production, "ifStmt", StringComparison.Ordinal))
        {
            foreach (var child in syntax.Children)
            {
                if (child is ExpressTokenSyntax token
                    && string.Equals(token.Text, "ELSE", StringComparison.OrdinalIgnoreCase))
                {
                    inElse = true;
                }
                else if (child is ExpressRuleSyntax { Production: "stmt", } statement)
                {
                    (inElse ? elseStatements : thenStatements).Add(Create(statement));
                }
            }
        }

        var identifiers = syntax.DescendantTokens()
            .Where(token => string.Equals(token.TokenName, "SimpleId", StringComparison.Ordinal))
            .Select(token => token.Text)
            .ToArray();
        return new(
            syntax.Production,
            syntax.Span,
            syntax.TokenText(),
            identifiers,
            children,
            thenStatements,
            elseStatements);
    }
}