// -----------------------------------------------------------------------
// <copyright file="ExpressBoundSchema.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents one independently valid schema in the closed compilation set.
/// </summary>
internal sealed class ExpressBoundSchema
{
    /// <summary>
    /// Initializes a bound schema.
    /// </summary>
    /// <param name="identity">The schema identity.</param>
    /// <param name="imports">The resolved imports.</param>
    /// <param name="declarations">The locally declared symbols.</param>
    /// <param name="nestedDeclarations">The declarations owned by nested algorithm scopes.</param>
    /// <param name="nameReferences">The resolved expression and statement names.</param>
    internal ExpressBoundSchema(
        ExpressBoundSchemaIdentity identity,
        IEnumerable<ExpressBoundImport> imports,
        IEnumerable<ExpressBoundDeclaration> declarations,
        IEnumerable<ExpressBoundDeclaration> nestedDeclarations,
        IEnumerable<ExpressBoundNameReference> nameReferences)
    {
        Identity = identity;
        Imports = new ReadOnlyCollection<ExpressBoundImport>(imports.ToArray());
        Declarations = new ReadOnlyCollection<ExpressBoundDeclaration>(declarations.ToArray());
        NestedDeclarations = new ReadOnlyCollection<ExpressBoundDeclaration>(nestedDeclarations.ToArray());
        NameReferences = new ReadOnlyCollection<ExpressBoundNameReference>(nameReferences.ToArray());
    }

    /// <summary>
    /// Gets the schema identity.
    /// </summary>
    internal ExpressBoundSchemaIdentity Identity { get; }

    /// <summary>
    /// Gets the source spelling of the schema name.
    /// </summary>
    internal string Name
    {
        get
        {
            return Identity.Name;
        }
    }

    /// <summary>
    /// Gets the resolved imports in source order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundImport> Imports { get; }

    /// <summary>
    /// Gets the local declarations in source order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundDeclaration> Declarations { get; }

    /// <summary>
    /// Gets declarations owned by nested algorithm scopes in source order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundDeclaration> NestedDeclarations { get; }

    /// <summary>
    /// Gets expression and statement names resolved in source order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundNameReference> NameReferences { get; }
}