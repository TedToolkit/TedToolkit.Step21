// -----------------------------------------------------------------------
// <copyright file="ExpressAttributeKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Identifies an EXPRESS entity attribute family.
/// </summary>
internal enum ExpressAttributeKind
{
    /// <summary>
    /// An explicit attribute.
    /// </summary>
    Explicit = 0,

    /// <summary>
    /// A derived attribute.
    /// </summary>
    Derived = 1,

    /// <summary>
    /// An inverse attribute.
    /// </summary>
    Inverse = 2,
}