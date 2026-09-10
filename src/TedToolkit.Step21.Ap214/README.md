# TedToolkit.Step21.Ap214 package guide

TedToolkit.Step21.Ap214 provides the precompiled AP214 Edition 3 AIM long-form
`AUTOMOTIVE_DESIGN` descriptor and generated schema types for TedToolkit.Step21. Applications that
do not use AP214 continue to reference only `TedToolkit.Step21`.

## Install and use AP214

```shell
dotnet add package TedToolkit.Step21.Ap214 --version 1.0.0
```

Pass the generated descriptor explicitly to the schema-neutral Step21 runtime:

```csharp
using TedToolkit.Step21;
using TedToolkit.Step21.Schemas.AutomotiveDesign;
using Ap214SchemaDescriptor = TedToolkit.Step21.Schemas.AutomotiveDesign.SchemaDescriptor;

using var input = File.OpenText("model.stp");
var structure = ExchangeStructure.Read(input, [Ap214SchemaDescriptor.Instance]);

foreach (var product in structure.Entities.OfType<Product>())
{
    Console.WriteLine(product.Name);
}
```

Do not add `AP214E3_2010.exp`, another `AUTOMOTIVE_DESIGN` `.exp`, or an equivalent
`AdditionalFiles` item when using this package. The descriptor and generated schema surface are
already compiled into the assembly; supplying the same schema again creates duplicate public types.
Distinct custom EXPRESS schemas remain supported through the Analyzer workflow.

## Schema identity and provenance

- Package version: `1.0.0`.
- EXPRESS nominal name: `AUTOMOTIVE_DESIGN`.
- Generated descriptor: `TedToolkit.Step21.Schemas.AutomotiveDesign.SchemaDescriptor.Instance`.
- Baseline: STEPcode commit `9baa5dadaa1dcfcdc623220d865d36d61ea351e9`,
  `data/ap214e3/AP214E3_2010.exp`, identified as ISO/DIS 10303-214:2007 Edition 3.
- Canonical-LF SHA-256:
  `9516315F0A8CBB9A4F6598D92FCE36BEE5189A28D1ACEA1D87E2C411266211B7`.
- Redistribution evidence: the pinned STEPcode material is BSD-3-Clause. The package carries its
  `COPYING`, `AUTHORS`, `INTENT.md` and `PROVENANCE.md` under `third-party/stepcode/`; the source
  repository retains the same evidence in `schemas/ap214/`.

## Package boundary

The package targets .NET 10, contains generated schema code, and declares the tested
`TedToolkit.Step21` runtime range `[1.0.0,2.0.0)`. It does not add a reader/writer facade, registry,
reflection-based discovery, CAD-kernel conversion, or a second runtime. Consumers do not receive the
EXPRESS source, generator implementation, or Analyzer-only dependencies as runtime assets.

The fixed source and package version define the supported baseline. A matching nominal schema name
or retained OID alone does not prove edition compatibility, and the package does not claim complete
AP214 conformance.

## Version compatibility

The package follows SemVer independently from the core runtime. A Patch may change implementation,
documentation, or provenance only while descriptor identity, schema semantics, generated public
surface, and runtime range remain compatible. A different edition or vendor variant, a descriptor or
closed-schema change, a removed, renamed, retyped, reordered, newly required, or less nullable public
member, or incompatible validation/mapping behavior requires a Major version. Every release compares
the generated public surface and provenance with the approved repository baseline.
