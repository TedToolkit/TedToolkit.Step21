// -----------------------------------------------------------------------
// <copyright file="ExpressClosedSetBindingStage.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Owns closed-set declaration, import, name, and type binding.
/// </summary>
internal static class ExpressClosedSetBindingStage
{
    /// <summary>
    /// Binds every independently valid parsed schema without expression or flow analysis.
    /// </summary>
    /// <param name="syntaxCompilation">The deterministic syntax-stage output.</param>
    /// <returns>The closed-set binding output and complete diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="syntaxCompilation"/> is null.</exception>
    internal static ExpressBindingCompilation Bind(ExpressSyntaxCompilation syntaxCompilation)
    {
        return ExpressSchemaBinder.BindClosedSet(syntaxCompilation);
    }
}