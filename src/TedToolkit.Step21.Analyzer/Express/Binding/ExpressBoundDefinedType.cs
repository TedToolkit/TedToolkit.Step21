// -----------------------------------------------------------------------
// <copyright file="ExpressBoundDefinedType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents an EXPRESS defined type with a completely bound underlying type.
/// </summary>
internal sealed class ExpressBoundDefinedType : ExpressBoundDeclaration
{
    /// <summary>
    /// Initializes a bound defined type.
    /// </summary>
    /// <param name="symbol">The type identity.</param>
    /// <param name="syntax">The preserved declaration syntax.</param>
    /// <param name="underlyingType">The recursively bound underlying type.</param>
    internal ExpressBoundDefinedType(
        ExpressBoundSymbol symbol,
        ExpressRuleSyntax syntax,
        ExpressBoundType underlyingType)
        : base(symbol, syntax)
    {
        UnderlyingType = underlyingType;
    }

    /// <summary>
    /// Gets the recursively bound underlying type.
    /// </summary>
    internal ExpressBoundType UnderlyingType { get; }
}