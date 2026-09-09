# Part 21 physical short names

The Analyzer accepts an optional `.p21map` `AdditionalFiles` input when the document defining an EXPRESS schema
also supplies short names. This metadata is implementation input, not an extension to EXPRESS or an invented
abbreviation scheme.

Normative trace:

- ISO 10303-21:2016 clause 12.1.7 — short encodings for enumeration values;
- clause 12.1.8 — short keywords for simple defined and enumeration types selected by a SELECT value;
- clause 12.2.11 — short encodings for entity type names;
- Annex D.3.2 — independent read/write capability declarations for all three categories.

The clause navigation source is the public
[ISO 10303-21 Edition 3 final text](https://www.steptools.com/stds/step/IS_final_p21e3.html). Repository fixtures are
independently authored and do not reproduce ISO prose.

## Input contract

Add the EXPRESS schema and its companion map to the consuming project:

```xml
<ItemGroup>
  <AdditionalFiles Include="Schemas/catalog.exp" />
  <AdditionalFiles Include="Schemas/catalog.p21map" />
</ItemGroup>
```

One map defines one supplied schema. Keywords are case-insensitive; identifiers are normalized to upper case;
blank lines, `#` comments, and optional trailing semicolons are accepted.

```text
SCHEMA catalog
ENTITY catalog_item ci
TYPE distance_measure dm
TYPE item_state ist
ENUMERATION item_state active act
ENUMERATION item_state inactive ina
END_SCHEMA
```

`ENTITY` rows name schema-local entity declarations. `TYPE` rows name schema-local simple defined or enumeration
types used as typed SELECT alternatives; a SELECT declaration itself has no physical keyword mapping.
`ENUMERATION` rows name a schema-local enumeration type, one of its values, and that value's physical short name.

The generated descriptor accepts either canonical long names or supplied short names. Writing uses a supplied
short name when present and otherwise uses the upper-case long name. No row means long-name-only behavior; the
Analyzer never derives a short name heuristically.

## Validation

`STEP21EXP007` rejects malformed rows, missing/duplicate schema maps, unknown or cross-category declarations,
duplicate mappings, and case-insensitive collisions between long and physical names. An invalid map withholds
generation for the closed input set so a partial physical-name contract cannot be compiled accidentally.

The mapping is resolved and emitted at build time. Generated code uses direct comparisons and switch expressions;
the runtime performs no reflection discovery, dictionary construction, or schema-specific interpretation.
