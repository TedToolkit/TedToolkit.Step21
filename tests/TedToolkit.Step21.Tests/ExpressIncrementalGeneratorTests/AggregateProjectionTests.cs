using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>
/// Proves generated ARRAY, LIST, BAG, and SET value projections.
/// </summary>
public sealed class AggregateProjectionTests
{
    private const string AGGREGATE_SCHEMA = """
        SCHEMA aggregate_model;
        ENTITY item;
        END_ENTITY;
        TYPE choice = SELECT (item);
        END_TYPE;
        TYPE integer_list = LIST [1:2] OF UNIQUE INTEGER;
        END_TYPE;
        TYPE integer_bag = BAG [0:2] OF INTEGER;
        END_TYPE;
        TYPE integer_set = SET [0:2] OF INTEGER;
        END_TYPE;
        TYPE integer_array = ARRAY [0:2] OF OPTIONAL UNIQUE INTEGER;
        END_TYPE;
        TYPE nested_bag = BAG [0:3] OF integer_list;
        END_TYPE;
        TYPE item_set = SET [0:?] OF item;
        END_TYPE;
        TYPE choice_list = LIST [0:?] OF choice;
        END_TYPE;
        TYPE matrix = ARRAY [-1:1] OF OPTIONAL UNIQUE integer_list;
        END_TYPE;
        ENTITY holder;
          nested : nested_bag;
          items : item_set;
          choices : choice_list;
          matrix_value : matrix;
          direct_list : LIST [0:?] OF STRING;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string AGGREGATE_CONSUMER = """
        #nullable enable
        using System.Linq;
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.AggregateModel;
        internal static class AggregateConsumer
        {
            internal static bool Exercise()
            {
                var listValue = new ExpressList<BigInteger>(1, 2, isUnique: true)
                {
                    BigInteger.One,
                    BigInteger.One,
                    new BigInteger(2),
                };
                var list = new IntegerList(listValue);
                var bagCandidate = new ExpressBag<BigInteger>(0, 2)
                {
                    BigInteger.One,
                    BigInteger.One,
                    new BigInteger(2),
                };
                var setCandidate = new ExpressSet<BigInteger>(0, 2)
                {
                    BigInteger.One,
                    BigInteger.One,
                    new BigInteger(2),
                };
                var arrayCandidate = new ExpressArray<BigInteger>(0, 2, isOptional: true, isUnique: true);
                arrayCandidate[0] = BigInteger.One;
                arrayCandidate[1] = BigInteger.One;
                arrayCandidate[2] = new BigInteger(2);
                _ = new IntegerBag(bagCandidate);
                _ = new IntegerSet(setCandidate);
                _ = new IntegerArray(arrayCandidate);
                var bagValue = new ExpressBag<IntegerList>(0, 3) { list, list };
                var item = new Item();
                var setValue = new ExpressSet<IItem> { item, item };
                var choicesValue = new ExpressList<Choice> { Choice.FromItem(item) };
                var matrixValue = new ExpressArray<IntegerList>(-1, 1, isOptional: true, isUnique: true);
                matrixValue[-1] = list;
                matrixValue[1] = list;
                var direct = new ExpressList<string> { "first", "second" };
                var holder = new Holder(
                    new NestedBag(bagValue),
                    new ItemSet(setValue),
                    new ChoiceList(choicesValue),
                    new Matrix(matrixValue),
                    direct);

                IExpressAggregate<BigInteger> generalList = listValue;
                IExpressAggregate<BigInteger> generalBag = bagCandidate;
                IExpressAggregate<BigInteger> generalSet = setCandidate;
                IExpressAggregate<BigInteger> generalArray = arrayCandidate;

                holder.DirectList.Add("third");
                return listValue.Validate().Failures.Count == 2
                    && bagCandidate.Validate().Failures.Count == 1
                    && setCandidate.Validate().Failures.Count == 2
                    && arrayCandidate.Validate().Failures.Count == 1
                    && bagValue.Validate().IsValid
                    && setValue.Validate().Failures.Count == 1
                    && matrixValue.Validate().Failures.Count == 1
                    && (generalList.LowBound, generalList.HighBound,
                        generalList.LowIndex, generalList.HighIndex, generalList.IsUnique)
                        == (1, 2, 1, 3, true)
                    && (generalBag.LowBound, generalBag.HighBound,
                        generalBag.LowIndex, generalBag.HighIndex, generalBag.IsUnique)
                        == (0, 2, 1, 3, false)
                    && (generalSet.LowBound, generalSet.HighBound,
                        generalSet.LowIndex, generalSet.HighIndex, generalSet.IsUnique)
                        == (0, 2, 1, 3, true)
                    && (generalArray.LowBound, generalArray.HighBound,
                        generalArray.LowIndex, generalArray.HighIndex, generalArray.IsUnique)
                        == (0, 2, 0, 2, true)
                    && holder.DirectList.SequenceEqual(["first", "second", "third"]);
            }
        }
        """;

    private const string AGGREGATE_FOUNDATION_SCHEMA = """
        SCHEMA aggregate_foundation;
        ENTITY remote_item;
        END_ENTITY;
        TYPE remote_choice = SELECT (remote_item);
        END_TYPE;
        END_SCHEMA;
        """;

    private const string AGGREGATE_BOUNDARY_SCHEMA = """
        SCHEMA aggregate_boundary;
        USE FROM aggregate_foundation (remote_item, remote_choice);
        CONSTANT
          lower_index : INTEGER := -2;
          upper_index : INTEGER := 2;
        END_CONSTANT;
        TYPE open_list = LIST OF INTEGER;
        END_TYPE;
        TYPE symbolic_array = ARRAY [lower_index:upper_index] OF INTEGER;
        END_TYPE;
        TYPE remote_items = BAG [0:?] OF remote_item;
        END_TYPE;
        ENTITY boundary_holder;
          nested : LIST [0:?] OF SET [1:?] OF INTEGER;
          choices : SET OF remote_choice;
          optional_values : OPTIONAL LIST OF INTEGER;
        END_ENTITY;
        END_SCHEMA;
        """;

    /// <summary>
    /// Verifies every aggregate category and nested/entity/SELECT element shape compiles without collection collapse.
    /// </summary>
    [Test]
    public async Task Should_generate_exact_mutable_aggregate_categories_and_element_shapes()
    {
        var result = GeneratorHostTests.Run(
            AGGREGATE_CONSUMER,
            ("schemas/aggregate-model.exp", AGGREGATE_SCHEMA));
        var integerList = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.AggregateModel.IntegerList")
            ?? throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        var nestedBag = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Generated.AggregateModel.NestedBag");
        var itemSet = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Generated.AggregateModel.ItemSet");
        var choiceList = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Generated.AggregateModel.ChoiceList");
        var matrix = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Generated.AggregateModel.Matrix");
        var holder = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Generated.AggregateModel.Holder");

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                .IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(RequiredProperty(integerList, "Value").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.ExpressList<System.Numerics.BigInteger>");
            await Assert.That(RequiredProperty(nestedBag, "Value").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.ExpressBag<TedToolkit.Step21.Generated.AggregateModel.IntegerList>");
            await Assert.That(RequiredProperty(itemSet, "Value").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.ExpressSet<TedToolkit.Step21.Generated.AggregateModel.IItem>");
            await Assert.That(RequiredProperty(choiceList, "Value").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.ExpressList<TedToolkit.Step21.Generated.AggregateModel.Choice>");
            await Assert.That(RequiredProperty(matrix, "Value").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.ExpressArray<TedToolkit.Step21.Generated.AggregateModel.IntegerList>");
            await Assert.That(RequiredProperty(holder, "DirectList").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.ExpressList<string>");
            await Assert.That(integerList.GetDocumentationCommentXml())
                .Contains("LIST [1:2] OF UNIQUE INTEGER")
                .And.Contains("Mutations do not run schema validation");
            await Assert.That(matrix.GetDocumentationCommentXml())
                .Contains("ARRAY [-1:1] OF OPTIONAL UNIQUE integer_list")
                .And.Contains("Mutations do not run schema validation");
            await Assert.That(RequiredProperty(holder, "DirectList").GetDocumentationCommentXml())
                .Contains("LIST [0:?] OF STRING")
                .And.Contains("Mutations do not run schema validation");
        }

        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("AggregateConsumer", throwOnError: true)!;
        await Assert.That((bool)consumer.GetMethod(
            "Exercise",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, null)!).IsTrue();
    }

    /// <summary>
    /// Verifies unbounded, symbolic, inline nested, cross-schema, and outer OPTIONAL shapes retain their exact contract.
    /// </summary>
    [Test]
    public async Task Should_preserve_all_aggregate_projection_boundaries_without_reflection_metadata()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/foundation.exp", AGGREGATE_FOUNDATION_SCHEMA),
            ("schemas/boundary.exp", AGGREGATE_BOUNDARY_SCHEMA));
        var openList = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Generated.AggregateBoundary.OpenList");
        var symbolicArray = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Generated.AggregateBoundary.SymbolicArray");
        var remoteItems = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Generated.AggregateBoundary.RemoteItems");
        var holder = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Generated.AggregateBoundary.BoundaryHolder");
        var generatedText = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                .IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(RequiredProperty(openList, "Value").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.ExpressList<System.Numerics.BigInteger>");
            await Assert.That(openList.GetDocumentationCommentXml()).Contains("LIST OF INTEGER");
            await Assert.That(RequiredProperty(symbolicArray, "Value").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.ExpressArray<System.Numerics.BigInteger>");
            await Assert.That(symbolicArray.GetDocumentationCommentXml())
                .Contains("ARRAY [lower_index:upper_index] OF INTEGER");
            await Assert.That(RequiredProperty(remoteItems, "Value").Type.ToDisplayString())
                .IsEqualTo(
                    "TedToolkit.Step21.ExpressBag<TedToolkit.Step21.Generated.AggregateFoundation.IRemoteItem>");
            await Assert.That(RequiredProperty(holder, "Nested").Type.ToDisplayString())
                .IsEqualTo(
                    "TedToolkit.Step21.ExpressList<TedToolkit.Step21.ExpressSet<System.Numerics.BigInteger>>");
            await Assert.That(RequiredProperty(holder, "Nested").GetDocumentationCommentXml())
                .Contains("LIST [0:?] OF SET [1:?] OF INTEGER");
            await Assert.That(RequiredProperty(holder, "Choices").Type.ToDisplayString())
                .IsEqualTo(
                    "TedToolkit.Step21.ExpressSet<TedToolkit.Step21.Generated.AggregateFoundation.RemoteChoice>");
            await Assert.That(RequiredProperty(holder, "OptionalValues").Type.NullableAnnotation)
                .IsEqualTo(NullableAnnotation.Annotated);
            await Assert.That(generatedText)
                .DoesNotContain("global::System.Collections.Generic.List<global::System.Numerics.BigInteger>");
            await Assert.That(generatedText).DoesNotContain("System.Reflection");
            await Assert.That(generatedText).DoesNotContain(" dynamic ");
        }
    }

    /// <summary>
    /// Verifies aggregate output is independent of additional-file order and machine-specific paths.
    /// </summary>
    [Test]
    public async Task Should_generate_aggregate_output_deterministically_for_reordered_relocated_inputs()
    {
        var first = GeneratorHostTests.Run(
            ("C:/agent-a/foundation.exp", AGGREGATE_FOUNDATION_SCHEMA),
            ("C:/agent-a/boundary.exp", AGGREGATE_BOUNDARY_SCHEMA));
        var second = GeneratorHostTests.Run(
            ("D:/agent-b/boundary.exp", AGGREGATE_BOUNDARY_SCHEMA),
            ("D:/agent-b/foundation.exp", AGGREGATE_FOUNDATION_SCHEMA));

        await Assert.That(GeneratedSnapshot(first)).IsEqualTo(GeneratedSnapshot(second));
    }

    private static INamedTypeSymbol RequiredType(Compilation compilation, string metadataName) =>
        compilation.GetTypeByMetadataName(metadataName)
        ?? throw new InvalidOperationException($"Generated type '{metadataName}' was not found.");

    private static IPropertySymbol RequiredProperty(INamedTypeSymbol type, string name) =>
        type.GetMembers(name).OfType<IPropertySymbol>().Single();

    private static string GeneratedSnapshot(GeneratorHostTests.GeneratorResult result) =>
        string.Join(
            "\n---\n",
            result.GeneratedSources
                .OrderBy(source => source.HintName, StringComparer.Ordinal)
                .Select(source => $"{source.HintName}\n{source.SourceText}"));

    private static System.Reflection.Assembly Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));

        return System.Reflection.Assembly.Load(stream.ToArray());
    }
}
