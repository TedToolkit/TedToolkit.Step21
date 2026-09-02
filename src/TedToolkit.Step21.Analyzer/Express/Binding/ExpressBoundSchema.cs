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
    /// <param name="lexicalNames">The names declared by nested algorithm scopes.</param>
    /// <param name="nameReferences">The resolved expression and statement names.</param>
    /// <param name="expressions">The immutable typed outermost expression trees.</param>
    /// <param name="indeterminateFunctions">Functions whose result can be indeterminate.</param>
    /// <param name="indeterminateLocals">Local variables that can hold an indeterminate value.</param>
    internal ExpressBoundSchema(
        ExpressBoundSchemaIdentity identity,
        IEnumerable<ExpressBoundImport> imports,
        IEnumerable<ExpressBoundDeclaration> declarations,
        IEnumerable<ExpressBoundDeclaration> nestedDeclarations,
        IEnumerable<ExpressBoundName> lexicalNames,
        IEnumerable<ExpressBoundNameReference> nameReferences,
        IEnumerable<ExpressBoundExpression> expressions,
        IEnumerable<ExpressBoundSymbol> indeterminateFunctions,
        IEnumerable<ExpressBoundName> indeterminateLocals)
    {
        Identity = identity;
        Imports = new ReadOnlyCollection<ExpressBoundImport>(imports.ToArray());
        Declarations = new ReadOnlyCollection<ExpressBoundDeclaration>(declarations.ToArray());
        NestedDeclarations = new ReadOnlyCollection<ExpressBoundDeclaration>(nestedDeclarations.ToArray());
        LexicalNames = new ReadOnlyCollection<ExpressBoundName>(lexicalNames.ToArray());
        NameReferences = new ReadOnlyCollection<ExpressBoundNameReference>(nameReferences.ToArray());
        Expressions = new ReadOnlyCollection<ExpressBoundExpression>(expressions.ToArray());
        IndeterminateFunctions = new ReadOnlyCollection<ExpressBoundSymbol>(indeterminateFunctions.ToArray());
        IndeterminateLocals = new ReadOnlyCollection<ExpressBoundName>(indeterminateLocals.ToArray());
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
    /// Gets names declared by nested algorithm scopes, including names that are never referenced.
    /// </summary>
    internal IReadOnlyList<ExpressBoundName> LexicalNames { get; }

    /// <summary>
    /// Gets expression and statement names resolved in source order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundNameReference> NameReferences { get; }

    /// <summary>
    /// Gets the immutable typed outermost expression trees in source order.
    /// </summary>
    internal IReadOnlyList<ExpressBoundExpression> Expressions { get; }

    /// <summary>
    /// Gets functions whose generated result must represent the EXPRESS indeterminate value.
    /// </summary>
    internal IReadOnlyCollection<ExpressBoundSymbol> IndeterminateFunctions { get; }

    /// <summary>
    /// Gets local variables whose generated storage must represent the EXPRESS indeterminate value.
    /// </summary>
    internal IReadOnlyCollection<ExpressBoundName> IndeterminateLocals { get; }
}