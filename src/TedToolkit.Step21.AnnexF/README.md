# TedToolkit.Step21.AnnexF

This optional package implements the normative ECMAScript binding in ISO 10303-21:2016 Annex F.
It references `TedToolkit.Step21`, but neither the core package nor this adapter brings a JavaScript engine or a
general script-evaluation API into the application.

## Host boundary

Create an `AnnexFModelBridge` over the authoritative `ExchangeStructure`, then supply its JSON snapshot to the
packaged `AnnexF.js` source through the ECMAScript 5.1-or-later host selected by the application:

```csharp
var bridge = new AnnexFModelBridge(structure, new Part21Resource("https://example.test/model.step"));
var state = bridge.ExportState();
var source = AnnexFEcmaScriptModule.Source;
```

The two-argument constructor reuses `Part21ProcessingLimits.Default`. Pass a third explicit limits argument to bound
bridge input/output characters, URI length, nested values, and total anchors/tags/populations/values. Limit failures
leave the authoritative structure unchanged.

The script publishes a `P21` object. Construct `new P21.Model(host)` with a caller-owned object that provides:

- `snapshot()` — returns the parsed bridge state;
- `apply(state)` — transactionally passes the complete candidate JSON to `bridge.ApplyState(...)`, then returns
  `JSON.parse(bridge.ExportState())` as the canonical committed state; it must throw without changing the authoritative
  model when application fails;
- optional `resolveEntity`, `resolveValue`, `resolveConstantEntity`, `resolveConstantValue`, and `resolveURI`
  callbacks — return the application-specific encoding required by each Annex F `valueOf()` method;
- `generateVerification(population)` — returns a canonical Base64 message digest using caller-owned resource and
  digest capabilities when `P21.Population.set_verification()` is used.

The module implements `P21.Wrapper`, all F.3 wrapper subtypes, anchor `$value` and `$tag` properties, and all F.4
model/population methods. `ApplyState` parses and validates the entire snapshot before changing the shared model;
it cannot add, remove, rename, or reorder anchors. A mutable `P21.Model` therefore requires both `snapshot()` and
transactional `apply()`; every attached setter stages its change and adopts host-returned state only after success.
Changing a population URI or timestamp invalidates its digest verification until `set_verification()` succeeds again.

Annex F maps both the anchor value and a legal tag named `value` to the same ECMAScript property spelling `$value`.
Because one object cannot hold two distinct properties with the same name, this otherwise ambiguous case returns a
collision object: its normal `valueOf()`, `toString()`, and `toP21String()` operations describe the anchor value, while
`anchorValue` and `tagValue` expose and mutate both values explicitly. An anchor whose name is also an F.4 method is a
callable anchor object, so `model.uri()` and `model.uri.$value` remain simultaneously available.

The bridge format is versioned by `AnnexFEcmaScriptModule.BridgeFormatVersion`. The exact embedded UTF-8 source
identity is available as `AnnexFEcmaScriptModule.SourceSha256`.
