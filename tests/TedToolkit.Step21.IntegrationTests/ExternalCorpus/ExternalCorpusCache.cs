using System.IO.Compression;
using System.Security.Cryptography;

namespace TedToolkit.Step21.IntegrationTests.ExternalCorpus;

internal static class ExternalCorpusCache
{
    public static async Task<IReadOnlyList<CorpusMaterializedFile>> MaterializeAsync(
        HttpClient httpClient,
        ExternalCorpusArtifact artifact,
        string cacheRoot,
        CancellationToken cancellationToken)
    {
        var normalizedCacheRoot = Path.GetFullPath(cacheRoot);
        var artifactDirectory = GetContainedPath(normalizedCacheRoot, artifact.Id);
        Directory.CreateDirectory(artifactDirectory);

        var downloadPath = GetContainedPath(artifactDirectory, artifact.FileName);
        if (!await HasExpectedContentAsync(downloadPath, artifact, cancellationToken))
        {
            await DownloadAsync(httpClient, artifact, downloadPath, cancellationToken);
        }

        return artifact.ArchiveKind switch
        {
            "file" => MaterializeFile(artifact, downloadPath),
            "zip" => await MaterializeZipAsync(artifact, downloadPath, artifactDirectory, cancellationToken),
            _ => throw new InvalidDataException($"Unsupported archive kind '{artifact.ArchiveKind}'."),
        };
    }

    private static IReadOnlyList<CorpusMaterializedFile> MaterializeFile(
        ExternalCorpusArtifact artifact,
        string downloadPath)
    {
        if (artifact.TestFiles.Count != 1 ||
            !string.Equals(artifact.TestFiles[0].Path, artifact.FileName, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Direct artifact '{artifact.Id}' must describe its downloaded file once.");
        }

        return [new CorpusMaterializedFile(downloadPath, artifact.TestFiles[0].Format)];
    }

    private static async Task<IReadOnlyList<CorpusMaterializedFile>> MaterializeZipAsync(
        ExternalCorpusArtifact artifact,
        string downloadPath,
        string artifactDirectory,
        CancellationToken cancellationToken)
    {
        var result = new List<CorpusMaterializedFile>();
        using var archive = ZipFile.OpenRead(downloadPath);

        foreach (var testFile in artifact.TestFiles)
        {
            EnsureFileName(testFile.CacheFileName);
            var entry = archive.GetEntry(testFile.Path)
                        ?? throw new InvalidDataException(
                            $"Archive '{artifact.Id}' does not contain '{testFile.Path}'.");
            var extractedPath = GetContainedPath(artifactDirectory, testFile.CacheFileName);

            await using var input = entry.Open();
            await using var output = new FileStream(
                extractedPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            await input.CopyToAsync(output, cancellationToken);
            result.Add(new CorpusMaterializedFile(extractedPath, testFile.Format));
        }

        return result;
    }

    private static async Task DownloadAsync(
        HttpClient httpClient,
        ExternalCorpusArtifact artifact,
        string downloadPath,
        CancellationToken cancellationToken)
    {
        var temporaryPath = downloadPath + "." + Guid.NewGuid().ToString("N") + ".download";

        try
        {
            await using (var input = await httpClient.GetStreamAsync(
                             new Uri(artifact.DownloadUrl, UriKind.Absolute),
                             cancellationToken))
            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            if (!await HasExpectedContentAsync(temporaryPath, artifact, cancellationToken))
            {
                throw new InvalidDataException($"Downloaded artifact '{artifact.Id}' failed size or SHA-256 validation.");
            }

            File.Move(temporaryPath, downloadPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task<bool> HasExpectedContentAsync(
        string path,
        ExternalCorpusArtifact artifact,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != artifact.SizeBytes)
        {
            return false;
        }

        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return string.Equals(Convert.ToHexString(hash), artifact.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetContainedPath(string parent, string child)
    {
        EnsureFileName(child);
        var path = Path.GetFullPath(Path.Combine(parent, child));
        var expectedPrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent))
                             + Path.DirectorySeparatorChar;

        if (!path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Cache path '{child}' escapes its parent directory.");
        }

        return path;
    }

    private static void EnsureFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"'{value}' is not a safe file name.");
        }
    }
}
