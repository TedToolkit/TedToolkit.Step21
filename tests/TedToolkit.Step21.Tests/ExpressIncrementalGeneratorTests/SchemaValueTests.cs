using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Proves generated nominal scalar, enumeration, select, and entity-value contracts.</summary>
public sealed class SchemaValueTests
{
    private const string VALUE_SCHEMA = """
        SCHEMA value_model;
        TYPE positive_count = INTEGER;
        END_TYPE;
        TYPE precise_value = REAL;
        END_TYPE;
        TYPE numeric_value = NUMBER;
        END_TYPE;
        TYPE text_value = STRING;
        END_TYPE;
        TYPE bit_value = BINARY;
        END_TYPE;
        TYPE boolean_value = BOOLEAN;
        END_TYPE;
        TYPE logical_value_type = LOGICAL;
        END_TYPE;
        TYPE colour = ENUMERATION OF (red, green);
        END_TYPE;
        TYPE open_colour = EXTENSIBLE ENUMERATION OF (blue);
        END_TYPE;
        ENTITY item;
        END_ENTITY;
        TYPE choice = SELECT (positive_count, colour, item);
        END_TYPE;
        ENTITY holder;
          count : positive_count;
          choice_value : choice;
          raw_integer : INTEGER;
          raw_real : REAL;
          raw_number : NUMBER;
          raw_string : STRING;
          raw_binary : BINARY;
          raw_boolean : BOOLEAN;
          raw_logical : LOGICAL;
          optional_text : OPTIONAL text_value;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string EXTENDED_VALUE_SCHEMA = """
        SCHEMA extended_values;
        TYPE root_alias = INTEGER;
        END_TYPE;
        TYPE nested_alias = root_alias;
        END_TYPE;
        TYPE base_state = EXTENSIBLE ENUMERATION OF (on, off);
        END_TYPE;
        TYPE extended_state = ENUMERATION BASED_ON base_state WITH (automatic);
        END_TYPE;
        ENTITY base_item;
        END_ENTITY;
        ENTITY extra_item;
        END_ENTITY;
        TYPE base_choice = EXTENSIBLE GENERIC_ENTITY SELECT (base_item);
        END_TYPE;
        TYPE extended_choice = SELECT BASED_ON base_choice WITH (extra_item);
        END_TYPE;
        END_SCHEMA;
        """;

    private const string FOUNDATION_SCHEMA = """
        SCHEMA value_foundation;
        TYPE length_value = REAL;
        END_TYPE;
        ENTITY target;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string VALUE_CONSUMER_SCHEMA = """
        SCHEMA value_consumer;
        USE FROM value_foundation (length_value, target);
        ENTITY holder;
          measured_value : length_value;
          target_ref : target;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string VALUE_COLLISION_SCHEMA = """
        SCHEMA value_collision;
        ENTITY item;
        END_ENTITY;
        TYPE choice = SELECT (item);
        END_TYPE;
        ENTITY choice_kind;
        END_ENTITY;
        TYPE status = ENUMERATION OF (value_);
        END_TYPE;
        END_SCHEMA;
        """;

    private const string DEFERRED_SELECT_EXTENSION_SCHEMA = """
        SCHEMA deferred_select_extension;
        ENTITY item;
        END_ENTITY;
        TYPE integer_list = LIST OF INTEGER;
        END_TYPE;
        TYPE base_choice = EXTENSIBLE SELECT (item);
        END_TYPE;
        TYPE extended_choice = SELECT BASED_ON base_choice WITH (integer_list);
        END_TYPE;
        END_SCHEMA;
        """;

    private const string VALUE_CONSUMER = """
        #nullable enable
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Schemas.ValueModel;
        internal static class ValueConsumer
        {
            internal static bool Exercise()
            {
                var count = new PositiveCount(BigInteger.One);
                var precise = new PreciseValue(new RealValue(125, -2));
                var numeric = new NumericValue(NumberValue.FromInteger(2));
                var text = new TextValue("text");
                var bits = new BitValue(new BinaryValue("001"));
                var boolean = new BooleanValue(true);
                var logical = new LogicalValueType(TedToolkit.Step21.LogicalValue.Unknown);
                var colour = Colour.Red;
                var open = new OpenColour("CUSTOM1");
                var item = new Item();
                var choice = Choice.FromPositiveCount(count);
                try
                {
                    _ = new OpenColour("not-valid");
                    return false;
                }
                catch (global::System.FormatException)
                {
                }

                var holder = new Holder(
                    count,
                    choice,
                    BigInteger.One,
                    new RealValue(25, -1),
                    NumberValue.FromReal(new RealValue(25, -1)),
                    "raw",
                    new BinaryValue("1"),
                    true,
                    TedToolkit.Step21.LogicalValue.True);
                holder.OptionalText = text;

                return precise.Value == new RealValue(125, -2)
                    && count == new PositiveCount(BigInteger.One)
                    && numeric.Value.Kind == NumberValueKind.Integer
                    && bits.Value.Equals(new BinaryValue("001"))
                    && boolean.Value
                    && logical.Value == TedToolkit.Step21.LogicalValue.Unknown
                    && colour == Colour.Red
                    && open.Value == "CUSTOM1"
                    && choice.Kind == ChoiceKind.PositiveCount
                    && choice == Choice.FromPositiveCount(count)
                    && choice.TryGetPositiveCount(out var selected)
                    && selected == count
                    && choice.Match(
                        positiveCount => positiveCount.Value.ToString(),
                        selectedColour => selectedColour.Value,
                        selectedItem => selectedItem.ToString()) == "1"
                    && holder.RawString == "raw"
                    && ReferenceEquals(item, Choice.FromItem(item).Match(
                        _ => item,
                        _ => item,
                        selectedItem => selectedItem));
            }
        }
        """;

    /// <summary>
    /// Verifies readonly record structs, enumeration construction boundaries, and record-based exhaustive SELECT APIs.
    /// </summary>
    [Test]
    public async Task Should_generate_nominal_defined_enumeration_and_select_values()
    {
        var result = GeneratorHostTests.Run(VALUE_CONSUMER, ("schemas/values.exp", VALUE_SCHEMA));
        const string invalidClosedEnumerationConsumer = """
            using TedToolkit.Step21.Schemas.ValueModel;
            internal sealed class InvalidConsumer
            {
                internal static Colour Create() => new Colour("RED");
            }
            """;
        var invalidClosedEnumeration = GeneratorHostTests.Run(
            invalidClosedEnumerationConsumer,
            ("schemas/values.exp", VALUE_SCHEMA));
        var positiveCount = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Schemas.ValueModel.PositiveCount");
        var colour = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Schemas.ValueModel.Colour");
        var openColour = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Schemas.ValueModel.OpenColour");
        var choice = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Schemas.ValueModel.Choice");
        var choiceKind = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Schemas.ValueModel.ChoiceKind");

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                .IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(positiveCount.IsRecord && positiveCount.IsValueType && positiveCount.IsReadOnly).IsTrue();
            await Assert.That(RequiredProperty(positiveCount, "Value").Type.ToDisplayString())
                .IsEqualTo("System.Numerics.BigInteger");
            await Assert.That(colour.IsRecord && colour.IsValueType && colour.IsReadOnly).IsTrue();
            await Assert.That(colour.Constructors.Any(constructor =>
                constructor.DeclaredAccessibility == Accessibility.Public && constructor.Parameters.Length > 0))
                .IsFalse();
            await Assert.That(colour.GetMembers().OfType<IPropertySymbol>()
                .Where(property => property.IsStatic).Select(property => property.Name))
                .IsEquivalentTo(["Red", "Green"]);
            await Assert.That(openColour.Constructors.Single(constructor =>
                constructor.DeclaredAccessibility == Accessibility.Public && constructor.Parameters.Length == 1)
                .Parameters.Single().Type.SpecialType)
                .IsEqualTo(SpecialType.System_String);
            await Assert.That(choice.IsRecord && choice.IsReferenceType && choice.IsSealed).IsTrue();
            await Assert.That(choiceKind.TypeKind).IsEqualTo(TypeKind.Enum);
            await Assert.That(choiceKind.GetMembers().OfType<IFieldSymbol>()
                .Where(field => field.HasConstantValue).Select(field => field.Name))
                .IsEquivalentTo(["PositiveCount", "Colour", "Item"]);
            await Assert.That(choice.GetMembers().OfType<IMethodSymbol>().Select(method => method.Name))
                .Contains("FromPositiveCount")
                .And.Contains("TryGetPositiveCount")
                .And.Contains("Match");
            await Assert.That(choice.GetMembers().OfType<IFieldSymbol>()
                .Where(field => field.Name == "_value")
                .Select(field => (field.Name, field.Type.SpecialType)))
                .IsEquivalentTo([("_value", SpecialType.System_Object)]);
            await Assert.That(invalidClosedEnumeration.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.Id)).Contains("CS0122");
            var generatedText = string.Join(
                Environment.NewLine,
                result.GeneratedSources.Select(source => source.SourceText.ToString()));
            await Assert.That(generatedText).DoesNotContain("System.Reflection");
            await Assert.That(generatedText).DoesNotContain("dynamic");
            await Assert.That(generatedText).DoesNotContain("TedToolkit.RoslynHelper");
        }

        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("ValueConsumer", throwOnError: true)!;
        await Assert.That((bool)consumer.GetMethod(
            "Exercise",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, null)!).IsTrue();
    }

    /// <summary>
    /// Verifies generated entity properties and mandatory construction cover every non-aggregate scalar mapping.
    /// </summary>
    [Test]
    public async Task Should_connect_all_non_aggregate_values_to_generated_entities()
    {
        var result = GeneratorHostTests.Run(("schemas/values.exp", VALUE_SCHEMA));
        var holder = RequiredType(result.OutputCompilation, "TedToolkit.Step21.Schemas.ValueModel.Holder");
        var constructor = holder.Constructors.Single(constructor => constructor.DeclaredAccessibility == Accessibility.Public);

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(RequiredProperty(holder, "Count").Type.Name).IsEqualTo("PositiveCount");
            await Assert.That(RequiredProperty(holder, "ChoiceValue").Type.Name).IsEqualTo("Choice");
            await Assert.That(RequiredProperty(holder, "RawInteger").Type.ToDisplayString())
                .IsEqualTo("System.Numerics.BigInteger");
            await Assert.That(RequiredProperty(holder, "RawReal").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.RealValue");
            await Assert.That(RequiredProperty(holder, "RawNumber").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.NumberValue");
            await Assert.That(RequiredProperty(holder, "RawString").Type.SpecialType)
                .IsEqualTo(SpecialType.System_String);
            await Assert.That(RequiredProperty(holder, "RawBinary").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.BinaryValue");
            await Assert.That(RequiredProperty(holder, "RawBoolean").Type.SpecialType)
                .IsEqualTo(SpecialType.System_Boolean);
            await Assert.That(RequiredProperty(holder, "RawLogical").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.LogicalValue");
            await Assert.That(RequiredProperty(holder, "OptionalText").Type.NullableAnnotation)
                .IsEqualTo(NullableAnnotation.Annotated);
            await Assert.That(constructor.Parameters.Select(parameter => parameter.Name)).IsEquivalentTo(
            [
                "count",
                "choiceValue",
                "rawInteger",
                "rawReal",
                "rawNumber",
                "rawString",
                "rawBinary",
                "rawBoolean",
                "rawLogical",
            ]);
        }
    }

    /// <summary>
    /// Verifies nominal aliases and enumeration/select extensions retain inherited and local value alternatives.
    /// </summary>
    [Test]
    public async Task Should_generate_nested_defined_types_and_constructed_type_extensions()
    {
        var result = GeneratorHostTests.Run(("schemas/extended-values.exp", EXTENDED_VALUE_SCHEMA));
        var nestedAlias = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Schemas.ExtendedValues.NestedAlias");
        var extendedState = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Schemas.ExtendedValues.ExtendedState");
        var baseState = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Schemas.ExtendedValues.BaseState");
        var baseChoice = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Schemas.ExtendedValues.BaseChoice");
        var baseChoiceKind = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Schemas.ExtendedValues.BaseChoiceKind");
        var extendedChoiceKind = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Schemas.ExtendedValues.ExtendedChoiceKind");

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(RequiredProperty(nestedAlias, "Value").Type.Name).IsEqualTo("RootAlias");
            await Assert.That(extendedState.GetMembers().OfType<IPropertySymbol>()
                .Where(property => property.IsStatic).Select(property => property.Name))
                .IsEquivalentTo(["On", "Off", "Automatic"]);
            await Assert.That(baseState.GetMembers().OfType<IPropertySymbol>()
                .Where(property => property.IsStatic).Select(property => property.Name))
                .IsEquivalentTo(["On", "Off", "Automatic"]);
            await Assert.That(baseChoiceKind.GetMembers().OfType<IFieldSymbol>()
                .Where(field => field.HasConstantValue).Select(field => field.Name))
                .IsEquivalentTo(["BaseItem", "ExtraItem"]);
            await Assert.That(baseChoice.GetMembers().OfType<IMethodSymbol>().Select(method => method.Name))
                .Contains("FromExtraItem");
            await Assert.That(extendedChoiceKind.GetMembers().OfType<IFieldSymbol>()
                .Where(field => field.HasConstantValue).Select(field => field.Name))
                .IsEquivalentTo(["BaseItem", "ExtraItem"]);
        }
    }

    /// <summary>
    /// Verifies imported generated value and entity types use their declaring schema namespace.
    /// </summary>
    [Test]
    public async Task Should_qualify_cross_schema_value_types()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/foundation.exp", FOUNDATION_SCHEMA),
            ("schemas/consumer.exp", VALUE_CONSUMER_SCHEMA));
        var holder = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Schemas.ValueConsumer.Holder")
            ?? throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(RequiredProperty(holder, "MeasuredValue").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.Schemas.ValueFoundation.LengthValue");
            await Assert.That(RequiredProperty(holder, "TargetRef").Type.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.Schemas.ValueFoundation.ITarget");
        }
    }

    /// <summary>
    /// Verifies companion value types and known symbols participate in atomic generated-name collision checks.
    /// </summary>
    [Test]
    public async Task Should_reject_generated_value_name_collisions_atomically()
    {
        var result = GeneratorHostTests.Run(("schemas/value-collision.exp", VALUE_COLLISION_SCHEMA));

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id)).Contains("STEP21EXP004");
            await Assert.That(result.GeneratedSources).IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies an extensible SELECT family includes aggregate alternatives after aggregate projection is available.
    /// </summary>
    [Test]
    public async Task Should_include_aggregate_select_extension_after_projection_is_available()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/deferred-select-extension.exp", DEFERRED_SELECT_EXTENSION_SCHEMA));
        var baseChoice = RequiredType(
            result.OutputCompilation,
            "TedToolkit.Step21.Schemas.DeferredSelectExtension.BaseChoice");

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(baseChoice.GetMembers().OfType<IMethodSymbol>().Select(method => method.Name))
                .Contains("FromIntegerList");
            await Assert.That(result.OutputCompilation.GetTypeByMetadataName(
                "TedToolkit.Step21.Schemas.DeferredSelectExtension.ExtendedChoice")).IsNotNull();
        }
    }

    private static INamedTypeSymbol RequiredType(Compilation compilation, string metadataName) =>
        compilation.GetTypeByMetadataName(metadataName)
        ?? throw new InvalidOperationException($"Generated type '{metadataName}' was not found.");

    private static IPropertySymbol RequiredProperty(INamedTypeSymbol type, string name) =>
        type.GetMembers(name).OfType<IPropertySymbol>().Single();

    private static System.Reflection.Assembly Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));

        return System.Reflection.Assembly.Load(stream.ToArray());
    }
}
