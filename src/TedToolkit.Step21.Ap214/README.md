# TedToolkit.Step21.Ap214 package guide

TedToolkit.Step21.Ap214 provides precompiled AP214 Edition 3 `AUTOMOTIVE_DESIGN` types for the
schema published by the MBx Interoperability Forum.

```csharp
using TedToolkit.Step21;
using TedToolkit.Step21.Schemas.AutomotiveDesign;

using var input = File.OpenText("model.stp");
var structure = ExchangeStructure.Read(input, [SchemaDescriptor.Instance]);
```

## Schema identity

- Authority: MBx Interoperability Forum—industry-authoritative, not ISO.
- ISO status: [ISO 10303-214:2010 is withdrawn](https://www.iso.org/standard/43669.html).
- Source index: [MBx-IF EXPRESS Schemas](https://www.mbx-if.org/home/mbx/resources/express-schemas/).
- EXPRESS name: `AUTOMOTIVE_DESIGN`.
- Source SHA-256: `71AB140FE7F774321BEEE6A31E6FEE2AFC3973FD60350AE2018C74C211FB4295`.

No current ISO-hosted AP214 EXP download was found. The exact fallback status is part of the package
provenance and must not be described as an official ISO publication.

The EXP is downloaded explicitly into a Git-ignored local cache and used only during code
generation. It is not committed or packed. Run `pwsh -NoProfile -File
build/fetch-express-schemas.ps1 -AcknowledgeThirdPartyTerms` before a local build.

## Distribution boundary

The package ships `netstandard2.0` and `net8.0` runtime assets; later .NET consumers select the
`net8.0` asset.

The package remains a local verification artifact. The acknowledgement switch is not a licence;
the operator must establish applicable rights for download, local processing, and generation.
Distribution of the EXP is prohibited, and publication of generated or compiled output requires a
separate rights review as specified by ADR-0013.
