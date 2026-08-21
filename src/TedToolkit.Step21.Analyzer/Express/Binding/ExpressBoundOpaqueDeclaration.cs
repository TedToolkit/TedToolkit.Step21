// -----------------------------------------------------------------------
// <copyright file="ExpressBoundOpaqueDeclaration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Preserves a resolved declaration whose executable semantics belong to a later stage.
/// </summary>
internal sealed class ExpressBoundOpaqueDeclaration : ExpressBoundDeclaration
{
    /// <summary>
    /// Initializes a preserved declaration.
    /// </summary>
    /// <param name="symbol">The resolved declaration identity.</param>
    /// <param name="syntax">The preserved source syntax.</param>
    /// <param name="declaredType">The constant or function result type when applicable.</param>
    internal ExpressBoundOpaqueDeclaration(
        ExpressBoundSymbol symbol,
        ExpressRuleSyntax syntax,
        ExpressBoundType? declaredType)
        : base(symbol, syntax)
    {
        DeclaredType = declaredType;
    }

    /// <summary>
    /// Gets the constant or function result type when applicable.
    /// </summary>
    internal ExpressBoundType? DeclaredType { get; }
}