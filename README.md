# TedToolkit.Step21

TedToolkit.Step21 provides the grammar and .NET project foundation for parsing ISO 10303-21 STEP physical files and analyzing EXPRESS schemas with ANTLR.

[![Build](https://github.com/TedToolkit/TedToolkit.Step21/actions/workflows/build.yml/badge.svg)](https://github.com/TedToolkit/TedToolkit.Step21/actions/workflows/build.yml)

## What it provides

- ANTLR grammars for STEP physical files and EXPRESS schemas.
- Generated C# lexers, parsers, and visitors built with ANTLR 4.13.1.
- A Roslyn analyzer project packaged with the main library.
- A shared TedToolkit build pipeline and GitHub Actions workflow.

## Quick start

Clone the repository with its TedToolkit submodule, then build the solution in Release mode:

```shell
git clone --recurse-submodules https://github.com/TedToolkit/TedToolkit.Step21.git
cd TedToolkit.Step21
dotnet build TedToolkit.Step21.slnx --configuration Release
```

The projects currently target .NET 10 and .NET Standard 2.0, so the .NET 10 SDK is required to build the complete solution.

## Components

| Component | Responsibility |
| --- | --- |
| `src/TedToolkit.Step21` | Hosts the generated STEP parser and packages the analyzer. |
| `src/TedToolkit.Step21.Analyzer` | Hosts the generated EXPRESS parser and Roslyn analyzer foundation. |
| `src/grammar` | Contains the source grammars used to generate the parser code. |
| `build/TedToolkit.Step21.Build` | Runs the repository build pipeline. |

## Development

### Regenerate the parsers

Parser generation requires the .NET SDK and Java 11 or newer. The shell script also requires `curl`; the PowerShell script downloads ANTLR through `Invoke-WebRequest`.

```powershell
.\build\generate-antlr.ps1
```

```shell
./build/generate-antlr.sh
```

Both scripts read the centrally managed ANTLR version from `Directory.Packages.props` and replace the generated parser directories.

### Run the build pipeline

Create the ignored local settings file before using the repository build wrapper:

```powershell
Copy-Item build/TedToolkit.Step21.Build/appsettings.example.json build/TedToolkit.Step21.Build/appsettings.json
.\build.ps1
```

On Unix-like systems:

```shell
cp build/TedToolkit.Step21.Build/appsettings.example.json build/TedToolkit.Step21.Build/appsettings.json
./build.sh
```

## License

This project is licensed under the [GNU Lesser General Public License v3.0](COPYING.LESSER). See [COPYING](COPYING) for the full license text.
