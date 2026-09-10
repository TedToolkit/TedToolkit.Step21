[CmdletBinding()]
param(
    [ValidateSet('Fetch', 'Verify')]
    [string] $Mode = 'Fetch',

    [string] $CacheRoot,

    [switch] $AcknowledgeThirdPartyTerms,

    [ValidateSet('all', 'ap203', 'ap214', 'ap242')]
    [string] $Schema = 'all'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repositoryRoot 'schemas/SOURCES.json'
if ([string]::IsNullOrWhiteSpace($CacheRoot)) {
    $CacheRoot = Join-Path $repositoryRoot 'schemas/.cache'
}
else {
    $CacheRoot = [IO.Path]::GetFullPath($CacheRoot)
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

if ($Mode -eq 'Fetch' -and -not $AcknowledgeThirdPartyTerms) {
    throw @"
Fetching these third-party publications requires an explicit acknowledgement.
Review schemas/SOURCES.md and the linked publisher terms, then rerun with:
  pwsh -NoProfile -File build/fetch-express-schemas.ps1 -AcknowledgeThirdPartyTerms
The switch records acknowledgement only; it does not grant any licence rights.
"@
}

function Get-Sha256([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Assert-ExpectedHash([string] $Path, [string] $ExpectedHash, [string] $SchemaId) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Missing $SchemaId EXPRESS input: $Path. Run: pwsh -NoProfile -File build/fetch-express-schemas.ps1"
    }

    $actualHash = Get-Sha256 $Path
    if ($actualHash -ne $ExpectedHash) {
        throw "$SchemaId EXPRESS input failed SHA-256 verification. Expected $ExpectedHash, got ${actualHash}: $Path"
    }
}

function Test-HasProperty($Value, [string] $Name) {
    return $Value.PSObject.Properties.Name -contains $Name
}

function Prepare-GenerationInput($SchemaEntry, [string] $SourcePath) {
    if (-not (Test-HasProperty $SchemaEntry 'generationInputPath')) {
        return
    }

    $generationInputPath = Join-Path $CacheRoot $SchemaEntry.generationInputPath
    $transformPath = Join-Path $repositoryRoot $SchemaEntry.compatibilityTransform
    & $transformPath `
        -SourcePath $SourcePath `
        -OutputPath $generationInputPath `
        -ExpectedSourceHash $SchemaEntry.sha256 `
        -ExpectedOutputHash $SchemaEntry.generationInputSha256
    Assert-ExpectedHash `
        $generationInputPath `
        $SchemaEntry.generationInputSha256 `
        "$($SchemaEntry.id) prepared generation"
}

foreach ($schemaEntry in $manifest.schemas) {
    if ($Schema -ne 'all' -and $schemaEntry.id -ne $Schema) {
        continue
    }

    $targetPath = Join-Path $CacheRoot $schemaEntry.cachePath
    if ($Mode -eq 'Verify') {
        Assert-ExpectedHash $targetPath $schemaEntry.sha256 $schemaEntry.id
        Write-Output "Verified $($schemaEntry.id): $($schemaEntry.sha256)"
        if (Test-HasProperty $schemaEntry 'generationInputPath') {
            $generationInputPath = Join-Path $CacheRoot $schemaEntry.generationInputPath
            Assert-ExpectedHash `
                $generationInputPath `
                $schemaEntry.generationInputSha256 `
                "$($schemaEntry.id) prepared generation"
            Write-Output "Verified $($schemaEntry.id) generation input: $($schemaEntry.generationInputSha256)"
        }

        continue
    }

    $requiresDownload = $true
    if (Test-Path -LiteralPath $targetPath -PathType Leaf) {
        $existingHash = Get-Sha256 $targetPath
        if ($existingHash -eq $schemaEntry.sha256) {
            Write-Output "Already verified $($schemaEntry.id): $($schemaEntry.sha256)"
            $requiresDownload = $false
        }
    }

    if ($requiresDownload) {
        $targetDirectory = Split-Path -Parent $targetPath
        [IO.Directory]::CreateDirectory($targetDirectory) | Out-Null
        $operationId = [Guid]::NewGuid().ToString('N')
        $downloadPath = Join-Path ([IO.Path]::GetTempPath()) "TedToolkit.Step21-$($schemaEntry.id)-$operationId.download"
        $candidatePath = "$targetPath.$operationId.candidate"

        try {
            Invoke-WebRequest -Uri $schemaEntry.downloadUrl -OutFile $downloadPath
            if ($null -ne $schemaEntry.archiveEntry) {
                Add-Type -AssemblyName System.IO.Compression.FileSystem
                $archive = [IO.Compression.ZipFile]::OpenRead($downloadPath)
                try {
                    $entry = $archive.GetEntry([string] $schemaEntry.archiveEntry)
                    if ($null -eq $entry) {
                        throw "Archive entry not found for $($schemaEntry.id): $($schemaEntry.archiveEntry)"
                    }

                    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $candidatePath, $true)
                }
                finally {
                    $archive.Dispose()
                }
            }
            else {
                [IO.File]::Copy($downloadPath, $candidatePath, $true)
            }

            Assert-ExpectedHash $candidatePath $schemaEntry.sha256 $schemaEntry.id
            [IO.File]::Move($candidatePath, $targetPath, $true)
            Write-Output "Fetched and verified $($schemaEntry.id): $($schemaEntry.sha256)"
        }
        finally {
            Remove-Item -LiteralPath $downloadPath -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $candidatePath -Force -ErrorAction SilentlyContinue
        }
    }

    Prepare-GenerationInput $schemaEntry $targetPath
}
