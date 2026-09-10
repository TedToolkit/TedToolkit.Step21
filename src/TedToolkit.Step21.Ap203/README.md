# TedToolkit.Step21.Ap203 package guide

TedToolkit.Step21.Ap203 provides the precompiled AP203 Amendment 1 AIM long-form
`CONFIG_CONTROL_DESIGN` descriptor and generated schema types for TedToolkit.Step21. The package is
optional: applications that do not use AP203 continue to reference only `TedToolkit.Step21`.

## Install and use AP203

```shell
dotnet add package TedToolkit.Step21.Ap203
```

Pass the generated descriptor explicitly to the schema-neutral Step21 runtime:

```csharp
using TedToolkit.Step21;
using TedToolkit.Step21.Schemas.ConfigControlDesign;
using Ap203SchemaDescriptor = TedToolkit.Step21.Schemas.ConfigControlDesign.SchemaDescriptor;

using var input = File.OpenText("model.stp");
var structure = ExchangeStructure.Read(input, [Ap203SchemaDescriptor.Instance]);

foreach (var product in structure.Entities.OfType<Product>())
{
    Console.WriteLine(product.Name);
}
```

Do not add the AP203 `.exp` file as an `AdditionalFiles` item when using this package. Its descriptor,
entities, defined types, enumerations, selects, and aggregate projections are already compiled into
the assembly; supplying the same schema again would generate duplicate public types.

## Schema identity and provenance

- Package version: `1.0.0`.
- EXPRESS nominal name: `config_control_design`.
- Generated descriptor: `TedToolkit.Step21.Schemas.ConfigControlDesign.SchemaDescriptor.Instance`.
- Baseline: STEPcode commit `9baa5dadaa1dcfcdc623220d865d36d61ea351e9`,
  `data/ap203/ap203.exp`, described upstream as the AP203 Amendment 1 AIM long form with
  non-semantic modifications.
- Corrected canonical-LF SHA-256:
  `19497DCA88C6FCFE763DA23772B68356BE4361668426954DE9863E4285D0C251`.
- Redistribution evidence: the pinned STEPcode material is BSD-3-Clause; the repository records the
  copied license and the two syntax-only corrections in `schemas/ap203/PROVENANCE.md`.
- Interoperability fixture: OCCT commit `7d2efad9c8a9a57ea96c4c8587134b34dd503cd8`, AP203 mode,
  fixed 10 x 20 x 30 mm box/product. Its checked-in STEP file SHA-256 is
  `2F40CE06A8646B3AE33A8BD871181A356D413CDD6B864D9C8D484A3D1E127B62`.

## Package boundary

The package contains generated schema code and declares the tested `TedToolkit.Step21` runtime
range `[1.0.0,2.0.0)`. It does not add an AP203 reader/writer facade, registry, reflection-based discovery, CAD
kernel conversion, JSON/XML serialization, or a second parser/writer. Consumers do not receive the
EXPRESS source, generator implementation, or RoslynHelper as runtime assets.

The package ships `netstandard2.0` and `net8.0` runtime assets. Later .NET consumers select the
`net8.0` asset. AP214, AP242, AP203 Edition 2, and schema extensions absent from the pinned long
form are outside this package's supported boundary. The checked-in OCCT extension
fixture records this boundary and must fail explicitly; the OCCT sample is interoperability evidence,
not a claim of complete AP203 conformance.

## Version compatibility

The package follows SemVer independently from the core runtime. A Patch may fix behavior,
documentation, or provenance only when descriptor identity, declared schema semantics, generated
public surface, and the supported runtime range remain compatible. A Minor may add backward-compatible
generated API within this same AP203 Amendment 1 baseline. A different edition or vendor variant,
a descriptor/closed-schema change, a removed, renamed, retyped, reordered, newly required, or less
nullable generated member, incompatible validation or mapping behavior, or a narrowed runtime range
requires a Major version.

Every release compares the generated public surface with the approved repository baseline. Package
metadata, this provenance record, descriptor identity, dependency range, and that comparison must
agree before a release is treated as compatible. Widening the runtime range requires package-consumer
and Native AOT evidence; it does not couple the AP203 package's release cadence to the core package.
