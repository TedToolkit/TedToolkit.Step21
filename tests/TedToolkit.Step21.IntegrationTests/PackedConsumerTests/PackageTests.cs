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
    /// Verifies a local package generates and compiles a schema marker without runtime analyzer dependencies.
    /// </summary>
    [Test]
    public async Task Should_generate_schema_artifact_when_local_package_is_consumed()
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
            var intermediateDirectory = Path.Combine(temporaryRoot, "obj") + Path.DirectorySeparatorChar;
            var outputDirectory = Path.Combine(temporaryRoot, "bin") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(packageDirectory);

            await RunDotNet(
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

            await RunDotNet(
                repositoryRoot,
                "restore",
                consumerProject,
                "--source",
                packageDirectory,
                $"--property:BaseIntermediateOutputPath={intermediateDirectory}",
                $"--property:MSBuildProjectExtensionsPath={intermediateDirectory}");
            await RunDotNet(
                repositoryRoot,
                "build",
                consumerProject,
                "--configuration",
                "Release",
                "--no-restore",
                $"--property:BaseIntermediateOutputPath={intermediateDirectory}",
                $"--property:MSBuildProjectExtensionsPath={intermediateDirectory}",
                $"--property:OutputPath={outputDirectory}");

            var generatedPath = Directory
                .GetFiles(intermediateDirectory, "ExpressSchema_LUNAR_CATALOG.g.cs", SearchOption.AllDirectories)
                .Single();
            var generatedSource = await File.ReadAllTextAsync(generatedPath);
            var runtimeLibraries = ReadRuntimeLibraries(Path.Combine(intermediateDirectory, "project.assets.json"));
            var packageEntries = ReadPackageEntries(packagePath);

            using (Assert.Multiple())
            {
                await Assert.That(generatedSource).Contains("internal sealed class ExpressSchema_lunar_catalog");
                await Assert.That(runtimeLibraries.Any(name => name.StartsWith(
                    "TedToolkit.RoslynHelper/",
                    StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(runtimeLibraries.Any(name => name.Contains("Json", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(runtimeLibraries.Any(name => name.Contains("Xml", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries).Contains("analyzers/dotnet/cs/TedToolkit.RoslynHelper.dll");
                await Assert.That(packageEntries).Contains("analyzers/dotnet/cs/ZString.dll");
                await Assert.That(packageEntries).Contains("analyzers/dotnet/cs/System.Memory.dll");
                await Assert.That(packageEntries.Any(path => path.StartsWith(
                    "lib/",
                    StringComparison.OrdinalIgnoreCase) && path.EndsWith(
                    "/TedToolkit.RoslynHelper.dll",
                    StringComparison.OrdinalIgnoreCase))).IsFalse();
            }
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static async Task RunDotNet(string workingDirectory, params string[] arguments)
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
        await Assert.That(process.ExitCode).IsEqualTo(0)
            .Because($"dotnet {string.Join(' ', arguments)} failed:{Environment.NewLine}"
                + $"{await standardOutput}{Environment.NewLine}{await standardError}");
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
}