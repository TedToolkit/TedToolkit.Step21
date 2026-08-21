// -----------------------------------------------------------------------
// <copyright file="ExpressBoundEnumerationType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents an EXPRESS enumeration definition or extension.
/// </summary>
internal sealed class ExpressBoundEnumerationType : ExpressBoundType
{
    /// <summary>
    /// Initializes a bound enumeration type.
    /// </summary>
    /// <param name="isExtensible">Whether the enumeration is extensible.</param>
    /// <param name="baseType">The optional resolved enumeration being extended.</param>
    /// <param name="values">The locally declared enumeration values.</param>
    /// <param name="span">The complete enumeration span.</param>
    internal ExpressBoundEnumerationType(
        bool isExtensible,
        ExpressBoundSymbol? baseType,
        IEnumerable<string> values,
        ExpressSourceSpan span)
        : base(span)
    {
        IsExtensible = isExtensible;
        BaseType = baseType;
        Values = new ReadOnlyCollection<string>(values.ToArray());
    }

    /// <summary>
    /// Gets a value indicating whether the enumeration is extensible.
    /// </summary>
    internal bool IsExtensible { get; }

    /// <summary>
    /// Gets the optional resolved enumeration being extended.
    /// </summary>
    internal ExpressBoundSymbol? BaseType { get; }

    /// <summary>
    /// Gets locally declared enumeration values in source order.
    /// </summary>
    internal IReadOnlyList<string> Values { get; }
}