// -----------------------------------------------------------------------
// <copyright file="ExpressEntityGenerationFailure.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Describes one source-located entity shape that cannot be represented safely in C#.
/// </summary>
internal sealed class ExpressEntityGenerationFailure
{
    /// <summary>
    /// Initializes an entity-generation failure.
    /// </summary>
    /// <param name="schema">The affected schema.</param>
    /// <param name="location">The unsupported declaration location.</param>
    /// <param name="message">The bounded failure detail.</param>
    internal ExpressEntityGenerationFailure(
        ExpressBoundSchema schema,
        ExpressSourceLocation location,
        string message)
    {
        Schema = schema;
        Location = location;
        Message = message;
    }

    /// <summary>
    /// Gets the affected schema.
    /// </summary>
    internal ExpressBoundSchema Schema { get; }

    /// <summary>
    /// Gets the unsupported declaration location.
    /// </summary>
    internal ExpressSourceLocation Location { get; }

    /// <summary>
    /// Gets the bounded failure detail.
    /// </summary>
    internal string Message { get; }
}