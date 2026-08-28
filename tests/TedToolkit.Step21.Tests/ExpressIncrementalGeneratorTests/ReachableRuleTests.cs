using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Analyzer.Express.Binding;
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
          positive : (SELF > 0);
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
        ENTITY unused_sample;
        END_ENTITY;
        RULE population_rule FOR (sample, unused_sample);
        WHERE
          nonempty : SIZEOF(sample) > 0;
          all_positive : SIZEOF(QUERY(candidate <* sample | candidate.amount <= 0)) = 0;
        END_RULE;
        RULE bounded_population_rule FOR (sample);
        WHERE
          below_limit : SIZEOF(QUERY(candidate <* sample | candidate.amount > 10)) = 0;
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

    private const string UNQUALIFIED_ENUMERATION_SCHEMA = """
        SCHEMA unqualified_enumeration_model;
        TYPE transition_code = ENUMERATION OF (continuous, discontinuous);
        END_TYPE;
        TYPE first_status = ENUMERATION OF (shared, first_only);
        END_TYPE;
        TYPE second_status = ENUMERATION OF (shared, second_only);
        END_TYPE;
        ENTITY sample;
          transition : transition_code;
          transitions : LIST [1:?] OF transition_code;
          first_value : first_status;
          second_value : second_status;
        DERIVE
          is_discontinuous : LOGICAL := transition = discontinuous;
        WHERE
          derived_value : is_discontinuous = (transition = discontinuous);
          query_value : SIZEOF(QUERY(temp <* transitions | temp = discontinuous)) = 0;
          qualified_values : (first_value = first_status.shared)
            AND (second_value = second_status.shared);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string UNQUALIFIED_ENUMERATION_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.UnqualifiedEnumerationModel;

        internal static class UnqualifiedEnumerationConsumer
        {
            internal static ValidationResult Validate(bool discontinuous)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["unqualified_enumeration_model"])),
                    [TedToolkit.Step21.Generated.UnqualifiedEnumerationModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("unqualified_enumeration_model"));
                structure.DataSections.Add(section);
                var transition = discontinuous
                    ? TransitionCode.Discontinuous
                    : TransitionCode.Continuous;
                _ = structure.Add(section, new Sample(
                    transition,
                    new ExpressList<TransitionCode>(1) { transition },
                    FirstStatus.Shared,
                    SecondStatus.Shared));
                return structure.Validate();
            }
        }
        """;

    private const string SCALAR_GENERIC_CHOOSE_SCHEMA = """
        SCHEMA scalar_generic_choose_model;
        ENTITY vertex;
          code : INTEGER;
        END_ENTITY;
        FUNCTION choose(flag : BOOLEAN;
                        left_value, right_value : GENERIC:item) : GENERIC:item;
          IF flag THEN
            RETURN(left_value);
          ELSE
            RETURN(right_value);
          END_IF;
        END_FUNCTION;
        ENTITY sample;
          actual : vertex;
        DERIVE
          direct_value : vertex := choose(TRUE, actual, actual);
        WHERE
          direct_matches : direct_value :=: actual;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SCALAR_GENERIC_CHOOSE_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.ScalarGenericChooseModel;

        internal static class ScalarGenericChooseConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["scalar_generic_choose_model"])),
                    [TedToolkit.Step21.Generated.ScalarGenericChooseModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("scalar_generic_choose_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(new Vertex(BigInteger.One)));
                return structure.Validate();
            }
        }
        """;

    private const string SELECT_MEMBERSHIP_SCHEMA = """
        SCHEMA select_membership_model;
        ENTITY marker;
          code : STRING;
        END_ENTITY;
        ENTITY other;
          code : STRING;
        END_ENTITY;
        TYPE marker_choice = SELECT (marker, other);
        END_TYPE;
        FUNCTION classify(logical_value : LOGICAL) : INTEGER;
          IF logical_value THEN
            RETURN(1);
          END_IF;
          IF NOT logical_value THEN
            RETURN(0);
          END_IF;
          RETURN(-1);
        END_FUNCTION;
        ENTITY sample;
          candidate : OPTIONAL marker;
          list_values : LIST [1:1] OF marker_choice;
          set_values : SET [1:1] OF marker_choice;
          bag_values : BAG [1:1] OF marker_choice;
          expected_result : INTEGER;
        WHERE
          list_result : classify(candidate IN list_values) = expected_result;
          set_result : classify(candidate IN set_values) = expected_result;
          bag_result : classify(candidate IN bag_values) = expected_result;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELECT_MEMBERSHIP_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SelectMembershipModel;

        internal static class SelectMembershipConsumer
        {
            internal static ValidationResult Validate(int mode)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["select_membership_model"])),
                    [TedToolkit.Step21.Generated.SelectMembershipModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("select_membership_model"));
                structure.DataSections.Add(section);
                var candidate = mode == 3 ? null : new Marker("same");
                var selected = mode switch
                {
                    0 => MarkerChoice.FromMarker(new Marker("same")),
                    1 => MarkerChoice.FromMarker(new Marker("different")),
                    2 => MarkerChoice.FromOther(new Other("same")),
                    _ => MarkerChoice.FromOther(new Other("different")),
                };
                var expected = mode == 0 ? BigInteger.One : mode == 3 ? -BigInteger.One : BigInteger.Zero;
                var sample = new Sample(
                    new ExpressList<MarkerChoice>(1, 1) { selected },
                    new ExpressSet<MarkerChoice>(1, 1) { selected },
                    new ExpressBag<MarkerChoice>(1, 1) { selected },
                    expected)
                {
                    Candidate = candidate,
                };
                _ = structure.Add(section, sample);
                return structure.Validate();
            }
        }
        """;

    private const string SELECT_TYPEOF_SCHEMA = """
        SCHEMA select_typeof_model;
        TYPE scalar_base = REAL;
        END_TYPE;
        TYPE scalar_leaf = scalar_base;
        END_TYPE;
        TYPE integer_leaf = INTEGER;
        END_TYPE;
        TYPE boolean_leaf = BOOLEAN;
        END_TYPE;
        ENTITY base_entity;
        END_ENTITY;
        ENTITY leaf_entity SUBTYPE OF (base_entity);
        END_ENTITY;
        TYPE inner_choice = SELECT (leaf_entity, scalar_leaf, integer_leaf, boolean_leaf);
        END_TYPE;
        TYPE outer_choice = SELECT (inner_choice);
        END_TYPE;
        ENTITY entity_sample;
          value_component : outer_choice;
        WHERE
          dynamic_leaf : 'SELECT_TYPEOF_MODEL.LEAF_ENTITY' IN TYPEOF(value_component);
          entity_supertype : 'SELECT_TYPEOF_MODEL.BASE_ENTITY' IN TYPEOF(value_component);
          no_outer_select : NOT ('SELECT_TYPEOF_MODEL.OUTER_CHOICE' IN TYPEOF(value_component));
          no_inner_select : NOT ('SELECT_TYPEOF_MODEL.INNER_CHOICE' IN TYPEOF(value_component));
        END_ENTITY;
        ENTITY scalar_sample;
          value_component : outer_choice;
        WHERE
          scalar_leaf_type : 'SELECT_TYPEOF_MODEL.SCALAR_LEAF' IN TYPEOF(value_component);
          scalar_base_type : 'SELECT_TYPEOF_MODEL.SCALAR_BASE' IN TYPEOF(value_component);
          real_type : 'REAL' IN TYPEOF(value_component);
          number_type : 'NUMBER' IN TYPEOF(value_component);
          no_outer_select : NOT ('SELECT_TYPEOF_MODEL.OUTER_CHOICE' IN TYPEOF(value_component));
          no_inner_select : NOT ('SELECT_TYPEOF_MODEL.INNER_CHOICE' IN TYPEOF(value_component));
        END_ENTITY;
        ENTITY integer_sample;
          value_component : outer_choice;
        WHERE
          integer_type : 'INTEGER' IN TYPEOF(value_component);
          number_type : 'NUMBER' IN TYPEOF(value_component);
        END_ENTITY;
        ENTITY boolean_sample;
          value_component : outer_choice;
        WHERE
          boolean_type : 'BOOLEAN' IN TYPEOF(value_component);
          logical_type : 'LOGICAL' IN TYPEOF(value_component);
        END_ENTITY;
        ENTITY optional_sample;
          value_component : OPTIONAL outer_choice;
        WHERE
          missing_is_indeterminate : NOT EXISTS(TYPEOF(value_component));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELECT_TYPEOF_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SelectTypeofModel;

        internal static class SelectTypeofConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["select_typeof_model"])),
                    [TedToolkit.Step21.Generated.SelectTypeofModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("select_typeof_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new EntitySample(
                    OuterChoice.FromInnerChoice(InnerChoice.FromLeafEntity(new LeafEntity()))));
                _ = structure.Add(section, new ScalarSample(
                    OuterChoice.FromInnerChoice(InnerChoice.FromScalarLeaf(
                        new ScalarLeaf(new ScalarBase(new RealValue(BigInteger.One, BigInteger.Zero)))))));
                _ = structure.Add(section, new IntegerSample(
                    OuterChoice.FromInnerChoice(InnerChoice.FromIntegerLeaf(new IntegerLeaf(BigInteger.One)))));
                _ = structure.Add(section, new BooleanSample(
                    OuterChoice.FromInnerChoice(InnerChoice.FromBooleanLeaf(new BooleanLeaf(true)))));
                _ = structure.Add(section, new OptionalSample());
                return structure.Validate();
            }
        }
        """;

    private const string DIMENSIONAL_SELECT_SCHEMA = """
        SCHEMA dimensional_select_model;
        TYPE length_measure = REAL;
        END_TYPE;
        TYPE measure_value = SELECT (length_measure);
        END_TYPE;
        TYPE unit = SELECT (named_unit);
        END_TYPE;
        TYPE si_unit_name = ENUMERATION OF (metre, radian);
        END_TYPE;
        ENTITY dimensional_exponents;
          length_exponent : REAL;
          mass_exponent : REAL;
          time_exponent : REAL;
          electric_current_exponent : REAL;
          thermodynamic_temperature_exponent : REAL;
          amount_of_substance_exponent : REAL;
          luminous_intensity_exponent : REAL;
        END_ENTITY;
        ENTITY named_unit
          SUPERTYPE OF (ONEOF (si_unit) ANDOR ONEOF (length_unit));
          dimensions : dimensional_exponents;
        END_ENTITY;
        ENTITY length_unit SUBTYPE OF (named_unit);
        END_ENTITY;
        ENTITY si_unit SUBTYPE OF (named_unit);
          name : si_unit_name;
        DERIVE
          SELF\named_unit.dimensions : dimensional_exponents := dimensions_for_si_unit(SELF.name);
        END_ENTITY;
        ENTITY measure_with_unit;
          value_component : measure_value;
          unit_component : unit;
        WHERE
          typed_value : 'DIMENSIONAL_SELECT_MODEL.LENGTH_MEASURE' IN TYPEOF(value_component);
          derived_dimensions : derive_dimensional_exponents(unit_component) =
            dimensional_exponents(1,0,0,0,0,0,0);
          valid_combination : valid_units(SELF);
        END_ENTITY;
        FUNCTION dimensions_for_si_unit(name : si_unit_name) : dimensional_exponents;
          CASE name OF
            metre : RETURN(dimensional_exponents(1,0,0,0,0,0,0));
            radian : RETURN(dimensional_exponents(0,0,0,0,0,0,0));
          END_CASE;
        END_FUNCTION;
        FUNCTION derive_dimensional_exponents(x : unit) : dimensional_exponents;
          LOCAL
            result : dimensional_exponents := dimensional_exponents(0,0,0,0,0,0,0);
          END_LOCAL;
          result := x.dimensions;
          RETURN(result);
        END_FUNCTION;
        FUNCTION valid_units(m : measure_with_unit) : BOOLEAN;
          IF 'DIMENSIONAL_SELECT_MODEL.LENGTH_MEASURE' IN TYPEOF(m.value_component) THEN
            IF derive_dimensional_exponents(m.unit_component) <>
              dimensional_exponents(1,0,0,0,0,0,0) THEN
              RETURN(FALSE);
            END_IF;
          END_IF;
          RETURN(TRUE);
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string DIMENSIONAL_SELECT_CONSUMER = """"
        using System.IO;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.DimensionalSelectModel;

        internal static class DimensionalSelectConsumer
        {
            internal static ValidationResult Validate()
            {
                const string sourceText = """
                    ISO-10303-21;
                    HEADER;
                    FILE_DESCRIPTION(('dimensional select'),'2;1');
                    FILE_NAME('dimensional-select.step','2026-08-26T00:00:00',(),(),'tests','tests','');
                    FILE_SCHEMA(('DIMENSIONAL_SELECT_MODEL'));
                    ENDSEC;
                    DATA;
                    #1 = (LENGTH_UNIT() NAMED_UNIT(*) SI_UNIT(.METRE.));
                    #2 = MEASURE_WITH_UNIT(LENGTH_MEASURE(1.E-07),#1);
                    ENDSEC;
                    END-ISO-10303-21;
                    """;
                try
                {
                    using var source = new StringReader(sourceText);
                    var structure = ExchangeStructure.Read(
                        source,
                        [TedToolkit.Step21.Generated.DimensionalSelectModel.SchemaDescriptor.Instance]);
                    return structure.Validate();
                }
                catch (ExchangeStructureReadValidationException exception)
                {
                    return exception.ValidationResult;
                }
            }
        }
        """";

    private const string AGGREGATE_TYPEOF_SCHEMA = """
        SCHEMA aggregate_typeof_model;
        TYPE integer_set = SET OF INTEGER;
        END_TYPE;
        FUNCTION maybe_set(present : BOOLEAN) : SET OF INTEGER;
          IF present THEN
            RETURN([1]);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        ENTITY sample;
          array_values : ARRAY [1:2] OF OPTIONAL INTEGER;
          bag_values : BAG OF INTEGER;
          list_values : LIST OF INTEGER;
          set_values : SET OF INTEGER;
          alias_values : integer_set;
        WHERE
          array_kind : ('ARRAY' IN TYPEOF(array_values)) AND NOT ('INTEGER' IN TYPEOF(array_values));
          bag_kind : ('BAG' IN TYPEOF(bag_values)) AND NOT ('INTEGER' IN TYPEOF(bag_values));
          list_kind : ('LIST' IN TYPEOF(list_values)) AND NOT ('INTEGER' IN TYPEOF(list_values));
          set_kind : ('SET' IN TYPEOF(set_values)) AND NOT ('INTEGER' IN TYPEOF(set_values));
          alias_is_only_set : ('SET' IN TYPEOF(alias_values)) AND
            NOT ('AGGREGATE_TYPEOF_MODEL.INTEGER_SET' IN TYPEOF(alias_values));
          present_aggregate : 'SET' IN TYPEOF(maybe_set(TRUE));
          unknown_aggregate : NOT EXISTS(TYPEOF(maybe_set(FALSE)));
          entity_regression : 'AGGREGATE_TYPEOF_MODEL.SAMPLE' IN TYPEOF(SELF);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string AGGREGATE_TYPEOF_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.AggregateTypeofModel;

        internal static class AggregateTypeofConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["aggregate_typeof_model"])),
                    [TedToolkit.Step21.Generated.AggregateTypeofModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("aggregate_typeof_model"));
                structure.DataSections.Add(section);
                for (var mode = 0; mode < 2; mode++)
                {
                    var array = new ExpressArray<BigInteger>(1, 2, isOptional: true);
                    var bag = new ExpressBag<BigInteger>(0);
                    var list = new ExpressList<BigInteger>(0);
                    var set = new ExpressSet<BigInteger>(0);
                    var alias = new ExpressSet<BigInteger>(0);
                    if (mode == 1)
                    {
                        array[1] = BigInteger.One;
                        bag.Add(BigInteger.One);
                        list.Add(BigInteger.One);
                        set.Add(BigInteger.One);
                        alias.Add(BigInteger.One);
                    }

                    _ = structure.Add(section, new Sample(array, bag, list, set, new IntegerSet(alias)));
                }

                return structure.Validate();
            }
        }
        """;

    private const string BUILTIN_SHADOWING_SCHEMA = """
        SCHEMA builtin_shadowing_model;
        ENTITY holder;
          items : SET [1:?] OF INTEGER;
        END_ENTITY;
        FUNCTION item_count(e : holder) : INTEGER;
          RETURN(SIZEOF(e.items) + SIZEOF(QUERY(item <* e.items | item > 0)));
        END_FUNCTION;
        ENTITY sample;
          candidate : holder;
        WHERE
          parameter_and_query_shadow : item_count(candidate) = 4;
          unshadowed_constants : (PI + PI > 6.2) AND (PI + PI < 6.4);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string BUILTIN_SHADOWING_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.BuiltinShadowingModel;

        internal static class BuiltinShadowingConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["builtin_shadowing_model"])),
                    [TedToolkit.Step21.Generated.BuiltinShadowingModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("builtin_shadowing_model"));
                structure.DataSections.Add(section);
                var holder = new Holder(new ExpressSet<BigInteger>(1)
                {
                    BigInteger.One,
                    new BigInteger(2),
                });
                _ = structure.Add(section, holder);
                _ = structure.Add(section, new Sample(holder));
                return structure.Validate();
            }
        }
        """;

    private const string SELECT_ATTRIBUTE_QUALIFIER_SCHEMA = """
        SCHEMA select_attribute_qualifier_model;
        ENTITY surface_value;
          code : STRING;
        END_ENTITY;
        ENTITY pcurve_value;
          basis_surface : surface_value;
        END_ENTITY;
        TYPE pcurve_or_surface = SELECT (pcurve_value, surface_value);
        END_TYPE;
        ENTITY direction_value;
          direction_ratios : LIST [1:2] OF REAL;
        END_ENTITY;
        ENTITY vector_value;
          orientation : direction_value;
          magnitude : REAL;
        END_ENTITY;
        TYPE vector_or_direction = SELECT (vector_value, direction_value);
        END_TYPE;
        ENTITY unit_value;
          dimensions : INTEGER;
        END_ENTITY;
        TYPE unit = SELECT (unit_value);
        END_TYPE;
        ENTITY named_value;
          name : STRING;
        END_ENTITY;
        ENTITY first_named_value SUBTYPE OF (named_value);
        END_ENTITY;
        ENTITY second_named_value SUBTYPE OF (named_value);
        END_ENTITY;
        TYPE named_choice = SELECT (first_named_value, second_named_value);
        END_TYPE;
        FUNCTION guarded_basis(item : pcurve_or_surface) : surface_value;
          IF 'SELECT_ATTRIBUTE_QUALIFIER_MODEL.PCURVE_VALUE' IN TYPEOF(item) THEN
            RETURN(item.basis_surface);
          ELSE
            RETURN(?);
          END_IF;
        END_FUNCTION;
        FUNCTION guarded_vector_measure(item : vector_or_direction) : REAL;
          IF 'SELECT_ATTRIBUTE_QUALIFIER_MODEL.VECTOR_VALUE' IN TYPEOF(item) THEN
            RETURN(item.orientation.direction_ratios[1] + item.magnitude);
          ELSE
            RETURN(item.direction_ratios[1]);
          END_IF;
        END_FUNCTION;
        FUNCTION unit_dimension(item : unit) : INTEGER;
          RETURN(item.dimensions);
        END_FUNCTION;
        FUNCTION unguarded_basis(item : pcurve_or_surface) : surface_value;
          RETURN(item.basis_surface);
        END_FUNCTION;
        FUNCTION passthrough(item : vector_or_direction) : vector_or_direction;
          RETURN(item);
        END_FUNCTION;
        FUNCTION unguarded_call_measure(item : vector_or_direction) : REAL;
          RETURN(passthrough(item).direction_ratios[1]);
        END_FUNCTION;
        FUNCTION common_name(item : named_choice) : STRING;
          RETURN(item.name);
        END_FUNCTION;
        ENTITY guarded_sample;
          curve_choice : pcurve_or_surface;
          vector_choice : vector_or_direction;
          expected_basis : surface_value;
        WHERE
          basis_rule : guarded_basis(curve_choice) = expected_basis;
          vector_rule : guarded_vector_measure(vector_choice) = 5.0;
        END_ENTITY;
        ENTITY direction_sample;
          vector_choice : vector_or_direction;
        WHERE
          direction_rule : guarded_vector_measure(vector_choice) = 2.0;
        END_ENTITY;
        ENTITY unit_sample;
          unit_choice : unit;
        WHERE
          unit_rule : unit_dimension(unit_choice) = 3;
        END_ENTITY;
        ENTITY wrong_alternative_sample;
          surface_choice : pcurve_or_surface;
        WHERE
          wrong_alternative_is_unknown : NOT EXISTS(unguarded_basis(surface_choice));
        END_ENTITY;
        ENTITY wrong_call_sample;
          vector_choice : vector_or_direction;
        WHERE
          unguarded_call_is_unknown : NOT EXISTS(unguarded_call_measure(vector_choice));
        END_ENTITY;
        ENTITY named_sample;
          selected : named_choice;
        WHERE
          common_attribute : LENGTH(common_name(selected)) > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string FUNCTION_SELECT_ARGUMENT_SCHEMA = """
        SCHEMA function_select_argument_model;
        ENTITY base_item;
          code : INTEGER;
        END_ENTITY;
        ENTITY child_item SUBTYPE OF (base_item);
          child_code : INTEGER;
        END_ENTITY;
        ENTITY other_item;
          code : INTEGER;
        END_ENTITY;
        TYPE item_choice = SELECT (base_item, other_item);
        END_TYPE;
        FUNCTION accepts(item : item_choice) : BOOLEAN;
          RETURN(EXISTS(item));
        END_FUNCTION;
        FUNCTION forwards(item : item_choice) : BOOLEAN;
          RETURN(accepts(item));
        END_FUNCTION;
        ENTITY sample;
          direct_item : base_item;
          inherited_item : child_item;
          selected_item : item_choice;
        WHERE
          direct_entity : accepts(direct_item);
          inherited_entity : accepts(inherited_item);
          same_select : forwards(selected_item);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string FUNCTION_SELECT_ARGUMENT_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.FunctionSelectArgumentModel;

        internal static class FunctionSelectArgumentConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["function_select_argument_model"])),
                    [TedToolkit.Step21.Generated.FunctionSelectArgumentModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("function_select_argument_model"));
                structure.DataSections.Add(section);
                var direct = new BaseItem(BigInteger.One);
                var inherited = new ChildItem(new BigInteger(2), new BigInteger(3));
                var selected = ItemChoice.FromOtherItem(new OtherItem(new BigInteger(4)));
                _ = structure.Add(section, direct);
                _ = structure.Add(section, inherited);
                _ = structure.Add(section, new Sample(direct, inherited, selected));
                return structure.Validate();
            }
        }
        """;

    private const string TYPEOF_GUARDED_SELECT_PATH_ARGUMENT_SCHEMA = """
        SCHEMA typeof_guarded_select_path_argument_model;
        ENTITY property_definition;
          name : STRING;
        END_ENTITY;
        ENTITY other_definition;
        END_ENTITY;
        TYPE represented_definition = SELECT (property_definition, other_definition);
        END_TYPE;
        ENTITY representation;
        END_ENTITY;
        ENTITY property_definition_representation;
          definition : represented_definition;
          used_representation : representation;
        END_ENTITY;
        FUNCTION correlates(pd : property_definition) : BOOLEAN;
          RETURN(EXISTS(pd));
        END_FUNCTION;
        FUNCTION unknown_comparison(pd : property_definition) : LOGICAL;
          RETURN(pd = ?);
        END_FUNCTION;
        RULE restrict_representation FOR (property_definition_representation);
        WHERE
          wr1 : SIZEOF(QUERY(pdr <* property_definition_representation |
            ('TYPEOF_GUARDED_SELECT_PATH_ARGUMENT_MODEL.PROPERTY_DEFINITION'
              IN TYPEOF(pdr.definition))
            AND correlates(pdr.definition)
            AND EXISTS(unknown_comparison(pdr.definition)))) = 0;
        END_RULE;
        END_SCHEMA;
        """;

    private const string INCOMPATIBLE_FUNCTION_SELECT_ARGUMENT_SCHEMA = """
        SCHEMA incompatible_function_select_argument_model;
        ENTITY accepted;
        END_ENTITY;
        ENTITY rejected;
        END_ENTITY;
        TYPE accepted_choice = SELECT (accepted);
        END_TYPE;
        FUNCTION accepts(item : accepted_choice) : BOOLEAN;
          RETURN(EXISTS(item));
        END_FUNCTION;
        ENTITY sample;
          value : rejected;
        WHERE
          invalid_call : accepts(value);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string AMBIGUOUS_FUNCTION_SELECT_ARGUMENT_SCHEMA = """
        SCHEMA ambiguous_function_select_argument_model;
        ENTITY base_item;
        END_ENTITY;
        ENTITY child_item SUBTYPE OF (base_item);
        END_ENTITY;
        TYPE item_choice = SELECT (base_item, child_item);
        END_TYPE;
        FUNCTION accepts(item : item_choice) : BOOLEAN;
          RETURN(EXISTS(item));
        END_FUNCTION;
        ENTITY sample;
          value : child_item;
        WHERE
          ambiguous_call : accepts(value);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string DYNAMIC_ENTITY_APPLICATION_SCHEMA = """
        SCHEMA dynamic_entity_application_model;
        ENTITY carrier;
          code : INTEGER;
        END_ENTITY;
        ENTITY accepted SUBTYPE OF (carrier);
        END_ENTITY;
        ENTITY accepted_child SUBTYPE OF (accepted);
        END_ENTITY;
        ENTITY alternate_item SUBTYPE OF (carrier);
        END_ENTITY;
        ENTITY rejected SUBTYPE OF (carrier);
        END_ENTITY;
        TYPE carrier_choice = SELECT (accepted, rejected);
        END_TYPE;
        TYPE accepted_choice = SELECT (accepted, alternate_item);
        END_TYPE;
        TYPE nested_choice = SELECT (accepted_choice);
        END_TYPE;
        FUNCTION accepts_entity(item : accepted) : BOOLEAN;
          RETURN(item.code > 0);
        END_FUNCTION;
        FUNCTION accepts_choice(item : nested_choice) : BOOLEAN;
          RETURN(EXISTS(item));
        END_FUNCTION;
        FUNCTION forwards_entity(item : carrier) : BOOLEAN;
          RETURN(accepts_entity(item));
        END_FUNCTION;
        FUNCTION proven_entity(item : carrier) : BOOLEAN;
          IF 'DYNAMIC_ENTITY_APPLICATION_MODEL.ACCEPTED' IN TYPEOF(item) THEN
            RETURN(accepts_entity(item));
          END_IF;
          RETURN(TRUE);
        END_FUNCTION;
        FUNCTION proven_selected_entity(item : carrier_choice) : BOOLEAN;
          IF 'DYNAMIC_ENTITY_APPLICATION_MODEL.ACCEPTED' IN TYPEOF(item) THEN
            RETURN(accepts_entity(item));
          END_IF;
          RETURN(TRUE);
        END_FUNCTION;
        FUNCTION maybe_carrier(expose : BOOLEAN; item : carrier) : carrier;
          IF expose THEN RETURN(item); END_IF;
          RETURN(?);
        END_FUNCTION;
        ENTITY sample;
          accepted_value : carrier;
          child_value : carrier;
          alternate_value : carrier;
          rejected_value : carrier;
          selected_value : carrier_choice;
        WHERE
          exact_entity : accepts_entity(accepted_value);
          inherited_entity : accepts_entity(child_value);
          wrong_entity_is_unknown : NOT EXISTS(accepts_entity(rejected_value));
          caller_propagates_unknown : NOT EXISTS(forwards_entity(rejected_value));
          nested_select_first : accepts_choice(accepted_value);
          nested_select_second : accepts_choice(alternate_value);
          wrong_select_is_unknown : NOT EXISTS(accepts_choice(rejected_value));
          absent_actual_is_unknown : NOT EXISTS(accepts_entity(
            maybe_carrier(FALSE,accepted_value)));
          proven_path_control : proven_entity(accepted_value);
          proven_select_path_control : proven_selected_entity(selected_value);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string DYNAMIC_ENTITY_APPLICATION_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.DynamicEntityApplicationModel;

        internal static class DynamicEntityApplicationConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["dynamic applications"], "3;1"),
                        new FileName("dynamic-applications.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["dynamic_entity_application_model"])),
                    [TedToolkit.Step21.Generated.DynamicEntityApplicationModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("dynamic_entity_application_model"));
                structure.DataSections.Add(section);
                var accepted = new Accepted(BigInteger.One);
                var child = new AcceptedChild(new BigInteger(2));
                var alternate = new AlternateItem(new BigInteger(3));
                var rejected = new Rejected(new BigInteger(4));
                _ = structure.Add(section, accepted);
                _ = structure.Add(section, child);
                _ = structure.Add(section, alternate);
                _ = structure.Add(section, rejected);
                var selected = CarrierChoice.FromAccepted(accepted);
                _ = structure.Add(section, new Sample(accepted, child, alternate, rejected, selected));
                return structure.Validate();
            }
        }
        """;

    private const string INCOMPATIBLE_DYNAMIC_ENTITY_APPLICATION_SCHEMA = """
        SCHEMA incompatible_dynamic_entity_application_model;
        ENTITY accepted;
        END_ENTITY;
        ENTITY rejected;
        END_ENTITY;
        FUNCTION accepts(item : accepted) : BOOLEAN;
          RETURN(TRUE);
        END_FUNCTION;
        ENTITY sample;
          value : rejected;
        WHERE
          incompatible : accepts(value);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SET_ENTITY_ASSIGNMENT_SCHEMA = """
        SCHEMA set_entity_assignment_model;
        ENTITY anchor;
        END_ENTITY;
        ENTITY root;
          link : anchor;
        END_ENTITY;
        ENTITY target SUBTYPE OF (root);
        END_ENTITY;
        ENTITY child SUBTYPE OF (target);
        END_ENTITY;
        ENTITY sibling SUBTYPE OF (root);
        END_ENTITY;
        FUNCTION bag_to_set(values : BAG OF GENERIC:t) : SET OF GENERIC:t;
          LOCAL result : SET OF GENERIC:t := []; END_LOCAL;
          IF SIZEOF(values) > 0 THEN
            REPEAT index := 1 TO HIINDEX(values) BY 1;
              result := result + values[index];
            END_REPEAT;
          END_IF;
          RETURN(result);
        END_FUNCTION;
        FUNCTION generic_targets(item : anchor; role : STRING) : SET OF target;
          LOCAL result : SET OF target; END_LOCAL;
          result := bag_to_set(USEDIN(item,role));
          RETURN(result);
        END_FUNCTION;
        FUNCTION base_targets(values : SET OF root) : SET OF target;
          LOCAL result : SET OF target; END_LOCAL;
          result := values;
          RETURN(result);
        END_FUNCTION;
        FUNCTION same_targets(values : SET OF target) : SET OF target;
          LOCAL result : SET OF target; END_LOCAL;
          result := values;
          RETURN(result);
        END_FUNCTION;
        FUNCTION unknown_roots(expose : BOOLEAN) : SET OF root;
          IF expose THEN RETURN([]); END_IF;
          RETURN(?);
        END_FUNCTION;
        ENTITY sample;
          matching_anchor : anchor;
          wrong_anchor : anchor;
          matching_roots : SET [1:?] OF root;
          wrong_roots : SET [1:?] OF root;
          typed_targets : SET [1:?] OF target;
        WHERE
          generic_matches : SIZEOF(generic_targets(
            matching_anchor,'SET_ENTITY_ASSIGNMENT_MODEL.ROOT.LINK')) = 2;
          generic_wrong_is_unknown : NOT EXISTS(generic_targets(
            wrong_anchor,'SET_ENTITY_ASSIGNMENT_MODEL.ROOT.LINK'));
          base_matches : SIZEOF(base_targets(matching_roots)) = 2;
          base_wrong_is_unknown : NOT EXISTS(base_targets(wrong_roots));
          unknown_source : NOT EXISTS(base_targets(unknown_roots(FALSE)));
          same_type : SIZEOF(same_targets(typed_targets)) = 2;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SET_ENTITY_ASSIGNMENT_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SetEntityAssignmentModel;

        internal static class SetEntityAssignmentConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["set entity assignment"], "3;1"),
                        new FileName("set-entity-assignment.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["set_entity_assignment_model"])),
                    [TedToolkit.Step21.Generated.SetEntityAssignmentModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("set_entity_assignment_model"));
                structure.DataSections.Add(section);
                var matchingAnchor = new Anchor();
                var wrongAnchor = new Anchor();
                var target = new Target(matchingAnchor);
                var child = new Child(matchingAnchor);
                var sibling = new Sibling(wrongAnchor);
                var matchingRoots = new ExpressSet<IRoot>(1) { target, child };
                var wrongRoots = new ExpressSet<IRoot>(1) { sibling };
                var typedTargets = new ExpressSet<ITarget>(1) { target, child };
                _ = structure.Add(section, matchingAnchor);
                _ = structure.Add(section, wrongAnchor);
                _ = structure.Add(section, target);
                _ = structure.Add(section, child);
                _ = structure.Add(section, sibling);
                _ = structure.Add(section, new Sample(
                    matchingAnchor,
                    wrongAnchor,
                    matchingRoots,
                    wrongRoots,
                    typedTargets));
                return structure.Validate();
            }
        }
        """;

    private const string SET_ENTITY_ASSIGNMENT_CONTROLS = """
        SCHEMA set_entity_assignment_controls;
        ENTITY root;
        END_ENTITY;
        ENTITY target SUBTYPE OF (root);
        END_ENTITY;
        ENTITY unrelated;
        END_ENTITY;
        FUNCTION bag_control(values : BAG OF root) : BAG OF target;
          LOCAL result : BAG OF target; END_LOCAL;
          result := values;
          RETURN(result);
        END_FUNCTION;
        FUNCTION list_control(values : LIST OF root) : LIST OF target;
          LOCAL result : LIST OF target; END_LOCAL;
          result := values;
          RETURN(result);
        END_FUNCTION;
        FUNCTION array_control(values : ARRAY [1:1] OF root) : ARRAY [1:1] OF target;
          LOCAL result : ARRAY [1:1] OF target; END_LOCAL;
          result := values;
          RETURN(result);
        END_FUNCTION;
        FUNCTION kind_control(values : LIST OF root) : SET OF target;
          LOCAL result : SET OF target; END_LOCAL;
          result := values;
          RETURN(result);
        END_FUNCTION;
        FUNCTION disjoint_control(values : SET OF unrelated) : SET OF target;
          LOCAL result : SET OF target; END_LOCAL;
          result := values;
          RETURN(result);
        END_FUNCTION;
        ENTITY sample;
          bag_values : BAG OF root;
          list_values : LIST OF root;
          array_values : ARRAY [1:1] OF root;
          unrelated_values : SET OF unrelated;
        WHERE
          bag_rejected : EXISTS(bag_control(bag_values));
          list_rejected : EXISTS(list_control(list_values));
          array_rejected : EXISTS(array_control(array_values));
          kind_rejected : EXISTS(kind_control(list_values));
          disjoint_rejected : EXISTS(disjoint_control(unrelated_values));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELECT_ASSIGNMENT_SCHEMA = """
        SCHEMA select_assignment_model;
        ENTITY base_item;
          code : INTEGER;
        END_ENTITY;
        ENTITY child_item SUBTYPE OF (base_item);
          extra : INTEGER;
        END_ENTITY;
        ENTITY other_item;
          code : INTEGER;
        END_ENTITY;
        TYPE item_choice = SELECT (base_item, other_item);
        END_TYPE;
        TYPE marker_kind = ENUMERATION OF (ok,other);
        END_TYPE;
        FUNCTION assign_direct(item : base_item; input_marker : marker_kind) : item_choice;
          LOCAL
            result : item_choice;
            flag : BOOLEAN := TRUE;
            marker : marker_kind := input_marker;
          END_LOCAL;
          IF flag AND (marker = input_marker) THEN
            result := item;
          ELSE
            result := item;
          END_IF;
          RETURN(result);
        END_FUNCTION;
        FUNCTION return_subtype(item : child_item) : item_choice;
          RETURN(item);
        END_FUNCTION;
        FUNCTION return_same(item : item_choice) : item_choice;
          RETURN(item);
        END_FUNCTION;
        FUNCTION project_select(item : base_item) : base_item;
          LOCAL
            choice : item_choice;
            result : base_item;
          END_LOCAL;
          choice := item;
          result := choice;
          RETURN(result);
        END_FUNCTION;
        FUNCTION maybe_choice(item : base_item) : item_choice;
          RETURN(item);
        END_FUNCTION;
        FUNCTION select_nvl(item : base_item) : base_item;
          LOCAL
            result : base_item := NVL(maybe_choice(item), item);
          END_LOCAL;
          RETURN(result);
        END_FUNCTION;
        ENTITY sample;
          direct_item : base_item;
          child_value : child_item;
          selected : item_choice;
          marker : marker_kind;
        WHERE
          direct_assignment : EXISTS(assign_direct(direct_item,marker));
          subtype_return : EXISTS(return_subtype(child_value));
          same_select : EXISTS(return_same(selected));
          selected_entity : EXISTS(project_select(direct_item));
          selected_nvl : EXISTS(select_nvl(direct_item));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELECT_ASSIGNMENT_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SelectAssignmentModel;

        internal static class SelectAssignmentConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["select_assignment_model"])),
                    [TedToolkit.Step21.Generated.SelectAssignmentModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("select_assignment_model"));
                structure.DataSections.Add(section);
                var direct = new BaseItem(BigInteger.One);
                var child = new ChildItem(new BigInteger(2), new BigInteger(3));
                _ = structure.Add(section, direct);
                _ = structure.Add(section, child);
                _ = structure.Add(section, new Sample(
                    direct,
                    child,
                    ItemChoice.FromOtherItem(new OtherItem(new BigInteger(4))),
                    MarkerKind.Ok));
                return structure.Validate();
            }
        }
        """;

    private const string INCOMPATIBLE_SELECT_ASSIGNMENT_SCHEMA = """
        SCHEMA incompatible_select_assignment_model;
        ENTITY accepted;
        END_ENTITY;
        ENTITY rejected;
        END_ENTITY;
        TYPE accepted_choice = SELECT (accepted);
        END_TYPE;
        FUNCTION invalid(item : rejected) : accepted_choice;
          RETURN(item);
        END_FUNCTION;
        ENTITY sample;
          item : rejected;
        WHERE
          rejected_assignment : EXISTS(invalid(item));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string AMBIGUOUS_SELECT_ASSIGNMENT_SCHEMA = """
        SCHEMA ambiguous_select_assignment_model;
        ENTITY base_item;
        END_ENTITY;
        ENTITY child_item SUBTYPE OF (base_item);
        END_ENTITY;
        TYPE ambiguous_choice = SELECT (base_item, child_item);
        END_TYPE;
        FUNCTION invalid(item : child_item) : ambiguous_choice;
          RETURN(item);
        END_FUNCTION;
        ENTITY sample;
          item : child_item;
        WHERE
          ambiguous_assignment : EXISTS(invalid(item));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INDETERMINATE_SELECT_ASSIGNMENT_SCHEMA = """
        SCHEMA indeterminate_select_assignment_model;
        ENTITY base_item;
        END_ENTITY;
        TYPE item_choice = SELECT (base_item);
        END_TYPE;
        FUNCTION invalid() : item_choice;
          LOCAL
            item : base_item;
          END_LOCAL;
          item := ?;
          RETURN(item);
        END_FUNCTION;
        ENTITY sample;
        WHERE
          indeterminate_assignment : EXISTS(invalid());
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NUMERIC_APPLICATION_WIDENING_SCHEMA = """
        SCHEMA numeric_application_widening_model;
        TYPE real_measure = REAL;
        END_TYPE;
        TYPE label_value = STRING;
        END_TYPE;
        TYPE year_value = INTEGER;
        END_TYPE;
        ENTITY base_item;
        END_ENTITY;
        TYPE item_choice = SELECT (base_item);
        END_TYPE;
        ENTITY real_pair;
          plain_value : REAL;
          defined_value : real_measure;
        END_ENTITY;
        ENTITY nominal_values;
          name_value : label_value;
          year_component : year_value;
          length_value : real_measure;
        END_ENTITY;
        FUNCTION widen_real(input_value : REAL) : REAL;
          RETURN(input_value);
        END_FUNCTION;
        FUNCTION widen_defined(input_value : real_measure) : REAL;
          RETURN(input_value);
        END_FUNCTION;
        FUNCTION accept_year(input_value : year_value) : INTEGER;
          RETURN(input_value);
        END_FUNCTION;
        FUNCTION select_scale(item : item_choice; scale : REAL) : REAL;
          IF EXISTS(item) THEN
            RETURN(scale);
          END_IF;
          RETURN(0.0);
        END_FUNCTION;
        ENTITY sample;
          item : base_item;
        DERIVE
          pair : real_pair := real_pair(1, 5);
          nominal : nominal_values := nominal_values('ok', 2026, 7);
        WHERE
          entity_plain : pair.plain_value = 1.0;
          entity_defined : pair.defined_value = 5.0;
          function_plain : widen_real(2) = 2.0;
          function_defined : widen_defined(3) = 3.0;
          string_defined : nominal.name_value = 'ok';
          integer_defined : accept_year(2026) = 2026;
          same_defined : accept_year(nominal.year_component) = 2026;
          real_defined : nominal.length_value = 7.0;
          select_then_numeric : select_scale(item, 4) = 4.0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NUMERIC_ASSIGNMENT_WIDENING_SCHEMA = """
        SCHEMA numeric_assignment_widening_model;
        TYPE dimension_count = INTEGER;
        END_TYPE;
        FUNCTION maybe_integer(flag : BOOLEAN) : INTEGER;
          IF flag THEN
            RETURN(2);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION promote_literal(flag : BOOLEAN) : REAL;
          LOCAL
            result_value : REAL;
          END_LOCAL;
          IF flag THEN
            result_value := 0;
          ELSE
            result_value := ?;
          END_IF;
          RETURN(result_value);
        END_FUNCTION;
        FUNCTION promote_optional(flag : BOOLEAN) : REAL;
          LOCAL
            result_value : REAL;
          END_LOCAL;
          result_value := maybe_integer(flag);
          RETURN(result_value);
        END_FUNCTION;
        FUNCTION keep_real(input : REAL) : REAL;
          LOCAL
            result_value : REAL;
          END_LOCAL;
          result_value := input;
          RETURN(result_value);
        END_FUNCTION;
        FUNCTION wrap_count(input : INTEGER) : dimension_count;
          LOCAL
            result_value : dimension_count;
          END_LOCAL;
          result_value := input;
          RETURN(result_value);
        END_FUNCTION;
        FUNCTION collect_count(input : dimension_count) : BOOLEAN;
          LOCAL
            values : SET OF dimension_count := [];
          END_LOCAL;
          values := values + [input];
          RETURN(SIZEOF(values) = 1);
        END_FUNCTION;
        ENTITY sample;
        WHERE
          literal_present : promote_literal(TRUE) = 0.0;
          literal_unknown : NOT EXISTS(promote_literal(FALSE));
          optional_present : promote_optional(TRUE) = 2.0;
          optional_unknown : NOT EXISTS(promote_optional(FALSE));
          real_unchanged : keep_real(3.0) = 3.0;
          defined_assignment : wrap_count(4) = 4;
          defined_aggregate : collect_count(wrap_count(4));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NUMERIC_ASSIGNMENT_WIDENING_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.NumericAssignmentWideningModel;

        internal static class NumericAssignmentWideningConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["numeric_assignment_widening_model"])),
                    [TedToolkit.Step21.Generated.NumericAssignmentWideningModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("numeric_assignment_widening_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample());
                return structure.Validate();
            }
        }
        """;

    private const string NUMERIC_ASSIGNMENT_NARROWING_SCHEMA = """
        SCHEMA numeric_assignment_narrowing_model;
        FUNCTION invalid(input : REAL) : INTEGER;
          LOCAL
            result_value : INTEGER;
          END_LOCAL;
          result_value := input;
          RETURN(result_value);
        END_FUNCTION;
        ENTITY sample;
        WHERE
          invalid_assignment : invalid(1.5) = 1;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NUMERIC_NVL_SCHEMA = """
        SCHEMA numeric_nvl_model;
        TYPE real_measure = REAL;
        END_TYPE;
        FUNCTION maybe_real(input_value : REAL; present : BOOLEAN) : REAL;
          IF present THEN
            RETURN(input_value);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION maybe_integer(input_value : INTEGER; present : BOOLEAN) : INTEGER;
          IF present THEN
            RETURN(input_value);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION maybe_measure(input_value : real_measure; present : BOOLEAN) : real_measure;
          IF present THEN
            RETURN(input_value);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION as_measure(input_value : real_measure) : real_measure;
          RETURN(input_value);
        END_FUNCTION;
        ENTITY sample;
        WHERE
          real_integer_present : NVL(maybe_real(2.5, TRUE), 1) = 2.5;
          real_integer_absent : NVL(maybe_real(2.5, FALSE), 1) = 1.0;
          integer_real_present : NVL(maybe_integer(2, TRUE), 1.5) = 2.0;
          integer_real_absent : NVL(maybe_integer(2, FALSE), 1.5) = 1.5;
          real_same : NVL(maybe_real(2.5, FALSE), 3.5) = 3.5;
          integer_same : NVL(maybe_integer(2, FALSE), 3) = 3;
          defined_same : NVL(maybe_measure(2.5, FALSE), as_measure(4.5)) = 4.5;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NUMERIC_NVL_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.NumericNvlModel;

        internal static class NumericNvlConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["numeric_nvl_model"])),
                    [TedToolkit.Step21.Generated.NumericNvlModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("numeric_nvl_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample());
                return structure.Validate();
            }
        }
        """;

    private const string NUMERIC_NVL_CONTROLS = """
        SCHEMA numeric_nvl_controls;
        TYPE first_measure = REAL;
        END_TYPE;
        TYPE second_measure = REAL;
        END_TYPE;
        FUNCTION maybe_real(present : BOOLEAN) : REAL;
          IF present THEN
            RETURN(1.5);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION maybe_first(present : BOOLEAN) : first_measure;
          IF present THEN
            RETURN(1.5);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION as_second(input_value : second_measure) : second_measure;
          RETURN(input_value);
        END_FUNCTION;
        FUNCTION real_to_integer(present : BOOLEAN) : INTEGER;
          RETURN(NVL(maybe_real(present), 1));
        END_FUNCTION;
        FUNCTION incompatible(present : BOOLEAN) : STRING;
          RETURN(NVL(maybe_real(present), 'fallback'));
        END_FUNCTION;
        FUNCTION different_defined(present : BOOLEAN) : second_measure;
          RETURN(NVL(maybe_first(present), as_second(1.0)));
        END_FUNCTION;
        ENTITY sample;
        WHERE
          narrowing_control : real_to_integer(TRUE) = 1;
          incompatible_control : incompatible(TRUE) = 'fallback';
          different_defined_control : different_defined(TRUE) = 1.0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NULLABLE_AGGREGATE_INITIALIZER_SCHEMA = """
        SCHEMA nullable_aggregate_initializer_model;
        FUNCTION maybe_real(input_value : REAL; present : BOOLEAN) : REAL;
          IF present THEN
            RETURN(input_value);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION accept_list(values : LIST [3:3] OF REAL) : BOOLEAN;
          RETURN((values[1] = 1.0) AND (values[2] = 2.0) AND (values[3] = 3.0));
        END_FUNCTION;
        FUNCTION accept_set(values : SET [3:3] OF STRING) : BOOLEAN;
          RETURN(('first' IN values) AND ('second' IN values) AND ('third' IN values));
        END_FUNCTION;
        FUNCTION guarded_list(present : BOOLEAN) : BOOLEAN;
          RETURN(accept_list([
            maybe_real(1.0, TRUE),
            maybe_real(2.0, present),
            maybe_real(3.0, TRUE)]));
        END_FUNCTION;
        FUNCTION optional_array(present : BOOLEAN) : ARRAY [1:2] OF OPTIONAL REAL;
          RETURN([maybe_real(1.0, TRUE), maybe_real(2.0, present)]);
        END_FUNCTION;
        FUNCTION set_control(flag : BOOLEAN) : BOOLEAN;
          LOCAL
            values : SET [1:?] OF REAL;
          END_LOCAL;
          IF NOT flag THEN
            RETURN(FALSE);
          END_IF;
          values := [1.0];
          RETURN((SIZEOF(values) = 1) AND (values[1] = 1.0));
        END_FUNCTION;
        FUNCTION bag_control(flag : BOOLEAN) : BOOLEAN;
          LOCAL
            values : BAG [2:2] OF REAL;
          END_LOCAL;
          IF NOT flag THEN
            RETURN(FALSE);
          END_IF;
          values := [1.0, 1.0];
          RETURN(SIZEOF(values) = 2);
        END_FUNCTION;
        FUNCTION maybe_upper(present : BOOLEAN) : INTEGER;
          IF present THEN RETURN(2); END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION guarded_repeat(present : BOOLEAN) : INTEGER;
          LOCAL result : INTEGER := 0; END_LOCAL;
          REPEAT i := 1 TO maybe_upper(present);
            result := result + 1;
          END_REPEAT;
          RETURN(result);
        END_FUNCTION;
        ENTITY sample;
        WHERE
          list_present : guarded_list(TRUE);
          list_unknown : NOT EXISTS(guarded_list(FALSE));
          optional_array_present : EXISTS(optional_array(FALSE));
          optional_array_first : optional_array(FALSE)[1] = 1.0;
          optional_array_unset : NOT EXISTS(optional_array(FALSE)[2]);
          set_initializer : set_control(TRUE);
          bag_initializer : bag_control(TRUE);
          contextual_set_initializer : accept_set(['first', 'second', 'third']);
          present_repeat : guarded_repeat(TRUE) = 2;
          absent_repeat : guarded_repeat(FALSE) = 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NULLABLE_AGGREGATE_INITIALIZER_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.NullableAggregateInitializerModel;

        internal static class NullableAggregateInitializerConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["nullable_aggregate_initializer_model"])),
                    [TedToolkit.Step21.Generated.NullableAggregateInitializerModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("nullable_aggregate_initializer_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample());
                return structure.Validate();
            }
        }
        """;

    private const string SELECT_AGGREGATE_APPLICATION_SCHEMA = """
        SCHEMA select_aggregate_application_model;
        ENTITY representation_item;
        END_ENTITY;
        TYPE list_representation_item = LIST [1:4] OF representation_item;
        END_TYPE;
        TYPE set_representation_item = SET [1:4] OF representation_item;
        END_TYPE;
        TYPE compound_item_definition = SELECT
          (list_representation_item, set_representation_item);
        END_TYPE;
        FUNCTION accepts_aggregate(values : AGGREGATE OF representation_item) : BOOLEAN;
          RETURN((SIZEOF(values) > 0)
            AND (LOBOUND(values) = 1)
            AND (HIBOUND(values) = 4)
            AND (LOINDEX(values) = 1)
            AND (HIINDEX(values) = SIZEOF(values)));
        END_FUNCTION;
        ENTITY compound_representation_item SUBTYPE OF (representation_item);
          item_element : compound_item_definition;
        WHERE
          valid_item_element : accepts_aggregate(item_element);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string DEFINED_RETURN_BOUNDARY_SCHEMA = """
        SCHEMA defined_return_boundary_model;
        TYPE dimension_count = INTEGER;
        END_TYPE;
        FUNCTION maybe_integer(present : BOOLEAN) : INTEGER;
          IF present THEN
            RETURN(2);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION dimension_from_integer(present : BOOLEAN) : dimension_count;
          RETURN(maybe_integer(present));
        END_FUNCTION;
        FUNCTION same_dimension(input_value : dimension_count) : dimension_count;
          RETURN(input_value);
        END_FUNCTION;
        ENTITY sample;
          present : BOOLEAN;
        DERIVE
          computed : dimension_count := dimension_from_integer(present);
          fixed_value : dimension_count := 3;
        WHERE
          function_present : dimension_from_integer(TRUE) = 2;
          function_unknown : NOT EXISTS(dimension_from_integer(FALSE));
          derived_state : (present AND (computed = 2)) OR
            ((NOT present) AND (NOT EXISTS(computed)));
          determinate_derived : fixed_value = 3;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NESTED_DEFINED_RETURN_SCHEMA = """
        SCHEMA nested_defined_return_model;
        TYPE dimension_count = INTEGER;
        END_TYPE;
        TYPE nested_dimension = dimension_count;
        END_TYPE;
        FUNCTION nested_from_integer(input_value : INTEGER) : nested_dimension;
          RETURN(input_value);
        END_FUNCTION;
        ENTITY sample;
        DERIVE
          nested_value : nested_dimension := nested_from_integer(4);
        WHERE
          nested_derived : nested_value = 4;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string DEFINED_RETURN_BOUNDARY_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.DefinedReturnBoundaryModel;

        internal static class DefinedReturnBoundaryConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["defined_return_boundary_model"])),
                    [TedToolkit.Step21.Generated.DefinedReturnBoundaryModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("defined_return_boundary_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(true));
                _ = structure.Add(section, new Sample(false));
                return structure.Validate();
            }
        }
        """;

    private const string DIFFERENT_DEFINED_RETURN_SCHEMA = """
        SCHEMA different_defined_return_model;
        TYPE dimension_count = INTEGER;
        END_TYPE;
        TYPE other_count = INTEGER;
        END_TYPE;
        FUNCTION invalid(input_value : other_count) : dimension_count;
          RETURN(input_value);
        END_FUNCTION;
        ENTITY sample;
          value : other_count;
        WHERE
          invalid_nominal : invalid(value) = 1;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NUMERIC_APPLICATION_WIDENING_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.NumericApplicationWideningModel;

        internal static class NumericApplicationWideningConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["numeric_application_widening_model"])),
                    [TedToolkit.Step21.Generated.NumericApplicationWideningModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("numeric_application_widening_model"));
                structure.DataSections.Add(section);
                var item = new BaseItem();
                _ = structure.Add(section, item);
                _ = structure.Add(section, new Sample(item));
                return structure.Validate();
            }
        }
        """;

    private const string NUMERIC_APPLICATION_NARROWING_SCHEMA = """
        SCHEMA numeric_application_narrowing_model;
        FUNCTION keep_integer(input_value : INTEGER) : INTEGER;
          RETURN(input_value);
        END_FUNCTION;
        ENTITY sample;
        WHERE
          invalid_narrowing : keep_integer(1.5) = 1;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string DIFFERENT_DEFINED_APPLICATION_SCHEMA = """
        SCHEMA different_defined_application_model;
        TYPE year_value = INTEGER;
        END_TYPE;
        TYPE day_value = INTEGER;
        END_TYPE;
        FUNCTION accept_year(input_value : year_value) : INTEGER;
          RETURN(input_value);
        END_FUNCTION;
        ENTITY sample;
          day_component : day_value;
        WHERE
          invalid_nominal : accept_year(day_component) = 1;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string OPTIONAL_DEFINED_APPLICATION_SCHEMA = """
        SCHEMA optional_defined_application_model;
        TYPE year_value = INTEGER;
        END_TYPE;
        FUNCTION accept_year(input_value : year_value) : INTEGER;
          RETURN(input_value);
        END_FUNCTION;
        ENTITY sample;
        WHERE
          invalid_unknown : accept_year(?) = 1;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SET_APPLICATION_INITIALIZER_SCHEMA = """
        SCHEMA set_application_initializer_model;
        ENTITY item;
          id : INTEGER;
        END_ENTITY;
        FUNCTION set_size(values : SET OF item) : INTEGER;
          RETURN(SIZEOF(values));
        END_FUNCTION;
        FUNCTION bounded_set_size(values : SET [1:?] OF item) : INTEGER;
          RETURN(SIZEOF(values));
        END_FUNCTION;
        FUNCTION set_passthrough(values : SET OF item) : INTEGER;
          RETURN(set_size(values));
        END_FUNCTION;
        FUNCTION list_order(values : LIST OF INTEGER) : LOGICAL;
          RETURN((values[1] = 1) AND (values[2] = 2));
        END_FUNCTION;
        ENTITY sample;
          first_item : item;
          members : SET OF item;
        WHERE
          singleton_set : set_size([first_item]) = 1;
          empty_set : set_size([]) = 0;
          bounded_singleton : bounded_set_size([first_item]) = 1;
          same_set : set_passthrough(members) = SIZEOF(members);
          ordered_list : list_order([1,2]);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SET_APPLICATION_INITIALIZER_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SetApplicationInitializerModel;

        internal static class SetApplicationInitializerConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["set_application_initializer_model"])),
                    [TedToolkit.Step21.Generated.SetApplicationInitializerModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("set_application_initializer_model"));
                structure.DataSections.Add(section);
                var item = new Item(1);
                _ = structure.Add(section, item);
                _ = structure.Add(section, new Sample(item, new ExpressSet<IItem> { item }));
                return structure.Validate();
            }
        }
        """;

    private const string UNSAFE_SET_APPLICATION_INITIALIZER_SCHEMA = """
        SCHEMA unsafe_set_application_initializer_model;
        FUNCTION set_size(values : SET OF INTEGER) : INTEGER;
          RETURN(SIZEOF(values));
        END_FUNCTION;
        FUNCTION bounded_set_size(values : SET [1:?] OF INTEGER) : INTEGER;
          RETURN(SIZEOF(values));
        END_FUNCTION;
        FUNCTION list_to_set(values : LIST OF INTEGER) : INTEGER;
          RETURN(set_size(values));
        END_FUNCTION;
        FUNCTION bag_to_set(values : BAG OF INTEGER) : INTEGER;
          RETURN(set_size(values));
        END_FUNCTION;
        ENTITY sample;
        WHERE
          duplicate_items : set_size([1,1]) = 2;
          repeated_item : set_size([1:2]) = 2;
          empty_bounded_set : bounded_set_size([]) = 0;
          list_variable : list_to_set([1]) = 1;
          bag_variable : bag_to_set([1]) = 1;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string OPTIONAL_DEFINED_VALUE_SCHEMA = """
        SCHEMA optional_defined_value_model;
        TYPE label_value = STRING;
        END_TYPE;
        TYPE minute_value = INTEGER;
        END_TYPE;
        TYPE second_value = REAL;
        END_TYPE;
        ENTITY optional_holder;
          label : OPTIONAL label_value;
        END_ENTITY;
        ENTITY base_address;
          location : OPTIONAL label_value;
        WHERE
          inherited_required : EXISTS(location);
        END_ENTITY;
        ENTITY child_address SUBTYPE OF (base_address);
        END_ENTITY;
        ENTITY clock;
          minute : OPTIONAL minute_value;
          second : OPTIONAL second_value;
        WHERE
          consistent : EXISTS(minute) = EXISTS(second);
        END_ENTITY;
        FUNCTION has_label(item : optional_holder) : BOOLEAN;
          RETURN(EXISTS(item.label));
        END_FUNCTION;
        FUNCTION read_label(item : optional_holder) : STRING;
          RETURN(item.label);
        END_FUNCTION;
        ENTITY sample;
          present_holder : optional_holder;
          absent_holder : optional_holder;
        WHERE
          present_exists : has_label(present_holder);
          absent_missing : NOT has_label(absent_holder);
          unguarded_read_is_unknown : NOT EXISTS(read_label(absent_holder));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string OPTIONAL_DEFINED_VALUE_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.OptionalDefinedValueModel;

        internal static class OptionalDefinedValueConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["optional_defined_value_model"])),
                    [TedToolkit.Step21.Generated.OptionalDefinedValueModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("optional_defined_value_model"));
                structure.DataSections.Add(section);
                var present = new OptionalHolder { Label = new LabelValue("present") };
                var absent = new OptionalHolder();
                _ = structure.Add(section, present);
                _ = structure.Add(section, absent);
                _ = structure.Add(section, new ChildAddress { Location = new LabelValue("child") });
                _ = structure.Add(section, new Clock
                {
                    Minute = new MinuteValue(BigInteger.One),
                    Second = new SecondValue(new RealValue(BigInteger.One, BigInteger.Zero)),
                });
                _ = structure.Add(section, new Clock());
                _ = structure.Add(section, new Sample(present, absent));
                return structure.Validate();
            }
        }
        """;

    private const string SELECT_ATTRIBUTE_QUALIFIER_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SelectAttributeQualifierModel;

        internal static class SelectAttributeQualifierConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["select_attribute_qualifier_model"])),
                    [TedToolkit.Step21.Generated.SelectAttributeQualifierModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("select_attribute_qualifier_model"));
                structure.DataSections.Add(section);
                var basis = new SurfaceValue("basis");
                var unrelatedSurface = new SurfaceValue("surface");
                var direction = new DirectionValue(new ExpressList<RealValue>(1, 2)
                {
                    new RealValue(new BigInteger(2), BigInteger.Zero),
                    new RealValue(new BigInteger(3), BigInteger.Zero),
                });
                var vector = new VectorValue(direction, new RealValue(new BigInteger(3), BigInteger.Zero));
                var pcurve = new PcurveValue(basis);
                var unit = new UnitValue(new BigInteger(3));
                _ = structure.Add(section, basis);
                _ = structure.Add(section, unrelatedSurface);
                _ = structure.Add(section, direction);
                _ = structure.Add(section, vector);
                _ = structure.Add(section, pcurve);
                _ = structure.Add(section, unit);
                _ = structure.Add(section, new GuardedSample(
                    PcurveOrSurface.FromPcurveValue(pcurve),
                    VectorOrDirection.FromVectorValue(vector),
                    basis));
                _ = structure.Add(section, new DirectionSample(
                    VectorOrDirection.FromDirectionValue(direction)));
                _ = structure.Add(section, new UnitSample(Unit.FromUnitValue(unit)));
                _ = structure.Add(section, new WrongAlternativeSample(
                    PcurveOrSurface.FromSurfaceValue(unrelatedSurface)));
                _ = structure.Add(section, new WrongCallSample(
                    VectorOrDirection.FromVectorValue(vector)));
                return structure.Validate();
            }
        }
        """;

    private const string SELECT_LEXICAL_NARROWING_SCHEMA = """
        SCHEMA select_lexical_narrowing_model;
        ENTITY surface_value;
          code : STRING;
        END_ENTITY;
        ENTITY pcurve_value;
          basis_surface : surface_value;
        END_ENTITY;
        TYPE pcurve_or_surface = SELECT (pcurve_value, surface_value);
        END_TYPE;
        TYPE nested_choice = SELECT (pcurve_or_surface);
        END_TYPE;
        FUNCTION closed_surface(item : pcurve_or_surface) : surface_value;
          IF 'SELECT_LEXICAL_NARROWING_MODEL.PCURVE_VALUE' IN TYPEOF(item) THEN
            RETURN(item.basis_surface);
          ELSE
            RETURN(item);
          END_IF;
        END_FUNCTION;
        FUNCTION guarded_surface(item : pcurve_or_surface) : surface_value;
          IF 'SELECT_LEXICAL_NARROWING_MODEL.SURFACE_VALUE' IN TYPEOF(item) THEN
            RETURN(item);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION nested_surface(item : nested_choice) : surface_value;
          IF 'SELECT_LEXICAL_NARROWING_MODEL.SURFACE_VALUE' IN TYPEOF(item) THEN
            RETURN(item);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        ENTITY sample;
          pcurve_choice : pcurve_or_surface;
          surface_choice : pcurve_or_surface;
          nested_surface_choice : nested_choice;
          expected : surface_value;
        WHERE
          closed_then : closed_surface(pcurve_choice) = expected;
          closed_else : closed_surface(surface_choice) = expected;
          guarded_then : guarded_surface(surface_choice) = expected;
          nested_then : nested_surface(nested_surface_choice) = expected;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELECT_LEXICAL_NARROWING_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SelectLexicalNarrowingModel;

        internal static class SelectLexicalNarrowingConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["select_lexical_narrowing_model"])),
                    [TedToolkit.Step21.Generated.SelectLexicalNarrowingModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("select_lexical_narrowing_model"));
                structure.DataSections.Add(section);
                var surface = new SurfaceValue("surface");
                var pcurve = new PcurveValue(surface);
                _ = structure.Add(section, surface);
                _ = structure.Add(section, pcurve);
                _ = structure.Add(section, new Sample(
                    PcurveOrSurface.FromPcurveValue(pcurve),
                    PcurveOrSurface.FromSurfaceValue(surface),
                    NestedChoice.FromPcurveOrSurface(PcurveOrSurface.FromSurfaceValue(surface)),
                    surface));
                return structure.Validate();
            }
        }
        """;

    private const string UNGUARDED_SELECT_LEXICAL_SCHEMA = """
        SCHEMA unguarded_select_lexical_model;
        ENTITY left_value;
        END_ENTITY;
        ENTITY right_value;
        END_ENTITY;
        TYPE value_choice = SELECT (left_value, right_value);
        END_TYPE;
        FUNCTION unguarded(item : value_choice) : right_value;
          RETURN(item);
        END_FUNCTION;
        ENTITY sample;
          selected : value_choice;
        WHERE
          remains_unprotected : EXISTS(unguarded(selected));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string QUALIFIED_PATH_NARROWING_SCHEMA = """
        SCHEMA qualified_path_narrowing_model;
        ENTITY surface_value;
          code : INTEGER;
        END_ENTITY;
        ENTITY pcurve_value;
          code : INTEGER;
        END_ENTITY;
        TYPE path_choice = SELECT (surface_value, pcurve_value);
        END_TYPE;
        ENTITY carrier;
          primary_values : LIST [2:2] OF path_choice;
          secondary_values : LIST [2:2] OF path_choice;
        END_ENTITY;
        ENTITY carrier_child SUBTYPE OF (carrier);
        END_ENTITY;
        FUNCTION surface_code(item : surface_value) : INTEGER;
          RETURN(item.code);
        END_FUNCTION;
        FUNCTION pcurve_code(item : pcurve_value) : INTEGER;
          RETURN(item.code);
        END_FUNCTION;
        FUNCTION qualified_code(item : carrier_child; i : INTEGER) : INTEGER;
          IF 'QUALIFIED_PATH_NARROWING_MODEL.SURFACE_VALUE' IN
              TYPEOF(item\carrier.primary_values[i]) THEN
            RETURN(surface_code(item\carrier.primary_values[i]));
          END_IF;
          IF 'QUALIFIED_PATH_NARROWING_MODEL.PCURVE_VALUE' IN
              TYPEOF(item\carrier.primary_values[i]) THEN
            RETURN(pcurve_code(item\carrier.primary_values[i]));
          END_IF;
          RETURN(-1);
        END_FUNCTION;
        ENTITY sample;
          item : carrier_child;
        WHERE
          first_path : qualified_code(item, 1) = 11;
          second_path : qualified_code(item, 2) = 22;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string QUALIFIED_PATH_NARROWING_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.QualifiedPathNarrowingModel;

        internal static class QualifiedPathNarrowingConsumer
        {
            internal static ValidationResult Validate()
            {
                var surface = new SurfaceValue(new BigInteger(11));
                var pcurve = new PcurveValue(new BigInteger(22));
                var values = new ExpressList<PathChoice>(2, 2)
                {
                    PathChoice.FromSurfaceValue(surface),
                    PathChoice.FromPcurveValue(pcurve),
                };
                var item = new CarrierChild(values, values);
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["qualified path narrowing"], "3;1"),
                        new FileName("qualified-path.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["qualified_path_narrowing_model"])),
                    [TedToolkit.Step21.Generated.QualifiedPathNarrowingModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("qualified_path_narrowing_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, surface);
                _ = structure.Add(section, pcurve);
                _ = structure.Add(section, item);
                _ = structure.Add(section, new Sample(item));
                return structure.Validate();
            }
        }
        """;

    private const string QUALIFIED_PATH_NARROWING_CONTROL = """
        SCHEMA qualified_path_narrowing_control;
        ENTITY surface_value; code : INTEGER; END_ENTITY;
        ENTITY pcurve_value; code : INTEGER; END_ENTITY;
        TYPE path_choice = SELECT (surface_value, pcurve_value); END_TYPE;
        ENTITY first_carrier;
          primary_values : LIST [2:2] OF path_choice;
          secondary_values : LIST [2:2] OF path_choice;
        END_ENTITY;
        ENTITY second_carrier;
          primary_values : LIST [2:2] OF path_choice;
        END_ENTITY;
        ENTITY combined SUBTYPE OF (first_carrier, second_carrier); END_ENTITY;
        FUNCTION surface_code(item : surface_value) : INTEGER; RETURN(item.code); END_FUNCTION;
        FUNCTION invalid_path(first : combined; second : combined; i : INTEGER; j : INTEGER) : INTEGER;
          __CONDITION__
            __THEN_STATEMENT__
          END_IF;
          __AFTER_STATEMENT__
        END_FUNCTION;
        ENTITY sample;
          first : combined;
          second : combined;
        WHERE
          reachable : invalid_path(first, second, 1, 2) = 1;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string QUALIFIED_PATH_SHADOW_CONTROL = """
        SCHEMA qualified_path_shadow_control;
        ENTITY surface_value; code : INTEGER; END_ENTITY;
        ENTITY pcurve_value; code : INTEGER; END_ENTITY;
        TYPE path_choice = SELECT (surface_value, pcurve_value); END_TYPE;
        ENTITY carrier; values : LIST [1:1] OF path_choice; END_ENTITY;
        FUNCTION surface_code(item : surface_value) : INTEGER; RETURN(item.code); END_FUNCTION;
        FUNCTION guarded(item : carrier; i : INTEGER) : INTEGER;
          IF 'QUALIFIED_PATH_SHADOW_CONTROL.SURFACE_VALUE' IN TYPEOF(item.values[i]) THEN
            RETURN(surface_code(item.values[i]));
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION shadowed(item : carrier; i : INTEGER) : INTEGER;
          RETURN(surface_code(item.values[i]));
        END_FUNCTION;
        ENTITY sample; item : carrier;
        WHERE
          guarded_reachable : guarded(item, 1) > 0;
          shadowed_identity_is_distinct : shadowed(item, 1) > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SIZEOF_BRANCH_DETERMINACY_SCHEMA = """
        SCHEMA sizeof_branch_determinacy_model;
        FUNCTION maybe_values(values : BAG OF INTEGER; expose : BOOLEAN) : BAG OF INTEGER;
          IF expose THEN
            RETURN(values);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION guarded_nonempty(values : BAG OF INTEGER; expose : BOOLEAN) : BOOLEAN;
          LOCAL
            items : BAG OF INTEGER;
          END_LOCAL;
          items := maybe_values(values, expose);
          IF SIZEOF(items) > 0 THEN
            REPEAT i := 1 TO HIINDEX(items) BY 1;
              RETURN(TRUE);
            END_REPEAT;
          END_IF;
          RETURN(FALSE);
        END_FUNCTION;
        ENTITY sample;
          nonempty : BAG OF INTEGER;
          empty_values : BAG OF INTEGER;
        WHERE
          present_nonempty : guarded_nonempty(nonempty, TRUE);
          present_empty : NOT guarded_nonempty(empty_values, TRUE);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SIZEOF_BRANCH_DETERMINACY_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SizeofBranchDeterminacyModel;

        internal static class SizeofBranchDeterminacyConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["sizeof branch determinacy"], "3;1"),
                        new FileName("sizeof-branch.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["sizeof_branch_determinacy_model"])),
                    [TedToolkit.Step21.Generated.SizeofBranchDeterminacyModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("sizeof_branch_determinacy_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(
                    new ExpressBag<BigInteger> { BigInteger.One },
                    new ExpressBag<BigInteger>()));
                return structure.Validate();
            }
        }
        """;

    private const string SIZEOF_BRANCH_DETERMINACY_CONTROL = """
        SCHEMA sizeof_branch_determinacy_control;
        FUNCTION maybe_values(values : BAG OF INTEGER; expose : BOOLEAN) : BAG OF INTEGER;
          IF expose THEN RETURN(values); END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION invalid_flow(values : BAG OF INTEGER; expose : BOOLEAN) : BOOLEAN;
          LOCAL items : BAG OF INTEGER; END_LOCAL;
          items := maybe_values(values, expose);
          __STATEMENTS__
          RETURN(FALSE);
        END_FUNCTION;
        ENTITY sample; values : BAG OF INTEGER;
        WHERE reachable : EXISTS(invalid_flow(values, TRUE)); END_ENTITY;
        END_SCHEMA;
        """;

    private const string ASSIGNED_LOCAL_DETERMINACY_SCHEMA = """
        SCHEMA assigned_local_determinacy_model;
        FUNCTION maybe_count(expose : BOOLEAN) : INTEGER;
          IF expose THEN RETURN(1); END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION first_value(values : LIST [1:?] OF INTEGER) : INTEGER;
          LOCAL count : INTEGER; END_LOCAL;
          count := SIZEOF(values);
          REPEAT index := 1 TO count BY 1;
            RETURN(values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION guarded_count(expose : BOOLEAN) : INTEGER;
          LOCAL count : INTEGER; END_LOCAL;
          count := maybe_count(expose);
          REPEAT index := 1 TO count BY 1;
            RETURN(count);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION initialized_value(values : BAG [1:?] OF INTEGER) : INTEGER;
          LOCAL items : BAG OF INTEGER := []; END_LOCAL;
          items := items + values;
          REPEAT index := 1 TO HIINDEX(items) BY 1;
            RETURN(items[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION maybe_boolean(expose, candidate : BOOLEAN) : BOOLEAN;
          IF expose THEN RETURN(candidate); END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION assigned_boolean(expose, candidate : BOOLEAN) : BOOLEAN;
          LOCAL assigned : BOOLEAN; END_LOCAL;
          assigned := maybe_boolean(expose,candidate);
          RETURN(NOT assigned);
        END_FUNCTION;
        ENTITY sample;
          values : LIST [1:?] OF INTEGER;
          bag_values : BAG [1:?] OF INTEGER;
        WHERE
          direct_assignment : first_value(values) = 7;
          present_assignment : guarded_count(TRUE) = 1;
          unknown_assignment : NOT EXISTS(guarded_count(FALSE));
          initialized_assignment : initialized_value(bag_values) = 7;
          assigned_false : assigned_boolean(TRUE,FALSE);
          assigned_true : NOT assigned_boolean(TRUE,TRUE);
          assigned_unknown : NOT EXISTS(assigned_boolean(FALSE,FALSE));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ASSIGNED_LOCAL_DETERMINACY_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.AssignedLocalDeterminacyModel;

        internal static class AssignedLocalDeterminacyConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["assigned local determinacy"], "3;1"),
                        new FileName("assigned-local.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["assigned_local_determinacy_model"])),
                    [TedToolkit.Step21.Generated.AssignedLocalDeterminacyModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("assigned_local_determinacy_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(
                    new ExpressList<BigInteger>(1) { 7 },
                    new ExpressBag<BigInteger>(1) { 7 }));
                return structure.Validate();
            }
        }
        """;

    private const string SELECT_SCALAR_NARROWING_SCHEMA = """
        SCHEMA select_scalar_narrowing_model;
        TYPE first_real = REAL;
        END_TYPE;
        TYPE second_real = REAL;
        END_TYPE;
        TYPE integer_value = NUMBER;
        END_TYPE;
        TYPE measure_choice = SELECT (first_real, second_real, integer_value);
        END_TYPE;
        FUNCTION positive_real(item : measure_choice) : BOOLEAN;
          IF 'REAL' IN TYPEOF(item) THEN
            RETURN(item > 0.0);
          END_IF;
          RETURN(TRUE);
        END_FUNCTION;
        FUNCTION positive_integer(item : measure_choice) : BOOLEAN;
          IF 'INTEGER' IN TYPEOF(item) THEN
            RETURN(item > 0);
          END_IF;
          RETURN(TRUE);
        END_FUNCTION;
        FUNCTION positive_closed_else(item : measure_choice) : BOOLEAN;
          IF 'REAL' IN TYPEOF(item) THEN
            RETURN(TRUE);
          ELSE
            RETURN(item > 0);
          END_IF;
        END_FUNCTION;
        FUNCTION scalar_assignment(item, replacement : measure_choice) : BOOLEAN;
          IF 'REAL' IN TYPEOF(item) THEN
            item := replacement;
          END_IF;
          RETURN(EXISTS(item));
        END_FUNCTION;
        FUNCTION scalar_use_after_no_else(item : measure_choice) : BOOLEAN;
          IF 'REAL' IN TYPEOF(item) THEN
            IF item > 0.0 THEN
              RETURN(TRUE);
            END_IF;
          END_IF;
          RETURN(EXISTS(item));
        END_FUNCTION;
        FUNCTION scalar_assignment_then_use(item, replacement : measure_choice) : BOOLEAN;
          IF 'REAL' IN TYPEOF(item) THEN
            item := replacement;
            IF 'INTEGER' IN TYPEOF(item) THEN
              RETURN(item > 0);
            END_IF;
          END_IF;
          RETURN(TRUE);
        END_FUNCTION;
        ENTITY sample;
          first : measure_choice;
          second : measure_choice;
          number_real : measure_choice;
          integer_item : measure_choice;
        WHERE
          first_real_branch : positive_real(first);
          second_real_branch : positive_real(second);
          number_real_branch : positive_real(number_real);
          integer_branch : positive_integer(integer_item);
          closed_else_branch : positive_closed_else(integer_item);
          scalar_assignment_storage : scalar_assignment(first, integer_item);
          scalar_no_else_storage : scalar_use_after_no_else(first);
          scalar_assignment_use : scalar_assignment_then_use(first, integer_item);
          integer_control : 2 > 1;
          real_control : 2.0 > 1.0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELECT_SCALAR_NARROWING_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SelectScalarNarrowingModel;

        internal static class SelectScalarNarrowingConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["select_scalar_narrowing_model"])),
                    [TedToolkit.Step21.Generated.SelectScalarNarrowingModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("select_scalar_narrowing_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(
                    MeasureChoice.FromFirstReal(new FirstReal(new RealValue(2, BigInteger.Zero))),
                    MeasureChoice.FromSecondReal(new SecondReal(new RealValue(3, BigInteger.Zero))),
                    MeasureChoice.FromIntegerValue(new IntegerValue(NumberValue.FromReal(
                        new RealValue(35, new BigInteger(-1))))),
                    MeasureChoice.FromIntegerValue(new IntegerValue(NumberValue.FromInteger(new BigInteger(4))))));
                return structure.Validate();
            }
        }
        """;

    private const string UNGUARDED_SELECT_SCALAR_SCHEMA = """
        SCHEMA unguarded_select_scalar_model;
        TYPE real_value = REAL;
        END_TYPE;
        TYPE text_value = STRING;
        END_TYPE;
        TYPE mixed_choice = SELECT (real_value, text_value);
        END_TYPE;
        FUNCTION unguarded(item : mixed_choice) : BOOLEAN;
          RETURN(item > 0.0);
        END_FUNCTION;
        FUNCTION wrong_guard(item : mixed_choice) : BOOLEAN;
          IF 'INTEGER' IN TYPEOF(item) THEN
            RETURN(item > 0);
          END_IF;
          RETURN(TRUE);
        END_FUNCTION;
        ENTITY sample;
          selected : mixed_choice;
        WHERE
          remains_unprotected : unguarded(selected);
          wrong_alternative : wrong_guard(selected);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELECT_GROUP_QUALIFIER_SCHEMA = """
        SCHEMA select_group_qualifier_model;
        ENTITY root;
          code : INTEGER;
        END_ENTITY;
        ENTITY left SUBTYPE OF (root);
          left_value : INTEGER;
        END_ENTITY;
        ENTITY deep_left SUBTYPE OF (left);
        END_ENTITY;
        ENTITY right SUBTYPE OF (root);
          right_value : INTEGER;
        END_ENTITY;
        TYPE item_choice = SELECT (deep_left, right);
        END_TYPE;
        FUNCTION guarded_left(item : item_choice) : INTEGER;
          IF 'SELECT_GROUP_QUALIFIER_MODEL.DEEP_LEFT' IN TYPEOF(item) THEN
            RETURN(item\left.left_value);
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION closed_choice(item : item_choice) : INTEGER;
          IF 'SELECT_GROUP_QUALIFIER_MODEL.DEEP_LEFT' IN TYPEOF(item) THEN
            RETURN(item\left.left_value);
          ELSE
            RETURN(item\right.right_value);
          END_IF;
        END_FUNCTION;
        FUNCTION inherited_root(item : item_choice) : INTEGER;
          RETURN(item\root.code);
        END_FUNCTION;
        FUNCTION unguarded_left(item : item_choice) : INTEGER;
          RETURN(item\left.left_value);
        END_FUNCTION;
        FUNCTION direct_group(item : deep_left) : INTEGER;
          RETURN(item\root.code + item\left.left_value);
        END_FUNCTION;
        ENTITY sample;
          left_choice : item_choice;
          right_choice : item_choice;
          direct_item : deep_left;
        WHERE
          guarded_rule : guarded_left(left_choice) = 2;
          closed_left_rule : closed_choice(left_choice) = 2;
          closed_right_rule : closed_choice(right_choice) = 4;
          inherited_left_rule : inherited_root(left_choice) = 1;
          inherited_right_rule : inherited_root(right_choice) = 3;
          wrong_alternative_is_unknown : NOT EXISTS(unguarded_left(right_choice));
          direct_group_rule : direct_group(direct_item) = 3;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELECT_GROUP_QUALIFIER_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SelectGroupQualifierModel;

        internal static class SelectGroupQualifierConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["select_group_qualifier_model"])),
                    [TedToolkit.Step21.Generated.SelectGroupQualifierModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("select_group_qualifier_model"));
                structure.DataSections.Add(section);
                var left = new DeepLeft(BigInteger.One, new BigInteger(2));
                var right = new Right(new BigInteger(3), new BigInteger(4));
                _ = structure.Add(section, left);
                _ = structure.Add(section, right);
                _ = structure.Add(section, new Sample(
                    ItemChoice.FromDeepLeft(left),
                    ItemChoice.FromRight(right),
                    left));
                return structure.Validate();
            }
        }
        """;

    private const string LOGICAL_BOUNDARY_SCHEMA = """
        SCHEMA logical_boundary_model;
        FUNCTION logical_from_boolean(input_value : BOOLEAN) : LOGICAL;
          RETURN(input_value);
        END_FUNCTION;
        FUNCTION boolean_from_logical(input_value : LOGICAL) : BOOLEAN;
          RETURN(input_value);
        END_FUNCTION;
        FUNCTION boolean_caller(input_value : LOGICAL) : BOOLEAN;
          RETURN(boolean_from_logical(input_value));
        END_FUNCTION;
        FUNCTION local_roundtrip(input_value : LOGICAL) : LOGICAL;
          LOCAL
            boolean_value : BOOLEAN;
            logical_value : LOGICAL;
          END_LOCAL;
          boolean_value := input_value;
          logical_value := boolean_value;
          RETURN(logical_value);
        END_FUNCTION;
        FUNCTION determinate_boolean(input_value : BOOLEAN) : BOOLEAN;
          RETURN(input_value);
        END_FUNCTION;
        FUNCTION determinate_logical(input_value : LOGICAL) : LOGICAL;
          RETURN(input_value);
        END_FUNCTION;
        FUNCTION recursive_logical(
          values : LIST [1:?] OF LOGICAL; index : INTEGER) : LOGICAL;
          IF index = SIZEOF(values) THEN
            RETURN(determinate_logical(values[index]));
          END_IF;
          RETURN(NOT recursive_logical(values,index + 1));
        END_FUNCTION;
        FUNCTION guarded_caller(input_value : LOGICAL) : BOOLEAN;
          RETURN(determinate_boolean(boolean_from_logical(input_value)));
        END_FUNCTION;
        FUNCTION if_score(input_value : LOGICAL) : INTEGER;
          IF determinate_boolean(boolean_from_logical(input_value)) THEN
            RETURN(1);
          END_IF;
          RETURN(0);
        END_FUNCTION;
        FUNCTION query_true_count(
          true_value, false_value, unknown_value : LOGICAL) : INTEGER;
          RETURN(SIZEOF(QUERY ( item <* [true_value,false_value,unknown_value] |
            determinate_boolean(boolean_from_logical(item)) )));
        END_FUNCTION;
        ENTITY sample;
          true_logical : LOGICAL;
          false_logical : LOGICAL;
          unknown_logical : LOGICAL;
        WHERE
          boolean_true_promotes : logical_from_boolean(TRUE);
          boolean_false_promotes : NOT logical_from_boolean(FALSE);
          logical_true_lowers : boolean_from_logical(true_logical) AND TRUE;
          logical_false_lowers : NOT boolean_from_logical(false_logical);
          logical_unknown_is_absent : NOT EXISTS(boolean_from_logical(unknown_logical));
          caller_propagates_unknown : NOT EXISTS(boolean_caller(unknown_logical));
          guarded_caller_propagates_unknown : NOT EXISTS(guarded_caller(unknown_logical));
          local_unknown_roundtrips : NOT EXISTS(boolean_from_logical(
            local_roundtrip(unknown_logical)));
          determinate_control : determinate_boolean(TRUE);
          exists_control : NOT EXISTS(determinate_boolean(
            boolean_from_logical(unknown_logical)));
          nvl_control : NOT NVL(determinate_boolean(
            boolean_from_logical(unknown_logical)),FALSE);
          if_true_enters : if_score(true_logical) = 1;
          if_false_skips : if_score(false_logical) = 0;
          if_unknown_skips : if_score(unknown_logical) = 0;
          query_keeps_only_true : query_true_count(
            true_logical,false_logical,unknown_logical) = 1;
        END_ENTITY;
        ENTITY predicate_sample;
          candidate : LOGICAL;
        WHERE
          accepted : determinate_boolean(boolean_from_logical(candidate));
        END_ENTITY;
        ENTITY logical_normalization_sample;
          candidate : LOGICAL;
        WHERE
          guarded_value : NOT determinate_logical(local_roundtrip(candidate));
          nullable_value : NOT local_roundtrip(candidate);
          recursive_not : recursive_logical([candidate],0);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string LOGICAL_BOUNDARY_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.LogicalBoundaryModel;

        internal static class LogicalBoundaryConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["logical_boundary_model"])),
                    [TedToolkit.Step21.Generated.LogicalBoundaryModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("logical_boundary_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(
                    LogicalValue.True,
                    LogicalValue.False,
                    LogicalValue.Unknown));
                return structure.Validate();
            }

            internal static ValidationResult ValidatePredicate(LogicalValue candidate)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["logical_boundary_model"])),
                    [TedToolkit.Step21.Generated.LogicalBoundaryModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("logical_boundary_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new PredicateSample(candidate));
                return structure.Validate();
            }

            internal static ValidationResult ValidateLogicalNormalization(LogicalValue candidate)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["logical_boundary_model"])),
                    [TedToolkit.Step21.Generated.LogicalBoundaryModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("logical_boundary_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new LogicalNormalizationSample(candidate));
                return structure.Validate();
            }
        }
        """;

    private const string STRING_CONCAT_SCHEMA = """
        SCHEMA string_concat_model;
        FUNCTION concatenate(prefix, suffix : STRING) : STRING;
          RETURN(prefix + suffix);
        END_FUNCTION;
        FUNCTION integer_sum(left_value, right_value : INTEGER) : INTEGER;
          RETURN(left_value + right_value);
        END_FUNCTION;
        FUNCTION real_sum(left_value : REAL; right_value : NUMBER) : NUMBER;
          RETURN(left_value + right_value);
        END_FUNCTION;
        FUNCTION number_sum(left_value : NUMBER; right_value : INTEGER) : NUMBER;
          RETURN(left_value + right_value);
        END_FUNCTION;
        ENTITY sample;
        WHERE
          direct_concat : concatenate('CONFIG_', 'ITEM') = 'CONFIG_ITEM';
          aggregate_intersection : SIZEOF(
            (['STRING_CONCAT_MODEL.' + 'SAMPLE', 'OTHER'] * TYPEOF(SELF))) = 1;
          integer_control : integer_sum(1, 2) = 3;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string STRING_CONCAT_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.StringConcatModel;

        internal static class StringConcatConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["string_concat_model"])),
                    [TedToolkit.Step21.Generated.StringConcatModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("string_concat_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample());
                return structure.Validate();
            }
        }
        """;

    private const string INVALID_STRING_NUMERIC_SCHEMA = """
        SCHEMA invalid_string_numeric;
        ENTITY sample;
        WHERE
          invalid_mixed : ('A' + 1) = 'A1';
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INVALID_HETEROGENEOUS_AGGREGATE_SCHEMA = """
        SCHEMA invalid_heterogeneous_aggregate;
        ENTITY sample;
        WHERE
          invalid_elements : SIZEOF(([1, 'A'] * TYPEOF(SELF))) = 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELECT_VALUE_EQUALITY_SCHEMA = """
        SCHEMA select_value_equality_model;
        TYPE text_value = STRING;
        END_TYPE;
        TYPE status = ENUMERATION OF (active, inactive);
        END_TYPE;
        ENTITY marker;
          code : STRING;
        END_ENTITY;
        TYPE mixed_value = SELECT (text_value, status, marker);
        END_TYPE;
        ENTITY text_sample;
          value_component : mixed_value;
        END_ENTITY;
        ENTITY enum_sample;
          value_component : mixed_value;
        END_ENTITY;
        ENTITY optional_sample;
          value_component : OPTIONAL mixed_value;
        WHERE
          matches : value_component = 'ok';
        END_ENTITY;
        RULE text_population_rule FOR (text_sample);
        WHERE
          all_match : SIZEOF(QUERY(candidate <* text_sample |
            NOT (candidate.value_component = 'ok'))) = 0;
        END_RULE;
        RULE enum_population_rule FOR (enum_sample);
        WHERE
          all_match : SIZEOF(QUERY(candidate <* enum_sample |
            NOT (candidate.value_component = status.active))) = 0;
        END_RULE;
        END_SCHEMA;
        """;

    private const string SELECT_VALUE_EQUALITY_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SelectValueEqualityModel;

        internal static class SelectValueEqualityConsumer
        {
            internal static ValidationResult ValidateText(string value)
            {
                var structure = CreateStructure(out var section);
                _ = structure.Add(section, new TextSample(
                    MixedValue.FromTextValue(new TextValue(value))));
                return structure.Validate();
            }

            internal static ValidationResult ValidateEnum(bool matching)
            {
                var structure = CreateStructure(out var section);
                var value = matching ? Status.Active : Status.Inactive;
                _ = structure.Add(section, new EnumSample(MixedValue.FromStatus(value)));
                return structure.Validate();
            }

            internal static ValidationResult ValidateWrongAlternative()
            {
                var structure = CreateStructure(out var section);
                _ = structure.Add(section, new TextSample(
                    MixedValue.FromMarker(new Marker("marker"))));
                return structure.Validate();
            }

            internal static ValidationResult ValidateUnknown()
            {
                var structure = CreateStructure(out var section);
                _ = structure.Add(section, new OptionalSample());
                return structure.Validate();
            }

            private static ExchangeStructure CreateStructure(out DataSection section)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["select_value_equality_model"])),
                    [TedToolkit.Step21.Generated.SelectValueEqualityModel.SchemaDescriptor.Instance]);
                section = new DataSection(new SchemaName("select_value_equality_model"));
                structure.DataSections.Add(section);
                return structure;
            }
        }
        """;

    private const string ENTITY_SELECT_VALUE_EQUALITY_SCHEMA = """
        SCHEMA entity_select_value_equality_model;
        ENTITY marker;
          code : STRING;
        END_ENTITY;
        TYPE marker_choice = SELECT (marker);
        END_TYPE;
        ENTITY sample;
          left_value : marker_choice;
          right_value : marker_choice;
        WHERE
          same_value : left_value = right_value;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ENTITY_SELECT_VALUE_EQUALITY_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.EntitySelectValueEqualityModel;

        internal static class EntitySelectValueEqualityConsumer
        {
            internal static ValidationResult Validate(bool matching)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["entity_select_value_equality_model"])),
                    [TedToolkit.Step21.Generated.EntitySelectValueEqualityModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("entity_select_value_equality_model"));
                structure.DataSections.Add(section);
                var left = MarkerChoice.FromMarker(new Marker("same"));
                var right = MarkerChoice.FromMarker(new Marker(matching ? "same" : "different"));
                _ = structure.Add(section, new Sample(left, right));
                return structure.Validate();
            }
        }
        """;

    private const string SELECT_INSTANCE_EQUALITY_SCHEMA = """
        SCHEMA select_instance_equality_model;
        TYPE text_value = STRING;
        END_TYPE;
        ENTITY base_item;
          code : STRING;
        END_ENTITY;
        ENTITY child_item SUBTYPE OF (base_item);
        END_ENTITY;
        ENTITY other_item;
        END_ENTITY;
        TYPE inner_choice = SELECT (child_item, text_value);
        END_TYPE;
        TYPE outer_choice = SELECT (inner_choice, other_item);
        END_TYPE;
        ENTITY select_entity_sample;
          selected : outer_choice;
          actual : base_item;
        WHERE
          forward_identity : selected :=: actual;
          reverse_identity : actual :=: selected;
        END_ENTITY;
        ENTITY optional_select_sample;
          selected : OPTIONAL outer_choice;
          actual : base_item;
        WHERE
          unknown_identity : selected :=: actual;
        END_ENTITY;
        ENTITY select_pair_sample;
          left_value : outer_choice;
          right_value : outer_choice;
        WHERE
          same_identity : left_value :=: right_value;
        END_ENTITY;
        ENTITY entity_pair_sample;
          left_value : base_item;
          right_value : base_item;
        WHERE
          same_identity : left_value :=: right_value;
          same_value : left_value = right_value;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELECT_INSTANCE_EQUALITY_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SelectInstanceEqualityModel;

        internal static class SelectInstanceEqualityConsumer
        {
            internal static ValidationResult ValidateSelectEntity(int mode)
            {
                var actual = new ChildItem("same");
                var selected = mode switch
                {
                    0 => OuterChoice.FromInnerChoice(InnerChoice.FromChildItem(actual)),
                    1 => OuterChoice.FromOtherItem(new OtherItem()),
                    _ => OuterChoice.FromInnerChoice(InnerChoice.FromTextValue(new TextValue("same"))),
                };
                return Validate(new SelectEntitySample(selected, actual));
            }

            internal static ValidationResult ValidateUnknown()
            {
                return Validate(new OptionalSelectSample(new ChildItem("same")));
            }

            internal static ValidationResult ValidateSelectPair(bool sameReference, bool wrongAlternative)
            {
                var left = new ChildItem("same");
                var right = sameReference ? left : new ChildItem("same");
                var leftChoice = OuterChoice.FromInnerChoice(InnerChoice.FromChildItem(left));
                var rightChoice = wrongAlternative
                    ? OuterChoice.FromOtherItem(new OtherItem())
                    : OuterChoice.FromInnerChoice(InnerChoice.FromChildItem(right));
                return Validate(new SelectPairSample(leftChoice, rightChoice));
            }

            internal static ValidationResult ValidateEntity(bool sameReference, bool sameValue)
            {
                var left = new ChildItem("same");
                var right = sameReference ? left : new ChildItem(sameValue ? "same" : "different");
                return Validate(new EntityPairSample(left, right));
            }

            private static ValidationResult Validate(Entity sample)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["select_instance_equality_model"])),
                    [TedToolkit.Step21.Generated.SelectInstanceEqualityModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("select_instance_equality_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, sample);
                return structure.Validate();
            }
        }
        """;

    private const string SELECT_USEDIN_CARRIER_SCHEMA = """
        SCHEMA select_usedin_carrier_model;
        ENTITY target_a;
        END_ENTITY;
        ENTITY target_b;
        END_ENTITY;
        TYPE target_choice = SELECT (target_a, target_b);
        END_TYPE;
        TYPE nested_target_choice = SELECT (target_choice);
        END_TYPE;
        ENTITY select_owner;
          target : target_choice;
        END_ENTITY;
        ENTITY nested_owner;
          target : nested_target_choice;
        END_ENTITY;
        ENTITY entity_owner;
          target : target_a;
        END_ENTITY;
        ENTITY select_sample;
          selected : target_choice;
        WHERE
          one_reference : SIZEOF(USEDIN(selected,
            'SELECT_USEDIN_CARRIER_MODEL.SELECT_OWNER.TARGET')) = 1;
        END_ENTITY;
        ENTITY nested_sample;
          selected : nested_target_choice;
        WHERE
          one_reference : SIZEOF(USEDIN(selected,
            'SELECT_USEDIN_CARRIER_MODEL.NESTED_OWNER.TARGET')) = 1;
        END_ENTITY;
        ENTITY entity_sample;
          selected : target_a;
        WHERE
          one_reference : SIZEOF(USEDIN(selected,
            'SELECT_USEDIN_CARRIER_MODEL.ENTITY_OWNER.TARGET')) = 1;
        END_ENTITY;
        ENTITY optional_sample;
          selected : OPTIONAL target_choice;
        WHERE
          unknown_reference : SIZEOF(USEDIN(selected,
            'SELECT_USEDIN_CARRIER_MODEL.SELECT_OWNER.TARGET')) = 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELECT_USEDIN_CARRIER_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SelectUsedinCarrierModel;

        internal static class SelectUsedinCarrierConsumer
        {
            internal static ValidationResult ValidateSelect(bool secondAlternative)
            {
                Entity target = secondAlternative ? new TargetB() : new TargetA();
                var selected = secondAlternative
                    ? TargetChoice.FromTargetB((TargetB)target)
                    : TargetChoice.FromTargetA((TargetA)target);
                return Validate(target, new SelectOwner(selected), new SelectSample(selected));
            }

            internal static ValidationResult ValidateNested()
            {
                var target = new TargetA();
                var selected = TargetChoice.FromTargetA(target);
                var nested = NestedTargetChoice.FromTargetChoice(selected);
                return Validate(target, new NestedOwner(nested), new NestedSample(nested));
            }

            internal static ValidationResult ValidateEntity()
            {
                var target = new TargetA();
                return Validate(target, new EntityOwner(target), new EntitySample(target));
            }

            internal static ValidationResult ValidateUnknown()
            {
                return Validate(new OptionalSample());
            }

            private static ValidationResult Validate(params Entity[] values)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["select_usedin_carrier_model"])),
                    [TedToolkit.Step21.Generated.SelectUsedinCarrierModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("select_usedin_carrier_model"));
                structure.DataSections.Add(section);
                foreach (var value in values)
                {
                    _ = structure.Add(section, value);
                }

                return structure.Validate();
            }
        }
        """;

    private const string MIXED_SELECT_USEDIN_SCHEMA = """
        SCHEMA mixed_select_usedin_model;
        TYPE text_value = STRING;
        END_TYPE;
        ENTITY target;
        END_ENTITY;
        TYPE mixed_choice = SELECT (target, text_value);
        END_TYPE;
        FUNCTION count_references(selected : mixed_choice) : INTEGER;
          RETURN(SIZEOF(USEDIN(selected, '')));
        END_FUNCTION;
        ENTITY sample;
          selected : mixed_choice;
        WHERE
          no_references : count_references(selected) = 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ENTITY_VALUE_CYCLE_SCHEMA = """
        SCHEMA entity_value_cycle_model;
        ENTITY base ABSTRACT SUPERTYPE OF (ONEOF(node, other));
        END_ENTITY;
        ENTITY node SUBTYPE OF (base);
          code : OPTIONAL STRING;
          next_node : OPTIONAL node;
        END_ENTITY;
        ENTITY other SUBTYPE OF (base);
          code : OPTIONAL STRING;
        END_ENTITY;
        ENTITY sample;
          left_value : base;
          right_value : base;
        WHERE
          same_value : left_value = right_value;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ENTITY_VALUE_CYCLE_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.EntityValueCycleModel;

        internal static class EntityValueCycleConsumer
        {
            internal static ValidationResult Validate(int mode)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["entity_value_cycle_model"])),
                    [TedToolkit.Step21.Generated.EntityValueCycleModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("entity_value_cycle_model"));
                structure.DataSections.Add(section);
                IBase left;
                IBase right;
                var references = new global::System.Collections.Generic.List<Entity>();
                if (mode == 0)
                {
                    var leftNode = new Node { Code = "same" };
                    var rightNode = new Node { Code = "same" };
                    leftNode.NextNode = leftNode;
                    rightNode.NextNode = rightNode;
                    left = leftNode;
                    right = rightNode;
                    references.Add(leftNode);
                    references.Add(rightNode);
                }
                else if (mode == 1)
                {
                    var leftFirst = new Node { Code = "same" };
                    var leftSecond = new Node { Code = "same" };
                    var rightFirst = new Node { Code = "same" };
                    var rightSecond = new Node { Code = "same" };
                    leftFirst.NextNode = leftSecond;
                    leftSecond.NextNode = leftFirst;
                    rightFirst.NextNode = rightSecond;
                    rightSecond.NextNode = rightFirst;
                    left = leftFirst;
                    right = rightFirst;
                    references.Add(leftFirst);
                    references.Add(leftSecond);
                    references.Add(rightFirst);
                    references.Add(rightSecond);
                }
                else if (mode == 2)
                {
                    left = new Node();
                    right = new Node();
                    references.Add((Entity)left);
                    references.Add((Entity)right);
                }
                else
                {
                    left = new Node { Code = "same" };
                    right = new Other { Code = "same" };
                    references.Add((Entity)left);
                    references.Add((Entity)right);
                }

                foreach (var reference in references)
                {
                    _ = structure.Add(section, reference);
                }

                _ = structure.Add(section, new Sample(left, right));
                return structure.Validate();
            }
        }
        """;

    private const string ENTITY_VALUE_AGGREGATE_SCHEMA = """
        SCHEMA entity_value_aggregate_model;
        ENTITY item;
          code : OPTIONAL STRING;
        END_ENTITY;
        ENTITY sample;
          left_list : LIST [0:?] OF item;
          right_list : LIST [0:?] OF item;
          left_array : ARRAY [1:2] OF OPTIONAL item;
          right_array : ARRAY [1:2] OF OPTIONAL item;
          left_set : SET [0:?] OF item;
          right_set : SET [0:?] OF item;
          left_bag : BAG [0:?] OF item;
          right_bag : BAG [0:?] OF item;
        WHERE
          list_same : left_list = right_list;
          array_same : left_array = right_array;
          set_same : left_set = right_set;
          bag_same : left_bag = right_bag;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ENTITY_VALUE_AGGREGATE_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.EntityValueAggregateModel;

        internal static class EntityValueAggregateConsumer
        {
            internal static ValidationResult Validate(int mode)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["rules"], "3;1"),
                        new FileName("rules.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["entity_value_aggregate_model"])),
                    [TedToolkit.Step21.Generated.EntityValueAggregateModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("entity_value_aggregate_model"));
                structure.DataSections.Add(section);

                var leftA = new Item { Code = mode is 6 or 7 ? null : "a" };
                var leftB = new Item { Code = "b" };
                var rightA = new Item { Code = mode is 6 or 7 ? null : "a" };
                var rightB = new Item { Code = "b" };
                var leftList = new ExpressList<IItem>(0) { leftA, leftB };
                var rightList = mode == 1
                    ? new ExpressList<IItem>(0) { rightB, rightA }
                    : new ExpressList<IItem>(0) { rightA, rightB };
                var leftArray = new ExpressArray<IItem>(1, 2, isOptional: true);
                var rightArray = new ExpressArray<IItem>(mode == 2 ? 0 : 1, mode == 2 ? 1 : 2, isOptional: true);
                leftArray[1] = leftA;
                leftArray[2] = leftB;
                rightArray[mode == 2 ? 0 : 1] = rightA;
                if (mode != 3)
                {
                    rightArray[mode == 2 ? 1 : 2] = rightB;
                }

                var leftSet = new ExpressSet<IItem>(0) { leftA, leftB };
                var rightSet = new ExpressSet<IItem>(0) { rightB, rightA };
                var leftBag = new ExpressBag<IItem>(0) { leftA, leftA, leftB };
                var rightBag = mode == 5
                    ? new ExpressBag<IItem>(0) { rightA, rightB, rightB }
                    : new ExpressBag<IItem>(0) { rightB, rightA, rightA };
                _ = structure.Add(section, leftA);
                _ = structure.Add(section, leftB);
                _ = structure.Add(section, rightA);
                _ = structure.Add(section, rightB);
                _ = structure.Add(section, new Sample(
                    leftList,
                    rightList,
                    leftArray,
                    rightArray,
                    leftSet,
                    rightSet,
                    leftBag,
                    rightBag));
                return structure.Validate();
            }
        }
        """;

    private const string ENTITY_VALUE_COMPLEX_SCHEMA = """
        SCHEMA entity_value_complex_model;
        ENTITY target;
          code : STRING;
        END_ENTITY;
        ENTITY root SUPERTYPE OF (left ANDOR right ANDOR marker);
          label : STRING;
          peer : target;
          note : OPTIONAL STRING;
        END_ENTITY;
        ENTITY left SUBTYPE OF (root);
          amount : INTEGER;
        END_ENTITY;
        ENTITY right SUBTYPE OF (root);
          rank : INTEGER;
        END_ENTITY;
        ENTITY marker SUBTYPE OF (root);
        END_ENTITY;
        ENTITY holder;
          left_value : root;
          right_value : root;
        WHERE
          same_value : left_value = right_value;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ENTITY_VALUE_COMPLEX_CONSUMER = """"
        using System.IO;
        using TedToolkit.Step21;

        internal static class EntityValueComplexConsumer
        {
            internal static ValidationResult Validate(int mode)
            {
                var rightValue = mode == 1
                    ? "(LEFT(1)RIGHT(3)ROOT('same',#11,'note'))"
                    : mode == 2
                        ? "(LEFT(1)MARKER()ROOT('same',#11,'note'))"
                        : mode == 3
                            ? "(LEFT(1)RIGHT(2)ROOT('same',#11,$))"
                            : "(LEFT(1)RIGHT(2)ROOT('same',#11,'note'))";
                var leftValue = mode == 3
                    ? "(LEFT(1)RIGHT(2)ROOT('same',#10,$))"
                    : "(LEFT(1)RIGHT(2)ROOT('same',#10,'note'))";
                var source = $"""
                    ISO-10303-21;
                    HEADER;
                    FILE_DESCRIPTION(('rules'),'3;1');
                    FILE_NAME('rules.step','2026-08-24T00:00:00+08:00', (), (),'tests','tests','');
                    FILE_SCHEMA(('entity_value_complex_model'));
                    ENDSEC;
                    DATA;
                    #10=TARGET('peer');
                    #11=TARGET('peer');
                    #1={leftValue};
                    #2={rightValue};
                    #3=HOLDER(#1,#2);
                    ENDSEC;
                    END-ISO-10303-21;
                    """;
                try
                {
                    var structure = ExchangeStructure.Read(
                        new StringReader(source),
                        [TedToolkit.Step21.Generated.EntityValueComplexModel.SchemaDescriptor.Instance]);
                    return structure.Validate();
                }
                catch (ExchangeStructureReadValidationException exception)
                {
                    return exception.ValidationResult;
                }
            }
        }
        """";

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
        ENTITY holder;
          amount_value : INTEGER;
        END_ENTITY;
        FUNCTION used(input_value : INTEGER) : INTEGER;
          LOCAL
            result_value : INTEGER;
            item : holder := holder(input_value);
          END_LOCAL;
          result_value := input_value;
          BEGIN
            REPEAT index := 1 TO 1 BY 1;
              item.amount_value := input_value;
              result_value := item.amount_value;
            END_REPEAT;
          END;
          CASE result_value OF
            1 : RETURN(result_value);
            OTHERWISE : RETURN(result_value);
          END_CASE;
        END_FUNCTION;
        ENTITY sample;
          amount : INTEGER;
        WHERE
          positive : used(amount) > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string REACHABLE_ALGORITHM_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.UnsupportedModel;

        internal static class ReachableAlgorithmConsumer
        {
            internal static ValidationResult Validate(BigInteger amount)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["algorithm"], "3;1"),
                        new FileName("algorithm.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["unsupported_model"])),
                    [TedToolkit.Step21.Generated.UnsupportedModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("unsupported_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(amount));
                return structure.Validate();
            }
        }
        """;

    private const string INDETERMINATE_FUNCTION_SCHEMA = """
        SCHEMA indeterminate_function_model;
        ENTITY holder;
          amount : INTEGER;
        END_ENTITY;
        FUNCTION direct_unknown(flag : BOOLEAN) : holder;
          IF flag THEN RETURN(?);
          END_IF;
          RETURN(holder(1));
        END_FUNCTION;
        FUNCTION local_unknown(flag : BOOLEAN) : holder;
          LOCAL
            result : holder;
          END_LOCAL;
          IF flag THEN result := ?;
          ELSE result := holder(1);
          END_IF;
          RETURN(result);
        END_FUNCTION;
        FUNCTION propagated_unknown(flag : BOOLEAN) : holder;
          RETURN(direct_unknown(flag));
        END_FUNCTION;
        FUNCTION determinate_result(flag : BOOLEAN) : holder;
          LOCAL
            result : holder;
          END_LOCAL;
          IF flag THEN result := holder(1);
          ELSE result := holder(1);
          END_IF;
          RETURN(result);
        END_FUNCTION;
        FUNCTION maybe_integer(flag : BOOLEAN) : INTEGER;
          IF flag THEN RETURN(?);
          END_IF;
          RETURN(1);
        END_FUNCTION;
        FUNCTION maybe_values(flag : BOOLEAN) : LIST [0:?] OF INTEGER;
          IF flag THEN RETURN(?);
          END_IF;
          RETURN([1]);
        END_FUNCTION;
        ENTITY sample;
          flag : BOOLEAN;
        DERIVE
          maybe_amount : INTEGER := maybe_integer(flag);
          propagated_amount : INTEGER := maybe_amount;
          maybe_list : LIST [0:?] OF INTEGER := maybe_values(flag);
          certain_amount : INTEGER := 1;
        UNIQUE
          derived_key : maybe_amount;
        WHERE
          direct_path : NOT EXISTS(direct_unknown(flag));
          local_path : NOT EXISTS(local_unknown(flag));
          propagated_path : NOT EXISTS(propagated_unknown(flag));
          determinate_path : EXISTS(determinate_result(flag));
          derived_scalar_path : (flag AND NOT EXISTS(maybe_amount)) OR
            (NOT flag AND EXISTS(maybe_amount));
          derived_propagated_path : (flag AND NOT EXISTS(propagated_amount)) OR
            (NOT flag AND EXISTS(propagated_amount));
          derived_aggregate_path : (flag AND NOT EXISTS(maybe_list)) OR
            (NOT flag AND EXISTS(maybe_list));
          determinate_derived_path : certain_amount = 1;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INDETERMINATE_CONSTANT_CONTROL_SCHEMA = """
        SCHEMA indeterminate_constant_control;
        CONSTANT
          invalid_value : INTEGER := ?;
        END_CONSTANT;
        ENTITY sample;
          amount : INTEGER;
        WHERE
          invalid_constant : invalid_value = amount;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INDETERMINATE_FUNCTION_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.IndeterminateFunctionModel;

        internal static class IndeterminateFunctionConsumer
        {
            internal static ValidationResult Validate(bool flag, bool duplicate)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["indeterminate function"], "3;1"),
                        new FileName("indeterminate-function.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["indeterminate_function_model"])),
                    [TedToolkit.Step21.Generated.IndeterminateFunctionModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("indeterminate_function_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(flag));
                if (duplicate)
                {
                    _ = structure.Add(section, new Sample(flag));
                }

                return structure.Validate();
            }
        }
        """;

    private const string INFERRED_GENERIC_LIST_SCHEMA = """
        SCHEMA inferred_generic_list_model;
        FUNCTION distinct_values(values : LIST [0:?] OF GENERIC:t) : SET OF GENERIC:t;
          LOCAL
            result : SET OF GENERIC:t := [];
          END_LOCAL;
          REPEAT index := 1 TO SIZEOF(values) BY 1;
            result := result + values[index];
            REPEAT nested := 1 TO SIZEOF(values) BY 1;
              result := result + values[index];
              result := result + values[nested];
            END_REPEAT;
          END_REPEAT;
          RETURN(result);
        END_FUNCTION;
        ENTITY sample;
          values : LIST [2:2] OF INTEGER;
        WHERE
          complete : SIZEOF(distinct_values(values)) = 2;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string INFERRED_GENERIC_LIST_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.InferredGenericListModel;

        internal static class InferredGenericListConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["generic list"], "3;1"),
                        new FileName("generic-list.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["inferred_generic_list_model"])),
                    [TedToolkit.Step21.Generated.InferredGenericListModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("inferred_generic_list_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(new ExpressList<BigInteger>(2, 2) { 1, 2 }));
                return structure.Validate();
            }
        }
        """;

    private const string NUMBER_INDEX_SCHEMA = """
        SCHEMA number_index_model;
        FUNCTION all_positive(values : LIST [1:?] OF INTEGER) : BOOLEAN;
          REPEAT index := 1 TO SIZEOF(values) BY 1;
            IF values[index] <= 0 THEN
              RETURN(FALSE);
            END_IF;
          END_REPEAT;
          RETURN(TRUE);
        END_FUNCTION;
        ENTITY sample;
          values : LIST [1:?] OF INTEGER;
        WHERE
          positive : all_positive(values);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NUMBER_INDEX_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.NumberIndexModel;

        internal static class NumberIndexConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["number index"], "3;1"),
                        new FileName("number-index.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["number_index_model"])),
                    [TedToolkit.Step21.Generated.NumberIndexModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("number_index_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(new ExpressList<BigInteger>(1) { 1, 2 }));
                return structure.Validate();
            }
        }
        """;

    private const string SAFE_INDEX_CONTROL_SCHEMA = """
        SCHEMA safe_index_control_model;
        FUNCTION different_aggregate(values, bounds : LIST [0:?] OF INTEGER) : INTEGER;
          REPEAT index := 1 TO SIZEOF(bounds) BY 1;
            RETURN(values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION different_bound(values : LIST [0:?] OF INTEGER) : INTEGER;
          REPEAT index := 0 TO SIZEOF(values) BY 1;
            RETURN(values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION different_step(values : LIST [0:?] OF INTEGER) : INTEGER;
          REPEAT index := 1 TO SIZEOF(values) BY 2;
            RETURN(values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION outside_repeat(values : LIST [0:?] OF INTEGER; index : INTEGER) : INTEGER;
          RETURN(values[index]);
        END_FUNCTION;
        FUNCTION rewritten_alias(values : LIST [0:?] OF INTEGER) : INTEGER;
          LOCAL count : INTEGER; END_LOCAL;
          count := SIZEOF(values);
          count := count + 1;
          REPEAT index := 2 TO count BY 1;
            RETURN(values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION different_alias(values, bounds : LIST [0:?] OF INTEGER) : INTEGER;
          LOCAL count : INTEGER; END_LOCAL;
          count := SIZEOF(bounds);
          REPEAT index := 2 TO count BY 1;
            RETURN(values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION branch_rewrite(values : LIST [0:?] OF INTEGER; trigger : BOOLEAN) : INTEGER;
          LOCAL count : INTEGER; END_LOCAL;
          count := SIZEOF(values);
          IF trigger THEN count := 0; END_IF;
          REPEAT index := 2 TO count BY 1;
            RETURN(values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION body_rewrite(values : LIST [0:?] OF INTEGER) : INTEGER;
          LOCAL count : INTEGER; END_LOCAL;
          count := SIZEOF(values);
          REPEAT unused := 1 TO 1 BY 1;
            count := 0;
          END_REPEAT;
          REPEAT index := 2 TO count BY 1;
            RETURN(values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION source_rewrite(values, replacement : LIST [0:?] OF INTEGER) : INTEGER;
          REPEAT index := 1 TO SIZEOF(values) BY 1;
            values := replacement;
            RETURN(values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION branch_source_rewrite(
                     values, replacement : LIST [0:?] OF INTEGER;
                     trigger : BOOLEAN) : INTEGER;
          REPEAT index := 1 TO SIZEOF(values) BY 1;
            IF trigger THEN
              values := replacement;
            END_IF;
            RETURN(values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION controls(trigger : BOOLEAN) : LOGICAL;
          LOCAL
            values : LIST [0:?] OF INTEGER := [];
            bounds : LIST [1:?] OF INTEGER := [1];
          END_LOCAL;
          RETURN(trigger
            AND NOT EXISTS(different_aggregate(values, bounds))
            AND NOT EXISTS(different_bound(bounds))
            AND EXISTS(different_step(bounds))
            AND NOT EXISTS(outside_repeat(values, 1))
            AND NOT EXISTS(rewritten_alias(values))
            AND NOT EXISTS(different_alias(values, bounds))
            AND NOT EXISTS(branch_rewrite(values, trigger))
            AND NOT EXISTS(body_rewrite(values))
            AND NOT EXISTS(source_rewrite(bounds, values))
            AND NOT EXISTS(branch_source_rewrite(bounds, values, trigger)));
        END_FUNCTION;
        ENTITY sample;
          trigger : BOOLEAN;
        WHERE
          valid : controls(trigger);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SAFE_INDEX_CONTROL_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SafeIndexControlModel;

        internal static class SafeIndexControlConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["safe index controls"], "3;1"),
                        new FileName("safe-index-controls.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["safe_index_control_model"])),
                    [TedToolkit.Step21.Generated.SafeIndexControlModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("safe_index_control_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(true));
                return structure.Validate();
            }
        }
        """;

    private const string QUALIFIED_SAFE_INDEX_SCHEMA = """
        SCHEMA qualified_safe_index_model;
        ENTITY patch_grid;
          segments : LIST [1:?] OF LIST [1:?] OF INTEGER;
          other_segments : LIST [1:?] OF LIST [1:?] OF INTEGER;
        END_ENTITY;
        FUNCTION first_segment_value(grid : patch_grid) : INTEGER;
          REPEAT row := 1 TO SIZEOF(grid.segments) BY 1;
            REPEAT column := 1 TO SIZEOF(grid.segments[row]) BY 1;
              RETURN(grid.segments[row][column]);
            END_REPEAT;
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        ENTITY sample;
          grid : patch_grid;
        WHERE
          nested_path : first_segment_value(grid) = 7;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string QUALIFIED_SAFE_INDEX_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.QualifiedSafeIndexModel;

        internal static class QualifiedSafeIndexConsumer
        {
            internal static ValidationResult Validate()
            {
                var grid = new PatchGrid(
                    new ExpressList<ExpressList<BigInteger>>(1)
                    {
                        new ExpressList<BigInteger>(1) { 7 },
                    },
                    new ExpressList<ExpressList<BigInteger>>(1)
                    {
                        new ExpressList<BigInteger>(1) { 9 },
                    });
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["qualified safe index"], "3;1"),
                        new FileName("qualified-safe-index.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["qualified_safe_index_model"])),
                    [TedToolkit.Step21.Generated.QualifiedSafeIndexModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("qualified_safe_index_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, grid);
                _ = structure.Add(section, new Sample(grid));
                return structure.Validate();
            }
        }
        """;

    private const string FLOW_FACT_LIFECYCLE_SCHEMA = """
        SCHEMA flow_fact_lifecycle_model;
        ENTITY first_value;
          code : INTEGER;
        END_ENTITY;
        ENTITY second_value;
          label : STRING;
        END_ENTITY;
        TYPE value_choice = SELECT (first_value, second_value);
        END_TYPE;
        ENTITY carrier;
          selected : value_choice;
        END_ENTITY;
        FUNCTION first_code(item : first_value) : INTEGER;
          RETURN(item.code);
        END_FUNCTION;
        FUNCTION direct_rewrite(item, replacement : value_choice) : INTEGER;
          IF 'FLOW_FACT_LIFECYCLE_MODEL.FIRST_VALUE' IN TYPEOF(item) THEN
            item := replacement;
            RETURN(first_code(item));
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION branch_rewrite(
                     item, replacement : value_choice;
                     change : BOOLEAN) : INTEGER;
          IF 'FLOW_FACT_LIFECYCLE_MODEL.FIRST_VALUE' IN TYPEOF(item) THEN
            IF change THEN
              item := replacement;
            END_IF;
            RETURN(first_code(item));
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION repeat_rewrite(item, replacement : value_choice) : INTEGER;
          IF 'FLOW_FACT_LIFECYCLE_MODEL.FIRST_VALUE' IN TYPEOF(item) THEN
            REPEAT index := 1 TO 1 BY 1;
              item := replacement;
            END_REPEAT;
            RETURN(first_code(item));
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION path_rewrite(item : carrier; replacement : value_choice) : INTEGER;
          LOCAL working : carrier := item; END_LOCAL;
          IF 'FLOW_FACT_LIFECYCLE_MODEL.FIRST_VALUE' IN TYPEOF(working.selected) THEN
            working.selected := replacement;
            RETURN(first_code(working.selected));
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION path_branch_rewrite(
                     item : carrier;
                     replacement : value_choice;
                     change : BOOLEAN) : INTEGER;
          LOCAL working : carrier := item; END_LOCAL;
          IF 'FLOW_FACT_LIFECYCLE_MODEL.FIRST_VALUE' IN TYPEOF(working.selected) THEN
            IF change THEN
              working.selected := replacement;
            END_IF;
            RETURN(first_code(working.selected));
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION path_case_rewrite(
                     item : carrier;
                     replacement : value_choice;
                     mode : INTEGER) : INTEGER;
          LOCAL
            working : carrier := item;
            untouched : INTEGER;
          END_LOCAL;
          IF 'FLOW_FACT_LIFECYCLE_MODEL.FIRST_VALUE' IN TYPEOF(working.selected) THEN
            CASE mode OF
              1 : working.selected := replacement;
              2 : untouched := 0;
              OTHERWISE : untouched := 1;
            END_CASE;
            RETURN(first_code(working.selected));
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION path_repeat_rewrite(
                     item : carrier;
                     replacement : value_choice;
                     repetitions : INTEGER) : INTEGER;
          LOCAL working : carrier := item; END_LOCAL;
          IF 'FLOW_FACT_LIFECYCLE_MODEL.FIRST_VALUE' IN TYPEOF(working.selected) THEN
            REPEAT index := 1 TO repetitions;
              working.selected := replacement;
            END_REPEAT;
            RETURN(first_code(working.selected));
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION opposite_branch_rewrite(
                     left, right, replacement : value_choice;
                     change : BOOLEAN) : INTEGER;
          IF 'FLOW_FACT_LIFECYCLE_MODEL.FIRST_VALUE' IN TYPEOF(left) THEN
            IF 'FLOW_FACT_LIFECYCLE_MODEL.FIRST_VALUE' IN TYPEOF(right) THEN
              IF change THEN
                left := replacement;
              ELSE
                right := replacement;
              END_IF;
              RETURN(first_code(left) + first_code(right));
            END_IF;
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION case_rewrite(item, replacement : value_choice; mode : INTEGER) : INTEGER;
          LOCAL untouched : INTEGER; END_LOCAL;
          IF 'FLOW_FACT_LIFECYCLE_MODEL.FIRST_VALUE' IN TYPEOF(item) THEN
            CASE mode OF
              1 : item := replacement;
              OTHERWISE : untouched := 0;
            END_CASE;
            RETURN(first_code(item));
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION aliased_root_write(left, right : carrier; replacement : value_choice) : INTEGER;
          LOCAL
            left_working : carrier := left;
            right_working : carrier := right;
          END_LOCAL;
          IF 'FLOW_FACT_LIFECYCLE_MODEL.FIRST_VALUE' IN TYPEOF(left_working.selected) THEN
            right_working.selected := replacement;
            RETURN(first_code(left_working.selected));
          END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION no_explicit_unknown(item, replacement : value_choice) : INTEGER;
          IF 'FLOW_FACT_LIFECYCLE_MODEL.FIRST_VALUE' IN TYPEOF(item) THEN
            item := replacement;
            RETURN(first_code(item));
          END_IF;
          RETURN(0);
        END_FUNCTION;
        FUNCTION immediate_return(input_value : INTEGER) : INTEGER;
          RETURN(input_value);
          input_value := input_value + 1;
          RETURN(input_value);
        END_FUNCTION;
        FUNCTION controls(first, second : value_choice; left, alias_value : carrier) : LOGICAL;
          RETURN(NOT EXISTS(direct_rewrite(first, second))
            AND NOT EXISTS(branch_rewrite(first, second, TRUE))
            AND NOT EXISTS(repeat_rewrite(first, second))
            AND NOT EXISTS(path_rewrite(left, second))
            AND NOT EXISTS(opposite_branch_rewrite(first, first, second, TRUE))
            AND NOT EXISTS(opposite_branch_rewrite(first, first, second, FALSE))
            AND NOT EXISTS(case_rewrite(first, second, 1))
            AND NOT EXISTS(aliased_root_write(alias_value, alias_value, second))
            AND NOT EXISTS(no_explicit_unknown(first, second))
            AND immediate_return(1) = 1);
        END_FUNCTION;
        ENTITY sample;
          first : value_choice;
          second : value_choice;
          path_left : carrier;
          branch_write : carrier;
          branch_no_write : carrier;
          case_write : carrier;
          case_nonwrite : carrier;
          case_otherwise : carrier;
          repeat_zero : carrier;
          repeat_one : carrier;
          alias_value : carrier;
        WHERE
          direct_fact : NOT EXISTS(direct_rewrite(first, second));
          branch_fact : NOT EXISTS(branch_rewrite(first, second, TRUE));
          repeat_fact : NOT EXISTS(repeat_rewrite(first, second));
          path_fact : NOT EXISTS(path_rewrite(path_left, second));
          path_branch_write : NOT EXISTS(path_branch_rewrite(branch_write, second, TRUE));
          path_branch_no_write : (path_branch_rewrite(branch_no_write, second, FALSE) = 7);
          path_case_write : NOT EXISTS(path_case_rewrite(case_write, second, 1));
          path_case_nonwrite : (path_case_rewrite(case_nonwrite, second, 2) = 7);
          path_case_otherwise : (path_case_rewrite(case_otherwise, second, 3) = 7);
          path_repeat_zero : (path_repeat_rewrite(repeat_zero, second, 0) = 7);
          path_repeat_one : NOT EXISTS(path_repeat_rewrite(repeat_one, second, 1));
          opposite_then_fact : NOT EXISTS(opposite_branch_rewrite(first, first, second, TRUE));
          opposite_else_fact : NOT EXISTS(opposite_branch_rewrite(first, first, second, FALSE));
          case_fact : NOT EXISTS(case_rewrite(first, second, 1));
          alias_fact : NOT EXISTS(aliased_root_write(alias_value, alias_value, second));
          no_explicit_unknown_fact : NOT EXISTS(no_explicit_unknown(first, second));
          top_return_fact : immediate_return(1) = 1;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string FLOW_FACT_LIFECYCLE_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.FlowFactLifecycleModel;

        internal static class FlowFactLifecycleConsumer
        {
            internal static ValidationResult Validate()
            {
                var firstValue = new FirstValue(new BigInteger(7));
                var secondValue = new SecondValue("other");
                var first = ValueChoice.FromFirstValue(firstValue);
                var second = ValueChoice.FromSecondValue(secondValue);
                var pathLeft = new Carrier(first);
                var branchWrite = new Carrier(first);
                var branchNoWrite = new Carrier(first);
                var caseWrite = new Carrier(first);
                var caseNonwrite = new Carrier(first);
                var caseOtherwise = new Carrier(first);
                var repeatZero = new Carrier(first);
                var repeatOne = new Carrier(first);
                var aliasValue = new Carrier(first);
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["flow fact lifecycle"], "3;1"),
                        new FileName("flow-fact.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["flow_fact_lifecycle_model"])),
                    [TedToolkit.Step21.Generated.FlowFactLifecycleModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("flow_fact_lifecycle_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, firstValue);
                _ = structure.Add(section, secondValue);
                _ = structure.Add(section, pathLeft);
                _ = structure.Add(section, aliasValue);
                _ = structure.Add(section, new Sample(
                    first,
                    second,
                    pathLeft,
                    branchWrite,
                    branchNoWrite,
                    caseWrite,
                    caseNonwrite,
                    caseOtherwise,
                    repeatZero,
                    repeatOne,
                    aliasValue));
                return structure.Validate();
            }
        }
        """;

    private const string UNPROTECTED_SELECT_APPLICATION_SCHEMA = """
        SCHEMA unprotected_select_application_model;
        ENTITY first_value;
          code : INTEGER;
        END_ENTITY;
        ENTITY second_value;
        END_ENTITY;
        TYPE value_choice = SELECT (first_value, second_value);
        END_TYPE;
        FUNCTION first_code(item : first_value) : INTEGER;
          RETURN(item.code);
        END_FUNCTION;
        FUNCTION unprotected(item : value_choice) : INTEGER;
          RETURN(first_code(item));
        END_FUNCTION;
        ENTITY sample;
          selected : value_choice;
        WHERE
          remains_a_type_error : unprotected(selected) > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string DYNAMIC_GENERIC_ARRAY_SCHEMA = """
        SCHEMA dynamic_generic_array_model;
        FUNCTION list_to_array(values : LIST [0:?] OF GENERIC:t;
                     low, high : INTEGER) : ARRAY OF GENERIC:t;
          LOCAL
            count : INTEGER;
            result : ARRAY [low:high] OF GENERIC:t;
          END_LOCAL;
          count := SIZEOF(values);
          IF count <> ((high - low) + 1) THEN RETURN(?);
          ELSE
            result := [values[1] : count];
            REPEAT index := 2 TO count BY 1;
              result[(low + index) - 1] := values[index];
            END_REPEAT;
            RETURN(result);
          END_IF;
        END_FUNCTION;
        FUNCTION forward_array(values : LIST [0:?] OF GENERIC:t;
                     low, high : INTEGER) : ARRAY OF GENERIC:t;
          LOCAL result : ARRAY [low:high] OF GENERIC:t; END_LOCAL;
          result := [values[1] : SIZEOF(values)];
          RETURN(list_to_array(values, low, high));
        END_FUNCTION;
        FUNCTION nested_array(values : LIST [1:?] OF LIST [0:?] OF GENERIC:t;
                     metadata : LIST [0:?] OF GENERIC:u;
                     low, high, inner_low, inner_high : INTEGER)
                     : ARRAY OF ARRAY OF GENERIC:t;
          LOCAL
            result : ARRAY [low:high] OF ARRAY [inner_low:inner_high] OF GENERIC:t;
          END_LOCAL;
          IF SIZEOF(values) <> ((high - low) + 1) THEN RETURN(?);
          END_IF;
          IF SIZEOF(metadata) < 0 THEN RETURN(?);
          END_IF;
          result := [list_to_array(values[1], inner_low, inner_high) : (high - low) + 1];
          RETURN(result);
        END_FUNCTION;
        FUNCTION array_size_control(values : LIST [2:2] OF INTEGER) : LOGICAL;
          RETURN(SIZEOF(forward_array(values, -1, 0)) = 2);
        END_FUNCTION;
        FUNCTION empty_array_control(bound : INTEGER) : LOGICAL;
          LOCAL empty : LIST [0:?] OF INTEGER := []; END_LOCAL;
          RETURN(NOT EXISTS(forward_array(empty, bound, bound)));
        END_FUNCTION;
        FUNCTION invalid_domain_control(low, high : INTEGER) : LOGICAL;
          LOCAL empty : LIST [0:?] OF INTEGER := []; END_LOCAL;
          RETURN(NOT EXISTS(forward_array(empty, low, high)));
        END_FUNCTION;
        FUNCTION nested_array_control(
                     values : LIST [1:1] OF LIST [2:2] OF INTEGER;
                     metadata : LIST [1:1] OF STRING) : LOGICAL;
          LOCAL
            result : ARRAY [0:0] OF ARRAY [-1:0] OF INTEGER;
          END_LOCAL;
          result := nested_array(values, metadata, 0, 0, -1, 0);
          RETURN(SIZEOF(result) = 1);
        END_FUNCTION;
        ENTITY sample;
          values : LIST [2:2] OF INTEGER;
          nested_values : LIST [1:1] OF LIST [2:2] OF INTEGER;
          nested_metadata : LIST [1:1] OF STRING;
        WHERE
          complete : array_size_control(values);
          empty_is_unknown : empty_array_control(0);
          invalid_domain_is_unknown : invalid_domain_control(1, 0);
          nested_complete : nested_array_control(nested_values, nested_metadata);
        END_ENTITY;
        ENTITY nested_derived_sample;
          derived_values : LIST [1:1] OF LIST [2:2] OF INTEGER;
          derived_metadata : LIST [1:1] OF STRING;
        DERIVE
          derived_result : ARRAY [0:0] OF ARRAY [-1:0] OF INTEGER :=
            nested_array(derived_values, derived_metadata, 0, 0, -1, 0);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string DYNAMIC_GENERIC_ARRAY_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.DynamicGenericArrayModel;

        internal static class DynamicGenericArrayConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["dynamic generic array"], "3;1"),
                        new FileName("dynamic-generic-array.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["dynamic_generic_array_model"])),
                    [TedToolkit.Step21.Generated.DynamicGenericArrayModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("dynamic_generic_array_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(
                    new ExpressList<BigInteger>(2, 2) { 11, 13 },
                    new ExpressList<ExpressList<BigInteger>>(1, 1)
                    {
                        new ExpressList<BigInteger>(2, 2) { 17, 19 },
                    },
                    new ExpressList<string>(1, 1) { "metadata" }));
                return structure.Validate();
            }
        }
        """;

    private const string EXPECTED_GENERIC_BAG_SCHEMA = """
        SCHEMA expected_generic_bag_model;
        FUNCTION bag_to_set(values : BAG OF GENERIC:t) : SET OF GENERIC:t;
          LOCAL
            result : SET OF GENERIC:t := [];
          END_LOCAL;
          IF SIZEOF(values) > 0 THEN
            REPEAT index := 1 TO HIINDEX(values) BY 1;
              result := result + values[index];
            END_REPEAT;
          END_IF;
          RETURN(result);
        END_FUNCTION;
        FUNCTION different_bag(values : BAG [0:?] OF INTEGER) : INTEGER;
          LOCAL other_values : BAG [0:?] OF INTEGER := []; END_LOCAL;
          REPEAT index := 1 TO HIINDEX(values) BY 1;
            RETURN(other_values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION different_lower(values : BAG [0:?] OF INTEGER) : INTEGER;
          REPEAT index := 0 TO HIINDEX(values) BY 1;
            RETURN(values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION different_step(values : BAG [0:?] OF INTEGER) : INTEGER;
          REPEAT index := 1 TO HIINDEX(values) BY 2;
            RETURN(values[index]);
          END_REPEAT;
          RETURN(?);
        END_FUNCTION;
        FUNCTION outside_repeat(values : BAG [0:?] OF INTEGER) : INTEGER;
          RETURN(values[1]);
        END_FUNCTION;
        FUNCTION has_typed_user(candidate : target) : LOGICAL;
          LOCAL
            users : SET OF owner;
            narrowed : SET OF owner;
          END_LOCAL;
          users := bag_to_set(USEDIN(candidate, 'EXPECTED_GENERIC_BAG_MODEL.OWNER.ITEM'));
          narrowed := QUERY (user <* bag_to_set(USEDIN(candidate, '')) |
            ('EXPECTED_GENERIC_BAG_MODEL.OWNER' IN TYPEOF(user)));
          RETURN((SIZEOF(users) = 1) AND (SIZEOF(narrowed) = 1)
            AND (SIZEOF(QUERY (user <* bag_to_set(USEDIN(candidate, '')) |
              ('EXPECTED_GENERIC_BAG_MODEL.OWNER' IN TYPEOF(user)))) = 1)
            AND (SIZEOF(QUERY (user <* bag_to_set(USEDIN(candidate,
              'EXPECTED_GENERIC_BAG_MODEL.' + 'OWNER.ITEM')) |
              ('EXPECTED_GENERIC_BAG_MODEL.OWNER' IN TYPEOF(user)))) = 1));
        END_FUNCTION;
        FUNCTION has_no_typed_user(candidate : target) : LOGICAL;
          LOCAL users : SET OF owner; END_LOCAL;
          users := bag_to_set(USEDIN(candidate, 'EXPECTED_GENERIC_BAG_MODEL.OWNER.ITEM'));
          RETURN(SIZEOF(users) = 0);
        END_FUNCTION;
        ENTITY target; END_ENTITY;
        ENTITY owner;
          item : target;
        END_ENTITY;
        ENTITY other;
          item : target;
        END_ENTITY;
        ENTITY check;
          candidate : target;
        WHERE
          typed_user : has_typed_user(candidate);
        END_ENTITY;
        ENTITY empty_check;
          candidate : target;
        WHERE
          no_typed_user : has_no_typed_user(candidate);
        END_ENTITY;
        ENTITY bag_control;
          values : BAG [0:?] OF INTEGER;
        WHERE
          different_aggregate : NOT EXISTS(different_bag(values));
          non_unit_lower : NOT EXISTS(different_lower(values));
          non_unit_step : NOT EXISTS(different_step(values));
          escaped_scope : NOT EXISTS(outside_repeat(values));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string EXPECTED_GENERIC_BAG_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.ExpectedGenericBagModel;

        internal static class ExpectedGenericBagConsumer
        {
            internal static ValidationResult ValidateTypedUsers()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["expected generic bag"], "3;1"),
                        new FileName("expected-generic-bag.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["expected_generic_bag_model"])),
                    [TedToolkit.Step21.Generated.ExpectedGenericBagModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("expected_generic_bag_model"));
                structure.DataSections.Add(section);
                var target = new Target();
                var emptyTarget = new Target();
                _ = structure.Add(section, target);
                _ = structure.Add(section, emptyTarget);
                _ = structure.Add(section, new Owner(target));
                _ = structure.Add(section, new Other(target));
                _ = structure.Add(section, new Check(target));
                _ = structure.Add(section, new EmptyCheck(emptyTarget));
                return structure.Validate();
            }

            internal static ValidationResult ValidateConservativeControls()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["bag index controls"], "3;1"),
                        new FileName("bag-index-controls.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["expected_generic_bag_model"])),
                    [TedToolkit.Step21.Generated.ExpectedGenericBagModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("expected_generic_bag_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new BagControl(new ExpressBag<BigInteger>(0)));
                return structure.Validate();
            }
        }
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

    private const string REPEATED_CYCLIC_DERIVED_DEPENDENCY_SCHEMA = """
        SCHEMA repeated_cyclic_derived_model;
        ENTITY sample;
          seed : INTEGER;
        DERIVE
          dimensions : INTEGER := derive_dimensions(SELF);
        WHERE
          dimensions_match : dimensions = seed;
        END_ENTITY;
        FUNCTION derive_dimensions(item : sample) : INTEGER;
          IF item.seed = 0 THEN
            RETURN(0);
          END_IF;
          RETURN(item.dimensions);
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string CYCLIC_DERIVED_DEPENDENCY_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.RepeatedCyclicDerivedModel;

        internal static class CyclicDerivedDependencyConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["derived-recursion"], "3;1"),
                        new FileName(
                            "derived-recursion.step",
                            "2026-08-28T00:00:00+08:00",
                            [],
                            [],
                            "tests",
                            "tests",
                            ""),
                        new FileSchema(["repeated_cyclic_derived_model"])),
                    [TedToolkit.Step21.Generated.RepeatedCyclicDerivedModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("repeated_cyclic_derived_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(BigInteger.Zero));
                return structure.Validate();
            }
        }
        """;

    private const string ALGORITHM_CONTROL_SCHEMA = """
        SCHEMA algorithm_control_model;
        FUNCTION sum_while(input : INTEGER) : INTEGER;
          LOCAL
            remaining : INTEGER := input;
            result : INTEGER := 0;
          END_LOCAL;
          REPEAT WHILE remaining > 0;
            result := result + remaining;
            remaining := remaining - 1;
          END_REPEAT;
          RETURN(result);
        END_FUNCTION;
        FUNCTION classify(input : INTEGER) : BOOLEAN;
          IF input < 0 THEN
            RETURN(FALSE);
          END_IF;
          CASE input OF
            0 : RETURN(TRUE);
            1 : RETURN(TRUE);
          END_CASE;
        END_FUNCTION;
        FUNCTION outer_check(values : LIST [1:?] OF INTEGER) : BOOLEAN;
          FUNCTION all_positive(items : LIST [1:?] OF INTEGER) : BOOLEAN;
            REPEAT i := 1 TO SIZEOF(items);
              IF items[i] <= 0 THEN
                RETURN(FALSE);
              END_IF;
            END_REPEAT;
            RETURN(TRUE);
          END_FUNCTION;
          RETURN(all_positive(values));
        END_FUNCTION;
        FUNCTION repeat_shadow(values : LIST [1:?] OF INTEGER) : INTEGER;
          LOCAL
            i : INTEGER := 99;
          END_LOCAL;
          REPEAT i := 1 TO SIZEOF(values);
            IF i < 0 THEN
              RETURN(0);
            END_IF;
          END_REPEAT;
          RETURN(i);
        END_FUNCTION;
        ENTITY sample;
          values : LIST [1:?] OF INTEGER;
        WHERE
          while_control : sum_while(3) = 6;
          guarded_case : classify(1);
          nested_function : outer_check(values);
          repeat_scope : repeat_shadow(values) = 99;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ALGORITHM_CONTROL_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.AlgorithmControlModel;

        internal static class AlgorithmControlConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["algorithm-control"], "3;1"),
                        new FileName(
                            "algorithm-control.step",
                            "2026-08-28T00:00:00+08:00",
                            [],
                            [],
                            "tests",
                            "tests",
                            ""),
                        new FileSchema(["algorithm_control_model"])),
                    [TedToolkit.Step21.Generated.AlgorithmControlModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("algorithm_control_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(
                    new ExpressList<BigInteger>(1) { BigInteger.One, new BigInteger(2) }));
                return structure.Validate();
            }
        }
        """;

    private const string SELF_RECURSIVE_FUNCTION_SCHEMA = """
        SCHEMA self_recursive_model;
        FUNCTION countdown(input_value : INTEGER) : BOOLEAN;
          IF input_value = 0 THEN
            RETURN(TRUE);
          END_IF;
          IF input_value < 0 THEN
            RETURN(FALSE);
          END_IF;
          RETURN(countdown(input_value - 1));
        END_FUNCTION;
        ENTITY sample;
          amount : INTEGER;
        WHERE
          reaches_zero : countdown(amount);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELF_RECURSIVE_FUNCTION_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SelfRecursiveModel;

        internal static class SelfRecursiveFunctionConsumer
        {
            internal static ValidationResult Validate(BigInteger amount)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["recursive"], "3;1"),
                        new FileName("recursive.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["self_recursive_model"])),
                    [TedToolkit.Step21.Generated.SelfRecursiveModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("self_recursive_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(amount));
                return structure.Validate();
            }
        }
        """;

    private const string MUTUALLY_RECURSIVE_FUNCTION_SCHEMA = """
        SCHEMA mutually_recursive_model;
        FUNCTION is_even(input_value : INTEGER) : BOOLEAN;
          IF input_value = 0 THEN
            RETURN(TRUE);
          END_IF;
          RETURN(is_odd(input_value - 1));
        END_FUNCTION;
        FUNCTION is_odd(input_value : INTEGER) : BOOLEAN;
          IF input_value = 0 THEN
            RETURN(FALSE);
          END_IF;
          RETURN(is_even(input_value - 1));
        END_FUNCTION;
        ENTITY sample;
          amount : INTEGER;
        WHERE
          even_amount : is_even(amount);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string MUTUALLY_RECURSIVE_FUNCTION_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.MutuallyRecursiveModel;

        internal static class MutuallyRecursiveFunctionConsumer
        {
            internal static ValidationResult Validate(BigInteger amount)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["mutual"], "3;1"),
                        new FileName("mutual.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["mutually_recursive_model"])),
                    [TedToolkit.Step21.Generated.MutuallyRecursiveModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("mutually_recursive_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(amount));
                return structure.Validate();
            }
        }
        """;

    private const string MIXED_RECURSIVE_DEPENDENCY_SCHEMA = """
        SCHEMA mixed_recursive_model;
        CONSTANT
          recursive_value : INTEGER := recursive_function(1);
        END_CONSTANT;
        FUNCTION recursive_function(input_value : INTEGER) : INTEGER;
          RETURN(recursive_value + input_value);
        END_FUNCTION;
        ENTITY sample;
          amount : INTEGER;
        WHERE
          positive : recursive_function(amount) > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string MODEL_CONTEXT_SCHEMA = """
        SCHEMA model_context_model;
        ENTITY target;
        INVERSE
          users : SET [0:?] OF owner FOR item;
        WHERE
          has_type : 'MODEL_CONTEXT_MODEL.TARGET' IN TYPEOF(SELF);
          has_role : 'MODEL_CONTEXT_MODEL.OWNER.ITEM' IN ROLESOF(SELF);
          has_user : SIZEOF(USEDIN(SELF, 'MODEL_CONTEXT_MODEL.OWNER.ITEM')) = SIZEOF(users);
        END_ENTITY;
        ENTITY owner;
          item : target;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string MODEL_CONTEXT_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.ModelContextModel;

        internal static class ModelContextConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["model context"], "3;1"),
                        new FileName("model.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["model_context_model"])),
                    [TedToolkit.Step21.Generated.ModelContextModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("model_context_model"));
                structure.DataSections.Add(section);
                var target = new Target();
                _ = structure.Add(section, target);
                _ = structure.Add(section, new Owner(target));
                return structure.Validate();
            }
        }
        """;

    private const string SINGULAR_INVERSE_SCHEMA = """
        SCHEMA singular_inverse_model;
        ENTITY target;
          code : INTEGER;
        INVERSE
          single_owner : owner FOR targets;
        WHERE
          direct_access : single_owner.rank > 0;
          repeated_access : (single_owner.rank > 0) AND (single_owner.rank > 0);
          function_access : owner_rank(SELF) > 0;
          identity_access : SELF :=: single_owner.targets[1];
          independent_rule : code > 0;
        END_ENTITY;
        ENTITY owner;
          rank : INTEGER;
          targets : LIST [0:?] OF target;
        END_ENTITY;
        ENTITY carrier;
          related : target;
        WHERE
          related_inverse : related.single_owner.rank > 0;
        END_ENTITY;
        ENTITY lazy_target;
        INVERSE
          single_owner : lazy_owner FOR targets;
        WHERE
          true_short_circuit : TRUE OR (single_owner.rank > 0);
          false_short_circuit : FALSE AND (single_owner.rank > 0);
        END_ENTITY;
        ENTITY lazy_owner;
          rank : INTEGER;
          targets : LIST [0:?] OF lazy_target;
        END_ENTITY;
        FUNCTION owner_rank(candidate : target) : INTEGER;
          RETURN(candidate.single_owner.rank);
        END_FUNCTION;
        END_SCHEMA;
        """;

    private const string SINGULAR_INVERSE_CONSUMER = """"
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Linq;
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SingularInverseModel;

        internal static class SingularInverseConsumer
        {
            internal static int OwnerQueryCount { get; private set; }

            internal static int LazyOwnerQueryCount { get; private set; }

            internal static ValidationResult Validate(int ownerCount, bool repeatOccurrence, BigInteger code)
            {
                var structure = CreateStructure();
                var section = structure.DataSections[0];
                var target = new Target(code);
                _ = structure.Add(section, target);
                for (var index = 0; index < ownerCount; index++)
                {
                    var targets = new ExpressList<ITarget>(0) { target };
                    if (repeatOccurrence)
                    {
                        targets.Add(target);
                    }

                    _ = structure.Add(section, new Owner(BigInteger.One, targets));
                }

                return structure.Validate();
            }

            internal static ValidationResult ValidateCountingOwner()
            {
                OwnerQueryCount = 0;
                var structure = CreateStructure();
                var section = structure.DataSections[0];
                var target = new Target(BigInteger.One);
                _ = structure.Add(section, target);
                _ = structure.Add(section, new CountingOwner(target));
                return structure.Validate();
            }

            internal static ValidationResult ValidateLazy()
            {
                var structure = CreateStructure();
                LazyOwnerQueryCount = 0;
                var target = new LazyTarget();
                _ = structure.Add(structure.DataSections[0], target);
                _ = structure.Add(structure.DataSections[0], new CountingLazyOwner(target));
                return structure.Validate();
            }

            internal static ValidationResult WriteZero()
            {
                var structure = CreateStructure();
                _ = structure.Add(structure.DataSections[0], new Target(BigInteger.One));
                var output = new StringWriter();
                try
                {
                    structure.Write(output);
                    throw new InvalidOperationException("Invalid singular inverse write unexpectedly succeeded.");
                }
                catch (ExchangeStructureWriteValidationException exception)
                {
                    if (output.ToString().Length != 0)
                    {
                        throw new InvalidOperationException("Invalid singular inverse write produced output.");
                    }

                    return exception.ValidationResult;
                }
            }

            internal static ValidationResult ReadZero()
            {
                const string source = """
                    ISO-10303-21;
                    HEADER;
                    FILE_DESCRIPTION(('singular inverse'),'3;1');
                    FILE_NAME('singular.step','2026-08-27T00:00:00', (), (),'tests','tests','');
                    FILE_SCHEMA(('singular_inverse_model'));
                    ENDSEC;
                    DATA;
                    #1=TARGET(1);
                    ENDSEC;
                    END-ISO-10303-21;
                    """;
                try
                {
                    _ = ExchangeStructure.Read(
                        new StringReader(source),
                        [TedToolkit.Step21.Generated.SingularInverseModel.SchemaDescriptor.Instance]);
                    throw new InvalidOperationException("Invalid singular inverse read unexpectedly succeeded.");
                }
                catch (ExchangeStructureReadValidationException exception)
                {
                    return exception.ValidationResult;
                }
            }

            internal static ValidationResult WriteMany()
            {
                var structure = CreateStructure();
                var section = structure.DataSections[0];
                var target = new Target(BigInteger.One);
                _ = structure.Add(section, target);
                _ = structure.Add(section, new Owner(BigInteger.One, new ExpressList<ITarget>(0) { target }));
                _ = structure.Add(section, new Owner(BigInteger.One, new ExpressList<ITarget>(0) { target }));
                var output = new StringWriter();
                try
                {
                    structure.Write(output);
                    throw new InvalidOperationException("Invalid singular inverse write unexpectedly succeeded.");
                }
                catch (ExchangeStructureWriteValidationException exception)
                {
                    if (output.ToString().Length != 0)
                    {
                        throw new InvalidOperationException("Invalid singular inverse write produced output.");
                    }

                    return exception.ValidationResult;
                }
            }

            internal static ValidationResult ReadMany()
            {
                const string source = """
                    ISO-10303-21;
                    HEADER;
                    FILE_DESCRIPTION(('singular inverse'),'3;1');
                    FILE_NAME('singular.step','2026-08-27T00:00:00', (), (),'tests','tests','');
                    FILE_SCHEMA(('singular_inverse_model'));
                    ENDSEC;
                    DATA;
                    #1=TARGET(1);
                    #2=OWNER(1,(#1));
                    #3=OWNER(1,(#1));
                    ENDSEC;
                    END-ISO-10303-21;
                    """;
                try
                {
                    _ = ExchangeStructure.Read(
                        new StringReader(source),
                        [TedToolkit.Step21.Generated.SingularInverseModel.SchemaDescriptor.Instance]);
                    throw new InvalidOperationException("Invalid singular inverse read unexpectedly succeeded.");
                }
                catch (ExchangeStructureReadValidationException exception)
                {
                    return exception.ValidationResult;
                }
            }

            internal static ValidationResult ValidateUnregistered(int ownerCount)
            {
                return CreateUnregisteredStructure(ownerCount).Validate();
            }

            internal static ValidationResult WriteUnregistered(int ownerCount)
            {
                var structure = CreateUnregisteredStructure(ownerCount);
                var output = new StringWriter();
                try
                {
                    structure.Write(output);
                    throw new InvalidOperationException("Invalid unregistered inverse write unexpectedly succeeded.");
                }
                catch (ExchangeStructureWriteValidationException exception)
                {
                    if (output.ToString().Length != 0)
                    {
                        throw new InvalidOperationException("Invalid unregistered inverse write produced output.");
                    }

                    return exception.ValidationResult;
                }
            }

            private static ExchangeStructure CreateUnregisteredStructure(int ownerCount)
            {
                var structure = CreateStructure();
                var section = structure.DataSections[0];
                var target = new Target(BigInteger.One);
                _ = structure.Add(section, new Carrier(target));
                for (var index = 0; index < ownerCount; index++)
                {
                    _ = structure.Add(
                        section,
                        new Owner(BigInteger.One, new ExpressList<ITarget>(0) { target }));
                }

                _ = structure.Remove(target);
                return structure;
            }

            private static ExchangeStructure CreateStructure()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["singular inverse"], "3;1"),
                        new FileName("singular.step", "2026-08-27T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["singular_inverse_model"])),
                    [TedToolkit.Step21.Generated.SingularInverseModel.SchemaDescriptor.Instance]);
                structure.DataSections.Add(new DataSection(new SchemaName("singular_inverse_model")));
                return structure;
            }

            private sealed class CountingOwner : Entity, IOwner
            {
                private readonly ExpressList<ITarget> targets;

                internal CountingOwner(ITarget target)
                {
                    targets = new ExpressList<ITarget>(0) { target, target };
                }

                public BigInteger Rank => BigInteger.One;

                public ExpressList<ITarget> Targets
                {
                    get
                    {
                        OwnerQueryCount++;
                        return targets;
                    }
                }

                public override IEnumerable<Entity> DirectReferences => targets.Cast<Entity>();
            }

            private sealed class CountingLazyOwner : Entity, ILazyOwner
            {
                private readonly ExpressList<ILazyTarget> targets;

                internal CountingLazyOwner(ILazyTarget target)
                {
                    targets = new ExpressList<ILazyTarget>(0) { target };
                }

                public BigInteger Rank => BigInteger.One;

                public ExpressList<ILazyTarget> Targets
                {
                    get
                    {
                        LazyOwnerQueryCount++;
                        return targets;
                    }
                }

                public override IEnumerable<Entity> DirectReferences => targets.Cast<Entity>();
            }
        }
        """";

    private const string SINGULAR_INVERSE_ORDER_SCHEMA = """
        SCHEMA singular_inverse_order_model;
        ENTITY order_target;
          code : INTEGER;
          values : LIST [1:1] OF INTEGER;
        INVERSE
          single_owner : order_owner FOR targets;
        UNIQUE
          unique_code : code;
        WHERE
          inverse_rule : single_owner.rank > 0;
          later_where : code > 0;
        END_ENTITY;
        ENTITY order_owner;
          rank : INTEGER;
          targets : LIST [0:?] OF order_target;
        END_ENTITY;
        RULE population_rule FOR (order_target);
        WHERE
          global_failure : SIZEOF(order_target) > 100;
        END_RULE;
        END_SCHEMA;
        """;

    private const string SINGULAR_INVERSE_ORDER_CONSUMER = """
        using System;
        using System.IO;
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.SingularInverseOrderModel;

        internal static class SingularInverseOrderConsumer
        {
            internal static ValidationResult[] ValidateTwice()
            {
                var structure = CreateInvalidStructure();
                return [structure.Validate(), structure.Validate()];
            }

            internal static ValidationResult Write()
            {
                var structure = CreateInvalidStructure();
                var output = new StringWriter();
                try
                {
                    structure.Write(output);
                    throw new InvalidOperationException("Invalid mixed failure write unexpectedly succeeded.");
                }
                catch (ExchangeStructureWriteValidationException exception)
                {
                    if (output.ToString().Length != 0)
                    {
                        throw new InvalidOperationException("Invalid mixed failure write produced output.");
                    }

                    return exception.ValidationResult;
                }
            }

            private static ExchangeStructure CreateInvalidStructure()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["mixed order"], "3;1"),
                        new FileName("mixed.step", "2026-08-27T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["singular_inverse_order_model"])),
                    [TedToolkit.Step21.Generated.SingularInverseOrderModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("singular_inverse_order_model"));
                structure.DataSections.Add(section);

                var missingOwner = new OrderTarget(-BigInteger.One, new ExpressList<BigInteger>(1, 1));
                var uniqueOwner = new OrderTarget(
                    -BigInteger.One,
                    new ExpressList<BigInteger>(1, 1) { BigInteger.One });
                var unregistered = new OrderTarget(
                    BigInteger.One,
                    new ExpressList<BigInteger>(1, 1) { BigInteger.One });
                _ = structure.Add(section, missingOwner);
                _ = structure.Add(section, uniqueOwner);
                _ = structure.Add(
                    section,
                    new OrderOwner(
                        BigInteger.One,
                        new ExpressList<IOrderTarget>(0) { uniqueOwner, unregistered }));
                _ = structure.Remove(unregistered);
                return structure;
            }
        }
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

    private const string COMPLEX_CONSTRUCTOR_SCHEMA = """
        SCHEMA complex_constructor_model;
        CONSTANT
          marker_value : marker := base_part('marker') || marker();
          deep_value : deepest := base_part('deep') || middle(2) || deepest(TRUE);
        END_CONSTANT;
        TYPE label_value = STRING;
        END_TYPE;
        TYPE real_measure = REAL;
        END_TYPE;
        ENTITY base_part;
          label : label_value;
        END_ENTITY;
        ENTITY marker SUBTYPE OF (base_part);
        END_ENTITY;
        ENTITY leaf SUBTYPE OF (base_part);
          amount : real_measure;
        END_ENTITY;
        ENTITY middle SUBTYPE OF (base_part);
          code : INTEGER;
        END_ENTITY;
        ENTITY deepest SUBTYPE OF (middle);
          enabled : BOOLEAN;
        END_ENTITY;
        FUNCTION attach(existing : base_part; amount : INTEGER) : leaf;
          RETURN(existing || leaf(amount));
        END_FUNCTION;
        FUNCTION attach_sqrt(existing : base_part; magnitude : REAL) : leaf;
          RETURN(existing || leaf(SQRT(magnitude)));
        END_FUNCTION;
        ENTITY sample;
          source : base_part;
        WHERE
          marker_path : marker_value.label = 'marker';
          existing_path : (attach(source, 5).label = 'source') AND (attach(source, 5).amount = 5);
          sqrt_present : attach_sqrt(source, 9).amount = 3;
          sqrt_unknown : NOT EXISTS(attach_sqrt(source, -1));
          deep_path : (deep_value.label = 'deep') AND (deep_value.code = 2) AND deep_value.enabled;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string COMPLEX_CONSTRUCTOR_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.ComplexConstructorModel;

        internal static class ComplexConstructorConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["complex constructor"], "3;1"),
                        new FileName("complex-constructor.step", "2026-08-24T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["complex_constructor_model"])),
                    [TedToolkit.Step21.Generated.ComplexConstructorModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("complex_constructor_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(new BasePart(new LabelValue("source"))));
                _ = structure.Add(
                    section,
                    new Leaf(
                        new LabelValue("full"),
                        new RealMeasure(new RealValue(9, 0))));
                return structure.Validate();
            }
        }
        """;

    private const string MISSING_COMPLEX_SLOT_SCHEMA = """
        SCHEMA missing_complex_slot_model;
        ENTITY left_part;
          left_value : STRING;
        END_ENTITY;
        ENTITY right_part;
          right_value : INTEGER;
        END_ENTITY;
        ENTITY combined SUBTYPE OF (left_part, right_part);
        END_ENTITY;
        FUNCTION incomplete(existing : left_part) : combined;
          RETURN(existing || combined());
        END_FUNCTION;
        ENTITY sample;
          source : left_part;
        WHERE
          rejected : EXISTS(incomplete(source));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SAME_NAME_COMPLEX_SLOT_SCHEMA = """
        SCHEMA same_name_complex_slot_model;
        ENTITY left_part;
          shared_name : STRING;
        END_ENTITY;
        ENTITY right_part;
          shared_name : INTEGER;
        END_ENTITY;
        ENTITY combined SUBTYPE OF (left_part, right_part);
        END_ENTITY;
        FUNCTION incomplete(existing : left_part) : combined;
          RETURN(existing || combined());
        END_FUNCTION;
        ENTITY sample;
          source : left_part;
        WHERE
          rejected : EXISTS(incomplete(source));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ATTRIBUTE_REDECLARATION_SCHEMA = """
        SCHEMA attribute_redeclaration_model;
        ENTITY vertex;
        END_ENTITY;
        ENTITY edge SUPERTYPE OF (ONEOF (oriented_edge));
          edge_start : vertex;
        END_ENTITY;
        ENTITY oriented_edge SUBTYPE OF (edge);
          edge_element : edge;
        DERIVE
          SELF\edge.edge_start : vertex := edge_element.edge_start;
        END_ENTITY;
        TYPE edge_choice = SELECT (edge, oriented_edge);
        END_TYPE;
        ENTITY holder;
          direct_edge : oriented_edge;
          edges : LIST [1:?] OF oriented_edge;
          base_edge : edge;
          selected_edge : edge_choice;
        WHERE
          direct_derived : EXISTS(direct_edge.edge_start);
          first_indexed_derived : EXISTS(edges[1].edge_start);
          last_indexed_derived : EXISTS(edges[HIINDEX(edges)].edge_start);
          base_explicit : EXISTS(base_edge.edge_start);
          selected_alternative : EXISTS(selected_edge.edge_start);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string UNRELATED_ATTRIBUTE_COLLISION_SCHEMA = """
        SCHEMA unrelated_attribute_collision_model;
        ENTITY left_part;
          amount : INTEGER;
        END_ENTITY;
        ENTITY right_part;
          amount : INTEGER;
        END_ENTITY;
        ENTITY combined SUBTYPE OF (left_part, right_part);
        END_ENTITY;
        ENTITY holder;
          item : combined;
        WHERE
          ambiguous : item.amount > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ENTITY_AGGREGATE_LUB_SCHEMA = """
        SCHEMA entity_aggregate_lub_model;
        ENTITY root;
          code : INTEGER;
        END_ENTITY;
        ENTITY left_item SUBTYPE OF (root);
        END_ENTITY;
        ENTITY right_item SUBTYPE OF (root);
        END_ENTITY;
        ENTITY leaf_item SUBTYPE OF (left_item);
        END_ENTITY;
        ENTITY unrelated_left;
          code : INTEGER;
        END_ENTITY;
        ENTITY unrelated_right;
          code : INTEGER;
        END_ENTITY;
        FUNCTION derived_base(values : SET OF leaf_item;
                              roots : SET OF root) : SET OF root;
          RETURN(values + roots);
        END_FUNCTION;
        FUNCTION sibling_union(left_values : SET OF left_item;
                               right_values : SET OF right_item) : SET OF root;
          RETURN(left_values + right_values);
        END_FUNCTION;
        FUNCTION ordered_union(values : LIST OF leaf_item;
                               roots : LIST OF root) : LIST OF root;
          RETURN(values + roots);
        END_FUNCTION;
        FUNCTION append_base(values : SET OF leaf_item;
                             item_value : root) : SET OF root;
          RETURN(values + item_value);
        END_FUNCTION;
        FUNCTION prepend_base(item_value : root;
                              values : SET OF leaf_item) : SET OF root;
          RETURN(item_value + values);
        END_FUNCTION;
        FUNCTION append_ordered(values : LIST OF leaf_item;
                                item_value : root) : LIST OF root;
          RETURN(values + item_value);
        END_FUNCTION;
        ENTITY sample;
          derived_values : SET [1:?] OF leaf_item;
          root_values : SET [1:?] OF root;
          left_values : SET [1:?] OF left_item;
          right_values : SET [1:?] OF right_item;
          ordered_values : LIST [1:?] OF leaf_item;
          ordered_roots : LIST [1:?] OF root;
          unrelated_left_values : BAG [1:?] OF unrelated_left;
          unrelated_right_values : BAG [1:?] OF unrelated_right;
          scalar_root : root;
        WHERE
          derived_distinct : SIZEOF(derived_base(derived_values, root_values)) = 1;
          sibling_common_root : SIZEOF(sibling_union(left_values, right_values)) = 2;
          ordered_common_root : (ordered_union(ordered_values, ordered_roots)[1].code = 1)
            AND (ordered_union(ordered_values, ordered_roots)[2].code = 2);
          unrelated_entity_root : SIZEOF(unrelated_left_values + unrelated_right_values) = 4;
          append_common_root : SIZEOF(append_base(derived_values, scalar_root)) = 2;
          prepend_common_root : SIZEOF(prepend_base(scalar_root, derived_values)) = 2;
          append_order_preserved : (append_ordered(ordered_values, scalar_root)[1].code = 1)
            AND (append_ordered(ordered_values, scalar_root)[2].code = 2);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ENTITY_AGGREGATE_LUB_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.EntityAggregateLubModel;

        internal static class EntityAggregateLubConsumer
        {
            internal static ValidationResult Validate()
            {
                var leaf = new LeafItem(BigInteger.One);
                var right = new RightItem(new BigInteger(2));
                var unrelatedLeft = new UnrelatedLeft(BigInteger.One);
                var unrelatedRight = new UnrelatedRight(new BigInteger(2));
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["entity aggregate lub"], "3;1"),
                        new FileName("entity-aggregate-lub.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["entity_aggregate_lub_model"])),
                    [TedToolkit.Step21.Generated.EntityAggregateLubModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("entity_aggregate_lub_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, leaf);
                _ = structure.Add(section, right);
                _ = structure.Add(section, unrelatedLeft);
                _ = structure.Add(section, unrelatedRight);
                _ = structure.Add(section, new Sample(
                    new ExpressSet<ILeafItem>(1) { leaf },
                    new ExpressSet<IRoot>(1) { leaf },
                    new ExpressSet<ILeftItem>(1) { leaf },
                    new ExpressSet<IRightItem>(1) { right },
                    new ExpressList<ILeafItem>(1) { leaf },
                    new ExpressList<IRoot>(1) { right },
                    new ExpressBag<IUnrelatedLeft>(1) { unrelatedLeft, unrelatedLeft },
                    new ExpressBag<IUnrelatedRight>(1) { unrelatedRight, unrelatedRight },
                    right));
                return structure.Validate();
            }
        }
        """;

    private const string ENTITY_AGGREGATE_LUB_AMBIGUOUS = """
        SCHEMA entity_aggregate_lub_ambiguous;
        ENTITY first_root; END_ENTITY;
        ENTITY second_root; END_ENTITY;
        ENTITY left_item SUBTYPE OF (first_root, second_root); END_ENTITY;
        ENTITY right_item SUBTYPE OF (first_root, second_root); END_ENTITY;
        ENTITY sample;
          left_values : SET OF left_item;
          right_values : SET OF right_item;
        WHERE
          ambiguous : SIZEOF(left_values + right_values) = 2;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ENTITY_AGGREGATE_LUB_INVALID_ASSIGNMENT = """
        SCHEMA entity_aggregate_lub_invalid_assignment;
        ENTITY left_item; END_ENTITY;
        ENTITY right_item; END_ENTITY;
        FUNCTION invalid_union(left_values : SET OF left_item;
                               right_values : SET OF right_item) : SET OF left_item;
          RETURN(left_values + right_values);
        END_FUNCTION;
        ENTITY sample;
          left_values : SET OF left_item;
          right_values : SET OF right_item;
        WHERE
          reachable : SIZEOF(invalid_union(left_values, right_values)) = 2;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string AGGREGATE_SELECT_UNION_SCHEMA = """
        SCHEMA aggregate_select_union_model;
        ENTITY base_item;
          code : INTEGER;
        END_ENTITY;
        ENTITY child_item SUBTYPE OF (base_item);
        END_ENTITY;
        TYPE item_choice = SELECT (child_item);
        END_TYPE;
        TYPE nested_choice = SELECT (item_choice);
        END_TYPE;
        FUNCTION union_forward(entities : SET OF child_item;
                               selections : SET OF item_choice) : SET OF child_item;
          RETURN(entities + selections);
        END_FUNCTION;
        FUNCTION union_reverse(entities : SET OF child_item;
                               selections : SET OF item_choice) : SET OF item_choice;
          RETURN(selections + entities);
        END_FUNCTION;
        FUNCTION union_nested(entities : SET OF child_item;
                              selections : SET OF nested_choice) : SET OF child_item;
          RETURN(entities + selections);
        END_FUNCTION;
        FUNCTION repeat_list(values : LIST OF child_item) : LIST OF child_item;
          RETURN(values + values);
        END_FUNCTION;
        ENTITY sample;
          entities : SET [1:?] OF child_item;
          selections : SET [1:?] OF item_choice;
          nested_selections : SET [1:?] OF nested_choice;
          ordered : LIST [1:?] OF child_item;
          duplicates : BAG [1:?] OF child_item;
        WHERE
          forward_union : SIZEOF(union_forward(entities, selections)) = 2;
          reverse_union : SIZEOF(union_reverse(entities, selections)) = 2;
          nested_union : SIZEOF(union_nested(entities, nested_selections)) = 2;
          same_entity : SIZEOF(entities + entities) = 1;
          same_select : SIZEOF(selections + selections) = 2;
          list_order : (repeat_list(ordered)[1] :=: ordered[1]) AND
            (repeat_list(ordered)[SIZEOF(ordered) + 1] :=: ordered[1]);
          bag_duplicates : SIZEOF(duplicates + duplicates) = 4;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string AGGREGATE_SELECT_UNION_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.AggregateSelectUnionModel;

        internal static class AggregateSelectUnionConsumer
        {
            internal static ValidationResult Validate()
            {
                var first = new ChildItem(BigInteger.One);
                var second = new ChildItem(new BigInteger(2));
                var entities = new ExpressSet<IChildItem>(1) { first };
                var selections = new ExpressSet<ItemChoice>(1)
                {
                    ItemChoice.FromChildItem(first),
                    ItemChoice.FromChildItem(second),
                };
                var nested = new ExpressSet<NestedChoice>(1)
                {
                    NestedChoice.FromItemChoice(ItemChoice.FromChildItem(first)),
                    NestedChoice.FromItemChoice(ItemChoice.FromChildItem(second)),
                };
                var ordered = new ExpressList<IChildItem>(1) { first, second };
                var duplicates = new ExpressBag<IChildItem>(1) { first, first };
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["aggregate select union"], "3;1"),
                        new FileName("aggregate-select-union.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["aggregate_select_union_model"])),
                    [TedToolkit.Step21.Generated.AggregateSelectUnionModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("aggregate_select_union_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, first);
                _ = structure.Add(section, second);
                _ = structure.Add(section, new Sample(entities, selections, nested, ordered, duplicates));
                return structure.Validate();
            }
        }
        """;

    private const string AGGREGATE_SELECT_UNION_INVALID_SCHEMA = """
        SCHEMA aggregate_select_union_invalid;
        ENTITY target_item;
        END_ENTITY;
        ENTITY other_item;
        END_ENTITY;
        TYPE text_item = STRING;
        END_TYPE;
        TYPE candidate = SELECT(__ALTERNATIVES__);
        END_TYPE;
        ENTITY sample;
          targets : __TARGET_AGGREGATE__;
          candidates : __CANDIDATE_AGGREGATE__;
        WHERE
          invalid_union : SIZEOF(targets + candidates) = 2;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string AGGREGATE_MULTI_SELECT_UNION_SCHEMA = """
        SCHEMA aggregate_multi_select_union_model;
        ENTITY root_item;
        END_ENTITY;
        ENTITY first_item SUBTYPE OF (root_item);
        END_ENTITY;
        ENTITY derived_first_item SUBTYPE OF (first_item);
        END_ENTITY;
        ENTITY second_item SUBTYPE OF (root_item);
        END_ENTITY;
        TYPE item_choice = SELECT (first_item, second_item);
        END_TYPE;
        ENTITY path_holder;
          candidate : item_choice;
        WHERE
          narrowed_path_reachable : SIZEOF(narrow_path(SELF)) <= 1;
        END_ENTITY;
        FUNCTION narrow_item(candidate : item_choice) : SET OF first_item;
          LOCAL
            result : SET OF first_item := [];
          END_LOCAL;
          IF 'AGGREGATE_MULTI_SELECT_UNION_MODEL.FIRST_ITEM' IN TYPEOF(candidate) THEN
            result := result + candidate;
          END_IF;
          RETURN(result);
        END_FUNCTION;
        FUNCTION narrow_path(holder_instance : path_holder) : SET OF derived_first_item;
          LOCAL
            result : SET OF derived_first_item := [];
          END_LOCAL;
          IF 'AGGREGATE_MULTI_SELECT_UNION_MODEL.DERIVED_FIRST_ITEM' IN TYPEOF(holder_instance.candidate) THEN
            result := result + holder_instance.candidate;
          END_IF;
          RETURN(result);
        END_FUNCTION;
        FUNCTION merge_items(values : SET OF root_item;
                             selections : SET OF item_choice) : SET OF item_choice;
          RETURN(selections + values);
        END_FUNCTION;
        FUNCTION unwrap_items(values : SET OF root_item;
                              selections : SET OF item_choice) : SET OF root_item;
          RETURN(values + selections);
        END_FUNCTION;
        ENTITY sample;
          values : SET [1:?] OF root_item;
          selections : SET [1:?] OF item_choice;
        WHERE
          reachable : SIZEOF(merge_items(values, selections)) > 0;
          reverse_reachable : SIZEOF(unwrap_items(values, selections)) > 0;
          narrowed_reachable : SIZEOF(narrow_item(selections[1])) <= 1;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NESTED_AGGREGATE_EQUALITY_SCHEMA = """
        SCHEMA nested_aggregate_equality_model;
        ENTITY item;
          code : INTEGER;
        END_ENTITY;
        ENTITY sample;
          rows : LIST [0:?] OF LIST [0:?] OF item;
          item_set : SET [0:?] OF item;
          item_bag : BAG [0:?] OF item;
          optional_rows : OPTIONAL LIST [0:?] OF LIST [0:?] OF item;
        WHERE
          empty_left : [] = QUERY (row <* rows | SIZEOF(row) > 0);
          empty_right : QUERY (row <* rows | SIZEOF(row) > 0) = [];
          nested_empty : [[]] = rows;
          empty_set : [] = item_set;
          empty_bag : [] <> item_bag;
          optional_unknown : optional_rows = optional_rows;
          entity_regression : rows = rows;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NESTED_AGGREGATE_EQUALITY_INVALID_SCHEMA = """
        SCHEMA nested_aggregate_equality_invalid;
        ENTITY item;
        END_ENTITY;
        ENTITY sample;
          rows : LIST [0:?] OF LIST [0:?] OF item;
        WHERE
          invalid : __EXPRESSION__ = rows;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NESTED_AGGREGATE_IDENTITY_SCHEMA = """
        SCHEMA nested_aggregate_identity_model;
        ENTITY item;
        END_ENTITY;
        ENTITY sample;
          rows : LIST [0:?] OF LIST [0:?] OF item;
        WHERE
          unchanged : [] :=: rows;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string QUALIFIED_ASSIGNMENT_UNKNOWN_SCHEMA = """
        SCHEMA qualified_assignment_unknown_model;
        FUNCTION maybe_value(present : BOOLEAN) : REAL;
          IF present THEN RETURN(2.0); END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION direct_assign(present : BOOLEAN) : REAL;
          LOCAL
            result_value : REAL;
          END_LOCAL;
          result_value := maybe_value(present);
          RETURN(result_value);
        END_FUNCTION;
        FUNCTION qualified_assign(present, force_unknown : BOOLEAN) : LIST [1:1] OF REAL;
          LOCAL
            values : LIST [1:1] OF REAL := [1.0];
          END_LOCAL;
          IF force_unknown THEN RETURN(?); END_IF;
          values[1] := maybe_value(present);
          RETURN(values);
        END_FUNCTION;
        FUNCTION optional_assign(present : BOOLEAN) : ARRAY [1:1] OF OPTIONAL REAL;
          LOCAL
            values : ARRAY [1:1] OF OPTIONAL REAL := [?];
          END_LOCAL;
          values[1] := maybe_value(present);
          RETURN(values);
        END_FUNCTION;
        ENTITY sample;
          present : BOOLEAN;
        WHERE
          direct_value : direct_assign(present) = 2.0;
          qualified_value : qualified_assign(present,FALSE)[1] = 2.0;
          optional_value : EXISTS(optional_assign(present)[1]) = present;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string QUALIFIED_ASSIGNMENT_UNKNOWN_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.QualifiedAssignmentUnknownModel;

        internal static class QualifiedAssignmentUnknownConsumer
        {
            internal static ValidationResult Validate(bool present)
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["qualified assignment unknown"], "3;1"),
                        new FileName("qualified-assignment-unknown.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["qualified_assignment_unknown_model"])),
                    [TedToolkit.Step21.Generated.QualifiedAssignmentUnknownModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("qualified_assignment_unknown_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample(present));
                return structure.Validate();
            }
        }
        """;

    private const string ASSIGNMENT_INDETERMINATE_FUNCTION_SCHEMA = """
        SCHEMA assignment_indeterminate_function_model;
        FUNCTION maybe_integer(present : BOOLEAN) : INTEGER;
          IF present THEN RETURN(2); END_IF;
          RETURN(?);
        END_FUNCTION;
        FUNCTION sum_to_value(present : BOOLEAN) : INTEGER;
          LOCAL
            upper : INTEGER;
            result_value : INTEGER := 0;
            index : INTEGER;
          END_LOCAL;
          upper := maybe_integer(present);
          REPEAT index := 1 TO upper BY 1;
            result_value := result_value + 1;
          END_REPEAT;
          RETURN(result_value);
        END_FUNCTION;
        FUNCTION forwarded_sum(present : BOOLEAN) : INTEGER;
          RETURN(sum_to_value(present) + 1);
        END_FUNCTION;
        FUNCTION determinate_assignment(present : BOOLEAN) : INTEGER;
          LOCAL
            upper : INTEGER;
            result_value : INTEGER := 0;
            index : INTEGER;
          END_LOCAL;
          upper := 2;
          IF NOT present THEN RETURN(0); END_IF;
          REPEAT index := 1 TO upper BY 1;
            result_value := result_value + 1;
          END_REPEAT;
          RETURN(result_value);
        END_FUNCTION;
        PROCEDURE procedure_control(present : BOOLEAN);
          LOCAL procedure_value : INTEGER; END_LOCAL;
          procedure_value := maybe_integer(present);
        END_PROCEDURE;
        ENTITY sample;
        WHERE
          success_continues : sum_to_value(TRUE) = 2;
          failure_is_unknown : NOT EXISTS(sum_to_value(FALSE));
          caller_success : forwarded_sum(TRUE) = 3;
          caller_failure : NOT EXISTS(forwarded_sum(FALSE));
          determinate_control : determinate_assignment(TRUE) = 2;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ASSIGNMENT_INDETERMINATE_FUNCTION_CONSUMER = """
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.AssignmentIndeterminateFunctionModel;

        internal static class AssignmentIndeterminateFunctionConsumer
        {
            internal static ValidationResult Validate()
            {
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["assignment indeterminate function"], "3;1"),
                        new FileName("assignment-indeterminate-function.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["assignment_indeterminate_function_model"])),
                    [TedToolkit.Step21.Generated.AssignmentIndeterminateFunctionModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("assignment_indeterminate_function_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, new Sample());
                return structure.Validate();
            }
        }
        """;

    private const string TYPEOF_GUARDED_ATTRIBUTE_SCHEMA = """
        SCHEMA typeof_guarded_attribute_model;
        ENTITY item;
        END_ENTITY;
        ENTITY applied_assignment;
          items : SET [1:?] OF item;
        END_ENTITY;
        ENTITY property_definition;
          name : STRING;
          description : STRING;
        END_ENTITY;
        ENTITY thread_definition SUBTYPE OF (property_definition);
        END_ENTITY;
        ENTITY external_definition SUBTYPE OF (property_definition);
        END_ENTITY;
        TYPE definition_select = SELECT (
          item,
          property_definition,
          thread_definition,
          external_definition);
        END_TYPE;
        ENTITY property_definition_representation;
          definition : definition_select;
        END_ENTITY;
        RULE guarded_assignment_items FOR (item);
        WHERE
          guarded_attribute : SIZEOF(QUERY(candidate <* item |
            SIZEOF(QUERY(assignment <* USEDIN(candidate, '') |
              (('TYPEOF_GUARDED_ATTRIBUTE_MODEL.' + 'APPLIED_ASSIGNMENT') IN TYPEOF(assignment)) AND
              (SIZEOF(assignment.items) > 0))) >= 0)) >= 0;
        END_RULE;
        RULE guarded_select_path FOR (property_definition_representation);
        WHERE
          guarded_path : SIZEOF(QUERY(pdr <* property_definition_representation |
            ('TYPEOF_GUARDED_ATTRIBUTE_MODEL.PROPERTY_DEFINITION' IN TYPEOF(pdr.definition)) AND
            (pdr.definition.name = 'document property') AND
            ((('TYPEOF_GUARDED_ATTRIBUTE_MODEL.THREAD_DEFINITION' IN TYPEOF(pdr.definition)) OR
              ('TYPEOF_GUARDED_ATTRIBUTE_MODEL.EXTERNAL_DEFINITION' IN TYPEOF(pdr.definition))) AND
            (pdr.definition.description = 'thread')))) >= 0;
        END_RULE;
        END_SCHEMA;
        """;

    private const string TYPEOF_GUARDED_DEFINED_SELECT_SCHEMA = """
        SCHEMA typeof_guarded_defined_select_model;
        ENTITY precision_qualifier;
        END_ENTITY;
        ENTITY uncertainty_qualifier;
          measure_name : STRING;
        END_ENTITY;
        TYPE value_qualifier = SELECT (precision_qualifier, uncertainty_qualifier);
        END_TYPE;
        ENTITY qualified_item;
          qualifiers : SET [1:?] OF value_qualifier;
        WHERE
          unique_uncertainty : SIZEOF(QUERY(u1 <* qualifiers |
            ('TYPEOF_GUARDED_DEFINED_SELECT_MODEL.UNCERTAINTY_QUALIFIER' IN TYPEOF(u1)) AND
            (SIZEOF(QUERY(u2 <* qualifiers |
              ('TYPEOF_GUARDED_DEFINED_SELECT_MODEL.UNCERTAINTY_QUALIFIER' IN TYPEOF(u2)) AND
              (u2\uncertainty_qualifier.measure_name = u1\uncertainty_qualifier.measure_name))) > 0))) > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string TYPEOF_GUARDED_REDECLARED_ATTRIBUTE_SCHEMA = """
        SCHEMA typeof_guarded_redeclared_attribute_model;
        ENTITY representation_item;
        END_ENTITY;
        ENTITY annotation_symbol SUBTYPE OF (representation_item);
        END_ENTITY;
        TYPE annotation_item = SELECT (annotation_symbol);
        END_TYPE;
        ENTITY styled_item;
          item : representation_item;
        END_ENTITY;
        ENTITY annotation_occurrence SUBTYPE OF (styled_item);
        END_ENTITY;
        ENTITY annotation_symbol_occurrence SUBTYPE OF (annotation_occurrence);
          SELF\styled_item.item : annotation_item;
        END_ENTITY;
        ENTITY draughting_annotation_occurrence SUBTYPE OF (annotation_occurrence);
        WHERE
          valid_symbol : NOT ('TYPEOF_GUARDED_REDECLARED_ATTRIBUTE_MODEL.ANNOTATION_SYMBOL_OCCURRENCE'
            IN TYPEOF(SELF)) OR ('TYPEOF_GUARDED_REDECLARED_ATTRIBUTE_MODEL.ANNOTATION_SYMBOL'
            IN TYPEOF(SELF.item));
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string QUERY_INTERFACE_AGGREGATE_SCHEMA = """
        SCHEMA query_interface_aggregate_model;
        ENTITY child;
          enabled : BOOLEAN;
        END_ENTITY;
        ENTITY parent;
          children : SET [1:?] OF child;
        WHERE
          has_enabled_child : SIZEOF(QUERY(candidate <* children | candidate.enabled)) > 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string TYPEOF_GUARDED_NUMERIC_SELECT_SCHEMA = """
        SCHEMA typeof_guarded_numeric_select_model;
        TYPE length_measure = REAL;
        END_TYPE;
        TYPE count_measure = INTEGER;
        END_TYPE;
        TYPE measure_value = SELECT (length_measure, count_measure);
        END_TYPE;
        FUNCTION project_real(item : measure_value) : REAL;
          LOCAL
            result : REAL;
          END_LOCAL;
          result := item;
          RETURN(result);
        END_FUNCTION;
        ENTITY measure_holder;
          value_component : measure_value;
        WHERE
          non_negative : ('NUMBER' IN TYPEOF(value_component)) AND (value_component >= 0.0);
          projected_real : project_real(value_component) >= 0.0;
        END_ENTITY;
        ENTITY measure_container;
          measurement : measure_holder;
        WHERE
          non_negative : ('NUMBER' IN TYPEOF(measurement.value_component)) AND
            (measurement.value_component >= 0.0);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string QUERY_RESULT_NARROWING_SCHEMA = """
        SCHEMA query_result_narrowing_model;
        ENTITY representation_item;
          code : INTEGER;
        END_ENTITY;
        ENTITY manifold_solid_brep SUBTYPE OF (representation_item);
        END_ENTITY;
        ENTITY faceted_brep SUBTYPE OF (manifold_solid_brep);
        END_ENTITY;
        ENTITY point SUBTYPE OF (representation_item);
        END_ENTITY;
        ENTITY curve SUBTYPE OF (representation_item);
        END_ENTITY;
        ENTITY conic SUBTYPE OF (curve);
        END_ENTITY;
        ENTITY polyline SUBTYPE OF (curve);
        END_ENTITY;
        ENTITY representation_reference SUBTYPE OF (representation_item);
          target : representation_item;
        END_ENTITY;
        TYPE point_or_curve = SELECT (point, curve);
        END_TYPE;
        TYPE nested_choice = SELECT (point_or_curve);
        END_TYPE;
        FUNCTION point_ok(item_value : point) : BOOLEAN;
          RETURN(item_value.code > 0);
        END_FUNCTION;
        FUNCTION brep_ok(item_value : manifold_solid_brep) : BOOLEAN;
          RETURN(item_value.code > 0);
        END_FUNCTION;
        FUNCTION conic_ok(item_value : conic) : BOOLEAN;
          RETURN(item_value.code > 0);
        END_FUNCTION;
        FUNCTION polyline_ok(item_value : polyline) : BOOLEAN;
          RETURN(item_value.code > 0);
        END_FUNCTION;
        FUNCTION generic_set_items(values : SET OF GENERIC_ENTITY) : SET OF representation_item;
          RETURN(QUERY (item <* values |
            'QUERY_RESULT_NARROWING_MODEL.REPRESENTATION_ITEM' IN TYPEOF(item)));
        END_FUNCTION;
        FUNCTION generic_bag_items(item_value : representation_item) : BAG OF representation_item;
          RETURN(QUERY (item <* USEDIN(item_value, '') |
            'QUERY_RESULT_NARROWING_MODEL.REPRESENTATION_ITEM' IN TYPEOF(item)));
        END_FUNCTION;
        ENTITY sample;
          generic_item : representation_item;
          choices : SET [1:?] OF nested_choice;
          direct_choices : SET [1:?] OF point_or_curve;
          items : SET [1:?] OF representation_item;
        WHERE
          nested_select : SIZEOF(QUERY (p <* QUERY (candidate <* choices |
            'QUERY_RESULT_NARROWING_MODEL.POINT' IN TYPEOF(candidate)) |
            NOT point_ok(p))) = 0;
          entity_subtype : SIZEOF(QUERY (brep <* QUERY (candidate <* items |
            'QUERY_RESULT_NARROWING_MODEL.FACETED_BREP' IN TYPEOF(candidate)) |
            NOT brep_ok(brep))) = 0;
          direct_select_subtype : SIZEOF(QUERY (shape <* QUERY (candidate <* direct_choices |
            'QUERY_RESULT_NARROWING_MODEL.CONIC' IN TYPEOF(candidate)) |
            NOT conic_ok(shape))) = 0;
          nested_select_subtype : SIZEOF(QUERY (shape <* QUERY (candidate <* choices |
            'QUERY_RESULT_NARROWING_MODEL.POLYLINE' IN TYPEOF(candidate)) |
            NOT polyline_ok(shape))) = 0;
          result_outside_query : SIZEOF(QUERY (candidate <* choices |
            'QUERY_RESULT_NARROWING_MODEL.POINT' IN TYPEOF(candidate))) = 1;
          generic_bag_result : SIZEOF(generic_bag_items(generic_item)) = 1;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string QUERY_RESULT_NARROWING_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.QueryResultNarrowingModel;

        internal static class QueryResultNarrowingConsumer
        {
            internal static ValidationResult Validate()
            {
                var point = new Point(BigInteger.One);
                var curve = new Curve(BigInteger.One);
                var conic = new Conic(BigInteger.One);
                var polyline = new Polyline(BigInteger.One);
                var faceted = new FacetedBrep(BigInteger.One);
                var reference = new RepresentationReference(BigInteger.One, point);
                var choices = new ExpressSet<NestedChoice>(1)
                {
                    NestedChoice.FromPointOrCurve(PointOrCurve.FromPoint(point)),
                    NestedChoice.FromPointOrCurve(PointOrCurve.FromCurve(curve)),
                    NestedChoice.FromPointOrCurve(PointOrCurve.FromCurve(conic)),
                    NestedChoice.FromPointOrCurve(PointOrCurve.FromCurve(polyline)),
                };
                var directChoices = new ExpressSet<PointOrCurve>(1)
                {
                    PointOrCurve.FromPoint(point),
                    PointOrCurve.FromCurve(curve),
                    PointOrCurve.FromCurve(conic),
                    PointOrCurve.FromCurve(polyline),
                };
                var items = new ExpressSet<IRepresentationItem>(1) { faceted };
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["query result narrowing"], "3;1"),
                        new FileName("query-result.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["query_result_narrowing_model"])),
                    [TedToolkit.Step21.Generated.QueryResultNarrowingModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("query_result_narrowing_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, point);
                _ = structure.Add(section, curve);
                _ = structure.Add(section, conic);
                _ = structure.Add(section, polyline);
                _ = structure.Add(section, faceted);
                _ = structure.Add(section, reference);
                _ = structure.Add(section, new Sample(
                    point,
                    choices,
                    directChoices,
                    items));
                return structure.Validate();
            }
        }
        """;

    private const string QUERY_RESULT_NARROWING_CONTROLS = """
        SCHEMA query_result_narrowing_controls;
        ENTITY root;
        END_ENTITY;
        ENTITY point SUBTYPE OF (root);
        END_ENTITY;
        ENTITY curve SUBTYPE OF (root);
        END_ENTITY;
        ENTITY conic SUBTYPE OF (curve);
        END_ENTITY;
        TYPE choice = SELECT (point, curve);
        END_TYPE;
        TYPE ambiguous_choice = SELECT (root, curve);
        END_TYPE;
        FUNCTION nonentity_generic_control(values : SET OF GENERIC:t) : SET OF root;
          RETURN(QUERY (item <* values |
            'QUERY_RESULT_NARROWING_CONTROLS.POINT' IN TYPEOF(item)));
        END_FUNCTION;
        FUNCTION missing_generic_entity_control(values : SET OF GENERIC_ENTITY) : SET OF root;
          RETURN(QUERY (item <* values |
            'QUERY_RESULT_NARROWING_CONTROLS.MISSING' IN TYPEOF(item)));
        END_FUNCTION;
        FUNCTION compound_generic_entity_control(values : SET OF GENERIC_ENTITY) : SET OF root;
          RETURN(QUERY (item <* values |
            ('QUERY_RESULT_NARROWING_CONTROLS.POINT' IN TYPEOF(item)) AND TRUE));
        END_FUNCTION;
        ENTITY sample;
          values : SET [1:?] OF choice;
          ambiguous_values : SET [1:?] OF ambiguous_choice;
        WHERE
          and_control : SIZEOF(QUERY (item <* values |
            ('QUERY_RESULT_NARROWING_CONTROLS.POINT' IN TYPEOF(item)) AND TRUE)) > 0;
          reverse_and_control : SIZEOF(QUERY (item <* values |
            TRUE AND ('QUERY_RESULT_NARROWING_CONTROLS.POINT' IN TYPEOF(item)))) > 0;
          or_control : SIZEOF(QUERY (item <* values |
            ('QUERY_RESULT_NARROWING_CONTROLS.POINT' IN TYPEOF(item)) OR TRUE)) > 0;
          not_control : SIZEOF(QUERY (item <* values |
            NOT ('QUERY_RESULT_NARROWING_CONTROLS.POINT' IN TYPEOF(item)))) > 0;
          multiple_control : SIZEOF(QUERY (item <* values |
            'QUERY_RESULT_NARROWING_CONTROLS.ROOT' IN TYPEOF(item))) > 0;
          wrong_control : SIZEOF(QUERY (item <* values |
            'QUERY_RESULT_NARROWING_CONTROLS.SAMPLE' IN TYPEOF(item))) = 0;
          unknown_control : SIZEOF(QUERY (item <* values |
            'QUERY_RESULT_NARROWING_CONTROLS.MISSING' IN TYPEOF(item))) = 0;
          ambiguous_control : SIZEOF(QUERY (item <* ambiguous_values |
            'QUERY_RESULT_NARROWING_CONTROLS.CONIC' IN TYPEOF(item))) = 0;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string EXHAUSTIVE_SELECT_QUERY_SCHEMA = """
        SCHEMA exhaustive_select_query_model;
        ENTITY root;
          code : INTEGER;
        END_ENTITY;
        ENTITY first_item SUBTYPE OF (root);
        END_ENTITY;
        ENTITY second_item SUBTYPE OF (root);
        END_ENTITY;
        ENTITY outside_item SUBTYPE OF (root);
        END_ENTITY;
        TYPE inner_choice = SELECT (first_item, second_item);
        END_TYPE;
        TYPE outer_choice = SELECT (inner_choice);
        END_TYPE;
        FUNCTION selected_items(values : SET OF root) : SET OF outer_choice;
          LOCAL
            result_items : SET OF outer_choice;
          END_LOCAL;
          result_items := QUERY (item <* values |
            ('EXHAUSTIVE_SELECT_QUERY_MODEL.FIRST_ITEM' IN TYPEOF(item)) OR
            ('EXHAUSTIVE_SELECT_QUERY_MODEL.SECOND_ITEM' IN TYPEOF(item)));
          RETURN(result_items);
        END_FUNCTION;
        FUNCTION reversed_items(values : SET OF root) : SET OF outer_choice;
          LOCAL
            result_items : SET OF outer_choice;
          END_LOCAL;
          result_items := QUERY (item <* values |
            ('EXHAUSTIVE_SELECT_QUERY_MODEL.SECOND_ITEM' IN TYPEOF(item)) OR
            ('EXHAUSTIVE_SELECT_QUERY_MODEL.FIRST_ITEM' IN TYPEOF(item)));
          RETURN(result_items);
        END_FUNCTION;
        ENTITY sample;
          values : SET [1:?] OF root;
        WHERE
          exhaustive_result : SIZEOF(selected_items(values)) = 2;
          order_independent : SIZEOF(reversed_items(values)) = 2;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string EXHAUSTIVE_SELECT_QUERY_CONSUMER = """
        using System.Numerics;
        using TedToolkit.Step21;
        using TedToolkit.Step21.Generated.ExhaustiveSelectQueryModel;

        internal static class ExhaustiveSelectQueryConsumer
        {
            internal static ValidationResult Validate()
            {
                var first = new FirstItem(BigInteger.One);
                var second = new SecondItem(BigInteger.One);
                var outside = new OutsideItem(BigInteger.One);
                var values = new ExpressSet<IRoot>(1) { first, outside, second };
                var structure = new ExchangeStructure(
                    new HeaderSection(
                        new FileDescription(["exhaustive select query"], "3;1"),
                        new FileName("exhaustive.step", "2026-08-25T00:00:00+08:00", [], [], "tests", "tests", ""),
                        new FileSchema(["exhaustive_select_query_model"])),
                    [TedToolkit.Step21.Generated.ExhaustiveSelectQueryModel.SchemaDescriptor.Instance]);
                var section = new DataSection(new SchemaName("exhaustive_select_query_model"));
                structure.DataSections.Add(section);
                _ = structure.Add(section, first);
                _ = structure.Add(section, second);
                _ = structure.Add(section, outside);
                _ = structure.Add(section, new Sample(values));
                return structure.Validate();
            }
        }
        """;

    private const string EXHAUSTIVE_SELECT_QUERY_CONTROLS = """
        SCHEMA exhaustive_select_query_controls;
        ENTITY root;
        END_ENTITY;
        ENTITY first_item SUBTYPE OF (root);
        END_ENTITY;
        ENTITY second_item SUBTYPE OF (root);
        END_ENTITY;
        ENTITY outside_item SUBTYPE OF (root);
        END_ENTITY;
        TYPE target_choice = SELECT (first_item, second_item);
        END_TYPE;
        FUNCTION subset(values : SET OF root) : SET OF target_choice;
          RETURN(QUERY (item <* values |
            'EXHAUSTIVE_SELECT_QUERY_CONTROLS.FIRST_ITEM' IN TYPEOF(item)));
        END_FUNCTION;
        FUNCTION superset(values : SET OF root) : SET OF target_choice;
          RETURN(QUERY (item <* values |
            ('EXHAUSTIVE_SELECT_QUERY_CONTROLS.FIRST_ITEM' IN TYPEOF(item)) OR
            ('EXHAUSTIVE_SELECT_QUERY_CONTROLS.SECOND_ITEM' IN TYPEOF(item)) OR
            ('EXHAUSTIVE_SELECT_QUERY_CONTROLS.OUTSIDE_ITEM' IN TYPEOF(item))));
        END_FUNCTION;
        FUNCTION duplicate(values : SET OF root) : SET OF target_choice;
          RETURN(QUERY (item <* values |
            ('EXHAUSTIVE_SELECT_QUERY_CONTROLS.FIRST_ITEM' IN TYPEOF(item)) OR
            ('EXHAUSTIVE_SELECT_QUERY_CONTROLS.FIRST_ITEM' IN TYPEOF(item)) OR
            ('EXHAUSTIVE_SELECT_QUERY_CONTROLS.SECOND_ITEM' IN TYPEOF(item))));
        END_FUNCTION;
        FUNCTION and_control(values : SET OF root) : SET OF target_choice;
          RETURN(QUERY (item <* values |
            ('EXHAUSTIVE_SELECT_QUERY_CONTROLS.FIRST_ITEM' IN TYPEOF(item)) AND
            ('EXHAUSTIVE_SELECT_QUERY_CONTROLS.SECOND_ITEM' IN TYPEOF(item))));
        END_FUNCTION;
        FUNCTION not_control(values : SET OF root) : SET OF target_choice;
          RETURN(QUERY (item <* values | NOT
            ('EXHAUSTIVE_SELECT_QUERY_CONTROLS.OUTSIDE_ITEM' IN TYPEOF(item))));
        END_FUNCTION;
        FUNCTION wrong(values : SET OF root) : SET OF target_choice;
          RETURN(QUERY (item <* values |
            ('EXHAUSTIVE_SELECT_QUERY_CONTROLS.FIRST_ITEM' IN TYPEOF(item)) OR
            ('EXHAUSTIVE_SELECT_QUERY_CONTROLS.MISSING' IN TYPEOF(item))));
        END_FUNCTION;
        FUNCTION dynamic_name(values : SET OF root; schema_name : STRING) : SET OF target_choice;
          RETURN(QUERY (item <* values |
            ((schema_name + '.FIRST_ITEM') IN TYPEOF(item)) OR
            ('EXHAUSTIVE_SELECT_QUERY_CONTROLS.SECOND_ITEM' IN TYPEOF(item))));
        END_FUNCTION;
        END_SCHEMA;
        """;

    /// <summary>
    /// Verifies aggregate union selects the unique entity least upper bound and preserves aggregate semantics.
    /// </summary>
    [Test]
    public async Task Should_union_entity_aggregates_through_their_unique_common_supertype()
    {
        var result = GeneratorHostTests.Run(
            ENTITY_AGGREGATE_LUB_CONSUMER,
            ("schemas/entity-aggregate-lub.exp", ENTITY_AGGREGATE_LUB_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var ambiguous = GeneratorHostTests.Run(
            ("schemas/entity-aggregate-lub-ambiguous.exp", ENTITY_AGGREGATE_LUB_AMBIGUOUS));
        var invalidAssignment = GeneratorHostTests.Run(
            ("schemas/entity-aggregate-lub-invalid-assignment.exp", ENTITY_AGGREGATE_LUB_INVALID_ASSIGNMENT));

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "EntityAggregateLubConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(ambiguous.Diagnostics.Concat(ambiguous.OutputCompilation.GetDiagnostics())
                .Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)).IsTrue();
            await Assert.That(invalidAssignment.Diagnostics.Concat(invalidAssignment.OutputCompilation.GetDiagnostics())
                .Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies SET union unwraps only sole-alternative entity SELECT elements and preserves other aggregate categories.
    /// </summary>
    [Test]
    public async Task Should_unwrap_sole_alternative_select_elements_in_set_union()
    {
        var result = GeneratorHostTests.Run(
            AGGREGATE_SELECT_UNION_CONSUMER,
            ("schemas/aggregate-select-union.exp", AGGREGATE_SELECT_UNION_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var multiple = GeneratorHostTests.Run(
            ("schemas/aggregate-multi-select-union.exp", AGGREGATE_MULTI_SELECT_UNION_SCHEMA));
        var multipleDiagnostics = multiple.Diagnostics.Concat(multiple.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var multipleGenerated = string.Join(
            Environment.NewLine,
            multiple.GeneratedSources.Select(source => source.SourceText.ToString()));
        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
        await Assert.That(multipleDiagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, multipleDiagnostics));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "AggregateSelectUnionConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        var invalidSchemas = new[]
        {
            AGGREGATE_SELECT_UNION_INVALID_SCHEMA
                .Replace("__ALTERNATIVES__", "target_item, other_item", StringComparison.Ordinal)
                .Replace("__TARGET_AGGREGATE__", "SET OF target_item", StringComparison.Ordinal)
                .Replace("__CANDIDATE_AGGREGATE__", "SET OF candidate", StringComparison.Ordinal),
            AGGREGATE_SELECT_UNION_INVALID_SCHEMA
                .Replace("__ALTERNATIVES__", "target_item, text_item", StringComparison.Ordinal)
                .Replace("__TARGET_AGGREGATE__", "SET OF target_item", StringComparison.Ordinal)
                .Replace("__CANDIDATE_AGGREGATE__", "SET OF candidate", StringComparison.Ordinal),
            AGGREGATE_SELECT_UNION_INVALID_SCHEMA
                .Replace("__ALTERNATIVES__", "other_item", StringComparison.Ordinal)
                .Replace("__TARGET_AGGREGATE__", "SET OF target_item", StringComparison.Ordinal)
                .Replace("__CANDIDATE_AGGREGATE__", "SET OF candidate", StringComparison.Ordinal),
            AGGREGATE_SELECT_UNION_INVALID_SCHEMA
                .Replace("__ALTERNATIVES__", "target_item", StringComparison.Ordinal)
                .Replace(
                    "__TARGET_AGGREGATE__",
                    "ARRAY [1:1] OF OPTIONAL target_item",
                    StringComparison.Ordinal)
                .Replace(
                    "__CANDIDATE_AGGREGATE__",
                    "ARRAY [1:1] OF OPTIONAL candidate",
                    StringComparison.Ordinal),
        };
        var invalidResults = invalidSchemas.Select((schema, index) => GeneratorHostTests.Run(
            ($"schemas/aggregate-select-union-invalid-{index}.exp", schema)))
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(multipleGenerated).Contains("ItemChoice.FromFirstItem");
            await Assert.That(multipleGenerated).Contains("ItemChoice.FromSecondItem");
            foreach (var invalid in invalidResults)
            {
                var invalidGenerated = string.Join(
                    Environment.NewLine,
                    invalid.GeneratedSources.Select(source => source.SourceText.ToString()));
                await Assert.That(invalidGenerated).DoesNotContain("__expressAggregateUnion");
            }
        }
    }

    /// <summary>
    /// Verifies value equality recursively types an aggregate initializer from the opposite aggregate operand.
    /// </summary>
    [Test]
    public async Task Should_apply_nested_aggregate_operand_type_for_value_equality()
    {
        var bound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource(
                "schemas/nested-aggregate-equality.exp",
                NESTED_AGGREGATE_EQUALITY_SCHEMA)]);
        if (bound.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                bound.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(bound.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var aggregateInitializers = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.AggregateInitializer)
            .ToArray();
        var nestedInitializer = aggregateInitializers.Single(expression => expression.SourceText == "[[]]");
        var nestedOuter = (ExpressBoundAggregateType)nestedInitializer.Type.DeclaredType!;
        var nestedInner = (ExpressBoundAggregateType)nestedOuter.ElementType;
        var nestedElement = (ExpressBoundNamedType)nestedInner.ElementType;
        var result = GeneratorHostTests.Run(
            ("schemas/nested-aggregate-equality.exp", NESTED_AGGREGATE_EQUALITY_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var invalidResults = new[] { "[1]", "[[], [1]]", "[['text']]", }
            .Select((expression, index) => GeneratorHostTests.Run(
                ($"schemas/nested-aggregate-equality-invalid-{index}.exp",
                    NESTED_AGGREGATE_EQUALITY_INVALID_SCHEMA.Replace(
                        "__EXPRESSION__",
                        expression,
                        StringComparison.Ordinal))))
            .ToArray();
        var identity = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource(
                "schemas/nested-aggregate-identity.exp",
                NESTED_AGGREGATE_IDENTITY_SCHEMA)]);
        var identityExpression = identity.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Single(expression => expression.Kind == ExpressExpressionKind.Binary
                && expression.Operation == ":=:");
        var identityLeft = (ExpressBoundAggregateType)identityExpression.Children[0].Type.DeclaredType!;
        var identityRight = (ExpressBoundAggregateType)identityExpression.Children[1].Type.DeclaredType!;

        using (Assert.Multiple())
        {
            await Assert.That(nestedElement.Declaration.Name).IsEqualTo("item");
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
            foreach (var invalid in invalidResults)
            {
                await Assert.That(invalid.Diagnostics.Concat(invalid.OutputCompilation.GetDiagnostics())
                    .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                    .IsNotEmpty();
            }

            await Assert.That(identityLeft.ElementType).IsTypeOf<ExpressBoundScalarType>();
            await Assert.That(identityRight.ElementType).IsTypeOf<ExpressBoundAggregateType>();
        }
    }

    /// <summary>
    /// Verifies UNKNOWN assigned to a nonoptional target returns UNKNOWN without evaluating the value twice.
    /// </summary>
    [Test]
    public async Task Should_return_unknown_before_assigning_an_indeterminate_nonoptional_value()
    {
        var result = GeneratorHostTests.Run(
            QUALIFIED_ASSIGNMENT_UNKNOWN_CONSUMER,
            ("schemas/qualified-assignment-unknown.exp", QUALIFIED_ASSIGNMENT_UNKNOWN_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));
        var qualifiedStart = generated.LastIndexOf("__ExpressFunction_QualifiedAssign", StringComparison.Ordinal);
        var qualifiedEnd = generated.IndexOf("private static", qualifiedStart + 1, StringComparison.Ordinal);
        var qualifiedCode = generated.Substring(qualifiedStart, qualifiedEnd - qualifiedStart);
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("QualifiedAssignmentUnknownConsumer", throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var present = (ValidationResult)validate.Invoke(null, [true])!;
        var unknown = (ValidationResult)validate.Invoke(null, [false])!;

        using (Assert.Multiple())
        {
            await Assert.That(present.IsValid).IsTrue();
            await Assert.That(unknown.Failures.Select(failure => failure.Code))
                .IsEquivalentTo([
                    "QUALIFIED_ASSIGNMENT_UNKNOWN_MODEL.SAMPLE.WHERE.DIRECT_VALUE",
                    "QUALIFIED_ASSIGNMENT_UNKNOWN_MODEL.SAMPLE.WHERE.QUALIFIED_VALUE",
                ]);
            await Assert.That(qualifiedCode.Split(
                    "__ExpressFunction_MaybeValue(",
                    StringSplitOptions.None).Length - 1)
                .IsEqualTo(1);
        }
    }

    /// <summary>
    /// Verifies an indeterminate nonoptional assignment makes its enclosing function indeterminate.
    /// </summary>
    [Test]
    public async Task Should_propagate_indeterminate_nonoptional_assignments_to_the_function()
    {
        var result = GeneratorHostTests.Run(
            ASSIGNMENT_INDETERMINATE_FUNCTION_CONSUMER,
            ("schemas/assignment-indeterminate-function.exp", ASSIGNMENT_INDETERMINATE_FUNCTION_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "AssignmentIndeterminateFunctionConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies a positive TYPEOF guard narrows a generic entity for the guarded side of AND.
    /// </summary>
    [Test]
    public async Task Should_resolve_attribute_after_static_typeof_guard()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/typeof-guarded-attribute.exp", TYPEOF_GUARDED_ATTRIBUTE_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
    }

    /// <summary>
    /// Verifies a TYPEOF guard unwraps a defined SELECT query element before a group-qualified access.
    /// </summary>
    [Test]
    public async Task Should_unwrap_a_defined_select_after_static_typeof_guard()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/typeof-guarded-defined-select.exp", TYPEOF_GUARDED_DEFINED_SELECT_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
    }

    /// <summary>
    /// Verifies a TYPEOF path proof reads a redeclared attribute through its declaring interface.
    /// </summary>
    [Test]
    public async Task Should_resolve_a_redeclared_attribute_after_static_typeof_guard()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/typeof-guarded-redeclared-attribute.exp", TYPEOF_GUARDED_REDECLARED_ATTRIBUTE_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
    }

    /// <summary>Verifies QUERY accepts the covariant aggregate interface exposed by an entity contract.</summary>
    [Test]
    public async Task Should_query_an_entity_interface_aggregate()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/query-interface-aggregate.exp", QUERY_INTERFACE_AGGREGATE_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
    }

    /// <summary>Verifies a TYPEOF proof promotes numeric SELECT alternatives to one NUMBER representation.</summary>
    [Test]
    public async Task Should_compare_a_numeric_select_after_static_typeof_guard()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/typeof-guarded-numeric-select.exp", TYPEOF_GUARDED_NUMERIC_SELECT_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
    }

    /// <summary>
    /// Verifies exact TYPEOF query predicates specialize their retained elements without leaking through compound predicates.
    /// </summary>
    [Test]
    public async Task Should_specialize_exact_typeof_query_results()
    {
        var bound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("schemas/query-result-narrowing.exp", QUERY_RESULT_NARROWING_SCHEMA)]);
        if (bound.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                bound.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(bound.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var exactQueries = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Query
                && expression.Children[1].Operation == "IN")
            .ToArray();
        var control = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("schemas/query-result-controls.exp", QUERY_RESULT_NARROWING_CONTROLS)]);
        if (control.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                control.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(control.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var controlQueries = control.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Query)
            .ToArray();
        var result = GeneratorHostTests.Run(
            QUERY_RESULT_NARROWING_CONSUMER,
            ("schemas/query-result-narrowing.exp", QUERY_RESULT_NARROWING_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(exactQueries).Count().IsEqualTo(7);
            await Assert.That(exactQueries.Select(expression =>
                    ((ExpressBoundNamedType)((ExpressBoundAggregateType)expression.Type.DeclaredType!).ElementType)
                    .Declaration.Name))
                .IsEquivalentTo([
                    "point",
                    "faceted_brep",
                    "conic",
                    "polyline",
                    "point",
                    "representation_item",
                    "representation_item",
                ]);
            await Assert.That(exactQueries.Where(expression =>
                    ((ExpressBoundAggregateType)expression.Children[0].Type.DeclaredType!).ElementType
                    is ExpressBoundGenericType { IsEntity: true, }))
                .Count().IsEqualTo(2);
            await Assert.That(exactQueries.All(expression => expression.Reference is
            { Kind: ExpressBoundNameKind.QueryVariable, })).IsTrue();
            await Assert.That(controlQueries).Count().IsEqualTo(11);
            await Assert.That(controlQueries.Count(expression =>
                    ((ExpressBoundAggregateType)expression.Type.DeclaredType!).ElementType
                    is ExpressBoundNamedType { Declaration.Name: "choice", }))
                .IsEqualTo(7);
            await Assert.That(controlQueries.Single(expression =>
                    ((ExpressBoundAggregateType)expression.Type.DeclaredType!).ElementType
                    is ExpressBoundNamedType { Declaration.Name: "ambiguous_choice", }))
                .IsNotNull();
            await Assert.That(controlQueries.Where(expression =>
                    ((ExpressBoundAggregateType)expression.Children[0].Type.DeclaredType!).ElementType
                    is ExpressBoundGenericType).All(expression => expression.Reference is null))
                .IsTrue();
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "QueryResultNarrowingConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies an exhaustive OR over a closed nested SELECT constructs only the proven selected alternatives.
    /// </summary>
    [Test]
    public async Task Should_specialize_exhaustive_closed_select_query_results()
    {
        var bound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("schemas/exhaustive-select-query.exp", EXHAUSTIVE_SELECT_QUERY_SCHEMA)]);
        if (bound.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                bound.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(bound.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var queries = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Query)
            .ToArray();
        var controls = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("schemas/exhaustive-select-controls.exp", EXHAUSTIVE_SELECT_QUERY_CONTROLS)]);
        if (controls.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                controls.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(controls.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var controlQueries = controls.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Query)
            .ToArray();
        var result = GeneratorHostTests.Run(
            EXHAUSTIVE_SELECT_QUERY_CONSUMER,
            ("schemas/exhaustive-select-query.exp", EXHAUSTIVE_SELECT_QUERY_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(queries).Count().IsEqualTo(2);
            await Assert.That(queries.All(expression => expression.Reference is
            { Kind: ExpressBoundNameKind.QueryVariable, }))
                .IsTrue();
            await Assert.That(queries.All(expression =>
                    ((ExpressBoundAggregateType)expression.Type.DeclaredType!).ElementType
                    is ExpressBoundNamedType { Declaration.Name: "outer_choice", }))
                .IsTrue();
            await Assert.That(controlQueries).Count().IsEqualTo(7);
            await Assert.That(controlQueries.Count(expression => expression.Reference is not null)).IsEqualTo(1);
            await Assert.That(controlQueries.Single(expression => expression.Reference is not null)).Satisfies(
                expression => expression is not null
                    && expression.Type.DeclaredType is ExpressBoundAggregateType
                    {
                        ElementType: ExpressBoundNamedType { Declaration.Name: "first_item", },
                    });
            await Assert.That(controlQueries.Where(expression => expression.Reference is null).All(expression =>
                    ((ExpressBoundAggregateType)expression.Type.DeclaredType!).ElementType
                    is ExpressBoundNamedType { Declaration.Name: "root", }))
                .IsTrue();
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "ExhaustiveSelectQueryConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies compatible partial entity values lower to one existing most-derived generated entity.
    /// </summary>
    [Test]
    public async Task Should_lower_compatible_complex_constructors_to_existing_leaf_storage()
    {
        var valid = GeneratorHostTests.Run(
            COMPLEX_CONSTRUCTOR_CONSUMER,
            ("schemas/complex-constructor.exp", COMPLEX_CONSTRUCTOR_SCHEMA));
        var missing = GeneratorHostTests.Run(
            ("schemas/missing-complex-slot.exp", MISSING_COMPLEX_SLOT_SCHEMA));
        var sameName = GeneratorHostTests.Run(
            ("schemas/same-name-complex-slot.exp", SAME_NAME_COMPLEX_SLOT_SCHEMA));

        await Assert.That(valid.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, valid.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var assembly = Emit(valid.OutputCompilation);
        var validate = assembly.GetType("ComplexConstructorConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validation = (ValidationResult)validate.Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(validation.IsValid).IsTrue();
            await Assert.That(missing.Diagnostics.Any(diagnostic =>
                    diagnostic.Id == "STEP21EXP006"
                    && diagnostic.GetMessage().Contains("missing", StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
            await Assert.That(sameName.Diagnostics.Any(diagnostic =>
                    diagnostic.Id == "STEP21EXP006"
                    && diagnostic.GetMessage().Contains("missing", StringComparison.OrdinalIgnoreCase)))
                .IsTrue()
                .Because(string.Join(Environment.NewLine, sameName.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }
    }

    /// <summary>
    /// Verifies a derived redeclaration shadows its base slot for statically derived carriers.
    /// </summary>
    [Test]
    public async Task Should_resolve_redeclared_attributes_from_the_static_carrier_projection()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/attribute-redeclaration.exp", ATTRIBUTE_REDECLARATION_SCHEMA));
        var unrelated = GeneratorHostTests.Run(
            ("schemas/unrelated-attribute-collision.exp", UNRELATED_ATTRIBUTE_COLLISION_SCHEMA));
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(generated).Contains("__ExpressDerived_OrientedEdge_EdgeStart");
            await Assert.That(generated).Contains(".Match");
            await Assert.That(unrelated.Diagnostics.Any(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error)).IsTrue();
        }
    }

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
        var overLimit = (ValidationResult)validate.Invoke(
            null,
            [true, new System.Numerics.BigInteger(11)])!;

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
            await Assert.That(overLimit.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(["GLOBAL_RULE_MODEL.RULE.BOUNDED_POPULATION_RULE.WHERE.BELOW_LIMIT"]);
        }
    }

    /// <summary>
    /// Verifies unqualified enumeration items retain their bound declaration inside derived and QUERY expressions.
    /// </summary>
    [Test]
    public async Task Should_emit_unqualified_enumeration_items_from_their_bound_declaration()
    {
        var result = GeneratorHostTests.Run(
            UNQUALIFIED_ENUMERATION_CONSUMER,
            ("schemas/unqualified-enumeration.exp", UNQUALIFIED_ENUMERATION_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("UnqualifiedEnumerationConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        ValidationResult Invoke(bool discontinuous) =>
            (ValidationResult)validate.Invoke(null, [discontinuous])!;
        var continuous = Invoke(false);
        var discontinuous = Invoke(true);

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(continuous.IsValid).IsTrue();
            await Assert.That(discontinuous.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(["UNQUALIFIED_ENUMERATION_MODEL.SAMPLE.WHERE.QUERY_VALUE"]);
        }
    }

    /// <summary>
    /// Verifies labeled scalar GENERIC functions close from concrete actuals.
    /// </summary>
    [Test]
    public async Task Should_specialize_scalar_generic_function_results()
    {
        var bound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("schemas/scalar-generic-choose.exp", SCALAR_GENERIC_CHOOSE_SCHEMA)]);
        var applications = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Application
                && string.Equals(expression.Reference?.Name, "choose", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();
        var result = GeneratorHostTests.Run(
            SCALAR_GENERIC_CHOOSE_CONSUMER,
            ("schemas/scalar-generic-choose.exp", SCALAR_GENERIC_CHOOSE_SCHEMA));
        var conflicting = new[] { "\n", "\r\n", }
            .Select((lineEnding, index) => GeneratorHostTests.Run((
                $"schemas/scalar-generic-conflict-{index}.exp",
                SCALAR_GENERIC_CHOOSE_SCHEMA.ReplaceLineEndings(lineEnding).Replace(
                    "ENTITY sample;",
                    "ENTITY other; END_ENTITY;" + Environment.NewLine + "ENTITY sample;",
                    StringComparison.Ordinal).Replace(
                    "  actual : vertex;",
                    "  actual : vertex;" + Environment.NewLine + "  other_value : other;",
                    StringComparison.Ordinal).Replace(
                    "choose(TRUE, actual, actual)",
                    "choose(TRUE, actual, other_value)",
                    StringComparison.Ordinal))))
            .ToArray();
        var unbound = GeneratorHostTests.Run(
            ("schemas/scalar-generic-unbound.exp", SCALAR_GENERIC_CHOOSE_SCHEMA.Replace(
                "choose(TRUE, actual, actual)",
                "choose(TRUE, ?, ?)",
                StringComparison.Ordinal)));

        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        }

        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("ScalarGenericChooseConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validation = (ValidationResult)validate.Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(applications).Count().IsEqualTo(1);
            await Assert.That(applications.All(application =>
                application.Type.DeclaredType is ExpressBoundNamedType
                {
                    Declaration.Kind: ExpressDeclarationKind.Entity,
                    Declaration.Name: "vertex",
                })).IsTrue();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.IsValid).IsTrue();
            await Assert.That(conflicting.All(conflict => conflict.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "STEP21EXP006"
                && diagnostic.GetMessage().Contains(
                    "no generated result type",
                    StringComparison.Ordinal)))).IsTrue();
            await Assert.That(unbound.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "STEP21EXP006"
                && diagnostic.GetMessage().Contains("no generated result type", StringComparison.Ordinal))).IsTrue();
        }
    }

    /// <summary>
    /// Verifies membership in SELECT aggregates uses EXPRESS value equality and three-valued ANY semantics.
    /// </summary>
    [Test]
    public async Task Should_compare_entity_membership_in_select_aggregates_by_value()
    {
        var result = GeneratorHostTests.Run(
            SELECT_MEMBERSHIP_CONSUMER,
            ("schemas/select-membership.exp", SELECT_MEMBERSHIP_SCHEMA));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("SelectMembershipConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            for (var mode = 0; mode < 4; mode++)
            {
                var validation = (ValidationResult)validate.Invoke(null, [mode])!;
                await Assert.That(validation.IsValid).IsTrue()
                    .Because($"membership mode {mode}: "
                        + string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            }
        }
    }

    /// <summary>
    /// Verifies TYPEOF unwraps the selected value without reporting SELECT carrier names.
    /// </summary>
    [Test]
    public async Task Should_report_the_actual_selected_type_from_typeof()
    {
        var result = GeneratorHostTests.Run(
            SELECT_TYPEOF_CONSUMER,
            ("schemas/select-typeof.exp", SELECT_TYPEOF_SCHEMA));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType("SelectTypeofConsumer", throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies typed SELECT values and derived complex-unit dimensions cooperate in value rules.
    /// </summary>
    [Test]
    public async Task Should_validate_typed_measure_against_derived_complex_unit_dimensions()
    {
        var result = GeneratorHostTests.Run(
            DIMENSIONAL_SELECT_CONSUMER,
            ("schemas/dimensional-select.exp", DIMENSIONAL_SELECT_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType("DimensionalSelectConsumer", throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
        }
    }

    /// <summary>
    /// Verifies TYPEOF reports only the concrete aggregate kind and preserves UNKNOWN independently of elements.
    /// </summary>
    [Test]
    public async Task Should_report_only_the_concrete_aggregate_kind_from_typeof()
    {
        var bound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("schemas/aggregate-typeof.exp", AGGREGATE_TYPEOF_SCHEMA)]);
        if (bound.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                bound.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(bound.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var typeOfExpressions = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Application
                && string.Equals(expression.Operation, "TYPEOF", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var result = GeneratorHostTests.Run(
            AGGREGATE_TYPEOF_CONSUMER,
            ("schemas/aggregate-typeof.exp", AGGREGATE_TYPEOF_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(typeOfExpressions).Count().IsEqualTo(13);
            await Assert.That(typeOfExpressions.Count(expression =>
                    expression.Children[0].Type.DeclaredType is ExpressBoundAggregateType))
                .IsEqualTo(10);
            await Assert.That(typeOfExpressions.Count(expression =>
                    expression.Children[0].Type.DeclaredType is ExpressBoundNamedType
                    {
                        Declaration.Kind: not ExpressDeclarationKind.Entity,
                    }))
                .IsEqualTo(2);
            await Assert.That(typeOfExpressions.Single(expression =>
                    expression.SourceText.Contains("maybe_set(FALSE)", StringComparison.Ordinal))
                .Type.CanBeIndeterminate)
                .IsTrue();
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "AggregateTypeofConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies a lexical E declaration takes precedence while unbound PI retains builtin numeric semantics.
    /// </summary>
    [Test]
    public async Task Should_prefer_lexical_names_over_builtin_numeric_constants()
    {
        var bound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("schemas/builtin-shadowing.exp", BUILTIN_SHADOWING_SCHEMA)]);
        if (bound.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                bound.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(bound.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var references = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Reference)
            .Distinct()
            .ToArray();
        var shadowed = references.Where(expression => expression.Reference is not null
            && string.Equals(expression.SourceText, "e", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var builtins = references.Where(expression => expression.Reference is null
            && string.Equals(expression.SourceText, "PI", StringComparison.Ordinal))
            .ToArray();
        var result = GeneratorHostTests.Run(
            BUILTIN_SHADOWING_CONSUMER,
            ("schemas/builtin-shadowing.exp", BUILTIN_SHADOWING_SCHEMA));

        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType("BuiltinShadowingConsumer", throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        using (Assert.Multiple())
        {
            await Assert.That(shadowed).Count().IsEqualTo(2);
            await Assert.That(shadowed.All(expression =>
                expression.Type.Kind == ExpressExpressionTypeKind.Entity)).IsTrue();
            await Assert.That(builtins).Count().IsEqualTo(4);
            await Assert.That(builtins.All(expression => expression.Type.Kind == ExpressExpressionTypeKind.Real))
                .IsTrue();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
        }
    }

    /// <summary>
    /// Verifies entity arguments enter a function's SELECT formal only through one compatible alternative.
    /// </summary>
    [Test]
    public async Task Should_adapt_entity_function_arguments_to_one_compatible_select_alternative()
    {
        var result = GeneratorHostTests.Run(
            FUNCTION_SELECT_ARGUMENT_CONSUMER,
            ("schemas/function-select-argument.exp", FUNCTION_SELECT_ARGUMENT_SCHEMA));
        var incompatible = GeneratorHostTests.Run(
            ("schemas/incompatible-function-select-argument.exp", INCOMPATIBLE_FUNCTION_SELECT_ARGUMENT_SCHEMA));
        var ambiguous = GeneratorHostTests.Run(
            ("schemas/ambiguous-function-select-argument.exp", AMBIGUOUS_FUNCTION_SELECT_ARGUMENT_SCHEMA));

        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "FunctionSelectArgumentConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));
        var incompatibleDiagnostics = incompatible.Diagnostics
            .Concat(incompatible.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var ambiguousDiagnostics = ambiguous.Diagnostics
            .Concat(ambiguous.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(Regex.Matches(generated, "ItemChoice\\.FromBaseItem\\(")).Count().IsEqualTo(3);
            await Assert.That(incompatibleDiagnostics).IsNotEmpty()
                .Because(string.Join(Environment.NewLine, incompatibleDiagnostics));
            await Assert.That(ambiguousDiagnostics).IsNotEmpty()
                .Because(string.Join(Environment.NewLine, ambiguousDiagnostics));
        }
    }

    /// <summary>Projects a SELECT-valued attribute proven by TYPEOF into an entity function formal.</summary>
    [Test]
    public async Task Should_project_typeof_guarded_select_paths_at_function_boundaries()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/typeof-guarded-select-path-argument.exp", TYPEOF_GUARDED_SELECT_PATH_ARGUMENT_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
    }

    /// <summary>
    /// Verifies a dynamically compatible entity application invokes only a matching runtime alternative.
    /// </summary>
    [Test]
    public async Task Should_guard_dynamically_narrow_entity_function_arguments()
    {
        var result = GeneratorHostTests.Run(
            DYNAMIC_ENTITY_APPLICATION_CONSUMER,
            ("schemas/dynamic-entity-application.exp", DYNAMIC_ENTITY_APPLICATION_SCHEMA));
        var incompatible = GeneratorHostTests.Run(
            ("schemas/incompatible-dynamic-entity-application.exp",
                INCOMPATIBLE_DYNAMIC_ENTITY_APPLICATION_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var incompatibleDiagnostics = incompatible.Diagnostics
            .Concat(incompatible.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
        await Assert.That(incompatibleDiagnostics).IsNotEmpty()
            .Because("statically disjoint entities must remain a type error");

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "DynamicEntityApplicationConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies same-kind entity sets narrow all elements atomically at an assignment boundary.
    /// </summary>
    [Test]
    public async Task Should_narrow_entity_sets_atomically_at_assignment_boundaries()
    {
        var result = GeneratorHostTests.Run(
            SET_ENTITY_ASSIGNMENT_CONSUMER,
            ("schemas/set-entity-assignment.exp", SET_ENTITY_ASSIGNMENT_SCHEMA));
        var controls = GeneratorHostTests.Run(
            ("schemas/set-entity-assignment-controls.exp", SET_ENTITY_ASSIGNMENT_CONTROLS));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var controlDiagnostics = controls.Diagnostics.Concat(controls.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
        using (Assert.Multiple())
        {
            await Assert.That(controlDiagnostics.Any(diagnostic =>
                diagnostic.GetMessage().Contains("ExpressBag", StringComparison.Ordinal))).IsTrue();
            await Assert.That(controlDiagnostics.Any(diagnostic =>
                diagnostic.GetMessage().Contains("ExpressList", StringComparison.Ordinal))).IsTrue();
            await Assert.That(controlDiagnostics.Any(diagnostic =>
                diagnostic.GetMessage().Contains("ExpressArray", StringComparison.Ordinal))).IsTrue();
            await Assert.That(controlDiagnostics.Any(diagnostic =>
                diagnostic.GetMessage().Contains("IUnrelated", StringComparison.Ordinal))).IsTrue();
        }

        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));
        await Assert.That(generated).DoesNotContain("Enumerable.Where(");
        await Assert.That(generated).DoesNotContain(".Cast<");
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "SetEntityAssignmentConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies local and return assignments enter a SELECT only through one compatible entity alternative.
    /// </summary>
    [Test]
    public async Task Should_adapt_entity_assignments_to_one_compatible_select_alternative()
    {
        var result = GeneratorHostTests.Run(
            SELECT_ASSIGNMENT_CONSUMER,
            ("schemas/select-assignment.exp", SELECT_ASSIGNMENT_SCHEMA));
        var incompatible = GeneratorHostTests.Run(
            ("schemas/incompatible-select-assignment.exp", INCOMPATIBLE_SELECT_ASSIGNMENT_SCHEMA));
        var ambiguous = GeneratorHostTests.Run(
            ("schemas/ambiguous-select-assignment.exp", AMBIGUOUS_SELECT_ASSIGNMENT_SCHEMA));
        var indeterminate = GeneratorHostTests.Run(
            ("schemas/indeterminate-select-assignment.exp", INDETERMINATE_SELECT_ASSIGNMENT_SCHEMA));

        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "SelectAssignmentConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(Regex.Matches(generated, "ItemChoice\\.FromBaseItem\\(")).Count().IsEqualTo(6);
            await Assert.That(incompatible.Diagnostics.Concat(incompatible.OutputCompilation.GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsNotEmpty();
            await Assert.That(ambiguous.Diagnostics.Concat(ambiguous.OutputCompilation.GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsNotEmpty();
            await Assert.That(indeterminate.Diagnostics.Concat(indeterminate.OutputCompilation.GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsNotEmpty();
        }
    }

    /// <summary>
    /// Verifies application arguments widen INTEGER to REAL without permitting the reverse narrowing.
    /// </summary>
    [Test]
    public async Task Should_widen_integer_application_arguments_to_real_formals()
    {
        var result = GeneratorHostTests.Run(
            NUMERIC_APPLICATION_WIDENING_CONSUMER,
            ("schemas/numeric-application-widening.exp", NUMERIC_APPLICATION_WIDENING_SCHEMA));
        var narrowing = GeneratorHostTests.Run(
            ("schemas/numeric-application-narrowing.exp", NUMERIC_APPLICATION_NARROWING_SCHEMA));
        var differentDefined = GeneratorHostTests.Run(
            ("schemas/different-defined-application.exp", DIFFERENT_DEFINED_APPLICATION_SCHEMA));
        var optionalDefined = GeneratorHostTests.Run(
            ("schemas/optional-defined-application.exp", OPTIONAL_DEFINED_APPLICATION_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "NumericApplicationWideningConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));
        var narrowingDiagnostics = narrowing.Diagnostics
            .Concat(narrowing.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var differentDefinedDiagnostics = differentDefined.Diagnostics
            .Concat(differentDefined.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var optionalDefinedDiagnostics = optionalDefined.Diagnostics
            .Concat(optionalDefined.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(generated).Contains("RealMeasure(new global::TedToolkit.Step21.RealValue(");
            await Assert.That(generated).Contains("new LabelValue(");
            await Assert.That(generated).Contains("new YearValue(");
            await Assert.That(generated).DoesNotContain("new YearValue(new YearValue(");
            await Assert.That(generated).Contains("ItemChoice.FromBaseItem(");
            await Assert.That(narrowingDiagnostics.Any(diagnostic => diagnostic.Id == "CS1503")).IsTrue();
            await Assert.That(differentDefinedDiagnostics).IsNotEmpty();
            await Assert.That(optionalDefinedDiagnostics).IsNotEmpty();
        }
    }

    /// <summary>
    /// Verifies assignment widens INTEGER values to REAL while retaining an indeterminate source as UNKNOWN.
    /// </summary>
    [Test]
    public async Task Should_widen_integer_assignments_to_real_targets()
    {
        var result = GeneratorHostTests.Run(
            NUMERIC_ASSIGNMENT_WIDENING_CONSUMER,
            ("schemas/numeric-assignment-widening.exp", NUMERIC_ASSIGNMENT_WIDENING_SCHEMA));
        var narrowing = GeneratorHostTests.Run(
            ("schemas/numeric-assignment-narrowing.exp", NUMERIC_ASSIGNMENT_NARROWING_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "NumericAssignmentWideningConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(narrowing.Diagnostics.Concat(narrowing.OutputCompilation.GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsNotEmpty();
        }
    }

    /// <summary>
    /// Verifies NVL selects a common numeric type without narrowing or exchanging nominal defined types.
    /// </summary>
    [Test]
    public async Task Should_promote_numeric_nvl_operands_to_their_common_type()
    {
        var bound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("schemas/numeric-nvl.exp", NUMERIC_NVL_SCHEMA)]);
        if (bound.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                bound.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(bound.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var nvlExpressions = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Application
                && string.Equals(expression.Operation, "NVL", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var controlBound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("schemas/numeric-nvl-controls.exp", NUMERIC_NVL_CONTROLS)]);
        if (controlBound.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                controlBound.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(controlBound.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var controlNvlExpressions = controlBound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Application
                && string.Equals(expression.Operation, "NVL", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var result = GeneratorHostTests.Run(
            NUMERIC_NVL_CONSUMER,
            ("schemas/numeric-nvl.exp", NUMERIC_NVL_SCHEMA));
        var controls = GeneratorHostTests.Run(("schemas/numeric-nvl-controls.exp", NUMERIC_NVL_CONTROLS));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "NumericNvlConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(nvlExpressions).Count().IsEqualTo(7);
            await Assert.That(nvlExpressions.Count(expression =>
                    expression.Type.Kind == ExpressExpressionTypeKind.Real))
                .IsEqualTo(6);
            await Assert.That(nvlExpressions.Single(expression =>
                    expression.Type.Kind == ExpressExpressionTypeKind.Integer).Type.CanBeIndeterminate)
                .IsFalse();
            await Assert.That(nvlExpressions.All(expression => !expression.Type.CanBeIndeterminate)).IsTrue();
            await Assert.That(controlNvlExpressions).Count().IsEqualTo(3);
            await Assert.That(controlNvlExpressions.Single(expression =>
                    expression.SourceText.Contains("maybe_real", StringComparison.Ordinal)
                    && expression.SourceText.Contains("1)", StringComparison.Ordinal)).Type.Kind)
                .IsEqualTo(ExpressExpressionTypeKind.Real);
            await Assert.That(controlNvlExpressions.Count(expression =>
                    expression.Type.Kind == ExpressExpressionTypeKind.Unresolved))
                .IsEqualTo(2);
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(controls.Diagnostics.Concat(controls.OutputCompilation.GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsNotEmpty();
        }
    }

    /// <summary>
    /// Verifies a nonoptional aggregate initializer propagates an UNKNOWN element as UNKNOWN.
    /// </summary>
    [Test]
    public async Task Should_guard_nonoptional_aggregate_initializer_elements()
    {
        var bound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource(
                "schemas/nullable-aggregate-initializer.exp",
                NULLABLE_AGGREGATE_INITIALIZER_SCHEMA)]);
        if (bound.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                bound.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(bound.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var initializers = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.AggregateInitializer)
            .ToArray();
        var guardedList = initializers.Single(expression =>
            expression.SourceText.Contains("maybe_real", StringComparison.Ordinal)
            && expression.SourceText.Contains("present", StringComparison.Ordinal)
            && expression.Type.DeclaredType is ExpressBoundAggregateType
            {
                Kind: ExpressAggregateKind.List,
            });
        var optionalArray = initializers.Single(expression =>
            expression.SourceText.Contains("maybe_real", StringComparison.Ordinal)
            && expression.SourceText.Contains("present", StringComparison.Ordinal)
            && expression.Type.DeclaredType is ExpressBoundAggregateType
            {
                Kind: ExpressAggregateKind.Array,
                IsOptional: true,
            });
        var result = GeneratorHostTests.Run(
            NULLABLE_AGGREGATE_INITIALIZER_CONSUMER,
            ("schemas/nullable-aggregate-initializer.exp", NULLABLE_AGGREGATE_INITIALIZER_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));

        using (Assert.Multiple())
        {
            await Assert.That(guardedList.Type.CanBeIndeterminate).IsTrue();
            await Assert.That(optionalArray.Type.CanBeIndeterminate).IsFalse();
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "NullableAggregateInitializerConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        var guardedStart = generated.LastIndexOf(
            "__ExpressFunction_GuardedList",
            StringComparison.Ordinal);
        var guardedEnd = generated.IndexOf(
            "private static",
            guardedStart + "__ExpressFunction_GuardedList".Length,
            StringComparison.Ordinal);
        var guardedCode = generated.Substring(guardedStart, guardedEnd - guardedStart);

        using (Assert.Multiple())
        {
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(guardedCode.Split(
                    "__ExpressFunction_MaybeReal(",
                    StringSplitOptions.None).Length - 1)
                .IsEqualTo(3);
        }
    }

    /// <summary>
    /// Verifies function and derived return boundaries reconstruct compatible defined scalar values.
    /// </summary>
    [Test]
    public async Task Should_reconstruct_defined_scalars_at_return_boundaries()
    {
        var result = GeneratorHostTests.Run(
            DEFINED_RETURN_BOUNDARY_CONSUMER,
            ("schemas/defined-return-boundary.exp", DEFINED_RETURN_BOUNDARY_SCHEMA));
        var differentDefined = GeneratorHostTests.Run(
            ("schemas/different-defined-return.exp", DIFFERENT_DEFINED_RETURN_SCHEMA));
        var nestedDefined = GeneratorHostTests.Run(
            ("schemas/nested-defined-return.exp", NESTED_DEFINED_RETURN_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
            await Assert.That(generated).Contains(".DimensionCount(");
            await Assert.That(generated).DoesNotContain("DimensionCount(new global::TedToolkit.Step21.Generated."
                + "DefinedReturnBoundaryModel.DimensionCount(");
            await Assert.That(string.Join(
                    Environment.NewLine,
                    nestedDefined.GeneratedSources.Select(source => source.SourceText.ToString())))
                .Contains(".NestedDimension(new global::TedToolkit.Step21.Generated."
                    + "NestedDefinedReturnModel.DimensionCount(");
            await Assert.That(differentDefined.Diagnostics.Concat(differentDefined.OutputCompilation.GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsNotEmpty();
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "DefinedReturnBoundaryConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies only structurally unique aggregate initializers inherit a SET function-parameter context.
    /// </summary>
    [Test]
    public async Task Should_contextually_bind_proven_unique_set_application_initializers()
    {
        var valid = GeneratorHostTests.Run(
            SET_APPLICATION_INITIALIZER_CONSUMER,
            ("schemas/set-application-initializer.exp", SET_APPLICATION_INITIALIZER_SCHEMA));
        var unsafeInitializers = GeneratorHostTests.Run(
            ("schemas/unsafe-set-application-initializer.exp", UNSAFE_SET_APPLICATION_INITIALIZER_SCHEMA));

        if (valid.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                valid.Diagnostics.Concat(valid.OutputCompilation.GetDiagnostics())));
        }

        await Assert.That(valid.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, valid.OutputCompilation.GetDiagnostics()));
        var assembly = Emit(valid.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "SetApplicationInitializerConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        var unsafeDiagnostics = unsafeInitializers.Diagnostics
            .Concat(unsafeInitializers.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(unsafeDiagnostics.Count(diagnostic => diagnostic.Id == "CS1503"))
                .IsGreaterThanOrEqualTo(5)
                .Because(string.Join(Environment.NewLine, unsafeDiagnostics));
        }
    }

    /// <summary>
    /// Verifies a SELECT of defined aggregate categories satisfies a general AGGREGATE formal without becoming a LIST.
    /// </summary>
    [Test]
    public async Task Should_preserve_selected_aggregate_categories_at_general_formals()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/select-aggregate-application.exp", SELECT_AGGREGATE_APPLICATION_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
            await Assert.That(generated).Contains(
                "global::TedToolkit.Step21.IExpressAggregate<global::TedToolkit.Step21.Generated."
                + "SelectAggregateApplicationModel.IRepresentationItem>");
            await Assert.That(generated).Contains(".Match(");
            await Assert.That(generated).Contains(".Value");
            await Assert.That(generated).Contains(".HighBound");
            await Assert.That(generated).Contains(".LowBound");
        }
    }

    /// <summary>
    /// Verifies OPTIONAL defined values retain nullability until their nominal wrappers are safely unwrapped.
    /// </summary>
    [Test]
    public async Task Should_check_optional_defined_wrappers_before_reading_their_values()
    {
        var result = GeneratorHostTests.Run(
            OPTIONAL_DEFINED_VALUE_CONSUMER,
            ("schemas/optional-defined-value.exp", OPTIONAL_DEFINED_VALUE_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "OptionalDefinedValueConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(generated).Contains("is { } __expressDefinedValue");
            await Assert.That(generated).DoesNotContain("?.Value");
        }
    }

    /// <summary>
    /// Verifies SELECT attributes lower through their selected entity alternative and retain UNKNOWN for a mismatch.
    /// </summary>
    [Test]
    public async Task Should_access_select_attributes_through_the_selected_entity_alternative()
    {
        var bound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource(
                "schemas/select-attribute-qualifier.exp",
                SELECT_ATTRIBUTE_QUALIFIER_SCHEMA)]);
        if (bound.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                bound.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(bound.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var qualifiers = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.AttributeQualifier)
            .ToArray();
        var partialSelectQualifiers = qualifiers.Where(expression =>
                expression.SourceText.EndsWith(".basis_surface", StringComparison.Ordinal)
                || expression.SourceText.EndsWith(".orientation", StringComparison.Ordinal)
                || expression.SourceText.EndsWith(".magnitude", StringComparison.Ordinal)
                || expression.SourceText.EndsWith(".direction_ratios", StringComparison.Ordinal))
            .ToArray();
        var soleAlternativeQualifier = qualifiers.Single(expression =>
            expression.SourceText.EndsWith(".dimensions", StringComparison.Ordinal));
        var result = GeneratorHostTests.Run(
            SELECT_ATTRIBUTE_QUALIFIER_CONSUMER,
            ("schemas/select-attribute-qualifier.exp", SELECT_ATTRIBUTE_QUALIFIER_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        using (Assert.Multiple())
        {
            await Assert.That(partialSelectQualifiers).Count().IsEqualTo(7);
            await Assert.That(partialSelectQualifiers.All(expression => expression.Type.CanBeIndeterminate))
                .IsTrue();
            await Assert.That(soleAlternativeQualifier.Type.CanBeIndeterminate).IsFalse();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            var generated = string.Join(
                Environment.NewLine,
                result.GeneratedSources.Select(source => source.SourceText.ToString()));
            var guardedBasisStart = generated.IndexOf(
                "private static ISurfaceValue? __ExpressFunction_GuardedBasis",
                StringComparison.Ordinal);
            var guardedBasis = generated.Substring(
                guardedBasisStart,
                generated.IndexOf(Environment.NewLine + "\t\t}", guardedBasisStart, StringComparison.Ordinal)
                - guardedBasisStart);
            var guardedVectorStart = generated.IndexOf(
                "private static global::TedToolkit.Step21.RealValue? __ExpressFunction_GuardedVectorMeasure",
                StringComparison.Ordinal);
            var guardedVector = generated.Substring(
                guardedVectorStart,
                generated.IndexOf(Environment.NewLine + "\t\t}", guardedVectorStart, StringComparison.Ordinal)
                - guardedVectorStart);
            var unguardedCallStart = generated.IndexOf(
                "private static global::TedToolkit.Step21.RealValue? __ExpressFunction_UnguardedCallMeasure",
                StringComparison.Ordinal);
            var unguardedCall = generated.Substring(
                unguardedCallStart,
                generated.IndexOf(Environment.NewLine + "\t\t}", unguardedCallStart, StringComparison.Ordinal)
                - unguardedCallStart);
            await Assert.That(guardedBasis).Contains("throw new global::System.InvalidOperationException()");
            await Assert.That(guardedVector).Contains("throw new global::System.InvalidOperationException()");
            await Assert.That(unguardedCall).Contains("default(");
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType("SelectAttributeQualifierConsumer", throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies lexical SELECT references unwrap only inside TYPEOF-proven branches.
    /// </summary>
    [Test]
    public async Task Should_narrow_lexical_select_references_in_typeof_branches()
    {
        var result = GeneratorHostTests.Run(
            SELECT_LEXICAL_NARROWING_CONSUMER,
            ("schemas/select-lexical-narrowing.exp", SELECT_LEXICAL_NARROWING_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "SelectLexicalNarrowingConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        var unguarded = GeneratorHostTests.Run(
            ("schemas/unguarded-select-lexical.exp", UNGUARDED_SELECT_LEXICAL_SCHEMA));

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(unguarded.Diagnostics.Concat(unguarded.OutputCompilation.GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsNotEmpty();
        }
    }

    /// <summary>
    /// Verifies TYPEOF facts narrow only the structurally identical qualified and indexed reference path.
    /// </summary>
    [Test]
    public async Task Should_narrow_qualified_index_paths_only_inside_the_proven_branch()
    {
        var result = GeneratorHostTests.Run(
            QUALIFIED_PATH_NARROWING_CONSUMER,
            ("schemas/qualified-path-narrowing.exp", QUALIFIED_PATH_NARROWING_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var condition = "IF 'QUALIFIED_PATH_NARROWING_CONTROL.SURFACE_VALUE' IN "
            + "TYPEOF(first\\first_carrier.primary_values[i]) THEN";
        var controls = new[]
        {
            (Condition: condition, Actual: "second\\first_carrier.primary_values[i]"),
            (Condition: condition, Actual: "first\\first_carrier.secondary_values[i]"),
            (Condition: condition, Actual: "first\\second_carrier.primary_values[i]"),
            (Condition: condition, Actual: "first\\first_carrier.primary_values[j]"),
            (Condition: "IF ('QUALIFIED_PATH_NARROWING_CONTROL.SURFACE_VALUE' IN "
                + "TYPEOF(first\\first_carrier.primary_values[i])) OR TRUE THEN",
                Actual: "first\\first_carrier.primary_values[i]"),
            (Condition: "IF NOT ('QUALIFIED_PATH_NARROWING_CONTROL.SURFACE_VALUE' IN "
                + "TYPEOF(first\\first_carrier.primary_values[i])) THEN",
                Actual: "first\\first_carrier.primary_values[i]"),
        };
        var invalidResults = controls.Select((control, index) => GeneratorHostTests.Run((
                $"schemas/qualified-path-control-{index}.exp",
                QUALIFIED_PATH_NARROWING_CONTROL
                    .Replace("__CONDITION__", control.Condition, StringComparison.Ordinal)
                    .Replace(
                        "__THEN_STATEMENT__",
                        $"RETURN(surface_code({control.Actual}));",
                        StringComparison.Ordinal)
                    .Replace("__AFTER_STATEMENT__", "RETURN(?);", StringComparison.Ordinal))))
            .Append(GeneratorHostTests.Run((
                "schemas/qualified-path-control-outside.exp",
                QUALIFIED_PATH_NARROWING_CONTROL
                    .Replace("__CONDITION__", condition, StringComparison.Ordinal)
                    .Replace("__THEN_STATEMENT__", "RETURN(0);", StringComparison.Ordinal)
                    .Replace(
                        "__AFTER_STATEMENT__",
                        "RETURN(surface_code(first\\first_carrier.primary_values[i]));",
                        StringComparison.Ordinal))))
            .Append(GeneratorHostTests.Run((
                "schemas/qualified-path-shadow-control.exp",
                QUALIFIED_PATH_SHADOW_CONTROL)))
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "QualifiedPathNarrowingConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(invalidResults.All(invalid => invalid.Diagnostics
                .Concat(invalid.OutputCompilation.GetDiagnostics())
                .Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)))
                .IsTrue();
        }
    }

    /// <summary>
    /// Verifies a true SIZEOF guard makes only its lexical aggregate determinate in the guarded branch.
    /// </summary>
    [Test]
    public async Task Should_reuse_sizeof_branch_determinacy_only_in_the_true_branch()
    {
        var result = GeneratorHostTests.Run(
            SIZEOF_BRANCH_DETERMINACY_CONSUMER,
            ("schemas/sizeof-branch-determinacy.exp", SIZEOF_BRANCH_DETERMINACY_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var repeat = "REPEAT i := 1 TO HIINDEX(items) BY 1; RETURN(TRUE); END_REPEAT;";
        var invalidStatements = new[]
        {
            $"IF SIZEOF(items) > 0 THEN RETURN(TRUE); ELSE {repeat} END_IF;",
            $"IF (SIZEOF(items) > 0) OR TRUE THEN {repeat} END_IF;",
            $"IF NOT (SIZEOF(items) = 0) THEN {repeat} END_IF;",
            $"IF SIZEOF(items) > 0 THEN items := items; END_IF; {repeat}",
        };
        var invalidResults = invalidStatements.Select((statements, index) => GeneratorHostTests.Run((
            $"schemas/sizeof-branch-control-{index}.exp",
            SIZEOF_BRANCH_DETERMINACY_CONTROL.Replace(
                "__STATEMENTS__",
                statements,
                StringComparison.Ordinal)))).ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "SizeofBranchDeterminacyConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(invalidResults.All(invalid => invalid.Diagnostics
                .Concat(invalid.OutputCompilation.GetDiagnostics())
                .Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)))
                .IsTrue();
        }
    }

    /// <summary>
    /// Verifies successful non-optional local assignments establish a lexical determinacy fact.
    /// </summary>
    [Test]
    public async Task Should_reuse_determinacy_after_a_successful_local_assignment()
    {
        var result = GeneratorHostTests.Run(
            ASSIGNED_LOCAL_DETERMINACY_CONSUMER,
            ("schemas/assigned-local-determinacy.exp", ASSIGNED_LOCAL_DETERMINACY_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "AssignedLocalDeterminacyConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies TYPEOF-proven scalar SELECT values participate in numeric operations only inside the proven branch.
    /// </summary>
    [Test]
    public async Task Should_narrow_lexical_select_references_to_terminal_scalars()
    {
        var result = GeneratorHostTests.Run(
            SELECT_SCALAR_NARROWING_CONSUMER,
            ("schemas/select-scalar-narrowing.exp", SELECT_SCALAR_NARROWING_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var unguarded = GeneratorHostTests.Run(
            ("schemas/unguarded-select-scalar.exp", UNGUARDED_SELECT_SCALAR_SCHEMA));

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
            await Assert.That(unguarded.Diagnostics.Concat(unguarded.OutputCompilation.GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsNotEmpty();
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "SelectScalarNarrowingConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies SELECT group qualifiers use the selected physical component and preserve UNKNOWN for a mismatch.
    /// </summary>
    [Test]
    public async Task Should_access_select_group_qualifiers_through_physical_components()
    {
        var bound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource(
                "schemas/select-group-qualifier.exp",
                SELECT_GROUP_QUALIFIER_SCHEMA)]);
        if (bound.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                bound.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(bound.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var groups = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.GroupQualifier)
            .ToArray();
        var partialGroups = groups.Where(expression =>
                expression.Children[0].Type.Kind == ExpressExpressionTypeKind.Select
                && (expression.SourceText.EndsWith("\\left", StringComparison.Ordinal)
                    || expression.SourceText.EndsWith("\\right", StringComparison.Ordinal)))
            .ToArray();
        var sharedGroups = groups.Where(expression =>
                expression.SourceText.EndsWith("\\root", StringComparison.Ordinal)
                && expression.Children[0].Type.Kind == ExpressExpressionTypeKind.Select)
            .ToArray();
        var directGroups = groups.Where(expression =>
                expression.SourceText.EndsWith("\\root", StringComparison.Ordinal)
                && expression.Children[0].Type.Kind == ExpressExpressionTypeKind.Entity)
            .ToArray();

        var result = GeneratorHostTests.Run(
            SELECT_GROUP_QUALIFIER_CONSUMER,
            ("schemas/select-group-qualifier.exp", SELECT_GROUP_QUALIFIER_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "SelectGroupQualifierConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));

        using (Assert.Multiple())
        {
            await Assert.That(partialGroups).IsNotEmpty();
            await Assert.That(partialGroups.All(expression => expression.Type.CanBeIndeterminate)).IsTrue();
            await Assert.That(sharedGroups).IsNotEmpty();
            await Assert.That(sharedGroups.All(expression => !expression.Type.CanBeIndeterminate)).IsTrue();
            await Assert.That(directGroups).IsNotEmpty();
            await Assert.That(directGroups.All(expression => !expression.Type.CanBeIndeterminate)).IsTrue();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(generated).Contains(
                ".Match<global::TedToolkit.Step21.Generated.SelectGroupQualifierModel.ILeft?>");
        }
    }

    /// <summary>
    /// Verifies BOOLEAN and LOGICAL expected-type boundaries preserve all three truth states.
    /// </summary>
    [Test]
    public async Task Should_adapt_boolean_and_logical_expected_types_without_losing_unknown()
    {
        var bound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("schemas/logical-boundary.exp", LOGICAL_BOUNDARY_SCHEMA)]);
        if (bound.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                bound.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(bound.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var indeterminateFunctions = bound.Schemas.Single().IndeterminateFunctions
            .Select(function => function.Name)
            .ToArray();
        var expressions = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .ToArray();
        var guardedApplications = expressions
            .Where(expression =>
                expression.Kind == ExpressExpressionKind.Application
                && string.Equals(
                    expression.Operation,
                    "determinate_boolean",
                    StringComparison.OrdinalIgnoreCase)
                && expression.Children.Any(child => child.Type.CanBeIndeterminate))
            .ToArray();
        var indeterminateHandlingBuiltins = expressions
            .Where(expression =>
                expression.Kind == ExpressExpressionKind.Application
                && (string.Equals(expression.Operation, "EXISTS", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(expression.Operation, "NVL", StringComparison.OrdinalIgnoreCase))
                && expression.Children.Any(child => child.Type.CanBeIndeterminate))
            .ToArray();
        var determinateApplication = expressions.Single(expression =>
            expression.Kind == ExpressExpressionKind.Application
            && string.Equals(
                expression.SourceText,
                "determinate_boolean(TRUE)",
                StringComparison.OrdinalIgnoreCase));
        var result = GeneratorHostTests.Run(
            LOGICAL_BOUNDARY_CONSUMER,
            ("schemas/logical-boundary.exp", LOGICAL_BOUNDARY_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType("LogicalBoundaryConsumer", throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        var validatePredicate = assembly.GetType("LogicalBoundaryConsumer", throwOnError: true)!
            .GetMethod(
                "ValidatePredicate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var truePredicate = (ValidationResult)validatePredicate.Invoke(null, [LogicalValue.True])!;
        var falsePredicate = (ValidationResult)validatePredicate.Invoke(null, [LogicalValue.False])!;
        var unknownPredicate = (ValidationResult)validatePredicate.Invoke(null, [LogicalValue.Unknown])!;
        var validateLogicalNormalization = assembly.GetType("LogicalBoundaryConsumer", throwOnError: true)!
            .GetMethod(
                "ValidateLogicalNormalization",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var trueLogicalNormalization = (ValidationResult)validateLogicalNormalization.Invoke(
            null,
            [LogicalValue.True])!;
        var falseLogicalNormalization = (ValidationResult)validateLogicalNormalization.Invoke(
            null,
            [LogicalValue.False])!;
        var unknownLogicalNormalization = (ValidationResult)validateLogicalNormalization.Invoke(
            null,
            [LogicalValue.Unknown])!;
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));

        using (Assert.Multiple())
        {
            await Assert.That(indeterminateFunctions).Contains("boolean_from_logical");
            await Assert.That(indeterminateFunctions).Contains("boolean_caller");
            await Assert.That(indeterminateFunctions).Contains("guarded_caller");
            await Assert.That(indeterminateFunctions).DoesNotContain("determinate_boolean");
            await Assert.That(guardedApplications.Length >= 4).IsTrue();
            await Assert.That(guardedApplications.All(expression => expression.Type.CanBeIndeterminate)).IsTrue();
            await Assert.That(indeterminateHandlingBuiltins).IsNotEmpty();
            await Assert.That(indeterminateHandlingBuiltins.All(
                expression => !expression.Type.CanBeIndeterminate)).IsTrue();
            await Assert.That(determinateApplication.Type.CanBeIndeterminate).IsFalse();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(truePredicate.IsValid).IsTrue();
            await Assert.That(falsePredicate.Failures.Select(failure => failure.Code))
                .Contains("LOGICAL_BOUNDARY_MODEL.PREDICATE_SAMPLE.WHERE.ACCEPTED");
            await Assert.That(unknownPredicate.Failures.Select(failure => failure.Code))
                .Contains("LOGICAL_BOUNDARY_MODEL.PREDICATE_SAMPLE.WHERE.ACCEPTED");
            await Assert.That(falseLogicalNormalization.IsValid).IsTrue();
            await Assert.That(trueLogicalNormalization.Failures.Select(failure => failure.Code))
                .Contains("LOGICAL_BOUNDARY_MODEL.LOGICAL_NORMALIZATION_SAMPLE.WHERE.GUARDED_VALUE");
            await Assert.That(unknownLogicalNormalization.Failures.Select(failure => failure.Code))
                .Contains("LOGICAL_BOUNDARY_MODEL.LOGICAL_NORMALIZATION_SAMPLE.WHERE.GUARDED_VALUE");
            await Assert.That(unknownLogicalNormalization.Failures.Select(failure => failure.Code))
                .Contains("LOGICAL_BOUNDARY_MODEL.LOGICAL_NORMALIZATION_SAMPLE.WHERE.NULLABLE_VALUE");
            await Assert.That(unknownLogicalNormalization.Failures.Select(failure => failure.Code))
                .Contains("LOGICAL_BOUNDARY_MODEL.LOGICAL_NORMALIZATION_SAMPLE.WHERE.RECURSIVE_NOT");
            await Assert.That(generated).Contains(
                "global::TedToolkit.Step21.LogicalValue.Unknown => (global::System.Boolean?)null");
            await Assert.That(generated).Contains(
                "true => global::TedToolkit.Step21.LogicalValue.True");
        }
    }

    /// <summary>
    /// Verifies STRING addition determines homogeneous aggregate element types without changing numeric promotion.
    /// </summary>
    [Test]
    public async Task Should_infer_string_concat_elements_without_retyping_numeric_aggregates()
    {
        var bound = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("schemas/string-concat.exp", STRING_CONCAT_SCHEMA)]);
        var result = GeneratorHostTests.Run(
            STRING_CONCAT_CONSUMER,
            ("schemas/string-concat.exp", STRING_CONCAT_SCHEMA));
        var invalidMixed = GeneratorHostTests.Run(
            ("schemas/invalid-string-numeric.exp", INVALID_STRING_NUMERIC_SCHEMA));
        var invalidAggregate = GeneratorHostTests.Run(
            ("schemas/invalid-heterogeneous-aggregate.exp", INVALID_HETEROGENEOUS_AGGREGATE_SCHEMA));
        var validDiagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var invalidMixedDiagnostics = invalidMixed.Diagnostics.Concat(invalidMixed.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var invalidAggregateDiagnostics = invalidAggregate.Diagnostics
            .Concat(invalidAggregate.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType("StringConcatConsumer", throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;
        var additionKinds = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Operation == "+")
            .Select(expression => expression.Type.Kind)
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(additionKinds).Contains(ExpressExpressionTypeKind.String);
            await Assert.That(additionKinds).Contains(ExpressExpressionTypeKind.Integer);
            await Assert.That(additionKinds).Contains(ExpressExpressionTypeKind.Real);
            await Assert.That(additionKinds).Contains(ExpressExpressionTypeKind.Number);
            await Assert.That(validDiagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, validDiagnostics));
            await Assert.That(validation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
            await Assert.That(invalidMixedDiagnostics).IsNotEmpty();
            await Assert.That(invalidAggregateDiagnostics).IsNotEmpty();
        }
    }

    /// <summary>
    /// Verifies SELECT value equality matches only compatible alternatives and retains UNKNOWN semantics.
    /// </summary>
    [Test]
    public async Task Should_compare_select_values_through_the_selected_typed_alternative()
    {
        var result = GeneratorHostTests.Run(
            SELECT_VALUE_EQUALITY_CONSUMER,
            ("schemas/select-value-equality.exp", SELECT_VALUE_EQUALITY_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("SelectValueEqualityConsumer", throwOnError: true)!;
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        ValidationResult Invoke(string method, params object?[] arguments) =>
            (ValidationResult)consumer.GetMethod(method, flags)!.Invoke(null, arguments)!;
        var matchingText = Invoke("ValidateText", "ok");
        var differentText = Invoke("ValidateText", "other");
        var matchingEnum = Invoke("ValidateEnum", true);
        var differentEnum = Invoke("ValidateEnum", false);
        var wrongAlternative = Invoke("ValidateWrongAlternative");
        var unknown = Invoke("ValidateUnknown");
        var entityEquality = GeneratorHostTests.Run(
            ENTITY_SELECT_VALUE_EQUALITY_CONSUMER,
            ("schemas/entity-select-value-equality.exp", ENTITY_SELECT_VALUE_EQUALITY_SCHEMA));
        var entityAssembly = Emit(entityEquality.OutputCompilation);
        var entityConsumer = entityAssembly.GetType(
            "EntitySelectValueEqualityConsumer",
            throwOnError: true)!;
        ValidationResult InvokeEntity(bool matching) => (ValidationResult)entityConsumer
            .GetMethod("Validate", flags)!
            .Invoke(null, [matching])!;
        var matchingEntity = InvokeEntity(true);
        var differentEntity = InvokeEntity(false);

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(matchingText.IsValid).IsTrue();
            await Assert.That(differentText.Failures.Select(failure => failure.Code))
                .Contains("SELECT_VALUE_EQUALITY_MODEL.RULE.TEXT_POPULATION_RULE.WHERE.ALL_MATCH");
            await Assert.That(matchingEnum.IsValid).IsTrue();
            await Assert.That(differentEnum.Failures.Select(failure => failure.Code))
                .Contains("SELECT_VALUE_EQUALITY_MODEL.RULE.ENUM_POPULATION_RULE.WHERE.ALL_MATCH");
            await Assert.That(wrongAlternative.Failures.Select(failure => failure.Code))
                .Contains("SELECT_VALUE_EQUALITY_MODEL.RULE.TEXT_POPULATION_RULE.WHERE.ALL_MATCH");
            await Assert.That(unknown.Failures.Select(failure => failure.Code))
                .Contains("SELECT_VALUE_EQUALITY_MODEL.OPTIONAL_SAMPLE.WHERE.MATCHES");
            await Assert.That(entityEquality.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(matchingEntity.IsValid).IsTrue();
            await Assert.That(differentEntity.Failures.Select(failure => failure.Code))
                .Contains("ENTITY_SELECT_VALUE_EQUALITY_MODEL.SAMPLE.WHERE.SAME_VALUE");
        }
    }

    /// <summary>
    /// Verifies SELECT instance equality unwraps nested alternatives without changing entity value equality.
    /// </summary>
    [Test]
    public async Task Should_compare_select_instances_through_nested_entity_alternatives()
    {
        var result = GeneratorHostTests.Run(
            SELECT_INSTANCE_EQUALITY_CONSUMER,
            ("schemas/select-instance-equality.exp", SELECT_INSTANCE_EQUALITY_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("SelectInstanceEqualityConsumer", throwOnError: true)!;
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        ValidationResult Invoke(string method, params object?[] arguments) =>
            (ValidationResult)consumer.GetMethod(method, flags)!.Invoke(null, arguments)!;
        var matching = Invoke("ValidateSelectEntity", 0);
        var wrongEntityAlternative = Invoke("ValidateSelectEntity", 1);
        var wrongScalarAlternative = Invoke("ValidateSelectEntity", 2);
        var unknown = Invoke("ValidateUnknown");
        var sameSelectedEntity = Invoke("ValidateSelectPair", true, false);
        var differentSelectedEntity = Invoke("ValidateSelectPair", false, false);
        var differentSelectedAlternative = Invoke("ValidateSelectPair", false, true);
        var sameEntity = Invoke("ValidateEntity", true, true);
        var distinctEqualEntity = Invoke("ValidateEntity", false, true);
        var distinctDifferentEntity = Invoke("ValidateEntity", false, false);

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
            await Assert.That(matching.IsValid).IsTrue();
            await Assert.That(wrongEntityAlternative.IsValid).IsFalse();
            await Assert.That(wrongScalarAlternative.IsValid).IsFalse();
            await Assert.That(unknown.Failures.Select(failure => failure.Code))
                .Contains("SELECT_INSTANCE_EQUALITY_MODEL.OPTIONAL_SELECT_SAMPLE.WHERE.UNKNOWN_IDENTITY");
            await Assert.That(sameSelectedEntity.IsValid).IsTrue();
            await Assert.That(differentSelectedEntity.IsValid).IsFalse();
            await Assert.That(differentSelectedAlternative.IsValid).IsFalse();
            await Assert.That(sameEntity.IsValid).IsTrue();
            await Assert.That(distinctEqualEntity.Failures.Select(failure => failure.Code))
                .Contains("SELECT_INSTANCE_EQUALITY_MODEL.ENTITY_PAIR_SAMPLE.WHERE.SAME_IDENTITY");
            await Assert.That(distinctEqualEntity.Failures.Select(failure => failure.Code))
                .DoesNotContain("SELECT_INSTANCE_EQUALITY_MODEL.ENTITY_PAIR_SAMPLE.WHERE.SAME_VALUE");
            await Assert.That(distinctDifferentEntity.Failures.Select(failure => failure.Code))
                .Contains("SELECT_INSTANCE_EQUALITY_MODEL.ENTITY_PAIR_SAMPLE.WHERE.SAME_VALUE");
        }
    }

    /// <summary>
    /// Verifies USEDIN unwraps complete all-entity SELECT carriers and preserves indeterminate values.
    /// </summary>
    [Test]
    public async Task Should_project_all_entity_select_carriers_for_usedin()
    {
        var result = GeneratorHostTests.Run(
            SELECT_USEDIN_CARRIER_CONSUMER,
            ("schemas/select-usedin-carrier.exp", SELECT_USEDIN_CARRIER_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("SelectUsedinCarrierConsumer", throwOnError: true)!;
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        ValidationResult Invoke(string method, params object?[] arguments) =>
            (ValidationResult)consumer.GetMethod(method, flags)!.Invoke(null, arguments)!;
        var firstAlternative = Invoke("ValidateSelect", false);
        var secondAlternative = Invoke("ValidateSelect", true);
        var nested = Invoke("ValidateNested");
        var entity = Invoke("ValidateEntity");
        var unknown = Invoke("ValidateUnknown");
        var mixed = GeneratorHostTests.Run(
            ("schemas/mixed-select-usedin.exp", MIXED_SELECT_USEDIN_SCHEMA));

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
            await Assert.That(firstAlternative.IsValid).IsTrue();
            await Assert.That(secondAlternative.IsValid).IsTrue();
            await Assert.That(nested.IsValid).IsTrue();
            await Assert.That(entity.IsValid).IsTrue();
            await Assert.That(unknown.Failures.Select(failure => failure.Code))
                .Contains("SELECT_USEDIN_CARRIER_MODEL.OPTIONAL_SAMPLE.WHERE.UNKNOWN_REFERENCE");
            await Assert.That(mixed.Diagnostics.Concat(mixed.OutputCompilation.GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsNotEmpty();
        }
    }

    /// <summary>
    /// Verifies entity value equality is cycle-safe, three-valued for missing optional slots, and exact by projection.
    /// </summary>
    [Test]
    public async Task Should_compare_entity_values_with_cycles_and_optional_unknown()
    {
        var result = GeneratorHostTests.Run(
            ENTITY_VALUE_CYCLE_CONSUMER,
            ("schemas/entity-value-cycle.exp", ENTITY_VALUE_CYCLE_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("EntityValueCycleConsumer", throwOnError: true)!;
        var validate = consumer.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        ValidationResult Invoke(int mode) => (ValidationResult)validate.Invoke(null, [mode])!;
        var selfCycle = Invoke(0);
        var mutualCycle = Invoke(1);
        var optionalUnknown = Invoke(2);
        var dynamicMismatch = Invoke(3);

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(selfCycle.IsValid).IsTrue();
            await Assert.That(mutualCycle.IsValid).IsTrue();
            await Assert.That(optionalUnknown.Failures.Select(failure => failure.Code))
                .Contains("ENTITY_VALUE_CYCLE_MODEL.SAMPLE.WHERE.SAME_VALUE");
            await Assert.That(dynamicMismatch.Failures.Select(failure => failure.Code))
                .Contains("ENTITY_VALUE_CYCLE_MODEL.SAMPLE.WHERE.SAME_VALUE");
        }
    }

    /// <summary>
    /// Verifies entity-valued aggregates retain ordered, domain, unset, and multiset equality semantics.
    /// </summary>
    [Test]
    public async Task Should_compare_entity_values_in_ordered_array_set_and_bag_aggregates()
    {
        var result = GeneratorHostTests.Run(
            ENTITY_VALUE_AGGREGATE_CONSUMER,
            ("schemas/entity-value-aggregate.exp", ENTITY_VALUE_AGGREGATE_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("EntityValueAggregateConsumer", throwOnError: true)!;
        var validate = consumer.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        ValidationResult Invoke(int mode) => (ValidationResult)validate.Invoke(null, [mode])!;
        var reorderedSetsAndBags = Invoke(0);
        var listOrderMismatch = Invoke(1);
        var arrayDomainMismatch = Invoke(2);
        var arrayUnset = Invoke(3);
        var bagMultiplicityMismatch = Invoke(5);
        var unknownPerfectMatch = Invoke(6);

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(reorderedSetsAndBags.IsValid).IsTrue();
            await Assert.That(listOrderMismatch.Failures.Select(failure => failure.Code))
                .Contains("ENTITY_VALUE_AGGREGATE_MODEL.SAMPLE.WHERE.LIST_SAME");
            await Assert.That(arrayDomainMismatch.Failures.Select(failure => failure.Code))
                .Contains("ENTITY_VALUE_AGGREGATE_MODEL.SAMPLE.WHERE.ARRAY_SAME");
            await Assert.That(arrayUnset.Failures.Select(failure => failure.Code))
                .Contains("ENTITY_VALUE_AGGREGATE_MODEL.SAMPLE.WHERE.ARRAY_SAME");
            await Assert.That(bagMultiplicityMismatch.Failures.Select(failure => failure.Code))
                .Contains("ENTITY_VALUE_AGGREGATE_MODEL.SAMPLE.WHERE.BAG_SAME");
            await Assert.That(unknownPerfectMatch.Failures.Select(failure => failure.Code))
                .Contains("ENTITY_VALUE_AGGREGATE_MODEL.SAMPLE.WHERE.SET_SAME");
            await Assert.That(unknownPerfectMatch.Failures.Select(failure => failure.Code))
                .Contains("ENTITY_VALUE_AGGREGATE_MODEL.SAMPLE.WHERE.BAG_SAME");
        }
    }

    /// <summary>
    /// Verifies synthetic complex entity equality preserves component storage, recursive references, and shape identity.
    /// </summary>
    [Test]
    public async Task Should_compare_synthetic_complex_entities_by_physical_storage_identity()
    {
        var result = GeneratorHostTests.Run(
            ENTITY_VALUE_COMPLEX_CONSUMER,
            ("schemas/entity-value-complex.exp", ENTITY_VALUE_COMPLEX_SCHEMA));
        if (result.GeneratedSources.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        }

        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("EntityValueComplexConsumer", throwOnError: true)!;
        var validate = consumer.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        ValidationResult Invoke(int mode) => (ValidationResult)validate.Invoke(null, [mode])!;
        var sameSlotsAndRecursiveEntityValue = Invoke(0);
        var differentSlot = Invoke(1);
        var differentEvaluatedSet = Invoke(2);
        var optionalUnknown = Invoke(3);

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(sameSlotsAndRecursiveEntityValue.IsValid)
                .IsTrue()
                .Because(string.Join(Environment.NewLine, sameSlotsAndRecursiveEntityValue.Failures.Select(
                    failure => $"{failure.Code}: {failure.Path}: {failure.Message}")));
            await Assert.That(differentSlot.Failures.Select(failure => failure.Code))
                .Contains("ENTITY_VALUE_COMPLEX_MODEL.HOLDER.WHERE.SAME_VALUE");
            await Assert.That(differentEvaluatedSet.Failures.Select(failure => failure.Code))
                .Contains("ENTITY_VALUE_COMPLEX_MODEL.HOLDER.WHERE.SAME_VALUE");
            await Assert.That(optionalUnknown.Failures.Select(failure => failure.Code))
                .Contains("ENTITY_VALUE_COMPLEX_MODEL.HOLDER.WHERE.SAME_VALUE");
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
    /// Verifies reachable LOCAL declarations and assignments execute in generated function helpers.
    /// </summary>
    [Test]
    public async Task Should_execute_reachable_function_locals_and_assignments()
    {
        var result = GeneratorHostTests.Run(
            REACHABLE_ALGORITHM_CONSUMER,
            ("schemas/unsupported.exp", REACHABLE_UNSUPPORTED_FUNCTION_SCHEMA));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("ReachableAlgorithmConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var valid = (ValidationResult)validate.Invoke(null, [new System.Numerics.BigInteger(1)])!;
        var invalid = (ValidationResult)validate.Invoke(null, [new System.Numerics.BigInteger(-1)])!;

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(valid.IsValid).IsTrue();
            await Assert.That(invalid.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(new[] { "UNSUPPORTED_MODEL.SAMPLE.WHERE.POSITIVE" });
        }
    }

    /// <summary>
    /// Verifies direct, local, and propagated function UNKNOWN results retain a nullable generated representation.
    /// </summary>
    [Test]
    public async Task Should_propagate_indeterminate_function_results_without_widening_determinate_results()
    {
        var bound = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("schemas/indeterminate-function.exp", INDETERMINATE_FUNCTION_SCHEMA),
        ]);
        var result = GeneratorHostTests.Run(
            INDETERMINATE_FUNCTION_CONSUMER,
            ("schemas/indeterminate-function.exp", INDETERMINATE_FUNCTION_SCHEMA));
        var invalidConstant = GeneratorHostTests.Run(
            ("schemas/indeterminate-constant-control.exp", INDETERMINATE_CONSTANT_CONTROL_SCHEMA));
        await Assert.That(bound.SyntaxDiagnostics)
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, bound.SyntaxDiagnostics.Select(diagnostic => diagnostic.Message)));
        await Assert.That(bound.BindingDiagnostics)
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, bound.BindingDiagnostics.Select(diagnostic => diagnostic.Message)));
        var derivedReferences = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind is ExpressExpressionKind.Reference
                && expression.Reference?.Attribute is { Kind: ExpressAttributeKind.Derived, })
            .ToArray();
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("IndeterminateFunctionConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var unknownKey = (ValidationResult)validate.Invoke(null, [true, true])!;
        var duplicateKey = (ValidationResult)validate.Invoke(null, [false, true])!;
        var descriptor = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.IndeterminateFunctionModel.SchemaDescriptor")!;
        var maybeAmount = descriptor.GetMembers("__ExpressDerived_Sample_MaybeAmount")
            .OfType<IMethodSymbol>()
            .Single();
        var propagatedAmount = descriptor.GetMembers("__ExpressDerived_Sample_PropagatedAmount")
            .OfType<IMethodSymbol>()
            .Single();
        var maybeList = descriptor.GetMembers("__ExpressDerived_Sample_MaybeList")
            .OfType<IMethodSymbol>()
            .Single();
        var certainAmount = descriptor.GetMembers("__ExpressDerived_Sample_CertainAmount")
            .OfType<IMethodSymbol>()
            .Single();

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(derivedReferences.Where(reference =>
                    reference.Reference!.Name != "certain_amount")
                .All(reference => reference.Type.CanBeIndeterminate)).IsTrue();
            await Assert.That(derivedReferences.Where(reference =>
                    reference.Reference!.Name == "certain_amount")
                .All(reference => !reference.Type.CanBeIndeterminate)).IsTrue();
            await Assert.That(maybeAmount.ReturnType.ToDisplayString()).EndsWith("?");
            await Assert.That(propagatedAmount.ReturnType.ToDisplayString()).EndsWith("?");
            await Assert.That(maybeList.ReturnType.NullableAnnotation).IsEqualTo(NullableAnnotation.Annotated);
            await Assert.That(certainAmount.ReturnType.ToDisplayString()).DoesNotEndWith("?");
            await Assert.That(invalidConstant.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "STEP21EXP006"
                && diagnostic.GetMessage().Contains(
                    "dependency 'invalid_value' can evaluate to the indeterminate value",
                    StringComparison.Ordinal))).IsTrue()
                .Because(string.Join(
                    Environment.NewLine,
                    invalidConstant.Diagnostics.Select(diagnostic => diagnostic.ToString())));
            await Assert.That(unknownKey.IsValid).IsTrue();
            await Assert.That(duplicateKey.Failures.Select(failure => failure.Code)).Contains(
                "INDETERMINATE_FUNCTION_MODEL.SAMPLE.UNIQUE.DERIVED_KEY");
        }
    }

    /// <summary>
    /// Verifies a generic LIST-to-SET helper infers its private type parameter from the first argument.
    /// </summary>
    [Test]
    public async Task Should_infer_a_private_generic_function_type_from_its_list_argument()
    {
        var result = GeneratorHostTests.Run(
            INFERRED_GENERIC_LIST_CONSUMER,
            ("schemas/inferred-generic-list.exp", INFERRED_GENERIC_LIST_SCHEMA));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("InferredGenericListConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validation = (ValidationResult)validate.Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies a numeric REPEAT control variable uses EXPRESS integer truncation when indexing an aggregate.
    /// </summary>
    [Test]
    public async Task Should_convert_a_number_repeat_variable_when_indexing_an_aggregate()
    {
        var result = GeneratorHostTests.Run(
            NUMBER_INDEX_CONSUMER,
            ("schemas/number-index.exp", NUMBER_INDEX_SCHEMA));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("NumberIndexConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validation = (ValidationResult)validate.Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies nested REPEAT bounds prove only the structurally identical qualified index path present.
    /// </summary>
    [Test]
    public async Task Should_reuse_safe_index_facts_for_identical_qualified_paths()
    {
        var result = GeneratorHostTests.Run(
            QUALIFIED_SAFE_INDEX_CONSUMER,
            ("schemas/qualified-safe-index.exp", QUALIFIED_SAFE_INDEX_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();

        await Assert.That(diagnostics).IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "QualifiedSafeIndexConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies a safe-index proof does not escape its exact aggregate, bounds, step, or REPEAT scope.
    /// </summary>
    [Test]
    public async Task Should_keep_non_matching_repeat_indices_indeterminate()
    {
        var result = GeneratorHostTests.Run(
            SAFE_INDEX_CONTROL_CONSUMER,
            ("schemas/safe-index-controls.exp", SAFE_INDEX_CONTROL_SCHEMA));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("SafeIndexControlConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validation = (ValidationResult)validate.Invoke(null, null)!;
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.IsValid).IsTrue();
            foreach (var methodName in new[]
                     {
                         "__ExpressFunction_DifferentAggregate",
                         "__ExpressFunction_DifferentBound",
                         "__ExpressFunction_DifferentStep",
                         "__ExpressFunction_OutsideRepeat",
                         "__ExpressFunction_RewrittenAlias",
                         "__ExpressFunction_DifferentAlias",
                         "__ExpressFunction_BranchRewrite",
                         "__ExpressFunction_BodyRewrite",
                         "__ExpressFunction_SourceRewrite",
                         "__ExpressFunction_BranchSourceRewrite",
                     })
            {
                var start = generated.IndexOf(methodName, StringComparison.Ordinal);
                var nextMethod = generated.IndexOf("private static", start + methodName.Length, StringComparison.Ordinal);
                var method = generated[start..(nextMethod < 0 ? generated.Length : nextMethod)];
                await Assert.That(method).Contains(" switch {");
                await Assert.That(method).Contains("(global::System.Numerics.BigInteger?)null");
            }
        }
    }

    /// <summary>
    /// Verifies mutable lexical and qualified-path facts are killed and joined by bound identity.
    /// </summary>
    [Test]
    public async Task Should_invalidate_mutable_flow_facts_at_assignments_and_control_flow_joins()
    {
        var bound = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("schemas/flow-fact-lifecycle.exp", FLOW_FACT_LIFECYCLE_SCHEMA),
        ]);
        if (bound.Schemas.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                bound.SyntaxDiagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                    .Concat(bound.BindingDiagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}: {diagnostic.Message}"))));
        }

        var narrowedApplications = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Application
                && string.Equals(expression.Reference?.Name, "first_code", StringComparison.OrdinalIgnoreCase)
                && expression.Children.Single().Type.Kind == ExpressExpressionTypeKind.Select)
            .Distinct()
            .ToArray();
        var result = GeneratorHostTests.Run(
            FLOW_FACT_LIFECYCLE_CONSUMER,
            ("schemas/flow-fact-lifecycle.exp", FLOW_FACT_LIFECYCLE_SCHEMA));
        var unprotected = GeneratorHostTests.Run(
            ("schemas/unprotected-select-application.exp", UNPROTECTED_SELECT_APPLICATION_SCHEMA));
        var diagnostics = result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));
        var immediateStart = generated.IndexOf("__ExpressFunction_ImmediateReturn", StringComparison.Ordinal);
        var immediateEnd = immediateStart < 0
            ? -1
            : generated.IndexOf("private static", immediateStart + 1, StringComparison.Ordinal);
        var immediateMethod = immediateStart < 0
            ? string.Empty
            : generated[immediateStart..(immediateEnd < 0 ? generated.Length : immediateEnd)];
        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, diagnostics));
            await Assert.That(narrowedApplications).IsNotEmpty();
            await Assert.That(narrowedApplications.All(application => application.Type.CanBeIndeterminate)).IsTrue();
            await Assert.That(unprotected.Diagnostics.Concat(unprotected.OutputCompilation.GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsNotEmpty();
            await Assert.That(immediateMethod).DoesNotContain("__parameter_InputValue =");
        }

        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "FlowFactLifecycleConsumer",
                throwOnError: true)!
            .GetMethod(
                "Validate",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies a generic ARRAY helper uses lexical numeric bounds and retains its nullable result.
    /// </summary>
    [Test]
    public async Task Should_generate_a_nullable_generic_array_with_lexical_bounds()
    {
        var bound = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("schemas/dynamic-generic-array.exp", DYNAMIC_GENERIC_ARRAY_SCHEMA),
        ]);
        var constructedArrays = bound.Schemas.Single().Expressions
            .Where(expression => expression.Kind == ExpressExpressionKind.AggregateInitializer
                && expression.Type.DeclaredType is ExpressBoundAggregateType
                {
                    Kind: ExpressAggregateKind.Array,
                })
            .ToArray();
        var nestedApplications = bound.Schemas.Single().Expressions
            .SelectMany(expression => expression.DescendantsAndSelf())
            .Where(expression => expression.Kind == ExpressExpressionKind.Application
                && string.Equals(expression.Reference?.Name, "nested_array", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();
        var result = GeneratorHostTests.Run(
            DYNAMIC_GENERIC_ARRAY_CONSUMER,
            ("schemas/dynamic-generic-array.exp", DYNAMIC_GENERIC_ARRAY_SCHEMA));
        var nestedMethod = result.OutputCompilation.GetTypeByMetadataName(
                "TedToolkit.Step21.Generated.DynamicGenericArrayModel.SchemaDescriptor")!
            .GetMembers("__ExpressFunction_NestedArray")
            .OfType<IMethodSymbol>()
            .Single();
        using (Assert.Multiple())
        {
            await Assert.That(bound.SyntaxDiagnostics).IsEmpty();
            await Assert.That(bound.BindingDiagnostics).IsEmpty();
            await Assert.That(constructedArrays).Count().IsEqualTo(3);
            await Assert.That(nestedApplications).Count().IsEqualTo(2);
            await Assert.That(nestedApplications.All(application =>
            {
                var element = ((ExpressBoundAggregateType)application.Type.DeclaredType!).ElementType;
                element = ((ExpressBoundAggregateType)element).ElementType;
                return element is ExpressBoundScalarType { Kind: ExpressScalarKind.Integer, };
            })).IsTrue();
            await Assert.That(constructedArrays.All(expression => expression.Type.CanBeIndeterminate)).IsTrue();
            await Assert.That(constructedArrays.Select(expression =>
                ((ExpressBoundAggregateType)expression.Type.DeclaredType!).LowerBoundText ?? string.Empty))
                .IsEquivalentTo(["low", "low", "low"]);
            await Assert.That(constructedArrays.Select(expression =>
                ((ExpressBoundAggregateType)expression.Type.DeclaredType!).UpperBoundText ?? string.Empty))
                .IsEquivalentTo(["high", "high", "high"]);
        }

        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("DynamicGenericArrayConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validation = (ValidationResult)validate.Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(nestedMethod.TypeParameters.Select(parameter => parameter.Name))
                .IsEquivalentTo(["TT", "TU"]);
            await Assert.That(validation.IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies nested generic specialization rejects unbound, conflicting, and incompatible aggregate shapes.
    /// </summary>
    [Test]
    public async Task Should_reject_incompatible_nested_generic_array_specializations()
    {
        var nestedStart = DYNAMIC_GENERIC_ARRAY_SCHEMA.IndexOf(
            "FUNCTION nested_array",
            StringComparison.Ordinal);
        var nestedEnd = DYNAMIC_GENERIC_ARRAY_SCHEMA.IndexOf(
            "END_FUNCTION;",
            nestedStart,
            StringComparison.Ordinal) + "END_FUNCTION;".Length;
        var nestedFunction = DYNAMIC_GENERIC_ARRAY_SCHEMA[nestedStart..nestedEnd];
        string[] invalidSchemas =
        [
            DYNAMIC_GENERIC_ARRAY_SCHEMA[..nestedStart]
                + nestedFunction.Replace("GENERIC:t", "GENERIC", StringComparison.Ordinal)
                + DYNAMIC_GENERIC_ARRAY_SCHEMA[nestedEnd..],
            DYNAMIC_GENERIC_ARRAY_SCHEMA[..nestedStart]
                + nestedFunction.Replace("GENERIC:u", "GENERIC_ENTITY:t", StringComparison.Ordinal)
                + DYNAMIC_GENERIC_ARRAY_SCHEMA[nestedEnd..],
            (DYNAMIC_GENERIC_ARRAY_SCHEMA[..nestedStart]
                + nestedFunction.Replace(
                    "LIST [0:?] OF GENERIC:t",
                    "LIST [2:2] OF GENERIC:t",
                    StringComparison.Ordinal)
                + DYNAMIC_GENERIC_ARRAY_SCHEMA[nestedEnd..]).Replace(
                    "values : LIST [1:1] OF LIST [2:2] OF INTEGER;",
                    "values : LIST [1:1] OF LIST [0:?] OF INTEGER;",
                    StringComparison.Ordinal),
            (DYNAMIC_GENERIC_ARRAY_SCHEMA[..nestedStart]
                + nestedFunction.Replace(
                    "values : LIST [1:?] OF LIST [0:?] OF GENERIC:t;",
                    "values : ARRAY [1:1] OF LIST [0:?] OF GENERIC:t;",
                    StringComparison.Ordinal)
                + DYNAMIC_GENERIC_ARRAY_SCHEMA[nestedEnd..]).Replace(
                    "values : LIST [1:1] OF LIST [2:2] OF INTEGER;",
                    "values : ARRAY [0:0] OF LIST [2:2] OF INTEGER;",
                    StringComparison.Ordinal),
        ];

        foreach (var schema in invalidSchemas)
        {
            var result = GeneratorHostTests.Run(("schemas/nested-generic-control.exp", schema));
            await Assert.That(result.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "STEP21EXP006"
                && diagnostic.GetMessage().Contains("no generated result type", StringComparison.Ordinal)))
                .IsTrue();
        }
    }

    /// <summary>
    /// Verifies a generated generic caller cannot close a callee through a different, absent, or conflicting label scope.
    /// </summary>
    [Test]
    public async Task Should_reject_unclosed_generic_array_call_scopes()
    {
        string[] invalidCallers =
        [
            """
            FUNCTION forward_array(values : LIST [0:?] OF GENERIC:u;
                         low, high : INTEGER) : ARRAY OF GENERIC:u;
              LOCAL result : ARRAY [low:high] OF GENERIC:u; END_LOCAL;
              result := [values[1] : SIZEOF(values)];
              RETURN(list_to_array(values, low, high));
            END_FUNCTION;
            """,
            """
            FUNCTION forward_array(values : LIST [0:?] OF GENERIC;
                         low, high : INTEGER) : ARRAY OF GENERIC;
              LOCAL result : ARRAY [low:high] OF GENERIC; END_LOCAL;
              result := [values[1] : SIZEOF(values)];
              RETURN(list_to_array(values, low, high));
            END_FUNCTION;
            """,
            """
            FUNCTION forward_array(values : LIST [0:?] OF GENERIC:t;
                         low, high : INTEGER) : ARRAY OF GENERIC:u;
              LOCAL result : ARRAY [low:high] OF GENERIC:u; END_LOCAL;
              result := [values[1] : SIZEOF(values)];
              RETURN(list_to_array(values, low, high));
            END_FUNCTION;
            """,
        ];
        var callerStart = DYNAMIC_GENERIC_ARRAY_SCHEMA.IndexOf(
            "FUNCTION forward_array",
            StringComparison.Ordinal);
        var callerEnd = DYNAMIC_GENERIC_ARRAY_SCHEMA.IndexOf(
            "END_FUNCTION;",
            callerStart,
            StringComparison.Ordinal) + "END_FUNCTION;".Length;

        foreach (var invalidCaller in invalidCallers)
        {
            var schema = DYNAMIC_GENERIC_ARRAY_SCHEMA[..callerStart]
                + invalidCaller
                + DYNAMIC_GENERIC_ARRAY_SCHEMA[callerEnd..];
            var result = GeneratorHostTests.Run(("schemas/generic-call-control.exp", schema));
            await Assert.That(result.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "STEP21EXP006"
                && diagnostic.GetMessage().Contains(
                    "Function 'list_to_array' has no generated result type",
                    StringComparison.Ordinal))).IsTrue();
        }
    }

    /// <summary>
    /// Verifies expected aggregate context specializes a generic BAG helper and its USEDIN input.
    /// </summary>
    [Test]
    public async Task Should_infer_a_generic_bag_element_from_the_expected_set_context()
    {
        var result = GeneratorHostTests.Run(
            EXPECTED_GENERIC_BAG_CONSUMER,
            ("schemas/expected-generic-bag.exp", EXPECTED_GENERIC_BAG_SCHEMA));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("ExpectedGenericBagConsumer", throwOnError: true)!;
        var validateTypedUsers = consumer.GetMethod(
            "ValidateTypedUsers",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validateControls = consumer.GetMethod(
            "ValidateConservativeControls",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var typedValidation = (ValidationResult)validateTypedUsers.Invoke(null, null)!;
        var controlValidation = (ValidationResult)validateControls.Invoke(null, null)!;
        var generated = string.Join(
            Environment.NewLine,
            result.GeneratedSources.Select(source => source.SourceText.ToString()));

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(typedValidation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, typedValidation.Failures.Select(failure => failure.Code)));
            await Assert.That(controlValidation.IsValid).IsTrue()
                .Because(string.Join(Environment.NewLine, controlValidation.Failures.Select(failure => failure.Code)));
            foreach (var methodName in new[]
                     {
                         "__ExpressFunction_DifferentBag",
                         "__ExpressFunction_DifferentLower",
                         "__ExpressFunction_DifferentStep",
                         "__ExpressFunction_OutsideRepeat",
                     })
            {
                var start = generated.LastIndexOf(methodName, StringComparison.Ordinal);
                var nextMethod = generated.IndexOf("private static", start + methodName.Length, StringComparison.Ordinal);
                var method = generated[start..(nextMethod < 0 ? generated.Length : nextMethod)];
                await Assert.That(method).Contains(" switch {");
                await Assert.That(method).Contains("(global::System.Numerics.BigInteger?)null");
            }
        }
    }

    /// <summary>
    /// Verifies dependency cycles stop generation with source evidence.
    /// </summary>
    [Test]
    public async Task Should_report_source_located_reachable_cycle_failures()
    {
        var cyclic = GeneratorHostTests.Run(("schemas/cyclic.exp", CYCLIC_DEPENDENCY_SCHEMA));
        var cycleDiagnostic = cyclic.Diagnostics.Single(diagnostic => diagnostic.Id == "STEP21EXP006");

        using (Assert.Multiple())
        {
            await Assert.That(cyclic.GeneratedSources).IsEmpty();
            await Assert.That(cycleDiagnostic.GetMessage()).Contains("dependency cycle");
            await Assert.That(cycleDiagnostic.Location.GetLineSpan().Path).IsEqualTo("schemas/cyclic.exp");
        }
    }

    /// <summary>
    /// Verifies a function/derived-attribute recursion component may terminate from runtime instance state.
    /// </summary>
    [Test]
    public async Task Should_execute_runtime_guarded_derived_attribute_recursion()
    {
        var result = GeneratorHostTests.Run(
            CYCLIC_DERIVED_DEPENDENCY_CONSUMER,
            ("schemas/repeated-cyclic-derived.exp", REPEATED_CYCLIC_DERIVED_DEPENDENCY_SCHEMA));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType(
                "CyclicDerivedDependencyConsumer",
                throwOnError: true)!
            .GetMethod("Validate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies ISO conditional REPEAT control, guarded fallthrough, and closure-free nested functions emit statically.
    /// </summary>
    [Test]
    public async Task Should_execute_supported_iso_algorithm_control_shapes()
    {
        var result = GeneratorHostTests.Run(
            ALGORITHM_CONTROL_CONSUMER,
            ("schemas/algorithm-control.exp", ALGORITHM_CONTROL_SCHEMA));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var assembly = Emit(result.OutputCompilation);
        var validation = (ValidationResult)assembly.GetType("AlgorithmControlConsumer", throwOnError: true)!
            .GetMethod("Validate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, null)!;

        await Assert.That(validation.IsValid).IsTrue()
            .Because(string.Join(Environment.NewLine, validation.Failures.Select(failure => failure.Code)));
    }

    /// <summary>
    /// Verifies a validation-reachable function may recurse into itself while retaining rule behavior.
    /// </summary>
    [Test]
    public async Task Should_execute_self_recursive_reachable_functions()
    {
        var result = GeneratorHostTests.Run(
            SELF_RECURSIVE_FUNCTION_CONSUMER,
            ("schemas/self-recursive.exp", SELF_RECURSIVE_FUNCTION_SCHEMA));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("SelfRecursiveFunctionConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var valid = (ValidationResult)validate.Invoke(null, [new System.Numerics.BigInteger(3)])!;
        var invalid = (ValidationResult)validate.Invoke(null, [new System.Numerics.BigInteger(-1)])!;

        using (Assert.Multiple())
        {
            await Assert.That(valid.IsValid).IsTrue();
            await Assert.That(invalid.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(new[] { "SELF_RECURSIVE_MODEL.SAMPLE.WHERE.REACHES_ZERO" });
        }
    }

    /// <summary>
    /// Verifies mutually recursive validation-reachable functions execute as one closed function component.
    /// </summary>
    [Test]
    public async Task Should_execute_mutually_recursive_reachable_functions()
    {
        var result = GeneratorHostTests.Run(
            MUTUALLY_RECURSIVE_FUNCTION_CONSUMER,
            ("schemas/mutually-recursive.exp", MUTUALLY_RECURSIVE_FUNCTION_SCHEMA));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("MutuallyRecursiveFunctionConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var valid = (ValidationResult)validate.Invoke(null, [new System.Numerics.BigInteger(4)])!;
        var invalid = (ValidationResult)validate.Invoke(null, [new System.Numerics.BigInteger(3)])!;

        using (Assert.Multiple())
        {
            await Assert.That(valid.IsValid).IsTrue();
            await Assert.That(invalid.Failures.Select(failure => failure.Code))
                .IsEquivalentTo(new[] { "MUTUALLY_RECURSIVE_MODEL.SAMPLE.WHERE.EVEN_AMOUNT" });
        }
    }

    /// <summary>
    /// Verifies a recursion component containing a constant remains a source-located generation failure.
    /// </summary>
    [Test]
    public async Task Should_reject_mixed_function_and_constant_dependency_cycles()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/mixed-recursive.exp", MIXED_RECURSIVE_DEPENDENCY_SCHEMA));
        var diagnostic = result.Diagnostics.Single(diagnostic =>
            diagnostic.Id == "STEP21EXP006" && diagnostic.GetMessage().Contains("dependency cycle"));

        using (Assert.Multiple())
        {
            await Assert.That(result.GeneratedSources).IsEmpty();
            await Assert.That(diagnostic.GetMessage()).Contains("recursive_function");
            await Assert.That(diagnostic.Location.GetLineSpan().Path).IsEqualTo("schemas/mixed-recursive.exp");
        }
    }

    /// <summary>
    /// Verifies model-context functions and inverse attributes execute against the existing validation population.
    /// </summary>
    [Test]
    public async Task Should_execute_model_context_functions_and_inverse_attributes_from_the_validation_population()
    {
        var result = GeneratorHostTests.Run(
            MODEL_CONTEXT_CONSUMER,
            ("schemas/model-context.exp", MODEL_CONTEXT_SCHEMA));

        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            .IsEmpty();

        var assembly = Emit(result.OutputCompilation);
        var validate = assembly.GetType("ModelContextConsumer", throwOnError: true)!.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validation = (ValidationResult)validate.Invoke(null, null)!;

        using (Assert.Multiple())
        {
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(validation.IsValid).IsTrue();
        }
    }

    /// <summary>
    /// Verifies entity-valued inverse resolution, invocation-local reuse, and lazy rule access.
    /// </summary>
    [Test]
    public async Task Should_execute_validation_reachable_singular_inverse_attributes()
    {
        var result = GeneratorHostTests.Run(
            SINGULAR_INVERSE_CONSUMER,
            ("schemas/singular-inverse.exp", SINGULAR_INVERSE_SCHEMA));
        var generatorDiagnostics = result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        await Assert.That(generatorDiagnostics)
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, generatorDiagnostics.Select(item => item.ToString())));
        var compilationDiagnostics = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        await Assert.That(compilationDiagnostics)
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, compilationDiagnostics.Select(item => item.ToString())));
        var targetType = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.SingularInverseModel.Target")!;
        var targetInterface = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.SingularInverseModel.ITarget")!;

        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("SingularInverseConsumer", throwOnError: true)!;
        var validate = consumer.GetMethod(
            "Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validateLazy = consumer.GetMethod(
            "ValidateLazy",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validateCountingOwner = consumer.GetMethod(
            "ValidateCountingOwner",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var writeZero = consumer.GetMethod(
            "WriteZero",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var readZero = consumer.GetMethod(
            "ReadZero",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var writeMany = consumer.GetMethod(
            "WriteMany",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var readMany = consumer.GetMethod(
            "ReadMany",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validateUnregistered = consumer.GetMethod(
            "ValidateUnregistered",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var writeUnregistered = consumer.GetMethod(
            "WriteUnregistered",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var unique = (ValidationResult)validate.Invoke(
            null,
            [1, true, new System.Numerics.BigInteger(1)])!;
        var zero = (ValidationResult)validate.Invoke(
            null,
            [0, false, new System.Numerics.BigInteger(-1)])!;
        var many = (ValidationResult)validate.Invoke(
            null,
            [2, false, new System.Numerics.BigInteger(1)])!;
        var explicitZero = (ValidationResult)validate.Invoke(
            null,
            [0, false, new System.Numerics.BigInteger(1)])!;
        var counting = (ValidationResult)validateCountingOwner.Invoke(null, null)!;
        var lazy = (ValidationResult)validateLazy.Invoke(null, null)!;
        var writeFailure = (ValidationResult)writeZero.Invoke(null, null)!;
        var readFailure = (ValidationResult)readZero.Invoke(null, null)!;
        var writeManyFailure = (ValidationResult)writeMany.Invoke(null, null)!;
        var readManyFailure = (ValidationResult)readMany.Invoke(null, null)!;
        var unregisteredZero = (ValidationResult)validateUnregistered.Invoke(null, [0])!;
        var unregisteredMany = (ValidationResult)validateUnregistered.Invoke(null, [2])!;
        var writeUnregisteredZero = (ValidationResult)writeUnregistered.Invoke(null, [0])!;
        var writeUnregisteredMany = (ValidationResult)writeUnregistered.Invoke(null, [2])!;
        var ownerQueryCount = (int)consumer.GetProperty(
            "OwnerQueryCount",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
        var lazyOwnerQueryCount = (int)consumer.GetProperty(
            "LazyOwnerQueryCount",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;

        using (Assert.Multiple())
        {
            await Assert.That(targetType.GetMembers("SingleOwner")).IsEmpty();
            await Assert.That(targetInterface.GetMembers("SingleOwner")).IsEmpty();
            await Assert.That(unique.IsValid).IsTrue();
            await Assert.That(zero.Failures.Select(failure => (failure.Code, failure.Path)).SequenceEqual(new[]
                {
                    ("SINGULAR_INVERSE_MODEL.TARGET.SINGLE_OWNER.INVERSE_CARDINALITY", "DataSections[0].#1.SingleOwner"),
                    ("SINGULAR_INVERSE_MODEL.TARGET.WHERE.INDEPENDENT_RULE", "DataSections[0].#1"),
                })).IsTrue();
            await Assert.That(zero.Failures[0].Message)
                .Contains("SINGULAR_INVERSE_MODEL.OWNER.TARGETS")
                .And.Contains("0");
            await Assert.That(zero.Failures[0].SourceLocation!.FilePath)
                .IsEqualTo("singular-inverse.exp");
            await Assert.That(zero.Failures[0].SourceLocation!.Line).IsEqualTo(5);
            await Assert.That(zero.Failures[0].SourceLocation!.Column).IsEqualTo(3);
            await Assert.That(many.Failures.Select(failure => (failure.Code, failure.Path)).SequenceEqual(new[]
                {
                    ("SINGULAR_INVERSE_MODEL.TARGET.SINGLE_OWNER.INVERSE_CARDINALITY", "DataSections[0].#1.SingleOwner"),
                })).IsTrue();
            await Assert.That(many.Failures[0].Message)
                .Contains("SINGULAR_INVERSE_MODEL.OWNER.TARGETS")
                .And.Contains("2");
            await Assert.That(many.Failures[0].SourceLocation!.FilePath)
                .IsEqualTo("singular-inverse.exp");
            await Assert.That(many.Failures[0].SourceLocation!.Line).IsEqualTo(5);
            await Assert.That(many.Failures[0].SourceLocation!.Column).IsEqualTo(3);
            await Assert.That(ownerQueryCount).IsEqualTo(3);
            await Assert.That(counting.Failures.Any(failure => failure.Code.EndsWith(
                ".INVERSE_CARDINALITY",
                StringComparison.Ordinal))).IsFalse();
            await Assert.That(lazyOwnerQueryCount).IsEqualTo(0);
            await Assert.That(lazy.Failures.Select(failure => failure.Code).SequenceEqual(new[]
                {
                    "SINGULAR_INVERSE_MODEL.LAZY_TARGET.WHERE.FALSE_SHORT_CIRCUIT",
                    "SINGULAR_INVERSE_MODEL.STRUCTURE.ENTITY_ASSIGNABILITY",
                })).IsTrue();
            await Assert.That(writeFailure.Failures.Select(FailureEvidence).SequenceEqual(
                explicitZero.Failures.Select(FailureEvidence))).IsTrue();
            await Assert.That(readFailure.Failures.Select(FailureEvidence).SequenceEqual(
                explicitZero.Failures.Select(FailureEvidence))).IsTrue();
            await Assert.That(writeManyFailure.Failures.Select(FailureEvidence).SequenceEqual(
                many.Failures.Select(FailureEvidence))).IsTrue();
            await Assert.That(readManyFailure.Failures.Select(FailureEvidence).SequenceEqual(
                many.Failures.Select(FailureEvidence))).IsTrue();
            await Assert.That(unregisteredZero.Failures.Select(failure => (failure.Code, failure.Path)).SequenceEqual(new[]
                {
                    ("P21.STRUCTURE.REFERENCE.REGISTRATION", "DataSections[0].#1.DirectReferences[0]"),
                })).IsTrue();
            await Assert.That(unregisteredMany.Failures.Select(failure => (failure.Code, failure.Path)).SequenceEqual(new[]
                {
                    ("P21.STRUCTURE.REFERENCE.REGISTRATION", "DataSections[0].#1.DirectReferences[0]"),
                    ("P21.STRUCTURE.REFERENCE.REGISTRATION", "DataSections[0].#3.DirectReferences[0]"),
                    ("P21.STRUCTURE.REFERENCE.REGISTRATION", "DataSections[0].#4.DirectReferences[0]"),
                })).IsTrue();
            await Assert.That(writeUnregisteredZero.Failures.Select(FailureEvidence).SequenceEqual(
                unregisteredZero.Failures.Select(FailureEvidence))).IsTrue();
            await Assert.That(writeUnregisteredMany.Failures.Select(FailureEvidence).SequenceEqual(
                unregisteredMany.Failures.Select(FailureEvidence))).IsTrue();
        }
    }

    /// <summary>
    /// Verifies the complete mixed-failure sequence is stable across validation and writer preflight.
    /// </summary>
    [Test]
    public async Task Should_preserve_complete_singular_inverse_failure_order_across_validation_and_writer_preflight()
    {
        var result = GeneratorHostTests.Run(
            SINGULAR_INVERSE_ORDER_CONSUMER,
            ("schemas/singular-inverse-order.exp", SINGULAR_INVERSE_ORDER_SCHEMA));
        var diagnostics = result.OutputCompilation.GetDiagnostics()
            .Concat(result.Diagnostics)
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        await Assert.That(diagnostics)
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, diagnostics.Select(item => item.ToString())));

        var assembly = Emit(result.OutputCompilation);
        var consumer = assembly.GetType("SingularInverseOrderConsumer", throwOnError: true)!;
        var validateTwice = consumer.GetMethod(
            "ValidateTwice",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var write = consumer.GetMethod(
            "Write",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validations = (ValidationResult[])validateTwice.Invoke(null, null)!;
        var writeFailure = (ValidationResult)write.Invoke(null, null)!;
        var firstEvidence = validations[0].Failures.Select(FailureEvidence).ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(validations[1].Failures.Select(FailureEvidence).SequenceEqual(firstEvidence)).IsTrue();
            await Assert.That(writeFailure.Failures.Select(FailureEvidence).SequenceEqual(firstEvidence)).IsTrue();
            await Assert.That(validations[0].Failures.Select(failure => (failure.Code, failure.Path)).SequenceEqual(new[]
                {
                    ("SINGULAR_INVERSE_ORDER_MODEL.ORDER_TARGET.VALUES.AGGREGATE_0.LOWER_BOUND", "DataSections[0].#1.Values"),
                    ("SINGULAR_INVERSE_ORDER_MODEL.ORDER_TARGET.SINGLE_OWNER.INVERSE_CARDINALITY", "DataSections[0].#1.SingleOwner"),
                    ("SINGULAR_INVERSE_ORDER_MODEL.ORDER_TARGET.WHERE.LATER_WHERE", "DataSections[0].#1"),
                    ("SINGULAR_INVERSE_ORDER_MODEL.ORDER_TARGET.WHERE.LATER_WHERE", "DataSections[0].#2"),
                    ("SINGULAR_INVERSE_ORDER_MODEL.ORDER_TARGET.UNIQUE.UNIQUE_CODE", "DataSections[0].#2.Code"),
                    ("SINGULAR_INVERSE_ORDER_MODEL.RULE.POPULATION_RULE.WHERE.GLOBAL_FAILURE", "Schema[singular_inverse_order_model].population_rule"),
                    ("P21.STRUCTURE.REFERENCE.REGISTRATION", "DataSections[0].#3.DirectReferences[1]"),
                })).IsTrue().Because(string.Join(Environment.NewLine, firstEvidence));
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
            (SchemaName: "SingularInverseModel", Source: SINGULAR_INVERSE_SCHEMA),
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

    private static string FailureEvidence(ValidationFailure failure)
    {
        return $"{failure.Code}|{failure.Path}|{failure.Message}|"
            + $"{failure.SourceLocation?.FilePath}|{failure.SourceLocation?.Line}|{failure.SourceLocation?.Column}";
    }

    private static bool IsExecutableRuleCode(string value)
    {
        return value.Contains(".WHERE.", StringComparison.Ordinal)
            || value.Contains(".UNIQUE.", StringComparison.Ordinal)
            || value.EndsWith(".INVERSE_CARDINALITY", StringComparison.Ordinal);
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
