# Annex F ECMAScript binding

`TedToolkit.Step21.AnnexF` is the optional ISO 10303-21:2016 Annex F boundary. It ships a deterministic
ECMAScript source artifact and `AnnexFModelBridge`; the core runtime has no script-engine dependency and never
evaluates script.

The machine-readable clause manifest is
[`annex-f-ecmascript-binding.json`](annex-f-ecmascript-binding.json). It covers the F.2 anchor properties, every
F.3 wrapper/value mapping, and the F.4 model and population methods. Integration proof executes the packaged
source in the locally installed Node.js ECMAScript engine, traces every manifest row to explicit assertions, applies
mutations through a transactional host boundary to the same `ExchangeStructure`, and writes the mutated Part 21
structure. The packaged source targets ECMAScript 5.1 or later and pins LF bytes for checkout-independent identity.

Application-specific encodings for entity, value, constant, and URI `valueOf()` results remain caller-owned, as
required by the Annex F materialization boundary. Resource bytes and schema-population digest generation also
remain explicit host capabilities; the module performs no implicit file, network, certificate, or time access.
Host application must return the canonical committed snapshot and throw on failure; attached setters retain their
prior observable state when application fails. Population URI or timestamp changes invalidate digest verification.
The bridge reuses the finite `Part21ProcessingLimits.Default` policy unless the caller supplies a stricter instance;
state/output characters, URI length, nested values, and total projected items are checked before model mutation.

The Annex F text assigns the identical `$value` spelling to both the required anchor value and a legal tag named
`value`. The binding resolves this otherwise unrepresentable collision explicitly: the `$value` collision object
delegates ordinary wrapper operations to the anchor value and exposes separate `anchorValue` and `tagValue` members.
F.4 method-name collisions use callable anchor objects so both the anchor property and required method remain usable.
