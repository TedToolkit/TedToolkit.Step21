# Atomic simple typed reading

The public `ExchangeStructure.Read` overloads consume a closed descriptor set, parse into private immutable syntax,
and bind zero `DATA` sections for forwarding-only structures, one named or unnamed section, or multiple named sections
governed by explicitly supplied schemas. They hydrate generated entities through direct descriptor dispatch, run
`ExchangeStructure.Validate()`, and return only the complete validated mutable structure.

```csharp
using var source = File.OpenText("sample.p21");
var structure = ExchangeStructure.Read(
    source,
    [TedToolkit.Step21.Generated.SampleSchema.SchemaDescriptor.Instance]);
```

The descriptor collection is snapshotted and checked before the first source read. A null descriptor or duplicate
ordinal `SchemaName` throws `ArgumentException`; a `TextReader` exception is allowed to propagate unchanged. Read
diagnostics use the stable logical source name `<reader>` because this overload receives no physical path.

## Supported publication slice

The typed entity-publication slice requires exactly one `FILE_SCHEMA` string. A forwarding-only distributed structure
may omit `DATA`; when data is present, a lone section may be unnamed, while a named section and every section in a
multi-section structure carries the Edition 3 section-name and single-schema parameter pair. Every governing schema
name must equal the header identifier and supplied descriptor.
Every simple instance across every section is allocated before hydration, occurrence names are unique in the complete
exchange structure and canonicalized without CLR integer narrowing, and physical parameters retain descriptor order.
See the [same-schema data-section boundary](same-schema-data-sections.md) and
[multi-schema population boundary](multi-schema-populations.md) for section context and population failure rules.

The binder decodes arbitrary-precision INTEGER, exact REAL/NUMBER, ISO 10303-21 STRING directives (including ISO
8859 pages and `X2`/`X4` Unicode forms), bit-accurate BINARY, BOOLEAN/LOGICAL symbols, enumerations, typed values,
recursive aggregates, local entity occurrences, `$`, and `*`. Generated descriptor mapping then applies nominal defined, enumeration, SELECT,
OPTIONAL, and aggregate semantics. A syntactically valid physical value that cannot denote a runtime value produces
`P21-BIND-VALUE`; descriptor entity/component/count/type failures retain their `P21-BIND-*` codes and receive the
originating record location when the generated diagnostic has no EXPRESS source location.

## Atomic failures and capability boundary

- invalid clear-text syntax throws `ExchangeStructureSyntaxException` with all syntax diagnostics;
- schema/header/entity/value/hydration mismatches throw `ExchangeStructureBindingException` with all binding
  diagnostics;
- an invalid hydrated population throws `ExchangeStructureReadValidationException` with the complete
  `ValidationResult`; and
- constant DATA occurrences throw `ExchangeStructureCapabilityException`. The explicit
  resource-options overload binds resolved `@n` values through direct, aggregate, and typed DATA positions.
  External-resource resolution reports missing capability,
  provider re-entry, archive, and quota failures through the same atomic exception boundary. Supported flat-`ANDOR`
  complex instances and multiple governing schemas are handled by their dedicated mapping/population contracts.

Local entity references are allocated and hydrated atomically. The legacy overload retains dedicated unresolved
external evidence; the opt-in resource overload applies clause-10 null behavior and binds successfully resolved
external targets by object identity. Incompatible resolved entity types retain aggregate read-validation evidence.
See the [reference-hydration boundary](reference-hydration.md) and
[distributed resource resolution](distributed-resource-resolution.md).

No exception exposes syntax nodes, a binding/hydration context, or a partially hydrated entity. There is no public
reader facade, result wrapper, raw model, registry, or nested public processing type.

## Verification

Fast generated-consumer tests compile arbitrary EXPRESS schemas and prove the exact public signature, scalar and
recursive aggregate fidelity, nominal/enumeration/SELECT values, OPTIONAL absence, ISO string and binary decoding,
descriptor preflight before source consumption, complete stage-specific failure evidence, unchanged I/O exceptions,
validation-before-publication, and absence of leaked processing types. The cumulative public API snapshot fixes the
single added method.
