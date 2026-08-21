// -----------------------------------------------------------------------
// <copyright file="ExpressGeneratorCompilation.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Retains the supplied Roslyn inputs beside their deterministic EXPRESS compilation.
/// </summary>
internal sealed class ExpressGeneratorCompilation
{
    /// <summary>
    /// Initializes a generator compilation result.
    /// </summary>
    /// <param name="inputs">The supplied additional files.</param>
    /// <param name="compilation">The closed EXPRESS compilation.</param>
    internal ExpressGeneratorCompilation(
        in ImmutableArray<ExpressGeneratorInput> inputs,
        ExpressSchemaCompilation compilation)
    {
        Inputs = inputs;
        Compilation = compilation;
    }

    /// <summary>
    /// Gets the supplied additional files.
    /// </summary>
    internal ImmutableArray<ExpressGeneratorInput> Inputs { get; }

    /// <summary>
    /// Gets the closed EXPRESS compilation.
    /// </summary>
    internal ExpressSchemaCompilation Compilation { get; }
}