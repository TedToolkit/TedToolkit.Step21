// -----------------------------------------------------------------------
// <copyright file="ExpressBoundSelectType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents an EXPRESS select definition or extension.
/// </summary>
internal sealed class ExpressBoundSelectType : ExpressBoundType
{
    /// <summary>
    /// Initializes a bound select type.
    /// </summary>
    /// <param name="isExtensible">Whether the select is extensible.</param>
    /// <param name="isGenericEntity">Whether the extensible select is GENERIC_ENTITY.</param>
    /// <param name="baseType">The optional resolved select being extended.</param>
    /// <param name="alternatives">The locally declared named-type alternatives.</param>
    /// <param name="span">The complete select span.</param>
    internal ExpressBoundSelectType(
        bool isExtensible,
        bool isGenericEntity,
        ExpressBoundSymbol? baseType,
        IEnumerable<ExpressBoundSymbol> alternatives,
        ExpressSourceSpan span)
        : base(span)
    {
        IsExtensible = isExtensible;
        IsGenericEntity = isGenericEntity;
        BaseType = baseType;
        Alternatives = new ReadOnlyCollection<ExpressBoundSymbol>(alternatives.ToArray());
    }

    /// <summary>
    /// Gets a value indicating whether the select is extensible.
    /// </summary>
    internal bool IsExtensible { get; }

    /// <summary>
    /// Gets a value indicating whether the extensible select is GENERIC_ENTITY.
    /// </summary>
    internal bool IsGenericEntity { get; }

    /// <summary>
    /// Gets the optional resolved select being extended.
    /// </summary>
    internal ExpressBoundSymbol? BaseType { get; }

    /// <summary>
    /// Gets the locally declared named-type alternatives.
    /// </summary>
    internal IReadOnlyList<ExpressBoundSymbol> Alternatives { get; }
}