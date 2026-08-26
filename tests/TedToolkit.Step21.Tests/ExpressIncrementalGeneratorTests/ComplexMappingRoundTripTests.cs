using System.Globalization;

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Proves schema-bound internal and external mapping semantic round trips.</summary>
public sealed class ComplexMappingRoundTripTests
{
    private const string COMPLEX_SCHEMA = """
        SCHEMA complex_mapping;
        ENTITY target;
          code : STRING;
        END_ENTITY;
        ENTITY other;
        END_ENTITY;
        ENTITY root SUPERTYPE OF (left ANDOR right ANDOR marker);
          label : STRING;
          peer : target;
          numbers : LIST [1:?] OF INTEGER;
          note : OPTIONAL STRING;
        END_ENTITY;
        ENTITY left SUBTYPE OF (root);
          SELF\root.label RENAMED display_label : STRING;
          enabled : BOOLEAN;
        END_ENTITY;
        ENTITY right SUBTYPE OF (root);
          rank : INTEGER;
          state : LOGICAL;
        END_ENTITY;
        ENTITY marker SUBTYPE OF (root);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string DERIVED_REDECLARATION_SCHEMA = """
        SCHEMA derived_redeclaration;
        ENTITY root;
          amount : INTEGER;
        END_ENTITY;
        ENTITY child SUBTYPE OF (root);
        DERIVE
          SELF\root.amount : INTEGER := 1;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string AGGREGATE_SELECT_SCHEMA = """
        SCHEMA aggregate_select;
        TYPE item_select = SELECT (first_item, second_item);
        END_TYPE;
        ENTITY first_item;
          name : STRING;
        END_ENTITY;
        ENTITY second_item;
          name : STRING;
        END_ENTITY;
        ENTITY container;
          items : LIST [1:?] OF item_select;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NESTED_SELECT_SCHEMA = """
        SCHEMA nested_select;
        TYPE inner_select = SELECT (first_item, second_item);
        END_TYPE;
        TYPE outer_select = SELECT (inner_select, third_item);
        END_TYPE;
        ENTITY first_item;
          name : STRING;
        END_ENTITY;
        ENTITY second_item;
          name : STRING;
        END_ENTITY;
        ENTITY third_item;
          name : STRING;
        END_ENTITY;
        ENTITY holder;
          item : outer_select;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string NUMBER_SELECT_SCHEMA = """
        SCHEMA number_select;
        TYPE real_quantity = REAL;
        END_TYPE;
        TYPE count_quantity = NUMBER;
        END_TYPE;
        TYPE quantity = SELECT (real_quantity, count_quantity);
        END_TYPE;
        ENTITY holder;
          item : quantity;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string COMPLEX_DERIVED_REDECLARATION_SCHEMA = """
        SCHEMA complex_derived_redeclaration;
        ENTITY root SUPERTYPE OF (left ANDOR right);
          amount : INTEGER;
        END_ENTITY;
        ENTITY left SUBTYPE OF (root);
        DERIVE
          SELF\root.amount : INTEGER := 1;
        WHERE
          wr1: SELF\root.amount = 1;
        END_ENTITY;
        ENTITY right SUBTYPE OF (root);
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string SAFE_GROUP_SCHEMA = """
        SCHEMA safe_group;
        ENTITY surface SUPERTYPE OF (ONEOF (plane, swept_surface));
        END_ENTITY;
        ENTITY plane SUBTYPE OF (surface);
        END_ENTITY;
        ENTITY swept_surface SUBTYPE OF (surface);
          swept_value : INTEGER;
        END_ENTITY;
        ENTITY face;
          geometry : surface;
        WHERE
          wr1: (NOT ('SAFE_GROUP.SWEPT_SURFACE' IN TYPEOF(geometry)))
            OR (geometry\swept_surface.swept_value = 1);
        END_ENTITY;
        END_SCHEMA;
        """;

    /// <summary>Maps a legal multi-leaf member selected by a nested ONEOF/ANDOR constraint.</summary>
    [Test]
    public async Task Should_read_a_nested_andor_complex_mapping()
    {
        const string schema = """
            SCHEMA nested_andor;
            ENTITY root SUPERTYPE OF (ONEOF (left, right) ANDOR marker);
            END_ENTITY;
            ENTITY left SUBTYPE OF (root);
            END_ENTITY;
            ENTITY right SUBTYPE OF (root);
            END_ENTITY;
            ENTITY marker SUBTYPE OF (root);
            END_ENTITY;
            END_SCHEMA;
            """;

        var descriptor = CreateDescriptor(schema, "NestedAndor");
        var structure = ExchangeStructure.Read(
            new StringReader(CreateExchange("#1=(LEFT()MARKER()ROOT());")
                .Replace("complex_mapping", "nested_andor", StringComparison.Ordinal)),
            [descriptor]);
        var entity = structure.Entities.Single();

        using (Assert.Multiple())
        {
            await Assert.That(entity.GetType().Name).IsEqualTo("__Complex_Left_Marker");
            await Assert.That(entity.GetType().GetInterface(
                "TedToolkit.Step21.Generated.NestedAndor.ILeft")).IsNotNull();
            await Assert.That(entity.GetType().GetInterface(
                "TedToolkit.Step21.Generated.NestedAndor.IMarker")).IsNotNull();
        }
    }

    /// <summary>Maps independent concrete siblings under an unconstrained supertype.</summary>
    [Test]
    public async Task Should_read_an_unconstrained_sibling_complex_mapping()
    {
        const string schema = """
            SCHEMA unconstrained_siblings;
            ENTITY root;
            END_ENTITY;
            ENTITY left SUBTYPE OF (root);
            END_ENTITY;
            ENTITY right SUBTYPE OF (root);
            END_ENTITY;
            END_SCHEMA;
            """;
        var descriptor = CreateDescriptor(schema, "UnconstrainedSiblings");
        var structure = ExchangeStructure.Read(
            new StringReader(CreateExchange("#1=(LEFT()RIGHT()ROOT());")
                .Replace("complex_mapping", "unconstrained_siblings", StringComparison.Ordinal)),
            [descriptor]);

        await Assert.That(structure.Entities.Single().GetType().Name)
            .IsEqualTo("__Complex_Left_Right");
    }

    /// <summary>Maps inherited, redeclared, aggregate, optional, and reference values through ordered components.</summary>
    [Test]
    public async Task Should_read_write_and_reread_the_same_complex_semantic_graph()
    {
        var descriptor = CreateDescriptor();
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('complex'),'3;1');
            FILE_NAME('complex.p21','2026-08-22T00:00:00',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('complex_mapping'));
            ENDSEC;
            DATA;
            #2=TARGET('peer');
            #1=(LEFT(.T.)RIGHT(7,.U.)ROOT('renamed',#2,(1,2),$));
            ENDSEC;
            END-ISO-10303-21;
            """;

        var first = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        var complex = first.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("1"))).Entity;
        var target = first.Registrations.Single(item => item.Name.Equals(new EntityInstanceName("2"))).Entity;
        var projection = descriptor.ProjectEntity(complex);
        var output = new StringWriter();
        var recordOutput = new StringWriter();

        first.Write(output);
        first.WriteEntity(recordOutput, complex);
        var reread = ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(complex.GetType().Name).IsEqualTo("__Complex_Left_Right");
            await Assert.That(complex.GetType().IsPublic).IsFalse();
            await Assert.That(complex.GetType().GetInterface(
                "TedToolkit.Step21.Generated.ComplexMapping.ILeft")).IsNotNull();
            await Assert.That(complex.GetType().GetInterface(
                "TedToolkit.Step21.Generated.ComplexMapping.IRight")).IsNotNull();
            await Assert.That(complex.GetType().GetProperty("DisplayLabel")!.GetValue(complex)).IsEqualTo("renamed");
            await Assert.That(complex.GetType().GetProperty("Label")!.GetValue(complex)).IsEqualTo("renamed");
            await Assert.That(complex.GetType().GetProperty("Peer")!.GetValue(complex)).IsSameReferenceAs(target);
            await Assert.That(complex.GetType().GetProperty("Note")!.GetValue(complex)).IsNull();
            await Assert.That(projection.Select(component => component.Key)
                .SequenceEqual(["LEFT", "RIGHT", "ROOT"])).IsTrue();
            await Assert.That(projection[0].Value).HasSingleItem();
            await Assert.That(projection[1].Value).Count().IsEqualTo(2);
            await Assert.That(projection[2].Value).Count().IsEqualTo(4);
            await Assert.That(output.ToString())
                .Contains("#1=(LEFT(.T.)RIGHT(7,.U.)ROOT('renamed',#2,(1,2),$));");
            await Assert.That(recordOutput.ToString())
                .IsEqualTo("#1=(LEFT(.T.)RIGHT(7,.U.)ROOT('renamed',#2,(1,2),$));");
            await Assert.That(SemanticSignature(reread, descriptor))
                .IsEqualTo(SemanticSignature(first, descriptor));
        }
    }

    /// <summary>Retains the required internal mapping for a single-leaf subtype value.</summary>
    [Test]
    public async Task Should_read_and_write_a_single_leaf_using_internal_mapping()
    {
        var descriptor = CreateDescriptor();
        var source = CreateExchange("#2=TARGET('peer');#1=LEFT('simple',#2,(3,4),'note',.F.);");
        var first = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        var output = new StringWriter();

        first.Write(output);
        var reread = ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(output.ToString())
                .Contains("#1=LEFT('simple',#2,(3,4),'note',.F.);");
            await Assert.That(SemanticSignature(reread, descriptor))
                .IsEqualTo(SemanticSignature(first, descriptor));
        }
    }

    /// <summary>Preserves an empty partial entity value in a valid multi-leaf external mapping.</summary>
    [Test]
    public async Task Should_preserve_an_empty_external_component()
    {
        var descriptor = CreateDescriptor();
        var source = CreateExchange(
            "#2=TARGET('peer');#1=(LEFT(.T.)MARKER()ROOT('value',#2,(5),$));");
        var first = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        var output = new StringWriter();

        first.Write(output);
        var reread = ExchangeStructure.Read(new StringReader(output.ToString()), [descriptor]);

        using (Assert.Multiple())
        {
            await Assert.That(output.ToString())
                .Contains("#1=(LEFT(.T.)MARKER()ROOT('value',#2,(5),$));");
            await Assert.That(SemanticSignature(reread, descriptor))
                .IsEqualTo(SemanticSignature(first, descriptor));
        }
    }

    /// <summary>Rejects incomplete, noncanonical, and incorrectly shaped external mappings atomically.</summary>
    [Test]
    [Arguments("(RIGHT(7,.U.)LEFT(.T.)ROOT('x',#2,(1),$))", "P21-BIND-ENTITY")]
    [Arguments("(LEFT(.T.)ROOT('x',#2,(1),$))", "P21-BIND-ENTITY")]
    [Arguments("(LEFT(.T.)RIGHT(7)ROOT('x',#2,(1),$))", "P21-BIND-PARAMETER-COUNT")]
    public async Task Should_reject_invalid_complex_component_mappings(string mappedValue, string expectedCode)
    {
        var descriptor = CreateDescriptor();
        var source = CreateExchange($"#2=TARGET('peer');#1={mappedValue};");

        var exception = Assert.Throws<ExchangeStructureBindingException>(() =>
            ExchangeStructure.Read(new StringReader(source), [descriptor]));

        using (Assert.Multiple())
        {
            await Assert.That(exception.Diagnostics.Select(diagnostic => diagnostic.Code)).Contains(expectedCode);
            await Assert.That(exception.Diagnostics.All(diagnostic => diagnostic.SourceLocation is
            { FilePath: "<reader>", Line: > 0, Column: > 0, })).IsTrue();
        }
    }

    /// <summary>Reports a complex-component reference mismatch against its exact physical parameter path.</summary>
    [Test]
    public async Task Should_report_the_exact_complex_reference_path()
    {
        var descriptor = CreateDescriptor();
        var source = CreateExchange(
            "#2=TARGET('peer');#3=OTHER();#1=(LEFT(.T.)RIGHT(7,.U.)ROOT('x',#3,(1),$));");

        var exception = Assert.Throws<ExchangeStructureReadValidationException>(() =>
            ExchangeStructure.Read(new StringReader(source), [descriptor]));
        var failure = exception.ValidationResult.Failures.Single(item => item.Code == "P21.READ.REFERENCE.TYPE");

        using (Assert.Multiple())
        {
            await Assert.That(failure.Path).IsEqualTo("DataSections[0].#1.Components[2].Parameters[1]");
            await Assert.That(failure.SourceLocation).IsNotNull();
            await Assert.That(failure.SourceLocation!.FilePath).IsEqualTo("<reader>");
        }
    }

    /// <summary>Preserves a derived-redeclaration marker without treating it as explicit storage.</summary>
    [Test]
    public async Task Should_read_and_write_a_derived_redeclaration_marker()
    {
        var descriptor = CreateDescriptor(DERIVED_REDECLARATION_SCHEMA, "DerivedRedeclaration");
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('derived'),'3;1');
            FILE_NAME('derived.p21','2026-08-22T00:00:00',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('derived_redeclaration'));
            ENDSEC;
            DATA;
            #1=CHILD(*);
            ENDSEC;
            END-ISO-10303-21;
            """;

        ExchangeStructure structure;
        try
        {
            structure = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        }
        catch (ExchangeStructureReadValidationException exception)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, exception.ValidationResult.Failures.Select(failure =>
                    $"{failure.Code} {failure.Path}: {failure.Message}")),
                exception);
        }
        var output = new StringWriter();

        structure.Write(output);

        await Assert.That(output.ToString()).Contains("#1=CHILD(*);");
    }

    /// <summary>Reads and writes direct entity alternatives contained in an aggregate of SELECT.</summary>
    [Test]
    public async Task Should_read_and_write_an_aggregate_of_select_values()
    {
        var descriptor = CreateDescriptor(AGGREGATE_SELECT_SCHEMA, "AggregateSelect");
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('aggregate select'),'3;1');
            FILE_NAME('aggregate-select.p21','2026-08-26T00:00:00',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('aggregate_select'));
            ENDSEC;
            DATA;
            #1=FIRST_ITEM('first');
            #2=SECOND_ITEM('second');
            #3=CONTAINER((#1,#2));
            ENDSEC;
            END-ISO-10303-21;
            """;

        var structure = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        var output = new StringWriter();

        structure.Write(output);

        await Assert.That(output.ToString()).Contains("#3=CONTAINER((#1,#2));");
    }

    /// <summary>Traverses nested SELECT declarations while preserving their generated nominal wrappers.</summary>
    [Test]
    public async Task Should_read_and_write_a_nested_select_leaf()
    {
        var descriptor = CreateDescriptor(NESTED_SELECT_SCHEMA, "NestedSelect");
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('nested select'),'3;1');
            FILE_NAME('nested-select.p21','2026-08-26T00:00:00',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('nested_select'));
            ENDSEC;
            DATA;
            #1=FIRST_ITEM('first');
            #2=HOLDER(#1);
            ENDSEC;
            END-ISO-10303-21;
            """;

        ExchangeStructure structure;
        try
        {
            structure = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        }
        catch (ExchangeStructureReadValidationException exception)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, exception.ValidationResult.Failures.Select(failure =>
                    $"{failure.Code} {failure.Path}: {failure.Message}")),
                exception);
        }
        var output = new StringWriter();

        structure.Write(output);

        await Assert.That(output.ToString()).Contains("#2=HOLDER(#1);");
    }

    /// <summary>Reads and writes a SELECT whose alternatives include NUMBER.</summary>
    [Test]
    public async Task Should_read_and_write_a_number_select_alternative()
    {
        var descriptor = CreateDescriptor(NUMBER_SELECT_SCHEMA, "NumberSelect");
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('number select'),'3;1');
            FILE_NAME('number-select.p21','2026-08-26T00:00:00',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('number_select'));
            ENDSEC;
            DATA;
            #1=HOLDER(REAL_QUANTITY(1.E-7));
            ENDSEC;
            END-ISO-10303-21;
            """;
        var structure = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        var output = new StringWriter();

        structure.Write(output);

        await Assert.That(output.ToString()).Contains("#1=HOLDER(REAL_QUANTITY(1.E-7));");
    }

    /// <summary>Uses the derived marker when one selected complex leaf redeclares a shared physical slot.</summary>
    [Test]
    public async Task Should_read_and_write_a_complex_derived_redeclaration_marker()
    {
        var descriptor = CreateDescriptor(
            COMPLEX_DERIVED_REDECLARATION_SCHEMA,
            "ComplexDerivedRedeclaration");
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('complex derived'),'3;1');
            FILE_NAME('complex-derived.p21','2026-08-26T00:00:00',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('complex_derived_redeclaration'));
            ENDSEC;
            DATA;
            #1=(LEFT()RIGHT()ROOT(*));
            ENDSEC;
            END-ISO-10303-21;
            """;
        ExchangeStructure structure;
        try
        {
            structure = ExchangeStructure.Read(new StringReader(source), [descriptor]);
        }
        catch (ExchangeStructureBindingException exception)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, exception.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}")),
                exception);
        }
        var output = new StringWriter();

        structure.Write(output);

        await Assert.That(output.ToString()).Contains("#1=(LEFT()RIGHT()ROOT(*));");
    }

    /// <summary>Treats an inapplicable group qualifier as indeterminate instead of throwing a CLR cast.</summary>
    [Test]
    public async Task Should_safely_evaluate_an_inapplicable_group_qualifier()
    {
        var descriptor = CreateDescriptor(SAFE_GROUP_SCHEMA, "SafeGroup");
        const string source = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('safe group'),'3;1');
            FILE_NAME('safe-group.p21','2026-08-26T00:00:00',('Author'),('Org'),'Pre','System','Auth');
            FILE_SCHEMA(('safe_group'));
            ENDSEC;
            DATA;
            #1=PLANE();
            #2=FACE(#1);
            ENDSEC;
            END-ISO-10303-21;
            """;

        var structure = ExchangeStructure.Read(new StringReader(source), [descriptor]);

        await Assert.That(structure.Entities).Count().IsEqualTo(2);
    }

    /// <summary>Reports one inherited structural failure for a multi-leaf value.</summary>
    [Test]
    public async Task Should_not_duplicate_inherited_validation_failures_across_complex_leaves()
    {
        var descriptor = CreateDescriptor();
        var source = CreateExchange(
            "#2=TARGET('peer');#1=(LEFT(.T.)RIGHT(7,.U.)ROOT('x',#2,(),$));");

        var exception = Assert.Throws<ExchangeStructureReadValidationException>(() =>
            ExchangeStructure.Read(new StringReader(source), [descriptor]));
        var failures = exception.ValidationResult.Failures.Where(failure =>
            failure.Code == "COMPLEX_MAPPING.ROOT.NUMBERS.AGGREGATE_0.LOWER_BOUND").ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(failures).HasSingleItem();
            await Assert.That(failures[0].Path).IsEqualTo("DataSections[0].#1.Numbers");
        }
    }

    private static string SemanticSignature(ExchangeStructure structure, SchemaDescriptor descriptor)
    {
        return string.Join("\n", structure.Registrations
            .OrderBy(item => item.Name.ToString(), StringComparer.Ordinal)
            .Select(item => $"{item.Name}:{item.Entity.GetType().FullName}:"
                + string.Join("|", descriptor.ProjectEntity(item.Entity).Select(component =>
                    $"{component.Key}({string.Join(",", component.Value.Select(value => Format(value, structure)))})"))));
    }

    private static string Format(ParameterValue value, ExchangeStructure structure)
    {
        if (value.TryGetInteger(out var integer))
            return $"I:{integer.ToString(CultureInfo.InvariantCulture)}";
        if (value.TryGetReal(out var real))
            return $"R:{real}";
        if (value.TryGetString(out var text))
            return $"S:{text}";
        if (value.TryGetBinary(out var binary))
            return $"B:{binary}";
        if (value.TryGetBoolean(out var boolean))
            return boolean ? "T" : "F";
        if (value.TryGetLogical(out var logical))
            return $"L:{logical}";
        if (value.TryGetEnumeration(out var symbol))
            return $"E:{symbol}";
        if (value.TryGetEntity(out var entity) && structure.TryGetName(entity, out var name))
            return $"#:{name}";
        if (value.TryGetAggregate(out var aggregate))
            return $"A:[{string.Join(",", aggregate.Select(item => Format(item, structure)))}]";
        if (value.TryGetTyped(out var typeName, out var inner))
            return $"Y:{typeName}({Format(inner, structure)})";
        return value.Kind == ParameterValueKind.Omitted ? "$" : "*";
    }

    private static string CreateExchange(string records) => $$"""
        ISO-10303-21;
        HEADER;
        FILE_DESCRIPTION(('complex'),'3;1');
        FILE_NAME('complex.p21','2026-08-22T00:00:00',('Author'),('Org'),'Pre','System','Auth');
        FILE_SCHEMA(('complex_mapping'));
        ENDSEC;
        DATA;
        {{records}}
        ENDSEC;
        END-ISO-10303-21;
        """;

    private static SchemaDescriptor CreateDescriptor()
        => CreateDescriptor(COMPLEX_SCHEMA, "ComplexMapping");

    private static SchemaDescriptor CreateDescriptor(string schema, string generatedSchemaName)
    {
        var result = GeneratorHostTests.Run(("schemas/complex.exp", schema));
        var diagnostics = result.Diagnostics
            .Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        if (diagnostics.Length > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.AsEnumerable()));

        using var stream = new MemoryStream();
        var emit = result.OutputCompilation.Emit(stream);
        if (!emit.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        return (SchemaDescriptor)assembly.GetType(
            $"TedToolkit.Step21.Generated.{generatedSchemaName}.SchemaDescriptor",
            throwOnError: true)!.GetProperty("Instance")!.GetValue(null)!;
    }
}