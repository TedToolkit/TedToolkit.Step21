// -----------------------------------------------------------------------
// <copyright file="BindingTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Tests.ExpressExpressionTests;

/// <summary>
/// Proves the immutable, typed EXPRESS expression boundary consumed by static generation.
/// </summary>
internal sealed class BindingTests
{
    private const string Schema = """
        SCHEMA expression_shapes;

        CONSTANT
          logical_value : LOGICAL := UNKNOWN XOR TRUE;
          real_value : REAL := 1.25;
          binary_value : BINARY := %101;
          string_value : STRING := 'a' || 'b';
          indeterminate_value : LOGICAL := ?;
        END_CONSTANT;

        TYPE state = ENUMERATION OF (on, off);
        END_TYPE;

        ENTITY item;
          numeric_value : INTEGER;
        END_ENTITY;

        FUNCTION exercise(
          items : LIST [0:?] OF INTEGER;
          candidate : item;
          flag : LOGICAL) : INTEGER;
          LOCAL
            selected : LIST [0:?] OF INTEGER := [];
            text : STRING := 'abc';
            total : INTEGER := 0;
          END_LOCAL;

          total := -1 + 2 * 3 ** 2 DIV 1 MOD 2;
          total := ABS(total);
          total := candidate.numeric_value;
          total := candidate\item.numeric_value;
          total := items[1];
          total := items[1:1];
          text := text[1:2];
          selected := QUERY(x <* items | (x >= 0) AND (x <= 10));
          selected := [1, 2:2];

          IF {0 <= total < 10} OR NOT flag THEN
            total := total + 1;
          END_IF;

          IF total IN items THEN
            total := total + 1;
          END_IF;

          IF 'abc' LIKE 'a*' THEN
            total := total + 1;
          END_IF;

          IF candidate :=: candidate THEN
            total := total + 1;
          END_IF;

          IF candidate :<>: candidate THEN
            total := total + 1;
          END_IF;

          total := state.on;
          RETURN(total);
        END_FUNCTION;

        END_SCHEMA;
        """;

    private const string ReusedLocalNamesSchema = """
        SCHEMA reused_local_names;
        FUNCTION first_value : INTEGER;
          LOCAL
            items : LIST OF INTEGER := [1];
          END_LOCAL;
          RETURN(items[1]);
        END_FUNCTION;
        FUNCTION second_value : INTEGER;
          LOCAL
            items : LIST OF INTEGER := [1];
          END_LOCAL;
          RETURN(items[1]);
        END_FUNCTION;
        END_SCHEMA;
        """;

    /// <summary>
    /// Verifies every grammar expression shape becomes source-located typed immutable IR.
    /// </summary>
    [Test]
    public async Task Should_bind_every_expression_shape_to_typed_ir()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("expressions.exp", Schema),
        ]);
        await Assert.That(compilation.SyntaxDiagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, compilation.SyntaxDiagnostics.Select(item => item.Message)));
        await Assert.That(compilation.BindingDiagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, compilation.BindingDiagnostics.Select(item => item.Message)));
        var schema = compilation.Schemas.Single();
        var expressions = schema.Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(expressions.Select(expression => expression.Kind).Distinct())
                .IsEquivalentTo(Enum.GetValues<ExpressExpressionKind>());
            await Assert.That(expressions.All(expression => expression.Type.Kind != ExpressExpressionTypeKind.Unresolved))
                .IsTrue();
            await Assert.That(expressions.All(expression => expression.Span.Start.FilePath == "expressions.exp"))
                .IsTrue();
        }
    }

    /// <summary>
    /// Verifies type inference retains EXPRESS numeric promotion, LOGICAL, aggregate, and qualifier results.
    /// </summary>
    [Test]
    public async Task Should_infer_expression_result_types_without_clr_guessing()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("expressions.exp", Schema),
        ]);
        await Assert.That(compilation.SyntaxDiagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, compilation.SyntaxDiagnostics.Select(item => item.Message)));
        await Assert.That(compilation.BindingDiagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, compilation.BindingDiagnostics.Select(item => item.Message)));
        var roots = compilation.Schemas.Single().Expressions;

        using (Assert.Multiple())
        {
            await Assert.That(Find(roots, "1.25").Type.Kind).IsEqualTo(ExpressExpressionTypeKind.Real);
            await Assert.That(Find(roots, "UNKNOWNXORTRUE").Type.Kind).IsEqualTo(ExpressExpressionTypeKind.Logical);
            await Assert.That(Find(roots, "'a'||'b'").Type.Kind).IsEqualTo(ExpressExpressionTypeKind.String);
            await Assert.That(Find(roots, "candidate.numeric_value").Type.Kind)
                .IsEqualTo(ExpressExpressionTypeKind.Integer);
            await Assert.That(Find(roots, "items[1]").Type.Kind).IsEqualTo(ExpressExpressionTypeKind.Integer);
            await Assert.That(Find(roots, "items[1:1]").Type.Kind).IsEqualTo(ExpressExpressionTypeKind.Integer);
            await Assert.That(Find(roots, "text[1:2]").Type.Kind).IsEqualTo(ExpressExpressionTypeKind.String);
            await Assert.That(Find(roots, "QUERY(x<*items|(x>=0)AND(x<=10))").Type.Kind)
                .IsEqualTo(ExpressExpressionTypeKind.Aggregate);
            await Assert.That(Find(roots, "{0<=total<10}").Type.Kind)
                .IsEqualTo(ExpressExpressionTypeKind.Logical);
            await Assert.That(Find(roots, "?").Type.Kind).IsEqualTo(ExpressExpressionTypeKind.Indeterminate);
        }
    }

    /// <summary>
    /// Verifies contextual aggregate typing uses declaration identity when algorithms reuse a local name.
    /// </summary>
    [Test]
    public async Task Should_scope_contextual_types_to_the_declaring_local()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("reused-local-names.exp", ReusedLocalNamesSchema),
        ]);
        var initializers = compilation.Schemas.Single().Expressions
            .Where(expression => expression.SourceText == "[1]")
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics).IsEmpty();
            await Assert.That(initializers.Count()).IsEqualTo(2);
            await Assert.That(initializers.All(expression =>
                expression.Type.DeclaredType is ExpressBoundAggregateType)).IsTrue();
        }
    }

    private static ExpressBoundExpression Find(
        IEnumerable<ExpressBoundExpression> roots,
        string sourceText)
    {
        return roots
            .SelectMany(root => root.DescendantsAndSelf())
            .Single(expression => expression.SourceText == sourceText);
    }
}
