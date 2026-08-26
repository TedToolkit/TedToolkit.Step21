using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Tests.ExpressBindingTests;

internal sealed class CompilationTests
{
    private const string BaseSchema = """
        SCHEMA base_schema;
        TYPE label = STRING;
        END_TYPE;
        ENTITY base_entity;
          name : label;
        END_ENTITY;
        END_SCHEMA;
        """;

    private const string ReferenceSchema = """
        SCHEMA reference_schema;
        TYPE helper_type = INTEGER;
        END_TYPE;
        END_SCHEMA;
        """;

    private const string ConsumerSchema = """
        SCHEMA consumer_schema;
        USE FROM base_schema (base_entity AS renamed_base);
        REFERENCE FROM reference_schema (helper_type AS local_helper);
        ENTITY child
          SUBTYPE OF (renamed_base);
          helper : local_helper;
        END_ENTITY;
        END_SCHEMA;
        """;

    /// <summary>
    /// Verifies closed-set imports, aliases, inheritance, and named types independent of input order.
    /// </summary>
    [Test]
    public async Task Should_bind_multifile_schema_set_independent_of_input_order()
    {
        var sources = new[]
        {
            new ExpressSchemaSource("consumer.exp", ConsumerSchema),
            new ExpressSchemaSource("base.exp", BaseSchema),
            new ExpressSchemaSource("reference.exp", ReferenceSchema),
        };

        var forward = ExpressSchemaCompiler.Compile(sources);
        var reverse = ExpressSchemaCompiler.Compile(sources.Reverse());
        var consumer = forward.Schemas.Single(schema => schema.Name == "consumer_schema");
        var child = (ExpressBoundEntity)consumer.Declarations.Single(declaration => declaration.Name == "child");
        var helper = child.Attributes.Single(attribute => attribute.Name == "helper");
        var helperType = (ExpressBoundNamedType)helper.Type;

        using (Assert.Multiple())
        {
            await Assert.That(forward.SyntaxDiagnostics).IsEmpty();
            await Assert.That(forward.BindingDiagnostics).IsEmpty();
            await Assert.That(Snapshot(forward)).IsEqualTo(Snapshot(reverse));
            await Assert.That(child.DirectSupertypes.Single().Name).IsEqualTo("base_entity");
            await Assert.That(child.DirectSupertypes.Single().DeclaringSchema.Name).IsEqualTo("base_schema");
            await Assert.That(helperType.Declaration.Name).IsEqualTo("helper_type");
            await Assert.That(helperType.Declaration.DeclaringSchema.Name).IsEqualTo("reference_schema");
            await Assert.That(consumer.Imports.Select(imported => imported.Kind))
                .IsEquivalentTo([ExpressImportKind.Use, ExpressImportKind.Reference]);
        }
    }

    /// <summary>
    /// Verifies that supplied text is the whole universe and logical paths are never opened.
    /// </summary>
    [Test]
    public async Task Should_compile_supplied_text_without_external_lookup()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource(@"Z:\definitely\absent\schema.exp", BaseSchema),
        ]);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics.Select(diagnostic => diagnostic.Code)).IsEmpty();
            await Assert.That(compilation.Schemas.Single().Name).IsEqualTo("base_schema");
        }
    }

    /// <summary>
    /// Verifies that USE interfaces chain while REFERENCE interfaces are not re-exported.
    /// </summary>
    [Test]
    public async Task Should_apply_use_and_reference_visibility_rules()
    {
        const string root = "SCHEMA root; ENTITY root_entity; END_ENTITY; END_SCHEMA;";
        const string useMiddle = "SCHEMA use_middle; USE FROM root (root_entity AS middle_entity); END_SCHEMA;";
        const string useLeaf = """
            SCHEMA use_leaf;
            USE FROM use_middle (middle_entity AS leaf_base);
            ENTITY leaf SUBTYPE OF (leaf_base); END_ENTITY;
            END_SCHEMA;
            """;
        const string referenceMiddle = "SCHEMA reference_middle; REFERENCE FROM root (root_entity); END_SCHEMA;";
        const string referenceLeaf = "SCHEMA reference_leaf; REFERENCE FROM reference_middle (root_entity); END_SCHEMA;";

        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("use-leaf.exp", useLeaf),
            new ExpressSchemaSource("reference-middle.exp", referenceMiddle),
            new ExpressSchemaSource("root.exp", root),
            new ExpressSchemaSource("reference-leaf.exp", referenceLeaf),
            new ExpressSchemaSource("use-middle.exp", useMiddle),
        ]);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.BindingDiagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(["EXPRESS-BIND-MISSING-IMPORT"]);
            await Assert.That(compilation.Schemas.Select(schema => schema.Name))
                .IsEquivalentTo(["root", "use_middle", "use_leaf", "reference_middle"]);
            var leaf = (ExpressBoundEntity)compilation.Schemas.Single(schema => schema.Name == "use_leaf")
                .Declarations.Single();
            await Assert.That(leaf.DirectSupertypes.Single().DeclaringSchema.Name).IsEqualTo("root");
        }
    }

    /// <summary>
    /// Verifies that mutually importing schemas bind through the standard's cycle-safe interface semantics.
    /// </summary>
    [Test]
    public async Task Should_bind_circular_schema_interfaces()
    {
        const string left = """
            SCHEMA left_schema;
            USE FROM right_schema (right_entity);
            ENTITY left_entity;
              peer : right_entity;
            END_ENTITY;
            END_SCHEMA;
            """;
        const string right = """
            SCHEMA right_schema;
            REFERENCE FROM left_schema (left_entity);
            ENTITY right_entity;
              peer : left_entity;
            END_ENTITY;
            END_SCHEMA;
            """;

        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("right.exp", right),
            new ExpressSchemaSource("left.exp", left),
        ]);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics).IsEmpty();
            await Assert.That(compilation.Schemas.Select(schema => schema.Name))
                .IsEquivalentTo(["left_schema", "right_schema"]);
            var entities = compilation.Schemas.SelectMany(schema => schema.Declarations)
                .OfType<ExpressBoundEntity>()
                .ToArray();
            await Assert.That(entities.Select(entity => ((ExpressBoundNamedType)entity.Attributes.Single().Type)
                .Declaration.DeclaringSchema.Name))
                .IsEquivalentTo(["right_schema", "left_schema"]);
        }
    }

    /// <summary>
    /// Verifies case-insensitive full imports and the distinct USE and REFERENCE resource sets.
    /// </summary>
    [Test]
    public async Task Should_bind_full_imports_case_insensitively()
    {
        const string foundation = """
            SCHEMA Foundation;
            CONSTANT marker : INTEGER := 1; END_CONSTANT;
            TYPE label = STRING; END_TYPE;
            ENTITY item; END_ENTITY;
            FUNCTION calculate : INTEGER; RETURN(1); END_FUNCTION;
            PROCEDURE update; END_PROCEDURE;
            END_SCHEMA;
            """;
        const string useConsumer = """
            SCHEMA use_consumer;
            USE FROM FOUNDATION;
            ENTITY derived SUBTYPE OF (ITEM); name : LABEL; END_ENTITY;
            END_SCHEMA;
            """;
        const string referenceConsumer = """
            SCHEMA reference_consumer;
            REFERENCE FROM foundation;
            FUNCTION invoke : INTEGER; RETURN(CALCULATE); END_FUNCTION;
            END_SCHEMA;
            """;

        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("reference.exp", referenceConsumer),
            new ExpressSchemaSource("foundation.exp", foundation),
            new ExpressSchemaSource("use.exp", useConsumer),
        ]);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics.Select(diagnostic => diagnostic.Message)).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics.Select(diagnostic => diagnostic.Code)).IsEmpty();
            var useImports = compilation.Schemas.Single(schema => schema.Name == "use_consumer").Imports;
            await Assert.That(useImports.Select(imported => imported.LocalName))
                .IsEquivalentTo(["label", "item"]);
            var referenceImports = compilation.Schemas.Single(schema => schema.Name == "reference_consumer").Imports;
            await Assert.That(referenceImports.Select(imported => imported.LocalName))
                .IsEquivalentTo(["marker", "label", "item", "calculate", "update"]);
        }
    }

    /// <summary>
    /// Verifies that neutral expression names bind to immutable schema and lexical targets.
    /// </summary>
    [Test]
    public async Task Should_bind_expression_names_across_lexical_scopes()
    {
        const string source = """
            SCHEMA expressions;
            TYPE base_state = EXTENSIBLE ENUMERATION OF (on, off); END_TYPE;
            TYPE state = ENUMERATION BASED_ON base_state WITH (automatic); END_TYPE;
            FUNCTION helper(input_value : INTEGER) : INTEGER;
              RETURN(input_value);
            END_FUNCTION;
            ENTITY item;
              value_code : INTEGER;
            DERIVE
              doubled : INTEGER := value_code + value_code;
            END_ENTITY;
            FUNCTION compute(input_value : INTEGER; values : LIST [0:?] OF INTEGER) : INTEGER;
              LOCAL
                result_value : INTEGER := input_value;
              END_LOCAL;
              result_value := helper(input_value);
              result_value := item(1).value_code;
              result_value := state.on;
              result_value := QUERY(element <* values | element > result_value);
              RETURN(result_value);
            END_FUNCTION;
            END_SCHEMA;
            """;

        var compilation = ExpressSchemaCompiler.Compile([new ExpressSchemaSource("expressions.exp", source)]);
        var references = compilation.Schemas.SingleOrDefault()?.NameReferences ?? [];

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics.Select(diagnostic => diagnostic.Message)).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics.Select(diagnostic => diagnostic.Code)).IsEmpty();
            await Assert.That(references.Where(reference => reference.Target.Name == "value_code").Count()).IsEqualTo(3);
            var helper = references.Single(reference => reference.Target.Name == "helper").Target;
            await Assert.That(helper.Kind).IsEqualTo(ExpressBoundNameKind.Function);
            await Assert.That(helper.Type).IsTypeOf<ExpressBoundScalarType>();
            await Assert.That(references.Any(reference => reference.Target.Kind == ExpressBoundNameKind.Parameter)).IsTrue();
            await Assert.That(references.Any(reference => reference.Target.Kind == ExpressBoundNameKind.Variable)).IsTrue();
            await Assert.That(references.Any(reference => reference.Target.Kind == ExpressBoundNameKind.QueryVariable)).IsTrue();
            await Assert.That(references.Any(reference => reference.Target.Kind == ExpressBoundNameKind.Attribute)).IsTrue();
            await Assert.That(references.Any(reference => reference.Target.Kind == ExpressBoundNameKind.Enumeration)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that declarations nested in an algorithm form a forward-reference-capable type scope.
    /// </summary>
    [Test]
    public async Task Should_bind_nested_algorithm_declarations()
    {
        const string source = """
            SCHEMA nested_declarations;
            FUNCTION outer : INTEGER;
              TYPE local_type = INTEGER; END_TYPE;
              FUNCTION nested(input_value : local_type) : local_type;
                RETURN(input_value);
              END_FUNCTION;
              LOCAL
                result_value : local_type := 0;
              END_LOCAL;
              result_value := nested(result_value);
              RETURN(result_value);
            END_FUNCTION;
            END_SCHEMA;
            """;

        var compilation = ExpressSchemaCompiler.Compile([new ExpressSchemaSource("nested.exp", source)]);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics.Select(diagnostic => diagnostic.Message)).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics.Select(diagnostic => diagnostic.Code)).IsEmpty();
            var schema = compilation.Schemas.Single();
            var references = schema.NameReferences;
            await Assert.That(schema.NestedDeclarations.Select(declaration => declaration.Name))
                .IsEquivalentTo(["local_type", "nested"]);
            var nested = references.Single(reference => reference.Target.Name == "nested").Target;
            await Assert.That(nested.Kind).IsEqualTo(ExpressBoundNameKind.Function);
            await Assert.That(nested.Type).IsTypeOf<ExpressBoundNamedType>();
            await Assert.That(((ExpressBoundNamedType)nested.Type!).Declaration)
                .IsSameReferenceAs(schema.NestedDeclarations.Single(declaration => declaration.Name == "local_type").Symbol);
            await Assert.That(references.Count(reference => reference.Target.Name == "result_value")).IsEqualTo(3);
        }
    }

    /// <summary>
    /// Verifies inherited, inverse, unique, and qualified attribute names share resolved entity identities.
    /// </summary>
    [Test]
    public async Task Should_bind_entity_attribute_relationships()
    {
        const string source = """
            SCHEMA relationships;
            ENTITY root;
              code_value : INTEGER;
            INVERSE
              children : SET [0:?] OF child FOR parent;
            UNIQUE
              UR1: code_value;
            END_ENTITY;
            ENTITY child SUBTYPE OF (root);
              parent : root;
            DERIVE
              inherited_copy : INTEGER := code_value;
              parent_code : INTEGER := parent.code_value;
            END_ENTITY;
            END_SCHEMA;
            """;

        var compilation = ExpressSchemaCompiler.Compile([new ExpressSchemaSource("relationships.exp", source)]);
        var references = compilation.Schemas.Single().NameReferences;

        using (Assert.Multiple())
        {
            await Assert.That(compilation.BindingDiagnostics.Select(diagnostic => diagnostic.Code)).IsEmpty();
            await Assert.That(references.Count(reference => reference.Target.Name == "parent")).IsEqualTo(2);
            await Assert.That(references.Count(reference => reference.Target.Name == "code_value")).IsEqualTo(3);
            await Assert.That(references.Any(reference => reference.Target.Name == "child"
                && reference.Target.Kind == ExpressBoundNameKind.Entity)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that every concrete and generalized type family survives complete binding.
    /// </summary>
    [Test]
    public async Task Should_bind_every_type_family()
    {
        var relativePath = Path.Combine("Express", "Valid", "complete-types.exp");
        var fullPath = Path.Combine(AppContext.BaseDirectory, "TestData", relativePath);
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource(relativePath, File.ReadAllText(fullPath)),
        ]);
        var schema = compilation.Schemas.Single();
        var types = schema.Declarations.OfType<ExpressBoundDefinedType>().ToArray();
        var function = schema.Declarations
            .OfType<ExpressBoundOpaqueDeclaration>()
            .Single(declaration => declaration.Kind == ExpressDeclarationKind.Function);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics).IsEmpty();
            await Assert.That(types.Select(type => type.UnderlyingType.GetType()))
                .Contains(typeof(ExpressBoundScalarType));
            await Assert.That(types.Select(type => type.UnderlyingType.GetType()))
                .Contains(typeof(ExpressBoundEnumerationType));
            await Assert.That(types.Select(type => type.UnderlyingType.GetType()))
                .Contains(typeof(ExpressBoundSelectType));
            await Assert.That(function.DeclaredType).IsTypeOf<ExpressBoundGenericType>();
            await Assert.That(types.Select(type => type.UnderlyingType)
                .OfType<ExpressBoundSelectType>()
                .Any(type => type.IsGenericEntity)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies OPTIONAL and UNIQUE modifiers bind only to the grammar level that directly contains them.
    /// </summary>
    [Test]
    public async Task Should_bind_attribute_and_nested_aggregate_modifiers_structurally()
    {
        const string source = """
            SCHEMA modifier_scope;
            ENTITY item;
              nested_optional : LIST [0:?] OF ARRAY [1:2] OF OPTIONAL STRING;
              optional_attribute : OPTIONAL ARRAY [1:2] OF STRING;
              nested_unique : ARRAY [1:2] OF LIST [0:?] OF UNIQUE STRING;
            END_ENTITY;
            END_SCHEMA;
            """;
        var compilation = ExpressSchemaCompiler.Compile([new ExpressSchemaSource("modifier-scope.exp", source)]);
        var attributes = compilation.Schemas.Single().Declarations
            .OfType<ExpressBoundEntity>()
            .Single()
            .Attributes
            .ToDictionary(attribute => attribute.Name);
        var nestedOptional = (ExpressBoundAggregateType)attributes["nested_optional"].Type;
        var nestedOptionalArray = (ExpressBoundAggregateType)nestedOptional.ElementType;
        var optionalAttribute = (ExpressBoundAggregateType)attributes["optional_attribute"].Type;
        var nestedUnique = (ExpressBoundAggregateType)attributes["nested_unique"].Type;
        var nestedUniqueList = (ExpressBoundAggregateType)nestedUnique.ElementType;

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics).IsEmpty();
            await Assert.That(attributes["nested_optional"].IsOptional).IsFalse();
            await Assert.That(nestedOptional.IsOptional).IsFalse();
            await Assert.That(nestedOptionalArray.IsOptional).IsTrue();
            await Assert.That(attributes["optional_attribute"].IsOptional).IsTrue();
            await Assert.That(optionalAttribute.IsOptional).IsFalse();
            await Assert.That(nestedUnique.IsUnique).IsFalse();
            await Assert.That(nestedUniqueList.IsUnique).IsTrue();
        }
    }

    /// <summary>
    /// Verifies an unqualified enumeration item resolves from its schema-visible declaration.
    /// </summary>
    [Test]
    public async Task Should_bind_unqualified_enumeration_items_in_expressions()
    {
        const string source = """
            SCHEMA enumeration_items;
            TYPE transition_code = ENUMERATION OF (continuous, discontinuous);
            END_TYPE;
            ENTITY segment;
              transition : transition_code;
            DERIVE
              is_closed : BOOLEAN := transition <> discontinuous;
            END_ENTITY;
            END_SCHEMA;
            """;

        var compilation = ExpressSchemaCompiler.Compile([new ExpressSchemaSource("enumeration-items.exp", source)]);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics).IsEmpty();
            await Assert.That(compilation.Schemas.Single().NameReferences.Any(reference =>
                reference.Target.Kind == ExpressBoundNameKind.Enumeration
                && reference.Target.Name == "discontinuous")).IsTrue();
        }
    }

    /// <summary>
    /// Verifies member qualifiers resolve through SELECT values and function result types.
    /// </summary>
    [Test]
    public async Task Should_bind_member_qualifiers_through_selects_and_function_results()
    {
        const string source = """
            SCHEMA qualified_results;
            ENTITY direction;
              ratios : LIST [1:3] OF REAL;
            END_ENTITY;
            ENTITY vector;
              orientation : direction;
              magnitude : REAL;
            END_ENTITY;
            ENTITY curve;
            END_ENTITY;
            ENTITY offset_curve
              SUBTYPE OF (curve);
              basis_curve : curve;
            END_ENTITY;
            ENTITY trimmed_curve
              SUBTYPE OF (curve);
              basis_curve : curve;
            END_ENTITY;
            TYPE vector_or_direction = SELECT (vector, direction);
            END_TYPE;
            FUNCTION normalise(arg : vector_or_direction) : vector_or_direction;
              RETURN(arg);
            END_FUNCTION;
            FUNCTION magnitude_of(arg : vector_or_direction) : REAL;
              RETURN(normalise(arg).magnitude + arg.orientation.ratios[1]);
            END_FUNCTION;
            FUNCTION unwrap_curve(candidate : curve) : curve;
              IF 'QUALIFIED_RESULTS.OFFSET_CURVE' IN TYPEOF(candidate) THEN
                RETURN(candidate.basis_curve);
              END_IF;
              RETURN(candidate);
            END_FUNCTION;
            FUNCTION unwrap_trimmed(candidate : curve) : curve;
              IF 'QUALIFIED_RESULTS.TRIMMED_CURVE' IN TYPEOF(candidate) THEN
                RETURN(candidate.basis_curve);
              END_IF;
              RETURN(candidate);
            END_FUNCTION;
            END_SCHEMA;
            """;

        var compilation = ExpressSchemaCompiler.Compile([new ExpressSchemaSource("qualified-results.exp", source)]);
        var references = compilation.Schemas.SingleOrDefault()?.NameReferences ?? [];

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics).IsEmpty();
            await Assert.That(references.Count(reference => reference.Target.Name == "magnitude")).IsEqualTo(1);
            await Assert.That(references.Count(reference => reference.Target.Name == "orientation")).IsEqualTo(1);
            await Assert.That(references.Count(reference => reference.Target.Name == "ratios")).IsEqualTo(1);
            await Assert.That(references.Count(reference => reference.Target.Name == "basis_curve")).IsEqualTo(2);
            await Assert.That(references.Where(reference => reference.Target.Name == "basis_curve")
                .All(reference => reference.Target.AttributeCandidates.Count == 2)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies indexed aggregate elements retain their entity type for following attribute binding.
    /// </summary>
    [Test]
    public async Task Should_bind_attributes_after_grouped_aggregate_indices()
    {
        const string source = """
            SCHEMA indexed_qualifiers;
            ENTITY vertex;
            END_ENTITY;
            ENTITY edge;
              edge_start : vertex;
              edge_end : vertex;
            END_ENTITY;
            ENTITY oriented_edge
              SUBTYPE OF (edge);
              edge_element : edge;
            DERIVE
              SELF\edge.edge_start : vertex := SELF.edge_element.edge_start;
              SELF\edge.edge_end : vertex := SELF.edge_element.edge_end;
            END_ENTITY;
            ENTITY path;
              edge_list : LIST [1:?] OF oriented_edge;
            END_ENTITY;
            ENTITY edge_loop
              SUBTYPE OF (path);
            WHERE
              connected : SELF\path.edge_list[1].edge_start :=: SELF\path.edge_list[1].edge_end;
            END_ENTITY;
            FUNCTION replace_start(candidate : edge_loop; replacement : vertex) : vertex;
              candidate\path.edge_list[1].edge_start := replacement;
              RETURN(candidate\path.edge_list[1].edge_start);
            END_FUNCTION;
            END_SCHEMA;
            """;

        var compilation = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("indexed-qualifiers.exp", source)]);
        var references = compilation.Schemas.SingleOrDefault()?.NameReferences ?? [];
        var invalid = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource(
                "missing-indexed-member.exp",
                source.Replace(
                    "edge_start := replacement",
                    "missing_slot := replacement",
                    StringComparison.Ordinal))]);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics).IsEmpty();
            await Assert.That(references.Where(reference => reference.Target.Name == "edge_start")
                .Count(reference => reference.Target.AttributeCandidates.Count == 2)).IsEqualTo(3);
            await Assert.That(references.Where(reference => reference.Target.Name == "edge_end")
                .Count(reference => reference.Target.AttributeCandidates.Count == 2)).IsEqualTo(1);
            await Assert.That(invalid.BindingDiagnostics.Select(diagnostic => diagnostic.Code))
                .Contains("EXPRESS-BIND-UNRESOLVED-MEMBER");
        }
    }

    private static string Snapshot(ExpressSchemaCompilation compilation)
    {
        return string.Join(
            "\n",
            compilation.Schemas.SelectMany(schema =>
                new[] { $"schema:{schema.Name}" }
                    .Concat(schema.Imports.Select(imported =>
                        $"import:{imported.Kind}:{imported.LocalName}->{imported.Declaration.DeclaringSchema.Name}.{imported.Declaration.Name}"))
                    .Concat(schema.Declarations.Select(declaration =>
                        $"declaration:{declaration.Kind}:{declaration.Name}"))
                    .Concat(schema.NameReferences.Select(reference =>
                        $"reference:{reference.Target.Kind}:{reference.Target.Name}:{reference.Span.Start.Line}:{reference.Span.Start.Column}"))));
    }
}