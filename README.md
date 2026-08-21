# TedToolkit.Step21

TedToolkit.Step21 is a .NET library focused on ISO 10303-21 exchange structures and their EXPRESS-defined schemas. The current repository provides the parser and generator foundation; its product boundary is ISO 10303-21 itself, not a particular application protocol or domain-specific format such as IFC.

[![Build](https://github.com/TedToolkit/TedToolkit.Step21/actions/workflows/build.yml/badge.svg)](https://github.com/TedToolkit/TedToolkit.Step21/actions/workflows/build.yml)

## What it provides

- ANTLR grammars for ISO 10303-21 exchange structures and EXPRESS schemas.
- Generated C# lexers, parsers, and visitors built with ANTLR 4.13.1.
- A Roslyn analyzer project packaged with the main library.
- A shared TedToolkit build pipeline and GitHub Actions workflow.

STEP application protocols and IFC files are useful conformance fixtures, but they do not define the library's public model or supported scope. See the [product intent](docs/product/README.md), [design principles](docs/principles/README.md), and [current architecture draft](docs/architecture/schema-bound-round-trip.md).

The Part 21 parser's `exchangeFile` entry rule recognizes the complete ISO 10303-21:2016 Edition 3 clear-text section and token syntax, enforces the normative section order, and consumes EOF. This recognition does not yet provide operational anchor/reference resolution, signature verification, archive handling, ECMAScript execution, or schema-bound materialization. See the [production-to-clause traceability](docs/conformance/part21-edition3-grammar.md) for the exact syntax boundary and fixtures.

The Analyzer's `syntax` entry rule recognizes ISO 10303-11:2004 Edition 2 EXPRESS and transforms complete input into internal immutable, source-located IR without executing declarations. Supplied schema texts then bind as one deterministic closed universe without external lookup. See the [EXPRESS grammar boundary](docs/conformance/express-edition2-grammar.md) and [closed-set binding boundary](docs/conformance/express-closed-set-binding.md).

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
| `src/TedToolkit.Step21` | Hosts the generated ISO 10303-21 parser and packages the analyzer. |
| `src/TedToolkit.Step21.Analyzer` | Hosts the generated EXPRESS parser and Roslyn analyzer foundation. |
| `src/grammar` | Contains the source grammars used to generate the parsers; Part 21 uses a split lexer/parser so signature Base64 is context-bound without target-language actions. |
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
They normalize generated files to UTF-8 with LF line endings, retain visitors, omit listeners, and
internalize generated top-level types. For isolated verification, pass an output root with
`-OutputRoot <path>` to the PowerShell script or as the first argument to the shell script.

### Run the parser tests

Fast TUnit tests use small repository-owned STEP, IFC, and EXPRESS fixtures and require no network access:

```powershell
dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release
```

The integration project always validates the external-corpus manifest and cache policy. Its network test is skipped unless explicitly enabled:

```powershell
$env:TEDTOOLKIT_STEP21_EXTERNAL_CORPUS = '1'
dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release
```

```shell
TEDTOOLKIT_STEP21_EXTERNAL_CORPUS=1 \
  dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release
```

External NIST and buildingSMART artifacts are declared in
`tests/TedToolkit.Step21.IntegrationTests/ExternalCorpus/manifest.json`. The manifest pins each
download by byte size and SHA-256 and records its source and license. Verified files are cached in
the Git-ignored `artifacts/test-corpus/` directory; they are never committed to the repository.

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
