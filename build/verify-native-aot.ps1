# -----------------------------------------------------------------------
# <copyright file="verify-native-aot.ps1" company="TedToolkit">
# Copyright (c) TedToolkit. All rights reserved.
# Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
# </copyright>
# -----------------------------------------------------------------------

param([switch] $Ap203)

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
$ap203Project = Join-Path $repositoryRoot 'src/TedToolkit.Step21.Ap203/TedToolkit.Step21.Ap203.csproj'
$consumerProject = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.PackedConsumer/TedToolkit.Step21.PackedConsumer.csproj'
$fixturePath = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.IntegrationTests/TestData/Ap203/occt-box-10x20x30-ap203.step'
$extensionFixturePath = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.IntegrationTests/TestData/Ap203/occt-unsupported-extension-ap203.step'

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

function Get-GlobalPackagesDirectory {
    $assetsPath = Join-Path $repositoryRoot 'src/TedToolkit.Step21/obj/project.assets.json'
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
    return $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
}

function Copy-CachedPackage {
    param(
        [Parameter(Mandatory = $true)][string] $Id,
        [Parameter(Mandatory = $true)][string] $Version)

    $globalPackagesDirectory = Get-GlobalPackagesDirectory
    $normalizedId = $Id.ToLowerInvariant()
    $packagePath = Join-Path $globalPackagesDirectory "$normalizedId/$Version/$normalizedId.$Version.nupkg"
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "Required cached Native AOT toolchain package is missing: $packagePath"
    }

    Copy-Item -LiteralPath $packagePath -Destination $packageDirectory
}

function Get-LatestCachedPackageVersion {
    param([Parameter(Mandatory = $true)][string] $Id)

    $normalizedId = $Id.ToLowerInvariant()
    $packageRoot = Join-Path (Get-GlobalPackagesDirectory) $normalizedId
    $versions = Get-ChildItem -LiteralPath $packageRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^10\.' } |
        Sort-Object { [Version]$_.Name } -Descending
    if (-not $versions) {
        throw "No cached .NET 10 package was found for '$Id'. Restore the repository's Native AOT toolchain once before running the offline proof."
    }

    return $versions[0].Name
}

New-Item -ItemType Directory -Path $packageDirectory | Out-Null
$nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="proof" value="$packageDirectory" />
  </packageSources>
</configuration>
"@
[System.IO.File]::WriteAllText($nugetConfigPath, $nugetConfig)

try {
    $buildProject = if ($Ap203) { $ap203Project } else { $productProject }
    Invoke-DotNet -Arguments @(
        'build',
        $buildProject,
        '--configuration', 'Release',
        '--no-restore',
        '--disable-build-servers'
    ) | Out-Null
    Invoke-DotNet -Arguments @(
        'pack',
        $productProject,
        '--configuration', 'Release',
        '--no-build',
        '--output', $packageDirectory
    ) | Out-Null
    if ($Ap203) {
        Invoke-DotNet -Arguments @(
            'pack',
            $ap203Project,
            '--configuration', 'Release',
            '--no-build',
            '--no-restore',
            '--output', $packageDirectory
        ) | Out-Null
    }

    Copy-CachedPackage -Id 'Antlr4.Runtime.Standard' -Version '4.13.1'
    $compilerVersion = Get-LatestCachedPackageVersion -Id 'Microsoft.DotNet.ILCompiler'
    Copy-CachedPackage -Id 'Microsoft.DotNet.ILCompiler' -Version $compilerVersion
    Copy-CachedPackage -Id "runtime.$runtimeIdentifier.Microsoft.DotNet.ILCompiler" -Version $compilerVersion
    foreach ($toolchainPackage in @(
        'Microsoft.NET.ILLink.Tasks',
        "Microsoft.NETCore.App.Runtime.$runtimeIdentifier",
        "Microsoft.WindowsDesktop.App.Runtime.$runtimeIdentifier",
        "Microsoft.AspNetCore.App.Runtime.$runtimeIdentifier",
        "Microsoft.NETCore.App.Runtime.NativeAOT.$runtimeIdentifier")) {
        Copy-CachedPackage -Id $toolchainPackage -Version $compilerVersion
    }

    $ap203Properties = if ($Ap203) {
        @('--property:Ap203PackageProof=true', '--property:Ap203FixtureProof=true')
    }
    else {
        @()
    }

    $restoreArguments = @(
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
    $restoreArguments += $ap203Properties
    $restoreLog = Invoke-DotNet -Arguments $restoreArguments
    $publishArguments = @(
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
    $publishArguments += $ap203Properties
    $publishLog = Invoke-DotNet -Arguments $publishArguments

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

    $runArguments = if ($Ap203) { @($fixturePath, $extensionFixturePath) } else { @() }
    $runOutput = @(& $executablePath @runArguments 2>&1)
    $runExitCode = $LASTEXITCODE
    foreach ($line in $runOutput) {
        Write-Host $line
    }

    $joinedOutput = $runOutput -join [Environment]::NewLine
    $expectedOutput = if ($Ap203) {
        @(
            'AP203_FIXTURE_OK entities=200 products=1 faces=6 edges=12 vertices=8 points=27 units=3',
            'AP203_ROUND_TRIP_OK edit=product.name entities=200 faces=6 edges=12 vertices=8 points=27 units=metre,radian,steradian shared-vertex-degrees=3,3,3,3,3,3,3,3',
            'AP203_INVALID_EDIT_REJECTED failures=9 output-bytes=0',
            'AP203_EXTENSION_REJECTED code=P21-BIND-ENTITY line=8 column=6')
    }
    else {
        @('PACKED_AOT_OK')
    }
    if ($runExitCode -ne 0 -or ($expectedOutput | Where-Object { -not $joinedOutput.Contains($_, [StringComparison]::Ordinal) })) {
        throw "Native packed consumer failed with exit code $runExitCode."
    }

    $forbiddenArtifacts = Get-ChildItem -LiteralPath $publishDirectory -File -Recurse |
        Where-Object {
            $_.Name -match 'RoslynHelper|FluentValidation|System\.Text\.Json|System\.Xml'
        }
    if ($forbiddenArtifacts) {
        throw "Forbidden runtime artifacts were published: $($forbiddenArtifacts.Name -join ', ')"
    }

    $executableBytes = (Get-Item -LiteralPath $executablePath).Length
    $proofName = if ($Ap203) { 'AP203_NATIVE_AOT_PACKAGE_PROOF_OK' } else { 'NATIVE_AOT_PACKAGE_PROOF_OK' }
    Write-Host "$proofName $runtimeIdentifier executable-bytes=$executableBytes compiler-package=$compilerVersion"
}
finally {
    $resolvedProofRoot = Resolve-Path -LiteralPath $proofRoot -ErrorAction SilentlyContinue
    $temporaryDirectory = [System.IO.Path]::GetTempPath().TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if ($resolvedProofRoot -and $resolvedProofRoot.Path.StartsWith(
        "$temporaryDirectory\TedToolkit.Step21.NativeAot.",
        [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedProofRoot.Path -Recurse -Force
    }
}
