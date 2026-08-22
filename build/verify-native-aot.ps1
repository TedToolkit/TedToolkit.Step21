# -----------------------------------------------------------------------
# <copyright file="verify-native-aot.ps1" company="TedToolkit">
# Copyright (c) TedToolkit. All rights reserved.
# Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
# </copyright>
# -----------------------------------------------------------------------

$ErrorActionPreference = 'Stop'
$runtimeIdentifier = 'win-x64'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$isWindowsHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)

if (-not $isWindowsHost) {
    throw "Native AOT proof '$runtimeIdentifier' must run on Windows."
}

$proofRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    'TedToolkit.Step21.NativeAot.' + [Guid]::NewGuid().ToString('N'))
$packageDirectory = Join-Path $proofRoot 'packages'
$consumerPackagesDirectory = Join-Path $proofRoot 'consumer-packages'
$intermediateDirectory = (Join-Path $proofRoot 'obj') + [System.IO.Path]::DirectorySeparatorChar
$publishDirectory = Join-Path $proofRoot 'publish'
$nugetConfigPath = Join-Path $proofRoot 'NuGet.Config'
$productProject = Join-Path $repositoryRoot 'src/TedToolkit.Step21/TedToolkit.Step21.csproj'
$consumerProject = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.PackedConsumer/TedToolkit.Step21.PackedConsumer.csproj'

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'dotnet'
    $startInfo.WorkingDirectory = $repositoryRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    if ($isWindowsHost) {
        $startInfo.Environment['OS'] = 'Windows_NT'
    }
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $standardOutputTask = $process.StandardOutput.ReadToEndAsync()
    $standardErrorTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $standardOutput = $standardOutputTask.GetAwaiter().GetResult()
    $standardError = $standardErrorTask.GetAwaiter().GetResult()
    $output = $standardOutput + $standardError
    Write-Host $output

    if ($process.ExitCode -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $($process.ExitCode)."
    }

    return $output
}

New-Item -ItemType Directory -Path $packageDirectory | Out-Null
$nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="proof" value="$packageDirectory" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@
[System.IO.File]::WriteAllText($nugetConfigPath, $nugetConfig)

try {
    Invoke-DotNet -Arguments @(
        'build',
        $productProject,
        '--configuration', 'Release',
        '--disable-build-servers'
    ) | Out-Null
    Invoke-DotNet -Arguments @(
        'pack',
        $productProject,
        '--configuration', 'Release',
        '--no-build',
        '--output', $packageDirectory
    ) | Out-Null

    $restoreLog = Invoke-DotNet -Arguments @(
        'restore',
        $consumerProject,
        '--runtime', $runtimeIdentifier,
        '--configfile', $nugetConfigPath,
        '--packages', $consumerPackagesDirectory,
        '--force',
        '--property:NativeAotProof=true',
        "--property:BaseIntermediateOutputPath=$intermediateDirectory",
        "--property:MSBuildProjectExtensionsPath=$intermediateDirectory"
    )
    $publishLog = Invoke-DotNet -Arguments @(
        'publish',
        $consumerProject,
        '--configuration', 'Release',
        '--runtime', $runtimeIdentifier,
        '--self-contained', 'true',
        '--no-restore',
        '--disable-build-servers',
        '--output', $publishDirectory,
        '--property:NativeAotProof=true',
        "--property:BaseIntermediateOutputPath=$intermediateDirectory",
        "--property:MSBuildProjectExtensionsPath=$intermediateDirectory"
    )

    $analysisWarnings = ($restoreLog + [Environment]::NewLine + $publishLog) |
        Select-String -Pattern '\bwarning\s+(?:IL|ILC|AOT)\d+\b' -AllMatches
    if ($analysisWarnings) {
        throw "Native AOT or trimming warnings were emitted: $analysisWarnings"
    }

    $executableName = if ($isWindowsHost) {
        'TedToolkit.Step21.PackedConsumer.exe'
    }
    else {
        'TedToolkit.Step21.PackedConsumer'
    }
    $executablePath = Join-Path $publishDirectory $executableName
    if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
        throw "Native executable was not produced at '$executablePath'."
    }

    $runOutput = @(& $executablePath 2>&1)
    $runExitCode = $LASTEXITCODE
    foreach ($line in $runOutput) {
        Write-Host $line
    }

    if ($runExitCode -ne 0 -or ($runOutput -join [Environment]::NewLine) -notmatch 'PACKED_AOT_OK') {
        throw "Native packed consumer failed with exit code $runExitCode."
    }

    $forbiddenArtifacts = Get-ChildItem -LiteralPath $publishDirectory -File -Recurse |
        Where-Object {
            $_.Name -match 'RoslynHelper|FluentValidation|System\.Text\.Json|System\.Xml'
        }
    if ($forbiddenArtifacts) {
        throw "Forbidden runtime artifacts were published: $($forbiddenArtifacts.Name -join ', ')"
    }

    Write-Host "NATIVE_AOT_PACKAGE_PROOF_OK $runtimeIdentifier"
}
finally {
    if (Test-Path -LiteralPath $proofRoot) {
        Remove-Item -LiteralPath $proofRoot -Recurse -Force
    }
}
