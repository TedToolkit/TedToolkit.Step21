// -----------------------------------------------------------------------
// <copyright file="Ap214PackageTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using TedToolkit.Step21.IntegrationTests.ExternalCorpus;

namespace TedToolkit.Step21.IntegrationTests.PackedConsumerTests;

/// <summary>
/// Proves the precompiled AP214 package boundary from an isolated consumer.
/// </summary>
[NotInParallel("packed-consumer")]
internal sealed class Ap214PackageTests
{
    /// <summary>Protects native GC-layout symbols from oversized compiler-generated lambda caches.</summary>
    [Test]
    public async Task Should_bound_the_precompiled_ap214_native_static_caches()
    {
        var assemblyPath = Path.Combine(RepositoryPaths.FindRoot(), "src", "TedToolkit.Step21.Ap214",
            "bin", "Release", "net8.0", "TedToolkit.Step21.Ap214.dll");
        using var stream = File.OpenRead(assemblyPath);
        using var image = new PEReader(stream);
        var metadata = image.GetMetadataReader();
        var cacheCount = 0;
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            if (metadata.GetString(type.Name) != "<>c")
            {
                continue;
            }

            cacheCount++;
            var owner = metadata.GetTypeDefinition(type.GetDeclaringType());
            // Leave headroom below the native symbol record limit without stripping debug data.
            await Assert.That(type.GetFields().Count).IsLessThanOrEqualTo(32_768)
                .Because($"{metadata.GetString(owner.Name)} must have a bounded lambda cache");
        }

        await Assert.That(cacheCount).IsGreaterThan(0);
    }

    /// <summary>
    /// Keeps the first-release compatibility manifest tied to the approved source and package inputs.
    /// </summary>
    [Test]
    public async Task Should_match_the_initial_ap214_compatibility_manifest()
    {
        var repositoryRoot = RepositoryPaths.FindRoot();
        var schemaDirectory = Path.Combine(repositoryRoot, "schemas", "ap214");
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(schemaDirectory, "BASELINE.json")));
        var manifest = document.RootElement;
        var expected = new Dictionary<string, string>
        {
            ["packageId"] = "TedToolkit.Step21.Ap214",
            ["packageVersion"] = "1.0.0",
            ["sourceAuthority"] = "MBx Interoperability Forum",
            ["sourceAuthorityStatus"] = "industry-authoritative-non-ISO",
            ["sourceManifest"] = "../SOURCES.json",
            ["sourceCachePath"] = "ap214/AP214E3_2010.exp",
            ["sourceSha256"] = "71AB140FE7F774321BEEE6A31E6FEE2AFC3973FD60350AE2018C74C211FB4295",
            ["edition"] = "ISO/DIS 10303-214:2007 AP214 Edition 3",
            ["descriptor"] = "TedToolkit.Step21.Schemas.AutomotiveDesign.SchemaDescriptor",
            ["runtimeRange"] = "[1.0.0,2.0.0)",
            ["publicApiSnapshot"] = "PublicApi.approved.sha256",
            ["fixtureSha256"] = "84B04D7AEFF27157B0FBEE09D681977E516C4F16C6CD6C4EF41E34FE8C4EC722",
            ["distribution"] = "local-code-generation-only; EXP and derived output publication require separate rights review",
        };
        var project = XDocument.Load(Path.Combine(
            repositoryRoot, "src", "TedToolkit.Step21.Ap214", "TedToolkit.Step21.Ap214.csproj"));
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
                .Select(value => value.GetString()).SequenceEqual(["AUTOMOTIVE_DESIGN"])).IsTrue();
            await Assert.That((string?)runtime.Attribute("VersionOverride"))
                .IsEqualTo(manifest.GetProperty("runtimeRange").GetString());
            await Assert.That(ComputeFileHash(Path.Combine(
                    repositoryRoot, "schemas", ".cache", expected["sourceCachePath"])))
                .IsEqualTo(manifest.GetProperty("sourceSha256").GetString());
            await Assert.That(ComputeFileHash(Path.Combine(repositoryRoot, "tests",
                "TedToolkit.Step21.IntegrationTests", "TestData", "Ap214", "occt-box-10x20x30-ap214.step")))
                .IsEqualTo(manifest.GetProperty("fixtureSha256").GetString());
            await Assert.That(File.ReadAllText(Path.Combine(schemaDirectory, expected["publicApiSnapshot"])).Trim().Length)
                .IsEqualTo(64);
        }
    }

    /// <summary>
    /// Verifies package-only consumption, runtime assets, metadata, and duplicate-input guidance.
    /// </summary>
    [Test]
    public async Task Should_build_and_run_the_precompiled_ap214_package_only_consumer()
    {
        var repositoryRoot = RepositoryPaths.FindRoot();
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"TedToolkit.Step21.Ap214.Packed.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            var runtimeProject = Path.Combine(repositoryRoot, "src", "TedToolkit.Step21", "TedToolkit.Step21.csproj");
            var packageProject = Path.Combine(
                repositoryRoot,
                "src",
                "TedToolkit.Step21.Ap214",
                "TedToolkit.Step21.Ap214.csproj");
            var ap203PackageProject = Path.Combine(
                repositoryRoot,
                "src",
                "TedToolkit.Step21.Ap203",
                "TedToolkit.Step21.Ap203.csproj");
            var consumerProject = Path.Combine(
                repositoryRoot,
                "tests",
                "TedToolkit.Step21.PackedConsumer",
                "TedToolkit.Step21.PackedConsumer.csproj");
            var packageDirectory = Path.Combine(temporaryRoot, "packages");
            var repeatPackageDirectory = Path.Combine(temporaryRoot, "repeat-packages");
            var consumerPackages = Path.Combine(temporaryRoot, "consumer-packages");
            var intermediateDirectory = Path.Combine(temporaryRoot, "obj") + Path.DirectorySeparatorChar;
            var outputDirectory = Path.Combine(temporaryRoot, "bin") + Path.DirectorySeparatorChar;
            var customIntermediateDirectory = Path.Combine(temporaryRoot, "custom-obj") + Path.DirectorySeparatorChar;
            var customOutputDirectory = Path.Combine(temporaryRoot, "custom-bin") + Path.DirectorySeparatorChar;
            var coexistIntermediateDirectory = Path.Combine(temporaryRoot, "coexist-obj") + Path.DirectorySeparatorChar;
            var coexistOutputDirectory = Path.Combine(temporaryRoot, "coexist-bin") + Path.DirectorySeparatorChar;
            var nugetConfigPath = Path.Combine(temporaryRoot, "NuGet.Config");
            var fixtureDirectory = Path.Combine(
                repositoryRoot,
                "tests",
                "TedToolkit.Step21.IntegrationTests",
                "TestData",
                "Ap214");
            Directory.CreateDirectory(packageDirectory);
            Directory.CreateDirectory(repeatPackageDirectory);

            _ = await RunDotNet(
                repositoryRoot,
                "pack",
                runtimeProject,
                "--configuration",
                "Release",
                "--no-build",
                "--no-restore",
                "--output",
                packageDirectory);
            _ = await RunDotNet(
                repositoryRoot,
                "pack",
                packageProject,
                "--configuration",
                "Release",
                "--no-build",
                "--no-restore",
                "--output",
                packageDirectory);
            _ = await RunDotNet(
                repositoryRoot,
                "pack",
                packageProject,
                "--configuration",
                "Release",
                "--no-build",
                "--no-restore",
                "--output",
                repeatPackageDirectory);
            _ = await RunDotNet(
                repositoryRoot,
                "pack",
                ap203PackageProject,
                "--configuration",
                "Release",
                "--no-build",
                "--no-restore",
                "--output",
                packageDirectory);
            PackageCache.CopyProjectDependencies(
                packageDirectory,
                Path.Combine(repositoryRoot, "src", "TedToolkit.Step21", "obj", "project.assets.json"));
            File.WriteAllText(
                nugetConfigPath,
                $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="candidate" value="{packageDirectory}" />
                  </packageSources>
                </configuration>
                """);

            _ = await RunDotNet(
                repositoryRoot,
                "restore",
                consumerProject,
                "--configfile",
                nugetConfigPath,
                "--packages",
                consumerPackages,
                "--force",
                "--no-cache",
                "--property:Ap214PackageProof=true",
                "--property:Ap214FixtureProof=true",
                $"--property:BaseIntermediateOutputPath={intermediateDirectory}",
                $"--property:MSBuildProjectExtensionsPath={intermediateDirectory}");
            _ = await RunDotNet(
                repositoryRoot,
                "build",
                consumerProject,
                "--configuration",
                "Release",
                "--no-restore",
                "--disable-build-servers",
                "--property:Ap214PackageProof=true",
                "--property:Ap214FixtureProof=true",
                $"--property:BaseIntermediateOutputPath={intermediateDirectory}",
                $"--property:MSBuildProjectExtensionsPath={intermediateDirectory}",
                $"--property:OutputPath={outputDirectory}");
            var runOutput = await RunDotNet(
                repositoryRoot,
                Path.Combine(outputDirectory, "TedToolkit.Step21.PackedConsumer.dll"));
            _ = await RunDotNet(
                repositoryRoot,
                "restore",
                consumerProject,
                "--configfile",
                nugetConfigPath,
                "--packages",
                consumerPackages,
                "--force",
                "--no-cache",
                $"--property:BaseIntermediateOutputPath={customIntermediateDirectory}",
                $"--property:MSBuildProjectExtensionsPath={customIntermediateDirectory}");
            _ = await RunDotNet(
                repositoryRoot,
                "build",
                consumerProject,
                "--configuration",
                "Release",
                "--no-restore",
                "--disable-build-servers",
                $"--property:BaseIntermediateOutputPath={customIntermediateDirectory}",
                $"--property:MSBuildProjectExtensionsPath={customIntermediateDirectory}",
                $"--property:OutputPath={customOutputDirectory}");
            var customRunOutput = await RunDotNet(
                repositoryRoot,
                Path.Combine(customOutputDirectory, "TedToolkit.Step21.PackedConsumer.dll"));
            _ = await RunDotNet(
                repositoryRoot,
                "restore",
                consumerProject,
                "--configfile",
                nugetConfigPath,
                "--packages",
                consumerPackages,
                "--force",
                "--no-cache",
                "--property:Ap203Ap214PackageProof=true",
                $"--property:BaseIntermediateOutputPath={coexistIntermediateDirectory}",
                $"--property:MSBuildProjectExtensionsPath={coexistIntermediateDirectory}");
            _ = await RunDotNet(
                repositoryRoot,
                "build",
                consumerProject,
                "--configuration",
                "Release",
                "--no-restore",
                "--disable-build-servers",
                "--property:Ap203Ap214PackageProof=true",
                $"--property:BaseIntermediateOutputPath={coexistIntermediateDirectory}",
                $"--property:MSBuildProjectExtensionsPath={coexistIntermediateDirectory}",
                $"--property:OutputPath={coexistOutputDirectory}");
            var coexistRunOutput = await RunDotNet(
                repositoryRoot,
                Path.Combine(coexistOutputDirectory, "TedToolkit.Step21.PackedConsumer.dll"),
                Path.Combine(
                    repositoryRoot,
                    "tests",
                    "TedToolkit.Step21.IntegrationTests",
                    "TestData",
                    "Ap203",
                    "occt-box-10x20x30-ap203.step"),
                Path.Combine(fixtureDirectory, "occt-box-10x20x30-ap214.step"));
            var fixtureOutput = await RunDotNet(
                repositoryRoot,
                Path.Combine(outputDirectory, "TedToolkit.Step21.PackedConsumer.dll"),
                Path.Combine(fixtureDirectory, "occt-box-10x20x30-ap214.step"),
                Path.Combine(fixtureDirectory, "occt-unsupported-extension-ap214.step"),
                Path.Combine(
                    repositoryRoot,
                    "tests",
                    "TedToolkit.Step21.IntegrationTests",
                    "TestData",
                    "Ap203",
                    "occt-box-10x20x30-ap203.step"));

            var packagePath = Directory.GetFiles(
                packageDirectory,
                "TedToolkit.Step21.Ap214.1.0.0.nupkg").Single();
            var repeatPackagePath = Directory.GetFiles(
                repeatPackageDirectory,
                "TedToolkit.Step21.Ap214.1.0.0.nupkg").Single();
            var ap203PackagePath = Directory.GetFiles(
                packageDirectory,
                "TedToolkit.Step21.Ap203.1.0.0.nupkg").Single();
            var packageEntries = ReadPackageEntries(packagePath);
            var packageSpecification = ReadPackageText(packagePath, "TedToolkit.Step21.Ap214.nuspec");
            var ap203PackageSpecification = ReadPackageText(
                ap203PackagePath,
                "TedToolkit.Step21.Ap203.nuspec");
            var packageReadme = ReadPackageText(packagePath, "README.md");
            var runtimeLibraries = ReadRuntimeLibraries(Path.Combine(intermediateDirectory, "project.assets.json"));
            var coexistLibraries = ReadRuntimeLibraries(
                Path.Combine(coexistIntermediateDirectory, "project.assets.json"));
            var generatedRoot = Path.Combine(intermediateDirectory, "Generated");
            var provenance = File.ReadAllText(Path.Combine(fixtureDirectory, "PROVENANCE.md"));

            using (Assert.Multiple())
            {
                await Assert.That(runOutput).Contains("PACKED_AP214_OK");
                await Assert.That(customRunOutput).Contains("PACKED_AOT_OK");
                await Assert.That(coexistRunOutput).Contains(
                    "PACKED_AP203_AP214_OK ap203-entities=200 ap214-entities=173 "
                    + "runtime-assemblies=1 schema-assemblies=2");
                await Assert.That(fixtureOutput).Contains(
                    "AP214_DUPLICATE_DESCRIPTOR_REJECTED input-chars=0");
                await Assert.That(fixtureOutput).Contains(
                    "AP214_RAW_EDITION_REJECTED rules=application-protocol-definition-required,product-requires-id-owner "
                    + "partial-model=false");
                await Assert.That(fixtureOutput).Contains(
                    "AP214_FIXTURE_OK entities=173 products=1 faces=6 edges=12 vertices=8 points=27 units=3 "
                    + "extents=10x20x30");
                await Assert.That(fixtureOutput).Contains(
                    "AP214_REFERENCE_GRAPH_OK entities=173 values-and-named-references=preserved");
                await Assert.That(fixtureOutput).Contains(
                    "AP214_ROUND_TRIP_OK edit=product.name entities=173 faces=6 edges=12 vertices=8 points=27 "
                    + "units=millimetre,radian,steradian extents=10x20x30 "
                    + "shared-vertex-degrees=3,3,3,3,3,3,3,3");
                await Assert.That(fixtureOutput).Contains(
                    "AP214_INVALID_EDIT_REJECTED failures=");
                await Assert.That(fixtureOutput).Contains(
                    "codes=product.frame-of-reference,face.bounds output-bytes=0");
                await Assert.That(fixtureOutput).Contains(
                    "AP214_EXTENSION_REJECTED code=P21-BIND-ENTITY line=8 column=6 partial-model=false");
                await Assert.That(fixtureOutput).Contains(
                    "AP214_SCHEMA_REJECTED code=P21-BIND-SCHEMA partial-model=false");
                await Assert.That(Directory.Exists(generatedRoot)
                    && Directory.EnumerateFiles(generatedRoot, "*.g.cs", SearchOption.AllDirectories).Any()).IsFalse();
                await Assert.That(packageEntries).Contains("lib/netstandard2.0/TedToolkit.Step21.Ap214.dll");
                await Assert.That(packageEntries).Contains("lib/net8.0/TedToolkit.Step21.Ap214.dll");
                await Assert.That(packageEntries).DoesNotContain("lib/net10.0/TedToolkit.Step21.Ap214.dll");
                await Assert.That(ReadNormalizedPackageManifest(packagePath)).IsEqualTo(
                    ReadNormalizedPackageManifest(repeatPackagePath));
                await Assert.That(packageEntries).Contains("README.md");
                foreach (var evidence in new[]
                {
                    (Entry: "third-party/schema-source/PROVENANCE.md",
                        Source: Path.Combine(repositoryRoot, "schemas", "ap214", "PROVENANCE.md")),
                    (Entry: "third-party/schema-source/SOURCES.md",
                        Source: Path.Combine(repositoryRoot, "schemas", "SOURCES.md")),
                })
                {
                    var evidenceEntry = evidence.Entry;
                    await Assert.That(packageEntries).Contains(evidenceEntry);
                    await Assert.That(ReadPackageText(packagePath, evidenceEntry).ReplaceLineEndings("\n"))
                        .IsEqualTo(File.ReadAllText(evidence.Source).ReplaceLineEndings("\n"));
                }

                await Assert.That(packageEntries.Any(path => path.EndsWith(".exp", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries.Any(path => path.StartsWith("analyzers/", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries.Any(path => path.Contains("RoslynHelper", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageSpecification).Contains(
                    "<version>1.0.0</version>");
                await Assert.That(packageSpecification).Contains(
                    "<dependency id=\"TedToolkit.Step21\" version=\"[1.0.0, 2.0.0)\" exclude=\"Build,Analyzers\" />");
                await Assert.That(packageSpecification).DoesNotContain("TedToolkit.Step21.Ap203");
                await Assert.That(ap203PackageSpecification).DoesNotContain("TedToolkit.Step21.Ap214");
                await Assert.That(packageSpecification).DoesNotContain("TedToolkit.Step21.Analyzer");
                await Assert.That(packageReadme).Contains("Do not add `AP214E3_2010.exp`");
                await Assert.That(packageReadme).Contains("Distinct custom EXPRESS schemas remain supported");
                await Assert.That(packageReadme).Contains(
                    "A different edition or vendor variant");
                await Assert.That(packageReadme).Contains("requires a Major version");
                await Assert.That(ComputeCanonicalTextHash(Path.Combine(
                    repositoryRoot,
                    "schemas",
                    "ap214",
                    "AP214E3_2010.exp"))).IsEqualTo(
                    "9516315F0A8CBB9A4F6598D92FCE36BEE5189A28D1ACEA1D87E2C411266211B7");
                await Assert.That(File.ReadAllText(Path.Combine(
                    repositoryRoot,
                    "schemas",
                    "ap214",
                    "PublicApi.approved.sha256")).Trim()).IsEqualTo(
                    "B1ADD873603E9B5D3DB9349FE3F0F20464EEE644C8B3A5E487F5575BA6591D2C");
                await Assert.That(runtimeLibraries.Count(name => name.Equals(
                    "TedToolkit.Step21.Ap214/1.0.0",
                    StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
                await Assert.That(runtimeLibraries.Count(name => name.Equals(
                    "TedToolkit.Step21/1.0.0",
                    StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
                await Assert.That(coexistLibraries.Count(name => name.Equals(
                    "TedToolkit.Step21/1.0.0",
                    StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
                await Assert.That(coexistLibraries.Count(name => name.Equals(
                    "TedToolkit.Step21.Ap203/1.0.0",
                    StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
                await Assert.That(coexistLibraries.Count(name => name.Equals(
                    "TedToolkit.Step21.Ap214/1.0.0",
                    StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
                await Assert.That(runtimeLibraries.Any(name => name.StartsWith(
                    "TedToolkit.Step21.Analyzer/",
                    StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(runtimeLibraries.Any(name => name.StartsWith(
                    "TedToolkit.RoslynHelper/",
                    StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(ComputeFileHash(Path.Combine(
                    fixtureDirectory,
                    "occt-box-10x20x30-ap214.step"))).IsEqualTo(
                    "84B04D7AEFF27157B0FBEE09D681977E516C4F16C6CD6C4EF41E34FE8C4EC722");
                await Assert.That(ComputeFileHash(Path.Combine(
                    fixtureDirectory,
                    "occt-unsupported-extension-ap214.step"))).IsEqualTo(
                    "ABE4ECFA37BBCC7A79FB641FD8E39C93D38111FAF752AED1571174A428E0D17C");
                await Assert.That(ComputeFileHash(Path.Combine(
                    fixtureDirectory,
                    "occt-box-10x20x30-generator.cxx"))).IsEqualTo(
                    "C3C6F7460FAD35AE964E41CCBA412655F5DC6DB3769C776C2E79ECDFD0E3F505");
                await Assert.That(ComputeFileHash(Path.Combine(
                    fixtureDirectory,
                    "LICENSE_LGPL_21.txt"))).IsEqualTo(
                    "E237FA56668030E928551DDD60F05DF5FE957F75EAB874BBD017E085ED722E7C");
                await Assert.That(ComputeFileHash(Path.Combine(
                    fixtureDirectory,
                    "OCCT_LGPL_EXCEPTION.txt"))).IsEqualTo(
                    "04580A884EA6CEA294402649FF7B5CBB167D47462D1340A4ED33E550DB10A81B");
                await Assert.That(provenance).Contains(
                    "7d2efad9c8a9a57ea96c4c8587134b34dd503cd8");
                await Assert.That(provenance).Contains("It does not establish");
                await Assert.That(provenance).Contains("complete AP214 conformance");
            }
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    /// <summary>Proves the AP242 package-only and fixed-fixture boundaries from an isolated local feed.</summary>
    [Test]
    public async Task Should_build_and_run_the_precompiled_ap242_package_and_fixture_consumer()
    {
        var repositoryRoot = RepositoryPaths.FindRoot();
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"TedToolkit.Step21.Ap242.Packed.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            var packageDirectory = Path.Combine(temporaryRoot, "packages");
            var repeatPackageDirectory = Path.Combine(temporaryRoot, "repeat-packages");
            Directory.CreateDirectory(packageDirectory);
            Directory.CreateDirectory(repeatPackageDirectory);
            var runtimeProject = Path.Combine(repositoryRoot, "src", "TedToolkit.Step21", "TedToolkit.Step21.csproj");
            var packageProject = Path.Combine(repositoryRoot, "src", "TedToolkit.Step21.Ap242", "TedToolkit.Step21.Ap242.csproj");
            var ap203PackageProject = Path.Combine(
                repositoryRoot,
                "src",
                "TedToolkit.Step21.Ap203",
                "TedToolkit.Step21.Ap203.csproj");
            var consumerProject = Path.Combine(repositoryRoot, "tests", "TedToolkit.Step21.PackedConsumer",
                "TedToolkit.Step21.PackedConsumer.csproj");
            _ = await RunDotNet(repositoryRoot, "pack", runtimeProject, "-c", "Release", "--no-build", "--no-restore",
                "-o", packageDirectory);
            _ = await RunDotNet(repositoryRoot, "pack", packageProject, "-c", "Release", "--no-build", "--no-restore",
                "-o", packageDirectory);
            _ = await RunDotNet(repositoryRoot, "pack", packageProject, "-c", "Release", "--no-build", "--no-restore",
                "-o", repeatPackageDirectory);
            _ = await RunDotNet(repositoryRoot, "pack", ap203PackageProject, "-c", "Release", "--no-build",
                "--no-restore", "-o", packageDirectory);
            PackageCache.CopyProjectDependencies(packageDirectory,
                Path.Combine(repositoryRoot, "src", "TedToolkit.Step21", "obj", "project.assets.json"));

            async Task<(string Output, string Intermediate)> BuildAndRun(bool fixture)
            {
                var mode = fixture ? "fixture" : "package";
                var intermediate = Path.Combine(temporaryRoot, mode, "obj") + Path.DirectorySeparatorChar;
                var output = Path.Combine(temporaryRoot, mode, "bin") + Path.DirectorySeparatorChar;
                var common = new[]
                {
                    $"--property:Ap242PackageProof=true",
                    $"--property:Ap242FixtureProof={fixture.ToString().ToLowerInvariant()}",
                    $"--property:BaseIntermediateOutputPath={intermediate}",
                    $"--property:MSBuildProjectExtensionsPath={intermediate}",
                    $"--property:OutputPath={output}",
                };
                _ = await RunDotNet(repositoryRoot,
                    ["restore", consumerProject, "--source", packageDirectory, "--packages",
                        Path.Combine(temporaryRoot, "cache"), .. common]);
                _ = await RunDotNet(repositoryRoot,
                    ["build", consumerProject, "-c", "Release", "--no-restore", .. common]);
                var assembly = Path.Combine(output, "TedToolkit.Step21.PackedConsumer.dll");
                var run = fixture
                    ? await RunDotNet(repositoryRoot, assembly,
                        Path.Combine(repositoryRoot, "tests", "TedToolkit.Step21.IntegrationTests", "TestData", "Ap242",
                            "occt-box-10x20x30-ap242.step"),
                        Path.Combine(repositoryRoot, "tests", "TedToolkit.Step21.IntegrationTests", "TestData", "Ap242",
                            "occt-unsupported-extension-ap242.step"))
                    : await RunDotNet(repositoryRoot, assembly);
                return (run, intermediate);
            }

            var packageRun = await BuildAndRun(fixture: false);
            var fixtureRun = await BuildAndRun(fixture: true);
            var packagePath = Directory.GetFiles(packageDirectory, "TedToolkit.Step21.Ap242.1.0.0.nupkg").Single();
            var repeatPackagePath = Directory.GetFiles(
                repeatPackageDirectory,
                "TedToolkit.Step21.Ap242.1.0.0.nupkg").Single();
            var entries = ReadPackageEntries(packagePath);
            var specification = ReadPackageText(packagePath, "TedToolkit.Step21.Ap242.nuspec");
            var libraries = ReadRuntimeLibraries(Path.Combine(packageRun.Intermediate, "project.assets.json"));
            var coexistIntermediate = Path.Combine(temporaryRoot, "coexist", "obj") + Path.DirectorySeparatorChar;
            var coexistOutput = Path.Combine(temporaryRoot, "coexist", "bin") + Path.DirectorySeparatorChar;
            var coexistProperties = new[]
            {
                "--property:Ap203Ap242PackageProof=true",
                $"--property:BaseIntermediateOutputPath={coexistIntermediate}",
                $"--property:MSBuildProjectExtensionsPath={coexistIntermediate}",
                $"--property:OutputPath={coexistOutput}",
            };
            _ = await RunDotNet(repositoryRoot,
                ["restore", consumerProject, "--source", packageDirectory, "--packages",
                    Path.Combine(temporaryRoot, "coexist-cache"), .. coexistProperties]);
            _ = await RunDotNet(repositoryRoot,
                ["build", consumerProject, "-c", "Release", "--no-restore", .. coexistProperties]);
            var coexistRun = await RunDotNet(
                repositoryRoot,
                Path.Combine(coexistOutput, "TedToolkit.Step21.PackedConsumer.dll"),
                Path.Combine(repositoryRoot, "tests", "TedToolkit.Step21.IntegrationTests", "TestData", "Ap203",
                    "occt-box-10x20x30-ap203.step"),
                Path.Combine(repositoryRoot, "tests", "TedToolkit.Step21.IntegrationTests", "TestData", "Ap242",
                    "occt-box-10x20x30-ap242.step"));
            var coexistLibraries = ReadRuntimeLibraries(Path.Combine(coexistIntermediate, "project.assets.json"));

            using (Assert.Multiple())
            {
                await Assert.That(packageRun.Output).Contains("PACKED_AP242_OK");
                await Assert.That(fixtureRun.Output).Contains(
                    "AP242_FIXTURE_OK entities=170 products=1 faces=6 edges=12 vertices=8 points=27 units=3");
                await Assert.That(fixtureRun.Output).Contains(
                    "AP242_ROUND_TRIP_OK edit=product.name entities=170 faces=6 edges=12 vertices=8 points=27 "
                    + "units=metre,radian,steradian shared-vertex-degrees=3,3,3,3,3,3,3,3");
                await Assert.That(fixtureRun.Output).Contains("AP242_INVALID_EDIT_REJECTED failures=8 output-bytes=0");
                await Assert.That(fixtureRun.Output).Contains(
                    "AP242_EXTENSION_REJECTED code=P21-BIND-ENTITY line=8 column=6");
                await Assert.That(coexistRun).Contains(
                    "PACKED_AP203_AP242_OK ap203-entities=200 ap242-entities=170 "
                    + "runtime-assemblies=1 schema-assemblies=2");
                await Assert.That(ReadNormalizedPackageManifest(packagePath)).IsEqualTo(
                    ReadNormalizedPackageManifest(repeatPackagePath));
                await Assert.That(entries).Contains("lib/netstandard2.0/TedToolkit.Step21.Ap242.dll");
                await Assert.That(entries).Contains("lib/net8.0/TedToolkit.Step21.Ap242.dll");
                await Assert.That(entries).DoesNotContain("lib/net10.0/TedToolkit.Step21.Ap242.dll");
                await Assert.That(entries).Contains("README.md");
                await Assert.That(entries.Any(path => path.EndsWith(".exp", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(entries.Any(path => path.StartsWith("analyzers/", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(specification).Contains(
                    "<dependency id=\"TedToolkit.Step21\" version=\"[1.0.0, 2.0.0)\" exclude=\"Build,Analyzers\" />");
                await Assert.That(specification).DoesNotContain("TedToolkit.Step21.Analyzer");
                await Assert.That(libraries.Count(name => name.Equals(
                    "TedToolkit.Step21.Ap242/1.0.0", StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
                await Assert.That(libraries.Count(name => name.Equals(
                    "TedToolkit.Step21/1.0.0", StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
                await Assert.That(coexistLibraries.Count(name => name.Equals(
                    "TedToolkit.Step21.Ap203/1.0.0", StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
                await Assert.That(coexistLibraries.Count(name => name.Equals(
                    "TedToolkit.Step21.Ap242/1.0.0", StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
                await Assert.That(coexistLibraries.Count(name => name.Equals(
                    "TedToolkit.Step21/1.0.0", StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
                await Assert.That(Directory.Exists(Path.Combine(packageRun.Intermediate, "Generated"))
                    && Directory.EnumerateFiles(Path.Combine(packageRun.Intermediate, "Generated"), "*.g.cs",
                        SearchOption.AllDirectories).Any()).IsFalse();
                await Assert.That(ComputeFileHash(Path.Combine(repositoryRoot, "tests",
                    "TedToolkit.Step21.IntegrationTests", "TestData", "Ap242", "occt-box-10x20x30-ap242.step")))
                    .IsEqualTo("88DA6C164CC685A881A4A52AC7D0BA90E27D649EE9183810887F1A8930AEFD30");
            }
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static async Task<string> RunDotNet(string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start dotnet.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet {string.Join(' ', arguments)} failed.\n{output}\n{error}");
        }

        return output + error;
    }

    private static string[] ReadPackageEntries(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        return archive.Entries.Select(entry => entry.FullName).ToArray();
    }

    private static string ReadPackageText(string packagePath, string entryName)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"The package entry {entryName} is missing.");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static string ReadNormalizedPackageManifest(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        return string.Join(
            '\n',
            archive.Entries
                .Where(entry => entry.Length > 0)
                .Where(entry => !entry.FullName.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                .Where(entry => !entry.FullName.Equals("_rels/.rels", StringComparison.OrdinalIgnoreCase))
                .Where(entry => !entry.FullName.StartsWith(
                    "package/services/metadata/core-properties/",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.FullName, StringComparer.Ordinal)
                .Select(entry => $"{entry.FullName}={ComputeStreamHash(entry.Open())}"));
    }

    private static string[] ReadRuntimeLibraries(string assetsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
        return document.RootElement.GetProperty("libraries")
            .EnumerateObject()
            .Where(item => item.Value.GetProperty("type").GetString() == "package")
            .Select(item => item.Name)
            .ToArray();
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return ComputeStreamHash(stream);
    }

    private static string ComputeCanonicalTextHash(string path) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal))));

    private static string ComputeStreamHash(Stream stream)
    {
        using (stream)
        {
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }
}
