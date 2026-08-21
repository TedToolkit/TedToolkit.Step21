// -----------------------------------------------------------------------
// <copyright file="ExpressValueProjection.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Projects one supported EXPRESS defined type into its generated nominal value shape.
/// </summary>
internal sealed class ExpressValueProjection
{
    /// <summary>
    /// Initializes one supported value projection.
    /// </summary>
    /// <param name="schema">The declaring schema.</param>
    /// <param name="declaration">The defined-type declaration.</param>
    /// <param name="resolver">The closed-set generated type resolver.</param>
    internal ExpressValueProjection(
        ExpressBoundSchema schema,
        ExpressBoundDefinedType declaration,
        ExpressGeneratedTypeResolver resolver)
    {
        Schema = schema;
        Declaration = declaration;
        Resolver = resolver;
        Name = ExpressEntityProjection.ToPascalCase(declaration.Name);
    }

    /// <summary>
    /// Gets the declaring schema.
    /// </summary>
    internal ExpressBoundSchema Schema { get; }

    /// <summary>
    /// Gets the bound defined-type declaration.
    /// </summary>
    internal ExpressBoundDefinedType Declaration { get; }

    /// <summary>
    /// Gets the generated C# type name.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Gets the closed-set generated type resolver.
    /// </summary>
    internal ExpressGeneratedTypeResolver Resolver { get; }

    /// <summary>
    /// Creates supported projections in schema declaration order.
    /// </summary>
    /// <param name="compilation">The closed bound schema compilation.</param>
    /// <param name="resolver">The supported type resolver.</param>
    /// <returns>The supported value projections.</returns>
    internal static IReadOnlyList<ExpressValueProjection> Create(
        ExpressSchemaCompilation compilation,
        ExpressGeneratedTypeResolver resolver)
    {
        var schemasByIdentity = compilation.Schemas.ToDictionary(schema => schema.Identity);
        return resolver.GetSupportedDeclarations(compilation)
            .Select(declaration => new ExpressValueProjection(
                schemasByIdentity[declaration.DeclaringSchema],
                declaration,
                resolver))
            .ToArray();
    }
}