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
    /// <param name="resolveApplication">Maps a resolved function application to static C#.</param>
    /// <param name="safeIndices">Identifies aggregate and REPEAT-variable pairs whose index access is proven present.</param>
    /// <param name="resolveLexicalBound">Maps a lexical aggregate bound to its generated value and bound type.</param>
    /// <param name="genericTypeLabels">Identifies type labels closed by the enclosing generated generic method.</param>
    /// <param name="allocateTemporaryName">Allocates a deterministic private C# name within the generated method.</param>
    /// <param name="isKnownDeterminate">Identifies expressions made determinate by the enclosing control-flow branch.</param>
    internal ExpressExpressionEmissionContext(
        Func<ExpressBoundName, ExpressBoundType?, string?, ExpressBoundSymbol?, string> resolveReference,
        string? selfExpression = null,
        Func<string, ExpressBoundExpression, IReadOnlyList<string>, string>? resolveModelFunction = null,
        Func<ExpressBoundExpression, string, ExpressBoundExpression, string, ExpressBoundType?, string>?
            resolveValueEquality = null,
        Func<ExpressBoundExpression, ExpressBoundName, string, string>? resolveAttribute = null,
        Func<ExpressBoundExpression, IReadOnlyList<string>, string>? resolveApplication = null,
        IReadOnlyCollection<KeyValuePair<ExpressBoundName, ExpressBoundName>>? safeIndices = null,
        Func<string, (string Code, ExpressBoundType Type)?>? resolveLexicalBound = null,
        IReadOnlyCollection<string>? genericTypeLabels = null,
        Func<string, string>? allocateTemporaryName = null,
        Func<ExpressBoundExpression, bool>? isKnownDeterminate = null)
    {
        ResolveReference = resolveReference;
        SelfExpression = selfExpression;
        ResolveModelFunction = resolveModelFunction;
        ResolveValueEquality = resolveValueEquality;
        ResolveAttribute = resolveAttribute;
        ResolveApplication = resolveApplication;
        SafeIndices = safeIndices ?? [];
        ResolveLexicalBound = resolveLexicalBound;
        GenericTypeLabels = genericTypeLabels ?? [];
        IsKnownDeterminate = isKnownDeterminate;
        if (allocateTemporaryName is null)
        {
            var temporaryOrdinal = 0;
            allocateTemporaryName = prefix => prefix
                + (temporaryOrdinal++).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        AllocateTemporaryName = allocateTemporaryName;
    }

    /// <summary>
    /// Gets the declaration-reference resolver.
    /// </summary>
    internal Func<ExpressBoundName, ExpressBoundType?, string?, ExpressBoundSymbol?, string> ResolveReference { get; }

    /// <summary>
    /// Gets the static SELF expression, when the surrounding operation has one.
    /// </summary>
    internal string? SelfExpression { get; }

    /// <summary>
    /// Gets the static model-context function resolver.
    /// </summary>
    internal Func<string, ExpressBoundExpression, IReadOnlyList<string>, string>? ResolveModelFunction { get; }

    /// <summary>
    /// Gets the static schema-dependent value-equality resolver.
    /// </summary>
    internal Func<ExpressBoundExpression, string, ExpressBoundExpression, string, ExpressBoundType?, string>?
        ResolveValueEquality
    { get; }

    /// <summary>
    /// Gets the generated qualified-attribute resolver, when the enclosing operation supplies one.
    /// </summary>
    internal Func<ExpressBoundExpression, ExpressBoundName, string, string>? ResolveAttribute { get; }

    /// <summary>
    /// Gets the generated function-application resolver, when the enclosing operation supplies one.
    /// </summary>
    internal Func<ExpressBoundExpression, IReadOnlyList<string>, string>? ResolveApplication { get; }

    /// <summary>
    /// Gets aggregate and REPEAT-variable pairs whose index access is proven present.
    /// </summary>
    internal IReadOnlyCollection<KeyValuePair<ExpressBoundName, ExpressBoundName>> SafeIndices { get; }

    /// <summary>
    /// Gets the lexical aggregate-bound resolver, when the enclosing function supplies one.
    /// </summary>
    internal Func<string, (string Code, ExpressBoundType Type)?>? ResolveLexicalBound { get; }

    /// <summary>
    /// Gets type labels closed by the enclosing generated generic method.
    /// </summary>
    internal IReadOnlyCollection<string> GenericTypeLabels { get; }

    /// <summary>
    /// Gets the deterministic generated-method temporary-name allocator.
    /// </summary>
    internal Func<string, string> AllocateTemporaryName { get; }

    /// <summary>
    /// Gets the branch-scoped determinacy proof, when one is available.
    /// </summary>
    internal Func<ExpressBoundExpression, bool>? IsKnownDeterminate { get; }

    /// <summary>
    /// Creates an equivalent context with a scoped declaration-reference resolver.
    /// </summary>
    /// <param name="resolveReference">The scoped resolver.</param>
    /// <returns>The scoped immutable context.</returns>
    internal ExpressExpressionEmissionContext WithReferenceResolver(
        Func<ExpressBoundName, ExpressBoundType?, string?, ExpressBoundSymbol?, string> resolveReference)
    {
        return new(
            resolveReference,
            SelfExpression,
            ResolveModelFunction,
            ResolveValueEquality,
            ResolveAttribute,
            ResolveApplication,
            SafeIndices,
            ResolveLexicalBound,
            GenericTypeLabels,
            AllocateTemporaryName,
            IsKnownDeterminate);
    }
}