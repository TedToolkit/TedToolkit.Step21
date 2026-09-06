# -----------------------------------------------------------------------
# <copyright file="verify-native-aot.ps1" company="TedToolkit">
# Copyright (c) TedToolkit. All rights reserved.
# Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
# </copyright>
# -----------------------------------------------------------------------

param(
    [switch] $Ap203,
    [switch] $Ap214,
    [switch] $Ap242)

$ErrorActionPreference = 'Stop'
$runtimeIdentifier = 'win-x64'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$isWindowsHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)

if (@($Ap203, $Ap214, $Ap242).Where({ $_ }).Count -gt 1) {
    throw 'Select at most one precompiled schema package proof.'
}

if (-not $isWindowsHost) {
    throw "Native AOT proof '$runtimeIdentifier' must run on Windows."
}

$proofRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    'TedToolkit.Step21.NativeAot.' + [Guid]::NewGuid().ToString('N'))
$proofSucceeded = $false
$packageDirectory = Join-Path $proofRoot 'packages'
$consumerPackagesDirectory = Join-Path $proofRoot 'consumer-packages'
$intermediateDirectory = (Join-Path $proofRoot 'obj') + [System.IO.Path]::DirectorySeparatorChar
$publishDirectory = Join-Path $proofRoot 'publish'
$nugetConfigPath = Join-Path $proofRoot 'NuGet.Config'
$productProject = Join-Path $repositoryRoot 'src/TedToolkit.Step21/TedToolkit.Step21.csproj'
$analyzerProject = Join-Path $repositoryRoot 'src/TedToolkit.Step21.Analyzer/TedToolkit.Step21.Analyzer.csproj'
$ap203Project = Join-Path $repositoryRoot 'src/TedToolkit.Step21.Ap203/TedToolkit.Step21.Ap203.csproj'
$ap214Project = Join-Path $repositoryRoot 'src/TedToolkit.Step21.Ap214/TedToolkit.Step21.Ap214.csproj'
$ap242Project = Join-Path $repositoryRoot 'src/TedToolkit.Step21.Ap242/TedToolkit.Step21.Ap242.csproj'
$consumerProject = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.PackedConsumer/TedToolkit.Step21.PackedConsumer.csproj'
$ap203FixturePath = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.IntegrationTests/TestData/Ap203/occt-box-10x20x30-ap203.step'
$ap203ExtensionFixturePath = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.IntegrationTests/TestData/Ap203/occt-unsupported-extension-ap203.step'
$ap214FixturePath = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.IntegrationTests/TestData/Ap214/occt-box-10x20x30-ap214.step'
$ap214ExtensionFixturePath = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.IntegrationTests/TestData/Ap214/occt-unsupported-extension-ap214.step'
$ap242FixturePath = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.IntegrationTests/TestData/Ap242/occt-box-10x20x30-ap242.step'
$ap242ExtensionFixturePath = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.IntegrationTests/TestData/Ap242/occt-unsupported-extension-ap242.step'

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

function Get-GlobalPackagesDirectories {
    $assetsPath = Join-Path $repositoryRoot 'src/TedToolkit.Step21/obj/project.assets.json'
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
    return $assets.packageFolders.PSObject.Properties.Name
}

function Copy-CachedPackage {
    param(
        [Parameter(Mandatory = $true)][string] $Id,
        [Parameter(Mandatory = $true)][string] $Version)

    $normalizedId = $Id.ToLowerInvariant()
    $packagePath = Get-GlobalPackagesDirectories |
        ForEach-Object { Join-Path $_ "$normalizedId/$Version/$normalizedId.$Version.nupkg" } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
    if (-not $packagePath) {
        throw "Required cached Native AOT toolchain package '$Id/$Version' is missing from all configured package folders."
    }

    Copy-Item -LiteralPath $packagePath -Destination $packageDirectory
}

function Get-LatestCachedPackageVersion {
    param([Parameter(Mandatory = $true)][string] $Id)

    $normalizedId = $Id.ToLowerInvariant()
    $versions = Get-GlobalPackagesDirectories |
        ForEach-Object {
            Get-ChildItem -LiteralPath (Join-Path $_ $normalizedId) -Directory -ErrorAction SilentlyContinue
        } |
        Where-Object { $_.Name -match '^10\.' } |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "$normalizedId.$($_.Name).nupkg") -PathType Leaf } |
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
    Invoke-DotNet -Arguments @(
        'build',
        $analyzerProject,
        '--configuration', 'Release',
        '--no-restore',
        '--disable-build-servers'
    ) | Out-Null
    $buildProject = if ($Ap203) {
        $ap203Project
    }
    elseif ($Ap214) {
        $ap214Project
    }
    elseif ($Ap242) {
        $ap242Project
    }
    else {
        $productProject
    }
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
    elseif ($Ap214) {
        Invoke-DotNet -Arguments @(
            'pack',
            $ap214Project,
            '--configuration', 'Release',
            '--no-build',
            '--no-restore',
            '--output', $packageDirectory
        ) | Out-Null
    }
    elseif ($Ap242) {
        Invoke-DotNet -Arguments @(
            'pack',
            $ap242Project,
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

    $schemaProperties = if ($Ap203) {
        @('--property:Ap203PackageProof=true', '--property:Ap203FixtureProof=true')
    }
    elseif ($Ap214) {
        @('--property:Ap214PackageProof=true', '--property:Ap214FixtureProof=true')
    }
    elseif ($Ap242) {
        @('--property:Ap242PackageProof=true', '--property:Ap242FixtureProof=true')
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
    $restoreArguments += $schemaProperties
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
    $publishArguments += $schemaProperties
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

    $runArguments = if ($Ap203) {
        @($ap203FixturePath, $ap203ExtensionFixturePath)
    }
    elseif ($Ap214) {
        @($ap214FixturePath, $ap214ExtensionFixturePath, $ap203FixturePath)
    }
    elseif ($Ap242) {
        @($ap242FixturePath, $ap242ExtensionFixturePath)
    }
    else {
        @()
    }
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
    elseif ($Ap214) {
        @(
            'AP214_RAW_EDITION_REJECTED rules=application-protocol-definition-required,product-requires-id-owner partial-model=false',
            'AP214_FIXTURE_OK entities=173 products=1 faces=6 edges=12 vertices=8 points=27 units=3 extents=10x20x30',
            'AP214_REFERENCE_GRAPH_OK entities=173 values-and-named-references=preserved',
            'AP214_ROUND_TRIP_OK edit=product.name entities=173 faces=6 edges=12 vertices=8 points=27 units=millimetre,radian,steradian extents=10x20x30 shared-vertex-degrees=3,3,3,3,3,3,3,3',
            'AP214_INVALID_EDIT_REJECTED failures=',
            'codes=product.frame-of-reference,face.bounds output-bytes=0',
            'AP214_EXTENSION_REJECTED code=P21-BIND-ENTITY line=8 column=6 partial-model=false',
            'AP214_SCHEMA_REJECTED code=P21-BIND-SCHEMA partial-model=false')
    }
    elseif ($Ap242) {
        @(
            'AP242_FIXTURE_OK entities=170 products=1 faces=6 edges=12 vertices=8 points=27 units=3',
            'AP242_ROUND_TRIP_OK edit=product.name entities=170 faces=6 edges=12 vertices=8 points=27 units=metre,radian,steradian shared-vertex-degrees=3,3,3,3,3,3,3,3',
            'AP242_INVALID_EDIT_REJECTED failures=8 output-bytes=0',
            'AP242_EXTENSION_REJECTED code=P21-BIND-ENTITY line=8 column=6')
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
    $proofName = if ($Ap203) {
        'AP203_NATIVE_AOT_PACKAGE_PROOF_OK'
    }
    elseif ($Ap214) {
        'AP214_NATIVE_AOT_PACKAGE_PROOF_OK'
    }
    elseif ($Ap242) {
        'AP242_NATIVE_AOT_PACKAGE_PROOF_OK'
    }
    else {
        'NATIVE_AOT_PACKAGE_PROOF_OK'
    }
    Write-Host "$proofName $runtimeIdentifier executable-bytes=$executableBytes compiler-package=$compilerVersion"
    $proofSucceeded = $true
}
finally {
    $resolvedProofRoot = Resolve-Path -LiteralPath $proofRoot -ErrorAction SilentlyContinue
    $temporaryDirectory = [System.IO.Path]::GetTempPath().TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if ($proofSucceeded -and $resolvedProofRoot -and $resolvedProofRoot.Path.StartsWith(
        "$temporaryDirectory\TedToolkit.Step21.NativeAot.",
        [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedProofRoot.Path -Recurse -Force
    }
    elseif ($resolvedProofRoot) {
        Write-Host "Native AOT proof failed; diagnostic artifacts retained at $($resolvedProofRoot.Path)"
    }
}
