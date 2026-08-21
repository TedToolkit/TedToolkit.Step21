// -----------------------------------------------------------------------
// <copyright file="ExpressAggregateKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Identifies an EXPRESS aggregate category.
/// </summary>
internal enum ExpressAggregateKind
{
    /// <summary>
    /// The general AGGREGATE category.
    /// </summary>
    Aggregate = 0,

    /// <summary>
    /// The ARRAY category.
    /// </summary>
    Array = 1,

    /// <summary>
    /// The BAG category.
    /// </summary>
    Bag = 2,

    /// <summary>
    /// The LIST category.
    /// </summary>
    List = 3,

    /// <summary>
    /// The SET category.
    /// </summary>
    Set = 4,
}