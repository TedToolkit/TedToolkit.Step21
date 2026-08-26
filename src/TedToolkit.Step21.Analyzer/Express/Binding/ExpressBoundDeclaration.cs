// -----------------------------------------------------------------------
// <copyright file="ExpressBoundDeclaration.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Provides immutable identity for a bound schema declaration.
/// </summary>
internal abstract class ExpressBoundDeclaration
{
    /// <summary>
    /// Initializes a bound declaration.
    /// </summary>
    /// <param name="symbol">The resolved declaration identity.</param>
    protected ExpressBoundDeclaration(ExpressBoundSymbol symbol)
    {
        Symbol = symbol;
    }

    /// <summary>
    /// Gets the resolved declaration identity.
    /// </summary>
    internal ExpressBoundSymbol Symbol { get; }

    /// <summary>
    /// Gets the declaration's source spelling.
    /// </summary>
    internal string Name
    {
        get
        {
            return Symbol.Name;
        }
    }

    /// <summary>
    /// Gets the declaration family.
    /// </summary>
    internal ExpressDeclarationKind Kind
    {
        get
        {
            return Symbol.Kind;
        }
    }

    /// <summary>
    /// Gets the owning schema identity.
    /// </summary>
    internal ExpressBoundSchemaIdentity DeclaringSchema
    {
        get
        {
            return Symbol.DeclaringSchema;
        }
    }
}