// -----------------------------------------------------------------------
// <copyright file="ExpressSyntaxDiagnostic.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express;

/// <summary>
/// Retains one deterministic internal EXPRESS syntax diagnostic.
/// </summary>
internal sealed class ExpressSyntaxDiagnostic
{
    /// <summary>
    /// Initializes a syntax diagnostic.
    /// </summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The parser or boundary message.</param>
    /// <param name="sourceLocation">The source location.</param>
    internal ExpressSyntaxDiagnostic(string code, string message, ExpressSourceLocation sourceLocation)
    {
        Code = code;
        Message = message;
        SourceLocation = sourceLocation;
    }

    /// <summary>
    /// Gets the stable diagnostic code.
    /// </summary>
    internal string Code { get; }

    /// <summary>
    /// Gets the parser or boundary message.
    /// </summary>
    internal string Message { get; }

    /// <summary>
    /// Gets the source location.
    /// </summary>
    internal ExpressSourceLocation SourceLocation { get; }
}