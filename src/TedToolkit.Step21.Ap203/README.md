# TedToolkit.Step21.Ap203 package guide

TedToolkit.Step21.Ap203 `2.0.0` provides precompiled types for the official ISO/TS 10303-403 AP203
MIM long form. This is an intentional breaking replacement for the former Amendment 1
`CONFIG_CONTROL_DESIGN` package contract.

```csharp
using TedToolkit.Step21;
using TedToolkit.Step21.Schemas.Ap203ConfigurationControlled3dDesignOfMechanicalPartsAndAssembliesMimLf;

using var input = File.OpenText("model.stp");
var structure = ExchangeStructure.Read(input, [SchemaDescriptor.Instance]);
foreach (var product in structure.Entities.OfType<Product>())
{
    Console.WriteLine(product.Name);
}
```

## Schema identity

- Authority: ISO.
- Standard: [ISO/TS 10303-403:2010](https://www.iso.org/standard/56238.html).
- EXPRESS name: `AP203_CONFIGURATION_CONTROLLED_3D_DESIGN_OF_MECHANICAL_PARTS_AND_ASSEMBLIES_MIM_LF`.
- Source SHA-256: `255EAFFD5984373F5FE2F41369088B6FD07F970EB5915CE9920F0A5F339DDD44`.
- Full download and archive identity: [`schemas/SOURCES.md`](../../schemas/SOURCES.md).

The EXP is downloaded explicitly into a Git-ignored local cache and used only during code
generation. It is not committed or packed. Run `pwsh -NoProfile -File
build/fetch-express-schemas.ps1 -AcknowledgeThirdPartyTerms` before a local build.

## Compatibility and distribution

The package ships `netstandard2.0` and `net8.0` runtime assets; later .NET consumers select the
`net8.0` asset. Its supported `TedToolkit.Step21` runtime range is `[1.0.0,2.0.0)`.

The old `CONFIG_CONTROL_DESIGN` descriptor and generated API are not compatibility aliases for this
schema. A Part 21 file declaring that former schema must use a matching descriptor/package or fail
the schema boundary explicitly.

The package remains a local verification artifact. The acknowledgement switch is not a licence;
the operator must establish applicable rights for download, local processing, and generation.
Distribution of the EXP is prohibited, and publication of generated or compiled output requires a
separate rights review as specified by ADR-0014 (which supersedes ADR-0013).
