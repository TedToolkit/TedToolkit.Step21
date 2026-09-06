using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExchangeStructureTests;
using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>
/// Proves generated schema descriptor identity and static cross-assembly dispatch.
/// </summary>
public sealed class GeneratedSchemaDescriptorTests
{
    private const string SIMPLE_SCHEMA = """
        SCHEMA simple_mapping;
        ENTITY item;
          name : STRING;
          note : OPTIONAL STRING;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SCALAR_SCHEMA = """
        SCHEMA scalar_mapping;
        ENTITY scalar_values;
          integer_value : INTEGER;
          real_value : REAL;
          number_integer : NUMBER;
          number_real : NUMBER;
          string_value : STRING;
          binary_value : BINARY;
          boolean_value : BOOLEAN;
          logical_value : LOGICAL;
          optional_text : OPTIONAL STRING;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NOMINAL_SCHEMA = """
        SCHEMA nominal_mapping;
        TYPE identifier = STRING;
        END_TYPE;
        TYPE nested_identifier = identifier;
        END_TYPE;
        TYPE status = ENUMERATION OF (active, inactive);
        END_TYPE;
        TYPE open_status = EXTENSIBLE ENUMERATION OF (custom);
        END_TYPE;
        ENTITY nominal_values;
          identifier_value : nested_identifier;
          status_value : status;
          open_status_value : open_status;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string REFERENCE_SCHEMA = """
        SCHEMA reference_mapping;
        ENTITY target;
          label : STRING;
        END_ENTITY;
        ENTITY holder;
          target_ref : target;
          optional_ref : OPTIONAL target;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SELECT_SCHEMA = """
        SCHEMA select_mapping;
        TYPE identifier = STRING;
        END_TYPE;
        ENTITY target;
        END_ENTITY;
        TYPE choice = SELECT (identifier, target);
        END_TYPE;
        ENTITY holder;
          typed_choice : choice;
          entity_choice : choice;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string AGGREGATE_SCHEMA = """
        SCHEMA aggregate_mapping;
        ENTITY target;
        END_ENTITY;
        ENTITY aggregate_values;
          array_value : ARRAY [1:3] OF OPTIONAL UNIQUE STRING;
          list_value : LIST [1:?] OF UNIQUE INTEGER;
          bag_value : BAG [0:4] OF STRING;
          set_value : SET [1:3] OF target;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string DESCRIPTOR_NAME_COLLISION_SCHEMA = """
        SCHEMA descriptor_name_collision;
        ENTITY schema_descriptor;
        END_ENTITY;
        END_SCHEMA;
        """;

    /// <summary>
    /// Verifies one sealed singleton descriptor exposes its exact schema name and allocates supported entities.
    /// </summary>
    [Test]
    public async Task Should_generate_sealed_singleton_descriptor_with_static_allocation_dispatch()
    {
        var result = GeneratorHostTests.Run(("schemas/simple.exp", SIMPLE_SCHEMA));
        var descriptorType = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.SimpleMapping.SchemaDescriptor")
            ?? throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                .IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                .IsEmpty();
            await Assert.That(descriptorType.IsSealed).IsTrue();
            await Assert.That(descriptorType.IsRecord).IsFalse();
            await Assert.That(descriptorType.BaseType?.ToDisplayString())
                .IsEqualTo("TedToolkit.Step21.SchemaDescriptor");
            await Assert.That(descriptorType.InstanceConstructors.Single().DeclaredAccessibility)
                .IsEqualTo(Accessibility.Private);
            await Assert.That(descriptorType.GetMembers("Instance").OfType<IPropertySymbol>().Single().IsStatic).IsTrue();
            await Assert.That(descriptorType.GetTypeMembers()).IsEmpty();
        }

        var assembly = Emit(result.OutputCompilation);
        var runtimeType = assembly.GetType(
            "TedToolkit.Step21.Generated.SimpleMapping.SchemaDescriptor",
            throwOnError: true)!;
        var first = (SchemaDescriptor)runtimeType.GetProperty("Instance")!.GetValue(null)!;
        var second = (SchemaDescriptor)runtimeType.GetProperty("Instance")!.GetValue(null)!;
        var allocated = first.AllocateEntity(["ITEM"]);

        using (Assert.Multiple())
        {
            await Assert.That(second).IsSameReferenceAs(first);
            await Assert.That(first.Name).IsEqualTo(new SchemaName("simple_mapping"));
            await Assert.That(allocated?.GetType().FullName)
                .IsEqualTo("TedToolkit.Step21.Generated.SimpleMapping.Item");
            await Assert.That(first.AllocateEntity(["UNKNOWN"])).IsNull();
            await Assert.That(first.AllocateEntity(["ITEM", "ITEM"])).IsNull();
        }
    }

    /// <summary>
    /// Verifies a simple component hydrates and projects mandatory and absent OPTIONAL values in schema order.
    /// </summary>
    [Test]
    public async Task Should_hydrate_and_project_simple_parameters_with_optional_absence()
    {
        var result = GeneratorHostTests.Run(("schemas/simple.exp", SIMPLE_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var descriptorType = assembly.GetType(
            "TedToolkit.Step21.Generated.SimpleMapping.SchemaDescriptor",
            throwOnError: true)!;
        var descriptor = (SchemaDescriptor)descriptorType.GetProperty("Instance")!.GetValue(null)!;
        var entity = descriptor.AllocateEntity(["ITEM"])
            ?? throw new InvalidOperationException("The generated descriptor did not allocate ITEM.");
        var structure = new ExchangeStructure(TestHeader.Create(), [descriptor]);
        var components = new KeyValuePair<string, IReadOnlyList<ParameterValue>>[]
        {
            new("ITEM", [ParameterValue.FromString("required"), ParameterValue.Omitted]),
        };

        var diagnostics = descriptor.HydrateEntity(structure, entity, components);
        var projected = descriptor.ProjectEntity(entity);

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty();
            await Assert.That((string?)entity.GetType().GetProperty("Name")!.GetValue(entity))
                .IsEqualTo("required");
            await Assert.That(entity.GetType().GetProperty("Note")!.GetValue(entity)).IsNull();
            await Assert.That(projected).IsEquivalentTo(components);
        }
    }

    /// <summary>
    /// Verifies every raw scalar alternative round-trips through ordered strong parameters without narrowing.
    /// </summary>
    [Test]
    public async Task Should_hydrate_and_project_every_raw_scalar_parameter()
    {
        var result = GeneratorHostTests.Run(("schemas/scalars.exp", SCALAR_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.ScalarMapping.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var entity = descriptor.AllocateEntity(["SCALAR_VALUES"])
            ?? throw new InvalidOperationException("The generated descriptor did not allocate SCALAR_VALUES.");
        var parameters = new ParameterValue[]
        {
            ParameterValue.FromInteger(System.Numerics.BigInteger.Parse("18446744073709551616")),
            ParameterValue.FromReal(new RealValue(125, -2)),
            ParameterValue.FromInteger(new System.Numerics.BigInteger(-7)),
            ParameterValue.FromReal(new RealValue(1, -30)),
            ParameterValue.FromString("decoded"),
            ParameterValue.FromBinary(new BinaryValue("00101")),
            ParameterValue.FromBoolean(true),
            ParameterValue.FromLogical(LogicalValue.Unknown),
            ParameterValue.Omitted,
        };
        var components = new KeyValuePair<string, IReadOnlyList<ParameterValue>>[]
        {
            new("SCALAR_VALUES", parameters),
        };

        var diagnostics = descriptor.HydrateEntity(
            new ExchangeStructure(TestHeader.Create(), [descriptor]),
            entity,
            components);
        var projected = descriptor.ProjectEntity(entity);

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty();
            await Assert.That(projected).HasSingleItem();
            await Assert.That(projected[0].Key).IsEqualTo("SCALAR_VALUES");
            await Assert.That(projected[0].Value.SequenceEqual(parameters)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies nested nominal values and closed/extensible enumerations map without collapsing generated types.
    /// </summary>
    [Test]
    public async Task Should_hydrate_and_project_nominal_and_enumeration_values()
    {
        var result = GeneratorHostTests.Run(("schemas/nominal.exp", NOMINAL_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.NominalMapping.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var entity = descriptor.AllocateEntity(["NOMINAL_VALUES"])
            ?? throw new InvalidOperationException("The generated descriptor did not allocate NOMINAL_VALUES.");
        var parameters = new ParameterValue[]
        {
            ParameterValue.FromString("nominal"),
            ParameterValue.FromEnumeration("ACTIVE"),
            ParameterValue.FromEnumeration("FUTURE_STATE"),
        };
        var components = new KeyValuePair<string, IReadOnlyList<ParameterValue>>[]
        {
            new("NOMINAL_VALUES", parameters),
        };

        var diagnostics = descriptor.HydrateEntity(
            new ExchangeStructure(TestHeader.Create(), [descriptor]),
            entity,
            components);
        var projected = descriptor.ProjectEntity(entity);

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty();
            await Assert.That(entity.GetType().GetProperty("IdentifierValue")!.PropertyType.Name)
                .IsEqualTo("NestedIdentifier");
            await Assert.That(entity.GetType().GetProperty("StatusValue")!.PropertyType.Name)
                .IsEqualTo("Status");
            await Assert.That(projected[0].Value.SequenceEqual(parameters)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies resolved entity parameters hydrate generated interfaces by reference identity and retain omission.
    /// </summary>
    [Test]
    public async Task Should_hydrate_and_project_resolved_entity_references()
    {
        var result = GeneratorHostTests.Run(("schemas/references.exp", REFERENCE_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.ReferenceMapping.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var target = descriptor.AllocateEntity(["TARGET"])
            ?? throw new InvalidOperationException("The generated descriptor did not allocate TARGET.");
        var holder = descriptor.AllocateEntity(["HOLDER"])
            ?? throw new InvalidOperationException("The generated descriptor did not allocate HOLDER.");
        var parameters = new ParameterValue[]
        {
            ParameterValue.FromEntity(target),
            ParameterValue.Omitted,
        };
        var components = new KeyValuePair<string, IReadOnlyList<ParameterValue>>[]
        {
            new("HOLDER", parameters),
        };

        var diagnostics = descriptor.HydrateEntity(
            new ExchangeStructure(TestHeader.Create(), [descriptor]),
            holder,
            components);
        var projected = descriptor.ProjectEntity(holder);

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty();
            await Assert.That(holder.GetType().GetProperty("TargetRef")!.GetValue(holder))
                .IsSameReferenceAs(target);
            await Assert.That(holder.GetType().GetProperty("OptionalRef")!.GetValue(holder)).IsNull();
            await Assert.That(projected[0].Value.SequenceEqual(parameters)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies SELECT defined-type alternatives stay typed while entity alternatives stay resolved references.
    /// </summary>
    [Test]
    public async Task Should_hydrate_and_project_typed_and_entity_select_alternatives()
    {
        var result = GeneratorHostTests.Run(("schemas/select.exp", SELECT_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.SelectMapping.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var target = descriptor.AllocateEntity(["TARGET"])
            ?? throw new InvalidOperationException("The generated descriptor did not allocate TARGET.");
        var holder = descriptor.AllocateEntity(["HOLDER"])
            ?? throw new InvalidOperationException("The generated descriptor did not allocate HOLDER.");
        var parameters = new ParameterValue[]
        {
            ParameterValue.FromTyped("IDENTIFIER", ParameterValue.FromString("typed")),
            ParameterValue.FromEntity(target),
        };
        var components = new KeyValuePair<string, IReadOnlyList<ParameterValue>>[]
        {
            new("HOLDER", parameters),
        };

        var diagnostics = descriptor.HydrateEntity(
            new ExchangeStructure(TestHeader.Create(), [descriptor]),
            holder,
            components);
        var projected = descriptor.ProjectEntity(holder);

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty();
            await Assert.That(projected[0].Value.SequenceEqual(parameters)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies generated mapping constructs every supported aggregate category with its declared metadata.
    /// </summary>
    [Test]
    public async Task Should_hydrate_and_project_literal_bounded_aggregate_candidates()
    {
        var result = GeneratorHostTests.Run(("schemas/aggregates.exp", AGGREGATE_SCHEMA));
        _ = result.OutputCompilation.GetTypeByMetadataName(
            "TedToolkit.Step21.Generated.AggregateMapping.SchemaDescriptor")
            ?? throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Concat(result.OutputCompilation.GetDiagnostics())));
        var assembly = Emit(result.OutputCompilation);
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.AggregateMapping.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var target = descriptor.AllocateEntity(["TARGET"])
            ?? throw new InvalidOperationException("The generated descriptor did not allocate TARGET.");
        var aggregateValues = descriptor.AllocateEntity(["AGGREGATE_VALUES"])
            ?? throw new InvalidOperationException("The generated descriptor did not allocate AGGREGATE_VALUES.");
        var parameters = new ParameterValue[]
        {
            ParameterValue.FromAggregate(
                [ParameterValue.FromString("first"), ParameterValue.Omitted, ParameterValue.FromString("third")]),
            ParameterValue.FromAggregate(
                [ParameterValue.FromInteger(10), ParameterValue.FromInteger(20)]),
            ParameterValue.FromAggregate(
                [ParameterValue.FromString("repeat"), ParameterValue.FromString("repeat")]),
            ParameterValue.FromAggregate([ParameterValue.FromEntity(target)]),
        };
        var components = new KeyValuePair<string, IReadOnlyList<ParameterValue>>[]
        {
            new("AGGREGATE_VALUES", parameters),
        };

        var diagnostics = descriptor.HydrateEntity(
            new ExchangeStructure(TestHeader.Create(), [descriptor]),
            aggregateValues,
            components);
        var projected = descriptor.ProjectEntity(aggregateValues);
        var array = aggregateValues.GetType().GetProperty("ArrayValue")!.GetValue(aggregateValues)!;
        var list = aggregateValues.GetType().GetProperty("ListValue")!.GetValue(aggregateValues)!;
        var bag = aggregateValues.GetType().GetProperty("BagValue")!.GetValue(aggregateValues)!;
        var set = aggregateValues.GetType().GetProperty("SetValue")!.GetValue(aggregateValues)!;

        using (Assert.Multiple())
        {
            await Assert.That(diagnostics).IsEmpty();
            await Assert.That((int)array.GetType().GetProperty("LowerIndex")!.GetValue(array)!).IsEqualTo(1);
            await Assert.That((int)array.GetType().GetProperty("UpperIndex")!.GetValue(array)!).IsEqualTo(3);
            await Assert.That((bool)array.GetType().GetProperty("IsOptional")!.GetValue(array)!).IsTrue();
            await Assert.That((bool)array.GetType().GetProperty("IsUnique")!.GetValue(array)!).IsTrue();
            await Assert.That((int)list.GetType().GetProperty("LowerBound")!.GetValue(list)!).IsEqualTo(1);
            await Assert.That(list.GetType().GetProperty("UpperBound")!.GetValue(list)).IsNull();
            await Assert.That((bool)list.GetType().GetProperty("IsUnique")!.GetValue(list)!).IsTrue();
            await Assert.That((int?)bag.GetType().GetProperty("UpperBound")!.GetValue(bag)).IsEqualTo(4);
            await Assert.That((int)set.GetType().GetProperty("LowerBound")!.GetValue(set)!).IsEqualTo(1);
            await Assert.That(projected[0].Value.SequenceEqual(parameters)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies a derived marker is rejected for an explicit attribute instead of being treated as absence.
    /// </summary>
    [Test]
    public async Task Should_reject_derived_marker_for_explicit_parameter()
    {
        var result = GeneratorHostTests.Run(("schemas/simple.exp", SIMPLE_SCHEMA));
        var assembly = Emit(result.OutputCompilation);
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.SimpleMapping.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var entity = descriptor.AllocateEntity(["ITEM"])
            ?? throw new InvalidOperationException("The generated descriptor did not allocate ITEM.");

        var diagnostics = descriptor.HydrateEntity(
            new ExchangeStructure(TestHeader.Create(), [descriptor]),
            entity,
            [new("ITEM", [ParameterValue.Derived, ParameterValue.Omitted])]);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Code))
            .IsEquivalentTo(["P21-BIND-PARAMETER"]);
    }

    /// <summary>
    /// Verifies large hydration dispatch is grouped without changing late-entity mapping behavior.
    /// </summary>
    [Test]
    public async Task Should_group_large_hydration_dispatch_and_preserve_mapping()
    {
        var declarations = string.Concat(Enumerable.Range(0, 33).Select(index => $"""
            ENTITY item_{index};
              name : STRING;
            END_ENTITY;

            """));
        var schema = $"""
            SCHEMA grouped_hydration;
            {declarations}END_SCHEMA;
            """;
        var result = GeneratorHostTests.Run(("schemas/grouped.exp", schema));
        var descriptorSource = result.GeneratedSources.Single(source =>
            source.HintName == "ExpressSchema_GROUPED_HYDRATION.g.cs").SourceText.ToString();
        var assembly = Emit(result.OutputCompilation);
        var descriptor = (SchemaDescriptor)assembly.GetType(
            "TedToolkit.Step21.Generated.GroupedHydration.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
        var entity = descriptor.AllocateEntity(["ITEM_32"])
            ?? throw new InvalidOperationException("The generated descriptor did not allocate ITEM_32.");
        var diagnostics = descriptor.HydrateEntity(
            new ExchangeStructure(TestHeader.Create(), [descriptor]),
            entity,
            [new("ITEM_32", [ParameterValue.FromString("late")])]);

        using (Assert.Multiple())
        {
            await Assert.That(descriptorSource).Contains("__ExpressTryHydrateGroup0");
            await Assert.That(descriptorSource).Contains("__ExpressTryHydrateGroup1");
            await Assert.That(descriptorSource).Contains("__ExpressTryValidateGroup1");
            await Assert.That(descriptorSource).Contains("__ExpressTryValidateEntityPopulationGroup1");
            await Assert.That(diagnostics).IsEmpty();
            await Assert.That((string?)entity.GetType().GetProperty("Name")!.GetValue(entity))
                .IsEqualTo("late");
        }
    }

    /// <summary>
    /// Verifies the fixed descriptor class name participates in atomic generated-name collision checks.
    /// </summary>
    [Test]
    public async Task Should_reject_schema_descriptor_type_name_collision_atomically()
    {
        var result = GeneratorHostTests.Run(
            ("schemas/descriptor-name-collision.exp", DESCRIPTOR_NAME_COLLISION_SCHEMA));

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id)).Contains("STEP21EXP004");
            await Assert.That(result.GeneratedSources).IsEmpty();
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        }
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