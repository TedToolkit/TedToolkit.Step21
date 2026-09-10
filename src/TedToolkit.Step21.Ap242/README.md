# TedToolkit.Step21.Ap242 package guide

TedToolkit.Step21.Ap242 provides precompiled types for the official ISO 10303-242:2025 AP242
Edition 4 / ISO/TS 10303-442 edition 7 MIM long form.

```csharp
using TedToolkit.Step21.Schemas.Ap242ManagedModelBased3dEngineeringMimLf;

var descriptor = SchemaDescriptor.Instance;
Console.WriteLine(descriptor.Name.Value);
```

## Schema identity

- Authority: ISO.
- Standard: [ISO 10303-242:2025](https://www.iso.org/standard/84300.html).
- Official EXP: [ISO/TS 10303-442 edition 7 MIM long form](https://standards.iso.org/iso/ts/10303/-442/ed-7/tech/express/mim_lf.exp).
- EXPRESS name: `AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF`.
- Source SHA-256: `E7E93CF97880FD87D634E4B9EE58400DA0A1BE6C06A1DE76EC13807ECDC15CCB`.

The build verifies those exact bytes, then uses the audited `mim_lf.compat.exp` generation input
(SHA-256 `00B6027C63671AAD36C943B7C65608773094CCDF87636B197EC634F30B306360`). The transform documents
and corrects two internal type contradictions: datum relationship projection and recursive 2D CSG
operand validation. It fails if the official hash, patch anchors, or transformed hash changes.

The EXP is downloaded explicitly into a Git-ignored local cache and used only during code
generation. It is not committed or packed. Run `pwsh -NoProfile -File
build/fetch-express-schemas.ps1 -AcknowledgeThirdPartyTerms` before a local build.

## Distribution boundary

The package ships `netstandard2.0` and `net8.0` runtime assets; later .NET consumers select the
`net8.0` asset.

The package remains a local verification artifact. The acknowledgement switch is not a licence;
the operator must establish applicable rights for download, local processing, and generation.
Distribution of the EXP is prohibited, and publication of generated or compiled output requires a
separate rights review as specified by ADR-0014 (which supersedes ADR-0013).
