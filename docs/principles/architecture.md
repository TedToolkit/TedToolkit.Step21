# Architecture principles

## AP-001: ISO 10303-21 defines the product boundary

- Status: Active
- Strength: Required
- Scope: public APIs, grammar scope, generated types, package positioning, and conformance claims
- Owner: repository maintainer
- Review trigger: a feature is justified only by one application protocol, file extension, or domain model

### Default

Model ISO 10303-21 exchange-structure concepts directly and treat STEP application protocols, IFC, and other EXPRESS-schema ecosystems only as consumers and conformance evidence.

### Rationale

Allowing a sampled schema or file extension to define the runtime would create incompatible abstractions, distort standard terminology, and prevent the same physical-file implementation from serving other EXPRESS schemas.

### Practical implications

- Public types use ISO 10303-21 terminology such as exchange structure, data section, entity instance name, and parameter.
- Domain conveniences do not enter the core runtime unless they are expressed as optional adapters outside the standard model.
- Tests use NIST, buildingSMART, and other corpora as interoperability evidence, never as substitutes for a standard clause.
- README and package descriptions distinguish currently implemented conformance from the library's governing scope.

### Exception route

Any domain-specific core abstraction requires an accepted ADR that defines a narrower package boundary without changing the ISO-neutral runtime.

## AP-002: Keep the runtime schema-neutral

- Status: Active
- Strength: Required
- Scope: runtime, EXPRESS compiler, source generator, and generated-code dependencies
- Owner: repository maintainer
- Review trigger: a dependency points from the runtime to a schema, analyzer, or generator-only library

### Default

The runtime defines schema-independent ISO 10303-21 values, identity, model, resolution, diagnostics, and writing contracts; generated schema code depends on those contracts, and generator implementation dependencies remain isolated in the analyzer package.

### Rationale

This direction permits any EXPRESS schema to use one runtime, keeps Roslyn and ANTLR generation details out of consumer objects, and allows standard physical syntax to be recognized in internal parser infrastructure before explicit generated-schema binding.

### Practical implications

- The Analyzer may depend on the EXPRESS compiler, ANTLR, Roslyn, and TedToolkit.RoslynHelper.
- Generated code references only TedToolkit.Step21 runtime contracts.
- TedToolkit.Step21 does not depend on the Analyzer or a concrete generated schema at runtime.
- Generated schema metadata bridges raw parameters and typed records without reflection-based domain discovery.

### Exception route

A reversed or cyclic dependency requires an accepted ADR demonstrating why a schema-neutral contract cannot represent the requirement.

## AP-003: Do not add domain concepts beyond ISO 10303-21 and EXPRESS

- Status: Active
- Strength: Required
- Scope: public domain model, generated schema types, naming, nullability, inheritance, collections, identity, reading, and writing
- Owner: repository maintainer
- Review trigger: a public domain type or behavior cannot cite its ISO 10303-21 or supplied EXPRESS source, implementation infrastructure is presented as domain semantics, or a C# convention would alter source semantics

### Default

The public domain model shall contain only concepts and relationships defined by ISO 10303-21 or the supplied EXPRESS schemas. The library shall not add convenience domain types, ownership relationships, identities, states, or behaviors beyond those sources. Clearly named parsing, binding, validation, diagnostic, generation, and writing infrastructure is permitted only when required to implement the standard and shall not be presented as ISO/EXPRESS domain semantics. Only after semantic equivalence is established may the implementation choose the most idiomatic C# representation.

### Rationale

Standard fidelity is the library's interoperability contract. An invented domain concept would change how consumers reason about identity, ownership, validity, navigation, or serialization and could make one application protocol appear to govern the schema-neutral runtime. C# design quality makes the standard contract usable, but convenience cannot justify a wrapper, state, relationship, or behavior that the exchange structure or schema does not contain. Conversely, literal syntax-shaped APIs should not be retained when C# can express exactly the same semantics more naturally and completely.

### Practical implications

- Every public domain type, member, relationship, and state has a documented trace to an ISO 10303-21 concept or a declaration in the supplied EXPRESS schema.
- A code review rejects a proposed public domain concept when that trace is absent; similarity to one STEP application protocol, a corpus, or another library is not sufficient evidence.
- Direct entity properties express the EXPRESS entity-valued attribute in ordinary C#; model-owned instance-name bookkeeping remains implementation infrastructure required for ISO writing.
- `OPTIONAL` may map to C# nullability only when absence remains distinct from EXPRESS values such as LOGICAL unknown.
- Interfaces and records are C# representations of EXPRESS entity assignability and data; they do not add another domain type system.
- Parser, binder, validation-result, diagnostic, schema-mapping, source-generator, and writer contracts are clearly named infrastructure. They do not appear as additional values or relationships in generated entities and do not claim to be standard concepts.
- Working API names such as `P21Model<TSchema>` are not accepted merely for implementation convenience; final public names must use standard concepts such as exchange structure, section, entity instance, parameter, or schema declaration where applicable.

### Exception route

A public domain concept without a normative or schema source is prohibited. An unavoidable public infrastructure abstraction must document its operational role and demonstrate that it contributes no new exchange-structure semantics; a difficult-to-reverse exception requires an accepted ADR.
