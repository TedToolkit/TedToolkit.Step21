// -----------------------------------------------------------------------
// <copyright file="ExpressExpressionType.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Retains the static EXPRESS result category and, when available, its complete declared type.
/// </summary>
internal sealed class ExpressExpressionType
{
    /// <summary>
    /// Initializes an expression result type.
    /// </summary>
    /// <param name="kind">The statically known value category.</param>
    /// <param name="declaredType">The complete declared type when one is available.</param>
    /// <param name="canBeIndeterminate">Whether evaluation can produce the EXPRESS indeterminate value.</param>
    /// <param name="definedValueDepth">The number of generated defined-value wrappers around the semantic value.</param>
    /// <param name="enumerationValues">The declared enumeration order when this is an enumeration.</param>
    internal ExpressExpressionType(
        ExpressExpressionTypeKind kind,
        ExpressBoundType? declaredType = null,
        bool canBeIndeterminate = false,
        int definedValueDepth = 0,
        IReadOnlyList<string>? enumerationValues = null)
    {
        Kind = kind;
        DeclaredType = declaredType;
        CanBeIndeterminate = canBeIndeterminate;
        DefinedValueDepth = definedValueDepth;
        EnumerationValues = enumerationValues;
    }

    /// <summary>
    /// Gets the statically known value category.
    /// </summary>
    internal ExpressExpressionTypeKind Kind { get; }

    /// <summary>
    /// Gets the complete declared type when one is available.
    /// </summary>
    internal ExpressBoundType? DeclaredType { get; }

    /// <summary>
    /// Gets a value indicating whether evaluation can produce the EXPRESS indeterminate value.
    /// </summary>
    internal bool CanBeIndeterminate { get; }

    /// <summary>
    /// Gets the number of generated defined-value wrappers around the semantic value.
    /// </summary>
    internal int DefinedValueDepth { get; }

    /// <summary>
    /// Gets the declared enumeration order, when applicable.
    /// </summary>
    internal IReadOnlyList<string>? EnumerationValues { get; }

    /// <summary>
    /// Returns this static type with the requested indeterminate-value capability.
    /// </summary>
    /// <param name="canBeIndeterminate">Whether the resulting expression can be indeterminate.</param>
    /// <returns>This instance when unchanged; otherwise, an equivalent immutable type.</returns>
    internal ExpressExpressionType WithIndeterminate(bool canBeIndeterminate)
    {
        return CanBeIndeterminate == canBeIndeterminate
            ? this
            : new ExpressExpressionType(
                Kind,
                DeclaredType,
                canBeIndeterminate,
                DefinedValueDepth,
                EnumerationValues);
    }
}