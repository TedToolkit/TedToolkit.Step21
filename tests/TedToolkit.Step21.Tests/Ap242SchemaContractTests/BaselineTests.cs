// -----------------------------------------------------------------------
// <copyright file="BaselineTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;

using TedToolkit.Step21.Analyzer.Express.Binding;
using TedToolkit.Step21.Tests.ExpressGeneratorTests;

namespace TedToolkit.Step21.Tests.Ap242SchemaContractTests;

/// <summary>Proves the pinned AP242 source and complete generated public contract.</summary>
internal sealed class BaselineTests
{
    private const string SchemaNamespace = "Ap242ManagedModelBased3dEngineeringMimLf";

    /// <summary>Keeps the first stable package inputs tied to the approved source and fixture.</summary>
    [Test]
    public async Task Should_match_the_initial_ap242_compatibility_manifest()
    {
        var repositoryRoot = FindRepositoryRoot();
        var schemaDirectory = Path.Combine(repositoryRoot, "schemas", "ap242");
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(schemaDirectory, "BASELINE.json")));
        var manifest = document.RootElement;
        var expected = new Dictionary<string, string>
        {
            ["packageId"] = "TedToolkit.Step21.Ap242",
            ["packageVersion"] = "1.0.0",
            ["sourceAuthority"] = "ISO",
            ["sourceAuthorityStatus"] = "official",
            ["sourceManifest"] = "../SOURCES.json",
            ["sourceCachePath"] = "ap242/mim_lf.exp",
            ["sourceSha256"] = "E7E93CF97880FD87D634E4B9EE58400DA0A1BE6C06A1DE76EC13807ECDC15CCB",
            ["generationInputCachePath"] = "ap242/mim_lf.compat.exp",
            ["generationInputSha256"] = "00B6027C63671AAD36C943B7C65608773094CCDF87636B197EC634F30B306360",
            ["compatibilityTransform"] = "../../build/prepare-ap242-schema.ps1",
            ["edition"] = "ISO 10303-242:2025 AP242 Edition 4; ISO/TS 10303-442 edition 7 MIM long form",
            ["descriptor"] =
                "TedToolkit.Step21.Schemas.Ap242ManagedModelBased3dEngineeringMimLf.SchemaDescriptor",
            ["runtimeRange"] = "[1.0.0,2.0.0)",
            ["publicApiSnapshot"] = "PublicApi.approved.sha256",
            ["fixtureSha256"] = "88DA6C164CC685A881A4A52AC7D0BA90E27D649EE9183810887F1A8930AEFD30",
            ["distribution"] = "local-code-generation-only; EXP and derived output publication require separate rights review",
        };
        var project = XDocument.Load(Path.Combine(
            repositoryRoot, "src", "TedToolkit.Step21.Ap242", "TedToolkit.Step21.Ap242.csproj"));
        var runtime = project.Descendants("PackageReference").Single(element =>
            (string?)element.Attribute("Include") == "TedToolkit.Step21");

        using (Assert.Multiple())
        {
            foreach (var pair in expected)
            {
                await Assert.That(manifest.GetProperty(pair.Key).GetString()).IsEqualTo(pair.Value);
            }

            await Assert.That(manifest.GetProperty("previousStableBaseline").ValueKind).IsEqualTo(JsonValueKind.Null);
            await Assert.That(manifest.GetProperty("closedSchemas").EnumerateArray()
                .Select(value => value.GetString())
                .SequenceEqual(["AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF"])).IsTrue();
            await Assert.That((string?)runtime.Attribute("VersionOverride"))
                .IsEqualTo(manifest.GetProperty("runtimeRange").GetString());
            await Assert.That(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
                    Path.Combine(TestDataDirectory(), "mim_lf.exp")))))
                .IsEqualTo(manifest.GetProperty("sourceSha256").GetString());
            await Assert.That(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(
                    repositoryRoot,
                    "tests",
                    "TedToolkit.Step21.IntegrationTests",
                    "TestData",
                    "Ap242",
                    "occt-box-10x20x30-ap242.step")))))
                .IsEqualTo(manifest.GetProperty("fixtureSha256").GetString());
            await Assert.That(File.ReadAllText(
                    Path.Combine(schemaDirectory, expected["publicApiSnapshot"])).Trim().Length)
                .IsEqualTo(64);
        }
    }

    /// <summary>Localizes full-source syntax and semantic binding before expensive source emission.</summary>
    [Test]
    public async Task Should_bind_the_complete_pinned_ap242_schema()
    {
        var source = File.ReadAllText(Path.Combine(TestDataDirectory(), "mim_lf.compat.exp"));
        var result = ExpressSchemaCompiler.Compile(
            [new ExpressSchemaSource("schemas/.cache/ap242/mim_lf.compat.exp", source)]);
        using (Assert.Multiple())
        {
            await Assert.That(result.SyntaxDiagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, result.SyntaxDiagnostics.Select(diagnostic => diagnostic.Message)));
            await Assert.That(result.BindingDiagnostics).IsEmpty()
                .Because(string.Join(Environment.NewLine, result.BindingDiagnostics.Select(diagnostic => diagnostic.Message)));
            await Assert.That(result.Schemas.Select(schema => schema.Name))
                .IsEquivalentTo(["Ap242_managed_model_based_3d_engineering_mim_lf"]);
        }

        var declarations = result.Schemas.Single().Declarations;
        Console.WriteLine(string.Join(", ", declarations.GroupBy(declaration => declaration.Kind)
            .Select(group => $"AP242_{group.Key}={group.Count()}")));
        await Assert.That(declarations.Select(declaration => declaration.Name))
            .Contains("product").And.Contains("maths_tuple").And.Contains("atom_based_value");
    }

    /// <summary>Verifies the pristine ISO source, audited generation input, and provenance boundary.</summary>
    [Test]
    public async Task Should_preserve_the_pinned_ap242_source_and_redistribution_evidence()
    {
        var directory = TestDataDirectory();
        var sourcePath = Path.Combine(directory, "mim_lf.exp");
        var generationInputPath = Path.Combine(directory, "mim_lf.compat.exp");
        var source = File.ReadAllText(sourcePath);
        var generationInput = File.ReadAllText(generationInputPath);
        var provenance = File.ReadAllText(Path.Combine(directory, "PROVENANCE.md"));
        const string sourceHash = "E7E93CF97880FD87D634E4B9EE58400DA0A1BE6C06A1DE76EC13807ECDC15CCB";

        using (Assert.Multiple())
        {
            await Assert.That(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))))
                .IsEqualTo(sourceHash);
            await Assert.That(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(generationInputPath))))
                .IsEqualTo("00B6027C63671AAD36C943B7C65608773094CCDF87636B197EC634F30B306360");

            await Assert.That(source).Contains("SCHEMA Ap242_managed_model_based_3d_engineering_mim_lf;");
            await Assert.That(source).Contains("ISO TC184/SC4/WG12 N11521");
            await Assert.That(source).Contains("ISO/TS 10303-442");
            await Assert.That(source).Contains("Supersedes ISO TC184/SC4/WG3 N11273");
            await Assert.That(provenance).Contains(sourceHash);
            await Assert.That(provenance).Contains("Authority: ISO");
            await Assert.That(provenance).Contains("unmodified ISO publication");
            await Assert.That(source).DoesNotContain("FUNCTION valid_csg_2d_operand");
            await Assert.That(generationInput).Contains("FUNCTION valid_csg_2d_operand");
            await Assert.That(generationInput).Contains(
                "the_datum                    : SET [1 : ?] OF datum := get_datums_for_datum_target(SELF);");
        }
    }

    /// <summary>Checks the full schema, representative EXPRESS fidelity and complete public API snapshot.</summary>
    [Test]
    public async Task Should_generate_the_approved_ap242_surface()
    {
        var directory = TestDataDirectory();
        var result = GeneratorHostTests.Run(("schemas/.cache/ap242/mim_lf.compat.exp",
            File.ReadAllText(Path.Combine(directory, "mim_lf.compat.exp"))));
        await Assert.That(result.Diagnostics.Where(diagnostic =>
                diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))
            .IsEmpty().Because(string.Join(Environment.NewLine, result.Diagnostics));

        var api = GeneratedPublicApi.Render(result.OutputCompilation, SchemaNamespace);
        var resultsDirectory = Path.Combine(AppContext.BaseDirectory, "TestResults");
        Directory.CreateDirectory(resultsDirectory);
        File.WriteAllText(Path.Combine(resultsDirectory, "ap242-public-api.received.txt"), api);
        Console.WriteLine($"AP242_GENERATED_PUBLIC_API_SHA256={Hash(api)}");

        var diagnostics = result.OutputCompilation.GetDiagnostics();
        await Assert.That(diagnostics.Where(diagnostic =>
                diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error))
            .IsEmpty().Because(string.Join(Environment.NewLine, diagnostics));

        var descriptorSource = result.GeneratedSources.Single(source =>
            source.HintName == "ExpressSchema_AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF.g.cs")
            .SourceText.ToString();
        var descriptor = RequiredType(result.OutputCompilation, "SchemaDescriptor");
        var product = RequiredType(result.OutputCompilation, "Product");
        var advancedFace = RequiredType(result.OutputCompilation, "IAdvancedFace");
        var productCategory = RequiredType(result.OutputCompilation, "ProductCategory");
        var point = RequiredType(result.OutputCompilation, "CartesianPoint");
        using (Assert.Multiple())
        {
            await Assert.That(descriptorSource)
                .Contains("SchemaName(\"Ap242_managed_model_based_3d_engineering_mim_lf\")");
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
            await Assert.That(point.GetMembers("Coordinates").OfType<IPropertySymbol>().Single().Type.Name)
                .IsEqualTo("ExpressList");
            await Assert.That(Hash(GeneratedPublicApi.NormalizeSchemaNamespaceForComparison(api))).IsEqualTo(
                File.ReadAllText(Path.Combine(directory, "PublicApi.approved.sha256")).Trim());
        }
    }

    private static INamedTypeSymbol RequiredType(Compilation compilation, string name) =>
        compilation.GetTypeByMetadataName($"TedToolkit.Step21.Schemas.{SchemaNamespace}.{name}")
        ?? throw new InvalidOperationException($"The generated AP242 {name} type was not found.");

    private static string TestDataDirectory() => Path.Combine(AppContext.BaseDirectory, "TestData", "Express", "Ap242");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TedToolkit.Step21.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the TedToolkit.Step21 repository root.");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}