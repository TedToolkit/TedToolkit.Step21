// -----------------------------------------------------------------------
// <copyright file="ExpressSourceLocation.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Identifies one 1-based source position in an EXPRESS input.
/// </summary>
internal sealed class ExpressSourceLocation
{
    /// <summary>
    /// Initializes a source position.
    /// </summary>
    /// <param name="filePath">The logical input path.</param>
    /// <param name="line">The 1-based line.</param>
    /// <param name="column">The 1-based column.</param>
    internal ExpressSourceLocation(string filePath, int line, int column)
    {
        FilePath = filePath;
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Gets the logical input path.
    /// </summary>
    internal string FilePath { get; }

    /// <summary>
    /// Gets the 1-based line.
    /// </summary>
    internal int Line { get; }

    /// <summary>
    /// Gets the 1-based column.
    /// </summary>
    internal int Column { get; }
}