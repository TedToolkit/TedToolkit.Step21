namespace TedToolkit.Step21.IntegrationTests.ExternalCorpus;

internal static class RepositoryPaths
{
    public static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TedToolkit.Step21.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the TedToolkit.Step21 repository root.");
    }
}
