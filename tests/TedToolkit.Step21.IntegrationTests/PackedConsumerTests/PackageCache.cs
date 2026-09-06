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
    internal static void Copy(string destination, string assetsPath, string packageId, string version)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
        var normalizedId = packageId.ToLowerInvariant();
        var packageName = $"{normalizedId}.{version}.nupkg";
        var packagePath = document.RootElement.GetProperty("packageFolders").EnumerateObject()
            .Select(folder => Path.Combine(folder.Name, normalizedId, version, packageName))
            .FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                $"Cached package '{packageId}/{version}' is missing from all configured package folders.");
        File.Copy(packagePath, Path.Combine(destination, packageName));
    }
}
