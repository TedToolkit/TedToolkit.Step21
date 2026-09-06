# Anchor, occurrence, and UUID semantics

`ExchangeStructure` preserves Edition 3 anchor entries and external occurrence associations as
schema-neutral state. Consumers can read, edit, validate, canonically write, and reread this state without a
resource provider or application-protocol-specific model.

Normative navigation uses the publicly accessible
[ISO 10303-21 Edition 3 final text](https://www.steptools.com/stds/step/IS_final_p21e3.html). The implementation
is traced to clauses 6.4.4, 6.5, and 9 plus normative Annex G; repository tests use independently authored
vectors rather than copied examples.

## Contract

| Standard area | Runtime representation | Enforced behavior |
| --- | --- | --- |
| 6.4.4 occurrence names | `EntityInstanceName`, `ValueInstanceName`, `ConstantEntityName`, `ConstantValueName` | Positive arbitrary-precision numeric identity, insignificant leading zeroes, one shared entity/value integer identity space, and the Part 21 `UPPER`/`DIGIT` alphabet (including `_` in `UPPER`) for constants and enumerations |
| 6.5.1–6.5.5 encodings | `Part21Resource`, `AnchorName`, `Part21AnchorTag` | RFC 2396 hierarchical, opaque, relative, fragment, query, and escaped URI-reference checks; non-numeric anchor names; and exact tag spelling including `_` |
| 9 anchor section | `Part21Anchor`, `Part21AnchorTag`, `ParameterValue` alternatives, `ExchangeStructure.Anchors` | Physical order, recursive lists, physical simple/null/resource/occurrence/constant items, ordered tags, unique names, and entity identity preservation |
| 9.2.7 EXPRESS constants | Generated `SchemaDescriptor` constant-name lookup | Constant category and existence are checked against the first `FILE_SCHEMA` descriptor without runtime reflection or AP knowledge |
| Annex G UUID mapping | `AnchorName.IsUuid` and `TryGetUuid` | Prefix-free RFC 4122 `D` spelling is recognized and equivalent UUID spellings share identity for duplicate detection |
| Reference declarations needed by anchor occurrences | `Part21Reference` and `ExchangeStructure.References` | Entity/value associations retain canonical local names and exact resource URI text; duplicate, cross-category overlap, and local-data collisions fail |

Local entity anchor items are bound to the registered entity object. Unresolved external entity and value anchor
items retain their occurrence category and identity; this record does not claim resource acquisition or resolution.
Public edits must likewise use `ParameterValue.FromEntity` for a local entity; the unresolved entity-name alternative
is reserved for an entity declared by `REFERENCE`. Physical `.T.`, `.F.`, and `.U.` anchor spellings are untyped
enumerations, so callers use `FromEnumeration`; the stronger Boolean and Logical parameter alternatives are rejected
at the anchor boundary because the physical anchor grammar cannot reconstruct them without changing value kind.
Anchor and reference sections are emitted before data sections, and validation completes before any destination
characters are published.

Generated constant lookup is shared by the runtime base descriptor and emitted only for schemas that actually
declare constants in the relevant category. Schemas without constants receive no repeated lookup override.

## Evidence

`AnchorOccurrenceConformanceTests` covers positive and neighboring-invalid/editing partitions: all physical anchor
item families, nested lists, ordered and underscore-bearing tags/enumerations, hierarchical and opaque URI editing,
the four occurrence categories, local object identity and retargeting resistance, external association retention,
UUID equality, arbitrary-precision names, constant category lookup, duplicate/overlap failures, and zero-output
writer atomicity.

The existing public API snapshot, schema-descriptor dispatch tests, compiler manifest, and pinned AP203/AP214/AP242
surface tests guard additive compatibility and generated-code containment.
