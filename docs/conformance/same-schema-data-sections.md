# Same-schema data-section binding

`ExchangeStructure.Read` binds one or more simple data sections into one atomic structure when every section is
governed by the same supplied generated descriptor. This focused boundary
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
which must occur in `FILE_SCHEMA`; in a same-schema structure every section selects that same descriptor.
The reader preserves each valid name as runtime-internal write context while public `DataSection` continues to expose
only its ISO governing `SchemaName`, never a generated descriptor relationship. Standard physical schema names use
uppercase EXPRESS spelling. Descriptor selection additionally compares nominal identifiers case-insensitively as an
explicit interoperability tolerance; lowercase or mixed-case input and retained writeback are not ISO 10303-21
syntax-conforming spellings. Selection ignores only the supported canonical space-delimited numeric-arc OID suffix
subset. Legal ASN.1 forms outside that subset, including named arcs, are unsupported. An accepted OID is syntax-checked
and retained rather than validated as the AP203 package or schema-baseline identity. The public schema name and
complete `FILE_SCHEMA` identifier retain their supplied spelling.

Malformed parameter counts/types, duplicate section names, and a section schema absent from `FILE_SCHEMA` aggregate as
source-located `P21-BIND-DATA-SECTION` diagnostics. Structures with different governing descriptors and explicit
schema populations use the [multi-schema population contract](multi-schema-populations.md).

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
unnamed-section behavior remains covered by the original atomic-read suite. Supported flat-`ANDOR` complex entity
instances share the same section and identity rules; operational external-resource acquisition remains unsupported.
