using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>
/// Proves generated execution of validation-reachable EXPRESS rules and their dependency closure.
/// </summary>
public sealed class ReachableRuleTests
{
    private const string ENTITY_WHERE_SCHEMA = """
        SCHEMA rule_model;
        ENTITY sample;
          amount : INTEGER;
        WHERE
          positive_amount : amount > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ENTITY_WHERE_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.RuleModel;

        internal static class RuleConsumer
        {
            internal static ValidationResult Validate(BigInteger amount)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-22T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["rule_model"])),
                    [TedToolkit.Step21.Generated.RuleModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("rule_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(amount));
                return structure.Validate();
            }
        }
        """;

    private const string REACHABLE_DEPENDENCY_SCHEMA = """
        SCHEMA dependency_model;
        CONSTANT
          exclusive_limit : INTEGER := 10;
        END_CONSTANT;
        FUNCTION scaled(input_value : INTEGER) : INTEGER;
          RETURN(input_value * 2);
        END_FUNCTION;
        ENTITY sample;
          amount : INTEGER;
          values : LIST [0:?] OF INTEGER;
        DERIVE
          computed : INTEGER := scaled(amount);
        WHERE
          below_limit : computed < exclusive_limit;
          contains_amount : SIZEOF(QUERY(candidate <* values | candidate = amount)) > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string REACHABLE_DEPENDENCY_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.DependencyModel;

        internal static class DependencyConsumer
        {
            internal static ValidationResult Validate(BigInteger amount, BigInteger listed)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-22T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["dependency_model"])),
                    [TedToolkit.Step21.Generated.DependencyModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("dependency_model"));
                structure.DataSections.Add(section);
                var values = new ExpressList<BigInteger>(0) { listed };
                _ = structure.Add(section, new Sample(amount, values));
                return structure.Validate();
            }
        }
        """;

    private const string TYPE_WHERE_SCHEMA = """
        SCHEMA type_rule_model;
        TYPE positive_integer = INTEGER;
        WHERE
          positive : SELF > 0;
        END_TYPE;
        ENTITY sample;
          amount : positive_integer;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string TYPE_WHERE_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.TypeRuleModel;

        internal static class TypeRuleConsumer
        {
            internal static ValidationResult Validate(BigInteger amount)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-22T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["type_rule_model"])),
                    [TedToolkit.Step21.Generated.TypeRuleModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("type_rule_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(new PositiveInteger(amount)));
                return structure.Validate();
            }
        }
        """;

    private const string UNIQUE_SCHEMA = """
        SCHEMA unique_rule_model;
        ENTITY sample;
          key_value : INTEGER;
        DERIVE
          normalized_key : INTEGER := key_value * key_value;
        UNIQUE
          unique_key : normalized_key;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string UNIQUE_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.UniqueRuleModel;

        internal static class UniqueRuleConsumer
        {
            internal static ValidationResult Validate(BigInteger first, BigInteger second)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-22T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["unique_rule_model"])),
                    [TedToolkit.Step21.Generated.UniqueRuleModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("unique_rule_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(first));
                _ = structure.Add(section, new Sample(second));
                return structure.Validate();
            }
        }
        """;

    private const string GLOBAL_RULE_SCHEMA = """
        SCHEMA global_rule_model;
        ENTITY sample;
          amount : INTEGER;
        END_ENTITY;
        RULE population_rule FOR (sample);
        WHERE
          nonempty : SIZEOF(sample) > 0;
          all_positive : SIZEOF(QUERY(candidate <* sample | candidate.amount <= 0)) = 0;
        END_RULE;
        END_SCHEMA;
        """;

    private const string GLOBAL_RULE_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.GlobalRuleModel;

        internal static class GlobalRuleConsumer
        {
            internal static ValidationResult Validate(bool add, BigInteger amount)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-22T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["global_rule_model"])),
                    [TedToolkit.Step21.Generated.GlobalRuleModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("global_rule_model"));
                structure.DataSections.Add(section);
                if (add)
                {
                    _ = structure.Add(section, new Sample(amount));
                }

                return structure.Validate();
            }
        }
        """;

    private const string UNREACHABLE_FUNCTION_SCHEMA = """
        SCHEMA unreachable_model;
        FUNCTION unused(input_value : INTEGER) : INTEGER;
          LOCAL
            result_value : INTEGER;
          END_LOCAL;
          result_value := input_value;
          RETURN(result_value);
        END_FUNCTION;
        TYPE unused_type = INTEGER;
        WHERE
          unused_rule : unused(SELF) > 0;
        END_TYPE;
        ENTITY sample;
          amount : INTEGER;
        WHERE
          positive : amount > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string UNIQUE_VALUE_SCHEMA = """
        SCHEMA unique_value_model;
        ENTITY aggregate_sample;
          key_values : LIST [0:?] OF INTEGER;
        UNIQUE
          aggregate_key : key_values;
        END_ENTITY;
        ENTITY bag_sample;
          key_values : BAG [0:?] OF INTEGER;
        UNIQUE
          bag_key : key_values;
        END_ENTITY;
        TYPE integer_list = LIST [0:?] OF INTEGER;
        END_TYPE;
        TYPE list_choice = SELECT (integer_list);
        END_TYPE;
        ENTITY select_sample;
          key_choice : list_choice;
        UNIQUE
          select_key : key_choice;
        END_ENTITY;
        ENTITY reference_sample;
          key_reference : aggregate_sample;
        UNIQUE
          reference_key : key_reference;
        END_ENTITY;
        ENTITY optional_sample;
          optional_key : OPTIONAL INTEGER;
        UNIQUE
          optional_key_rule : optional_key;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string UNIQUE_VALUE_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.UniqueValueModel;

        internal static class UniqueValueConsumer
        {
            internal static ValidationResult ValidateValueKeys()
            {
                var structure = CreateStructure(out var section);
                var target = new AggregateSample(new ExpressList<BigInteger> { 1, 2 });
                _ = structure.Add(section, target);
                _ = structure.Add(section, new AggregateSample(new ExpressList<BigInteger> { 1, 2 }));
                _ = structure.Add(section, new BagSample(new ExpressBag<BigInteger> { 1, 2, 1 }));
                _ = structure.Add(section, new BagSample(new ExpressBag<BigInteger> { 2, 1, 1 }));
                _ = structure.Add(section, new SelectSample(
                    ListChoice.FromIntegerList(new IntegerList(new ExpressList<BigInteger> { 1, 2 }))));
                _ = structure.Add(section, new SelectSample(
                    ListChoice.FromIntegerList(new IntegerList(new ExpressList<BigInteger> { 1, 2 }))));
                _ = structure.Add(section, new ReferenceSample(target));
                _ = structure.Add(section, new ReferenceSample(target));
                return structure.Validate();
            }

            internal static ValidationResult ValidateMissingOptionalKeys()
            {
                var structure = CreateStructure(out var section);
                _ = structure.Add(section, new OptionalSample());
                _ = structure.Add(section, new OptionalSample());
                return structure.Validate();
            }

            private static ExchangeStructure CreateStructure(out DataSection section)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-22T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["unique_value_model"])),
                    [TedToolkit.Step21.Generated.UniqueValueModel.SchemaDescriptor.Instance]);
                section = new DataSection(new SchemaName("unique_value_model"));
                structure.DataSections.Add(section);
                return structure;
            }
        }
        """;

    private const string REACHABLE_UNSUPPORTED_FUNCTION_SCHEMA = """
        SCHEMA unsupported_model;
        FUNCTION used(input_value : INTEGER) : INTEGER;
          LOCAL
            result_value : INTEGER;
          END_LOCAL;
          result_value := input_value;
          RETURN(result_value);
        END_FUNCTION;
        ENTITY sample;
          amount : INTEGER;
        WHERE
          positive : used(amount) > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string CYCLIC_DEPENDENCY_SCHEMA = """
        SCHEMA cyclic_model;
        CONSTANT
          first_value : INTEGER := second_value;
          second_value : INTEGER := first_value;
        END_CONSTANT;
        ENTITY sample;
          amount : INTEGER;
        WHERE
          below_cycle : amount < first_value;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string MODEL_CONTEXT_SCHEMA = """
        SCHEMA model_context_model;
        ENTITY sample;
          amount : INTEGER;
        WHERE
          has_type : SIZEOF(TYPEOF(SELF)) > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string IMPORTED_RULE_FOUNDATION_SCHEMA = """
        SCHEMA imported_rule_foundation;
        TYPE imported_positive = INTEGER;
        WHERE
          positive : SELF > 0;
        END_TYPE;
        END_SCHEMA;
        """;

    private const string IMPORTED_RULE_CONSUMER_SCHEMA = """
        SCHEMA imported_rule_consumer;
        USE FROM imported_rule_foundation (imported_positive);
        ENTITY sample;
          amount : imported_positive;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SYMBOLIC_BOUND_SCHEMA = """
        SCHEMA symbolic_bound_model;
        CONSTANT
          minimum_count : INTEGER := 2;
          maximum_count : INTEGER := 3;
        END_CONSTANT;
        ENTITY sample;
          values : LIST [minimum_count:maximum_count] OF INTEGER;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SYMBOLIC_BOUND_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SymbolicBoundModel;

        internal static class SymbolicBoundConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-22T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["symbolic_bound_model"])),
                    [TedToolkit.Step21.Generated.SymbolicBoundModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("symbolic_bound_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(new ExpressList<BigInteger>(0, 1)));
                return structure.Validate();
            }
        }
        """;

    private const string UNSUPPORTED_BOUND_SCHEMA = """
        SCHEMA expression_bound_model;
        CONSTANT
          minimum_count : INTEGER := 2;
        END_CONSTANT;
        ENTITY sample;
          values : LIST [minimum_count + 1:minimum_count ** 2] OF INTEGER;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string EXPRESSION_BOUND_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.ExpressionBoundModel;

        internal static class ExpressionBoundConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-22T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["expression_bound_model"])),
                    [TedToolkit.Step21.Generated.ExpressionBoundModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("expression_bound_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(new ExpressList<BigInteger>(0, 1) { 1, 2 }));
                return structure.Validate();
            }
        }
        """;

    private const string DYNAMIC_BOUND_SCHEMA = """
        SCHEMA dynamic_bound_model;
        FUNCTION minimum_count(dummy : INTEGER) : INTEGER;
          RETURN(dummy + 1);
        END_FUNCTION;
        ENTITY sample;
          values : LIST [minimum_count(1):?] OF INTEGER;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INHERITED_RULE_SCHEMA = """
        SCHEMA inherited_rule_model;
        ENTITY root ABSTRACT SUPERTYPE;
          amount : INTEGER;
        WHERE
          positive : amount > 0;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INHERITED_RULE_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.InheritedRuleModel;

        internal static class InheritedRuleConsumer
        {
            internal static ValidationResult Validate(BigInteger amount)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-22T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["inherited_rule_model"])),
                    [TedToolkit.Step21.Generated.InheritedRuleModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("inherited_rule_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Child(amount));
                return structure.Validate();
            }
        }
        """;

    /// <summary>
    /// Verifies a named entity WHERE rule executes for every candidate and reports its stable source-located identity.
    /// </summary>
    [Test]
    public async Task Should_execute_named_entity_where_rule_and_keep_valid_candidates_clean()
    {
        var result = GeneratorHostTests.Run(
            ENTITY_WHERE_CONSUMER,
            ("schemas/rule.exp", ENTITY_WHERE_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("RuleConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var invalid = (ValidationResult)validate.Invoke(null, [new System.Numerics.BigInteger(0)])!;
        var valid = (ValidationResult)validate.Invoke(null, [new System.Numerics.BigInteger(1)])!;
        var location = invalid.Failures.Single().SourceLocation
            ?? throw new InvalidOperationException("The generated rule failure has no source location.");

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(invalid.Failures).HasSingleItem();
            await Assert.That(invalid.Failures[0].Code)
                .IsEqualTo("RULE_MODEL.SAMPLE.WHERE.POSITIVE_AMOUNT");
            await Assert.That(invalid.Failures[0].Path).IsEqualTo("DataSections[0].#1");
            await Assert.That(location.FilePath).IsEqualTo("rule.exp");
            await Assert.That(location.Line).IsEqualTo(5);
            await Assert.That(location.Column).IsEqualTo(3);
            await Assert.That(valid.IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies entity rules execute their reachable derived, function, constant, and query dependency closure.
    /// </summary>
    [Test]
    public async Task Should_execute_reachable_derived_function_constant_and_query_dependencies()
    {
        var result = GeneratorHostTests.Run(
            REACHABLE_DEPENDENCY_CONSUMER,
            ("schemas/dependency.exp", REACHABLE_DEPENDENCY_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("DependencyConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var invalid = (ValidationResult)validate.Invoke(
            null,
            [new System.Numerics.BigInteger(5), new System.Numerics.BigInteger(4)])!;
        var valid = (ValidationResult)validate.Invoke(
            null,
            [new System.Numerics.BigInteger(4), new System.Numerics.BigInteger(4)])!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(invalid.Failures.Select(failure => failure.Code)).IsEquivalentTo(new[]
            {
                "DEPENDENCY_MODEL.SAMPLE.WHERE.BELOW_LIMIT",
                "DEPENDENCY_MODEL.SAMPLE.WHERE.CONTAINS_AMOUNT",
            });
            await Assert.That(valid.IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies a defined-type WHERE rule evaluates SELF at every generated entity value occurrence.
    /// </summary>
    [Test]
    public async Task Should_execute_defined_type_where_rule_at_the_value_path()
    {
        var result = GeneratorHostTests.Run(
            TYPE_WHERE_CONSUMER,
            ("schemas/type-rule.exp", TYPE_WHERE_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("TypeRuleConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var invalid = (ValidationResult)validate.Invoke(null, [new System.Numerics.BigInteger(0)])!;
        var valid = (ValidationResult)validate.Invoke(null, [new System.Numerics.BigInteger(1)])!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(invalid.Failures).HasSingleItem();
            await Assert.That(invalid.Failures[0].Code)
                .IsEqualTo("TYPE_RULE_MODEL.POSITIVE_INTEGER.WHERE.POSITIVE");
            await Assert.That(invalid.Failures[0].Path).IsEqualTo("DataSections[0].#1.Amount");
            await Assert.That(valid.IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies a named entity UNIQUE rule checks the complete population and reports every duplicate candidate path.
    /// </summary>
    [Test]
    public async Task Should_execute_named_unique_rule_over_the_entity_population()
    {
        var result = GeneratorHostTests.Run(
            UNIQUE_CONSUMER,
            ("schemas/unique-rule.exp", UNIQUE_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("UniqueRuleConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var invalid = (ValidationResult)validate.Invoke(
            null,
            [new System.Numerics.BigInteger(1), new System.Numerics.BigInteger(-1)])!;
        var valid = (ValidationResult)validate.Invoke(
            null,
            [new System.Numerics.BigInteger(1), new System.Numerics.BigInteger(2)])!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(invalid.Failures).HasSingleItem();
            await Assert.That(invalid.Failures[0].Code)
                .IsEqualTo("UNIQUE_RULE_MODEL.SAMPLE.UNIQUE.UNIQUE_KEY");
            await Assert.That(invalid.Failures[0].Path).IsEqualTo("DataSections[0].#2.NormalizedKey");
            await Assert.That(valid.IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies a global RULE receives typed entity populations and aggregates each violated WHERE rule.
    /// </summary>
    [Test]
    public async Task Should_execute_global_rule_over_typed_entity_populations()
    {
        var result = GeneratorHostTests.Run(
            GLOBAL_RULE_CONSUMER,
            ("schemas/global-rule.exp", GLOBAL_RULE_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("GlobalRuleConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var empty = (ValidationResult)validate.Invoke(
            null,
            [false, System.Numerics.BigInteger.Zero])!;
        var invalid = (ValidationResult)validate.Invoke(
            null,
            [true, System.Numerics.BigInteger.Zero])!;
        var valid = (ValidationResult)validate.Invoke(
            null,
            [true, System.Numerics.BigInteger.One])!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(empty.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(["GLOBAL_RULE_MODEL.RULE.POPULATION_RULE.WHERE.NONEMPTY"]);
            await Assert.That(invalid.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(["GLOBAL_RULE_MODEL.RULE.POPULATION_RULE.WHERE.ALL_POSITIVE"]);
            await Assert.That(valid.IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies unreachable executable declarations remain IR-only and add no generated invocation surface.
    /// </summary>
    [Test]
    public async Task Should_leave_unreachable_function_in_ir_without_generated_facade()
    {
        var result = GeneratorHostTests.Run(("schemas/unreachable.exp", UNREACHABLE_FUNCTION_SCHEMA));
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));
        var descriptor = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.UnreachableModel.SchemaDescriptor")
            ?? throw new InvalidOperationException("The generated descriptor was not found.");

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                .IsEmpty();
            await Assert.That(generated).DoesNotContain("__ExpressFunction_Unused");
            await Assert.That(generated).DoesNotContain("UNREACHABLE_MODEL.UNUSED_TYPE.WHERE.UNUSED_RULE");
            await Assert.That(descriptor.GetMembers().OfType<IMethodSymbol>()
                .Where(method => method.DeclaredAccessibility == Accessibility.Public)
                .Select(method => method.Name))
                .DoesNotContain("Unused");
        }
    }

    /// <summary>
    /// Verifies UNIQUE compares aggregate keys by EXPRESS value and excludes indeterminate optional keys.
    /// </summary>
    [Test]
    public async Task Should_apply_express_value_and_indeterminate_semantics_to_unique_keys()
    {
        var result = GeneratorHostTests.Run(
            UNIQUE_VALUE_CONSUMER,
            ("schemas/unique-value.exp", UNIQUE_VALUE_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("UniqueValueConsumer", throwOnError: true)!;
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var valueKeys = (ValidationResult)consumer.GetMethod("ValidateValueKeys", flags)!.Invoke(null, null)!;
        var optional = (ValidationResult)consumer.GetMethod("ValidateMissingOptionalKeys", flags)!.Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(valueKeys.Failures.Select(failure => failure.Code)).IsEquivalentTo(new[]
            {
                "UNIQUE_VALUE_MODEL.AGGREGATE_SAMPLE.UNIQUE.AGGREGATE_KEY",
                "UNIQUE_VALUE_MODEL.BAG_SAMPLE.UNIQUE.BAG_KEY",
                "UNIQUE_VALUE_MODEL.SELECT_SAMPLE.UNIQUE.SELECT_KEY",
                "UNIQUE_VALUE_MODEL.REFERENCE_SAMPLE.UNIQUE.REFERENCE_KEY",
            });
            await Assert.That(optional.IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies unsupported reachable algorithms/model context and dependency cycles stop generation with source evidence.
    /// </summary>
    [Test]
    public async Task Should_report_source_located_reachable_function_and_cycle_failures()
    {
        var unsupported = GeneratorHostTests.Run(
            ("schemas/unsupported.exp", REACHABLE_UNSUPPORTED_FUNCTION_SCHEMA));
        var cyclic = GeneratorHostTests.Run(("schemas/cyclic.exp", CYCLIC_DEPENDENCY_SCHEMA));
        var modelContext = GeneratorHostTests.Run(("schemas/model-context.exp", MODEL_CONTEXT_SCHEMA));
        var unsupportedDiagnostic = unsupported.Diagnostics.Single(diagnostic => diagnostic.Id == "STEP21EXP006");
        var cycleDiagnostic = cyclic.Diagnostics.Single(diagnostic => diagnostic.Id == "STEP21EXP006");
        var modelDiagnostic = modelContext.Diagnostics.Single(diagnostic => diagnostic.Id == "STEP21EXP006");

        using (Assert.Multiple())
        {
            await Assert.That(unsupported.GeneratedSources).IsEmpty();
            await Assert.That(unsupportedDiagnostic.GetMessage()).Contains("exactly one value RETURN");
            await Assert.That(unsupportedDiagnostic.Location.GetLineSpan().Path)
                .IsEqualTo("schemas/unsupported.exp");
            await Assert.That(unsupportedDiagnostic.Location.GetLineSpan().StartLinePosition.Line)
                .IsEqualTo(1);
            await Assert.That(cyclic.GeneratedSources).IsEmpty();
            await Assert.That(cycleDiagnostic.GetMessage()).Contains("dependency cycle");
            await Assert.That(cycleDiagnostic.Location.GetLineSpan().Path).IsEqualTo("schemas/cyclic.exp");
            await Assert.That(modelContext.GeneratedSources).IsEmpty();
            await Assert.That(modelDiagnostic.GetMessage()).Contains("model traversal callback");
            await Assert.That(modelDiagnostic.Location.GetLineSpan().Path)
                .IsEqualTo("schemas/model-context.exp");
        }
    }

    /// <summary>
    /// Verifies integer constants used by aggregate bounds become executable structural metadata and checks.
    /// </summary>
    [Test]
    public async Task Should_execute_symbolic_aggregate_bounds_resolved_from_constants()
    {
        var result = GeneratorHostTests.Run(
            SYMBOLIC_BOUND_CONSUMER,
            ("schemas/symbolic-bound.exp", SYMBOLIC_BOUND_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "SymbolicBoundConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.Failures.Select(failure => failure.Code)).IsEquivalentTo(new[]
            {
                "SYMBOLIC_BOUND_MODEL.SAMPLE.VALUES.AGGREGATE_0.SHAPE",
                "SYMBOLIC_BOUND_MODEL.SAMPLE.VALUES.AGGREGATE_0.LOWER_BOUND",
            });
        }
    }

    /// <summary>
    /// Verifies validation-reachable arithmetic constant bounds are statically evaluated and executed.
    /// </summary>
    [Test]
    public async Task Should_execute_static_arithmetic_aggregate_bound_expressions()
    {
        var result = GeneratorHostTests.Run(
            EXPRESSION_BOUND_CONSUMER,
            ("schemas/expression-bound.exp", UNSUPPORTED_BOUND_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType("ExpressionBoundConsumer", throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.Failures.Select(failure => failure.Code)).IsEquivalentTo(new[]
            {
                "EXPRESSION_BOUND_MODEL.SAMPLE.VALUES.AGGREGATE_0.SHAPE",
                "EXPRESSION_BOUND_MODEL.SAMPLE.VALUES.AGGREGATE_0.LOWER_BOUND",
            });
        }
    }

    /// <summary>
    /// Verifies a non-static validation boundary stops schema generation at the declaration source.
    /// </summary>
    [Test]
    public async Task Should_reject_non_static_aggregate_bounds_without_emitting_a_partial_schema()
    {
        var result = GeneratorHostTests.Run(("schemas/dynamic-bound.exp", DYNAMIC_BOUND_SCHEMA));
        var diagnostic = result.Diagnostics.SingleOrDefault(item => item.Id == "STEP21EXP006")
            ?? throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));

        using (Assert.Multiple())
        {
            await Assert.That(result.GeneratedSources).IsEmpty();
            await Assert.That(diagnostic.GetMessage()).Contains("aggregate bound");
            await Assert.That(diagnostic.Location.GetLineSpan().Path)
                .IsEqualTo("schemas/dynamic-bound.exp");
            await Assert.That(diagnostic.Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(5);
        }
    }

    /// <summary>
    /// Verifies a cross-schema executable dependency becomes a source diagnostic instead of a generator exception.
    /// </summary>
    [Test]
    public async Task Should_report_cross_schema_rule_dependencies_at_the_importing_source()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/foundation.exp", IMPORTED_RULE_FOUNDATION_SCHEMA),
            ("schemas/consumer.exp", IMPORTED_RULE_CONSUMER_SCHEMA));
        var diagnostic = result.Diagnostics.SingleOrDefault(item => item.Id == "STEP21EXP006")
            ?? throw new InvalidOperationException(
                string.Join(Environment.NewLine, result.Diagnostics) + Environment.NewLine + GeneratedSnapshot(result));

        using (Assert.Multiple())
        {
            await Assert.That(diagnostic.GetMessage()).Contains("cross-schema");
            await Assert.That(diagnostic.Location.GetLineSpan().Path).IsEqualTo("schemas/consumer.exp");
            await Assert.That(result.GeneratedSources.Select(source => source.HintName))
                .DoesNotContain("ImportedRuleConsumer.SchemaDescriptor.g.cs");
        }
    }

    /// <summary>
    /// Verifies a concrete subtype executes every inherited entity WHERE rule with the governing rule identity.
    /// </summary>
    [Test]
    public async Task Should_execute_inherited_entity_where_rules_for_subtype_instances()
    {
        var result = GeneratorHostTests.Run(
            INHERITED_RULE_CONSUMER,
            ("schemas/inherited-rule.exp", INHERITED_RULE_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("InheritedRuleConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var invalid = (ValidationResult)validate.Invoke(null, [System.Numerics.BigInteger.Zero])!;
        var valid = (ValidationResult)validate.Invoke(null, [System.Numerics.BigInteger.One])!;

        using (Assert.Multiple())
        {
            await Assert.That(invalid.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(["INHERITED_RULE_MODEL.ROOT.WHERE.POSITIVE"]);
            await Assert.That(valid.IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies every executable named or anonymous WHERE and UNIQUE code has deterministic equivalent XML documentation.
    /// </summary>
    [Test]
    public async Task Should_keep_rule_failure_ids_and_xml_documentation_in_deterministic_parity()
    {
        var cases = new[]
        {
            (SchemaName: "RuleModel", Source: ENTITY_WHERE_SCHEMA),
            (SchemaName: "DependencyModel", Source: REACHABLE_DEPENDENCY_SCHEMA),
            (SchemaName: "TypeRuleModel", Source: TYPE_WHERE_SCHEMA),
            (SchemaName: "UniqueRuleModel", Source: UNIQUE_SCHEMA),
            (SchemaName: "UniqueValueModel", Source: UNIQUE_VALUE_SCHEMA),
            (SchemaName: "GlobalRuleModel", Source: GLOBAL_RULE_SCHEMA),
        };
        foreach (var item in cases)
        {
            var result = GeneratorHostTests.Run(("C:/agent-a/schemas/rules.exp", item.Source));
            var descriptor = result.OutputCompilation.GetTypeByMetadataName(
                $"TedToolkit.Step21.Generated.{item.SchemaName}.SchemaDescriptor")
                ?? throw new InvalidOperationException($"Descriptor for {item.SchemaName} was not found.");
            var xml = descriptor.GetDocumentationCommentXml()
                ?? throw new InvalidOperationException($"Descriptor XML for {item.SchemaName} was not found.");
            var documented = XDocument.Parse(xml)
                .Descendants("term")
                .Select(term => term.Value.Trim())
                .Where(IsExecutableRuleCode)
                .ToHashSet(StringComparer.Ordinal);
            var emitted = Regex.Matches(
                    GeneratedSnapshot(result),
                    "ValidationFailure\\(\\s*\"(?<code>[A-Z0-9_.]+)\"")
                .Select(match => match.Groups["code"].Value)
                .Where(IsExecutableRuleCode)
                .ToHashSet(StringComparer.Ordinal);

            using (Assert.Multiple())
            {
                await Assert.That(documented).IsEquivalentTo(emitted);
                await Assert.That(xml).Contains("Normalized requirement:");
                await Assert.That(xml).Contains("Validation boundary:");
            }
        }

        var first = GeneratorHostTests.Run(("C:/agent-a/schemas/rules.exp", GLOBAL_RULE_SCHEMA));
        var second = GeneratorHostTests.Run(("D:/agent-b/schemas/rules.exp", GLOBAL_RULE_SCHEMA));
        await Assert.That(GeneratedSnapshot(first)).IsEqualTo(GeneratedSnapshot(second));
    }

    private static bool IsExecutableRuleCode(string value)
    {
        return value.Contains(".WHERE.", StringComparison.Ordinal)
            || value.Contains(".UNIQUE.", StringComparison.Ordinal);
    }

    private static string GeneratedSnapshot(GeneratorHostTests.GeneratorResult result)
    {
        return string.Join(
            "\n---\n",
            result.GeneratedSources
                .OrderBy(source => source.HintName, StringComparer.Ordinal)
                .Select(source => $"{source.HintName}\n{source.SourceText}"));
    }

    private static System.Reflection.Assembly Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));

        return System.Reflection.Assembly.Load(stream.ToArray());
    }
}