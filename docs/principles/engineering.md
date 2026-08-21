# Engineering principles

## EP-001: Change grammars only from standard evidence

- Status: Active
- Strength: Required
- Scope: `src/grammar/*.g4`, generated ANTLR artifacts, syntax diagnostics, and syntax-conformance claims
- Owner: repository maintainer
- Review trigger: any grammar production or lexical rule changes

### Default

Every behavior-changing `.g4` change must cite the governing ISO 10303-21 or ISO 10303-11 clause or production, add focused positive and negative fixtures, regenerate rather than hand-edit ANTLR output, and distinguish syntax conformance from schema conformance.

### Rationale

Real-world STEP and IFC files contain vendor conventions and historical deviations. Accepting a corpus artifact without normative evidence can silently redefine the language, while rejecting a valid construct can make a parser appear domain-specific.

### Practical implications

- ISO 10303-21:2016 Edition 3 is the physical-file baseline; ISO 10303-11:2004 Edition 2 is the EXPRESS-language baseline until superseded.
- Clause citations and implemented conformance scope accompany grammar-facing tests or documentation.
- Authoritative external corpora supplement but do not replace clause-level fixtures.
- Semantic rules such as uniqueness, reference target compatibility, attribute ordering, and schema constraints belong outside the grammar unless the standard defines them syntactically.
- Generated files under `Generated/` are reproducible outputs, never the source of a grammar fix.

### Exception route

A deliberate compatibility extension requires an accepted ADR, a diagnostic or opt-in conformance mode, and documentation that it is not ISO syntax.

## EP-002: Preserve ISO semantics before convenience

- Status: Active
- Strength: Required
- Scope: typed models, reference navigation, mutation, writing, diagnostics, and generated public surfaces
- Owner: repository maintainer
- Review trigger: an API hides model ownership, silently repairs invalid data, or adds a non-ISO serialization contract

### Default

Ergonomic APIs may shorten explicit ISO operations but must not erase entity instance identity, schema-defined reference types, omitted or derived parameter meaning, or write-time conformance diagnostics.

### Rationale

Direct C# entity references are the most natural generated API, but ISO 10303-21 instance names and incomplete parse-time construction still have to be managed explicitly by the owning model. General-purpose serializers would obscure those responsibilities and add unrelated contracts.

### Practical implications

- Schema entity properties expose the generated entity interface directly; consumers do not handle reference or identifier wrappers.
- Parsing uses a model-owned two-phase hydration boundary: allocate and register every entity first, then bind direct object references before publishing the typed model.
- ISO 10303-21 instance names remain in the model's identity map and raw exchange-structure layer, not as required public properties on generated entities.
- Writing is a schema-aware model operation, not JSON/XML serialization of object properties.
- Canonical semantic round-trip is required; exact whitespace, comment, and token spelling preservation is optional unless separately designed.
- Unsupported mappings fail with diagnostics instead of being guessed or omitted.

### Exception route

Any proxy/lazy-resolution or alternate-serialization contract requires an accepted ADR covering identity, cycles, equality, model lifetime, and ISO write-back behavior.

## EP-003: Prefer static contracts and keep runtime paths Native AOT-ready

- Status: Active
- Strength: Required
- Scope: runtime libraries, generated schema code, runtime discovery and activation, runtime package dependencies, trimming, single-file deployment, and Native AOT consumer behavior; build-time Analyzer execution is excluded
- Owner: repository maintainer
- Review trigger: a runtime path or dependency introduces unbounded reflection, assembly scanning, runtime code generation, linker preservation configuration, or an AOT warning

### Default

Core runtime behavior and generated schema behavior shall be statically reachable or source-generated and shall remain usable from a trimmed Native AOT application without runtime discovery, dynamic code generation, or reflection-only fallback behavior.

### Rationale

Runtime reflection and dynamic discovery hide dependency edges from the compiler and linker, making trimming and Native AOT behavior fragile or environment-dependent. The repository already owns a compile-time schema generator, so it can emit direct mappings, reference enumeration, validator wiring, and writer dispatch rather than rediscovering them at runtime.

### Practical implications

- Source generation emits direct schema descriptors, physical mappings, validator construction, and one-level entity-reference enumeration; runtime code does not scan assemblies or inspect generated properties to recover this information.
- Reflection is not categorically forbidden, but any retained use must be statically analyzable, trimming-safe, Native AOT-safe, and unnecessary for core schema discovery or execution.
- Runtime code does not depend on `Reflection.Emit`, runtime source compilation, unbounded `MakeGenericType`, convention-based `Activator.CreateInstance`, or automatic assembly scanning.
- The runtime project declares `IsAotCompatible` when its target framework supports the SDK contract, and AOT, trimming, and single-file analyzer warnings are treated as defects rather than suppressed by default.
- A representative generated-schema consumer is published and executed with Native AOT in Release; read, validation, graph registration, and write paths must succeed without trimming/AOT warnings attributable to the delivered runtime or generated code.
- Every runtime dependency must have documented Native AOT evidence for the exercised paths. An analyzer-only dependency does not become a consumer runtime dependency merely to satisfy generator implementation convenience.

### Exception route

A runtime dependency or behavior that cannot satisfy the Native AOT proof requires an accepted ADR identifying the affected public paths, why no static/source-generated alternative is viable, the warning or runtime consequences, and an objective removal trigger. Warning suppression or linker-root configuration alone is not proof of compatibility.
