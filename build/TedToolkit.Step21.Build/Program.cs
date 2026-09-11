// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.ModularPipelines;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
var solution = new FileInfo(Path.Combine(repositoryRoot.FullName, "TedToolkit.Step21.slnx"));
var packageSolution = new FileInfo(Path.Combine(repositoryRoot.FullName, "TedToolkit.Step21.Package.slnx"));
var buildProjectDirectory = new DirectoryInfo(Path.Combine(
    repositoryRoot.FullName,
    "build",
    "TedToolkit.Step21.Build"));

var pipeline = new TedPipeline(
    new()
    {
        BuildFiles =
        [
            packageSolution,
        ],
        Solution = solution,
        // GitHub Actions runs the sharded unit and integration suites before invoking this
        // release pipeline. Re-running them here would serialize the expensive AP package tests.
        TestFiles = [],
    },
    new FileInfo(Path.Combine(buildProjectDirectory.FullName, "appsettings.json")));

await pipeline.ExecuteAsync().ConfigureAwait(false);

static DirectoryInfo FindRepositoryRoot(string startPath)
{
    for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "TedToolkit.Step21.slnx")))
        {
            return directory;
        }
    }

    throw new DirectoryNotFoundException("Could not locate TedToolkit.Step21.slnx from the build output path.");
}
