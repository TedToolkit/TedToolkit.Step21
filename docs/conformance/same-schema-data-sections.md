# Same-schema data-section binding

`ExchangeStructure.Read` binds one or more simple data sections into one atomic structure when exactly one schema name
appears in `FILE_SCHEMA` and every section is governed by that same supplied generated descriptor. This boundary
implements the data-section context and structure-wide occurrence rules in ISO 10303-21:2016 Edition 3 clauses 11.1
and 11.2; the repository's [normative source](https://www.steptools.com/stds/step/IS_final_p21e3.html) supplies the
clause text.

## Section context

A single section may use the legacy unparameterized `DATA;` form. A parameterized single section and every section in
a multi-section structure must use:

```step
DATA('section-name',('schema_name'));
```

The first value is a unique decoded STRING section name. The second is a list containing exactly one decoded STRING,
which must occur in `FILE_SCHEMA`; this same-schema stage therefore requires it to equal the one header schema name.
The reader preserves each valid name as runtime-internal write context while public `DataSection` continues to expose
only its ISO governing `SchemaName`, never a generated descriptor relationship.

Malformed parameter counts/types, duplicate section names, and a section schema absent from `FILE_SCHEMA` aggregate as
source-located `P21-BIND-DATA-SECTION` diagnostics. Multiple header schema identifiers remain the explicit
`P21-CAP-SCHEMA-POPULATION` capability boundary owned by the multi-schema work item.

## One identity space and atomic publication

Entity occurrence names are unique across the complete exchange structure, not per section. The reader allocates every
recognized entity in every valid section, detects structure-wide duplicate `#n` names, registers each object with its
own `DataSection`, and only then hydrates any parameter. A local reference may therefore cross section boundaries,
appear before its definition, participate in a cycle, and still resolve to the exact registered object.

Reference failures use the originating section index, for example
`DataSections[1].#8.Parameters[2][0]`. `ExchangeStructure.Validate()` dispatches each section under its governing schema
with section-qualified entity paths. Any binding, reference, structural, or generated EXPRESS failure prevents the
entire structure from being returned; all detectable failures in the applicable stage retain deterministic section
context.

## Verification

Generated-consumer tests compile an arbitrary schema and prove named-section preservation, per-registration section
membership, cross-section forward/cyclic reference identity, complete malformed-context and duplicate-occurrence
binding evidence, reference paths from multiple sections, and generated rule failures from every section. Single
unnamed-section behavior remains covered by the original atomic-read suite. Multiple descriptors, cross-schema
population rules, complex entity instances, and operational external-resource acquisition remain staged.
