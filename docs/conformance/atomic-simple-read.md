# Atomic simple typed reading

`ExchangeStructure.Read(TextReader, IReadOnlyCollection<SchemaDescriptor>)` is the only public Part 21 read entry
point. It consumes a closed descriptor set, parses into private immutable syntax, binds one unnamed `DATA` section,
hydrates generated entities through direct descriptor dispatch, runs `ExchangeStructure.Validate()`, and returns only
the complete validated mutable structure.

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

The first typed-read slice requires exactly one unparameterized data section and exactly one `FILE_SCHEMA` string.
That identifier becomes the section's governing `SchemaName` and must match one supplied descriptor exactly. Every
simple instance is allocated before hydration, occurrence names are canonicalized without CLR integer narrowing, and
physical parameters retain descriptor order.

The binder decodes arbitrary-precision INTEGER, exact REAL/NUMBER, ISO 10303-21 STRING directives (including ISO
8859 pages and `X2`/`X4` Unicode forms), bit-accurate BINARY, BOOLEAN/LOGICAL symbols, enumerations, typed values,
recursive aggregates, local entity occurrences, `$`, and `*`. Generated descriptor mapping then applies nominal defined, enumeration, SELECT,
OPTIONAL, and aggregate semantics. A syntactically valid physical value that cannot denote a runtime value produces
`P21-BIND-VALUE`; descriptor entity/component/count/type failures retain their `P21-BIND-*` codes and receive the
originating record location when the generated diagnostic has no EXPRESS source location.

## Atomic failures and staged capability

- invalid clear-text syntax throws `ExchangeStructureSyntaxException` with all syntax diagnostics;
- schema/header/entity/value/hydration mismatches throw `ExchangeStructureBindingException` with all binding
  diagnostics;
- an invalid hydrated population throws `ExchangeStructureReadValidationException` with the complete
  `ValidationResult`; and
- anchor/signature operations, external value occurrences, complex instances, named or multiple data sections,
  multiple schemas, and additional header entities throw `ExchangeStructureCapabilityException`.

Local entity references are allocated and hydrated atomically; missing, declared external, and incompatible targets
instead produce the dedicated aggregate read-validation evidence documented by the
[reference-hydration boundary](reference-hydration.md).

No exception exposes syntax nodes, a binding/hydration context, or a partially hydrated entity. There is no public
reader facade, result wrapper, raw model, registry, or nested public processing type. Multi-section and complex
populations remain later work items.

## Verification

Fast generated-consumer tests compile arbitrary EXPRESS schemas and prove the exact public signature, scalar and
recursive aggregate fidelity, nominal/enumeration/SELECT values, OPTIONAL absence, ISO string and binary decoding,
descriptor preflight before source consumption, complete stage-specific failure evidence, unchanged I/O exceptions,
validation-before-publication, and absence of leaked processing types. The cumulative public API snapshot fixes the
single added method.
