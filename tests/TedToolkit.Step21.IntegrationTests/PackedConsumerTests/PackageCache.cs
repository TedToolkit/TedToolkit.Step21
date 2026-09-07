// -----------------------------------------------------------------------
// <copyright file="PackageCache.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

namespace TedToolkit.Step21.IntegrationTests.PackedConsumerTests;

/// <summary>
/// Copies offline consumer dependencies from the producer's configured package caches in precedence order.
/// </summary>
internal static class PackageCache
{
    internal static void CopyProjectDependencies(string destination, string assetsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
        var root = document.RootElement;
        var libraries = root.GetProperty("libraries");
        var dependencies = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var framework in root.GetProperty("project").GetProperty("frameworks").EnumerateObject())
        {
            foreach (var dependency in framework.Value.GetProperty("dependencies").EnumerateObject())
            {
                var library = libraries.EnumerateObject().SingleOrDefault(candidate =>
                    candidate.Name.StartsWith(dependency.Name + "/", StringComparison.OrdinalIgnoreCase)
                    && candidate.Value.GetProperty("type").GetString() == "package");
                if (library.Name is null)
                    throw new InvalidDataException($"Package dependency '{dependency.Name}' has no resolved library.");
                dependencies[dependency.Name] = library.Name[(library.Name.LastIndexOf('/') + 1)..];
            }
        }

        var copies = dependencies.Select(dependency =>
        {
            var packagePath = ResolvePackagePath(document, dependency.Key, dependency.Value);
            return (Source: packagePath, Destination: Path.Combine(destination, Path.GetFileName(packagePath)));
        }).ToArray();
        foreach (var copy in copies)
            File.Copy(copy.Source, copy.Destination);
    }

    internal static void Copy(string destination, string assetsPath, string packageId, string version)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
        Copy(document, destination, packageId, version);
    }

    private static void Copy(
        JsonDocument document,
        string destination,
        string packageId,
        string version)
    {
        var packagePath = ResolvePackagePath(document, packageId, version);
        File.Copy(packagePath, Path.Combine(destination, Path.GetFileName(packagePath)));
    }

    private static string ResolvePackagePath(JsonDocument document, string packageId, string version)
    {
        var normalizedId = packageId.ToLowerInvariant();
        var packageName = $"{normalizedId}.{version}.nupkg";
        return document.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.Combine(folder.Name, normalizedId, version, packageName))
            .FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                $"Cached package '{packageId}/{version}' is missing from all configured package folders.");
    }
}