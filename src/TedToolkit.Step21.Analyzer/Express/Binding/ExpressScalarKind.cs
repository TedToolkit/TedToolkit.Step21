// -----------------------------------------------------------------------
// <copyright file="ExpressScalarKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Identifies an EXPRESS simple type.
/// </summary>
internal enum ExpressScalarKind
{
    /// <summary>
    /// The BINARY type.
    /// </summary>
    Binary = 0,

    /// <summary>
    /// The BOOLEAN type.
    /// </summary>
    Boolean = 1,

    /// <summary>
    /// The INTEGER type.
    /// </summary>
    Integer = 2,

    /// <summary>
    /// The LOGICAL type.
    /// </summary>
    Logical = 3,

    /// <summary>
    /// The NUMBER type.
    /// </summary>
    Number = 4,

    /// <summary>
    /// The REAL type.
    /// </summary>
    Real = 5,

    /// <summary>
    /// The STRING type.
    /// </summary>
    String = 6,
}