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

    private const string StaticallyBoundIndexSchema = """
        SCHEMA statically_bound_index;
        FUNCTION inspect(
          low, high : INTEGER;
          fixed_array : ARRAY [2:4] OF INTEGER;
          dynamic_array : ARRAY [low:high] OF INTEGER;
          bounded_list : LIST [2:4] OF INTEGER;
          open_list : LIST [0:?] OF INTEGER;
          index : INTEGER) : INTEGER;
          RETURN(fixed_array[2] + fixed_array[4] + fixed_array[1]
            + dynamic_array[2]
            + bounded_list[1] + bounded_list[2] + bounded_list[3]
            + open_list[1] + fixed_array[index]);
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string RedeclaredAttributeSchema = """
        SCHEMA redeclared_attribute_binding;
        ENTITY representation_item;
        END_ENTITY;
        ENTITY curve SUBTYPE OF (representation_item);
          dimension : INTEGER;
        END_ENTITY;
        ENTITY styled_item;
          item : representation_item;
        END_ENTITY;
        ENTITY curve_style SUBTYPE OF (styled_item);
          SELF\styled_item.item : curve;
        END_ENTITY;
        ENTITY holder;
          style : curve_style;
        WHERE
          valid_dimension : style.item.dimension > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ContextualNvlComplexConstructionSchema = """
        SCHEMA contextual_nvl_complex_construction;
        CONSTANT
          dummy_gri : geometric_representation_item :=
            representation_item('') || geometric_representation_item();
        END_CONSTANT;
        ENTITY representation_item;
          name : STRING;
        END_ENTITY;
        ENTITY geometric_representation_item
          SUPERTYPE OF (ONEOF (direction))
          SUBTYPE OF (representation_item);
        END_ENTITY;
        ENTITY direction SUBTYPE OF (geometric_representation_item);
          direction_ratios : LIST [2:3] OF REAL;
        END_ENTITY;
        FUNCTION choose_direction(axis : direction) : direction;
          LOCAL
            selected : direction;
          END_LOCAL;
          selected := NVL(axis, dummy_gri || direction([1.0, 0.0]));
          RETURN(selected);
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
    /// Verifies an assignment target supplies the most-specific entity type through NVL to a complex constructor.
    /// </summary>
    [Test]
    public async Task Should_contextually_type_a_complex_constructor_inside_nvl()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("contextual-nvl-complex.exp", ContextualNvlComplexConstructionSchema),
        ]);
        await Assert.That(compilation.SyntaxDiagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, compilation.SyntaxDiagnostics.Select(item => item.Message)));
        await Assert.That(compilation.BindingDiagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, compilation.BindingDiagnostics.Select(item => item.Message)));
        var schema = compilation.Schemas.Single();
        var nvl = schema.Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Single(expression => string.Equals(expression.Operation, "NVL", StringComparison.OrdinalIgnoreCase));
        var construction = nvl.Children[1];
        var expected = schema.Declarations.Single(declaration => declaration.Name == "direction").Symbol;

        using (Assert.Multiple())
        {
            await Assert.That(((ExpressBoundNamedType)nvl.Type.DeclaredType!).Declaration)
                .IsSameReferenceAs(expected);
            await Assert.That(((ExpressBoundNamedType)construction.Type.DeclaredType!).Declaration)
                .IsSameReferenceAs(expected);
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
            await Assert.That(roots.SelectMany(root => root.DescendantsAndSelf())
                .Any(expression => expression.SourceText == "?"
                    && expression.Type.Kind == ExpressExpressionTypeKind.Indeterminate)).IsTrue();
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

    /// <summary>
    /// Verifies only constant indices guaranteed by declared aggregate bounds are determinate.
    /// </summary>
    [Test]
    public async Task Should_distinguish_statically_safe_indices_from_potentially_missing_elements()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("statically-bound-index.exp", StaticallyBoundIndexSchema),
        ]);
        await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
        await Assert.That(compilation.BindingDiagnostics).IsEmpty();
        var roots = compilation.Schemas.Single().Expressions;

        using (Assert.Multiple())
        {
            await Assert.That(Find(roots, "fixed_array[2]").Type.CanBeIndeterminate).IsFalse();
            await Assert.That(Find(roots, "fixed_array[4]").Type.CanBeIndeterminate).IsFalse();
            await Assert.That(Find(roots, "bounded_list[1]").Type.CanBeIndeterminate).IsFalse();
            await Assert.That(Find(roots, "bounded_list[2]").Type.CanBeIndeterminate).IsFalse();
            await Assert.That(Find(roots, "fixed_array[1]").Type.CanBeIndeterminate).IsTrue();
            await Assert.That(Find(roots, "bounded_list[3]").Type.CanBeIndeterminate).IsTrue();
            await Assert.That(Find(roots, "open_list[1]").Type.CanBeIndeterminate).IsTrue();
            await Assert.That(Find(roots, "dynamic_array[2]").Type.CanBeIndeterminate).IsTrue();
            await Assert.That(Find(roots, "fixed_array[index]").Type.CanBeIndeterminate).IsTrue();
        }
    }

    /// <summary>
    /// Verifies an explicit redeclaration shadows the inherited physical slot during member binding.
    /// </summary>
    [Test]
    public async Task Should_bind_the_nearest_explicit_attribute_redeclaration()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("redeclared-attribute.exp", RedeclaredAttributeSchema),
        ]);
        await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
        await Assert.That(compilation.BindingDiagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, compilation.BindingDiagnostics.Select(item => item.Message)));
        var dimension = compilation.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Single(expression => expression.SourceText == "style.item.dimension");
        await Assert.That(dimension.Type.Kind).IsEqualTo(ExpressExpressionTypeKind.Integer);
    }

    /// <summary>
    /// Verifies contextual ARRAY initializers retain indeterminacy from their bounds and children.
    /// </summary>
    [Test]
    public async Task Should_propagate_array_initializer_indeterminacy()
    {
        const string source = """
            SCHEMA array_initializer_indeterminacy;
            FUNCTION evaluate(values : LIST [0:?] OF INTEGER;
                              low, high : INTEGER) : LOGICAL;
              LOCAL
                dynamic_result : ARRAY [low:high] OF INTEGER;
                unsafe_result : ARRAY [1:1] OF INTEGER;
                fixed_result : ARRAY [1:1] OF INTEGER;
              END_LOCAL;
              dynamic_result := [1];
              unsafe_result := [values[1]];
              fixed_result := [1];
              RETURN(TRUE);
            END_FUNCTION;
            END_SCHEMA;
            """;
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("array-initializer-indeterminacy.exp", source),
        ]);
        var initializers = compilation.Schemas.Single().Expressions
            .Where(expression => expression.Kind == ExpressExpressionKind.AggregateInitializer)
            .OrderBy(expression => expression.Span.Start.Line)
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics).IsEmpty();
            await Assert.That(initializers).Count().IsEqualTo(3);
            await Assert.That(initializers[0].Type.CanBeIndeterminate).IsTrue();
            await Assert.That(initializers[1].Type.CanBeIndeterminate).IsTrue();
            await Assert.That(initializers[2].Type.CanBeIndeterminate).IsFalse();
        }
    }

    /// <summary>
    /// Verifies only an exact statically folded USEDIN role binds a concrete owner element type.
    /// </summary>
    [Test]
    public async Task Should_bind_only_exact_static_usedin_roles()
    {
        const string source = """
            SCHEMA usedin_role_binding;
            FUNCTION valid_role(candidate : target) : INTEGER;
              RETURN(SIZEOF(USEDIN(candidate, 'USEDIN_ROLE_BINDING.' + 'OWNER.ITEM')));
            END_FUNCTION;
            FUNCTION unknown_role(candidate : target) : INTEGER;
              RETURN(SIZEOF(USEDIN(candidate, 'USEDIN_ROLE_BINDING.OWNER.MISSING')));
            END_FUNCTION;
            FUNCTION dynamic_role(candidate : target; role_name : STRING) : INTEGER;
              RETURN(SIZEOF(USEDIN(candidate, role_name)));
            END_FUNCTION;
            ENTITY target; END_ENTITY;
            ENTITY owner;
              item : target;
            END_ENTITY;
            END_SCHEMA;
            """;
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("usedin-role-binding.exp", source),
        ]);
        var usedIn = compilation.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => string.Equals(expression.Operation, "USEDIN", StringComparison.OrdinalIgnoreCase))
            .OrderBy(expression => expression.Span.Start.Line)
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics).IsEmpty();
            await Assert.That(usedIn).Count().IsEqualTo(3);
            await Assert.That(((ExpressBoundAggregateType)usedIn[0].Type.DeclaredType!).ElementType)
                .IsTypeOf<ExpressBoundNamedType>();
            await Assert.That(((ExpressBoundAggregateType)usedIn[1].Type.DeclaredType!).ElementType)
                .IsTypeOf<ExpressBoundGenericType>();
            await Assert.That(((ExpressBoundAggregateType)usedIn[2].Type.DeclaredType!).ElementType)
                .IsTypeOf<ExpressBoundGenericType>();
        }
    }

    /// <summary>
    /// Verifies unlabeled GENERIC_ENTITY is the only generic actual closed to the runtime Entity representation.
    /// </summary>
    [Test]
    public async Task Should_close_only_an_unlabeled_generic_entity_actual()
    {
        const string source = """
            SCHEMA generic_entity_closure;
            FUNCTION bag_to_set(values : BAG OF GENERIC:t) : SET OF GENERIC:t;
              LOCAL result : SET OF GENERIC:t := []; END_LOCAL;
              IF SIZEOF(values) = 0 THEN RETURN(result); END_IF;
              RETURN(result);
            END_FUNCTION;
            FUNCTION entity_actual(candidate : target) : INTEGER;
              RETURN(SIZEOF(bag_to_set(USEDIN(candidate, ''))));
            END_FUNCTION;
            FUNCTION unlabeled_value_actual(values : BAG OF GENERIC) : INTEGER;
              RETURN(SIZEOF(bag_to_set(values)));
            END_FUNCTION;
            FUNCTION conflicting_label_actual(values : BAG OF GENERIC:u) : INTEGER;
              RETURN(SIZEOF(bag_to_set(values)));
            END_FUNCTION;
            FUNCTION explicit_entity(values : BAG OF GENERIC_ENTITY) : SET OF GENERIC_ENTITY;
              LOCAL result : SET OF GENERIC_ENTITY := []; END_LOCAL;
              IF SIZEOF(values) = 0 THEN RETURN(result); END_IF;
              RETURN(result);
            END_FUNCTION;
            FUNCTION explicit_entity_control(candidate : target) : SET OF target;
              LOCAL result : SET OF target := explicit_entity(USEDIN(candidate, '')); END_LOCAL;
              RETURN(result);
            END_FUNCTION;
            ENTITY target; END_ENTITY;
            END_SCHEMA;
            """;
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("generic-entity-closure.exp", source),
        ]);
        var schema = compilation.Schemas.Single();
        var calls = schema.Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => string.Equals(expression.Operation, "bag_to_set", StringComparison.OrdinalIgnoreCase))
            .OrderBy(expression => expression.Span.Start.Line)
            .ToArray();
        var elements = calls
            .Select(call => (ExpressBoundGenericType)((ExpressBoundAggregateType)call.Type.DeclaredType!).ElementType)
            .ToArray();
        var entityActual = (ExpressBoundGenericType)((ExpressBoundAggregateType)calls[0].Children[0].Type.DeclaredType!).ElementType;
        var explicitEntityCall = schema.Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Single(expression => string.Equals(
                expression.Operation,
                "explicit_entity",
                StringComparison.OrdinalIgnoreCase));
        var explicitEntityResult = (ExpressBoundGenericType)((ExpressBoundAggregateType)explicitEntityCall.Type.DeclaredType!).ElementType;

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics).IsEmpty();
            await Assert.That(calls).Count().IsEqualTo(3);
            await Assert.That(entityActual.IsEntity).IsTrue();
            await Assert.That(entityActual.TypeLabel).IsNull();
            await Assert.That(elements[0].IsEntity).IsTrue();
            await Assert.That(elements[0].TypeLabel).IsNull();
            await Assert.That(elements[1].IsEntity).IsFalse();
            await Assert.That(elements[1].TypeLabel).IsEqualTo("t");
            await Assert.That(elements[2].TypeLabel).IsEqualTo("t");
            await Assert.That(explicitEntityResult.IsEntity).IsTrue();
            await Assert.That(explicitEntityResult.TypeLabel).IsNull();
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