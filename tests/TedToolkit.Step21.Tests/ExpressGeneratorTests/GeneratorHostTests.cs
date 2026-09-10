// -----------------------------------------------------------------------
// <copyright file="GeneratorHostTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using TedToolkit.Step21.Analyzer.Generation;

namespace TedToolkit.Step21.Tests.ExpressGeneratorTests;

/// <summary>
/// Proves the minimal EXPRESS incremental-generator boundary.
/// </summary>
public sealed class GeneratorHostTests
{
    private const string VALID_SCHEMA = """
        SCHEMA lunar_catalog;
        ENTITY crater;
          diameter : REAL;
        END_ENTITY;
        END_SCHEMA;
        """;

    /// <summary>
    /// Verifies that an arbitrary non-IFC schema produces a compiled descriptor.
    /// </summary>
    [Test]
    public async Task Should_generate_compiled_descriptor_for_arbitrary_schema()
    {
        var result = Run(("models/lunar.exp", VALID_SCHEMA));

        using (Assert.Multiple())
        {
            await Assert.That(result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)).IsEmpty();
            await Assert.That(result.GeneratedSources.Select(source => source.HintName))
                .IsEquivalentTo(["ExpressEntity_LUNAR_CATALOG_CRATER.g.cs", "ExpressSchema_LUNAR_CATALOG.g.cs"])
                .Because(string.Join(", ", result.GeneratedSources.Select(source => source.HintName)));
            var descriptor = result.GeneratedSources.Single(
                source => source.HintName == "ExpressSchema_LUNAR_CATALOG.g.cs");
            await Assert.That(descriptor.SourceText.ToString())
                .Contains("public sealed class SchemaDescriptor");
            await Assert.That(descriptor.SourceText.ToString())
                .Contains("SchemaName(\"lunar_catalog\")");
            await Assert.That(result.OutputCompilation.GetDiagnostics()
                .Where(item => item.Severity == DiagnosticSeverity.Error)).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies output is independent of input order and machine-specific paths.
    /// </summary>
    [Test]
    public async Task Should_generate_identical_output_for_reordered_relocated_inputs()
    {
        const string secondSchema = "SCHEMA orbit_data; END_SCHEMA;";
        var first = Run(
            ("C:/agent-a/input/lunar.exp", VALID_SCHEMA),
            ("C:/agent-a/input/orbit.exp", secondSchema));
        var second = Run(
            ("D:/agent-b/schemas/orbit.exp", secondSchema),
            ("D:/agent-b/schemas/lunar.exp", VALID_SCHEMA));

        await Assert.That(Snapshot(first)).IsEqualTo(Snapshot(second));
    }

    /// <summary>
    /// Verifies invalid EXPRESS reports a source-located diagnostic and emits no artifact.
    /// </summary>
    [Test]
    public async Task Should_report_invalid_schema_without_emitting_source()
    {
        var result = Run(("invalid/broken.exp", "SCHEMA broken;"));
        var diagnostic = result.Diagnostics.Single(item => item.Id == "STEP21EXP001");

        using (Assert.Multiple())
        {
            await Assert.That(result.GeneratedSources).IsEmpty();
            await Assert.That(diagnostic.Location.GetLineSpan().Path).IsEqualTo("invalid/broken.exp");
            await Assert.That(diagnostic.Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(0);
        }
    }

    internal static GeneratorResult Run(params (string Path, string Text)[] sources)
    {
        return Run("internal sealed class Consumer { }", sources);
    }

    internal static GeneratorResult Run(string consumerSource, params (string Path, string Text)[] sources)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(consumerSource);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(Entity).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "Consumer",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic>
                {
                    // The test host targets net10.0 while the supported runtime asset targets net8.0.
                    // Framework assembly unification is expected for this deliberate compatibility probe.
                    ["CS1701"] = ReportDiagnostic.Suppress,
                }));
        var additionalTexts = sources
            .Select(source => (AdditionalText)new InMemoryAdditionalText(source.Path, source.Text))
            .ToImmutableArray();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ExpressIncrementalGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);
        var runResult = driver.GetRunResult();
        return new GeneratorResult(
            runResult.Results.SelectMany(result => result.GeneratedSources).ToImmutableArray(),
            runResult.Diagnostics,
            outputCompilation);
    }

    private static string Snapshot(GeneratorResult result)
    {
        return string.Join(
            "\n---\n",
            result.GeneratedSources
                .OrderBy(source => source.HintName, StringComparer.Ordinal)
                .Select(source => $"{source.HintName}\n{source.SourceText}"));
    }

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return SourceText.From(text);
        }
    }

    internal sealed record GeneratorResult(
        ImmutableArray<GeneratedSourceResult> GeneratedSources,
        ImmutableArray<Diagnostic> Diagnostics,
        Compilation OutputCompilation);
}