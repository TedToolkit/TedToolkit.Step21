// -----------------------------------------------------------------------
// <copyright file="ExpressBindingDiagnostic.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Describes one deterministic source-located EXPRESS binding failure.
/// </summary>
internal sealed class ExpressBindingDiagnostic
{
    /// <summary>
    /// Initializes a binding diagnostic.
    /// </summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="sourceLocation">The exact source location.</param>
    internal ExpressBindingDiagnostic(
        string code,
        string message,
        ExpressSourceLocation sourceLocation)
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
    /// Gets the diagnostic message.
    /// </summary>
    internal string Message { get; }

    /// <summary>
    /// Gets the exact source location.
    /// </summary>
    internal ExpressSourceLocation SourceLocation { get; }
}