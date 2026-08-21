// -----------------------------------------------------------------------
// <copyright file="ExpressBoundNamedType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents a resolved reference to an entity or defined type.
/// </summary>
internal sealed class ExpressBoundNamedType : ExpressBoundType
{
    /// <summary>
    /// Initializes a resolved named type.
    /// </summary>
    /// <param name="declaration">The resolved named-type declaration.</param>
    /// <param name="span">The reference span.</param>
    internal ExpressBoundNamedType(ExpressBoundSymbol declaration, ExpressSourceSpan span)
        : base(span)
    {
        Declaration = declaration;
    }

    /// <summary>
    /// Gets the resolved entity or defined-type declaration.
    /// </summary>
    internal ExpressBoundSymbol Declaration { get; }
}