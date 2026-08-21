using TedToolkit.Step21.IntegrationTests.ExternalCorpus;

namespace TedToolkit.Step21.IntegrationTests.ExternalCorpusManifestTests;

internal sealed class ManifestTests
{
    /// <summary>
    /// Verifies that every external corpus artifact has immutable integrity and provenance metadata.
    /// </summary>
    [Test]
    public async Task Should_describe_safe_reproducible_artifacts_when_manifest_is_loaded()
    {
        var manifest = ExternalCorpusManifest.Load();
        var artifacts = manifest.Artifacts;

        using (Assert.Multiple())
        {
            await Assert.That(manifest.CacheDirectory).IsEqualTo("artifacts/test-corpus");
            await Assert.That(artifacts.Count).IsGreaterThan(0);
            await Assert.That(artifacts.Select(artifact => artifact.Id).Distinct().Count())
                .IsEqualTo(artifacts.Count);
            await Assert.That(artifacts.All(IsValidArtifact)).IsTrue();
        }
    }

    /// <summary>
    /// Verifies that the configured external corpus cache cannot be committed accidentally.
    /// </summary>
    [Test]
    public async Task Should_ignore_cache_directory_when_repository_is_checked()
    {
        var gitIgnore = File.ReadAllText(Path.Combine(RepositoryPaths.FindRoot(), ".gitignore"));

        await Assert.That(gitIgnore).Contains("artifacts/test-corpus/");
    }

    private static bool IsValidArtifact(ExternalCorpusArtifact artifact)
    {
        return Uri.TryCreate(artifact.DownloadUrl, UriKind.Absolute, out var downloadUri) &&
               downloadUri.Scheme == Uri.UriSchemeHttps &&
               Uri.TryCreate(artifact.SourcePage, UriKind.Absolute, out var sourceUri) &&
               sourceUri.Scheme == Uri.UriSchemeHttps &&
               artifact.SizeBytes > 0 &&
               artifact.Sha256.Length == 64 &&
               artifact.Sha256.All(Uri.IsHexDigit) &&
               !string.IsNullOrWhiteSpace(artifact.License) &&
               artifact.TestFiles.Count > 0;
    }
}
