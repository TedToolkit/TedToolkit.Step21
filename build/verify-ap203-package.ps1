# -----------------------------------------------------------------------
# <copyright file="verify-ap203-package.ps1" company="TedToolkit">
# Copyright (c) TedToolkit. All rights reserved.
# Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
# </copyright>
# -----------------------------------------------------------------------

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$proofRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    'TedToolkit.Step21.Ap203.Repro.' + [Guid]::NewGuid().ToString('N'))
$packageDirectory = Join-Path $proofRoot 'packages'
$consumerPackagesDirectory = Join-Path $proofRoot 'consumer-packages'
$intermediateDirectory = (Join-Path $proofRoot 'obj') + [System.IO.Path]::DirectorySeparatorChar
$outputDirectory = (Join-Path $proofRoot 'bin') + [System.IO.Path]::DirectorySeparatorChar
$nugetConfigPath = Join-Path $proofRoot 'NuGet.Config'
$runtimeProject = Join-Path $repositoryRoot 'src/TedToolkit.Step21/TedToolkit.Step21.csproj'
$packageProject = Join-Path $repositoryRoot 'src/TedToolkit.Step21.Ap203/TedToolkit.Step21.Ap203.csproj'
$consumerProject = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.PackedConsumer/TedToolkit.Step21.PackedConsumer.csproj'
$schemaPath = Join-Path $repositoryRoot 'schemas/.cache/ap203/mim_lf.exp'
$fixturePath = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.IntegrationTests/TestData/Ap203/occt-box-10x20x30-ap203.step'
$extensionFixturePath = Join-Path $repositoryRoot 'tests/TedToolkit.Step21.IntegrationTests/TestData/Ap203/occt-unsupported-extension-ap203.step'

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-FileSha256 {
    param([Parameter(Mandatory = $true)][string] $Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-CanonicalTextSha256 {
    param([Parameter(Mandatory = $true)][string] $Path)

    $text = [System.IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes))
}

function Copy-CachedPackage {
    param(
        [Parameter(Mandatory = $true)][string] $Id,
        [Parameter(Mandatory = $true)][string] $Version)

    $assetsPath = Join-Path $repositoryRoot 'src/TedToolkit.Step21/obj/project.assets.json'
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
    $globalPackagesDirectory = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    $packagePath = Join-Path $globalPackagesDirectory (
        "$($Id.ToLowerInvariant())/$Version/$($Id.ToLowerInvariant()).$Version.nupkg")
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "Required cached toolchain package is missing: $packagePath"
    }

    Copy-Item -LiteralPath $packagePath -Destination $packageDirectory
}

function Get-NormalizedPackageDigest {
    param([Parameter(Mandatory = $true)][string] $Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $lines = foreach ($entry in $archive.Entries | Sort-Object FullName) {
            if (($entry.Length -eq 0) -or
                ($entry.FullName -eq '[Content_Types].xml') -or
                ($entry.FullName -eq '_rels/.rels') -or
                $entry.FullName.StartsWith('package/services/metadata/core-properties/', [StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $stream = $entry.Open()
            try {
                $hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream))
            }
            finally {
                $stream.Dispose()
            }

            "$($entry.FullName)=$hash"
        }

        $manifest = [System.Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
        return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($manifest))
    }
    finally {
        $archive.Dispose()
    }
}

New-Item -ItemType Directory -Path $packageDirectory | Out-Null
$nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="candidate" value="$packageDirectory" />
  </packageSources>
</configuration>
"@
[System.IO.File]::WriteAllText($nugetConfigPath, $nugetConfig)

try {
    Invoke-DotNet @(
        'build', $packageProject,
        '--configuration', 'Release',
        '--no-restore',
        '--no-incremental',
        '--disable-build-servers')
    Invoke-DotNet @(
        'pack', $runtimeProject,
        '--configuration', 'Release',
        '--no-build', '--no-restore',
        '--output', $packageDirectory)
    Invoke-DotNet @(
        'pack', $packageProject,
        '--configuration', 'Release',
        '--no-build', '--no-restore',
        '--output', $packageDirectory)
    Copy-CachedPackage -Id 'Antlr4.Runtime.Standard' -Version '4.13.1'

    Invoke-DotNet @(
        'restore', $consumerProject,
        '--configfile', $nugetConfigPath,
        '--packages', $consumerPackagesDirectory,
        '--force', '--no-cache',
        '--property:Ap203PackageProof=true',
        '--property:Ap203FixtureProof=true',
        "--property:BaseIntermediateOutputPath=$intermediateDirectory",
        "--property:MSBuildProjectExtensionsPath=$intermediateDirectory")
    Invoke-DotNet @(
        'build', $consumerProject,
        '--configuration', 'Release',
        '--no-restore',
        '--disable-build-servers',
        '--property:Ap203PackageProof=true',
        '--property:Ap203FixtureProof=true',
        "--property:BaseIntermediateOutputPath=$intermediateDirectory",
        "--property:MSBuildProjectExtensionsPath=$intermediateDirectory",
        "--property:OutputPath=$outputDirectory")

    $consumerAssembly = Join-Path $outputDirectory 'TedToolkit.Step21.PackedConsumer.dll'
    $runOutput = @(& dotnet $consumerAssembly $fixturePath $extensionFixturePath 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "AP203 package consumer failed with exit code $LASTEXITCODE.`n$($runOutput -join "`n")"
    }

    $joinedOutput = $runOutput -join "`n"
    foreach ($expected in @(
        'AP203_FIXTURE_OK entities=200 products=1 faces=6 edges=12 vertices=8 points=27 units=3',
        'AP203_ROUND_TRIP_OK edit=product.name entities=200 faces=6 edges=12 vertices=8 points=27 units=metre,radian,steradian shared-vertex-degrees=3,3,3,3,3,3,3,3',
        'AP203_INVALID_EDIT_REJECTED failures=9 output-bytes=0',
        'AP203_EXTENSION_REJECTED code=P21-BIND-ENTITY line=8 column=6')) {
        if (-not $joinedOutput.Contains($expected, [StringComparison]::Ordinal)) {
            throw "AP203 package consumer output is missing '$expected'."
        }
    }

    $schemaHash = Get-CanonicalTextSha256 $schemaPath
    $fixtureHash = Get-FileSha256 $fixturePath
    $extensionFixtureHash = Get-FileSha256 $extensionFixturePath
    if (($schemaHash -ne '255EAFFD5984373F5FE2F41369088B6FD07F970EB5915CE9920F0A5F339DDD44') -or
        ($fixtureHash -ne '2F40CE06A8646B3AE33A8BD871181A356D413CDD6B864D9C8D484A3D1E127B62') -or
        ($extensionFixtureHash -ne '00B8AA7438180351BE42F30972DA96350302E435D3C778184C174CCCFA1B466F')) {
        throw 'The verified AP203 source cache or a fixture no longer matches its approved checksum.'
    }

    $packagePath = Get-ChildItem -LiteralPath $packageDirectory -Filter 'TedToolkit.Step21.Ap203.2.0.0.nupkg' | Select-Object -ExpandProperty FullName -First 1
    $packageDigest = Get-NormalizedPackageDigest $packagePath
    Write-Host $joinedOutput
    Write-Host "AP203_REPRODUCIBLE_PACKAGE_OK package-sha256=$packageDigest schema-sha256=$schemaHash fixture-sha256=$fixtureHash"
}
finally {
    $resolvedProofRoot = Resolve-Path -LiteralPath $proofRoot -ErrorAction SilentlyContinue
    $temporaryDirectory = [System.IO.Path]::GetTempPath().TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if ($resolvedProofRoot -and $resolvedProofRoot.Path.StartsWith(
        "$temporaryDirectory\TedToolkit.Step21.Ap203.Repro.",
        [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedProofRoot.Path -Recurse -Force
    }
}
