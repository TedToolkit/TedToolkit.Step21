// -----------------------------------------------------------------------
// <copyright file="PackageTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using TedToolkit.Step21.IntegrationTests.ExternalCorpus;

namespace TedToolkit.Step21.IntegrationTests.PackedConsumerTests;

/// <summary>
/// Proves the generator and dependency boundary from the packed consumer's perspective.
/// </summary>
[NotInParallel("packed-consumer")]
internal sealed class PackageTests
{
    private const string Ap203DescriptorTypeName =
        "TedToolkit.Step21.Schemas.Ap203ConfigurationControlled3dDesignOfMechanicalPartsAndAssembliesMimLf.SchemaDescriptor";

    /// <summary>
    /// Verifies the precompiled AP203 package exposes generated types without consumer schema inputs or analyzer runtime assets.
    /// </summary>
    [Test]
    public async Task Should_build_and_run_the_precompiled_ap203_package_only_consumer()
    {
        var repositoryRoot = RepositoryPaths.FindRoot();
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"TedToolkit.Step21.Ap203.Packed.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            var runtimeProject = Path.Combine(repositoryRoot, "src", "TedToolkit.Step21", "TedToolkit.Step21.csproj");
            var packageProject = Path.Combine(repositoryRoot, "src", "TedToolkit.Step21.Ap203", "TedToolkit.Step21.Ap203.csproj");
            var consumerProject = Path.Combine(
                repositoryRoot,
                "tests",
                "TedToolkit.Step21.PackedConsumer",
                "TedToolkit.Step21.PackedConsumer.csproj");
            var packageDirectory = Path.Combine(temporaryRoot, "packages");
            Directory.CreateDirectory(packageDirectory);

            _ = await RunDotNet(
                repositoryRoot,
                "pack",
                runtimeProject,
                "--configuration",
                "Release",
                "--no-build",
                "--output",
                packageDirectory);
            _ = await RunDotNet(
                repositoryRoot,
                "pack",
                packageProject,
                "--configuration",
                "Release",
                "--no-build",
                "--output",
                packageDirectory);
            var packagePath = Directory.GetFiles(packageDirectory, "TedToolkit.Step21.Ap203.2.0.0.nupkg").Single();
            PackageCache.CopyProjectDependencies(
                packageDirectory,
                Path.Combine(repositoryRoot, "src", "TedToolkit.Step21", "obj", "project.assets.json"));

            var consumer = await BuildConsumer(
                repositoryRoot,
                consumerProject,
                packageDirectory,
                Path.Combine(temporaryRoot, "consumer"),
                "--property:Ap203PackageProof=true");
            var runtimeLibraries = ReadRuntimeLibraries(
                Path.Combine(consumer.IntermediateDirectory, "project.assets.json"));
            var packageEntries = ReadPackageEntries(packagePath);
            var packageSpecification = ReadPackageText(packagePath, "TedToolkit.Step21.Ap203.nuspec");
            var runOutput = await RunDotNet(
                repositoryRoot,
                Path.Combine(consumer.OutputDirectory, "TedToolkit.Step21.PackedConsumer.dll"));
            var fixtureConsumer = await BuildConsumer(
                repositoryRoot,
                consumerProject,
                packageDirectory,
                Path.Combine(temporaryRoot, "fixture-consumer"),
                "--property:Ap203PackageProof=true",
                "--property:Ap203FixtureProof=true");
            var fixtureOutput = await RunDotNet(
                repositoryRoot,
                Path.Combine(fixtureConsumer.OutputDirectory, "TedToolkit.Step21.PackedConsumer.dll"),
                Path.Combine(
                    repositoryRoot,
                    "tests",
                    "TedToolkit.Step21.IntegrationTests",
                    "TestData",
                    "Ap203",
                    "occt-box-10x20x30-ap203.step"),
                Path.Combine(
                    repositoryRoot,
                    "tests",
                    "TedToolkit.Step21.IntegrationTests",
                    "TestData",
                    "Ap203",
                    "occt-unsupported-extension-ap203.step"));
            var generatedRoot = Path.Combine(consumer.IntermediateDirectory, "Generated");

            using (Assert.Multiple())
            {
                await Assert.That(runOutput).Contains("PACKED_AP203_OK");
                await Assert.That(fixtureOutput).Contains(
                    "AP203_FIXTURE_OK entities=200 products=1 faces=6 edges=12 vertices=8 points=27 units=3");
                await Assert.That(fixtureOutput).Contains(
                    "AP203_ROUND_TRIP_OK edit=product.name entities=200 faces=6 edges=12 "
                    + "vertices=8 points=27 units=metre,radian,steradian shared-vertex-degrees=3,3,3,3,3,3,3,3");
                await Assert.That(fixtureOutput).Contains(
                    "AP203_INVALID_EDIT_REJECTED failures=9 output-bytes=0");
                await Assert.That(fixtureOutput).Contains(
                    "AP203_EXTENSION_REJECTED code=P21-BIND-ENTITY line=8 column=6");
                await Assert.That(Directory.Exists(generatedRoot)
                    && Directory.EnumerateFiles(generatedRoot, "*.g.cs", SearchOption.AllDirectories).Any()).IsFalse();
                await Assert.That(packageEntries).Contains("lib/netstandard2.0/TedToolkit.Step21.Ap203.dll");
                await Assert.That(packageEntries).Contains("lib/net8.0/TedToolkit.Step21.Ap203.dll");
                await Assert.That(packageEntries).DoesNotContain("lib/net10.0/TedToolkit.Step21.Ap203.dll");
                await Assert.That(packageEntries).Contains("README.md");
                await Assert.That(packageEntries.Any(path => path.EndsWith(".exp", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries.Any(path => path.StartsWith("analyzers/", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries.Any(path => path.Contains("RoslynHelper", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries.Any(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries.Any(path => path.EndsWith("System.Xml.dll", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageSpecification).Contains(
                    "<dependency id=\"TedToolkit.Step21\" version=\"[1.0.0, 2.0.0)\" exclude=\"Build,Analyzers\" />");
                await Assert.That(packageSpecification).DoesNotContain("TedToolkit.Step21.Analyzer");
                await Assert.That(packageSpecification).DoesNotContain("TedToolkit.RoslynHelper");
                await Assert.That(runtimeLibraries.Count(name => name.Equals(
                    "TedToolkit.Step21.Ap203/2.0.0",
                    StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
                await Assert.That(runtimeLibraries.Count(name => name.Equals(
                    "TedToolkit.Step21/1.0.0",
                    StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
                await Assert.That(runtimeLibraries.Any(name => name.StartsWith(
                    "TedToolkit.Step21.Analyzer/",
                    StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(runtimeLibraries.Any(name => name.StartsWith(
                    "TedToolkit.RoslynHelper/",
                    StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(runtimeLibraries.Any(name => name.Contains("Json", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(runtimeLibraries.Any(name => name.Contains("Xml", StringComparison.OrdinalIgnoreCase))).IsFalse();
            }
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the candidate package, repository-owned inputs, and approved generated API describe one versioned baseline.
    /// </summary>
    [Test]
    public async Task Should_match_the_approved_ap203_package_contract_and_reproducible_inputs()
    {
        var repositoryRoot = RepositoryPaths.FindRoot();
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"TedToolkit.Step21.Ap203.Contract.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            var packageProject = Path.Combine(
                repositoryRoot,
                "src",
                "TedToolkit.Step21.Ap203",
                "TedToolkit.Step21.Ap203.csproj");
            var firstPackageDirectory = Path.Combine(temporaryRoot, "first");
            var secondPackageDirectory = Path.Combine(temporaryRoot, "second");
            Directory.CreateDirectory(firstPackageDirectory);
            Directory.CreateDirectory(secondPackageDirectory);

            _ = await RunDotNet(
                repositoryRoot,
                "pack",
                packageProject,
                "--configuration",
                "Release",
                "--no-build",
                "--no-restore",
                "--output",
                firstPackageDirectory);
            _ = await RunDotNet(
                repositoryRoot,
                "pack",
                packageProject,
                "--configuration",
                "Release",
                "--no-build",
                "--no-restore",
                "--output",
                secondPackageDirectory);

            var firstPackage = Directory.GetFiles(
                firstPackageDirectory,
                "TedToolkit.Step21.Ap203.2.0.0.nupkg").Single();
            var secondPackage = Directory.GetFiles(
                secondPackageDirectory,
                "TedToolkit.Step21.Ap203.2.0.0.nupkg").Single();
            var firstManifest = ReadNormalizedPackageManifest(firstPackage);
            var secondManifest = ReadNormalizedPackageManifest(secondPackage);
            var packageSpecification = ReadPackageText(firstPackage, "TedToolkit.Step21.Ap203.nuspec");
            var packagedReadme = ReadPackageText(firstPackage, "README.md");
            var projectReadme = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "TedToolkit.Step21.Ap203",
                "README.md"));
            var schemaHash = ComputeFileHash(Path.Combine(
                repositoryRoot,
                "schemas",
                ".cache",
                "ap203",
                "mim_lf.exp"));
            var validFixtureHash = ComputeFileHash(Path.Combine(
                repositoryRoot,
                "tests",
                "TedToolkit.Step21.IntegrationTests",
                "TestData",
                "Ap203",
                "occt-box-10x20x30-ap203.step"));
            var unsupportedFixtureHash = ComputeFileHash(Path.Combine(
                repositoryRoot,
                "tests",
                "TedToolkit.Step21.IntegrationTests",
                "TestData",
                "Ap203",
                "occt-unsupported-extension-ap203.step"));
            var assemblyContract = InspectPackageAssembly(firstPackage);
            var approvedApiHash = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "tests",
                "TedToolkit.Step21.IntegrationTests",
                "TestData",
                "Ap203",
                "PublicApi.approved.sha256")).Trim();

            using (Assert.Multiple())
            {
                await Assert.That(firstManifest).IsEqualTo(secondManifest);
                await Assert.That(packageSpecification).Contains(
                    "<dependency id=\"TedToolkit.Step21\" version=\"[1.0.0, 2.0.0)\" exclude=\"Build,Analyzers\" />");
                await Assert.That(packagedReadme).IsEqualTo(projectReadme);
                await Assert.That(packagedReadme).Contains("official ISO/TS 10303-403 AP203");
                await Assert.That(packagedReadme).Contains(
                    "`AP203_CONFIGURATION_CONTROLLED_3D_DESIGN_OF_MECHANICAL_PARTS_AND_ASSEMBLIES_MIM_LF`");
                await Assert.That(packagedReadme).Contains("`[1.0.0,2.0.0)`");
                await Assert.That(packagedReadme).Contains("separate rights review");
                await Assert.That(schemaHash).IsEqualTo(
                    "255EAFFD5984373F5FE2F41369088B6FD07F970EB5915CE9920F0A5F339DDD44");
                await Assert.That(validFixtureHash).IsEqualTo(
                    "2F40CE06A8646B3AE33A8BD871181A356D413CDD6B864D9C8D484A3D1E127B62");
                await Assert.That(unsupportedFixtureHash).IsEqualTo(
                    "00B8AA7438180351BE42F30972DA96350302E435D3C778184C174CCCFA1B466F");
                await Assert.That(assemblyContract.DescriptorName).IsEqualTo(
                    "Ap203_configuration_controlled_3d_design_of_mechanical_parts_and_assemblies_mim_lf");
                await Assert.That(assemblyContract.PublicApiHash).IsEqualTo(approvedApiHash)
                    .Because($"Actual AP203 generated public API SHA-256: {assemblyContract.PublicApiHash}");
            }
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a local package reproducibly generates, builds, and runs the representative consumer cleanly.
    /// </summary>
    [Test]
    public async Task Should_build_run_and_reproduce_generated_artifacts_from_the_local_package()
    {
        var repositoryRoot = RepositoryPaths.FindRoot();
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"TedToolkit.Step21.Packed.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            var packageProject = Path.Combine(repositoryRoot, "src", "TedToolkit.Step21", "TedToolkit.Step21.csproj");
            var consumerProject = Path.Combine(
                repositoryRoot,
                "tests",
                "TedToolkit.Step21.PackedConsumer",
                "TedToolkit.Step21.PackedConsumer.csproj");
            var packageDirectory = Path.Combine(temporaryRoot, "packages");
            Directory.CreateDirectory(packageDirectory);

            _ = await RunDotNet(
                repositoryRoot,
                "pack",
                packageProject,
                "--configuration",
                "Release",
                "--no-build",
                "--output",
                packageDirectory);
            var packagePath = Directory.GetFiles(packageDirectory, "TedToolkit.Step21.1.0.0.nupkg").Single();
            PackageCache.CopyProjectDependencies(
                packageDirectory,
                Path.Combine(repositoryRoot, "src", "TedToolkit.Step21", "obj", "project.assets.json"));

            var first = await BuildConsumer(
                repositoryRoot,
                consumerProject,
                packageDirectory,
                Path.Combine(temporaryRoot, "first"));
            var second = await BuildConsumer(
                repositoryRoot,
                consumerProject,
                packageDirectory,
                Path.Combine(temporaryRoot, "second"));
            var firstGenerated = await ReadGeneratedSnapshot(first.IntermediateDirectory);
            var secondGenerated = await ReadGeneratedSnapshot(second.IntermediateDirectory);
            var generatedSource = string.Join('\n', firstGenerated.Select(item => item.Source));
            var runtimeLibraries = ReadRuntimeLibraries(
                Path.Combine(first.IntermediateDirectory, "project.assets.json"));
            var packageEntries = ReadPackageEntries(packagePath);
            var packagedReadme = ReadPackageText(packagePath, "README.md");
            var projectReadme = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "TedToolkit.Step21",
                "README.md"));
            var runOutput = await RunDotNet(
                repositoryRoot,
                Path.Combine(first.OutputDirectory, "TedToolkit.Step21.PackedConsumer.dll"));

            using (Assert.Multiple())
            {
                await Assert.That(firstGenerated.Select(item => item.RelativePath)
                    .SequenceEqual(secondGenerated.Select(item => item.RelativePath))).IsTrue();
                await Assert.That(firstGenerated.Select(item => item.Source)
                    .SequenceEqual(secondGenerated.Select(item => item.Source))).IsTrue();
                await Assert.That(generatedSource).Contains("public sealed class SchemaDescriptor");
                await Assert.That(generatedSource).Contains("global::TedToolkit.Step21.SchemaDescriptor");
                await Assert.That(generatedSource).Contains("public static SchemaDescriptor Instance");
                await Assert.That(generatedSource).Contains("internal sealed class __Complex_Left_Right");
                await Assert.That(generatedSource).DoesNotContain("TedToolkit.RoslynHelper");
                await Assert.That(generatedSource).DoesNotContain("System.Text.Json");
                await Assert.That(generatedSource).DoesNotContain("System.Xml.Serialization");
                await Assert.That(runOutput).Contains("PACKED_AOT_OK");
                await Assert.That(runtimeLibraries.Any(name => name.StartsWith(
                    "TedToolkit.RoslynHelper/",
                    StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(runtimeLibraries.Any(name => name.Contains("Json", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(runtimeLibraries.Any(name => name.Contains("Xml", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries).Contains("analyzers/dotnet/cs/TedToolkit.RoslynHelper.dll");
                await Assert.That(packageEntries).Contains("analyzers/dotnet/cs/ZString.dll");
                await Assert.That(packageEntries).Contains("analyzers/dotnet/cs/System.Memory.dll");
                await Assert.That(packageEntries).Contains("lib/netstandard2.0/TedToolkit.Step21.dll");
                await Assert.That(packageEntries).Contains("lib/net8.0/TedToolkit.Step21.dll");
                await Assert.That(packageEntries).DoesNotContain("lib/net10.0/TedToolkit.Step21.dll");
                await Assert.That(packageEntries).Contains("README.md");
                await Assert.That(packagedReadme).IsEqualTo(projectReadme);
                await Assert.That(packageEntries.Any(path => path.StartsWith(
                    "lib/",
                    StringComparison.OrdinalIgnoreCase) && path.EndsWith(
                    "/TedToolkit.RoslynHelper.dll",
                    StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries.Any(path => path.Contains(
                    "FluentValidation",
                    StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries.Any(path => path.Contains(
                    "System.Text.Json",
                    StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries.Any(path => path.Contains(
                    "System.Xml",
                    StringComparison.OrdinalIgnoreCase))).IsFalse();
            }
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static async Task<ConsumerBuild> BuildConsumer(
        string repositoryRoot,
        string consumerProject,
        string packageDirectory,
        string buildRoot,
        params string[] properties)
    {
        var consumerPackagesDirectory = Path.Combine(buildRoot, "packages");
        var intermediateDirectory = Path.Combine(buildRoot, "obj") + Path.DirectorySeparatorChar;
        var outputDirectory = Path.Combine(buildRoot, "bin") + Path.DirectorySeparatorChar;
        var restoreArguments = new List<string>
        {
            "restore",
            consumerProject,
            "--source",
            packageDirectory,
            "--packages",
            consumerPackagesDirectory,
            "--force",
            "--no-cache",
            $"--property:BaseIntermediateOutputPath={intermediateDirectory}",
            $"--property:MSBuildProjectExtensionsPath={intermediateDirectory}",
        };
        restoreArguments.AddRange(properties);
        _ = await RunDotNet(repositoryRoot, [.. restoreArguments]);

        var buildArguments = new List<string>
        {
            "build",
            consumerProject,
            "--configuration",
            "Release",
            "--no-restore",
            $"--property:BaseIntermediateOutputPath={intermediateDirectory}",
            $"--property:MSBuildProjectExtensionsPath={intermediateDirectory}",
            $"--property:OutputPath={outputDirectory}",
        };
        buildArguments.AddRange(properties);
        _ = await RunDotNet(repositoryRoot, [.. buildArguments]);
        return new(intermediateDirectory, outputDirectory);
    }

    private static async Task<GeneratedSource[]> ReadGeneratedSnapshot(string intermediateDirectory)
    {
        var generatedRoot = Path.Combine(intermediateDirectory, "Generated");
        var paths = Directory.GetFiles(generatedRoot, "*.g.cs", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(generatedRoot, path), StringComparer.Ordinal)
            .ToArray();
        var result = new GeneratedSource[paths.Length];
        for (var index = 0; index < paths.Length; index++)
        {
            result[index] = new(
                Path.GetRelativePath(generatedRoot, paths[index]).Replace('\\', '/'),
                await File.ReadAllTextAsync(paths[index]));
        }

        return result;
    }

    private static async Task<string> RunDotNet(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the dotnet process.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await standardOutput;
        var error = await standardError;
        await Assert.That(process.ExitCode).IsEqualTo(0)
            .Because($"dotnet {string.Join(' ', arguments)} failed:{Environment.NewLine}"
                + $"{output}{Environment.NewLine}{error}");
        return output;
    }

    private static string[] ReadRuntimeLibraries(string assetsPath)
    {
        using var stream = File.OpenRead(assetsPath);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement
            .GetProperty("libraries")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
    }

    private static string[] ReadPackageEntries(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        return archive.Entries.Select(entry => entry.FullName).ToArray();
    }

    private static string ReadPackageText(string packagePath, string entryPath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.GetEntry(entryPath)
            ?? throw new InvalidOperationException($"Package entry '{entryPath}' is missing.");
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

    private static PackageAssemblyContract InspectPackageAssembly(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.GetEntry("lib/net8.0/TedToolkit.Step21.Ap203.dll")
            ?? throw new InvalidOperationException("The packaged AP203 assembly is missing.");
        using var assemblyStream = new MemoryStream();
        using (var entryStream = entry.Open())
        {
            entryStream.CopyTo(assemblyStream);
        }

        assemblyStream.Position = 0;
        var loadContext = new AssemblyLoadContext($"ap203-contract-{Guid.NewGuid():N}", isCollectible: true);
        loadContext.Resolving += static (_, name) => AssemblyLoadContext.Default.Assemblies
            .SingleOrDefault(assembly => AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), name));
        PackageAssemblyContract result;
        try
        {
            var assembly = loadContext.LoadFromStream(assemblyStream);
            var descriptor = assembly.GetType(Ap203DescriptorTypeName, throwOnError: true)!;
            var descriptorInstance = descriptor.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
                .GetValue(null)!;
            var descriptorName = descriptor.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)!
                .GetValue(descriptorInstance)!;
            var descriptorNameValue = (string)descriptorName.GetType().GetProperty("Value")!
                .GetValue(descriptorName)!;
            result = new(
                ComputeTextHash(NormalizeSchemaNamespaceForComparison(RenderPublicApi(assembly))),
                descriptorNameValue);
        }
        finally
        {
            loadContext.Unload();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        return result;
    }

    private static string ComputeCanonicalTextHash(string path)
    {
        var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        return ComputeTextHash(text);
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return ComputeStreamHash(stream);
    }

    private static string ComputeTextHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string ComputeStreamHash(Stream stream)
    {
        using (stream)
        {
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }

    private static string RenderPublicApi(Assembly assembly)
    {
        var lines = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            lines.Add($"{FormatTypeKind(type)} {FormatType(type)} : {FormatType(type.BaseType)}");
            lines.AddRange(type.GetInterfaces()
                .OrderBy(FormatType, StringComparer.Ordinal)
                .Select(contract => $"  interface {FormatType(contract)}"));
            lines.AddRange(type.GetConstructors(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(IsPublicContract)
                .Select(constructor =>
                    $"  constructor {FormatVisibility(constructor)}({FormatParameters(constructor.GetParameters())})")
                .OrderBy(value => value, StringComparer.Ordinal));
            lines.AddRange(type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly)
                .Select(field =>
                    $"  field {FormatVisibility(field)}{(field.IsStatic ? "static " : string.Empty)}"
                    + $"{FormatType(new NullabilityInfoContext().Create(field))} {field.Name}"
                    + (field.IsLiteral ? $" = {FormatConstant(field.GetRawConstantValue())}" : string.Empty))
                .OrderBy(value => value, StringComparer.Ordinal));
            lines.AddRange(type.GetProperties(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(property => property.GetAccessors(nonPublic: true).Any(IsPublicContract))
                .Select(property =>
                {
                    var accessor = property.GetAccessors(nonPublic: true).First(IsPublicContract);
                    return $"  property {FormatVisibility(accessor)}{(accessor.IsStatic ? "static " : string.Empty)}"
                        + $"{FormatType(new NullabilityInfoContext().Create(property))} {property.Name}"
                        + FormatAccessors(property);
                })
                .OrderBy(value => value, StringComparer.Ordinal));
            lines.AddRange(type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => IsPublicContract(method)
                    && (!method.IsSpecialName || method.Name.StartsWith("op_", StringComparison.Ordinal)))
                .Select(method =>
                    $"  method {FormatVisibility(method)}{(method.IsStatic ? "static " : string.Empty)}"
                    + $"{FormatType(new NullabilityInfoContext().Create(method.ReturnParameter))} "
                    + $"{method.Name}({FormatParameters(method.GetParameters())})")
                .OrderBy(value => value, StringComparer.Ordinal));
        }

        return string.Join('\n', lines) + '\n';
    }

    private static string NormalizeSchemaNamespaceForComparison(string publicApi) => publicApi.Replace(
        "TedToolkit.Step21.Schemas.",
        "TedToolkit.Step21.Generated.",
        StringComparison.Ordinal);

    private static bool IsPublicContract(MethodBase method) =>
        method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    private static string FormatVisibility(MethodBase method) => method.IsPublic ? string.Empty : "protected ";

    private static string FormatVisibility(FieldInfo field) => field.IsPublic ? string.Empty : "protected ";

    private static string FormatParameters(IEnumerable<ParameterInfo> parameters) =>
        string.Join(", ", parameters.Select(FormatParameter));

    private static string FormatParameter(ParameterInfo parameter)
    {
        var modifier = parameter.IsOut
            ? "out "
            : parameter.ParameterType.IsByRef && parameter.IsIn
                ? "in "
                : parameter.ParameterType.IsByRef
                    ? "ref "
                    : string.Empty;
        var defaultValue = parameter.HasDefaultValue
            ? $" = {FormatConstant(parameter.DefaultValue)}"
            : string.Empty;
        var nullability = new NullabilityInfoContext().Create(parameter);
        var parameterType = parameter.ParameterType.IsByRef
            ? $"{FormatType(parameter.ParameterType.GetElementType())}"
                + (nullability.ReadState == NullabilityState.Nullable ? "?" : string.Empty)
            : FormatType(nullability);
        return $"{modifier}{parameterType} {parameter.Name}{defaultValue}";
    }

    private static string FormatAccessors(PropertyInfo property)
    {
        var accessors = new List<string>();
        if (property.GetMethod is not null && IsPublicContract(property.GetMethod))
            accessors.Add($"{FormatVisibility(property.GetMethod)}get;");
        if (property.SetMethod is not null && IsPublicContract(property.SetMethod))
            accessors.Add($"{FormatVisibility(property.SetMethod)}set;");
        return $" {{ {string.Join(' ', accessors)} }}";
    }

    private static string FormatConstant(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        char character => $"'{character}'",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
    };

    private static string FormatTypeKind(Type type)
    {
        if (type.IsInterface)
            return "interface";
        if (type.IsEnum)
            return "enum";
        if (type.IsValueType)
        {
            var isReadOnly = type.CustomAttributes.Any(attribute =>
                attribute.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
            return isReadOnly ? "readonly struct" : "struct";
        }

        if (type.IsAbstract && type.IsSealed)
            return "static class";
        if (type.IsAbstract)
            return "abstract class";
        return type.IsSealed ? "sealed class" : "class";
    }

    private static string FormatType(NullabilityInfo nullability)
    {
        var type = nullability.Type;
        var nullableSuffix = nullability.ReadState == NullabilityState.Nullable ? "?" : string.Empty;
        if (type.IsByRef)
            return $"{FormatType(type.GetElementType())}{nullableSuffix}&";
        if (type.IsArray)
            return $"{FormatType(nullability.ElementType!)}[]{nullableSuffix}";
        if (!type.IsGenericType)
            return $"{type.FullName ?? type.Name}{nullableSuffix}";

        if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
            return $"{FormatType(type.GetGenericArguments()[0])}?";

        var genericName = type.GetGenericTypeDefinition().FullName!;
        genericName = genericName[..genericName.IndexOf('`')];
        return $"{genericName}<{string.Join(",", nullability.GenericTypeArguments.Select(FormatType))}>{nullableSuffix}";
    }

    private static string FormatType(Type? type)
    {
        if (type is null)
            return "<none>";
        if (type.IsByRef)
            return $"{FormatType(type.GetElementType())}&";
        if (type.IsArray)
            return $"{FormatType(type.GetElementType())}[]";
        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var genericName = type.GetGenericTypeDefinition().FullName!;
        genericName = genericName[..genericName.IndexOf('`')];
        return $"{genericName}<{string.Join(",", type.GetGenericArguments().Select(FormatType))}>";
    }

    private sealed record ConsumerBuild(string IntermediateDirectory, string OutputDirectory);

    private sealed record GeneratedSource(string RelativePath, string Source);

    private sealed record PackageAssemblyContract(string PublicApiHash, string DescriptorName);
}
