// -----------------------------------------------------------------------
// <copyright file="ExpressBoundNameKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Identifies the declaration class selected for an EXPRESS name.
/// </summary>
internal enum ExpressBoundNameKind
{
    /// <summary>
    /// An entity declaration or population.
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
    /// An entity attribute.
    /// </summary>
    Attribute = 5,

    /// <summary>
    /// A formal parameter.
    /// </summary>
    Parameter = 6,

    /// <summary>
    /// A local variable.
    /// </summary>
    Variable = 7,

    /// <summary>
    /// An alias variable.
    /// </summary>
    Alias = 8,

    /// <summary>
    /// A query control variable.
    /// </summary>
    QueryVariable = 9,

    /// <summary>
    /// A repeat control variable.
    /// </summary>
    RepeatVariable = 10,

    /// <summary>
    /// An enumeration item.
    /// </summary>
    Enumeration = 11,
}