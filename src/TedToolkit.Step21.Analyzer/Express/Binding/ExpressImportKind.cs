// -----------------------------------------------------------------------
// <copyright file="ExpressImportKind.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Identifies the two EXPRESS interface specification forms.
/// </summary>
internal enum ExpressImportKind
{
    /// <summary>
    /// Treats an imported named type as local and permits transitive interfacing.
    /// </summary>
    Use = 0,

    /// <summary>
    /// Makes a resource visible without treating it as locally declared.
    /// </summary>
    Reference = 1,
}