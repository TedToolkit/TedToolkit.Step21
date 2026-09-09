// -----------------------------------------------------------------------
// <copyright file="ExpressPhysicalNameFailure.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Describes one source-located invalid physical-name declaration.
/// </summary>
internal sealed class ExpressPhysicalNameFailure
{
    /// <summary>
    /// Initializes a physical-name failure.
    /// </summary>
    /// <param name="filePath">The logical additional-file path.</param>
    /// <param name="line">The one-based source line.</param>
    /// <param name="column">The one-based source column.</param>
    /// <param name="message">The diagnostic message.</param>
    internal ExpressPhysicalNameFailure(string filePath, int line, int column, string message)
    {
        Location = new(filePath, line, column);
        Message = message;
    }

    /// <summary>
    /// Gets the failing map location.
    /// </summary>
    internal ExpressSourceLocation Location { get; }

    /// <summary>
    /// Gets the failure message.
    /// </summary>
    internal string Message { get; }
}