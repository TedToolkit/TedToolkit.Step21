// -----------------------------------------------------------------------
// <copyright file="ExpressRedeclarationClassification.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Identifies how one explicit attribute domain relates to its inherited physical slot.
/// </summary>
internal enum ExpressRedeclarationClassification
{
    /// <summary>
    /// The generated and EXPRESS domains are equivalent.
    /// </summary>
    Equivalent = 0,

    /// <summary>
    /// The domain is an implemented ISO specialization.
    /// </summary>
    Supported = 1,

    /// <summary>
    /// The domain relationship is ISO-invalid.
    /// </summary>
    Invalid = 2,

    /// <summary>
    /// The domain may be legal but is outside the implemented mapping matrix.
    /// </summary>
    Unsupported = 3,
}