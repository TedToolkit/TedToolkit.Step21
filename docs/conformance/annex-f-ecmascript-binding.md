# Annex F ECMAScript binding

`TedToolkit.Step21.AnnexF` is the optional ISO 10303-21:2016 Annex F boundary. It ships a deterministic
ECMAScript source artifact and `AnnexFModelBridge`; the core runtime has no script-engine dependency and never
evaluates script.

The machine-readable clause manifest is
[`annex-f-ecmascript-binding.json`](annex-f-ecmascript-binding.json). It covers the F.2 anchor properties, every
F.3 wrapper/value mapping, and the F.4 model and population methods. Integration proof executes the packaged
source in the locally installed Node.js ECMAScript engine, exercises all manifest rows, applies mutations to the
same `ExchangeStructure`, and writes the mutated Part 21 structure.

Application-specific encodings for entity, value, constant, and URI `valueOf()` results remain caller-owned, as
required by the Annex F materialization boundary. Resource bytes and schema-population digest generation also
remain explicit host capabilities; the module performs no implicit file, network, certificate, or time access.
