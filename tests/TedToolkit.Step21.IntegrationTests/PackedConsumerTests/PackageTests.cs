// -----------------------------------------------------------------------
// <copyright file="PackageTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

using TedToolkit.Step21.IntegrationTests.ExternalCorpus;

namespace TedToolkit.Step21.IntegrationTests.PackedConsumerTests;

/// <summary>
/// Proves the generator and dependency boundary from the packed consumer's perspective.
/// </summary>
[NotInParallel("packed-consumer")]
internal sealed class PackageTests
{
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
            CopyCachedPackage(
                packageDirectory,
                Path.Combine(repositoryRoot, "src", "TedToolkit.Step21", "obj", "project.assets.json"),
                "antlr4.runtime.standard",
                "4.13.1");

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
                await Assert.That(packageEntries).Contains("README.md");
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
        string buildRoot)
    {
        var consumerPackagesDirectory = Path.Combine(buildRoot, "packages");
        var intermediateDirectory = Path.Combine(buildRoot, "obj") + Path.DirectorySeparatorChar;
        var outputDirectory = Path.Combine(buildRoot, "bin") + Path.DirectorySeparatorChar;
        _ = await RunDotNet(
            repositoryRoot,
            "restore",
            consumerProject,
            "--source",
            packageDirectory,
            "--packages",
            consumerPackagesDirectory,
            "--force",
            "--no-cache",
            $"--property:BaseIntermediateOutputPath={intermediateDirectory}",
            $"--property:MSBuildProjectExtensionsPath={intermediateDirectory}");
        _ = await RunDotNet(
            repositoryRoot,
            "build",
            consumerProject,
            "--configuration",
            "Release",
            "--no-restore",
            $"--property:BaseIntermediateOutputPath={intermediateDirectory}",
            $"--property:MSBuildProjectExtensionsPath={intermediateDirectory}",
            $"--property:OutputPath={outputDirectory}");
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

    private static void CopyCachedPackage(
        string destinationDirectory,
        string producerAssetsPath,
        string packageId,
        string version)
    {
        using var stream = File.OpenRead(producerAssetsPath);
        using var document = JsonDocument.Parse(stream);
        var globalPackages = document.RootElement
            .GetProperty("packageFolders")
            .EnumerateObject()
            .Select(property => property.Name)
            .Single();

        var packagePath = Path.Combine(
            globalPackages,
            packageId,
            version,
            $"{packageId}.{version}.nupkg");
        File.Copy(packagePath, Path.Combine(destinationDirectory, Path.GetFileName(packagePath)));
    }

    private static string[] ReadPackageEntries(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        return archive.Entries.Select(entry => entry.FullName).ToArray();
    }

    private sealed record ConsumerBuild(string IntermediateDirectory, string OutputDirectory);

    private sealed record GeneratedSource(string RelativePath, string Source);
}
