// -----------------------------------------------------------------------
// <copyright file="ExpressSyntaxNavigation.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Provides deterministic structural navigation over the immutable syntax IR.
/// </summary>
internal static class ExpressSyntaxNavigation
{
    /// <summary>
    /// Gets direct child rules, optionally restricted to one production.
    /// </summary>
    /// <param name="node">The parent rule.</param>
    /// <param name="production">The optional required production.</param>
    /// <returns>The matching direct child rules.</returns>
    internal static IEnumerable<ExpressRuleSyntax> ChildRules(
        this ExpressRuleSyntax node,
        string? production = null)
    {
        return node.Children
            .OfType<ExpressRuleSyntax>()
            .Where(child => production is null || child.Production == production);
    }

    /// <summary>
    /// Gets the single direct child with the required production.
    /// </summary>
    /// <param name="node">The parent rule.</param>
    /// <param name="production">The required production.</param>
    /// <returns>The matching child rule.</returns>
    internal static ExpressRuleSyntax RequiredChild(this ExpressRuleSyntax node, string production)
    {
        return node.ChildRules(production).Single();
    }

    /// <summary>
    /// Gets the first identifier token in a rule.
    /// </summary>
    /// <param name="node">The containing rule.</param>
    /// <returns>The identifier token.</returns>
    internal static ExpressTokenSyntax IdentifierToken(this ExpressRuleSyntax node)
    {
        return node.DescendantTokens().First(token => token.TokenName == "SimpleId");
    }

    /// <summary>
    /// Gets a value indicating whether the rule contains a keyword or punctuation spelling.
    /// </summary>
    /// <param name="node">The containing rule.</param>
    /// <param name="text">The source spelling to compare case-insensitively.</param>
    /// <returns><see langword="true"/> when a matching terminal exists.</returns>
    internal static bool HasToken(this ExpressRuleSyntax node, string text)
    {
        return node.DescendantTokens().Any(token =>
            string.Equals(token.Text, text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reconstructs a compact token spelling for retained bound and width expressions.
    /// </summary>
    /// <param name="node">The containing rule.</param>
    /// <returns>The concatenated original terminal spellings.</returns>
    internal static string TokenText(this ExpressRuleSyntax node)
    {
        return string.Concat(node.DescendantTokens().Select(token => token.Text));
    }
}