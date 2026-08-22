// -----------------------------------------------------------------------
// <copyright file="ExpressBoundName.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Provides immutable identity for one schema or lexical EXPRESS name.
/// </summary>
internal sealed class ExpressBoundName
{
    /// <summary>
    /// Initializes a bound name.
    /// </summary>
    /// <param name="name">The declaration spelling.</param>
    /// <param name="kind">The declaration class.</param>
    /// <param name="type">The declared type when one is statically available.</param>
    /// <param name="schemaDeclaration">The schema declaration, if this is a schema name.</param>
    /// <param name="span">The declaration span.</param>
    /// <param name="isOptional">Whether this value name can denote the EXPRESS indeterminate value.</param>
    internal ExpressBoundName(
        string name,
        ExpressBoundNameKind kind,
        ExpressBoundType? type,
        ExpressBoundSymbol? schemaDeclaration,
        ExpressSourceSpan span,
        bool isOptional = false)
    {
        Name = name;
        Kind = kind;
        Type = type;
        SchemaDeclaration = schemaDeclaration;
        Span = span;
        IsOptional = isOptional;
    }

    /// <summary>
    /// Gets the declaration spelling.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Gets the declaration class.
    /// </summary>
    internal ExpressBoundNameKind Kind { get; }

    /// <summary>
    /// Gets the declared type when one is statically available.
    /// </summary>
    internal ExpressBoundType? Type { get; }

    /// <summary>
    /// Gets the schema declaration, if this is a schema name.
    /// </summary>
    internal ExpressBoundSymbol? SchemaDeclaration { get; }

    /// <summary>
    /// Gets the declaration span.
    /// </summary>
    internal ExpressSourceSpan Span { get; }

    /// <summary>
    /// Gets a value indicating whether this value name can denote the EXPRESS indeterminate value.
    /// </summary>
    internal bool IsOptional { get; }
}