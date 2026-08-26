// -----------------------------------------------------------------------
// <copyright file="BaselineTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.Step21.Analyzer.Generation;
using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.ExpressCompilerBaselineTests;

internal sealed class BaselineTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// Verifies the approved compiler baseline preserves every generated source, diagnostic, and withholding result.
    /// </summary>
    [Test]
    public async Task Should_match_approved_generated_source_diagnostic_and_withholding_manifest()
    {
        var approvedPath = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "Express",
            "Baseline",
            "compiler-baseline.approved.json");
        var approved = File.ReadAllText(approvedPath).ReplaceLineEndings("\n");
        var actual = CreateManifest();
        var serialized = (JsonSerializer.Serialize(actual, SerializerOptions) + "\n").ReplaceLineEndings("\n");

        await Assert.That(serialized).IsEqualTo(approved);
    }

    private static BaselineManifest CreateManifest()
    {
        var cases = new[]
        {
            new CaseDefinition(
                "ap203",
                [new("schemas/ap203/ap203.exp", "Ap203/ap203.exp", "ExpressSchema_CONFIG_CONTROL_DESIGN.g.cs")]),
            new CaseDefinition(
                "syntax-withholding",
                [
                    new("schemas/syntax-invalid.exp", "Baseline/syntax-invalid.exp", "ExpressSchema_SYNTAX_INVALID.g.cs"),
                    new("schemas/syntax-valid.exp", "Baseline/valid.exp", "ExpressSchema_BASELINE_VALID.g.cs"),
                ]),
            new CaseDefinition(
                "binding-withholding",
                [
                    new("schemas/binding-invalid.exp", "Baseline/binding-invalid.exp", "ExpressSchema_BINDING_INVALID.g.cs"),
                    new("schemas/binding-dependent.exp", "Baseline/binding-dependent.exp", "ExpressSchema_BINDING_DEPENDENT.g.cs"),
                    new("schemas/binding-valid.exp", "Baseline/valid.exp", "ExpressSchema_BASELINE_VALID.g.cs"),
                ]),
            new CaseDefinition(
                "generation-withholding",
                [
                    new("schemas/generation-invalid.exp", "Baseline/generation-invalid.exp", "ExpressSchema_GENERATION_INVALID.g.cs"),
                    new("schemas/generation-dependent.exp", "Baseline/generation-dependent.exp", "ExpressSchema_GENERATION_DEPENDENT.g.cs"),
                    new("schemas/generation-valid.exp", "Baseline/valid.exp", "ExpressSchema_BASELINE_VALID.g.cs"),
                ]),
            new CaseDefinition(
                "flow-withholding",
                [
                    new("schemas/flow-invalid.exp", "Baseline/flow-invalid.exp", "ExpressSchema_FLOW_INVALID.g.cs"),
                    new("schemas/flow-valid.exp", "Baseline/valid.exp", "ExpressSchema_BASELINE_VALID.g.cs"),
                ]),
        };

        return new BaselineManifest(
            1,
            "a90ed3f13ce0e543904bbaaa9d0188747d73aef2",
            new ToolchainManifest(
                "10.0.400",
                InformationalVersion(typeof(CSharpCompilation).Assembly),
                AssemblyVersion(typeof(ExpressIncrementalGenerator).Assembly),
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Antlr4.Runtime.Standard"] = "4.13.1",
                    ["TedToolkit.RoslynHelper"] = "2026.7.15",
                    ["TUnit"] = "1.63.0",
                }),
            cases.Select(CreateCaseSnapshot).ToArray());
    }

    private static CaseSnapshot CreateCaseSnapshot(CaseDefinition definition)
    {
        var inputs = definition.Inputs
            .OrderBy(input => input.LogicalPath, StringComparer.Ordinal)
            .Select(input => new LoadedInput(input, LoadText(input.RelativeTestDataPath)))
            .ToArray();
        var result = GeneratorHostTests.Run(inputs
            .Select(input => (input.Definition.LogicalPath, input.Text))
            .ToArray());
        var generatedSources = result.GeneratedSources
            .OrderBy(source => source.HintName, StringComparer.Ordinal)
            .Select(source => new GeneratedSourceSnapshot(source.HintName, Hash(source.SourceText.ToString())))
            .ToArray();

        return new CaseSnapshot(
            definition.Name,
            inputs.Select(input => new InputSnapshot(input.Definition.LogicalPath, Hash(input.Text))).ToArray(),
            generatedSources,
            result.Diagnostics.Select(DiagnosticSnapshot.From).ToArray(),
            result.OutputCompilation.GetDiagnostics()
                .Select(DiagnosticSnapshot.From)
                .ToArray(),
            inputs.Select(input => new WithholdingSnapshot(
                    input.Definition.LogicalPath,
                    generatedSources.Any(source => string.Equals(
                        source.HintName,
                        input.Definition.DescriptorHintName,
                        StringComparison.Ordinal))))
                .ToArray());
    }

    private static string LoadText(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Express", relativePath));

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string InformationalVersion(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? assembly.GetName().Version?.ToString()
        ?? throw new InvalidOperationException($"Assembly {assembly.FullName} has no version.");

    private static string AssemblyVersion(Assembly assembly) =>
        assembly.GetName().Version?.ToString()
        ?? throw new InvalidOperationException($"Assembly {assembly.FullName} has no version.");

    private sealed record CaseDefinition(string Name, InputDefinition[] Inputs);

    private sealed record InputDefinition(string LogicalPath, string RelativeTestDataPath, string DescriptorHintName);

    private sealed record LoadedInput(InputDefinition Definition, string Text);

    private sealed record BaselineManifest(
        int FormatVersion,
        string BaselineRevision,
        ToolchainManifest Toolchain,
        CaseSnapshot[] Cases);

    private sealed record ToolchainManifest(
        string DotNetSdk,
        string RoslynAssembly,
        string GeneratorAssembly,
        SortedDictionary<string, string> Packages);

    private sealed record CaseSnapshot(
        string Name,
        InputSnapshot[] Inputs,
        GeneratedSourceSnapshot[] GeneratedSources,
        DiagnosticSnapshot[] GeneratorDiagnostics,
        DiagnosticSnapshot[] CompilerDiagnostics,
        WithholdingSnapshot[] Withholding);

    private sealed record InputSnapshot(string Path, string Sha256);

    private sealed record GeneratedSourceSnapshot(string HintName, string Sha256);

    private sealed record DiagnosticSnapshot(
        string Id,
        string Severity,
        string Message,
        string Path,
        int StartLine,
        int StartColumn,
        int EndLine,
        int EndColumn)
    {
        internal static DiagnosticSnapshot From(Diagnostic diagnostic)
        {
            var span = diagnostic.Location.GetLineSpan();
            return new(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.GetMessage(),
                span.Path,
                span.StartLinePosition.Line,
                span.StartLinePosition.Character,
                span.EndLinePosition.Line,
                span.EndLinePosition.Character);
        }
    }

    private sealed record WithholdingSnapshot(string Path, bool Emitted);
}
