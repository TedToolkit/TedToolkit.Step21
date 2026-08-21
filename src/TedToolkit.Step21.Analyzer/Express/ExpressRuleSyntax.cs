// -----------------------------------------------------------------------
// <copyright file="ExpressRuleSyntax.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Represents one immutable generated-grammar production and its ordered child IR.
/// </summary>
internal sealed class ExpressRuleSyntax : ExpressSyntaxElement
{
    /// <summary>
    /// Initializes a grammar-production IR node.
    /// </summary>
    /// <param name="production">The stable generated grammar production name.</param>
    /// <param name="children">The child rules and terminals in physical source order.</param>
    /// <param name="span">The production's half-open source span.</param>
    internal ExpressRuleSyntax(
        string production,
        IEnumerable<ExpressSyntaxElement> children,
        ExpressSourceSpan span)
        : base(span)
    {
        Production = production;
        Children = new ReadOnlyCollection<ExpressSyntaxElement>(children.ToArray());
    }

    /// <summary>
    /// Gets the stable generated grammar production name.
    /// </summary>
    internal string Production { get; }

    /// <summary>
    /// Gets the child rules and terminals in physical source order.
    /// </summary>
    internal IReadOnlyList<ExpressSyntaxElement> Children { get; }

    /// <summary>
    /// Enumerates this rule and every descendant rule in source order.
    /// </summary>
    /// <returns>The depth-first rule sequence.</returns>
    internal IEnumerable<ExpressRuleSyntax> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children.OfType<ExpressRuleSyntax>())
        {
            foreach (var descendant in child.DescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Enumerates every terminal token below this rule in source order.
    /// </summary>
    /// <returns>The depth-first terminal sequence.</returns>
    internal IEnumerable<ExpressTokenSyntax> DescendantTokens()
    {
        foreach (var child in Children)
        {
            if (child is ExpressTokenSyntax token)
            {
                yield return token;
                continue;
            }

            foreach (var descendant in ((ExpressRuleSyntax)child).DescendantTokens())
            {
                yield return descendant;
            }
        }
    }
}