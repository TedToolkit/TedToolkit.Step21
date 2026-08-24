# Multi-schema binding and schema instance populations

`ExchangeStructure.Read` accepts a closed set of generated `SchemaDescriptor` instances and binds every data section
under the descriptor whose nominal EXPRESS identifier matches its governing schema. Descriptor selection is
case-insensitive and ignores only a valid numeric object-identifier suffix while retaining the supplied schema text.
It does not discover schemas, load external metadata, or attach descriptor state to entities or `DataSection` objects.

This boundary implements the schema-population and cross-schema rules in ISO 10303-21:2016 Edition 3 Annex E. The
repository's [normative source](https://www.steptools.com/stds/step/IS_final_p21e3.html) supplies the clause text.

## Header and section binding

Every schema identifier in `FILE_SCHEMA` must have one explicitly supplied descriptor. Supplied descriptor names that
normalize to one binding identifier are rejected before source consumption rather than resolved by order. Multiple
named data sections use the standard form:

```step
DATA('section-name',('governing_schema'));
```

The governing schema must occur in `FILE_SCHEMA`. Descriptor names, header schema names, and section schema names use
exact ordinal matching. Missing descriptors, invalid section associations, malformed `FILE_POPULATION` entities,
duplicate population input names, and absent input sections aggregate as source-located binding diagnostics before a
model can be returned.

## `FILE_POPULATION`

The reader retains each valid header declaration in this standard order:

```step
FILE_POPULATION('governing_schema','determination_method',('input-section-1','input-section-2'));
```

`$` in the third parameter denotes every data section. The three ISO determination methods have these effects:

- `SECTION_BOUNDARY` includes every entity in the named input sections.
- `INCLUDE_ALL_COMPATIBLE` also includes entities in other sections whose generated types can be referenced through a
  local declaration or an EXPRESS `USE`/`REFERENCE` interface of the governing schema.
- `INCLUDE_REFERENCED` also includes registered entities in other sections that are directly referenced by entities in
  the input sections.

A section not named by any `FILE_POPULATION` is validated as its own implicit `SECTION_BOUNDARY` population under its
section schema. Within any population, a reference to a registered entity outside that population behaves as unset:
an OPTIONAL reference becomes omitted, while a required reference produces population validation evidence. Validation
uses a detached generated projection, so applying that rule never mutates or republishes the caller's object graph.

The governing descriptor executes its global RULE declarations over the selected complete population. Each imported
entity is also dispatched to its declaring descriptor for entity-local WHERE and UNIQUE constraints, without executing
unrelated global RULE declarations from that declaring schema.

An unknown determination method is a `P21-CAP-SCHEMA-POPULATION` capability failure. No vendor algorithm is guessed.

## Cross-schema references

Annex E.1 permits cross-schema validity through EXPRESS interfaces or SDAI domain equivalence. This implementation can
prove the EXPRESS-interface method from the supplied closed `.exp` set: generated imported interfaces enforce actual
CLR assignability, preserving forward, shared, and cyclic references by object identity. A structurally similar entity
from an unrelated schema is rejected as `P21.READ.REFERENCE.TYPE` with its originating section path.

SDAI domain equivalence depends on ISO 10303-22 repository metadata that is not carried by the exchange structure or
the supplied descriptors. It is therefore outside this explicit closed-set boundary; the runtime does not infer
equivalence by entity name, shape, or CLR coincidence.

For manually constructed graphs, register a referenced entity in its governing `DataSection` before adding a root in
another section. Graph traversal skips existing registrations, so the referenced entity retains its original section.
The [canonical simple writer](canonical-simple-writing.md) retains these named section associations and standard
`FILE_POPULATION` declarations in deterministic output.
