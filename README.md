# TedToolkit.Step21

TedToolkit.Step21 is a .NET 10 library for reading, editing, validating, and writing ISO 10303-21 exchange structures against compile-time-generated EXPRESS schema types. It is schema-neutral: STEP application protocols and IFC are interoperability evidence, not alternate public models.

[![Build](https://github.com/TedToolkit/TedToolkit.Step21/actions/workflows/build.yml/badge.svg)](https://github.com/TedToolkit/TedToolkit.Step21/actions/workflows/build.yml)

## Start here

- [Package and API guide](src/TedToolkit.Step21/README.md) — install, add an EXPRESS schema, read or construct a model, validate, and write it.
- [Conformance and capability matrix](docs/conformance/README.md) — delivered syntax/operations, syntax-only facilities, exclusions, and their evidence.
- [Product intent](docs/product/README.md) — consumers, value, and non-goals.
- [Architecture overview](docs/architecture/README.md), [detailed architecture](docs/architecture/schema-bound-round-trip.md), and [design principles](docs/principles/README.md) — boundaries, dependency direction, and governance decisions.

## Repository map

| Path | Responsibility |
| --- | --- |
| `src/TedToolkit.Step21` | Public runtime, generated ISO 10303-21 parser, and packaged analyzer. |
| `src/TedToolkit.Step21.Ap203` | Optional precompiled AP203 Amendment 1 `CONFIG_CONTROL_DESIGN` package. |
| `src/TedToolkit.Step21.Analyzer` | EXPRESS parser, closed-set binder, and incremental generator. |
| `src/grammar` | Normative grammar sources. Part 21 uses separate lexer/parser grammars because lexer modes are lexer-grammar-only ANTLR features. |
| `tests/TedToolkit.Step21.Tests` | Fast, repository-owned TUnit conformance tests. |
| `tests/TedToolkit.Step21.IntegrationTests` | Package, documentation, and opt-in pinned-corpus tests. |
| `docs/conformance` | Evidence-backed capability records. |

## Build and verify

Clone with the TedToolkit submodule and build Release:

```shell
git clone --recurse-submodules https://github.com/TedToolkit/TedToolkit.Step21.git
cd TedToolkit.Step21
dotnet build TedToolkit.Step21.slnx --configuration Release
dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release
dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release
```

External NIST/buildingSMART corpus downloads are explicit opt-in. Their HTTPS source, license, byte size, and SHA-256 are pinned in `tests/TedToolkit.Step21.IntegrationTests/ExternalCorpus/manifest.json`; the verified cache is ignored under `artifacts/test-corpus/`.

```powershell
$env:TEDTOOLKIT_STEP21_EXTERNAL_CORPUS = '1'
dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release
```

The packed consumer and Native AOT deployment proof are documented in [the AOT/package record](docs/conformance/native-aot-package-proof.md). The executable proof target is `win-x64`; it is evidence, not an exclusive platform-support list.

## Regenerate parsers

Parser generation requires the .NET SDK and Java 11 or newer:

```powershell
.\build\generate-antlr.ps1
```

The script reads the central ANTLR version, replaces generated parser directories, retains visitors, omits listeners, internalizes generated types, and normalizes deterministic UTF-8/LF output.

## License

Licensed under the [GNU Lesser General Public License v3.0](COPYING.LESSER). See [COPYING](COPYING) for the full text.
