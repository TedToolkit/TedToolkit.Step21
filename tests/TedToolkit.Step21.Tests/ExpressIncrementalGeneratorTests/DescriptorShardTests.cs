using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using TedToolkit.RoslynHelper;
using TedToolkit.RoslynHelper.Syntaxes;

using TedToolkit.Step21.Analyzer.Generation;
using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressIncrementalGeneratorTests;

/// <summary>Proves structural descriptor shards isolate caches and preserve executable calls.</summary>
public sealed class DescriptorShardTests
{
    /// <summary>Verifies both structural layouts compile, execute, and deterministically emit the same public contract.</summary>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Should_preserve_descriptor_execution(bool partition)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "DescriptorShardProof" + partition,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new PartitionGenerator(partition));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        await Assert.That(diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var sources = driver.GetRunResult().Results.Single().GeneratedSources;
        await Assert.That(sources.Length).IsEqualTo(partition ? 3 : 1);
        var core = sources.Single(source => source.HintName == "ExpressSchema_TEST.g.cs").SyntaxTree.GetRoot();
        var descriptor = core.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        await Assert.That(descriptor.Modifiers.Any(token => token.IsKind(SyntaxKind.PartialKeyword))).IsEqualTo(partition);
        if (partition)
        {
            await Assert.That(descriptor.Members.OfType<MethodDeclarationSyntax>()
                .Select(method => method.Identifier.ValueText)).IsEquivalentTo(["Calculate", "Identity"]);
            await Assert.That(core.DescendantNodes().OfType<LambdaExpressionSyntax>()).IsEmpty();
            await Assert.That(sources.Skip(1).All(source => source.SyntaxTree.GetRoot().DescendantNodes()
                .OfType<LambdaExpressionSyntax>().Count() == 1)).IsTrue();
        }

        using var bytes = new MemoryStream();
        var emitted = output.Emit(bytes);
        await Assert.That(emitted.Success).IsTrue().Because(string.Join(Environment.NewLine, emitted.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(bytes.ToArray());
        var calculate = assembly.GetType("Example.SchemaDescriptor")!.GetMethod("Calculate")!;
        await Assert.That((int)calculate.Invoke(null, [new[] { -1, 1, 2 }])!).IsEqualTo(3);

        var repeated = driver.RunGenerators(compilation).GetRunResult().Results.Single().GeneratedSources;
        await Assert.That(sources.Select(source => (source.HintName, source.SourceText.ToString())))
            .IsEquivalentTo(repeated.Select(source => (source.HintName, source.SourceText.ToString())));
    }

    /// <summary>Prevents unrelated validators from accumulating delegates in one native GC-static layout.</summary>
    [Test]
    public async Task Should_bound_entity_validation_groups()
    {
        await Assert.That(ExpressDescriptorShards.IsRequired(256)).IsFalse();
        await Assert.That(ExpressDescriptorShards.IsRequired(257)).IsTrue();
        await Assert.That(Enumerable.Range(0, 65)
            .Select(ExpressStructuralValidationEmitter.ValidationShardName)
            .Distinct(StringComparer.Ordinal).Count()).IsEqualTo(65);
    }

    /// <summary>Exercises real generation above the partition threshold, including calls from nested validation to shared rules.</summary>
    [Test]
    public async Task Should_compile_partitioned_schema_with_shared_rule_calls()
    {
        var entities = Enumerable.Range(0, 257).Select(index =>
            $"ENTITY item_{index}; values : LIST [1:?] OF REAL; WHERE positive : positive_count(values) > 0; END_ENTITY;");
        var schema = "SCHEMA partitioned; FUNCTION positive_count(values : LIST [1:?] OF REAL) : INTEGER; "
            + "RETURN(SIZEOF(QUERY(element <* values | element > 0))); END_FUNCTION; "
            + string.Join("\n", entities) + " END_SCHEMA;";
        var result = GeneratorHostTests.Run(("partitioned.exp", schema));
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Count(source =>
            source.HintName.Contains("__ExpressValidationShard", StringComparison.Ordinal))).IsEqualTo(257);
        await Assert.That(result.GeneratedSources.Count(source =>
            source.HintName.Contains("__ExpressHydrationShard", StringComparison.Ordinal))).IsEqualTo(9);
    }

    /// <summary>Keeps value-equality delegate caches outside the shared descriptor while preserving calls.</summary>
    [Test]
    public async Task Should_isolate_entity_value_equality_caches()
    {
        var entities = Enumerable.Range(0, 257).Select(index =>
            $"ENTITY item_{index}; values : LIST [1:?] OF REAL; WHERE identical : SELF = SELF; END_ENTITY;");
        var result = GeneratorHostTests.Run(("equality_caches.exp",
            "SCHEMA equality_caches; " + string.Join("\n", entities) + " END_SCHEMA;"));
        await Assert.That(result.Diagnostics).IsEmpty();
        await Assert.That(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var equalityShards = result.GeneratedSources.Where(source =>
            source.HintName.Contains("__ExpressEntityEqualityShard", StringComparison.Ordinal)).ToArray();
        await Assert.That(equalityShards.Length).IsEqualTo(9);
        foreach (var shard in equalityShards)
        {
            var methods = CSharpSyntaxTree.ParseText(shard.SourceText.ToString()).GetRoot()
                .DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
            await Assert.That(methods.Length).IsEqualTo(1);
            await Assert.That(methods[0].Identifier.ValueText.StartsWith(
                "__ExpressTryEntityValueEqualsGroup", StringComparison.Ordinal)).IsTrue();
        }
    }

    private sealed class PartitionGenerator(bool partition) : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.CompilationProvider, (production, _) =>
            {
                var descriptor = SourceComposer<ExpressIncrementalGenerator>.Class("SchemaDescriptor");
                descriptor.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
                descriptor.Polymorphism = Polymorphism.SEALED;
                var shards = new ExpressDescriptorShards(descriptor, partition);
                var first = CreateMethod("First", "global::System.Linq.Enumerable.Count(values, value => Identity(value) > 0)");
                var second = CreateMethod("Second", "global::System.Linq.Enumerable.Count(values, value => Identity(value) < 0)");
                shards.Add(first, "FirstShard");
                shards.Add(second, "SecondShard");
                var calculate = CreateMethod("Calculate",
                    shards.Qualify("First", "FirstShard") + "(values) + " + shards.Qualify("Second", "SecondShard") + "(values)");
                calculate.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
                descriptor.AddMember(calculate);
                var identity = SourceComposer<ExpressIncrementalGenerator>.Method("Identity", SourceComposer.ReturnType(DataType.Int));
                identity.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
                identity.IsStatic = true;
                identity.AddParameter(SourceComposer.Parameter(DataType.Int, "value"));
                identity.AddStatement(new CustomExpression("value").Return);
                descriptor.AddMember(identity);
                shards.Emit(production, "Example", "TEST");
            });
        }

        private static Method CreateMethod(string name, string expression)
        {
            var method = SourceComposer<ExpressIncrementalGenerator>.Method(name, SourceComposer.ReturnType(DataType.Int));
            method.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
            method.IsStatic = true;
            method.AddParameter(SourceComposer.Parameter(DataType.Int.Array, "values"));
            method.AddStatement(new CustomExpression(expression).Return);
            return method;
        }
    }
}
