# Architecture overview

This page is the maintainer-oriented map of TedToolkit.Step21. The active detailed record remains
[`schema-bound-round-trip.md`](schema-bound-round-trip.md); this overview names boundaries and
invariants without duplicating their rationale.

## Purpose and system boundary

TedToolkit.Step21 turns one or more EXPRESS schemas into strongly typed C# at compile time, then
uses those generated contracts to read, edit, validate, and canonically write schema-bound ISO
10303-21 exchange structures at runtime. The core remains schema-neutral: STEP application
protocols, IFC schemas, and future maintained schema packages are consumers of the same runtime,
not alternate core models.

## Two execution stages

```text
Compile time
  EXPRESS AdditionalFiles
    -> EXPRESS syntax -> closed-set binding -> immutable schema IR
    -> structural source generation
    -> generated entities, values, validation, and SchemaDescriptor
                                      |
                                      v
Runtime
  ISO 10303-21 text -> internal syntax -> descriptor allocation/hydration
    -> complete mutable ExchangeStructure -> validation -> canonical writer
    -> ISO 10303-21 text
```

The generated schema code depends only on the public runtime contracts. The runtime never depends
on the Analyzer, generator implementation, or a concrete schema. `SchemaDescriptor` is the narrow
bridge through which the schema-neutral runtime invokes statically generated allocation, hydration,
validation, compatibility, and projection behavior.

## Responsibility map

| Boundary | Responsibility | Consumer visibility |
| --- | --- | --- |
| `src/grammar` | Normative Part 21 and EXPRESS grammar sources | Repository/build only |
| `TedToolkit.Step21.Analyzer` syntax and binding | Parse all supplied `.exp` files as one closed set and produce a validated immutable schema IR | Analyzer internal |
| `TedToolkit.Step21.Analyzer` generation | Emit deterministic C# declarations and descriptor behavior from the bound IR | Build-time diagnostics and generated source |
| Generated schema code | Represent the supplied EXPRESS declarations and implement direct mapping, reference, validation, and projection code | Consumer public types plus internal generated infrastructure |
| `TedToolkit.Step21` syntax and Part 21 pipeline | Recognize ISO 10303-21 text, stage binding, and produce canonical output | Internal; public stage-specific failures |
| `TedToolkit.Step21` exchange model | Own header, sections, occurrence identity, graph registration, mutation, and aggregate validation | Public runtime API |
| `SchemaDescriptor` boundary | Connect schema-neutral model operations to generated schema behavior without reflection or discovery | Public descriptor identity; internal dispatch |
| Tests and `docs/conformance` | Prove and state the delivered syntax, semantic, packaging, and deployment boundary | Maintainer and consumer evidence |

## Architectural invariants

- ISO 10303-21 and the supplied EXPRESS schemas are the only sources of public domain semantics.
- Generated schema code depends on the runtime; runtime and generated code do not depend on Analyzer-only libraries at execution time.
- Parser syntax, bound IR, hydration state, and generator machinery remain internal; callers receive complete public values or stage-specific failures.
- Editing may be temporarily invalid, but validation is side-effect-free and read/write public boundaries are atomic.
- Unsupported behavior is explicit and evidence-backed; it is never guessed, silently omitted, or promoted from corpus success to conformance.
- Runtime and generated execution remain statically reachable and Native AOT-ready.

## Durable records

- [Product intent](../product/README.md)
- [Design principles](../principles/README.md)
- [Detailed schema-bound round-trip architecture](schema-bound-round-trip.md)
- [Architecture decision records](../adr/)
- [Conformance and capability matrix](../conformance/README.md)

