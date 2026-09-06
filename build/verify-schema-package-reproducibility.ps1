# -----------------------------------------------------------------------
# <copyright file="verify-schema-package-reproducibility.ps1" company="TedToolkit">
# Copyright (c) TedToolkit. All rights reserved.
# Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
# </copyright>
# -----------------------------------------------------------------------

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Ap203', 'Ap214', 'Ap242')]
    [string] $Schema)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$schemaKey = $Schema.ToLowerInvariant()
$packageId = "TedToolkit.Step21.$Schema"
$packageProject = Join-Path $repositoryRoot "src/$packageId/$packageId.csproj"
$schemaDirectory = Join-Path $repositoryRoot "schemas/$schemaKey"
$fixtureDirectory = Join-Path $repositoryRoot (
    "tests/TedToolkit.Step21.IntegrationTests/TestData/$Schema")
$proofRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    "TedToolkit.Step21.$Schema.Repro." + [Guid]::NewGuid().ToString('N'))

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    & dotnet @Arguments | ForEach-Object { Write-Host $_ }
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

function Get-NormalizedPackageManifest {
    param([Parameter(Mandatory = $true)][string] $Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        return @($archive.Entries |
            Where-Object {
                ($_.Length -gt 0) -and
                ($_.FullName -ne '[Content_Types].xml') -and
                ($_.FullName -ne '_rels/.rels') -and
                (-not $_.FullName.StartsWith(
                    'package/services/metadata/core-properties/',
                    [StringComparison]::OrdinalIgnoreCase))
            } |
            Sort-Object FullName |
            ForEach-Object {
                $stream = $_.Open()
                try {
                    $hash = [Convert]::ToHexString(
                        [System.Security.Cryptography.SHA256]::HashData($stream))
                }
                finally {
                    $stream.Dispose()
                }

                "$($_.FullName)=$hash"
            })
    }
    finally {
        $archive.Dispose()
    }
}

function Get-InputManifest {
    $paths = @(
        $packageProject
        (Join-Path $repositoryRoot 'Directory.Build.props')
        (Join-Path $repositoryRoot 'Directory.Packages.props')
        (Join-Path $schemaDirectory 'PROVENANCE.md')
        (Join-Path $schemaDirectory 'COPYING')
        (Join-Path $schemaDirectory 'AUTHORS')
        (Join-Path $schemaDirectory 'INTENT.md')
    )
    $publicApiPath = Join-Path $schemaDirectory 'PublicApi.approved.sha256'
    if (Test-Path -LiteralPath $publicApiPath -PathType Leaf) {
        $paths += $publicApiPath
    }
    foreach ($baselineFile in @('BASELINE.json', 'COMPATIBILITY.md')) {
        $baselinePath = Join-Path $schemaDirectory $baselineFile
        if (Test-Path -LiteralPath $baselinePath -PathType Leaf) {
            $paths += $baselinePath
        }
    }

    $schemaPaths = @(Get-ChildItem -LiteralPath $schemaDirectory -Filter '*.exp' -File |
        Select-Object -ExpandProperty FullName)
    if ($schemaPaths.Count -ne 1) {
        throw "Expected exactly one EXPRESS source in $schemaDirectory; found $($schemaPaths.Count)."
    }

    $paths += $schemaPaths[0]
    if (Test-Path -LiteralPath $fixtureDirectory -PathType Container) {
        $paths += @(Get-ChildItem -LiteralPath $fixtureDirectory -File -Recurse |
            Select-Object -ExpandProperty FullName)
    }

    return @($paths |
        Sort-Object -Unique |
        ForEach-Object {
            if (-not (Test-Path -LiteralPath $_ -PathType Leaf)) {
                throw "Required reproducibility input is missing: $_"
            }

            $relativePath = [System.IO.Path]::GetRelativePath($repositoryRoot, $_).Replace('\', '/')
            $hash = if ([System.IO.Path]::GetExtension($_) -in @('.md', '.exp', '.csproj')) {
                Get-CanonicalTextSha256 $_
            }
            else {
                Get-FileSha256 $_
            }

            "$relativePath=$hash"
        })
}

function Invoke-BuildRound {
    param([Parameter(Mandatory = $true)][string] $Name)

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $roundRoot = Join-Path $proofRoot $Name
    $packageDirectory = Join-Path $roundRoot 'packages'
    New-Item -ItemType Directory -Path $packageDirectory | Out-Null
    Write-Host "$Schema reproducibility round '$Name': clean build and pack"

    Invoke-DotNet @(
        'clean', $packageProject,
        '--configuration', 'Release',
        '--disable-build-servers')
    Invoke-DotNet @(
        'build', $packageProject,
        '--configuration', 'Release',
        '--no-restore',
        '--no-incremental',
        '--disable-build-servers',
        '--maxcpucount:1',
        '--property:UseSharedCompilation=false',
        '--property:NuGetAudit=false')
    Invoke-DotNet @(
        'pack', $packageProject,
        '--configuration', 'Release',
        '--no-build', '--no-restore',
        '--output', $packageDirectory)

    $packagePath = @(Get-ChildItem -LiteralPath $packageDirectory -Filter "$packageId.*.nupkg" -File)
    if ($packagePath.Count -ne 1) {
        throw "Expected exactly one $packageId package in $packageDirectory; found $($packagePath.Count)."
    }

    $stopwatch.Stop()
    return [PSCustomObject]@{
        PackagePath = $packagePath[0].FullName
        PackageManifest = @(Get-NormalizedPackageManifest $packagePath[0].FullName)
        InputManifest = @(Get-InputManifest)
        Duration = $stopwatch.Elapsed
    }
}

if (-not (Test-Path -LiteralPath $packageProject -PathType Leaf)) {
    throw "Schema package project does not exist: $packageProject"
}

New-Item -ItemType Directory -Path $proofRoot | Out-Null
try {
    $first = Invoke-BuildRound 'first'
    $second = Invoke-BuildRound 'second'

    $packageDifference = Compare-Object $first.PackageManifest $second.PackageManifest
    if ($packageDifference) {
        throw "The two normalized package manifests differ:`n$($packageDifference | Out-String)"
    }

    $inputDifference = Compare-Object $first.InputManifest $second.InputManifest
    if ($inputDifference) {
        throw "The two source/provenance/API input manifests differ:`n$($inputDifference | Out-String)"
    }

    $packageManifestBytes = [System.Text.Encoding]::UTF8.GetBytes(
        ($first.PackageManifest -join "`n"))
    $packageDigest = [Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($packageManifestBytes))
    $inputManifestBytes = [System.Text.Encoding]::UTF8.GetBytes(
        ($first.InputManifest -join "`n"))
    $inputDigest = [Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($inputManifestBytes))

    Write-Host "$($Schema.ToUpperInvariant())_REPRODUCIBLE_PACKAGE_OK package-sha256=$packageDigest inputs-sha256=$inputDigest first=$($first.Duration) second=$($second.Duration)"
}
finally {
    $resolvedProofRoot = Resolve-Path -LiteralPath $proofRoot -ErrorAction SilentlyContinue
    $temporaryDirectory = [System.IO.Path]::GetTempPath().TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar)
    if ($resolvedProofRoot -and $resolvedProofRoot.Path.StartsWith(
        "$temporaryDirectory\TedToolkit.Step21.$Schema.Repro.",
        [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedProofRoot.Path -Recurse -Force
    }
}
