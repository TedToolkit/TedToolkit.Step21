using System.Text.Json;

namespace TedToolkit.Step21.IntegrationTests.ExternalCorpus;

internal sealed record ExternalCorpusManifest(
    string CacheDirectory,
    IReadOnlyList<ExternalCorpusArtifact> Artifacts)
{
    public static ExternalCorpusManifest Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ExternalCorpus", "manifest.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ExternalCorpusManifest>(
                   json,
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidDataException("The external corpus manifest is empty.");
    }
}

internal sealed record ExternalCorpusArtifact(
    string Id,
    string DownloadUrl,
    string FileName,
    string ArchiveKind,
    long SizeBytes,
    string Sha256,
    string License,
    string SourcePage,
    IReadOnlyList<ExternalCorpusTestFile> TestFiles);

internal sealed record ExternalCorpusTestFile(
    string Path,
    string CacheFileName,
    string Format);
