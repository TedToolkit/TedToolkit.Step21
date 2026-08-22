// -----------------------------------------------------------------------
// <copyright file="ExpressExpressionEmissionContext.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Supplies static C# spellings owned by the surrounding generated schema operation.
/// </summary>
internal sealed class ExpressExpressionEmissionContext
{
    /// <summary>
    /// Initializes one expression emission context.
    /// </summary>
    /// <param name="resolveReference">Maps a resolved EXPRESS value or callable to static C#.</param>
    /// <param name="selfExpression">The static C# expression for SELF, when available.</param>
    /// <param name="resolveModelFunction">Maps a model-context function and its emitted arguments to static C#.</param>
    /// <param name="resolveValueEquality">Maps schema-dependent value equality to static C#.</param>
    /// <param name="resolveAttribute">Maps a qualified entity attribute access to static C#.</param>
    internal ExpressExpressionEmissionContext(
        Func<ExpressBoundName, string> resolveReference,
        string? selfExpression = null,
        Func<string, IReadOnlyList<string>, string>? resolveModelFunction = null,
        Func<ExpressExpressionType, string, string, string>? resolveValueEquality = null,
        Func<ExpressBoundName, string, string>? resolveAttribute = null)
    {
        ResolveReference = resolveReference;
        SelfExpression = selfExpression;
        ResolveModelFunction = resolveModelFunction;
        ResolveValueEquality = resolveValueEquality;
        ResolveAttribute = resolveAttribute;
    }

    /// <summary>
    /// Gets the declaration-reference resolver.
    /// </summary>
    internal Func<ExpressBoundName, string> ResolveReference { get; }

    /// <summary>
    /// Gets the static SELF expression, when the surrounding operation has one.
    /// </summary>
    internal string? SelfExpression { get; }

    /// <summary>
    /// Gets the static model-context function resolver.
    /// </summary>
    internal Func<string, IReadOnlyList<string>, string>? ResolveModelFunction { get; }

    /// <summary>
    /// Gets the static schema-dependent value-equality resolver.
    /// </summary>
    internal Func<ExpressExpressionType, string, string, string>? ResolveValueEquality { get; }

    /// <summary>
    /// Gets the generated qualified-attribute resolver, when the enclosing operation supplies one.
    /// </summary>
    internal Func<ExpressBoundName, string, string>? ResolveAttribute { get; }

    /// <summary>
    /// Creates an equivalent context with a scoped declaration-reference resolver.
    /// </summary>
    /// <param name="resolveReference">The scoped resolver.</param>
    /// <returns>The scoped immutable context.</returns>
    internal ExpressExpressionEmissionContext WithReferenceResolver(Func<ExpressBoundName, string> resolveReference)
    {
        return new(resolveReference, SelfExpression, ResolveModelFunction, ResolveValueEquality, ResolveAttribute);
    }
}