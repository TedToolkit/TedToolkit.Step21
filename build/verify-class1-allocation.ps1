param(
    [string]$Baseline = "63b2757",
    [int]$Iterations = 20
)

$ErrorActionPreference = "Stop"
if ($Iterations -le 0) {
    throw "Iterations must be positive."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$probeRelativePath = "build/TedToolkit.Step21.Class1AllocationProbe/TedToolkit.Step21.Class1AllocationProbe.csproj"
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("TedToolkit-Step21-allocation-" + [Guid]::NewGuid().ToString("N"))
$baselineRoot = Join-Path $temporaryRoot "baseline"
$archivePath = Join-Path $temporaryRoot "baseline.zip"

function Invoke-Checked([string]$command, [string[]]$arguments) {
    & $command @arguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $command $($arguments -join ' ')"
    }
}

function Invoke-Probe([string]$root) {
    $project = Join-Path $root $probeRelativePath
    Invoke-Checked "dotnet" @("restore", $project, "--ignore-failed-sources", "--nologo")
    Invoke-Checked "dotnet" @("build", $project, "--configuration", "Release", "--no-restore", "--nologo", "--disable-build-servers")
    $assembly = Join-Path (Split-Path -Parent $project) "bin/Release/net10.0/TedToolkit.Step21.Class1AllocationProbe.dll"
    $json = & dotnet $assembly --iterations $Iterations
    if ($LASTEXITCODE -ne 0) {
        throw "Allocation probe failed with exit code $LASTEXITCODE."
    }

    return $json | Select-Object -Last 1 | ConvertFrom-Json
}

New-Item -ItemType Directory -Path $baselineRoot -Force | Out-Null
try {
    Invoke-Checked "git" @("-C", $repositoryRoot, "archive", "--format=zip", "--output=$archivePath", $Baseline)
    Expand-Archive -LiteralPath $archivePath -DestinationPath $baselineRoot

    $baselineProbeRoot = Join-Path $baselineRoot "build/TedToolkit.Step21.Class1AllocationProbe"
    New-Item -ItemType Directory -Path $baselineProbeRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $probeRelativePath) -Destination $baselineProbeRoot
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "build/TedToolkit.Step21.Class1AllocationProbe/Program.cs") -Destination $baselineProbeRoot

    $baselineResult = Invoke-Probe $baselineRoot
    $candidateResult = Invoke-Probe $repositoryRoot
    $baselineBytes = [long]$baselineResult.medianAllocatedBytes
    $candidateBytes = [long]$candidateResult.medianAllocatedBytes
    $maximumBytes = [Math]::Max([long][Math]::Ceiling($baselineBytes * 1.05), $baselineBytes + 16384L)

    if ($candidateBytes -gt $maximumBytes) {
        throw "Class-1 allocation regression: baseline=$baselineBytes candidate=$candidateBytes maximum=$maximumBytes."
    }

    Write-Output "CLASS1_ALLOCATION_OK baseline=$baselineBytes candidate=$candidateBytes maximum=$maximumBytes iterations=$Iterations baselineRevision=$Baseline"
}
finally {
    $resolvedTemporaryRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
    $resolvedSystemTemporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $temporaryLeaf = Split-Path -Leaf $resolvedTemporaryRoot
    $isSystemTemporaryChild = $resolvedTemporaryRoot.StartsWith(
        $resolvedSystemTemporaryRoot,
        [StringComparison]::OrdinalIgnoreCase)
    $isOwnedTemporaryDirectory = $temporaryLeaf.StartsWith(
        "TedToolkit-Step21-allocation-",
        [StringComparison]::Ordinal)
    if ($isSystemTemporaryChild -and $isOwnedTemporaryDirectory) {
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
