// -----------------------------------------------------------------------
// <copyright file="ExpressBindingCompilation.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Owns the binding-stage result and the source syntax needed only while semantic analysis runs.
/// </summary>
internal sealed class ExpressBindingCompilation
{
    private readonly ReadOnlyDictionary<ExpressBoundDeclaration, ExpressRuleSyntax> _declarationSyntax;

    /// <summary>
    /// Initializes one closed-set binding handoff.
    /// </summary>
    /// <param name="compilation">The syntax-free bound compilation.</param>
    /// <param name="declarationSyntax">The binding-only source syntax for each declaration.</param>
    internal ExpressBindingCompilation(
        ExpressSchemaCompilation compilation,
        IReadOnlyDictionary<ExpressBoundDeclaration, ExpressRuleSyntax> declarationSyntax)
    {
        Compilation = compilation;
        _declarationSyntax = new(declarationSyntax.ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    /// <summary>
    /// Gets the syntax-free bound compilation.
    /// </summary>
    internal ExpressSchemaCompilation Compilation { get; }

    /// <summary>
    /// Gets the source syntax consumed by semantic analysis for one bound declaration.
    /// </summary>
    /// <param name="declaration">The bound declaration.</param>
    /// <returns>The binding-owned source syntax.</returns>
    internal ExpressRuleSyntax GetSyntax(ExpressBoundDeclaration declaration)
    {
        return _declarationSyntax[declaration];
    }
}