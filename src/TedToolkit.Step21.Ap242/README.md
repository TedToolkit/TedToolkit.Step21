# TedToolkit.Step21.Ap242 package guide

This optional package provides precompiled types and an explicit descriptor for the fixed
N11521 / ISO/TS 10303-442 AP242 MIM long form. It uses the same schema-neutral Step21 runtime.

Version `1.0.0` is the first stable package contract. Repository builds do not imply publication to NuGet.org.

## Install and select the schema

After packing the candidate to a local feed, reference it from that feed:

```shell
dotnet add package TedToolkit.Step21.Ap242 --version 1.0.0 --source <local-feed>
```

```csharp
using TedToolkit.Step21.Schemas.Ap242ManagedModelBased3dEngineeringMimLf;

var descriptor = SchemaDescriptor.Instance;
Console.WriteLine(descriptor.Name.Value);
Console.WriteLine(typeof(Product).Assembly.GetName().Name);
```

The descriptor is passed explicitly to the existing `ExchangeStructure.Read` and `Write`
boundary. Generated entity interfaces, mutable entity classes, defined types, enumerations,
selects and aggregates reflect the pinned EXPRESS source; they are not a separate CAD model.
The [runtime guide](https://github.com/TedToolkit/TedToolkit.Step21/blob/main/src/TedToolkit.Step21/README.md)
owns model construction, navigation, validation, diagnostics and writing instructions.

## Build-input boundary

Do not include the AP242 MIM LF `.exp` or configure `AdditionalFiles` for
`AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF` alongside this precompiled package. These are
mutually exclusive ways to generate the same schema. There is no automatic duplicate-input
diagnostic, registry, assembly scanning, implicit descriptor selection or first-wins resolution.
Distinct custom EXPRESS schemas may continue to use the Analyzer workflow.

## Compatibility and provenance

- Target framework: `net10.0`.
- Runtime dependency: `TedToolkit.Step21` `[1.0.0,2.0.0)`; no dependency on AP203 or AP214.
- Fixed STEPcode revision: `9baa5dadaa1dcfcdc623220d865d36d61ea351e9`.
- Source: `data/ap242/242_mim_lf.exp`, N11521, superseding N11273.
- Upstream source SHA-256: `E7E93CF97880FD87D634E4B9EE58400DA0A1BE6C06A1DE76EC13807ECDC15CCB`.
- Exact patched build-input SHA-256: `221222ED7F92873D8A1BBDDAE569ED72C87730E09F56226A92E3F108FC9EB7A0`;
  the two type-correcting rewrites are recorded in the packaged provenance.
- Source attribution and provenance are included under `third-party/stepcode/` in the package.

The package is independently versioned. Baseline, generated public API, schema semantics and runtime
range changes follow [ADR-0006](https://github.com/TedToolkit/TedToolkit.Step21/blob/main/docs/adr/ADR-0006-precompiled-schema-package-distribution.md).
An OID is retained exchange data, not package identity or evidence that different editions are equivalent.

No full AP242 conformance is claimed. The fixed OCCT 7.9.3 AP242DIS manifold B-rep fixture proves
typed read/edit/validate/write/reread for one 10 × 20 × 30 mm box while preserving its 170-entity
value/reference graph. PMI, tessellation, kinematics, colour and layer interoperability are not
claimed. The package does not add a CAD-kernel adapter or another parser, model, validator or writer.
