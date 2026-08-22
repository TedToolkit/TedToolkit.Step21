// -----------------------------------------------------------------------
// <copyright file="GenerationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Runtime.Loader;
using System.Numerics;

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Analyzer.Express.Binding;
using TedToolkit.Step21.Analyzer.Generation;
using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressExpressionTests;

/// <summary>
/// Proves bound EXPRESS expressions execute as ordinary statically compiled C#.
/// </summary>
internal sealed class GenerationTests
{
    private const string Schema = """
        SCHEMA expression_execution;
        CONSTANT
          arithmetic_value : INTEGER := (-7 + 3 * 4) DIV 2 MOD 3;
          logical_value : LOGICAL := (UNKNOWN AND FALSE) OR (TRUE XOR FALSE);
          interval_value : LOGICAL := {0 <= 5 < 10};
          text_value : STRING := 'static' || '-code';
          real_value : REAL := 1.25;
          real_arithmetic : REAL := 1.25 + 2.5 * 2.0;
          real_division : REAL := 5.0 / 2.0;
          real_square_root : REAL := SQRT(4.0);
          negative_division : INTEGER := -5 DIV 2;
          positive_division : INTEGER := 5 DIV -2;
          positive_modulo : INTEGER := -5 MOD 2;
          negative_modulo : INTEGER := 5 MOD -2;
          encoded_text : STRING := "000000410001F642";
        END_CONSTANT;
        END_SCHEMA;
        """;

    private const string ContextSchema = """
        SCHEMA expression_context;
        ENTITY item;
          numeric_value : INTEGER;
        END_ENTITY;

        FUNCTION exercise(items : LIST [0:?] OF INTEGER; candidate : item) : INTEGER;
          LOCAL
            selected : LIST [0:?] OF INTEGER := [];
          END_LOCAL;
          selected := [1, 2:2];
          selected := QUERY(x <* items | ODD(x));
          selected := [1];
          RETURN(candidate.numeric_value + candidate\item.numeric_value + items[1] + SIZEOF(items));
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string IndexAndBoundsSchema = """
        SCHEMA index_and_bounds;
        CONSTANT
          source_bits : BINARY := %101;
          source_text : STRING := 'abc';
          joined_bits : BINARY := %10 + %01;
          indexed_bits : BINARY := source_bits[2];
          sliced_bits : BINARY := source_bits[1:2];
          indexed_text : STRING := source_text[2];
          sliced_text : STRING := source_text[1:2];
        END_CONSTANT;

        FUNCTION inspect_bounds(
          bounded : LIST [2:5] OF INTEGER;
          unbounded : BAG OF INTEGER) : INTEGER;
          RETURN(HIBOUND(bounded) + HIINDEX(bounded) + LOBOUND(bounded)
            + LOINDEX(bounded) + NVL(HIBOUND(unbounded), 0));
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string NumericSchema = """
        SCHEMA numeric_expression;
        CONSTANT
          parsed_integer : NUMBER := VALUE('20');
          parsed_real : NUMBER := VALUE('1.25');
          parsed_invalid : NUMBER := VALUE('abc');
          integer_root : REAL := SQRT(4);
          formatted_integer : STRING := FORMAT(10, '+7I');
          formatted_picture : STRING := FORMAT(7123.456, '###,###.##');
          formatted_invalid : STRING := FORMAT(10, 'BAD');
        END_CONSTANT;

        FUNCTION numeric(number_value : NUMBER; real_value : REAL) : NUMBER;
          RETURN(number_value + 2 + real_value + number_value / 2);
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string AggregateOperationsSchema = """
        SCHEMA aggregate_operations;
        FUNCTION aggregate_operations(
          left_bag : BAG OF INTEGER;
          right_bag : BAG OF INTEGER;
          left_set : SET OF INTEGER;
          right_set : SET OF INTEGER;
          left_list : LIST OF INTEGER;
          right_list : LIST OF INTEGER) : LOGICAL;
          LOCAL
            bag_value : BAG OF INTEGER := [];
            set_value : SET OF INTEGER := [];
            list_value : LIST OF INTEGER := [];
          END_LOCAL;
          bag_value := left_bag * right_bag;
          bag_value := left_bag - right_bag;
          set_value := left_set + right_set;
          list_value := left_list + right_list;
          list_value := 0 + left_list;
          list_value := left_list + 3;
          RETURN((left_bag <= right_bag) AND (right_bag >= left_bag)
            AND (left_bag = right_bag) AND (left_list <> right_list));
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string LikeSchema = """
        SCHEMA like_expression;
        FUNCTION matches(input : STRING; pattern : STRING) : LOGICAL;
          RETURN(input LIKE pattern);
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string IndeterminateSchema = """
        SCHEMA indeterminate_expression;
        ENTITY optional_item;
          optional_number : OPTIONAL INTEGER;
        END_ENTITY;

        FUNCTION exercise(
          candidate : optional_item;
          items : LIST OF INTEGER;
          index : INTEGER;
          text : STRING;
          low : INTEGER;
          high : INTEGER) : LOGICAL;
          LOCAL
            current_number : INTEGER := 0;
            output_text : STRING := '';
          END_LOCAL;
          current_number := candidate.optional_number + 1;
          current_number := NVL(candidate.optional_number, 2);
          current_number := items[index];
          output_text := text[low:high];
          RETURN(EXISTS(candidate.optional_number) AND (candidate.optional_number = 1)
            AND (? AND FALSE) AND (VALUE('bad') = 1));
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string NumericDomainSchema = """
        SCHEMA numeric_domain;
        FUNCTION exercise(input_number : REAL; divisor : INTEGER; exponent : INTEGER) : REAL;
          RETURN(SQRT(input_number) + LOG(input_number) + ACOS(input_number) + (1.0 / divisor)
            + (2 ** exponent));
        END_FUNCTION;
        FUNCTION divide_real(left_number : REAL; right_number : REAL) : REAL;
          RETURN(left_number / right_number);
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string ModelContextSchema = """
        SCHEMA model_context_expression;
        ENTITY item;
        WHERE
          self_identity : SELF :=: SELF;
        END_ENTITY;

        FUNCTION inspect(candidate : item) : LOGICAL;
          RETURN((SIZEOF(TYPEOF(candidate)) > 0)
            AND (SIZEOF(ROLESOF(candidate)) > 0)
            AND (SIZEOF(USEDIN(candidate, 'MODEL_CONTEXT_EXPRESSION.ITEM')) > 0));
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string AggregateIndeterminateSchema = """
        SCHEMA aggregate_indeterminate;
        FUNCTION inspect(items : ARRAY [1:3] OF OPTIONAL INTEGER; candidate : INTEGER) : LOGICAL;
          RETURN(VALUE_IN(items, candidate) AND VALUE_UNIQUE(items) AND (candidate IN items));
        END_FUNCTION;
        FUNCTION compare(
          left_items : ARRAY [1:3] OF OPTIONAL INTEGER;
          right_items : ARRAY [1:3] OF OPTIONAL INTEGER) : LOGICAL;
          RETURN((left_items = right_items) AND (left_items <> right_items));
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string ContextualAggregateSchema = """
        SCHEMA contextual_aggregate;
        FUNCTION inspect : INTEGER;
          LOCAL
            bag_value : BAG OF INTEGER := [1, 1, 2];
            set_value : SET OF INTEGER := [];
            list_value : LIST OF INTEGER := [3, 4];
            array_value : ARRAY [2:3] OF INTEGER := [5, 6];
          END_LOCAL;
          RETURN(SIZEOF(bag_value) + SIZEOF(set_value) + SIZEOF(list_value) + array_value[2]);
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string NominalExpressionSchema = """
        SCHEMA nominal_expression;
        TYPE measure = INTEGER;
        END_TYPE;
        TYPE state = ENUMERATION OF (off, on, unknown_state);
        END_TYPE;
        TYPE choice = SELECT (measure, state);
        END_TYPE;

        FUNCTION inspect(
          left_measure : measure;
          right_measure : measure;
          current_state : state;
          left_choice : choice;
          right_choice : choice) : LOGICAL;
          RETURN((left_measure + right_measure = 3)
            AND (current_state > state.off)
            AND (left_choice = right_choice));
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string EntityEqualitySchema = """
        SCHEMA entity_equality;
        ENTITY item;
          code : INTEGER;
        END_ENTITY;
        FUNCTION inspect(left_item : item; right_item : item) : LOGICAL;
          RETURN((left_item = right_item) AND (left_item :=: right_item));
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string QueryCategoriesSchema = """
        SCHEMA query_categories;
        FUNCTION inspect(
          array_items : ARRAY [2:4] OF OPTIONAL INTEGER;
          bag_items : BAG [0:5] OF INTEGER;
          list_items : LIST [0:5] OF INTEGER;
          set_items : SET [0:5] OF INTEGER) : LOGICAL;
          LOCAL
            selected_array : ARRAY [2:4] OF OPTIONAL INTEGER;
            selected_bag : BAG [0:5] OF INTEGER;
            selected_list : LIST [0:5] OF INTEGER;
            selected_set : SET [0:5] OF INTEGER;
          END_LOCAL;
          selected_array := QUERY(item <* array_items | item > 1);
          selected_bag := QUERY(item <* bag_items | item > 1);
          selected_list := QUERY(item <* list_items | item > 1);
          selected_set := QUERY(item <* set_items | item > 1);
          RETURN(TRUE);
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string BuiltInCoverageSchema = """
        SCHEMA builtin_coverage;
        CONSTANT
          absolute_integer : INTEGER := ABS(-3);
          arc_cosine : REAL := ACOS(1);
          arc_sine : REAL := ASIN(0);
          arc_tangent : REAL := ATAN(0);
          arc_tangent_pair : REAL := ATAN(0, 1);
          cosine : REAL := COS(0);
          exponential : REAL := EXP(0);
          natural_log : REAL := LOG(1);
          binary_log : REAL := LOG2(8);
          decimal_log : REAL := LOG10(100);
          sine : REAL := SIN(0);
          square_root : REAL := SQRT(9);
          tangent : REAL := TAN(0);
          binary_length : INTEGER := BLENGTH(%101);
          string_length : INTEGER := LENGTH('abc');
        END_CONSTANT;

        FUNCTION bounds(
          array_items : ARRAY [2:4] OF INTEGER;
          bag_items : BAG [1:5] OF INTEGER;
          list_items : LIST [2:5] OF INTEGER;
          set_items : SET [0:5] OF INTEGER) : INTEGER;
          RETURN(HIBOUND(array_items) + HIINDEX(array_items) + LOBOUND(array_items) + LOINDEX(array_items)
            + HIBOUND(bag_items) + HIINDEX(bag_items) + LOBOUND(bag_items) + LOINDEX(bag_items)
            + HIBOUND(list_items) + HIINDEX(list_items) + LOBOUND(list_items) + LOINDEX(list_items)
            + HIBOUND(set_items) + HIINDEX(set_items) + LOBOUND(set_items) + LOINDEX(set_items));
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string ApplicationSchema = """
        SCHEMA application_expression;
        ENTITY pair;
          left_value : INTEGER;
          right_value : INTEGER;
        END_ENTITY;
        ENTITY marker;
        END_ENTITY;
        FUNCTION add(left_value : INTEGER; right_value : INTEGER) : INTEGER;
          RETURN(left_value + right_value);
        END_FUNCTION;
        FUNCTION inspect(input_number : INTEGER) : INTEGER;
          LOCAL
            result_pair : pair;
          END_LOCAL;
          result_pair := pair(input_number, add(input_number, 1));
          RETURN(result_pair.left_value + result_pair.right_value);
        END_FUNCTION;
        FUNCTION create_marker : marker;
          RETURN(marker());
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string InvalidEncodedStringSchema = """
        SCHEMA invalid_encoded_string;
        CONSTANT
          invalid_text : STRING := "00110000";
        END_CONSTANT;
        END_SCHEMA;
        """;

    private const string RepetitionSchema = """
        SCHEMA repetition_expression;
        FUNCTION inspect(repetition_count : INTEGER) : INTEGER;
          LOCAL
            items : LIST OF INTEGER := [7 : repetition_count];
          END_LOCAL;
          RETURN(SIZEOF(items));
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string ArrayRepetitionSchema = """
        SCHEMA array_repetition_expression;
        FUNCTION build_array(repetition_count : INTEGER) : ARRAY [2:4] OF INTEGER;
          RETURN([7 : repetition_count]);
        END_FUNCTION;
        FUNCTION sparse : ARRAY [2:4] OF OPTIONAL INTEGER;
          RETURN([1, ?, 3]);
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string ComplexConstructionSchema = """
        SCHEMA complex_construction_expression;
        ENTITY first_part;
        END_ENTITY;
        ENTITY second_part;
        END_ENTITY;
        FUNCTION combine(left_part : first_part; right_part : second_part) : GENERIC_ENTITY;
          RETURN(left_part || right_part);
        END_FUNCTION;
        END_SCHEMA;
        """;

    /// <summary>
    /// Verifies generated scalar, LOGICAL, interval, and concatenation expressions execute without a runtime interpreter.
    /// </summary>
    [Test]
    public async Task Should_execute_static_scalar_and_logical_expressions()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("execution.exp", Schema),
        ]).Schemas.Single();
        var emitted = schema.Expressions.Select(expression => ExpressExpressionEmitter.Emit(expression)).ToArray();
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;

            internal sealed class ExpressionProbe : SchemaDescriptor
            {
                internal static BigInteger? Arithmetic() => {{emitted[0].Code}};
                internal static LogicalValue Logical() => {{emitted[1].Code}};
                internal static LogicalValue Interval() => {{emitted[2].Code}};
                internal static string Text() => {{emitted[3].Code}};
                internal static RealValue Real() => {{emitted[4].Code}};
                internal static RealValue RealArithmetic() => {{emitted[5].Code}};
                internal static RealValue? RealDivision() => {{emitted[6].Code}};
                internal static RealValue? RealSquareRoot() => {{emitted[7].Code}};
                internal static BigInteger? NegativeDivision() => {{emitted[8].Code}};
                internal static BigInteger? PositiveDivision() => {{emitted[9].Code}};
                internal static BigInteger? PositiveModulo() => {{emitted[10].Code}};
                internal static BigInteger? NegativeModulo() => {{emitted[11].Code}};
                internal static string EncodedText() => {{emitted[12].Code}};

                public override SchemaName Name => new("expression_execution");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("execution.exp", Schema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("ExpressionProbe", throwOnError: true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That((BigInteger)probe.GetMethod("Arithmetic", flags)!.Invoke(null, null)!)
                .IsEqualTo(new BigInteger(2));
            await Assert.That((LogicalValue)probe.GetMethod("Logical", flags)!.Invoke(null, null)!)
                .IsEqualTo(LogicalValue.True);
            await Assert.That((LogicalValue)probe.GetMethod("Interval", flags)!.Invoke(null, null)!)
                .IsEqualTo(LogicalValue.True);
            await Assert.That((string)probe.GetMethod("Text", flags)!.Invoke(null, null)!)
                .IsEqualTo("static-code");
            await Assert.That((RealValue)probe.GetMethod("Real", flags)!.Invoke(null, null)!)
                .IsEqualTo(new RealValue(125, -2));
            await Assert.That((RealValue)probe.GetMethod("RealArithmetic", flags)!.Invoke(null, null)!)
                .IsEqualTo(new RealValue(625, -2));
            await Assert.That((RealValue)probe.GetMethod("RealDivision", flags)!.Invoke(null, null)!)
                .IsEqualTo(new RealValue(25, -1));
            await Assert.That((RealValue)probe.GetMethod("RealSquareRoot", flags)!.Invoke(null, null)!)
                .IsEqualTo(new RealValue(2, 0));
            await Assert.That((BigInteger)probe.GetMethod("NegativeDivision", flags)!.Invoke(null, null)!)
                .IsEqualTo(new BigInteger(-3));
            await Assert.That((BigInteger)probe.GetMethod("PositiveDivision", flags)!.Invoke(null, null)!)
                .IsEqualTo(new BigInteger(-3));
            await Assert.That((BigInteger)probe.GetMethod("PositiveModulo", flags)!.Invoke(null, null)!)
                .IsEqualTo(BigInteger.One);
            await Assert.That((BigInteger)probe.GetMethod("NegativeModulo", flags)!.Invoke(null, null)!)
                .IsEqualTo(new BigInteger(-1));
            await Assert.That((string)probe.GetMethod("EncodedText", flags)!.Invoke(null, null)!)
                .IsEqualTo("A🙂");
            await Assert.That(string.Join("\n", emitted.Select(expression => expression.Code)))
                .DoesNotContain("dynamic")
                .And.DoesNotContain("Reflection")
                .And.DoesNotContain("Expression.Compile");
        }
    }

    /// <summary>
    /// Verifies references, navigation, aggregate construction, slicing, querying, and aggregate built-ins remain static.
    /// </summary>
    [Test]
    public async Task Should_execute_static_reference_aggregate_and_query_expressions()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("context.exp", ContextSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var aggregate = ExpressExpressionEmitter.Emit(Find(schema, "[1,2:2]"), Resolve);
        var query = ExpressExpressionEmitter.Emit(Find(schema, "QUERY(x<*items|ODD(x))"), Resolve);
        var navigation = ExpressExpressionEmitter.Emit(
            Find(schema, "candidate.numeric_value+candidate\\item.numeric_value+items[1]+SIZEOF(items)"),
            Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.ExpressionContext;

            internal sealed class ContextProbe : TedToolkit.Step21.SchemaDescriptor
            {
                internal static ExpressList<BigInteger> Aggregate() => {{aggregate.Code}};
                internal static ExpressList<BigInteger> Query(ExpressList<BigInteger> items) => {{query.Code}};
                internal static BigInteger? Navigate(Item candidate, ExpressList<BigInteger> items) => {{navigation.Code}};

                public override SchemaName Name => new("expression_context");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("context.exp", ContextSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("ContextProbe", throwOnError: true)!;
        var generatedItem = assembly.GetType(
            "TedToolkit.Step21.Generated.ExpressionContext.Item",
            throwOnError: true)!;
        var item = Activator.CreateInstance(generatedItem, new BigInteger(5))!;
        var items = new ExpressList<BigInteger> { 1, 2, 3, 4 };
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var aggregateValue = (ExpressList<BigInteger>)probe.GetMethod("Aggregate", flags)!.Invoke(null, null)!;
        var queryValue = (ExpressList<BigInteger>)probe.GetMethod("Query", flags)!.Invoke(null, [items])!;
        var navigationValue = (BigInteger)probe.GetMethod("Navigate", flags)!.Invoke(null, [item, items])!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(aggregateValue).IsEquivalentTo(new BigInteger[] { 1, 2, 2 });
            await Assert.That(queryValue).IsEquivalentTo(new BigInteger[] { 1, 3 });
            await Assert.That(navigationValue).IsEqualTo(new BigInteger(15));
        }
    }

    /// <summary>
    /// Verifies BINARY/STRING qualifiers preserve value types and bound functions distinguish declarations from values.
    /// </summary>
    [Test]
    public async Task Should_execute_binary_string_index_and_aggregate_bound_semantics()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("index-and-bounds.exp", IndexAndBoundsSchema),
        ]);
        if (compilation.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                compilation.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(compilation.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var schema = compilation.Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var joinedBits = ExpressExpressionEmitter.Emit(Find(schema, "%10+%01"), Resolve);
        var indexedBits = ExpressExpressionEmitter.Emit(Find(schema, "source_bits[2]"),
            _ => "new global::TedToolkit.Step21.BinaryValue(\"101\")");
        var slicedBits = ExpressExpressionEmitter.Emit(Find(schema, "source_bits[1:2]"),
            _ => "new global::TedToolkit.Step21.BinaryValue(\"101\")");
        var indexedText = ExpressExpressionEmitter.Emit(Find(schema, "source_text[2]"), _ => "\"abc\"");
        var slicedText = ExpressExpressionEmitter.Emit(Find(schema, "source_text[1:2]"), _ => "\"abc\"");
        var highBound = ExpressExpressionEmitter.Emit(Find(schema, "HIBOUND(bounded)"), Resolve);
        var highIndex = ExpressExpressionEmitter.Emit(Find(schema, "HIINDEX(bounded)"), Resolve);
        var lowBound = ExpressExpressionEmitter.Emit(Find(schema, "LOBOUND(bounded)"), Resolve);
        var lowIndex = ExpressExpressionEmitter.Emit(Find(schema, "LOINDEX(bounded)"), Resolve);
        var unboundedHigh = ExpressExpressionEmitter.Emit(Find(schema, "HIBOUND(unbounded)"), Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;

            internal sealed class IndexAndBoundsProbe : SchemaDescriptor
            {
                internal static BinaryValue JoinedBits() => {{joinedBits.Code}};
                internal static BinaryValue? IndexedBits() => {{indexedBits.Code}};
                internal static BinaryValue? SlicedBits() => {{slicedBits.Code}};
                internal static string? IndexedText() => {{indexedText.Code}};
                internal static string? SlicedText() => {{slicedText.Code}};
                internal static BigInteger HighBound(ExpressList<BigInteger> bounded) => {{highBound.Code}};
                internal static BigInteger HighIndex(ExpressList<BigInteger> bounded) => {{highIndex.Code}};
                internal static BigInteger LowBound(ExpressList<BigInteger> bounded) => {{lowBound.Code}};
                internal static BigInteger LowIndex(ExpressList<BigInteger> bounded) => {{lowIndex.Code}};
                internal static BigInteger? UnboundedHigh(ExpressBag<BigInteger> unbounded) => {{unboundedHigh.Code}};

                public override SchemaName Name => new("index_and_bounds");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("index-and-bounds.exp", IndexAndBoundsSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("IndexAndBoundsProbe", throwOnError: true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var bounded = new ExpressList<BigInteger>(2, 5) { 1, 2, 3 };
        var unbounded = new ExpressBag<BigInteger>();

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(probe.GetMethod("JoinedBits", flags)!.Invoke(null, null)!.ToString())
                .IsEqualTo("1001");
            await Assert.That(probe.GetMethod("IndexedBits", flags)!.Invoke(null, null)!.ToString())
                .IsEqualTo("0");
            await Assert.That(probe.GetMethod("SlicedBits", flags)!.Invoke(null, null)!.ToString())
                .IsEqualTo("10");
            await Assert.That(probe.GetMethod("IndexedText", flags)!.Invoke(null, null))
                .IsEqualTo("b");
            await Assert.That(probe.GetMethod("SlicedText", flags)!.Invoke(null, null))
                .IsEqualTo("ab");
            await Assert.That(probe.GetMethod("HighBound", flags)!.Invoke(null, [bounded]))
                .IsEqualTo(new BigInteger(5));
            await Assert.That(probe.GetMethod("HighIndex", flags)!.Invoke(null, [bounded]))
                .IsEqualTo(new BigInteger(3));
            await Assert.That(probe.GetMethod("LowBound", flags)!.Invoke(null, [bounded]))
                .IsEqualTo(new BigInteger(2));
            await Assert.That(probe.GetMethod("LowIndex", flags)!.Invoke(null, [bounded]))
                .IsEqualTo(BigInteger.One);
            await Assert.That(probe.GetMethod("UnboundedHigh", flags)!.Invoke(null, [unbounded]))
                .IsNull();
        }
    }

    /// <summary>
    /// Verifies NUMBER alternatives, mixed numeric promotion, numeric comparison, and VALUE remain strongly typed.
    /// </summary>
    [Test]
    public async Task Should_execute_number_promotion_and_value_semantics()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("numeric.exp", NumericSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var numberPlusInteger = ExpressExpressionEmitter.Emit(Find(schema, "number_value+2"), Resolve);
        var realPlusInteger = ExpressExpressionEmitter.Emit(Find(schema, "number_value+2+real_value"), Resolve);
        var numberDivision = ExpressExpressionEmitter.Emit(Find(schema, "number_value/2"), Resolve);
        var numericEquality = ExpressExpressionEmitter.Emit(Find(schema, "VALUE('20')"), Resolve);
        var parsedReal = ExpressExpressionEmitter.Emit(Find(schema, "VALUE('1.25')"), Resolve);
        var parsedInvalid = ExpressExpressionEmitter.Emit(Find(schema, "VALUE('abc')"), Resolve);
        var integerRoot = ExpressExpressionEmitter.Emit(Find(schema, "SQRT(4)"), Resolve);
        var formattedInteger = ExpressExpressionEmitter.Emit(Find(schema, "FORMAT(10,'+7I')"), Resolve);
        var formattedPicture = ExpressExpressionEmitter.Emit(
            Find(schema, "FORMAT(7123.456,'###,###.##')"),
            Resolve);
        var formattedInvalid = ExpressExpressionEmitter.Emit(Find(schema, "FORMAT(10,'BAD')"), Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;

            internal sealed class NumericProbe : SchemaDescriptor
            {
                internal static NumberValue AddInteger(NumberValue number_value) => {{numberPlusInteger.Code}};
                internal static RealValue AddReal(NumberValue number_value, RealValue real_value) => {{realPlusInteger.Code}};
                internal static RealValue? Divide(NumberValue number_value) => {{numberDivision.Code}};
                internal static NumberValue? ParseInteger() => {{numericEquality.Code}};
                internal static NumberValue? ParseReal() => {{parsedReal.Code}};
                internal static NumberValue? ParseInvalid() => {{parsedInvalid.Code}};
                internal static RealValue? IntegerRoot() => {{integerRoot.Code}};
                internal static string? FormattedInteger() => {{formattedInteger.Code}};
                internal static string? FormattedPicture() => {{formattedPicture.Code}};
                internal static string? FormattedInvalid() => {{formattedInvalid.Code}};

                public override SchemaName Name => new("numeric_expression");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("numeric.exp", NumericSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("NumericProbe", throwOnError: true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var two = NumberValue.FromInteger(2);
        var half = new RealValue(5, -1);

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(probe.GetMethod("AddInteger", flags)!.Invoke(null, [two]))
                .IsEqualTo(NumberValue.FromInteger(4));
            await Assert.That(probe.GetMethod("AddReal", flags)!.Invoke(null, [two, half]))
                .IsEqualTo(new RealValue(45, -1));
            await Assert.That(probe.GetMethod("Divide", flags)!.Invoke(null, [two]))
                .IsEqualTo(new RealValue(1, 0));
            await Assert.That(probe.GetMethod("ParseInteger", flags)!.Invoke(null, null))
                .IsEqualTo(NumberValue.FromInteger(20));
            await Assert.That(probe.GetMethod("ParseReal", flags)!.Invoke(null, null))
                .IsEqualTo(NumberValue.FromReal(new RealValue(125, -2)));
            await Assert.That(probe.GetMethod("ParseInvalid", flags)!.Invoke(null, null)).IsNull();
            await Assert.That(probe.GetMethod("IntegerRoot", flags)!.Invoke(null, null))
                .IsEqualTo(new RealValue(2, 0));
            await Assert.That(probe.GetMethod("FormattedInteger", flags)!.Invoke(null, null))
                .IsEqualTo("    +10");
            await Assert.That(probe.GetMethod("FormattedPicture", flags)!.Invoke(null, null))
                .IsEqualTo("  7,123.46");
            await Assert.That(probe.GetMethod("FormattedInvalid", flags)!.Invoke(null, null)).IsNull();
        }
    }

    /// <summary>
    /// Verifies aggregate operators preserve LIST order, BAG multiplicity, and SET uniqueness semantics.
    /// </summary>
    [Test]
    public async Task Should_execute_aggregate_union_intersection_difference_and_comparison()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("aggregate-operations.exp", AggregateOperationsSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var intersection = ExpressExpressionEmitter.Emit(Find(schema, "left_bag*right_bag"), Resolve);
        var difference = ExpressExpressionEmitter.Emit(Find(schema, "left_bag-right_bag"), Resolve);
        var setUnion = ExpressExpressionEmitter.Emit(Find(schema, "left_set+right_set"), Resolve);
        var listUnion = ExpressExpressionEmitter.Emit(Find(schema, "left_list+right_list"), Resolve);
        var prepend = ExpressExpressionEmitter.Emit(Find(schema, "0+left_list"), Resolve);
        var append = ExpressExpressionEmitter.Emit(Find(schema, "left_list+3"), Resolve);
        var subset = ExpressExpressionEmitter.Emit(Find(schema, "left_bag<=right_bag"), Resolve);
        var superset = ExpressExpressionEmitter.Emit(Find(schema, "right_bag>=left_bag"), Resolve);
        var bagEquality = ExpressExpressionEmitter.Emit(Find(schema, "left_bag=right_bag"), Resolve);
        var listInequality = ExpressExpressionEmitter.Emit(Find(schema, "left_list<>right_list"), Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;

            internal sealed class AggregateOperationsProbe : SchemaDescriptor
            {
                internal static ExpressBag<BigInteger> Intersection(
                    ExpressBag<BigInteger> left_bag, ExpressBag<BigInteger> right_bag) => {{intersection.Code}};
                internal static ExpressBag<BigInteger> Difference(
                    ExpressBag<BigInteger> left_bag, ExpressBag<BigInteger> right_bag) => {{difference.Code}};
                internal static ExpressSet<BigInteger> SetUnion(
                    ExpressSet<BigInteger> left_set, ExpressSet<BigInteger> right_set) => {{setUnion.Code}};
                internal static ExpressList<BigInteger> ListUnion(
                    ExpressList<BigInteger> left_list, ExpressList<BigInteger> right_list) => {{listUnion.Code}};
                internal static ExpressList<BigInteger> Prepend(ExpressList<BigInteger> left_list) => {{prepend.Code}};
                internal static ExpressList<BigInteger> Append(ExpressList<BigInteger> left_list) => {{append.Code}};
                internal static LogicalValue Subset(
                    ExpressBag<BigInteger> left_bag, ExpressBag<BigInteger> right_bag) => {{subset.Code}};
                internal static LogicalValue Superset(
                    ExpressBag<BigInteger> left_bag, ExpressBag<BigInteger> right_bag) => {{superset.Code}};
                internal static LogicalValue BagEquality(
                    ExpressBag<BigInteger> left_bag, ExpressBag<BigInteger> right_bag) => {{bagEquality.Code}};
                internal static LogicalValue ListInequality(
                    ExpressList<BigInteger> left_list, ExpressList<BigInteger> right_list) => {{listInequality.Code}};

                public override SchemaName Name => new("aggregate_operations");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("aggregate-operations.exp", AggregateOperationsSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("AggregateOperationsProbe", throwOnError: true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var leftBag = new ExpressBag<BigInteger> { 1, 1, 2, 3 };
        var rightBag = new ExpressBag<BigInteger> { 1, 2, 2 };
        var subsetBag = new ExpressBag<BigInteger> { 1, 1 };
        var supersetBag = new ExpressBag<BigInteger> { 2, 1, 1 };
        var leftSet = new ExpressSet<BigInteger> { 1, 2 };
        var rightSet = new ExpressSet<BigInteger> { 2, 3 };
        var leftList = new ExpressList<BigInteger> { 1, 2 };
        var rightList = new ExpressList<BigInteger> { 3, 4 };
        var reverseList = new ExpressList<BigInteger> { 2, 1 };

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That((ExpressBag<BigInteger>)probe.GetMethod("Intersection", flags)!
                    .Invoke(null, [leftBag, rightBag])!)
                .IsEquivalentTo(new BigInteger[] { 1, 2 });
            await Assert.That((ExpressBag<BigInteger>)probe.GetMethod("Difference", flags)!
                    .Invoke(null, [leftBag, rightBag])!)
                .IsEquivalentTo(new BigInteger[] { 1, 3 });
            await Assert.That((ExpressSet<BigInteger>)probe.GetMethod("SetUnion", flags)!
                    .Invoke(null, [leftSet, rightSet])!)
                .IsEquivalentTo(new BigInteger[] { 1, 2, 3 });
            await Assert.That((ExpressList<BigInteger>)probe.GetMethod("ListUnion", flags)!
                    .Invoke(null, [leftList, rightList])!)
                .IsEquivalentTo(new BigInteger[] { 1, 2, 3, 4 });
            await Assert.That((ExpressList<BigInteger>)probe.GetMethod("Prepend", flags)!
                    .Invoke(null, [leftList])!)
                .IsEquivalentTo(new BigInteger[] { 0, 1, 2 });
            await Assert.That((ExpressList<BigInteger>)probe.GetMethod("Append", flags)!
                    .Invoke(null, [leftList])!)
                .IsEquivalentTo(new BigInteger[] { 1, 2, 3 });
            await Assert.That(probe.GetMethod("Subset", flags)!.Invoke(null, [subsetBag, supersetBag]))
                .IsEqualTo(LogicalValue.True);
            await Assert.That(probe.GetMethod("Superset", flags)!.Invoke(null, [subsetBag, supersetBag]))
                .IsEqualTo(LogicalValue.True);
            await Assert.That(probe.GetMethod("BagEquality", flags)!.Invoke(null, [subsetBag, supersetBag]))
                .IsEqualTo(LogicalValue.False);
            await Assert.That(probe.GetMethod("ListInequality", flags)!.Invoke(null, [leftList, reverseList]))
                .IsEqualTo(LogicalValue.True);
        }
    }

    /// <summary>
    /// Verifies the complete EXPRESS LIKE token vocabulary with runtime-supplied patterns.
    /// </summary>
    [Test]
    public async Task Should_execute_like_patterns()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("like.exp", LikeSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var like = ExpressExpressionEmitter.Emit(Find(schema, "inputLIKEpattern"), Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using TedToolkit.Step21;

            internal sealed class LikeProbe : SchemaDescriptor
            {
                internal static LogicalValue Matches(string input, string pattern) => {{like.Code}};

                public override SchemaName Name => new("like_expression");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("like.exp", LikeSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("LikeProbe", throwOnError: true)!;
        var method = probe.GetMethod("Matches", BindingFlags.Static | BindingFlags.NonPublic)!;
        var cases = new (string Input, string Pattern, LogicalValue Expected)[]
        {
            ("The quick red fox", "$$$$", LogicalValue.True),
            ("Page 407", "$*", LogicalValue.True),
            ("A7!", "^#?", LogicalValue.True),
            ("Alpha", "@@@@@", LogicalValue.True),
            ("ALPHA", "^^^^^", LogicalValue.True),
            ("Alpha", "^^^^^", LogicalValue.False),
            ("abc", "a&", LogicalValue.True),
            ("7", "!@", LogicalValue.True),
            ("abc", "a!dc", LogicalValue.True),
            ("abc", "a!bc", LogicalValue.False),
            ("@", "\\@", LogicalValue.True),
        };

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            foreach (var testCase in cases)
            {
                var actual = (LogicalValue)method.Invoke(null, [testCase.Input, testCase.Pattern])!;
                await Assert.That(actual).IsEqualTo(testCase.Expected);
            }
        }
    }

    /// <summary>
    /// Verifies indeterminate values lift arithmetic/results, become UNKNOWN in logic, and never throw on indexing.
    /// </summary>
    [Test]
    public async Task Should_propagate_indeterminate_values_without_reinterpreting_clr_defaults()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("indeterminate.exp", IndeterminateSchema),
        ]);
        if (compilation.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                compilation.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(compilation.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var schema = compilation.Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var add = ExpressExpressionEmitter.Emit(Find(schema, "candidate.optional_number+1"), Resolve);
        var equality = ExpressExpressionEmitter.Emit(Find(schema, "candidate.optional_number=1"), Resolve);
        var exists = ExpressExpressionEmitter.Emit(Find(schema, "EXISTS(candidate.optional_number)"), Resolve);
        var nvl = ExpressExpressionEmitter.Emit(Find(schema, "NVL(candidate.optional_number,2)"), Resolve);
        var index = ExpressExpressionEmitter.Emit(Find(schema, "items[index]"), Resolve);
        var slice = ExpressExpressionEmitter.Emit(Find(schema, "text[low:high]"), Resolve);
        var falseAndUnknown = ExpressExpressionEmitter.Emit(Find(schema, "?ANDFALSE"), Resolve);
        var invalidValueComparison = ExpressExpressionEmitter.Emit(Find(schema, "VALUE('bad')=1"), Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.IndeterminateExpression;

            internal sealed class IndeterminateProbe : TedToolkit.Step21.SchemaDescriptor
            {
                internal static BigInteger? Add(OptionalItem candidate) => {{add.Code}};
                internal static LogicalValue Equal(OptionalItem candidate) => {{equality.Code}};
                internal static bool Exists(OptionalItem candidate) => {{exists.Code}};
                internal static BigInteger Nvl(OptionalItem candidate) => {{nvl.Code}};
                internal static BigInteger? Index(ExpressList<BigInteger> items, BigInteger index) => {{index.Code}};
                internal static string? Slice(string text, BigInteger low, BigInteger high) => {{slice.Code}};
                internal static LogicalValue FalseAndUnknown() => {{falseAndUnknown.Code}};
                internal static LogicalValue InvalidValueComparison() => {{invalidValueComparison.Code}};

                public override SchemaName Name => new("indeterminate_expression");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("indeterminate.exp", IndeterminateSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("IndeterminateProbe", throwOnError: true)!;
        var itemType = assembly.GetType(
            "TedToolkit.Step21.Generated.IndeterminateExpression.OptionalItem",
            throwOnError: true)!;
        var missing = Activator.CreateInstance(itemType)!;
        var present = Activator.CreateInstance(itemType)!;
        itemType.GetProperty("OptionalNumber")!.SetValue(present, (BigInteger?)new BigInteger(4));
        var items = new ExpressList<BigInteger> { 10, 20 };
        var flags = BindingFlags.Static | BindingFlags.NonPublic;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(probe.GetMethod("Add", flags)!.Invoke(null, [missing])).IsNull();
            await Assert.That(probe.GetMethod("Add", flags)!.Invoke(null, [present]))
                .IsEqualTo(new BigInteger(5));
            await Assert.That(probe.GetMethod("Equal", flags)!.Invoke(null, [missing]))
                .IsEqualTo(LogicalValue.Unknown);
            await Assert.That((bool)probe.GetMethod("Exists", flags)!.Invoke(null, [missing])!).IsFalse();
            await Assert.That((bool)probe.GetMethod("Exists", flags)!.Invoke(null, [present])!).IsTrue();
            await Assert.That(probe.GetMethod("Nvl", flags)!.Invoke(null, [missing]))
                .IsEqualTo(new BigInteger(2));
            await Assert.That(probe.GetMethod("Index", flags)!.Invoke(null, [items, new BigInteger(3)]))
                .IsNull();
            await Assert.That(probe.GetMethod("Index", flags)!.Invoke(null, [items, BigInteger.One]))
                .IsEqualTo(new BigInteger(10));
            await Assert.That(probe.GetMethod("Slice", flags)!.Invoke(
                null,
                ["abcd", new BigInteger(2), new BigInteger(3)]))
                .IsEqualTo("bc");
            await Assert.That(probe.GetMethod("Slice", flags)!.Invoke(
                null,
                ["abcd", BigInteger.Zero, new BigInteger(3)]))
                .IsNull();
            await Assert.That(probe.GetMethod("FalseAndUnknown", flags)!.Invoke(null, null))
                .IsEqualTo(LogicalValue.False);
            await Assert.That(probe.GetMethod("InvalidValueComparison", flags)!.Invoke(null, null))
                .IsEqualTo(LogicalValue.Unknown);
        }
    }

    /// <summary>
    /// Verifies arithmetic domain failures produce the EXPRESS indeterminate value instead of CLR exceptions.
    /// </summary>
    [Test]
    public async Task Should_return_indeterminate_for_numeric_domain_errors()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("numeric-domain.exp", NumericDomainSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var squareRoot = ExpressExpressionEmitter.Emit(Find(schema, "SQRT(input_number)"), Resolve);
        var logarithm = ExpressExpressionEmitter.Emit(Find(schema, "LOG(input_number)"), Resolve);
        var arcCosine = ExpressExpressionEmitter.Emit(Find(schema, "ACOS(input_number)"), Resolve);
        var division = ExpressExpressionEmitter.Emit(Find(schema, "1.0/divisor"), Resolve);
        var realDivision = ExpressExpressionEmitter.Emit(Find(schema, "left_number/right_number"), Resolve);
        var power = ExpressExpressionEmitter.Emit(Find(schema, "2**exponent"), Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;

            internal sealed class NumericDomainProbe : SchemaDescriptor
            {
                internal static RealValue? SquareRoot(RealValue input_number) => {{squareRoot.Code}};
                internal static RealValue? Logarithm(RealValue input_number) => {{logarithm.Code}};
                internal static RealValue? ArcCosine(RealValue input_number) => {{arcCosine.Code}};
                internal static RealValue? Divide(BigInteger divisor) => {{division.Code}};
                internal static RealValue? DivideReal(
                    RealValue left_number,
                    RealValue right_number) => {{realDivision.Code}};
                internal static BigInteger? Power(BigInteger exponent) => {{power.Code}};

                public override SchemaName Name => new("numeric_domain");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("numeric-domain.exp", NumericDomainSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("NumericDomainProbe", throwOnError: true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var negative = new RealValue(-1, 0);
        var zero = new RealValue(0, 0);
        var outsideUnitInterval = new RealValue(2, 0);

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(probe.GetMethod("SquareRoot", flags)!.Invoke(null, [negative])).IsNull();
            await Assert.That(probe.GetMethod("Logarithm", flags)!.Invoke(null, [zero])).IsNull();
            await Assert.That(probe.GetMethod("ArcCosine", flags)!.Invoke(null, [outsideUnitInterval])).IsNull();
            await Assert.That(probe.GetMethod("Divide", flags)!.Invoke(null, [BigInteger.Zero])).IsNull();
            await Assert.That(probe.GetMethod("DivideReal", flags)!.Invoke(
                null,
                [new RealValue(1, 10000), new RealValue(1, -10000)])).IsNull();
            await Assert.That(probe.GetMethod("Power", flags)!.Invoke(null, [new BigInteger(-1)])).IsNull();
            await Assert.That(probe.GetMethod("Power", flags)!.Invoke(null, [new BigInteger(3)]))
                .IsEqualTo(new BigInteger(8));
        }
    }

    /// <summary>
    /// Verifies model-context expressions compile to caller-supplied static operations with source-located failures otherwise.
    /// </summary>
    [Test]
    public async Task Should_emit_static_model_context_operations_without_owning_model_traversal()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("model-context.exp", ModelContextSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        string ResolveModel(string operation, IReadOnlyList<string> arguments) => operation switch
        {
            "TYPEOF" => $"ModelTypes({arguments[0]})",
            "ROLESOF" => $"ModelRoles({arguments[0]})",
            "USEDIN" => $"ModelUsers({arguments[0]}, {arguments[1]})",
            _ => throw new InvalidOperationException(operation),
        };

        var context = new ExpressExpressionEmissionContext(Resolve, "candidate", ResolveModel);
        var typeOf = ExpressExpressionEmitter.Emit(Find(schema, "TYPEOF(candidate)"), context);
        var rolesOf = ExpressExpressionEmitter.Emit(Find(schema, "ROLESOF(candidate)"), context);
        var usedIn = ExpressExpressionEmitter.Emit(
            Find(schema, "USEDIN(candidate,'MODEL_CONTEXT_EXPRESSION.ITEM')"),
            context);
        var self = ExpressExpressionEmitter.Emit(Find(schema, "SELF:=:SELF"), context);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.ModelContextExpression;

            internal sealed class ModelContextProbe : TedToolkit.Step21.SchemaDescriptor
            {
                internal static ExpressSet<string> Types(Item candidate) => {{typeOf.Code}};
                internal static ExpressSet<string> Roles(Item candidate) => {{rolesOf.Code}};
                internal static ExpressBag<Entity> Users(Item candidate) => {{usedIn.Code}};
                internal static LogicalValue SelfIdentity(Item candidate) => {{self.Code}};

                private static ExpressSet<string> ModelTypes(Entity candidate) =>
                    ["MODEL_CONTEXT_EXPRESSION.ITEM"];
                private static ExpressSet<string> ModelRoles(Entity candidate) =>
                    ["MODEL_CONTEXT_EXPRESSION.ITEM"];
                private static ExpressBag<Entity> ModelUsers(Entity candidate, string role) => [candidate];

                public override SchemaName Name => new("model_context_expression");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("model-context.exp", ModelContextSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("ModelContextProbe", throwOnError: true)!;
        var itemType = assembly.GetType(
            "TedToolkit.Step21.Generated.ModelContextExpression.Item",
            throwOnError: true)!;
        var candidate = Activator.CreateInstance(itemType)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That((IEnumerable<string>)probe.GetMethod("Types", flags)!.Invoke(null, [candidate])!)
                .IsEquivalentTo(["MODEL_CONTEXT_EXPRESSION.ITEM"]);
            await Assert.That((IEnumerable<string>)probe.GetMethod("Roles", flags)!.Invoke(null, [candidate])!)
                .IsEquivalentTo(["MODEL_CONTEXT_EXPRESSION.ITEM"]);
            await Assert.That((System.Collections.IEnumerable)probe.GetMethod("Users", flags)!
                .Invoke(null, [candidate])!).HasSingleItem();
            await Assert.That(probe.GetMethod("SelfIdentity", flags)!.Invoke(null, [candidate]))
                .IsEqualTo(LogicalValue.True);
        }

        var unsupported = Assert.Throws<InvalidOperationException>(() =>
            ExpressExpressionEmitter.Emit(Find(schema, "TYPEOF(candidate)"), Resolve));
        await Assert.That(unsupported.Message).StartsWith("model-context.exp:");
    }

    /// <summary>
    /// Verifies VALUE_IN and VALUE_UNIQUE retain UNKNOWN for unresolved optional ARRAY slots.
    /// </summary>
    [Test]
    public async Task Should_apply_unknown_semantics_to_optional_array_value_functions()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("aggregate-indeterminate.exp", AggregateIndeterminateSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var valueIn = ExpressExpressionEmitter.Emit(Find(schema, "VALUE_IN(items,candidate)"), Resolve);
        var valueUnique = ExpressExpressionEmitter.Emit(Find(schema, "VALUE_UNIQUE(items)"), Resolve);
        var membership = ExpressExpressionEmitter.Emit(Find(schema, "candidateINitems"), Resolve);
        var equality = ExpressExpressionEmitter.Emit(Find(schema, "left_items=right_items"), Resolve);
        var inequality = ExpressExpressionEmitter.Emit(Find(schema, "left_items<>right_items"), Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;

            internal sealed class AggregateIndeterminateProbe : SchemaDescriptor
            {
                internal static LogicalValue Contains(
                    ExpressArray<BigInteger> items,
                    BigInteger candidate) => {{valueIn.Code}};
                internal static LogicalValue Unique(ExpressArray<BigInteger> items) => {{valueUnique.Code}};
                internal static LogicalValue Member(
                    ExpressArray<BigInteger> items,
                    BigInteger candidate) => {{membership.Code}};
                internal static LogicalValue Equal(
                    ExpressArray<BigInteger> left_items,
                    ExpressArray<BigInteger> right_items) => {{equality.Code}};
                internal static LogicalValue NotEqual(
                    ExpressArray<BigInteger> left_items,
                    ExpressArray<BigInteger> right_items) => {{inequality.Code}};

                public override SchemaName Name => new("aggregate_indeterminate");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(
            consumer,
            ("aggregate-indeterminate.exp", AggregateIndeterminateSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("AggregateIndeterminateProbe", throwOnError: true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var items = new ExpressArray<BigInteger>(1, 3, isOptional: true);
        items[1] = 7;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(probe.GetMethod("Contains", flags)!.Invoke(null, [items, new BigInteger(7)]))
                .IsEqualTo(LogicalValue.True);
            await Assert.That(probe.GetMethod("Contains", flags)!.Invoke(null, [items, new BigInteger(8)]))
                .IsEqualTo(LogicalValue.Unknown);
            await Assert.That(probe.GetMethod("Member", flags)!.Invoke(null, [items, new BigInteger(8)]))
                .IsEqualTo(LogicalValue.Unknown);
            await Assert.That(probe.GetMethod("Unique", flags)!.Invoke(null, [items]))
                .IsEqualTo(LogicalValue.Unknown);
            items[2] = 7;
            await Assert.That(probe.GetMethod("Unique", flags)!.Invoke(null, [items]))
                .IsEqualTo(LogicalValue.False);
        }

        var equalButIncomplete = new ExpressArray<BigInteger>(1, 3, isOptional: true);
        equalButIncomplete[1] = 7;
        await Assert.That(probe.GetMethod("Equal", flags)!.Invoke(null, [equalButIncomplete, equalButIncomplete]))
            .IsEqualTo(LogicalValue.Unknown);
        await Assert.That(probe.GetMethod("NotEqual", flags)!.Invoke(null, [equalButIncomplete, equalButIncomplete]))
            .IsEqualTo(LogicalValue.Unknown);
        var mismatched = new ExpressArray<BigInteger>(1, 3, isOptional: true);
        mismatched[1] = 7;
        mismatched[2] = 8;
        await Assert.That(probe.GetMethod("Equal", flags)!.Invoke(null, [equalButIncomplete, mismatched]))
            .IsEqualTo(LogicalValue.False);
    }

    /// <summary>
    /// Verifies aggregate initializers inherit BAG, SET, LIST, and ARRAY types from their declaration context.
    /// </summary>
    [Test]
    public async Task Should_contextually_type_and_execute_every_aggregate_initializer_category()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("contextual-aggregate.exp", ContextualAggregateSchema),
        ]).Schemas.Single();
        var initializers = schema.Expressions.SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.AggregateInitializer)
            .DistinctBy(expression => (expression.Span.Start.Line, expression.Span.Start.Column))
            .ToArray();
        var byKind = initializers.ToDictionary(
            expression => ((ExpressBoundAggregateType)expression.Type.DeclaredType!).Kind);
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var bag = ExpressExpressionEmitter.Emit(byKind[ExpressAggregateKind.Bag], Resolve);
        var set = ExpressExpressionEmitter.Emit(byKind[ExpressAggregateKind.Set], Resolve);
        var list = ExpressExpressionEmitter.Emit(byKind[ExpressAggregateKind.List], Resolve);
        var array = ExpressExpressionEmitter.Emit(byKind[ExpressAggregateKind.Array], Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;

            internal sealed class ContextualAggregateProbe : SchemaDescriptor
            {
                internal static ExpressBag<BigInteger> Bag() => {{bag.Code}};
                internal static ExpressSet<BigInteger> Set() => {{set.Code}};
                internal static ExpressList<BigInteger> List() => {{list.Code}};
                internal static ExpressArray<BigInteger> Array() => {{array.Code}};

                public override SchemaName Name => new("contextual_aggregate");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(
            consumer,
            ("contextual-aggregate.exp", ContextualAggregateSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("ContextualAggregateProbe", throwOnError: true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(((System.Collections.IEnumerable)probe.GetMethod("Bag", flags)!
                .Invoke(null, null)!).Cast<object>()).Count().IsEqualTo(3);
            await Assert.That(((System.Collections.IEnumerable)probe.GetMethod("Set", flags)!
                .Invoke(null, null)!).Cast<object>()).Count().IsEqualTo(0);
            await Assert.That(((System.Collections.IEnumerable)probe.GetMethod("List", flags)!
                .Invoke(null, null)!).Cast<object>()).Count().IsEqualTo(2);
            var actualArray = (ExpressArray<BigInteger>)probe.GetMethod("Array", flags)!.Invoke(null, null)!;
            await Assert.That(actualArray.LowerIndex).IsEqualTo(2);
            await Assert.That(actualArray.UpperIndex).IsEqualTo(3);
            await Assert.That(actualArray[2]).IsEqualTo(new BigInteger(5));
            await Assert.That(actualArray[3]).IsEqualTo(new BigInteger(6));
        }
    }

    /// <summary>
    /// Verifies defined scalars unwrap statically while enumeration order and SELECT value equality retain nominal semantics.
    /// </summary>
    [Test]
    public async Task Should_execute_defined_enumeration_and_select_values_without_erasing_nominal_storage()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("nominal-expression.exp", NominalExpressionSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var sum = ExpressExpressionEmitter.Emit(Find(schema, "left_measure+right_measure"), Resolve);
        var order = ExpressExpressionEmitter.Emit(Find(schema, "current_state>state.off"), Resolve);
        var selectionContext = new ExpressExpressionEmissionContext(
            Resolve,
            resolveValueEquality: static (_, left, right) => $"SelectValueEquals({left}, {right})");
        var selection = ExpressExpressionEmitter.Emit(
            Find(schema, "left_choice=right_choice"),
            selectionContext);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.NominalExpression;

            internal sealed class NominalExpressionProbe : TedToolkit.Step21.SchemaDescriptor
            {
                internal static BigInteger Sum(Measure left_measure, Measure right_measure) => {{sum.Code}};
                internal static LogicalValue Ordered(State current_state) => {{order.Code}};
                internal static LogicalValue Equal(Choice left_choice, Choice right_choice) => {{selection.Code}};

                private static bool SelectValueEquals(Choice left, Choice right) => left == right;

                public override SchemaName Name => new("nominal_expression");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("nominal-expression.exp", NominalExpressionSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("NominalExpressionProbe", throwOnError: true)!;
        var measureType = assembly.GetType(
            "TedToolkit.Step21.Generated.NominalExpression.Measure",
            throwOnError: true)!;
        var stateType = assembly.GetType(
            "TedToolkit.Step21.Generated.NominalExpression.State",
            throwOnError: true)!;
        var choiceType = assembly.GetType(
            "TedToolkit.Step21.Generated.NominalExpression.Choice",
            throwOnError: true)!;
        var one = Activator.CreateInstance(measureType, new BigInteger(1))!;
        var two = Activator.CreateInstance(measureType, new BigInteger(2))!;
        var on = stateType.GetProperty("On", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
        var firstChoice = choiceType.GetMethod("FromMeasure", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, [one])!;
        var equalChoice = choiceType.GetMethod("FromMeasure", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, [one])!;
        var otherChoice = choiceType.GetMethod("FromMeasure", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, [two])!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(probe.GetMethod("Sum", flags)!.Invoke(null, [one, two]))
                .IsEqualTo(new BigInteger(3));
            await Assert.That(probe.GetMethod("Ordered", flags)!.Invoke(null, [on]))
                .IsEqualTo(LogicalValue.True);
            await Assert.That(probe.GetMethod("Equal", flags)!.Invoke(null, [firstChoice, equalChoice]))
                .IsEqualTo(LogicalValue.True);
            await Assert.That(probe.GetMethod("Equal", flags)!.Invoke(null, [firstChoice, otherChoice]))
                .IsEqualTo(LogicalValue.False);
        }
    }

    /// <summary>
    /// Verifies entity value equality uses a generated schema comparer while instance equality retains object identity.
    /// </summary>
    [Test]
    public async Task Should_distinguish_entity_value_and_instance_equality()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("entity-equality.exp", EntityEqualitySchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var context = new ExpressExpressionEmissionContext(
            Resolve,
            resolveValueEquality: static (_, left, right) => $"EntityValueEquals({left}, {right})");
        var valueEquality = ExpressExpressionEmitter.Emit(Find(schema, "left_item=right_item"), context);
        var instanceEquality = ExpressExpressionEmitter.Emit(Find(schema, "left_item:=:right_item"), context);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.EntityEquality;

            internal sealed class EntityEqualityProbe : TedToolkit.Step21.SchemaDescriptor
            {
                internal static LogicalValue ValueEqual(Item left_item, Item right_item) => {{valueEquality.Code}};
                internal static LogicalValue InstanceEqual(Item left_item, Item right_item) => {{instanceEquality.Code}};

                private static bool EntityValueEquals(Item left, Item right) => left.Code == right.Code;

                public override SchemaName Name => new("entity_equality");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("entity-equality.exp", EntityEqualitySchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("EntityEqualityProbe", throwOnError: true)!;
        var itemType = assembly.GetType(
            "TedToolkit.Step21.Generated.EntityEquality.Item",
            throwOnError: true)!;
        var left = itemType.GetConstructors().Single().Invoke([new BigInteger(7)]);
        var equalButDistinct = itemType.GetConstructors().Single().Invoke([new BigInteger(7)]);
        var flags = BindingFlags.Static | BindingFlags.NonPublic;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(probe.GetMethod("ValueEqual", flags)!.Invoke(null, [left, equalButDistinct]))
                .IsEqualTo(LogicalValue.True);
            await Assert.That(probe.GetMethod("InstanceEqual", flags)!.Invoke(null, [left, equalButDistinct]))
                .IsEqualTo(LogicalValue.False);
            await Assert.That(probe.GetMethod("InstanceEqual", flags)!.Invoke(null, [left, left]))
                .IsEqualTo(LogicalValue.True);
        }
    }

    /// <summary>
    /// Verifies QUERY preserves ARRAY positions and each variable-size aggregate result category.
    /// </summary>
    [Test]
    public async Task Should_preserve_query_result_category_bounds_and_array_positions()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("query-categories.exp", QueryCategoriesSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var queries = schema.Expressions.SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Query)
            .DistinctBy(expression => (expression.Span.Start.Line, expression.Span.Start.Column))
            .ToDictionary(expression => ((ExpressBoundAggregateType)expression.Type.DeclaredType!).Kind);
        var array = ExpressExpressionEmitter.Emit(queries[ExpressAggregateKind.Array], Resolve);
        var bag = ExpressExpressionEmitter.Emit(queries[ExpressAggregateKind.Bag], Resolve);
        var list = ExpressExpressionEmitter.Emit(queries[ExpressAggregateKind.List], Resolve);
        var set = ExpressExpressionEmitter.Emit(queries[ExpressAggregateKind.Set], Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;

            internal sealed class QueryCategoriesProbe : SchemaDescriptor
            {
                internal static ExpressArray<BigInteger> Array(
                    ExpressArray<BigInteger> array_items) => {{array.Code}};
                internal static ExpressBag<BigInteger> Bag(ExpressBag<BigInteger> bag_items) => {{bag.Code}};
                internal static ExpressList<BigInteger> List(ExpressList<BigInteger> list_items) => {{list.Code}};
                internal static ExpressSet<BigInteger> Set(ExpressSet<BigInteger> set_items) => {{set.Code}};

                public override SchemaName Name => new("query_categories");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("query-categories.exp", QueryCategoriesSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("QueryCategoriesProbe", throwOnError: true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var sourceArray = new ExpressArray<BigInteger>(2, 4, isOptional: true);
        sourceArray[2] = 1;
        sourceArray[4] = 3;
        var sourceBag = new ExpressBag<BigInteger>(0, 5) { 1, 2, 2, 3 };
        var sourceList = new ExpressList<BigInteger>(0, 5) { 3, 1, 2 };
        var sourceSet = new ExpressSet<BigInteger>(0, 5) { 1, 2, 3 };
        var actualArray = (ExpressArray<BigInteger>)probe.GetMethod("Array", flags)!
            .Invoke(null, [sourceArray])!;
        var actualBag = (ExpressBag<BigInteger>)probe.GetMethod("Bag", flags)!.Invoke(null, [sourceBag])!;
        var actualList = (ExpressList<BigInteger>)probe.GetMethod("List", flags)!.Invoke(null, [sourceList])!;
        var actualSet = (ExpressSet<BigInteger>)probe.GetMethod("Set", flags)!.Invoke(null, [sourceSet])!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(actualArray.LowerIndex).IsEqualTo(2);
            await Assert.That(actualArray.UpperIndex).IsEqualTo(4);
            await Assert.That(actualArray.IsSet(2)).IsFalse();
            await Assert.That(actualArray.IsSet(3)).IsFalse();
            await Assert.That(actualArray[4]).IsEqualTo(new BigInteger(3));
            await Assert.That(actualBag).IsEquivalentTo(new BigInteger[] { 2, 2, 3 });
            await Assert.That(actualBag.LowerBound).IsEqualTo(0);
            await Assert.That(actualBag.UpperBound).IsEqualTo(5);
            await Assert.That(actualList).IsEquivalentTo(new BigInteger[] { 3, 2 });
            await Assert.That(actualList.LowerBound).IsEqualTo(0);
            await Assert.That(actualList.UpperBound).IsEqualTo(5);
            await Assert.That(actualSet).IsEquivalentTo(new BigInteger[] { 2, 3 });
            await Assert.That(actualSet.LowerBound).IsEqualTo(0);
            await Assert.That(actualSet.UpperBound).IsEqualTo(5);
        }
    }

    /// <summary>
    /// Verifies every pure numeric/length built-in and aggregate bound/index category compiles and executes statically.
    /// </summary>
    [Test]
    public async Task Should_execute_complete_pure_builtin_and_aggregate_bound_matrix()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("builtin-coverage.exp", BuiltInCoverageSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var sourceExpressions = new[]
        {
            "ABS(-3)", "ACOS(1)", "ASIN(0)", "ATAN(0)", "ATAN(0,1)", "COS(0)", "EXP(0)",
            "LOG(1)", "LOG2(8)", "LOG10(100)", "SIN(0)", "SQRT(9)", "TAN(0)",
            "BLENGTH(%101)", "LENGTH('abc')",
        };
        var emitted = sourceExpressions.ToDictionary(
            source => source,
            source => ExpressExpressionEmitter.Emit(Find(schema, source), Resolve));
        var boundExpressions = schema.Expressions.SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Application
                && expression.Operation is "HIBOUND" or "HIINDEX" or "LOBOUND" or "LOINDEX")
            .DistinctBy(expression => (expression.Span.Start.Line, expression.Span.Start.Column))
            .ToArray();
        ExpressGeneratedExpression Bound(string operation, string argument) => ExpressExpressionEmitter.Emit(
            boundExpressions.Single(expression => expression.Operation == operation
                && expression.Children[0].SourceText == argument),
            Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;

            internal sealed class BuiltInCoverageProbe : SchemaDescriptor
            {
                internal static BigInteger Abs() => {{emitted["ABS(-3)"].Code}};
                internal static RealValue? Acos() => {{emitted["ACOS(1)"].Code}};
                internal static RealValue? Asin() => {{emitted["ASIN(0)"].Code}};
                internal static RealValue? Atan() => {{emitted["ATAN(0)"].Code}};
                internal static RealValue? AtanPair() => {{emitted["ATAN(0,1)"].Code}};
                internal static RealValue? Cos() => {{emitted["COS(0)"].Code}};
                internal static RealValue? Exp() => {{emitted["EXP(0)"].Code}};
                internal static RealValue? Log() => {{emitted["LOG(1)"].Code}};
                internal static RealValue? Log2() => {{emitted["LOG2(8)"].Code}};
                internal static RealValue? Log10() => {{emitted["LOG10(100)"].Code}};
                internal static RealValue? Sin() => {{emitted["SIN(0)"].Code}};
                internal static RealValue? Sqrt() => {{emitted["SQRT(9)"].Code}};
                internal static RealValue? Tan() => {{emitted["TAN(0)"].Code}};
                internal static BigInteger BinaryLength() => {{emitted["BLENGTH(%101)"].Code}};
                internal static BigInteger StringLength() => {{emitted["LENGTH('abc')"].Code}};
                internal static BigInteger ArrayBounds(ExpressArray<BigInteger> array_items) =>
                    {{Bound("HIBOUND", "array_items").Code}} + {{Bound("HIINDEX", "array_items").Code}}
                    + {{Bound("LOBOUND", "array_items").Code}} + {{Bound("LOINDEX", "array_items").Code}};
                internal static BigInteger BagBounds(ExpressBag<BigInteger> bag_items) =>
                    {{Bound("HIBOUND", "bag_items").Code}} + {{Bound("HIINDEX", "bag_items").Code}}
                    + {{Bound("LOBOUND", "bag_items").Code}} + {{Bound("LOINDEX", "bag_items").Code}};
                internal static BigInteger ListBounds(ExpressList<BigInteger> list_items) =>
                    {{Bound("HIBOUND", "list_items").Code}} + {{Bound("HIINDEX", "list_items").Code}}
                    + {{Bound("LOBOUND", "list_items").Code}} + {{Bound("LOINDEX", "list_items").Code}};
                internal static BigInteger SetBounds(ExpressSet<BigInteger> set_items) =>
                    {{Bound("HIBOUND", "set_items").Code}} + {{Bound("HIINDEX", "set_items").Code}}
                    + {{Bound("LOBOUND", "set_items").Code}} + {{Bound("LOINDEX", "set_items").Code}};

                public override SchemaName Name => new("builtin_coverage");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("builtin-coverage.exp", BuiltInCoverageSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("BuiltInCoverageProbe", throwOnError: true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var zero = new RealValue(0, 0);
        var one = new RealValue(1, 0);
        var array = new ExpressArray<BigInteger>(2, 4);
        array[2] = 1;
        array[3] = 2;
        array[4] = 3;
        var bag = new ExpressBag<BigInteger>(1, 5) { 1, 2, 3 };
        var list = new ExpressList<BigInteger>(2, 5) { 1, 2 };
        var set = new ExpressSet<BigInteger>(0, 5) { 1, 2 };

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(probe.GetMethod("Abs", flags)!.Invoke(null, null)).IsEqualTo(new BigInteger(3));
            foreach (var name in new[] { "Acos", "Asin", "Atan", "AtanPair", "Log", "Sin", "Tan", })
            {
                await Assert.That(probe.GetMethod(name, flags)!.Invoke(null, null)).IsEqualTo(zero);
            }

            foreach (var name in new[] { "Cos", "Exp", })
            {
                await Assert.That(probe.GetMethod(name, flags)!.Invoke(null, null)).IsEqualTo(one);
            }

            await Assert.That(probe.GetMethod("Log2", flags)!.Invoke(null, null)).IsEqualTo(new RealValue(3, 0));
            await Assert.That(probe.GetMethod("Log10", flags)!.Invoke(null, null)).IsEqualTo(new RealValue(2, 0));
            await Assert.That(probe.GetMethod("Sqrt", flags)!.Invoke(null, null)).IsEqualTo(new RealValue(3, 0));
            await Assert.That(probe.GetMethod("BinaryLength", flags)!.Invoke(null, null))
                .IsEqualTo(new BigInteger(3));
            await Assert.That(probe.GetMethod("StringLength", flags)!.Invoke(null, null))
                .IsEqualTo(new BigInteger(3));
            await Assert.That(probe.GetMethod("ArrayBounds", flags)!.Invoke(null, [array]))
                .IsEqualTo(new BigInteger(12));
            await Assert.That(probe.GetMethod("BagBounds", flags)!.Invoke(null, [bag]))
                .IsEqualTo(new BigInteger(10));
            await Assert.That(probe.GetMethod("ListBounds", flags)!.Invoke(null, [list]))
                .IsEqualTo(new BigInteger(10));
            await Assert.That(probe.GetMethod("SetBounds", flags)!.Invoke(null, [set]))
                .IsEqualTo(new BigInteger(8));
        }
    }

    /// <summary>
    /// Verifies user functions and entity constructors compile as direct strongly typed calls.
    /// </summary>
    [Test]
    public async Task Should_emit_direct_user_function_and_entity_constructor_applications()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("application-expression.exp", ApplicationSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var function = ExpressExpressionEmitter.Emit(Find(schema, "add(input_number,1)"), Resolve);
        var constructor = ExpressExpressionEmitter.Emit(
            Find(schema, "pair(input_number,add(input_number,1))"),
            Resolve);
        var emptyConstructor = ExpressExpressionEmitter.Emit(Find(schema, "marker()"), Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.ApplicationExpression;

            internal sealed class ApplicationExpressionProbe : TedToolkit.Step21.SchemaDescriptor
            {
                internal static BigInteger Function(BigInteger input_number) => {{function.Code}};
                internal static Pair Constructor(BigInteger input_number) => {{constructor.Code}};
                internal static Marker EmptyConstructor() => {{emptyConstructor.Code}};

                private static BigInteger add(BigInteger left_value, BigInteger right_value) =>
                    left_value + right_value;

                public override SchemaName Name => new("application_expression");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("application-expression.exp", ApplicationSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("ApplicationExpressionProbe", throwOnError: true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(probe.GetMethod("Function", flags)!.Invoke(null, [new BigInteger(4)]))
                .IsEqualTo(new BigInteger(5));
            var pair = probe.GetMethod("Constructor", flags)!.Invoke(null, [new BigInteger(4)])!;
            await Assert.That(pair.GetType().GetProperty("LeftValue")!.GetValue(pair)).IsEqualTo(new BigInteger(4));
            await Assert.That(pair.GetType().GetProperty("RightValue")!.GetValue(pair)).IsEqualTo(new BigInteger(5));
            await Assert.That(probe.GetMethod("EmptyConstructor", flags)!.Invoke(null, null)!.GetType().Name)
                .IsEqualTo("Marker");
        }
    }

    /// <summary>
    /// Verifies an invalid encoded Unicode scalar is rejected with its retained source location.
    /// </summary>
    [Test]
    public async Task Should_report_invalid_encoded_string_with_source_location()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("invalid-encoded-string.exp", InvalidEncodedStringSchema),
        ]).Schemas.Single();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExpressExpressionEmitter.Emit(schema.Expressions.Single()));

        await Assert.That(exception.Message).StartsWith("invalid-encoded-string.exp:")
            .And.Contains("contains invalid Unicode scalar U+00110000");
    }

    /// <summary>
    /// Verifies repetition evaluates its count once and returns indeterminate for an invalid runtime count.
    /// </summary>
    [Test]
    public async Task Should_guard_dynamic_aggregate_repetition_counts()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("repetition-expression.exp", RepetitionSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var repetition = ExpressExpressionEmitter.Emit(Find(schema, "[7:repetition_count]"), Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;

            internal sealed class RepetitionProbe : SchemaDescriptor
            {
                internal static ExpressList<BigInteger>? Repeat(BigInteger repetition_count) => {{repetition.Code}};

                public override SchemaName Name => new("repetition_expression");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("repetition-expression.exp", RepetitionSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("RepetitionProbe", throwOnError: true)!;
        var method = probe.GetMethod("Repeat", BindingFlags.Static | BindingFlags.NonPublic)!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That((ExpressList<BigInteger>)method.Invoke(null, [new BigInteger(3)])!)
                .IsEquivalentTo(new BigInteger[] { 7, 7, 7 });
            await Assert.That(method.Invoke(null, [new BigInteger(-1)])).IsNull();
        }
    }

    /// <summary>
    /// Verifies ARRAY repetition preserves its fixed domain and sparse slot positions.
    /// </summary>
    [Test]
    public async Task Should_execute_array_repetition_and_sparse_initializers()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("array-repetition.exp", ArrayRepetitionSchema),
        ]);
        await Assert.That(compilation.SyntaxDiagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, compilation.SyntaxDiagnostics.Select(item => item.Message)));
        await Assert.That(compilation.BindingDiagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, compilation.BindingDiagnostics.Select(item => item.Message)));
        var schema = compilation.Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        var repetition = ExpressExpressionEmitter.Emit(Find(schema, "[7:repetition_count]"), Resolve);
        var sparse = ExpressExpressionEmitter.Emit(Find(schema, "[1,?,3]"), Resolve);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using System.Numerics;
            using TedToolkit.Step21;

            internal sealed class ArrayRepetitionProbe : SchemaDescriptor
            {
                internal static ExpressArray<BigInteger>? Repeat(BigInteger repetition_count) => {{repetition.Code}};
                internal static ExpressArray<BigInteger> Sparse() => {{sparse.Code}};

                public override SchemaName Name => new("array_repetition_expression");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(consumer, ("array-repetition.exp", ArrayRepetitionSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("ArrayRepetitionProbe", throwOnError: true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            var repeated = (ExpressArray<BigInteger>)probe.GetMethod("Repeat", flags)!
                .Invoke(null, [new BigInteger(3)])!;
            await Assert.That(repeated).IsEquivalentTo(new BigInteger[] { 7, 7, 7 });
            await Assert.That(probe.GetMethod("Repeat", flags)!.Invoke(null, [new BigInteger(2)])).IsNull();
            await Assert.That(probe.GetMethod("Repeat", flags)!.Invoke(null, [new BigInteger(-1)])).IsNull();
            var sparseArray = (ExpressArray<BigInteger>)probe.GetMethod("Sparse", flags)!.Invoke(null, null)!;
            await Assert.That(sparseArray.IsSet(2)).IsTrue();
            await Assert.That(sparseArray.IsSet(3)).IsFalse();
            await Assert.That(sparseArray.IsSet(4)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies complex entity construction compiles to a static model operation owned by the later complex mapper.
    /// </summary>
    [Test]
    public async Task Should_emit_complex_entity_construction_as_a_static_model_operation()
    {
        var schema = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("complex-construction.exp", ComplexConstructionSchema),
        ]).Schemas.Single();
        string Resolve(ExpressBoundName reference) => reference.Name.ToLowerInvariant();
        string ResolveModel(string operation, IReadOnlyList<string> arguments) => operation == "COMPLEX_CONSTRUCTOR"
            ? $"ConstructComplex({arguments[0]}, {arguments[1]})"
            : throw new InvalidOperationException(operation);
        var context = new ExpressExpressionEmissionContext(Resolve, resolveModelFunction: ResolveModel);
        var construction = ExpressExpressionEmitter.Emit(Find(schema, "left_part||right_part"), context);
        var consumer = $$"""
            #nullable enable
            using System.Collections.Generic;
            using TedToolkit.Step21;
            using TedToolkit.Step21.Generated.ComplexConstructionExpression;

            internal sealed class ComplexConstructionProbe : TedToolkit.Step21.SchemaDescriptor
            {
                internal static Entity Combine(FirstPart left_part, SecondPart right_part) => {{construction.Code}};

                private static Entity ConstructComplex(FirstPart left, SecondPart right) => left;

                public override SchemaName Name => new("complex_construction_expression");
                protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) => null;
                protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
                    ExchangeStructure structure,
                    Entity value,
                    IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components) => [];
                protected override ValidationResult ValidateCore(
                    ExchangeStructure structure,
                    IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);
                protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
                    ExchangeStructure structure) => [];
                protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
                    Entity value) => [];
            }
            """;
        var result = GeneratorHostTests.Run(
            consumer,
            ("complex-construction.exp", ComplexConstructionSchema));
        var assembly = Emit(result.OutputCompilation);
        var probe = assembly.GetType("ComplexConstructionProbe", throwOnError: true)!;
        var firstType = assembly.GetType(
            "TedToolkit.Step21.Generated.ComplexConstructionExpression.FirstPart",
            throwOnError: true)!;
        var secondType = assembly.GetType(
            "TedToolkit.Step21.Generated.ComplexConstructionExpression.SecondPart",
            throwOnError: true)!;
        var first = Activator.CreateInstance(firstType)!;
        var second = Activator.CreateInstance(secondType)!;
        var actual = probe.GetMethod("Combine", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [first, second]);

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(actual).IsSameReferenceAs(first);
        }
    }

    private static ExpressBoundExpression Find(ExpressBoundSchema schema, string sourceText)
    {
        return schema.Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Single(expression => expression.SourceText == sourceText);
    }

    private static Assembly Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }

        stream.Position = 0;
        var loadContext = new AssemblyLoadContext($"ExpressionTests-{Guid.NewGuid():N}", isCollectible: true);
        return loadContext.LoadFromStream(stream);
    }
}