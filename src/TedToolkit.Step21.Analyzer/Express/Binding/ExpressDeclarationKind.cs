// -----------------------------------------------------------------------
// <copyright file="ExpressDeclarationKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Identifies an EXPRESS schema-level declaration family.
/// </summary>
internal enum ExpressDeclarationKind
{
    /// <summary>
    /// An entity declaration.
    /// </summary>
    Entity = 0,

    /// <summary>
    /// A defined-type declaration.
    /// </summary>
    Type = 1,

    /// <summary>
    /// A constant declaration.
    /// </summary>
    Constant = 2,

    /// <summary>
    /// A function declaration.
    /// </summary>
    Function = 3,

    /// <summary>
    /// A procedure declaration.
    /// </summary>
    Procedure = 4,

    /// <summary>
    /// A rule declaration.
    /// </summary>
    Rule = 5,

    /// <summary>
    /// A subtype-constraint declaration.
    /// </summary>
    SubtypeConstraint = 6,
}