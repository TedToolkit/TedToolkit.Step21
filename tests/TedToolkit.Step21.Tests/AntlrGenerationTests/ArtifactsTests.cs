using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TedToolkit.Step21.Tests.AntlrGenerationTests;

[NotInParallel("antlr-generation")]
internal sealed class ArtifactsTests
{
    private static readonly string[] RequiredSources =
    [
        "src/TedToolkit.Step21/Generated/STEP/STEPParser.cs",
        "src/TedToolkit.Step21/Generated/STEP/STEPLexer.cs",
        "src/TedToolkit.Step21/Generated/STEP/STEPParserVisitor.cs",
        "src/TedToolkit.Step21/Generated/STEP/STEPParserBaseVisitor.cs",
        "src/TedToolkit.Step21.Analyzer/Generated/Express/ExpressParser.cs",
        "src/TedToolkit.Step21.Analyzer/Generated/Express/ExpressLexer.cs",
        "src/TedToolkit.Step21.Analyzer/Generated/Express/ExpressVisitor.cs",
        "src/TedToolkit.Step21.Analyzer/Generated/Express/ExpressBaseVisitor.cs",
    ];

    /// <summary>
    /// Verifies that the native ANTLR script emits complete, internal, listener-free, byte-stable artifacts.
    /// </summary>
    [Test]
    public async Task Should_generate_reproducible_internal_artifacts_when_native_script_runs_twice()
    {
        var repositoryRoot = FindRepositoryRoot();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"TedToolkit.Step21.Antlr.{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputRoot);

        try
        {
            var firstRun = await RunGenerator(repositoryRoot, outputRoot);
            await Assert.That(firstRun.ExitCode).IsEqualTo(0)
                .Because($"ANTLR generation failed:{Environment.NewLine}{firstRun.StandardError}");
            var firstSnapshot = Snapshot(outputRoot);

            var secondRun = await RunGenerator(repositoryRoot, outputRoot);
            await Assert.That(secondRun.ExitCode).IsEqualTo(0)
                .Because($"ANTLR regeneration failed:{Environment.NewLine}{secondRun.StandardError}");
            var secondSnapshot = Snapshot(outputRoot);

            foreach (var requiredSource in RequiredSources)
            {
                await Assert.That(firstSnapshot.ContainsKey(requiredSource)).IsTrue()
                    .Because($"Required generated artifact '{requiredSource}' was not emitted.");
            }

            await Assert.That(firstSnapshot.Keys.Any(path => path.Contains("Listener", StringComparison.Ordinal))).IsFalse();
            await Assert.That(firstSnapshot.Keys).IsEquivalentTo(secondSnapshot.Keys);

            foreach (var (path, firstBytes) in firstSnapshot)
            {
                await Assert.That(firstBytes.SequenceEqual(secondSnapshot[path])).IsTrue()
                    .Because($"Generated artifact '{path}' changed between identical runs.");
                await Assert.That(firstBytes.Contains((byte)'\r')).IsFalse()
                    .Because($"Generated artifact '{path}' must use normalized LF line endings.");

                if (!path.EndsWith(".cs", StringComparison.Ordinal))
                {
                    continue;
                }

                var source = File.ReadAllText(Path.Combine(outputRoot, path.Replace('/', Path.DirectorySeparatorChar)));
                await Assert.That(Regex.IsMatch(source, "(?m)^public (?=(?:partial class|interface)\\s)")).IsFalse()
                    .Because($"Generated top-level type in '{path}' must not be public.");
                await Assert.That(Regex.IsMatch(source, "(?m)^internal (?=(?:partial class|interface)\\s)")).IsTrue()
                    .Because($"Generated top-level type in '{path}' must be internal.");
                await Assert.That(source).DoesNotContain("Console.Out, Console.Error")
                    .Because($"Generated parser defaults in '{path}' must be analyzer-host safe.");
            }
        }
        finally
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TedToolkit.Step21.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the TedToolkit.Step21 repository root.");
    }

    private static async Task<ProcessResult> RunGenerator(string repositoryRoot, string outputRoot)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "pwsh" : "/bin/sh",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = repositoryRoot,
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "build", "generate-antlr.ps1"));
            startInfo.ArgumentList.Add("-OutputRoot");
        }
        else
        {
            startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "build", "generate-antlr.sh"));
        }

        startInfo.ArgumentList.Add(outputRoot);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the ANTLR generation process.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
    }

    private static Dictionary<string, byte[]> Snapshot(string outputRoot)
    {
        return Directory
            .EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(outputRoot, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
