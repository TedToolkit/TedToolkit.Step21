# Closed-set EXPRESS binding

## Input boundary

`ExpressSchemaCompiler` treats the supplied `(logical path, text)` values as the complete schema universe. Logical paths are diagnostic identities only: compilation does not open them and performs no disk, environment, package, or network lookup. Sources, schemas, imports, bound output, and diagnostics are ordered deterministically, so permuting the input sequence does not change the result.

Schema and EXPRESS declaration names are case-insensitive. Duplicate schema identities, duplicate declarations, conflicting visible names, missing schemas/resources, unresolved names/types, incompatible declaration kinds, invalid extension bases, and illegal type/inheritance cycles are reported at 1-based source locations. Syntax and binding diagnostics remain separate. An invalid schema is withheld; every schema that imports it is withheld transitively, while an independent valid dependency component remains available.

## Interface semantics

The binder follows ISO 10303-11:2004 Edition 2 interface semantics:

- `USE FROM` imports named types (entities and defined types). USE-visible names are local and are re-exported through subsequent USE interfaces.
- `REFERENCE FROM` imports constants, entities, functions, procedures, and defined types that the target declares or makes visible through USE. REFERENCE-visible names are not re-exported.
- Explicit import lists and aliases retain the declaration identity of their source. A local or USE-visible name takes precedence over a same-spelled REFERENCE-visible name.
- Mutually importing schemas are legal. Export visibility is computed to a fixed point before references are connected, so partial and total schema recursion is order-independent.

The edition baseline is the [ISO 10303-11:2004 catalogue record](https://www.iso.org/standard/38047.html). The cycle-safe multi-pass strategy and lexical-to-schema lookup order are independently corroborated by NIST's [EXPRESS Toolkit design and implementation report](https://tsapps.nist.gov/publication/get_pdf.cfm?pub_id=821300).

## Bound IR and execution boundary

The Analyzer publishes no new public API. Its internal immutable bound IR retains schema/declaration identity, direct inheritance, attribute categories and declared types, aggregate/constructed/generalized types, imports, and resolved name references. Neutral syntax such as `name(...)` and bare names is classified against schema and lexical scopes, including parameters, locals, nested algorithm declarations, aliases, query variables, repeat variables, entity attributes, and enumeration members. Nested type references are bound in their declaring lexical scope, and REPEAT variables use the normative `NUMBER` type.

Binding does not evaluate expressions or procedures and does not generate C#. Operator result typing, runtime value semantics, constraint reachability, and static generated execution belong to SBRT-016 and SBRT-017. The retained declaration targets and declared types are their deterministic input; no runtime interpreter or lookup service is introduced here.

## Verification

Fast TUnit cases cover reordered multi-file inputs, explicit/full imports, aliases, USE re-export versus REFERENCE non-re-export, circular interfaces, case-insensitive lookup, inheritance, every declared type family, lexical and qualified name binding, nested declarations, immutable internal surface, logical paths that cannot exist on disk, aggregate deterministic failures, and independent-schema publication. Release build and generated-artifact tests enforce zero diagnostics and byte-stable ANTLR output.
