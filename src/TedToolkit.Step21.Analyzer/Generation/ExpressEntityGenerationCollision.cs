// -----------------------------------------------------------------------
// <copyright file="ExpressEntityGenerationCollision.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Describes one source-located generated C# name collision.
/// </summary>
internal sealed class ExpressEntityGenerationCollision
{
    /// <summary>
    /// Initializes a generated C# name collision.
    /// </summary>
    /// <param name="schema">The affected schema.</param>
    /// <param name="location">The colliding source name location.</param>
    /// <param name="sourceName">The colliding source name.</param>
    /// <param name="generatedName">The generated C# name.</param>
    internal ExpressEntityGenerationCollision(
        ExpressBoundSchema schema,
        ExpressSourceLocation location,
        string sourceName,
        string generatedName)
    {
        Schema = schema;
        Location = location;
        SourceName = sourceName;
        GeneratedName = generatedName;
    }

    /// <summary>
    /// Gets the affected schema.
    /// </summary>
    internal ExpressBoundSchema Schema { get; }

    /// <summary>
    /// Gets the colliding source name location.
    /// </summary>
    internal ExpressSourceLocation Location { get; }

    /// <summary>
    /// Gets the colliding source name.
    /// </summary>
    internal string SourceName { get; }

    /// <summary>
    /// Gets the generated C# name.
    /// </summary>
    internal string GeneratedName { get; }
}