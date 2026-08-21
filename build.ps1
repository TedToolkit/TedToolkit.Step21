$project = Join-Path $PSScriptRoot 'build\TedToolkit.Step21.Build\TedToolkit.Step21.Build.csproj'

dotnet run --project $project --configuration Release
exit $LASTEXITCODE
