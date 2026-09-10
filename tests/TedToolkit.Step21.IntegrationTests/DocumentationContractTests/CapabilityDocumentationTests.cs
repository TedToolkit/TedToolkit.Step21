// -----------------------------------------------------------------------
// <copyright file="CapabilityDocumentationTests.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

using TedToolkit.Step21.IntegrationTests.ExternalCorpus;

namespace TedToolkit.Step21.IntegrationTests.DocumentationContractTests;

/// <summary>
/// Fixes the released repository, package, and conformance documentation boundary.
/// </summary>
internal sealed class CapabilityDocumentationTests
{
    /// <summary>Verifies that each documentation façade states the delivered and unsupported capabilities.</summary>
    [Test]
    public async Task Should_publish_truthful_repository_package_and_conformance_guides()
    {
        var root = RepositoryPaths.FindRoot();
        var repositoryReadme = File.ReadAllText(Path.Combine(root, "README.md"));
        var packageReadme = File.ReadAllText(Path.Combine(root, "src", "TedToolkit.Step21", "README.md"));
        var conformanceReadme = File.ReadAllText(Path.Combine(root, "docs", "conformance", "README.md"));

        using (Assert.Multiple())
        {
            await Assert.That(repositoryReadme).Contains("src/TedToolkit.Step21/README.md");
            await Assert.That(repositoryReadme).Contains("docs/conformance/README.md");
            await Assert.That(packageReadme).Contains("dotnet add package TedToolkit.Step21");
            await Assert.That(packageReadme).Contains("<AdditionalFiles Include=");
            await Assert.That(packageReadme).Contains("SchemaDescriptor.Instance");
            await Assert.That(packageReadme).Contains("ExchangeStructure.Read");
            await Assert.That(packageReadme).Contains("structure.Entities");
            await Assert.That(packageReadme).Contains("structure.Validate()");
            await Assert.That(packageReadme).Contains("structure.Write(");
            await Assert.That(packageReadme).Contains("DataSection");
            await Assert.That(packageReadme).Contains("JSON or XML");
            await Assert.That(packageReadme).Contains("byte-preserving");
            await Assert.That(packageReadme).Contains("Native AOT");
            await Assert.That(conformanceReadme).Contains("Delivered");
            await Assert.That(conformanceReadme).Contains("Source-excluded operations");
            await Assert.That(conformanceReadme).Contains("Outside the public contract");
            await Assert.That(conformanceReadme).Contains("Explicit distributed reference");
            await Assert.That(conformanceReadme).Contains("EXPRESS constant occurrences");
            await Assert.That(conformanceReadme).Contains("signature verification");
            await Assert.That(conformanceReadme).Contains("source-bounded complex");
            await Assert.That(conformanceReadme).Contains("Normative evidence");
            await Assert.That(conformanceReadme).Contains("Corpus regression evidence");
        }
    }

    /// <summary>Verifies that the documentation entry points contain no broken repository-relative file links.</summary>
    [Test]
    public async Task Should_resolve_local_links_from_documentation_entry_points()
    {
        var root = RepositoryPaths.FindRoot();
        var files = new[]
        {
            Path.Combine(root, "README.md"),
            Path.Combine(root, "src", "TedToolkit.Step21", "README.md"),
            Path.Combine(root, "docs", "conformance", "README.md"),
        };
        var missing = files
            .SelectMany(file => FindMissingLinks(file))
            .ToArray();

        await Assert.That(missing).IsEmpty();
    }

    private static IEnumerable<string> FindMissingLinks(string markdownPath)
    {
        var directory = Path.GetDirectoryName(markdownPath)!;
        foreach (Match match in Regex.Matches(File.ReadAllText(markdownPath), @"\[[^\]]+\]\(([^)]+)\)"))
        {
            var target = match.Groups[1].Value.Split('#')[0];
            if (string.IsNullOrEmpty(target)
                || Uri.TryCreate(target, UriKind.Absolute, out _))
            {
                continue;
            }

            var path = Path.GetFullPath(Path.Combine(directory, Uri.UnescapeDataString(target)));
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                yield return $"{Path.GetRelativePath(directory, markdownPath)} -> {target}";
            }
        }
    }
}