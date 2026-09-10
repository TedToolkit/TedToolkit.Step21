// -----------------------------------------------------------------------
// <copyright file="BaselineTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.Step21.Analyzer.Express.Binding;
using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.Ap214SchemaContractTests;

/// <summary>
/// Proves the pinned AP214 source, redistribution evidence, and complete generated contract.
/// </summary>
internal sealed class BaselineTests
{
    private const string UpstreamSha256 = "71AB140FE7F774321BEEE6A31E6FEE2AFC3973FD60350AE2018C74C211FB4295";

    private const string CanonicalLfSha256 = "9516315F0A8CBB9A4F6598D92FCE36BEE5189A28D1ACEA1D87E2C411266211B7";

    /// <summary>
    /// Ensures the public API oracle observes compatibility changes, not just member names.
    /// </summary>
    [Test]
    [Arguments("public string Value { get; set; }", "public int Value { get; set; }")]
    [Arguments("public string Value { get; set; }", "public string? Value { get; set; }")]
    [Arguments("public void Apply(int value) { }", "public void Apply(string value) { }")]
    [Arguments("public void Apply(int value = 1) { }", "public void Apply(int value = 2) { }")]
    [Arguments("public const int Value = 1;", "public const int Value = 2;")]
    [Arguments("public int Value { get; set; }", "public static int Value { get; set; }")]
    [Arguments("public int Value { get; set; }", "public int Value { get; init; }")]
    [Arguments("public int Value { get; set; }", "public required int Value { get; set; }")]
    [Arguments("public void Apply<T>() where T : class { }", "public void Apply<T>() where T : struct { }")]
    [Arguments("public class Nested { }", "public sealed class Nested { }")]
    [Arguments("public enum Kind { First, Second }", "public enum Kind { Second, First }")]
    public async Task Should_detect_public_contract_changes(string before, string after)
    {
        await Assert.That(RenderPublicApi(CompileApi(before))).IsNotEqualTo(RenderPublicApi(CompileApi(after)));
    }

    /// <summary>
    /// Keeps private implementation layout outside the compatibility snapshot.
    /// </summary>
    [Test]
    public async Task Should_ignore_private_implementation_changes()
    {
        const string before = "public int Value => Compute(); private static int Compute() => 1;";
        const string after = "public int Value => Shard.Compute(); private static class Shard { internal static int Compute() => 1; }";
        await Assert.That(RenderPublicApi(CompileApi(before))).IsEqualTo(RenderPublicApi(CompileApi(after)));
    }

    private static Compilation CompileApi(string members) => CSharpCompilation.Create(
        "ApiOracle",
        [CSharpSyntaxTree.ParseText(
            "#nullable enable\nnamespace TedToolkit.Step21.Schemas.AutomotiveDesign; public class Sample { "
            + members + " }")],
        [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

    /// <summary>
    /// Verifies the checked-in AP214 source and STEPcode attribution retain every approved identity and hash.
    /// </summary>
    [Test]
    public async Task Should_preserve_the_pinned_source_and_redistribution_evidence()
    {
        var directory = TestDataDirectory();
        var schemaPath = Path.Combine(directory, "AP214E3_2010.exp");
        var sourceBytes = File.ReadAllBytes(schemaPath);
        var source = Encoding.UTF8.GetString(sourceBytes);
        var canonicalSource = source.ReplaceLineEndings("\n");
        var upstreamSource = canonicalSource.ReplaceLineEndings("\r\n");
        var provenance = File.ReadAllText(Path.Combine(directory, "PROVENANCE.md"));

        using (Assert.Multiple())
        {
            await Assert.That(ComputeTextHash(upstreamSource)).IsEqualTo(UpstreamSha256);
            await Assert.That(ComputeTextHash(canonicalSource)).IsEqualTo(CanonicalLfSha256);
            await Assert.That(ComputeCanonicalTextFileHash(Path.Combine(directory, "COPYING")))
                .IsEqualTo("C787486F3E1358CF1CB4456B56E00862DE9C0433E7D49F5501D1289FF8BEF37E");
            await Assert.That(ComputeCanonicalTextFileHash(Path.Combine(directory, "AUTHORS")))
                .IsEqualTo("619EE3D3D9CE6DB690B4A20F36AB30616CB8F1FB8616FAEB85D9685AFDFD15FB");
            await Assert.That(ComputeCanonicalTextFileHash(Path.Combine(directory, "INTENT.md")))
                .IsEqualTo("B10C7DCC9C269B383C944ACC139F787CCA050D497142CA03ED90C49EF41CE23F");
            await Assert.That(source).Contains("SCHEMA AUTOMOTIVE_DESIGN;");
            await Assert.That(source).Contains("ISO/DIS 10303-214:2007");
            await Assert.That(source).Contains("2009-06-30");
            await Assert.That(source).Contains(
                "{ iso standard 10303 part(214) version(3) object(1) automotive-design-schema(1) }");
            await Assert.That(provenance).Contains("9baa5dadaa1dcfcdc623220d865d36d61ea351e9");
            await Assert.That(provenance).Contains(UpstreamSha256);
            await Assert.That(provenance).Contains(CanonicalLfSha256);
            await Assert.That(provenance).Contains("BSD-3-Clause");
        }
    }

    /// <summary>
    /// Verifies the complete AP214 source generates its unique descriptor, representative EXPRESS fidelity, and approved public API.
    /// </summary>
    [Test]
    public async Task Should_generate_the_approved_automotive_design_surface()
    {
        var directory = TestDataDirectory();
        var schemaText = File.ReadAllText(Path.Combine(directory, "AP214E3_2010.exp"));
        var result = GeneratorHostTests.Run(("schemas/ap214/AP214E3_2010.exp", schemaText));
        await Assert.That(result.Diagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var descriptor = RequiredType(result.OutputCompilation, "SchemaDescriptor");
        var advancedFace = RequiredType(result.OutputCompilation, "IAdvancedFace");
        var product = RequiredType(result.OutputCompilation, "Product");
        var productCategory = RequiredType(result.OutputCompilation, "ProductCategory");
        var reversibleList = RequiredType(result.OutputCompilation, "ListOfReversibleTopologyItem");
        var reversibleSet = RequiredType(result.OutputCompilation, "SetOfReversibleTopologyItem");
        var aheadOrBehind = RequiredType(result.OutputCompilation, "AheadOrBehind");
        var axis2PlacementKind = RequiredType(result.OutputCompilation, "Axis2PlacementKind");
        var descriptorSource = result.GeneratedSources.Single(source =>
            source.HintName == "ExpressSchema_AUTOMOTIVE_DESIGN.g.cs").SourceText.ToString();
        var publicApi = RenderPublicApi(result.OutputCompilation);
        var publicApiHash = ComputeTextHash(
            GeneratedPublicApi.NormalizeSchemaNamespaceForComparison(publicApi));
        var approvedPublicApiHash = File.ReadAllText(Path.Combine(directory, "PublicApi.approved.sha256")).Trim();
        var resultsDirectory = Path.Combine(AppContext.BaseDirectory, "TestResults");
        Directory.CreateDirectory(resultsDirectory);
        File.WriteAllText(Path.Combine(resultsDirectory, "ap214-public-api.received.txt"), publicApi);
        Console.WriteLine($"AP214_GENERATED_PUBLIC_API_SHA256={publicApiHash}");

        var compilationDiagnostics = result.OutputCompilation.GetDiagnostics();
        await Assert.That(compilationDiagnostics
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))
            .IsEmpty()
            .Because(string.Join(Environment.NewLine, compilationDiagnostics));

        using (Assert.Multiple())
        {
            await Assert.That(result.GeneratedSources.Count(source =>
                source.HintName == "ExpressSchema_AUTOMOTIVE_DESIGN.g.cs")).IsEqualTo(1);
            await Assert.That(descriptorSource).Contains("SchemaName(\"AUTOMOTIVE_DESIGN\")");
            await Assert.That(descriptor.GetMembers("Name").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("SchemaName");
            await Assert.That(advancedFace.Interfaces.Select(type => type.Name)).Contains("IFaceSurface");
            await Assert.That(product.Constructors.Single(constructor =>
                    constructor.DeclaredAccessibility == Accessibility.Public).Parameters.Select(parameter => parameter.Name)
                    .SequenceEqual(["id", "name", "frameOfReference"]))
                .IsTrue();
            await Assert.That(product.GetMembers("FrameOfReference").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("ExpressSet");
            await Assert.That(productCategory.GetMembers("Description").OfType<IPropertySymbol>().Single().Type.NullableAnnotation)
                .IsEqualTo(NullableAnnotation.Annotated);
            await Assert.That(reversibleList.GetMembers("Value").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("ExpressList");
            await Assert.That(reversibleSet.GetMembers("Value").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("ExpressSet");
            await Assert.That(aheadOrBehind.GetMembers().OfType<IPropertySymbol>()
                    .Where(property => property.IsStatic).Select(property => property.Name)
                    .SequenceEqual(["Ahead", "Exact", "Behind"]))
                .IsTrue();
            await Assert.That(axis2PlacementKind.GetMembers().OfType<IFieldSymbol>()
                    .Where(field => field.HasConstantValue).Select(field => field.Name)
                    .SequenceEqual(["Axis2Placement2d", "Axis2Placement3d"]))
                .IsTrue();
        }

        await Assert.That(publicApiHash).IsEqualTo(approvedPublicApiHash)
            .Because($"Actual AP214 generated public API SHA-256: {publicApiHash}");
    }

    private static string TestDataDirectory() => Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "Express",
        "Ap214");

    private static INamedTypeSymbol RequiredType(Compilation compilation, string name) =>
        compilation.GetTypeByMetadataName($"TedToolkit.Step21.Schemas.AutomotiveDesign.{name}")
        ?? throw new InvalidOperationException($"The generated AP214 {name} type was not found.");

    private static string RenderPublicApi(Compilation compilation) =>
        GeneratedPublicApi.Render(compilation, "AutomotiveDesign");

    private static string ComputeCanonicalTextFileHash(string path) =>
        ComputeTextHash(File.ReadAllText(path).ReplaceLineEndings("\n"));

    private static string ComputeTextHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
