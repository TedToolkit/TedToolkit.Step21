using System.Reflection;

using TedToolkit.Step21.Analyzer.Express;

namespace TedToolkit.Step21.Tests.ExpressSyntaxTests;

internal sealed class ParseTests
{
    private static readonly string[] DeclarationProductions =
    [
        "referenceClause",
        "useClause",
        "constantDecl",
        "typeDecl",
        "enumerationType",
        "selectType",
        "arrayType",
        "bagType",
        "listType",
        "setType",
        "entityDecl",
        "deriveClause",
        "inverseClause",
        "uniqueClause",
        "whereClause",
        "functionDecl",
        "procedureDecl",
        "ruleDecl",
        "subtypeConstraintDecl",
        "localDecl",
    ];

    private static readonly string[] ExecutableProductions =
    [
        "aggregateInitializer",
        "namedApplication",
        "interval",
        "queryExpression",
        "unaryOp",
        "aliasStmt",
        "assignmentStmt",
        "caseStmt",
        "compoundStmt",
        "escapeStmt",
        "ifStmt",
        "nullStmt",
        "procedureCallStmt",
        "repeatStmt",
        "returnStmt",
        "skipStmt",
    ];

    private static readonly string[] TypeProductions =
    [
        "binaryType",
        "booleanType",
        "integerType",
        "logicalType",
        "numberType",
        "realType",
        "stringType",
        "aggregateType",
        "generalArrayType",
        "generalBagType",
        "generalListType",
        "generalSetType",
        "genericEntityType",
        "genericType",
        "redeclaredAttribute",
    ];

    /// <summary>
    /// Verifies that every declaration family, including unreachable executable declarations, survives in source order.
    /// </summary>
    [Test]
    public async Task Should_preserve_every_declaration_family_with_stable_spans()
    {
        var result = Parse("complete-declarations.exp");
        var root = RequireRoot(result);
        var productions = root.DescendantsAndSelf().Select(node => node.Production).ToHashSet(StringComparer.Ordinal);
        var topLevelDeclarations = root.DescendantsAndSelf()
            .Where(node => node.Production is "functionDecl" or "procedureDecl" or "ruleDecl")
            .ToArray();
        var schemaToken = root.DescendantTokens().First();

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics).IsEmpty();
            await Assert.That(root.Production).IsEqualTo("syntax");
            await Assert.That(DeclarationProductions.All(productions.Contains)).IsTrue();
            await Assert.That(topLevelDeclarations.Select(node => node.Production)
                .SequenceEqual(["functionDecl", "procedureDecl", "ruleDecl"])).IsTrue();
            await Assert.That(topLevelDeclarations.All(node => node.Span.Start.FilePath.EndsWith("complete-declarations.exp", StringComparison.Ordinal))).IsTrue();
            await Assert.That(topLevelDeclarations.All(node => IsOrdered(node.Span.Start, node.Span.End))).IsTrue();
            await Assert.That(root.DescendantTokens().Any(token => token.Text == "hidden_function")).IsTrue();
            await Assert.That(root.DescendantTokens().Any(token => token.Text == "all_roots")).IsTrue();
            await Assert.That(schemaToken.Text).IsEqualTo("SCHEMA");
            await Assert.That(schemaToken.Span.Start.Line).IsEqualTo(1);
            await Assert.That(schemaToken.Span.Start.Column).IsEqualTo(1);
            await Assert.That(schemaToken.Span.End.Line).IsEqualTo(1);
            await Assert.That(schemaToken.Span.End.Column).IsEqualTo(7);
            await Assert.That(root.DescendantsAndSelf().All(node => IsNonDecreasing(node.Span.Start, node.Span.End))).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that every statement/expression family and its physical operator tokens remain in immutable IR.
    /// </summary>
    [Test]
    public async Task Should_preserve_every_executable_syntax_family_without_executing_it()
    {
        var result = Parse("complete-expressions.exp");
        var root = RequireRoot(result);
        var productions = root.DescendantsAndSelf().Select(node => node.Production).ToHashSet(StringComparer.Ordinal);
        var tokenTexts = root.DescendantTokens().Select(token => token.Text).ToHashSet(StringComparer.Ordinal);

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics).IsEmpty();
            await Assert.That(ExecutableProductions.All(productions.Contains))
                .IsTrue()
                .Because($"missing productions: {string.Join(", ", ExecutableProductions.Where(value => !productions.Contains(value)))}");
            await Assert.That(new[] { "**", "QUERY", "<*", "ESCAPE", "SKIP", ":=", "[", "]" }
                .All(tokenTexts.Contains)).IsTrue();
            await Assert.That(root.DescendantTokens().All(token => IsOrdered(token.Span.Start, token.Span.End))).IsTrue();
        }
    }

    /// <summary>
    /// Verifies Edition 2 extensions, multiple schemas, case insensitivity, and standard lexical forms.
    /// </summary>
    [Test]
    public async Task Should_preserve_edition2_and_lexical_forms()
    {
        var result = Parse("edition2-and-lexical.exp");
        var root = RequireRoot(result);
        var productions = root.DescendantsAndSelf().Select(node => node.Production).ToArray();
        var tokens = root.DescendantTokens().ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics).IsEmpty();
            await Assert.That(productions.Count(production => production == "schemaDecl")).IsEqualTo(2);
            await Assert.That(productions).Contains("schemaVersionId");
            await Assert.That(productions).Contains("aggregateInitializer");
            await Assert.That(productions).Contains("actualParameterList");
            await Assert.That(tokens.Any(token => token.Text == "schema")).IsTrue();
            await Assert.That(tokens.Any(token => token.Text == "'version ''two'''")).IsTrue();
            await Assert.That(tokens.Any(token => token.Text == "\"0000004100000042\"")).IsTrue();
            await Assert.That(tokens.Any(token => token.TokenName == "EOF")).IsFalse();
        }
    }

    /// <summary>
    /// Verifies concrete, generalized, generic, and redeclared-attribute type forms.
    /// </summary>
    [Test]
    public async Task Should_preserve_every_type_family()
    {
        var result = Parse("complete-types.exp");
        var productions = RequireRoot(result).DescendantsAndSelf()
            .Select(node => node.Production)
            .ToHashSet(StringComparer.Ordinal);

        await Assert.That(TypeProductions.All(productions.Contains))
            .IsTrue()
            .Because($"missing productions: {string.Join(", ", TypeProductions.Where(value => !productions.Contains(value)))}");
    }

    /// <summary>
    /// Verifies that the semantic handoff is immutable, Analyzer-internal, visitor-based, and absent from runtime API.
    /// </summary>
    [Test]
    public async Task Should_keep_ir_internal_immutable_and_visitor_based()
    {
        var analyzerAssembly = typeof(ExpressSyntaxParser).Assembly;
        var runtimeAssembly = typeof(ExchangeStructure).Assembly;
        var irTypes = analyzerAssembly.GetTypes()
            .Where(type => type.Namespace == "TedToolkit.Step21.Analyzer.Express")
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(irTypes).IsNotEmpty();
            await Assert.That(irTypes.All(type => !type.IsPublic && !type.IsNestedPublic)).IsTrue();
            await Assert.That(irTypes.Where(type => type.IsClass).All(type => type.IsSealed || type.IsAbstract)).IsTrue();
            await Assert.That(irTypes.SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                .All(property => property.SetMethod is null)).IsTrue();
            await Assert.That(typeof(ExpressSyntaxVisitor).BaseType?.Name).StartsWith("ExpressBaseVisitor");
            await Assert.That(runtimeAssembly.GetExportedTypes().Any(type => type.Name.Contains("ExpressSyntax", StringComparison.Ordinal))).IsFalse();
        }
    }

    private static ExpressSyntaxParseResult Parse(string fileName)
    {
        var relativePath = Path.Combine("Express", "Valid", fileName);
        var fullPath = Path.Combine(AppContext.BaseDirectory, "TestData", relativePath);
        return ExpressSyntaxParser.Parse(relativePath, File.ReadAllText(fullPath));
    }

    private static ExpressRuleSyntax RequireRoot(ExpressSyntaxParseResult result)
    {
        return result.Root ?? throw new InvalidOperationException(
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic =>
                $"{diagnostic.SourceLocation.Line}:{diagnostic.SourceLocation.Column} {diagnostic.Message}")));
    }

    private static bool IsOrdered(ExpressSourceLocation start, ExpressSourceLocation end) =>
        end.Line > start.Line || end.Line == start.Line && end.Column > start.Column;

    private static bool IsNonDecreasing(ExpressSourceLocation start, ExpressSourceLocation end) =>
        end.Line > start.Line || end.Line == start.Line && end.Column >= start.Column;
}
