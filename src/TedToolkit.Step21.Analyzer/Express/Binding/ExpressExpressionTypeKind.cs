// -----------------------------------------------------------------------
// <copyright file="ExpressExpressionTypeKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Identifies the statically known result category of a bound EXPRESS expression.
/// </summary>
internal enum ExpressExpressionTypeKind
{
    /// <summary>
    /// No conforming static type was established.
    /// </summary>
    Unresolved = 0,

    /// <summary>
    /// The context-polymorphic indeterminate value.
    /// </summary>
    Indeterminate = 1,

    /// <summary>
    /// The BINARY value domain.
    /// </summary>
    Binary = 2,

    /// <summary>
    /// The two-state BOOLEAN value domain.
    /// </summary>
    Boolean = 3,

    /// <summary>
    /// The INTEGER value domain.
    /// </summary>
    Integer = 4,

    /// <summary>
    /// The three-state LOGICAL value domain.
    /// </summary>
    Logical = 5,

    /// <summary>
    /// The NUMBER value domain.
    /// </summary>
    Number = 6,

    /// <summary>
    /// The REAL value domain.
    /// </summary>
    Real = 7,

    /// <summary>
    /// The STRING value domain.
    /// </summary>
    String = 8,

    /// <summary>
    /// An aggregate value domain.
    /// </summary>
    Aggregate = 9,

    /// <summary>
    /// An entity value domain.
    /// </summary>
    Entity = 10,

    /// <summary>
    /// An enumeration value domain.
    /// </summary>
    Enumeration = 11,

    /// <summary>
    /// A select value domain.
    /// </summary>
    Select = 12,

    /// <summary>
    /// A generic value domain.
    /// </summary>
    Generic = 13,

    /// <summary>
    /// A named type whose imported underlying declaration is not local.
    /// </summary>
    Defined = 14,
}