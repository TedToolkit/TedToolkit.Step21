# Canonical simple writing

`ExchangeStructure.Write(TextWriter)` and `WriteEntity(TextWriter, Entity)` are the only public Part 21 write entry
points. Both are instance methods because occurrence names, reference names, section membership, descriptors, and
schema-population context belong to one `ExchangeStructure`, not to an entity.

## Complete structures

`Write` emits deterministic ISO 10303-21 clear text with `\n` line separators:

1. `ISO-10303-21`, the three required header entities, and retained standard `FILE_POPULATION` entities;
2. each data section in `DataSections` order, including its retained name and exact governing `SchemaName` when named;
3. each registered entity in structure registration order within its owning section; and
4. `END-ISO-10303-21` without an invented trailing record or metadata field.

The writer canonicalizes decoded header/data strings using Part 21 character directives. It preserves list order and
emits structure-owned canonical occurrence digits. A sole unnamed section uses `DATA;`; every multi-section structure
requires retained names and emits `DATA('name',('schema'));`.

## Entity records and parameters

`WriteEntity` emits exactly one registered record such as:

```step
#7=ROOT($,*,#42,(.U.),LABEL('x'));
```

The governing generated descriptor projects the entity to one simple physical component and ordered strong
`ParameterValue` values. The runtime then formats INTEGER, REAL/NUMBER, STRING, BINARY, BOOLEAN, LOGICAL (including
UNKNOWN), enumeration, typed values, recursive aggregates, omitted `$`, derived `*`, and entity references. Reference
objects are resolved through the current structure's reverse identity map, so the same CLR object may write under
different names in different structures without mutation. `Entity.ToString()` and diagnostic `ToString()` methods are
never serialization contracts.

## Failure and I/O boundary

Before the first destination write, the runtime:

- runs complete structure validation and throws `ExchangeStructureWriteValidationException` when invalid;
- verifies descriptor and section write capabilities and throws `ExchangeStructureCapabilityException` when the
  simple writer cannot represent the request; and
- projects and formats every requested simple record into a private buffer.

These detectable domain/capability failures therefore produce no partial output. An unregistered `WriteEntity` target
is a write-validation failure. Once the buffered text is handed to the supplied `TextWriter`, its I/O exception is
allowed to propagate unchanged; the runtime does not catch, translate, or claim rollback of external I/O.

This item does not claim byte preservation. The later
[complex-mapping round-trip](complex-mapping-round-trip.md) extends the same buffered writer with ordered external
mapping, and [atomic pre-write validation](atomic-prewrite-validation.md) defines the final complete failure gate shared
by both public writer entry points.
