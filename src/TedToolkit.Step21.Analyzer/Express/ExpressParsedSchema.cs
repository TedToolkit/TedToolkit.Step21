// -----------------------------------------------------------------------
// <copyright file="ExpressParsedSchema.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Carries one parsed schema from syntax into closed-set binding.
/// </summary>
internal sealed class ExpressParsedSchema
{
    /// <summary>
    /// Initializes a parsed schema.
    /// </summary>
    /// <param name="syntax">The complete schema syntax.</param>
    /// <param name="body">The schema body syntax.</param>
    /// <param name="nameToken">The declared schema-name token.</param>
    internal ExpressParsedSchema(
        ExpressRuleSyntax syntax,
        ExpressRuleSyntax body,
        ExpressTokenSyntax nameToken)
    {
        Syntax = syntax;
        Body = body;
        NameToken = nameToken;
    }

    /// <summary>
    /// Gets the complete schema syntax.
    /// </summary>
    internal ExpressRuleSyntax Syntax { get; }

    /// <summary>
    /// Gets the schema body syntax.
    /// </summary>
    internal ExpressRuleSyntax Body { get; }

    /// <summary>
    /// Gets the declared schema-name token.
    /// </summary>
    internal ExpressTokenSyntax NameToken { get; }
}