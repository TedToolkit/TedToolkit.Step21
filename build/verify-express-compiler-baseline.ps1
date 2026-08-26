[CmdletBinding()]
param()

$baselineRoot = Split-Path -Parent $PSScriptRoot
$baselineManifestPath = Join-Path $baselineRoot 'tests/TedToolkit.Step21.Tests/TestData/Express/Baseline/compiler-baseline.approved.json'
$baselineHashPath = Join-Path $baselineRoot 'tests/TedToolkit.Step21.Tests/TestData/Express/Baseline/compiler-baseline.approved.sha256'
$baselineProjectPath = Join-Path $baselineRoot 'tests/TedToolkit.Step21.Tests/TedToolkit.Step21.Tests.csproj'
$baselinePackagesPath = Join-Path $baselineRoot 'Directory.Packages.props'

$baselineManifest = Get-Content -LiteralPath $baselineManifestPath -Raw -ErrorAction Stop | ConvertFrom-Json
$expectedManifestHash = (Get-Content -LiteralPath $baselineHashPath -Raw -ErrorAction Stop).Trim()
$actualManifestHash = (Get-FileHash -LiteralPath $baselineManifestPath -Algorithm SHA256 -ErrorAction Stop).Hash
if ($actualManifestHash -cne $expectedManifestHash) {
    throw "Compiler baseline manifest SHA-256 mismatch. Expected $expectedManifestHash; actual $actualManifestHash."
}

$actualSdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet --version failed.'
}

if ($actualSdkVersion -cne $baselineManifest.toolchain.dotNetSdk) {
    throw "Compiler baseline requires .NET SDK $($baselineManifest.toolchain.dotNetSdk); actual $actualSdkVersion."
}

[xml]$baselinePackages = Get-Content -LiteralPath $baselinePackagesPath -Raw -ErrorAction Stop
$actualPackages = @{}
foreach ($package in $baselinePackages.Project.ItemGroup.PackageVersion) {
    $actualPackages[$package.Include] = $package.Version
}

foreach ($expectedPackage in $baselineManifest.toolchain.packages.PSObject.Properties) {
    if (-not $actualPackages.ContainsKey($expectedPackage.Name)) {
        throw "Compiler baseline package '$($expectedPackage.Name)' is missing from Directory.Packages.props."
    }

    if ($actualPackages[$expectedPackage.Name] -cne $expectedPackage.Value) {
        throw "Compiler baseline package '$($expectedPackage.Name)' requires $($expectedPackage.Value); actual $($actualPackages[$expectedPackage.Name])."
    }
}

$baselineRevision = $baselineManifest.baselineRevision
$baselineCommitExpression = '{0}^{{commit}}' -f $baselineRevision
& git -C $baselineRoot rev-parse --verify $baselineCommitExpression *> $null
if ($LASTEXITCODE -ne 0) {
    throw "Compiler baseline revision '$baselineRevision' is not available in this checkout."
}

& git -C $baselineRoot merge-base --is-ancestor $baselineRevision HEAD
if ($LASTEXITCODE -ne 0) {
    throw "Compiler baseline revision '$baselineRevision' is not an ancestor of HEAD."
}

Write-Host "Compiler baseline revision: $baselineRevision"
Write-Host "Compiler baseline manifest SHA-256: $actualManifestHash"
Write-Host "Compiler baseline toolchain: SDK $actualSdkVersion; Roslyn $($baselineManifest.toolchain.roslynAssembly); generator $($baselineManifest.toolchain.generatorAssembly)"

& dotnet restore $baselineProjectPath --ignore-failed-sources
if ($LASTEXITCODE -ne 0) {
    throw 'Compiler baseline restore failed.'
}

& dotnet build $baselineProjectPath --configuration Release --no-restore --no-incremental
if ($LASTEXITCODE -ne 0) {
    throw 'Compiler baseline build failed.'
}

& dotnet run --project $baselineProjectPath --configuration Release --no-restore --no-build -- --treenode-filter '/*/*/*/Should_match_approved_generated_source_diagnostic_and_withholding_manifest'
if ($LASTEXITCODE -ne 0) {
    throw 'Compiler baseline verification failed.'
}
