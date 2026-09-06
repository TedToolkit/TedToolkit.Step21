# Atomic entity-reference hydration

`ExchangeStructure.Read` resolves structure-local entity occurrence parameters after every simple entity has been
allocated and registered. This two-phase order permits forward, backward, shared, aggregate, SELECT-contained, and
cyclic references without a proxy, lazy resolver, ID wrapper, or entity container back-reference. Generated entity
properties receive the actual allocated target object through their declared generated interface.

## Resolution and hydration order

For the supported one-schema read slice, the runtime:

1. allocates every recognized simple entity and canonicalizes its positive `#n` name;
2. registers all allocated objects in the private structure identity index;
3. recursively converts each physical parameter, replacing every local `#n` with `ParameterValue.FromEntity` of the
   exact registered object;
4. invokes generated descriptor hydration only through strong `ParameterValue` component lists; and
5. validates and publishes only after reference and schema validation evidence is empty.

The generated descriptor retains static target-type checks for direct, aggregate, defined, and SELECT reference
positions. Its internal mismatch code carries the physical parameter index, allowing the runtime to attach the
original Part 21 occurrence location and deterministic structure path without parsing human-readable messages,
reflection, or public context objects.

## Atomic reference failures

Reference failures are validation evidence, not syntax or general binding diagnostics. A failed public read throws one
`ExchangeStructureReadValidationException` whose `ValidationResult` contains every detected occurrence in source and
physical-parameter order:

| Code | Meaning |
| --- | --- |
| `P21.READ.REFERENCE.MISSING` | The local `#n` has no allocated data-section entity and no external declaration. |
| `P21.READ.REFERENCE.EXTERNAL` | A `REFERENCE` section declares `#n`, but operational external-resource resolution is unavailable. |
| `P21.READ.REFERENCE.TYPE` | The allocated target exists but is not assignable to the generated entity interface required at that physical parameter. |

Paths begin with the occurrence's actual `DataSections[s].#n.Parameters[i]`; recursive aggregate elements append
`[i]`, and typed values append `.Value`. Each failure retains the exact `<reader>` line and 1-based column of the originating occurrence (or the root
physical parameter for descriptor-reported target incompatibility). Missing and unsupported external occurrences stay
distinct even when both occur in one recursive parameter.

No failed attempt exposes its private structure, generated entities, syntax graph, resolver, or hydration state.
External resource acquisition remains unsupported: the `REFERENCE` section is consumed only to distinguish declared
external occurrences from missing local names. Using external value occurrences (`@n`) in typed parameters and
verifying signatures remain explicit capability failures. Anchor and reference declarations themselves retain the
schema-neutral round-trip semantics described in the
[anchor/occurrence/UUID record](anchor-occurrence-uuid.md). Multiple governing schemas and supported flat-`ANDOR`
complex entity mappings use the same structure-local identity space across all data sections.

## Verification

Generated-consumer tests compile an arbitrary reference schema and prove reference equality across forward, backward,
shared, aggregate, and cyclic navigation. Negative fixtures combine incompatible, missing, and external occurrences,
assert every stable code/path/source coordinate in one read-validation result, and confirm that generated compilation
and runtime paths remain warning-free and reflection-free.
