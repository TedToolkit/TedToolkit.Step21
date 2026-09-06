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
    /// <param name="mayReturnIndeterminate">Identifies function applications whose generated method can return UNKNOWN.</param>
    /// <param name="resolveNarrowedScalarReference">Projects a SELECT carrier to a branch-proven scalar value.</param>
    /// <param name="resolveSelectToEntityValue">Projects a SELECT value to one compatible entity alternative.</param>
    /// <param name="resolveNarrowedEntityCarrier">Projects an explicitly typed SELECT carrier to a proven entity.</param>
    /// <param name="resolveAggregateElement">Adapts one present aggregate element to its declared element type.</param>
    /// <param name="resolveAggregateType">Resolves the aggregate value beneath a nominal type.</param>
    /// <param name="resolveSelectedAggregateIndex">Indexes a schema-defined SELECT of aggregate values.</param>
    /// <param name="resolveAggregateSource">Projects a nominal or selected aggregate to a shared read-only carrier.</param>
    /// <param name="resolveDefinedValueType">Resolves the generated semantic carrier beneath defined-value wrappers.</param>
    /// <param name="isSelectValueType">Identifies nominal SELECT carriers even when expression inference is unresolved.</param>
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
        Func<ExpressBoundExpression, bool>? isKnownDeterminate = null,
        Func<ExpressBoundExpression, bool>? mayReturnIndeterminate = null,
        Func<ExpressBoundName, ExpressBoundType, string, ExpressBoundScalarType, string?>?
            resolveNarrowedScalarReference = null,
        Func<ExpressBoundExpression, string, ExpressBoundNamedType, string>?
            resolveSelectToEntityValue = null,
        Func<ExpressBoundType, string, ExpressBoundNamedType, string>?
            resolveNarrowedEntityCarrier = null,
        Func<ExpressBoundExpression, string, ExpressBoundType, bool, string>?
            resolveAggregateElement = null,
        Func<ExpressBoundType, ExpressBoundAggregateType?>? resolveAggregateType = null,
        Func<ExpressBoundExpression, IReadOnlyList<string>, string, string?>?
            resolveSelectedAggregateIndex = null,
        Func<ExpressBoundType, string, ExpressBoundAggregateType, string?>?
            resolveAggregateSource = null,
        Func<ExpressBoundType, ExpressBoundType>? resolveDefinedValueType = null,
        Func<ExpressBoundType, bool>? isSelectValueType = null)
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
        MayReturnIndeterminate = mayReturnIndeterminate;
        ResolveNarrowedScalarReference = resolveNarrowedScalarReference;
        ResolveSelectToEntityValue = resolveSelectToEntityValue;
        ResolveNarrowedEntityCarrier = resolveNarrowedEntityCarrier;
        ResolveAggregateElement = resolveAggregateElement;
        ResolveAggregateType = resolveAggregateType;
        ResolveSelectedAggregateIndex = resolveSelectedAggregateIndex;
        ResolveAggregateSource = resolveAggregateSource;
        ResolveDefinedValueType = resolveDefinedValueType;
        IsSelectValueType = isSelectValueType;
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
    /// Gets the enclosing schema's transitive function-indeterminacy classifier.
    /// </summary>
    internal Func<ExpressBoundExpression, bool>? MayReturnIndeterminate { get; }

    /// <summary>
    /// Gets the resolver for a SELECT carrier narrowed to a scalar branch.
    /// </summary>
    internal Func<ExpressBoundName, ExpressBoundType, string, ExpressBoundScalarType, string?>?
        ResolveNarrowedScalarReference
    { get; }

    /// <summary>
    /// Gets the resolver that safely projects a SELECT value to a compatible entity alternative.
    /// </summary>
    internal Func<ExpressBoundExpression, string, ExpressBoundNamedType, string>?
        ResolveSelectToEntityValue
    { get; }

    /// <summary>
    /// Gets the resolver that projects a carrier with an explicit nominal type to a proven entity.
    /// </summary>
    internal Func<ExpressBoundType, string, ExpressBoundNamedType, string>?
        ResolveNarrowedEntityCarrier
    { get; }

    /// <summary>
    /// Gets the resolver that adapts a present aggregate element to its declared element type.
    /// </summary>
    internal Func<ExpressBoundExpression, string, ExpressBoundType, bool, string>?
        ResolveAggregateElement
    { get; }

    /// <summary>
    /// Gets the resolver for an aggregate value beneath its nominal wrappers.
    /// </summary>
    internal Func<ExpressBoundType, ExpressBoundAggregateType?>? ResolveAggregateType { get; }

    /// <summary>
    /// Gets the schema-aware lowering for an index over a SELECT of aggregate values.
    /// </summary>
    internal Func<ExpressBoundExpression, IReadOnlyList<string>, string, string?>?
        ResolveSelectedAggregateIndex
    { get; }

    /// <summary>
    /// Gets the schema-aware projection of a nominal or selected aggregate to a shared read-only carrier.
    /// </summary>
    internal Func<ExpressBoundType, string, ExpressBoundAggregateType, string?>? ResolveAggregateSource { get; }

    /// <summary>
    /// Gets the resolver for the generated semantic carrier beneath defined-value wrappers.
    /// </summary>
    internal Func<ExpressBoundType, ExpressBoundType>? ResolveDefinedValueType { get; }

    /// <summary>
    /// Gets the classifier for nominal SELECT value carriers.
    /// </summary>
    internal Func<ExpressBoundType, bool>? IsSelectValueType { get; }

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
            IsKnownDeterminate,
            MayReturnIndeterminate,
            ResolveNarrowedScalarReference,
            ResolveSelectToEntityValue,
            ResolveNarrowedEntityCarrier,
            ResolveAggregateElement,
            ResolveAggregateType,
            ResolveSelectedAggregateIndex,
            ResolveAggregateSource,
            ResolveDefinedValueType,
            IsSelectValueType);
    }
}