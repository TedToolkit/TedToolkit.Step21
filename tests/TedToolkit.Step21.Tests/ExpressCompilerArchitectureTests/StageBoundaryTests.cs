// -----------------------------------------------------------------------
// <copyright file="StageBoundaryTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Tests.ExpressCompilerArchitectureTests;

internal sealed class StageBoundaryTests
{
    private static readonly string[] SyntaxOwners =
    [
        "ExpressSyntaxStage.cs",
        "ExpressSyntaxCompilation.cs",
        "ExpressParsedSchema.cs",
    ];

    /// <summary>
    /// Verifies that compiler source dependencies follow the approved one-way stage order.
    /// </summary>
    [Test]
    public async Task Should_follow_one_way_dependencies_when_stage_sources_are_inspected()
    {
        var analyzerRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TedToolkit.Step21.Analyzer");
        var expressRoot = Path.Combine(analyzerRoot, "Express");
        var generationRoot = Path.Combine(analyzerRoot, "Generation");

        var syntaxViolations = SyntaxOwners
            .Select(file => Path.Combine(expressRoot, file))
            .SelectMany(file => ForbiddenReferences(
                file,
                ".Express.Binding",
                ".Express.Analysis",
                ".Generation"))
            .ToArray();
        var bindingViolations = Directory.GetFiles(
                Path.Combine(expressRoot, "Binding"),
                "*.cs",
                SearchOption.TopDirectoryOnly)
            .SelectMany(file => ForbiddenReferences(file, ".Express.Analysis", ".Generation"))
            .ToArray();
        var analysisViolations = Directory.GetFiles(
                Path.Combine(expressRoot, "Analysis"),
                "*.cs",
                SearchOption.TopDirectoryOnly)
            .SelectMany(file => ForbiddenReferences(file, ".Generation"))
            .ToArray();
        var generationViolations = Directory.GetFiles(generationRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .SelectMany(file => ForbiddenReferences(
                file,
                "ExpressRuleSyntax",
                "ExpressTokenSyntax",
                ".Syntax.",
                "ExpressSyntaxNavigation"))
            .ToArray();
        var planningViolations = Directory.GetFiles(generationRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(file => file.EndsWith("Plan.cs", StringComparison.Ordinal)
                || file.EndsWith("Projection.cs", StringComparison.Ordinal))
            .SelectMany(file => ForbiddenReferences(file, "Emitter"))
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(syntaxViolations).IsEmpty();
            await Assert.That(bindingViolations).IsEmpty();
            await Assert.That(analysisViolations).IsEmpty();
            await Assert.That(generationViolations).IsEmpty();
            await Assert.That(planningViolations).IsEmpty();
        }
    }

    /// <summary>
    /// Verifies that cross-stage handoff objects remain immutable after construction.
    /// </summary>
    [Test]
    public async Task Should_expose_get_only_state_when_stage_handoffs_are_inspected()
    {
        var analyzerRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TedToolkit.Step21.Analyzer");
        var handoffs = new[]
        {
            Path.Combine(analyzerRoot, "Express", "ExpressSyntaxCompilation.cs"),
            Path.Combine(analyzerRoot, "Express", "Analysis", "ExpressAnalyzedCompilation.cs"),
            Path.Combine(analyzerRoot, "Express", "Analysis", "ExpressSchemaAnalysis.cs"),
            Path.Combine(analyzerRoot, "Express", "Analysis", "ExpressSemanticRule.cs"),
            Path.Combine(analyzerRoot, "Generation", "ExpressGenerationPlan.cs"),
        };
        var violations = handoffs
            .SelectMany(file => ForbiddenReferences(file, " set;", "private set", "internal set"))
            .ToArray();

        await Assert.That(violations).IsEmpty();
    }

    private static IEnumerable<string> ForbiddenReferences(string path, params string[] forbidden)
    {
        var source = File.ReadAllText(path);
        return forbidden
            .Where(source.Contains)
            .Select(value => $"{Path.GetFileName(path)} contains forbidden stage reference '{value}'.");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TedToolkit.Step21.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the TedToolkit.Step21 repository root.");
    }
}