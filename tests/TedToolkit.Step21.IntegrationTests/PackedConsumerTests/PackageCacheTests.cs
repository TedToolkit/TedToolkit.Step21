// -----------------------------------------------------------------------
// <copyright file="PackageCacheTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

namespace TedToolkit.Step21.IntegrationTests.PackedConsumerTests;

internal sealed class PackageCacheTests
{
    /// <summary>Uses the first existing package without assuming a single cache or ignoring fallback folders.</summary>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Should_resolve_configured_cache_precedence(bool primaryContainsPackage)
    {
        var root = Directory.CreateTempSubdirectory("TedToolkit.Step21.CacheTest.");
        try
        {
            var primary = Path.Combine(root.FullName, "primary");
            var fallback = Path.Combine(root.FullName, "fallback");
            var output = Directory.CreateDirectory(Path.Combine(root.FullName, "output")).FullName;
            const string relativePackage = "example.package/1.0.0/example.package.1.0.0.nupkg";
            var fallbackPackage = Path.Combine(fallback, relativePackage);
            Directory.CreateDirectory(Path.GetDirectoryName(fallbackPackage)!);
            File.WriteAllText(fallbackPackage, "fallback bytes");
            if (primaryContainsPackage)
            {
                var primaryPackage = Path.Combine(primary, relativePackage);
                Directory.CreateDirectory(Path.GetDirectoryName(primaryPackage)!);
                File.WriteAllText(primaryPackage, "primary bytes");
            }

            var assets = Path.Combine(root.FullName, "project.assets.json");
            File.WriteAllText(assets, JsonSerializer.Serialize(new
            {
                packageFolders = new Dictionary<string, object> { [primary] = new { }, [fallback] = new { }, },
            }));
            PackageCache.Copy(output, assets, "Example.Package", "1.0.0");
            await Assert.That(File.ReadAllText(Path.Combine(output, "example.package.1.0.0.nupkg")))
                .IsEqualTo(primaryContainsPackage ? "primary bytes" : "fallback bytes");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>Reports an absent offline dependency without writing a partial destination package.</summary>
    [Test]
    public async Task Should_reject_missing_cached_packages()
    {
        var root = Directory.CreateTempSubdirectory("TedToolkit.Step21.CacheTest.");
        try
        {
            var assets = Path.Combine(root.FullName, "project.assets.json");
            File.WriteAllText(assets, "{\"packageFolders\":{}}");
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                Task.Run(() => PackageCache.Copy(root.FullName, assets, "Missing.Package", "1.0.0")));
            await Assert.That(Directory.GetFiles(root.FullName, "*.nupkg")).IsEmpty();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
