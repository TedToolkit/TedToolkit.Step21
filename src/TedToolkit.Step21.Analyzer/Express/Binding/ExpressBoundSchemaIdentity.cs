// -----------------------------------------------------------------------
// <copyright file="ExpressBoundSchemaIdentity.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Identifies one supplied EXPRESS schema independently of input ordering.
/// </summary>
internal sealed class ExpressBoundSchemaIdentity
{
    /// <summary>
    /// Initializes a schema identity.
    /// </summary>
    /// <param name="name">The source spelling of the schema name.</param>
    /// <param name="span">The schema declaration span.</param>
    internal ExpressBoundSchemaIdentity(string name, ExpressSourceSpan span)
    {
        Name = name;
        Span = span;
    }

    /// <summary>
    /// Gets the source spelling of the schema name.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Gets the schema declaration span.
    /// </summary>
    internal ExpressSourceSpan Span { get; }
}