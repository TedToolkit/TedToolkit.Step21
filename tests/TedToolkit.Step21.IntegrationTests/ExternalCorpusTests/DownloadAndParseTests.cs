using TedToolkit.Step21.IntegrationTests.ExternalCorpus;
using TedToolkit.Step21.IntegrationTests.Parsing;

namespace TedToolkit.Step21.IntegrationTests.ExternalCorpusTests;

internal sealed class DownloadAndParseTests
{
    /// <summary>
    /// Verifies that opted-in external artifacts download into the ignored cache, pass SHA-256 validation, and parse completely.
    /// </summary>
    [Test]
    [NotInParallel("external-corpus-cache")]
    public async Task Should_download_verify_and_parse_corpus_when_external_run_is_enabled()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("TEDTOOLKIT_STEP21_EXTERNAL_CORPUS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip.Test("Set TEDTOOLKIT_STEP21_EXTERNAL_CORPUS=1 to download and verify the external corpus.");
        }

        var manifest = ExternalCorpusManifest.Load();
        var cacheRoot = Path.Combine(RepositoryPaths.FindRoot(), manifest.CacheDirectory);
        var failures = new List<string>();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        foreach (var artifact in manifest.Artifacts)
        {
            var files = await ExternalCorpusCache.MaterializeAsync(
                httpClient,
                artifact,
                cacheRoot,
                CancellationToken.None);

            foreach (var file in files)
            {
                var errors = ExternalCorpusParser.Parse(file.Path, file.Format);
                failures.AddRange(errors.Select(error => $"{artifact.Id}/{Path.GetFileName(file.Path)}: {error}"));
            }
        }

        foreach (var failure in failures)
        {
            Console.WriteLine(failure);
        }

        await Assert.That(failures.Count).IsEqualTo(0);
    }
}
