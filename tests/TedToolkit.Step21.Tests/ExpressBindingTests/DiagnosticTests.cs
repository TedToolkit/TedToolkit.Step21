using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Tests.ExpressBindingTests;

internal sealed class DiagnosticTests
{
    /// <summary>
    /// Verifies aggregate deterministic binding diagnostics and dependency-closure suppression.
    /// </summary>
    [Test]
    public async Task Should_report_all_binding_failures_and_keep_independent_valid_schema()
    {
        var sources = new[]
        {
            new ExpressSchemaSource("duplicate-a.exp", "SCHEMA duplicate; END_SCHEMA;"),
            new ExpressSchemaSource("duplicate-b.exp", "SCHEMA duplicate; END_SCHEMA;"),
            new ExpressSchemaSource("broken.exp", """
                SCHEMA broken;
                USE FROM absent_schema (missing_type);
                ENTITY bad;
                  first : unknown_type;
                  second : another_unknown_type;
                END_ENTITY;
                END_SCHEMA;
                """),
            new ExpressSchemaSource("dependent.exp", """
                SCHEMA dependent;
                USE FROM broken;
                END_SCHEMA;
                """),
            new ExpressSchemaSource("valid.exp", "SCHEMA valid; END_SCHEMA;"),
        };

        var forward = ExpressSchemaCompiler.Compile(sources);
        var reverse = ExpressSchemaCompiler.Compile(sources.Reverse());
        var diagnostics = forward.BindingDiagnostics;

        using (Assert.Multiple())
        {
            await Assert.That(forward.SyntaxDiagnostics).IsEmpty();
            await Assert.That(diagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(
                [
                    "EXPRESS-BIND-DUPLICATE-SCHEMA",
                    "EXPRESS-BIND-DUPLICATE-SCHEMA",
                    "EXPRESS-BIND-MISSING-SCHEMA",
                    "EXPRESS-BIND-MISSING-IMPORT",
                    "EXPRESS-BIND-UNRESOLVED-TYPE",
                    "EXPRESS-BIND-UNRESOLVED-TYPE",
                    "EXPRESS-BIND-INVALID-DEPENDENCY",
                ]);
            await Assert.That(diagnostics.All(diagnostic => diagnostic.SourceLocation.Line > 0)).IsTrue();
            await Assert.That(diagnostics.All(diagnostic => diagnostic.SourceLocation.Column > 0)).IsTrue();
            await Assert.That(Snapshot(diagnostics)).IsEqualTo(Snapshot(reverse.BindingDiagnostics));
            await Assert.That(forward.Schemas.Select(schema => schema.Name)).IsEquivalentTo(["valid"]);
        }
    }

    /// <summary>
    /// Verifies that syntax failures remain distinct and publish no bound schema for the affected file.
    /// </summary>
    [Test]
    public async Task Should_keep_syntax_and_binding_diagnostics_distinct()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("invalid.exp", "SCHEMA invalid; ENTITY item; value : ; END_ENTITY; END_SCHEMA;"),
            new ExpressSchemaSource("valid.exp", "SCHEMA valid; END_SCHEMA;"),
        ]);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.SyntaxDiagnostics).IsNotEmpty();
            await Assert.That(compilation.BindingDiagnostics).IsEmpty();
            await Assert.That(compilation.Schemas.Select(schema => schema.Name)).IsEquivalentTo(["valid"]);
        }
    }

    /// <summary>
    /// Verifies circular inheritance/type aliases and incompatible extension bases are rejected atomically.
    /// </summary>
    [Test]
    public async Task Should_reject_semantic_cycles_and_wrong_extension_bases()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("cycles.exp", """
                SCHEMA cycles;
                TYPE first = second;
                END_TYPE;
                TYPE second = first;
                END_TYPE;
                TYPE scalar = INTEGER;
                END_TYPE;
                TYPE invalid_extension = ENUMERATION BASED_ON scalar;
                END_TYPE;
                ENTITY left SUBTYPE OF (right); END_ENTITY;
                ENTITY right SUBTYPE OF (left); END_ENTITY;
                END_SCHEMA;
                """),
        ]);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.Schemas).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(
                [
                    "EXPRESS-BIND-TYPE-CYCLE",
                    "EXPRESS-BIND-TYPE-CYCLE",
                    "EXPRESS-BIND-EXPECTED-ENUMERATION",
                    "EXPRESS-BIND-INHERITANCE-CYCLE",
                    "EXPRESS-BIND-INHERITANCE-CYCLE",
                ]);
        }
    }

    /// <summary>
    /// Verifies case-insensitive declaration conflicts, import collisions, and declaration-kind checks.
    /// </summary>
    [Test]
    public async Task Should_report_name_conflicts_and_wrong_declaration_kinds()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("first.exp", "SCHEMA first; ENTITY source; END_ENTITY; END_SCHEMA;"),
            new ExpressSchemaSource("second.exp", "SCHEMA second; ENTITY source; END_ENTITY; END_SCHEMA;"),
            new ExpressSchemaSource("conflict.exp", """
                SCHEMA conflict;
                USE FROM first (source AS collision);
                USE FROM second (source AS COLLISION);
                TYPE not_entity = INTEGER; END_TYPE;
                ENTITY child SUBTYPE OF (not_entity); END_ENTITY;
                END_SCHEMA;
                """),
            new ExpressSchemaSource("duplicate.exp", """
                SCHEMA duplicate;
                TYPE repeated = INTEGER; END_TYPE;
                ENTITY REPEATED; END_ENTITY;
                END_SCHEMA;
                """),
        ]);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.BindingDiagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(
                [
                    "EXPRESS-BIND-CONFLICTING-NAME",
                    "EXPRESS-BIND-EXPECTED-ENTITY",
                    "EXPRESS-BIND-DUPLICATE-DECLARATION",
                    "EXPRESS-BIND-DUPLICATE-DECLARATION",
                ]);
            await Assert.That(compilation.Schemas.Select(schema => schema.Name))
                .IsEquivalentTo(["first", "second"]);
        }
    }

    /// <summary>
    /// Verifies unresolved references and invalid applications aggregate without publishing the schema.
    /// </summary>
    [Test]
    public async Task Should_report_expression_name_binding_failures()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("names.exp", """
                SCHEMA names;
                TYPE scalar = INTEGER; END_TYPE;
                FUNCTION broken : INTEGER;
                  RETURN(missing_value + scalar(1) + missing_call(2));
                END_FUNCTION;
                END_SCHEMA;
                """),
        ]);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.Schemas).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(
                [
                    "EXPRESS-BIND-UNRESOLVED-NAME",
                    "EXPRESS-BIND-EXPECTED-APPLICATION",
                    "EXPRESS-BIND-UNRESOLVED-NAME",
                ]);
        }
    }

    /// <summary>
    /// Verifies that illegal cycles in nested declarations are checked with their shared bound identities.
    /// </summary>
    [Test]
    public async Task Should_reject_nested_type_and_inheritance_cycles()
    {
        var compilation = ExpressSchemaCompiler.Compile(
        [
            new ExpressSchemaSource("nested-cycles.exp", """
                SCHEMA nested_cycles;
                FUNCTION broken : INTEGER;
                  TYPE first = second; END_TYPE;
                  TYPE second = first; END_TYPE;
                  ENTITY left SUBTYPE OF (right); END_ENTITY;
                  ENTITY right SUBTYPE OF (left); END_ENTITY;
                  RETURN(0);
                END_FUNCTION;
                END_SCHEMA;
                """),
        ]);

        using (Assert.Multiple())
        {
            await Assert.That(compilation.Schemas).IsEmpty();
            await Assert.That(compilation.BindingDiagnostics.Select(diagnostic => diagnostic.Code))
                .IsEquivalentTo(
                [
                    "EXPRESS-BIND-TYPE-CYCLE",
                    "EXPRESS-BIND-TYPE-CYCLE",
                    "EXPRESS-BIND-INHERITANCE-CYCLE",
                    "EXPRESS-BIND-INHERITANCE-CYCLE",
                ]);
        }
    }

    /// <summary>
    /// Verifies bound schema values expose no mutation or public API surface.
    /// </summary>
    [Test]
    public async Task Should_keep_bound_ir_internal_and_immutable()
    {
        var assembly = typeof(ExpressSchemaCompiler).Assembly;
        var boundTypes = assembly.GetTypes()
            .Where(type => type.Namespace == "TedToolkit.Step21.Analyzer.Express.Binding"
                && !type.IsNested
                && (type.Name.StartsWith("ExpressBound", StringComparison.Ordinal)
                    || type == typeof(ExpressSchemaCompilation)))
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(boundTypes).IsNotEmpty();
            await Assert.That(boundTypes.All(type => !type.IsPublic)).IsTrue();
            await Assert.That(boundTypes.Where(type => type.IsClass).All(type => type.IsSealed || type.IsAbstract)).IsTrue();
            await Assert.That(boundTypes.SelectMany(type => type.GetProperties())
                .All(property => property.SetMethod is null)).IsTrue();
        }
    }

    private static string Snapshot(IEnumerable<ExpressBindingDiagnostic> diagnostics)
    {
        return string.Join(
            "\n",
            diagnostics.Select(diagnostic =>
                $"{diagnostic.SourceLocation.FilePath}:{diagnostic.SourceLocation.Line}:{diagnostic.SourceLocation.Column}:{diagnostic.Code}:{diagnostic.Message}"));
    }
}