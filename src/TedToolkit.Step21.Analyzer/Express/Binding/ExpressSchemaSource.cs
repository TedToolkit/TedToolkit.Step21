// -----------------------------------------------------------------------
// <copyright file="ExpressSchemaSource.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Supplies one complete closed-set EXPRESS source without granting file-system access.
/// </summary>
internal sealed class ExpressSchemaSource
{
    /// <summary>
    /// Initializes a supplied EXPRESS source.
    /// </summary>
    /// <param name="filePath">The logical path used only for diagnostics.</param>
    /// <param name="text">The complete supplied source text.</param>
    internal ExpressSchemaSource(string filePath, string text)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>
    /// Gets the logical diagnostic path.
    /// </summary>
    internal string FilePath { get; }

    /// <summary>
    /// Gets the complete supplied source text.
    /// </summary>
    internal string Text { get; }
}