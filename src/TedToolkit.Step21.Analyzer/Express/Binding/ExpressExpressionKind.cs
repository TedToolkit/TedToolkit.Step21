// -----------------------------------------------------------------------
// <copyright file="ExpressExpressionKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Identifies one semantically distinct EXPRESS expression shape.
/// </summary>
internal enum ExpressExpressionKind
{
    /// <summary>
    /// An EXPRESS literal.
    /// </summary>
    Literal = 0,

    /// <summary>
    /// The indeterminate value.
    /// </summary>
    Indeterminate = 1,

    /// <summary>
    /// A resolved value reference or built-in constant.
    /// </summary>
    Reference = 2,

    /// <summary>
    /// A function application or entity constructor.
    /// </summary>
    Application = 3,

    /// <summary>
    /// A unary operation.
    /// </summary>
    Unary = 4,

    /// <summary>
    /// A binary operation.
    /// </summary>
    Binary = 5,

    /// <summary>
    /// An aggregate initializer.
    /// </summary>
    AggregateInitializer = 6,

    /// <summary>
    /// An aggregate initializer element with a repetition count.
    /// </summary>
    Repetition = 7,

    /// <summary>
    /// An interval expression.
    /// </summary>
    Interval = 8,

    /// <summary>
    /// A query expression.
    /// </summary>
    Query = 9,

    /// <summary>
    /// An attribute qualification.
    /// </summary>
    AttributeQualifier = 10,

    /// <summary>
    /// A group qualification.
    /// </summary>
    GroupQualifier = 11,

    /// <summary>
    /// A single aggregate index qualification.
    /// </summary>
    IndexQualifier = 12,

    /// <summary>
    /// An aggregate slice qualification.
    /// </summary>
    SliceQualifier = 13,
}