// -----------------------------------------------------------------------
// <copyright file="StageBoundaryTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Tests.ExpressCompilerArchitectureTests;

using System.Collections;
using System.Reflection;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Binding;
using TedToolkit.Step21.Analyzer.Generation;

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

    /// <summary>
    /// Verifies that the planning handoff cannot reach parser syntax through its semantic object graph.
    /// </summary>
    [Test]
    public async Task Should_not_reach_syntax_when_planning_handoff_types_are_traversed()
    {
        var violations = ReachableAnalyzerTypes(typeof(ExpressGenerationPlan))
            .Where(path => path.Type == typeof(ExpressRuleSyntax)
                || path.Type == typeof(ExpressTokenSyntax))
            .Select(path => path.Path)
            .ToArray();

        await Assert.That(violations).IsEmpty();
    }

    /// <summary>
    /// Verifies that every entity-attribute projection is final when emission receives it.
    /// </summary>
    [Test]
    public async Task Should_expose_immutable_entity_attribute_projections()
    {
        var violations = typeof(ExpressEntityAttributeProjection)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.SetMethod is not null)
            .Select(property => property.Name)
            .ToArray();

        await Assert.That(violations).IsEmpty();
    }

    /// <summary>
    /// Verifies that no collection exposed through a real planning handoff has a writable runtime representation.
    /// </summary>
    [Test]
    public async Task Should_freeze_collections_when_generation_plan_is_created()
    {
        const string source = """
            SCHEMA architecture_boundary;
            ENTITY item;
              value : INTEGER;
            END_ENTITY;
            END_SCHEMA;
            """;
        var analyzed = ExpressSchemaCompiler.Analyze(
        [
            new ExpressSchemaSource("architecture-boundary.exp", source),
        ]);
        var plan = ExpressGenerationPlan.Create(analyzed);

        await Assert.That(WritableCollectionsReachableFrom(plan)).IsEmpty();
    }

    /// <summary>
    /// Verifies that binding implementation and pipeline orchestration have distinct type owners.
    /// </summary>
    [Test]
    public async Task Should_keep_binding_implementation_out_of_the_pipeline_facade()
    {
        var expressRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TedToolkit.Step21.Analyzer",
            "Express");
        var declarations = Directory.GetFiles(expressRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file))
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Select(declaration => (File: file, Declaration: declaration)))
            .Where(item => item.Declaration.Identifier.ValueText
                is "ExpressSchemaBinder" or "ExpressSchemaCompiler")
            .ToArray();
        var binders = declarations
            .Where(item => item.Declaration.Identifier.ValueText == "ExpressSchemaBinder")
            .ToArray();
        var facades = declarations
            .Where(item => item.Declaration.Identifier.ValueText == "ExpressSchemaCompiler")
            .ToArray();
        var partialDeclarations = declarations
            .Where(item => item.Declaration.Modifiers.Any(modifier =>
                modifier.RawKind == (int)SyntaxKind.PartialKeyword))
            .Select(item => $"{Path.GetFileName(item.File)}:{item.Declaration.Identifier.ValueText}")
            .ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(binders).HasSingleItem();
            await Assert.That(facades).HasSingleItem();
            await Assert.That(partialDeclarations).IsEmpty();
            await Assert.That(binders.Single().Declaration.Members.OfType<MethodDeclarationSyntax>()
                .Select(method => method.Identifier.ValueText)).Contains("BindClosedSet");
            await Assert.That(facades.Single().Declaration.Members.OfType<MethodDeclarationSyntax>()
                .Select(method => method.Identifier.ValueText)).DoesNotContain("BindClosedSet");
        }
    }

    private static IEnumerable<string> ForbiddenReferences(string path, params string[] forbidden)
    {
        var source = File.ReadAllText(path);
        return forbidden
            .Where(source.Contains)
            .Select(value => $"{Path.GetFileName(path)} contains forbidden stage reference '{value}'.");
    }

    private static IReadOnlyList<(Type Type, string Path)> ReachableAnalyzerTypes(Type root)
    {
        var result = new List<(Type Type, string Path)>();
        var visited = new HashSet<Type>();
        Visit(root, root.Name);
        return result;

        void Visit(Type candidate, string path)
        {
            if (candidate.IsArray)
            {
                Visit(candidate.GetElementType()!, path + "[]");
                return;
            }

            if (candidate.IsGenericType)
            {
                foreach (var argument in candidate.GetGenericArguments())
                {
                    Visit(argument, path + $"<{argument.Name}>");
                }
            }

            if (candidate.Assembly != root.Assembly || !visited.Add(candidate))
            {
                return;
            }

            result.Add((candidate, path));
            foreach (var property in candidate.GetProperties(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (property.GetMethod is not null && property.GetIndexParameters().Length == 0)
                {
                    Visit(property.PropertyType, path + "." + property.Name);
                }
            }

            foreach (var field in candidate.GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Visit(field.FieldType, path + "." + field.Name);
            }
        }
    }

    private static IReadOnlyList<string> WritableCollectionsReachableFrom(object root)
    {
        var result = new List<string>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Visit(root, root.GetType().Name);
        return result;

        void Visit(object? candidate, string path)
        {
            if (candidate is null || candidate is string || !visited.Add(candidate))
            {
                return;
            }

            var type = candidate.GetType();
            if (IsWritableCollection(type))
            {
                result.Add($"{path} exposes writable {type.FullName}.");
                return;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                Visit(type.GetProperty("Value")!.GetValue(candidate), path + ".Value");
                return;
            }

            if (type.Assembly != root.GetType().Assembly)
            {
                if (candidate is IEnumerable items)
                {
                    var index = 0;
                    foreach (var item in items)
                    {
                        Visit(item, path + $"[{index++}]");
                    }
                }

                return;
            }

            foreach (var property in type.GetProperties(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (property.GetMethod is not null && property.GetIndexParameters().Length == 0)
                {
                    Visit(property.GetValue(candidate), path + "." + property.Name);
                }
            }
        }
    }

    private static bool IsWritableCollection(Type type)
    {
        if (type.IsArray)
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(List<>)
            || definition == typeof(Dictionary<,>)
            || definition == typeof(HashSet<>);
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