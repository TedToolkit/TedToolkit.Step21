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
using TedToolkit.Step21.Generated.ConfigControlDesign;
using Ap203SchemaDescriptor = TedToolkit.Step21.Generated.ConfigControlDesign.SchemaDescriptor;

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

- EXPRESS nominal name: `config_control_design`.
- Baseline: STEPcode commit `9baa5dadaa1dcfcdc623220d865d36d61ea351e9`,
  `data/ap203/ap203.exp`, described upstream as the AP203 Amendment 1 AIM long form with
  non-semantic modifications.
- Corrected canonical-LF SHA-256:
  `19497DCA88C6FCFE763DA23772B68356BE4361668426954DE9863E4285D0C251`.
- Redistribution evidence: the pinned STEPcode material is BSD-3-Clause; the repository records the
  copied license and the two syntax-only corrections in `schemas/ap203/PROVENANCE.md`.

## Package boundary

The package contains generated schema code and depends on the compatible `TedToolkit.Step21`
runtime. It does not add an AP203 reader/writer facade, registry, reflection-based discovery, CAD
kernel conversion, JSON/XML serialization, or a second parser/writer. Consumers do not receive the
EXPRESS source, generator implementation, or RoslynHelper as runtime assets.

The package targets .NET 10. AP214, AP242, AP203 Edition 2, and schema extensions absent from the
pinned long form are outside this package's supported boundary.
