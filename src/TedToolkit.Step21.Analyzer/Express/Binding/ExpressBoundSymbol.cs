// -----------------------------------------------------------------------
// <copyright file="ExpressBoundSymbol.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Provides immutable identity for one resolved schema-level EXPRESS declaration.
/// </summary>
internal sealed class ExpressBoundSymbol
{
    /// <summary>
    /// Initializes a bound declaration identity.
    /// </summary>
    /// <param name="name">The declaration's source spelling.</param>
    /// <param name="kind">The declaration family.</param>
    /// <param name="declaringSchema">The owning schema identity.</param>
    /// <param name="span">The declaration span.</param>
    internal ExpressBoundSymbol(
        string name,
        ExpressDeclarationKind kind,
        ExpressBoundSchemaIdentity declaringSchema,
        ExpressSourceSpan span)
    {
        Name = name;
        Kind = kind;
        DeclaringSchema = declaringSchema;
        Span = span;
    }

    /// <summary>
    /// Gets the source spelling of the declaration name.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Gets the declaration family.
    /// </summary>
    internal ExpressDeclarationKind Kind { get; }

    /// <summary>
    /// Gets the owning schema identity.
    /// </summary>
    internal ExpressBoundSchemaIdentity DeclaringSchema { get; }

    /// <summary>
    /// Gets the declaration span.
    /// </summary>
    internal ExpressSourceSpan Span { get; }
}