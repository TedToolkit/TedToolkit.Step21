// -----------------------------------------------------------------------
// <copyright file="ExpressTokenSyntax.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Represents one immutable EXPRESS terminal token with its original spelling.
/// </summary>
internal sealed class ExpressTokenSyntax : ExpressSyntaxElement
{
    /// <summary>
    /// Initializes a terminal-token IR node.
    /// </summary>
    /// <param name="tokenType">The generated token type.</param>
    /// <param name="tokenName">The generated symbolic or literal token name.</param>
    /// <param name="text">The retained source spelling.</param>
    /// <param name="span">The token's half-open source span.</param>
    internal ExpressTokenSyntax(int tokenType, string tokenName, string text, ExpressSourceSpan span)
        : base(span)
    {
        TokenType = tokenType;
        TokenName = tokenName;
        Text = text;
    }

    /// <summary>
    /// Gets the generated token type.
    /// </summary>
    internal int TokenType { get; }

    /// <summary>
    /// Gets the generated symbolic or literal token name.
    /// </summary>
    internal string TokenName { get; }

    /// <summary>
    /// Gets the retained source spelling.
    /// </summary>
    internal string Text { get; }
}