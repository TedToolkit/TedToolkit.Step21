// -----------------------------------------------------------------------
// <copyright file="ExpressBoundExpression.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Represents one immutable, source-located and statically typed EXPRESS expression.
/// </summary>
internal sealed class ExpressBoundExpression
{
    /// <summary>
    /// Initializes a bound expression node.
    /// </summary>
    /// <param name="kind">The semantic expression shape.</param>
    /// <param name="type">The static result type.</param>
    /// <param name="sourceText">The compact retained source spelling.</param>
    /// <param name="operation">The operator, application, or control name when applicable.</param>
    /// <param name="reference">The resolved declaration identity when applicable.</param>
    /// <param name="children">The ordered operand expressions.</param>
    /// <param name="span">The complete source span.</param>
    internal ExpressBoundExpression(
        ExpressExpressionKind kind,
        ExpressExpressionType type,
        string sourceText,
        string? operation,
        ExpressBoundName? reference,
        IEnumerable<ExpressBoundExpression> children,
        ExpressSourceSpan span)
    {
        Kind = kind;
        Type = type;
        SourceText = sourceText;
        Operation = operation;
        Reference = reference;
        Children = new ReadOnlyCollection<ExpressBoundExpression>(children.ToArray());
        Span = span;
    }

    /// <summary>
    /// Gets the semantic expression shape.
    /// </summary>
    internal ExpressExpressionKind Kind { get; }

    /// <summary>
    /// Gets the static result type.
    /// </summary>
    internal ExpressExpressionType Type { get; }

    /// <summary>
    /// Gets the compact retained source spelling.
    /// </summary>
    internal string SourceText { get; }

    /// <summary>
    /// Gets the operator, application, or control name when applicable.
    /// </summary>
    internal string? Operation { get; }

    /// <summary>
    /// Gets the resolved declaration identity when applicable.
    /// </summary>
    internal ExpressBoundName? Reference { get; }

    /// <summary>
    /// Gets the ordered immutable operand expressions.
    /// </summary>
    internal IReadOnlyList<ExpressBoundExpression> Children { get; }

    /// <summary>
    /// Gets the complete source span.
    /// </summary>
    internal ExpressSourceSpan Span { get; }

    /// <summary>
    /// Enumerates this expression and all operand expressions depth first.
    /// </summary>
    /// <returns>The expression sequence.</returns>
    internal IEnumerable<ExpressBoundExpression> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var descendant in child.DescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }
}