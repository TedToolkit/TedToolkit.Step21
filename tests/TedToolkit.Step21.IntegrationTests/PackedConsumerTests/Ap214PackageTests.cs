// -----------------------------------------------------------------------
// <copyright file="Ap214PackageTests.cs" company="TedToolkit">
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
/// Proves the precompiled AP214 package boundary from an isolated consumer.
/// </summary>
[NotInParallel("packed-consumer")]
internal sealed class Ap214PackageTests
{
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
            var consumerProject = Path.Combine(
                repositoryRoot,
                "tests",
                "TedToolkit.Step21.PackedConsumer",
                "TedToolkit.Step21.PackedConsumer.csproj");
            var packageDirectory = Path.Combine(temporaryRoot, "packages");
            var consumerPackages = Path.Combine(temporaryRoot, "consumer-packages");
            var intermediateDirectory = Path.Combine(temporaryRoot, "obj") + Path.DirectorySeparatorChar;
            var outputDirectory = Path.Combine(temporaryRoot, "bin") + Path.DirectorySeparatorChar;
            var customIntermediateDirectory = Path.Combine(temporaryRoot, "custom-obj") + Path.DirectorySeparatorChar;
            var customOutputDirectory = Path.Combine(temporaryRoot, "custom-bin") + Path.DirectorySeparatorChar;
            var nugetConfigPath = Path.Combine(temporaryRoot, "NuGet.Config");
            Directory.CreateDirectory(packageDirectory);

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
            CopyCachedPackage(
                packageDirectory,
                Path.Combine(repositoryRoot, "src", "TedToolkit.Step21", "obj", "project.assets.json"),
                "antlr4.runtime.standard",
                "4.13.1");
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

            var packagePath = Directory.GetFiles(
                packageDirectory,
                "TedToolkit.Step21.Ap214.1.0.0.nupkg").Single();
            var packageEntries = ReadPackageEntries(packagePath);
            var packageSpecification = ReadPackageText(packagePath, "TedToolkit.Step21.Ap214.nuspec");
            var packageReadme = ReadPackageText(packagePath, "README.md");
            var runtimeLibraries = ReadRuntimeLibraries(Path.Combine(intermediateDirectory, "project.assets.json"));
            var generatedRoot = Path.Combine(intermediateDirectory, "Generated");

            using (Assert.Multiple())
            {
                await Assert.That(runOutput).Contains("PACKED_AP214_OK");
                await Assert.That(customRunOutput).Contains("PACKED_AOT_OK");
                await Assert.That(Directory.Exists(generatedRoot)
                    && Directory.EnumerateFiles(generatedRoot, "*.g.cs", SearchOption.AllDirectories).Any()).IsFalse();
                await Assert.That(packageEntries).Contains("lib/net10.0/TedToolkit.Step21.Ap214.dll");
                await Assert.That(packageEntries).Contains("README.md");
                await Assert.That(packageEntries.Any(path => path.EndsWith(".exp", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries.Any(path => path.StartsWith("analyzers/", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageEntries.Any(path => path.Contains("RoslynHelper", StringComparison.OrdinalIgnoreCase))).IsFalse();
                await Assert.That(packageSpecification).Contains(
                    "<dependency id=\"TedToolkit.Step21\" version=\"[1.0.0, 2.0.0)\" exclude=\"Build,Analyzers\" />");
                await Assert.That(packageSpecification).DoesNotContain("TedToolkit.Step21.Ap203");
                await Assert.That(packageSpecification).DoesNotContain("TedToolkit.Step21.Analyzer");
                await Assert.That(packageReadme).Contains("Do not add `AP214E3_2010.exp`");
                await Assert.That(packageReadme).Contains("Distinct custom EXPRESS schemas remain supported");
                await Assert.That(runtimeLibraries.Count(name => name.Equals(
                    "TedToolkit.Step21.Ap214/1.0.0",
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

    private static void CopyCachedPackage(
        string destination,
        string assetsPath,
        string packageId,
        string packageVersion)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
        var globalPackages = document.RootElement.GetProperty("packageFolders")
            .EnumerateObject().First().Name;
        var packageName = packageId.ToLowerInvariant();
        var packagePath = Path.Combine(
            globalPackages,
            packageName,
            packageVersion,
            $"{packageName}.{packageVersion}.nupkg");
        File.Copy(packagePath, Path.Combine(destination, Path.GetFileName(packagePath)));
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

    private static string[] ReadRuntimeLibraries(string assetsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
        return document.RootElement.GetProperty("libraries")
            .EnumerateObject()
            .Where(item => item.Value.GetProperty("type").GetString() == "package")
            .Select(item => item.Name)
            .ToArray();
    }
}
