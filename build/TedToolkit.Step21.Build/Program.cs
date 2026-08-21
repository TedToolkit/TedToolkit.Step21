// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Sourcy.DotNet;

using TedToolkit.ModularPipelines;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var pipeline = new TedPipeline(
    new()
    {
        BuildFiles =
        [
            Solutions.TedToolkit_Step21,
        ],
        Solution = Solutions.TedToolkit_Step21,
        TestFiles =
        [
        ],
    },
    new FileInfo(Path.Combine(Projects.TedToolkit_Step21_Build.Directory!.FullName, "appsettings.json")));

await pipeline.ExecuteAsync().ConfigureAwait(false);
