[CmdletBinding()]
param(
    [Parameter()]
    [string] $OutputRoot
)

$ErrorActionPreference = 'Stop'

$scriptDirectory = $PSScriptRoot
$repositoryRoot = Split-Path -Parent $scriptDirectory
$generationRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $repositoryRoot
}
else {
    [System.IO.Path]::GetFullPath($OutputRoot)
}
$toolsDirectory = Join-Path $scriptDirectory 'tools'
$versionOutputFile = Join-Path $toolsDirectory 'antlr-version.txt'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK is required to read the centrally managed ANTLR version.'
}

New-Item -ItemType Directory -Path $toolsDirectory -Force | Out-Null
& dotnet msbuild (Join-Path $scriptDirectory 'GetPackageVersion.proj') `
    -nologo `
    -verbosity:quiet `
    -target:GetPackageVersion `
    -property:PackageId=Antlr4.Runtime.Standard `
    "-property:VersionOutputFile=$versionOutputFile"
if ($LASTEXITCODE -ne 0) {
    throw "Failed to read Antlr4.Runtime.Standard from Directory.Packages.props (exit code $LASTEXITCODE)."
}

$antlrVersion = (Get-Content -Raw -LiteralPath $versionOutputFile).Trim()
if ([string]::IsNullOrWhiteSpace($antlrVersion)) {
    throw 'Antlr4.Runtime.Standard has an empty centrally managed version.'
}

$antlrJar = Join-Path $scriptDirectory "tools/antlr-$antlrVersion-complete.jar"

if (-not (Get-Command java -ErrorAction SilentlyContinue)) {
    throw 'Java 11 or newer is required to run ANTLR.'
}

if (-not (Test-Path -LiteralPath $antlrJar)) {
    Invoke-WebRequest -Uri "https://www.antlr.org/download/antlr-$antlrVersion-complete.jar" -OutFile $antlrJar
}

$grammarDirectory = Join-Path $repositoryRoot 'src/grammar'
$stepOutput = Join-Path $generationRoot 'src/TedToolkit.Step21/Generated/STEP'
$expressOutput = Join-Path $generationRoot 'src/TedToolkit.Step21.Analyzer/Generated/Express'

foreach ($outputDirectory in @($stepOutput, $expressOutput)) {
    if (Test-Path -LiteralPath $outputDirectory) {
        Remove-Item -LiteralPath $outputDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

Push-Location $grammarDirectory
try {
    & java -jar $antlrJar -Dlanguage=CSharp -visitor -no-listener -package TedToolkit.Step21.Grammar -o $stepOutput STEPLexer.g4 STEPParser.g4
    if ($LASTEXITCODE -ne 0) {
        throw "ANTLR failed to generate the STEP lexer/parser grammars (exit code $LASTEXITCODE)."
    }

    & java -jar $antlrJar -Dlanguage=CSharp -visitor -no-listener -package TedToolkit.Step21.Analyzer.Grammar -o $expressOutput Express.g4
    if ($LASTEXITCODE -ne 0) {
        throw "ANTLR failed to generate Express.g4 (exit code $LASTEXITCODE)."
    }

    $publicTypePattern = '(?m)^\[System\.CLSCompliant\(false\)\]\r?\npublic (?=(?:partial class|interface)\s)'
    foreach ($generatedFile in Get-ChildItem -LiteralPath $stepOutput, $expressOutput -File) {
        $content = [System.IO.File]::ReadAllText($generatedFile.FullName)
        if ($generatedFile.Extension -ceq '.cs') {
            $rewrittenContent = $content -replace $publicTypePattern, 'internal '
            if ($rewrittenContent -ceq $content) {
                throw "ANTLR generated no public top-level type in '$($generatedFile.FullName)'."
            }

            $content = $rewrittenContent -replace '[\t ]+(?=\r?\n|$)', ''
        }

        $content = $content -replace "\r\n?", "`n"
        [System.IO.File]::WriteAllText(
            $generatedFile.FullName,
            $content,
            [System.Text.UTF8Encoding]::new($false))
    }
}
finally {
    Pop-Location
}
