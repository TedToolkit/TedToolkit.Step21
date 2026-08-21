// -----------------------------------------------------------------------
// <copyright file="ExpressBoundImport.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents one resolved local import name and its foreign declaration.
/// </summary>
internal sealed class ExpressBoundImport
{
    /// <summary>
    /// Initializes a resolved import.
    /// </summary>
    /// <param name="kind">The interface specification kind.</param>
    /// <param name="localName">The visible local or reference name.</param>
    /// <param name="declaration">The resolved foreign declaration.</param>
    /// <param name="span">The imported-name source span.</param>
    internal ExpressBoundImport(
        ExpressImportKind kind,
        string localName,
        ExpressBoundSymbol declaration,
        ExpressSourceSpan span)
    {
        Kind = kind;
        LocalName = localName;
        Declaration = declaration;
        Span = span;
    }

    /// <summary>
    /// Gets the interface specification kind.
    /// </summary>
    internal ExpressImportKind Kind { get; }

    /// <summary>
    /// Gets the visible local or reference name.
    /// </summary>
    internal string LocalName { get; }

    /// <summary>
    /// Gets the resolved foreign declaration.
    /// </summary>
    internal ExpressBoundSymbol Declaration { get; }

    /// <summary>
    /// Gets the imported-name source span.
    /// </summary>
    internal ExpressSourceSpan Span { get; }
}