// -----------------------------------------------------------------------
// <copyright file="ExpressGeneratedExpression.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Retains one strongly typed static C# expression composed from bound EXPRESS semantics.
/// </summary>
internal sealed class ExpressGeneratedExpression
{
    /// <summary>
    /// Initializes a generated expression.
    /// </summary>
    /// <param name="code">The complete parenthesized C# expression.</param>
    /// <param name="typeName">The fully qualified C# result type.</param>
    internal ExpressGeneratedExpression(string code, string typeName)
    {
        Code = code;
        TypeName = typeName;
    }

    /// <summary>
    /// Gets the complete parenthesized C# expression.
    /// </summary>
    internal string Code { get; }

    /// <summary>
    /// Gets the fully qualified C# result type.
    /// </summary>
    internal string TypeName { get; }
}