# Generated schema descriptors

Every valid supplied EXPRESS schema emits one reflection-free descriptor in `TedToolkit.Step21.Generated.<SchemaPascalCase>`. The descriptor is an ordinary public sealed `class`, not a `record`, because it represents singleton behavior and identity rather than a structurally comparable ISO value. Its private constructor and public static `Instance` property expose the only instance; `Name` preserves the nominal schema name.

The runtime `SchemaDescriptor` base is an abstract `class`. Its public surface contains only `SchemaName Name`; runtime-internal non-virtual dispatch forwards to protected allocation, hydration, validation, capability-diagnostic, and projection hooks. The hooks exchange only `Entity`, `ExchangeStructure`, diagnostics/results, ordered BCL component-name/`ParameterValue` lists, and an ordered BCL path/entity list for validation. `ExchangeStructure` derives that validation list from its private registration index, so generated consumer-assembly overrides need neither internal access nor a public registry. The hooks expose no parser syntax, context, resolver, writer callback, nested public type, reflection, or dynamic code.

## Simple physical mapping

For inherited-free entities whose physical attributes are supported by this stage, the generated descriptor:

- allocates a generated entity for one exact physical entity name;
- requires one exact component and the exact physical parameter count;
- hydrates and projects scalars without narrowing `BigInteger`, `RealValue`, or `NumberValue`;
- preserves nominal defined-type wrappers, closed/extensible enumerations, SELECT typed alternatives, and resolved entity identity;
- maps an absent outer `OPTIONAL` attribute only from/to `$`;
- rejects `*` for an explicit attribute with `P21-BIND-PARAMETER`; and
- constructs literal-bounded `ARRAY`, `LIST`, `BAG`, and `SET` candidates with their declared bounds and `OPTIONAL`/`UNIQUE` metadata, preserving unset optional array slots.

Component, count, entity, and parameter mismatches return stable `P21-BIND-*` diagnostics. A standard-conforming physical schema name uses its uppercase EXPRESS spelling. Descriptor lookup also compares the nominal identifier case-insensitively as an explicit interoperability tolerance; accepting or writing a lowercase or mixed-case header does not make that spelling ISO 10303-21 syntax-conforming. Lookup accepts only the supported canonical numeric object-identifier suffix subset: at least one space followed by braces containing at least two space-delimited decimal arcs. Arcs use canonical decimal spelling without leading zeroes; the root arc is 0 through 2, and a root of 0 or 1 limits the second arc to 0 through 39. Legal ASN.1 forms outside that subset, including named arcs, are unsupported and return `P21-BIND-SCHEMA`. The complete `FILE_SCHEMA` string remains unchanged, and an accepted OID is syntax-checked and retained rather than validated as the AP203 package or schema-baseline identity. Other protocols, close names, malformed suffixes, and missing descriptors also return `P21-BIND-SCHEMA`.

Descriptors supplied to reading or manual structure construction are snapshotted before source consumption. Names that resolve to the same nominal binding identifier fail as duplicates, including case-only variants. Different raw `FILE_SCHEMA` strings that resolve to one nominal identifier fail atomically with `P21-BIND-SCHEMA`; the reader never chooses the first or silently folds them. Public `SchemaName` equality remains exact and ordinal; normalization is private to descriptor selection and header association.

Entity-capable physical parameters use an internal indexed `P21-BIND-REFERENCE-TYPE-*` mismatch code. The atomic
reader consumes that machine evidence and publishes `P21.READ.REFERENCE.TYPE` with the original occurrence location;
non-reference mismatches retain the public `P21-BIND-PARAMETER` behavior. Target-type classification never parses
human-readable messages.

## Structural validation

The generated descriptor dispatches each registered entity governed by its schema to directly generated structural checks. The checks cover mandatory/`OPTIONAL` presence, generated entity assignability, literal aggregate shape and bounds, required array slots, uniqueness, nested elements, nominal wrappers, enumerations, and SELECT alternatives. See the [structural validation boundary](structural-validation.md) for ordering, paths, constraint IDs, XML traceability, and explicit unsupported cases.

## Current boundary

Generated descriptors execute the accepted validation-reachable expression/rule closure and support atomic simple and
source-bounded complex read/write mapping, including inherited and redeclared physical components. They do not discover
descriptors or acquire external resources. The runtime binds and writes named sections and populations only through
the explicitly supplied closed descriptor set.
Structure-local parsed occurrences now hydrate through the atomic
[reference boundary](reference-hydration.md), including governed multi-schema populations and supported complex mappings.
Unsupported entities are not guessed through reflection or property names.

Fast tests execute every scalar alternative and supported simple parameter form in both directions, including absence, invalid derived markers, nominal/enumeration/SELECT values, entity identity, and all four aggregate categories. Runtime API snapshots fix the abstract-class hook contract. The packed-consumer integration test compiles the generated sealed descriptor from the real package without a runtime dependency on analyzer implementation libraries.
