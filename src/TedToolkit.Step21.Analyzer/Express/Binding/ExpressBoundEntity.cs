// -----------------------------------------------------------------------
// <copyright file="ExpressBoundEntity.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents a bound EXPRESS entity declaration.
/// </summary>
internal sealed class ExpressBoundEntity : ExpressBoundDeclaration
{
    /// <summary>
    /// Initializes a bound entity.
    /// </summary>
    /// <param name="symbol">The entity identity.</param>
    /// <param name="syntax">The preserved declaration syntax.</param>
    /// <param name="isAbstract">Whether the entity is abstract.</param>
    /// <param name="directSupertypes">The resolved direct supertypes.</param>
    /// <param name="attributes">The locally declared attributes.</param>
    internal ExpressBoundEntity(
        ExpressBoundSymbol symbol,
        ExpressRuleSyntax syntax,
        bool isAbstract,
        IEnumerable<ExpressBoundSymbol> directSupertypes,
        IEnumerable<ExpressBoundAttribute> attributes)
        : base(symbol, syntax)
    {
        IsAbstract = isAbstract;
        DirectSupertypes = new ReadOnlyCollection<ExpressBoundSymbol>(directSupertypes.ToArray());
        Attributes = new ReadOnlyCollection<ExpressBoundAttribute>(attributes.ToArray());
    }

    /// <summary>
    /// Gets a value indicating whether the entity is abstract.
    /// </summary>
    internal bool IsAbstract { get; }

    /// <summary>
    /// Gets the resolved direct supertypes.
    /// </summary>
    internal IReadOnlyList<ExpressBoundSymbol> DirectSupertypes { get; }

    /// <summary>
    /// Gets locally declared attributes in source order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundAttribute> Attributes { get; }
}