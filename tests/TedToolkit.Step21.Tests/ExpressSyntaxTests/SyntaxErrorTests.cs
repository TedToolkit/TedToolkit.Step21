using TedToolkit.Step21.Analyzer.Express;

namespace TedToolkit.Step21.Tests.ExpressSyntaxTests;

internal sealed class SyntaxErrorTests
{
    /// <summary>
    /// Verifies that syntax errors produce deterministic source evidence and never publish a partial IR.
    /// </summary>
    [Test]
    public async Task Should_report_source_location_and_withhold_invalid_ir()
    {
        var relativePath = Path.Combine("Express", "Invalid", "invalid-attribute-type.exp");
        var fullPath = Path.Combine(AppContext.BaseDirectory, "TestData", relativePath);

        var result = ExpressSyntaxParser.Parse(relativePath, File.ReadAllText(fullPath));

        using (Assert.Multiple())
        {
            await Assert.That(result.Root).IsNull();
            await Assert.That(result.Diagnostics).IsNotEmpty();
            await Assert.That(result.Diagnostics.All(diagnostic => diagnostic.Code == "EXPRESS-SYNTAX")).IsTrue();
            await Assert.That(result.Diagnostics.All(diagnostic => diagnostic.SourceLocation.FilePath == relativePath)).IsTrue();
            await Assert.That(result.Diagnostics[0].SourceLocation.Line).IsEqualTo(3);
            await Assert.That(result.Diagnostics[0].SourceLocation.Column).IsGreaterThan(0);
        }
    }

    /// <summary>
    /// Verifies that valid schema text followed by unrelated input is rejected rather than partially accepted.
    /// </summary>
    [Test]
    public async Task Should_reject_trailing_input_after_schema()
    {
        const string text = "SCHEMA trailing; END_SCHEMA; unexpected";

        var result = ExpressSyntaxParser.Parse("trailing.exp", text);

        using (Assert.Multiple())
        {
            await Assert.That(result.Root).IsNull();
            await Assert.That(result.Diagnostics.Single().Code).IsEqualTo("EXPRESS-SYNTAX");
            await Assert.That(result.Diagnostics.Single().Message).Contains("unexpected");
        }
    }

    /// <summary>
    /// Verifies that a parameterless procedure call omits an actual-parameter list.
    /// </summary>
    [Test]
    public async Task Should_accept_parameterless_procedure_call_without_parentheses()
    {
        const string text = """
            SCHEMA calls;
            PROCEDURE ping; END_PROCEDURE;
            PROCEDURE invoke;
              ping;
            END_PROCEDURE;
            END_SCHEMA;
            """;

        var result = ExpressSyntaxParser.Parse("calls.exp", text);

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics).IsEmpty();
            await Assert.That(result.Root).IsNotNull();
            await Assert.That(result.Root!.DescendantsAndSelf().Count(node => node.Production == "procedureCallStmt"))
                .IsEqualTo(1);
            await Assert.That(result.Root.DescendantsAndSelf().Any(node => node.Production == "actualParameterList"))
                .IsFalse();
        }
    }

    /// <summary>
    /// Verifies that present actual-parameter lists contain at least one expression.
    /// </summary>
    [Test]
    public async Task Should_reject_empty_actual_parameter_list()
    {
        const string text = """
            SCHEMA calls;
            PROCEDURE ping; END_PROCEDURE;
            PROCEDURE invoke;
              ping();
            END_PROCEDURE;
            END_SCHEMA;
            """;

        var result = ExpressSyntaxParser.Parse("calls.exp", text);

        using (Assert.Multiple())
        {
            await Assert.That(result.Root).IsNull();
            await Assert.That(result.Diagnostics).IsNotEmpty();
        }
    }

    /// <summary>
    /// Verifies that an entity with no explicit attributes retains its required empty constructor list.
    /// </summary>
    [Test]
    public async Task Should_accept_empty_entity_constructor_list()
    {
        const string text = """
            SCHEMA constructors;
            ENTITY marker;
            END_ENTITY;
            FUNCTION create_marker : marker;
              RETURN (marker());
            END_FUNCTION;
            END_SCHEMA;
            """;

        var result = ExpressSyntaxParser.Parse("constructors.exp", text);

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics).IsEmpty();
            await Assert.That(result.Root).IsNotNull();
            await Assert.That(result.Root!.DescendantsAndSelf()
                .Count(node => node.Production == "emptyEntityConstructorList")).IsEqualTo(1);
        }
    }
}