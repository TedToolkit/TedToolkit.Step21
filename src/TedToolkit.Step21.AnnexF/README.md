# TedToolkit.Step21.AnnexF

This optional package implements the normative ECMAScript binding in ISO 10303-21:2016 Annex F.
It references `TedToolkit.Step21`, but neither the core package nor this adapter brings a JavaScript engine or a
general script-evaluation API into the application.

## Host boundary

Create an `AnnexFModelBridge` over the authoritative `ExchangeStructure`, then supply its JSON snapshot to the
packaged `AnnexF.js` source through the ECMAScript host selected by the application:

```csharp
var bridge = new AnnexFModelBridge(structure, new Part21Resource("https://example.test/model.step"));
var state = bridge.ExportState();
var source = AnnexFEcmaScriptModule.Source;
```

The script publishes a `P21` object. Construct `new P21.Model(host)` with a caller-owned object that provides:

- `snapshot()` — returns the parsed bridge state;
- `apply(state)` — accepts a complete mutated state and passes its JSON to `bridge.ApplyState(...)`;
- optional `resolveEntity`, `resolveValue`, `resolveConstantEntity`, `resolveConstantValue`, and `resolveURI`
  callbacks — return the application-specific encoding required by each Annex F `valueOf()` method;
- `generateVerification(population)` — returns a canonical Base64 message digest using caller-owned resource and
  digest capabilities when `P21.Population.set_verification()` is used.

The module implements `P21.Wrapper`, all F.3 wrapper subtypes, anchor `$value` and `$tag` properties, and all F.4
model/population methods. `ApplyState` parses and validates the entire snapshot before changing the shared model;
it cannot add, remove, rename, or reorder anchors.

The bridge format is versioned by `AnnexFEcmaScriptModule.BridgeFormatVersion`. The exact embedded UTF-8 source
identity is available as `AnnexFEcmaScriptModule.SourceSha256`.
