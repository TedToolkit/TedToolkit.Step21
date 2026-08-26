# Schema-bound ISO 10303-21 round-trip architecture

- Status: Active
- Owner: repository maintainer
- Scope and system boundary: the long-lived boundary among ISO 10303-21 parsing/writing, EXPRESS schema compilation, generated .NET types, and schema-bound model navigation
- Applicable product intent: [`docs/product/README.md`](../product/README.md), approved at `5021769553568e25aa8ae8e5b7af39cc2c9b1c82`
- Governing principles: [`docs/principles/architecture.md`](../principles/architecture.md) and [`docs/principles/engineering.md`](../principles/engineering.md), active; dependent delivery records pin the applicable committed revision
- Related ADRs: [`ADR-0001`](../adr/ADR-0001-schema-bound-entity-model.md) and [`ADR-0004`](../adr/ADR-0004-mutable-entities-and-boundary-validation.md), superseded; [`ADR-0002`](../adr/ADR-0002-roslynhelper-source-composition.md), accepted at `5021769553568e25aa8ae8e5b7af39cc2c9b1c82`; [`ADR-0003`](../adr/ADR-0003-aot-ready-schema-validation.md), [`ADR-0005`](../adr/ADR-0005-explicit-section-graph-registration.md), [`ADR-0006`](../adr/ADR-0006-precompiled-schema-package-distribution.md), and [`ADR-0007`](../adr/ADR-0007-separate-part21-lexer-parser-grammars.md), accepted
- Last approved revision: approved by the repository maintainer on 2026-08-26; dependent delivery records pin the committed revision

## Current architecture

### Baseline and standards

The runtime project hosts the complete ISO 10303-21:2016 Edition 3 clear-text syntax parser and the public schema-bound exchange-structure model. The packaged Analyzer hosts the ISO 10303-11:2004 Edition 2 EXPRESS parser, closed-set compiler, and incremental generator. Repository fixtures and a packed consumer prove generated types, atomic read/edit/validate/write/read semantics, while opt-in pinned NIST/buildingSMART corpora independently regress the declared syntax boundary.

The governing standards are:

- [ISO 10303-21:2016 Edition 3](https://www.iso.org/standard/63141.html), confirmed current in 2022, for clear-text exchange-structure syntax and EXPRESS mapping;
- [ISO 10303-11:2004 Edition 2](https://www.iso.org/standard/38047.html), confirmed current in 2025, for EXPRESS data types and constraints.

Clause navigation may use the publicly accessible [ISO 10303-21 Edition 3 final-text mirror](https://www.steptools.com/stds/step/IS_final_p21e3.html), but behavior-changing grammar work must cite an authorized normative copy or an equivalently authoritative source. Repository tests contain original minimal examples rather than copied normative text.

### Dependency direction

```text
EXPRESS AdditionalFile (.exp)
  -> Analyzer: syntax parse -> schema bind -> immutable schema IR
  -> TedToolkit.RoslynHelper structural composition
  -> generated schema contracts, mutable entity classes, value types, and metadata
  -> depends only on TedToolkit.Step21 runtime

ISO 10303-21 input
  -> schema-neutral exchange-structure parser
  -> internal immutable ExchangeStructureSyntax
  -> generated schema metadata binder
  -> complete mutable ExchangeStructure containing generated entity classes and direct entity references
  -> schema-aware writer
  -> ISO 10303-21 output
```

The runtime must not reference the Analyzer, TedToolkit.RoslynHelper, a generated schema, JSON, or XML. The Analyzer may reference ANTLR, Roslyn, and TedToolkit.RoslynHelper. Analyzer implementation dependencies that execute inside the compiler must be packaged beside the analyzer assembly. Generated source must not reference TedToolkit.RoslynHelper.

### Component responsibilities

These are conceptual ownership boundaries; they do not require one assembly per row.

| Component boundary | Owned input and output | Visibility | Allowed dependency direction |
| --- | --- | --- | --- |
| Normative grammar sources | ISO-backed `.g4` sources -> reproducible generated ANTLR artifacts | Repository/build only | Standards evidence -> grammar -> generated parser |
| EXPRESS syntax and closed-set compiler | Complete `.exp` input set -> diagnostics plus immutable bound schema IR | Analyzer internal | Syntax -> binding; never runtime model discovery |
| EXPRESS generation | Valid bound IR -> generated schema contracts, types, validation, and descriptor behavior | Build-time | Bound IR -> RoslynHelper composition -> generated source |
| Generated schema code | Supplied EXPRESS declarations -> public schema types and direct internal mapping behavior | Consumer compile/runtime | Generated code -> runtime contracts only |
| Part 21 syntax and staging | Complete input text -> immutable internal syntax and stage diagnostics | Runtime internal | Grammar artifacts -> syntax -> binding orchestration |
| Schema descriptor bridge | Physical parameters and entities -> allocation, hydration, compatibility, validation, and projection | Public identity; internal dispatch | Runtime -> abstract descriptor contract -> generated overrides |
| Exchange model and values | Bound header, sections, identities, parameters, and generated entities -> mutable `ExchangeStructure` | Runtime public | Schema-neutral runtime contracts only |
| Writer | Valid registered graph and generated projections -> canonical Part 21 text | Runtime internal | Exchange model -> descriptor projection -> buffered output |
| Conformance evidence | Normative fixtures, independent corpora, package consumers, and deployment proofs -> bounded claims | Repository/public documentation | Evidence constrains claims; corpus success does not redefine standards |

No component may bypass a preceding semantic stage by reinterpreting its private syntax or IR. In particular, runtime code does not inspect generated properties to reconstruct schema metadata, generated code does not parse Part 21 text, and public callers do not receive parser or binder state.

### Schema-neutral exchange-structure model

The runtime parser internally owns immutable syntax representations for recognized standard constructs, including:

- exchange structure and header, anchor, reference, data, and signature sections, with unsupported sections preserved or rejected according to the declared conformance level;
- simple and complex entity instances;
- entity instance names, value instance names, constant occurrence names, and resources;
- typed, untyped, omitted, and derived parameters;
- integer, real, string, binary, enumeration, logical, aggregate, and occurrence-reference values;
- source locations, diagnostics, and declared conformance capabilities.

`EntityInstanceName` is a nominal runtime/raw-model value type. It canonicalizes leading zeros for equality, rejects zero, retains enough numeric range to satisfy the standard rather than assuming `long`, and formats with the required `#` prefix. It is not a required property of generated entity APIs: `ExchangeStructure` owns the bidirectional mapping between object identity and entity instance name.

`ExchangeStructureSyntax` is internal parser/binder infrastructure and is absent from public metadata, generated source, and XML documentation. Public read either returns a complete validated mutable `ExchangeStructure` or throws an applicable read-stage exception containing diagnostics or the aggregate validation result. Canonical writing operates on `ExchangeStructure`; it does not promise byte-for-byte preservation of comments, whitespace, spelling, or original entity ordering.

### EXPRESS compiler and schema metadata

The Analyzer converts the EXPRESS parse tree into an immutable semantic IR before generation. The IR resolves schema imports, declarations, defined types, enumerations, selects, aggregates, entity inheritance, redeclarations, explicit/derived/inverse attributes, bounds, optionality, and source locations. Parsing success alone is insufficient; unresolved or contradictory semantics produce Roslyn diagnostics and suppress affected generated declarations.

The compiler inside the Analyzer is one deterministic entry composed from five one-way stages:

| Stage | Owner | Input | Immutable output | May depend on |
| --- | --- | --- | --- | --- |
| Syntax | `ExpressSyntaxStage` | Complete normalized `.exp` source set | `ExpressSyntaxCompilation` with parsed schemas and ordered syntax diagnostics | Parser artifacts and syntax nodes only |
| Closed-set binding | `ExpressClosedSetBindingStage` | Complete syntax compilation | `ExpressBindingCompilation` with a syntax-free `ExpressSchemaCompilation`, ordered diagnostics, and binding-owned declaration syntax consumed by analysis | Syntax output |
| Expression/flow analysis | `ExpressExpressionFlowAnalysisStage` | Closed-set binding compilation | `ExpressAnalyzedCompilation`, typed expressions, flow facts, and token-free `ExpressSemanticRule` lowering input | Binding output |
| Generation planning | `ExpressGenerationPlan` and projection/plan types | Analyzed compilation | Complete immutable value, entity, complex-entity, reachable-rule, failure, and withholding plans | Analysis output and syntax-independent mapping support |
| Source emission | `ExpressSourceEmissionStage` and emitter types | One complete generation plan | Ordered diagnostics and deterministic Roslyn generated sources | Generation plan and RoslynHelper composition |

The dependency direction is exactly syntax -> binding -> analysis -> planning -> emission. Syntax nodes and parser tokens stop at expression/flow analysis; planning and emission consume source spans, bound facts, and `ExpressSemanticRule`, never `ExpressRuleSyntax`, `ExpressTokenSyntax`, or a declaration's retained `Syntax`. Stage handoffs expose get-only state and copy incoming collections. Planning does not call emitter helpers; shared type facts live in `ExpressTypeAnalysis`, while descriptor mapping support lives in `ExpressDescriptorTypeSupport`. There are no approved reverse-dependency, mutable-draft, syntax-leak, or emitter-private-representation exceptions. `ExpressIncrementalGenerator` remains the sole Roslyn entry and only composes compilation, planning, and emission.

Generated schema metadata implements a runtime schema contract and provides:

- normalized schema identity and aliases used by `FILE_SCHEMA`;
- entity descriptors and subtype assignability;
- physical parameter mappings for simple and complex entity instances;
- inherited explicit-attribute order and redeclaration behavior;
- scalar, aggregate, select, defined-type, omitted, and derived mappings;
- allocation and hydration factories that bind raw parameters to generated entity classes in two phases;
- writers that project generated entity classes back to standard parameters without reflection-based property discovery.
- direct, reflection-free validation of entity, schema-population, and validation-reachable EXPRESS rules, with stable constraint traceability.

### Generated type system

Interfaces are the complete and exclusive representation of EXPRESS entity inheritance. Every EXPRESS entity produces both an interface and a mutable class implementation.

- Each generated entity interface extends exactly its EXPRESS supertype interfaces.
- Each generated entity class inherits directly from runtime `abstract class Entity` and implements its own generated interface. It does not inherit another generated entity class. Because the own interface already inherits all schema supertypes, repeating the transitive interface list on the class is unnecessary.
- `Entity` represents the EXPRESS entity category and supplies only behavior common to all entity instances. It exposes neither an entity instance name nor schema attributes and does not introduce a schema supertype.
- Classes for abstract EXPRESS entities are abstract; all instantiable generated entity classes can be `sealed` because schema subtype polymorphism is carried by interfaces rather than class inheritance.
- Inherited schema-defined storage is generated exactly once in each concrete class projection. No arbitrary "primary supertype" exists.
- Explicit attributes are mutable properties. Mandatory members are non-nullable and required for ordinary construction; `OPTIONAL` members are nullable. Setters do not validate and may participate in temporarily invalid multi-step edits. Derived and inverse attributes are not serialized properties; optional computed/navigation APIs may be generated separately only when their semantics are supported.
- EXPRESS defined types produce `readonly record struct` wrappers rather than aliases.
- EXPRESS enumerations produce `readonly record struct` symbol types with generated known values, avoiding C# enum limitations around extensible EXPRESS enumerations.
- EXPRESS selects produce immutable record-based discriminated values with typed construction and exhaustive matching.
- Aggregates use mutable schema-neutral `ExpressList<T>`, `ExpressSet<T>`, `ExpressBag<T>`, and `ExpressArray<T>` values that retain kind, bounds, order, multiplicity, optional slots, and invalid edit candidates until explicit or boundary validation.
- Boolean maps to `bool`; LOGICAL maps to a three-state runtime value; omitted `$` uses an explicit optional representation rather than conflating absence with a default value.

Every generated entity class has a custom, bounded, non-recursive diagnostic string representation. It may show the schema entity name and selected scalar state, but it does not traverse entity relationships, invent instance names, or claim ISO 10303-21 conformance. Neither `Entity.ToString()` nor `ExchangeStructure.ToString()` is a serialization contract. Canonical exchange-structure text and individual entity-instance records are produced only by explicit writer operations that can return diagnostics.

### Direct entity references and two-phase hydration

Generated entity-valued properties expose the schema entity interface directly:

```csharp
public ILoop Bound { get; }
```

No public reference wrapper, identifier property, lazy proxy, service provider, or resolver call is required for ordinary navigation. The binder handles forward and cyclic references in two phases:

1. Allocate every generated entity class and register the raw `EntityInstanceName -> object` mapping without requiring entity-valued properties to be complete.
2. Resolve every raw occurrence name through the completed identity index and assign the actual target object to the generated backing member.
3. Validate required/optional references, target-interface assignability, model membership, and completeness before the typed model is published.

After hydration, cycles are ordinary CLR object-reference cycles. A consumer writes `faceBound.Bound`, and receives the target entity interface directly. Missing or incompatible required targets are binding diagnostics rather than proxy access failures.

ISO 10303-21 defines `PARAMETER` syntax and maps an entity-valued EXPRESS attribute to an instance name; it does not define .NET attributes such as `P21ParameterAttribute` or `P21EntityReferenceAttribute`. No public mapping attribute is required by the first API contract. If a later consumer need justifies generated attributes, they are explicitly library-defined, non-normative metadata and never replace the generated schema descriptor or introduce reflection-based writing.

Two-phase assignment requires generated private hydration machinery or backing fields. Public properties remain strongly typed. The parser does not publish an entity or typed model until all mandatory relationships are bound and validation succeeds. Ordinary construction requires mandatory members, but public setters and aggregate operations intentionally permit temporarily incomplete or schema-invalid edited state; they do not trigger automatic validation.

### Model ownership, construction, and mutation

`ExchangeStructure` is the non-generic C# representation of the standard exchange structure. It owns section context, maps local `EntityInstanceName` values to generated entity objects, and maintains reverse maps using CLR reference identity rather than object value equality. Its header retains the ISO `file_schema.schema_identifiers` list. Each data section retains the one governing `SchemaName` required by clause 11.1; for the single unnamed data-section syntax, that effective name is normalized from the sole `FILE_SCHEMA` entry. The structure owns the binding from those standard schema names to generated schema descriptors. A descriptor is stateless mapping/validation infrastructure for an EXPRESS `SCHEMA`, not an additional value or relationship in the exchange structure, data section, or entity. No domain-specific schema facade is generated. This preserves standard structures involving multiple data sections and schemas without making the exchange-structure root generic. Publicly returned generated objects are complete; parser-only hydration state never crosses the parse-result boundary.

- Parsed entities do not expose their entity instance names; the exchange structure retains that association.
- New entities may be registered with an explicit valid name or through a model allocator after construction.
- Every public graph-registration operation requires an owned `DataSection`; no overload infers a section or changes behavior from the current number of sections. A foreign section is rejected before mutation, new graph members join the selected section, and an already registered entity retains its section membership.
- Replacement, removal, and graph mutation do not automatically run schema validation; later explicit/read/write validation reports dangling, foreign, or incompatible references.
- Model validation detects duplicate names, unresolved references, wrong target types, foreign-model object references, invalid optionality, aggregate constraint violations, schema mismatch, and unsupported mapping cases.

`ExchangeStructure.Validate()` is the side-effect-free validation entry point. It first performs schema-neutral section, identity-index, membership, and reference checks. It then resolves each data section's governing `SchemaName` through the structure-owned descriptor bindings and invokes direct generated code for entity-local and applicable population-level EXPRESS rules. It returns one immutable `ValidationResult`; each `ValidationFailure` contains only a stable `Code`, deterministic string `Path`, caller-facing `Message`, and optional EXPRESS constraint `SourceLocation`. The shared immutable location contains exactly `FilePath`, 1-based `Line`, and 1-based `Column`, with no end span. Every failure invalidates the result, while non-invalidating information remains in the separate diagnostics channel. Read and write boundaries reuse this path and convert an invalid result into their stage-specific aggregate exceptions. No property or collection mutation performs automatic validation.

### ISO 10303-21 writing

Writing is a model operation driven by generated schema metadata, not general object serialization. The writer:

1. validates the selected schema and implemented conformance scope;
2. validates the model-owned object/name identity map and direct entity references across applicable sections;
3. chooses and applies the permitted simple/complex entity mapping from ISO 10303-21 clause 12;
4. emits explicit attributes in the standard-defined physical order, emits `$` for absent OPTIONAL values, handles typed parameters for selects/defined types, and handles required derived markers through metadata;
5. emits deterministic canonical text and reports diagnostics instead of silently dropping unsupported values;
6. supports write-read-write semantic equivalence without promising lexical equivalence.

The standard permits references before definitions and does not require entity instances to be ordered. The default canonical writer orders local entity instances by canonical instance name for reproducibility; this is an output policy, not a semantic requirement.

The delivered operational set is intentionally narrower than the complete Edition 3 document syntax. Anchor resolution, external resource retrieval, and signature verification remain explicit capability failures. Validation executes the supported statically generated validation-reachable EXPRESS closure; it does not expose a general EXPRESS interpreter.

### Public lifecycle and atomic boundaries

| Boundary | Successful result | Failure result | State visible to callers |
| --- | --- | --- | --- |
| EXPRESS generation | A complete collision-free generated schema surface for each valid independent schema | Ordered Roslyn diagnostics; affected invalid schemas are withheld | Diagnostics and complete generated declarations only |
| Part 21 read | A fully allocated, hydrated, and validated mutable `ExchangeStructure` | Syntax, capability, binding, or aggregate validation exception | No syntax graph, hydration state, or partial structure |
| Construction and editing | A mutable graph that may temporarily violate schema rules | Registration and ownership operations reject invalid structural mutations before commit | Public entities, aggregates, sections, and registrations through approved APIs |
| Explicit validation | One immutable ordered `ValidationResult` without mutation | Invalid results contain the complete detected failure set | Validation evidence only; the graph is unchanged |
| Part 21 write | Complete canonical text committed to the caller's `TextWriter` | Capability, projection, or aggregate validation failure before domain-controlled output | No partial library-produced exchange structure |

Destination I/O failures remain owned by the supplied `TextWriter` and can occur while that writer accepts the already validated complete buffer. Atomicity covers library-controlled validation and projection failures; it does not claim transactional behavior from an arbitrary external destination.

### Grammar governance

Grammar is limited to standard syntax. Each behavior-changing grammar proposal must include:

1. the exact ISO edition, clause, and WSN/EXPRESS production or semantic statement;
2. a minimal positive fixture and a neighboring invalid fixture;
3. a statement of whether the rule is lexical/syntactic or belongs in schema binding;
4. regenerated ANTLR outputs produced by the repository script;
5. corpus evidence showing that the narrower normative change does not regress supported files.

Examples of semantic requirements that must not be solved only in `.g4` include `#001 == #1`, uniqueness of occurrence names, type compatibility of a referenced entity, ordering of inherited attributes, aggregate bounds, and schema constraints. Current grammar changes in the working tree, including generic EXPRESS schema names, form part of the baseline but require the same evidence before acceptance.

The split `STEPLexer.g4`/`STEPParser.g4` pair is the audited normative grammar baseline. Its `exchangeFile` start rule recognizes the complete Edition 3 clear-text syntax and consumes EOF. [`ADR-0007`](../adr/ADR-0007-separate-part21-lexer-parser-grammars.md) requires the split while context-bound URI and signature tokenization uses ANTLR lexer modes, which are available only in a lexer grammar. Compatibility fixtures cannot justify syntax that conflicts with the standard.

Complete syntactic recognition does not imply that every optional facility has complete operational semantics in the first release. The raw model and visitor retain all recognized standard section/value forms. External resource retrieval, signature validation, archive transport, ECMAScript execution, and comparable facilities may return explicit unsupported-capability diagnostics until implemented; they are not made syntactically invalid merely because their runtime behavior is staged.

### ANTLR traversal strategy

Parser generation retains `-visitor -no-listener` for both grammars:

- the Part 21 base visitor transforms the parse tree into internal immutable `ExchangeStructureSyntax` for atomic schema binding;
- the EXPRESS base visitor transforms the parse tree into an immutable syntax/semantic IR that is subsequently name-bound and validated;
- application behavior remains outside `.g4` actions, keeping the grammars standard-focused and target-independent;
- generated parser, lexer, visitor, and base-visitor types remain internal implementation details.

Both generated base visitors are consumed by the immutable syntax/IR transformations. Listener generation would duplicate traversal infrastructure without a consumer, so it remains disabled. Generation scripts reproduce and internalize the selected artifacts deterministically.

### TedToolkit.RoslynHelper syntax-object map

| Generated requirement | TedToolkit.RoslynHelper public syntax object |
| --- | --- |
| Source unit and emission | `File()` / `SourceFile.Generate(context, hintName)` |
| Namespace and imports | `NameSpace`, `Using` |
| Entity contracts | `Interface` |
| Entity implementations | `Class` |
| Select wrappers | Sealed `Record` reference type |
| Defined types, enumeration symbols, and standard value representations | `RecordStruct` |
| Schema metadata and factories | `Class`, `Method`, `Constructor`, `Field`, `Property` |
| Generic schema and aggregate constraints | `DataType`, type parameters, and constraint APIs |
| Attributes and generated markers | `SourceComposer<TGenerator>` factories and `Attribute` conversion/composition |
| Construction, validation, and dispatch | object-creation, invocation, return, conditional, assignment, and available structured statement/expression objects |
| Caller-facing XML documentation | description syntax objects |

`Record` in this table is deliberately limited to an immutable SELECT discriminated value. In C#, that declaration is still a reference type, but its structural equality matches value semantics. Generated EXPRESS entities and schema descriptors instead remain ordinary `Class` declarations because they carry mutable/reference or singleton/behavior identity and must not acquire record equality.

Schema names are converted to `DataType` objects; source strings are not concatenated to form declarations, generic types, punctuation, indentation, or directives. Source remains structural until one final `Generate` call per stable, collision-free hint name. The pinned helper has no general loop, lambda, pattern/presence, or switch-expression object. Reviewed custom fragments are therefore limited to three families: the whole-loop fragment for a general EXPRESS `REPEAT`, necessary lambdas, and necessary pattern/presence/switch expressions. Ordinary declarations, locals, assignments, `IF`/`CASE`, returns, and compound statements remain structural. Any additional fragment family requires separate review before generator code is edited.

### Package and deployment view

The `TedToolkit.Step21` NuGet package contains the .NET 10 runtime assembly and packages the .NET Standard 2.0 Analyzer plus its compiler-time dependencies under analyzer assets. Consumer-supplied EXPRESS files are `AdditionalFiles`; generated schema code is compiled into the consumer assembly. Analyzer-only dependencies do not become runtime dependencies of the generated object graph.

The packed-consumer boundary verifies the actual package rather than relying only on project references. It compares emitted generated-source paths and bytes across isolated builds, audits the resolved runtime graph, and publishes and executes a representative `win-x64` Native AOT consumer. The executable runtime identifier is proof scope, not an exclusive supported-platform list.

A family of maintained precompiled schema packages is an accepted extension direction, though it is not yet part of the delivered package topology. [`ADR-0006`](../adr/ADR-0006-precompiled-schema-package-distribution.md) requires one independently versioned optional package per maintained schema baseline, explicit descriptors, pinned provenance, SemVer classification of generated/schema-semantic compatibility, and a bounded dependency on the schema-neutral runtime. Acceptance governs delivery but does not claim that any schema package has been implemented or released.

### Resource ownership and concurrency

The current public read path consumes the complete `TextReader` into memory, temporarily materializes the complete parser representation, then publishes a fully materialized entity graph. It is not a streaming or lazy model. The writer validates and projects the complete graph, buffers all domain-controlled output, and only then calls the destination writer. This full-buffer design supplies atomic library-controlled publication and output at the cost of memory proportional to the input, parser state, object graph, projections, and output that overlap during an operation.

`ExchangeStructure`, generated mutable entities, and mutable EXPRESS aggregates provide no thread-safety or synchronization contract. A caller must not mutate a graph concurrently with validation, registration, removal, projection, or writing. `TextReader` and `TextWriter` lifetime, synchronization, encoding, transport, and destination durability remain caller-owned.

Streaming, lazy entity materialization, parallel graph mutation, or incremental output would change atomicity, identity, reference hydration, validation completeness, and destination-failure behavior. Such a direction requires representative workload evidence and an architecture decision rather than a local optimization. No fixed file-size or memory threshold is claimed until representative STEP/IFC/AP workloads are measured.

### Observable compatibility and determinism

The versioned compatibility surface includes the runtime public API and XML documentation, generated public type shapes for a fixed normalized EXPRESS input set and generator version, documented diagnostic and validation identities, and canonical writer semantics. Repository public-API snapshots protect the cumulative runtime surface. Generated schema public APIs change when their governing EXPRESS inputs or an explicitly versioned mapping contract changes; private parser contexts, syntax nodes, bound IR, emitter organization, hydration backing members, and Roslyn composition are not compatibility surfaces.

For the same normalized inputs, pinned package/tool versions, and declared build environment, observable diagnostics, generated-source names and contents, validation-failure ordering, and canonical Part 21 output are deterministic. Exact NuGet archive or assembly byte reproducibility is not claimed unless a dedicated proof defines and verifies that stronger boundary. Unordered collections, timestamps, machine-specific paths, active network assets, or nondeterministic parallel scheduling must not affect a result declared deterministic.

Compatibility changes must identify the affected surface and migration consequence. A diagnostic wording improvement is distinct from changing a stable code or path; an internal refactor is distinct from changing generated public nullability, inheritance, constructor shape, mapping, or writer semantics.

## Constraints for change design

- Treat ISO 10303-21 and the selected EXPRESS schema as the sole sources of public domain semantics and terminology; introduce no library-defined domain abstraction.
- Use C# conventions only where they preserve the standard/schema meaning without addition, omission, or reinterpretation.
- Keep parsing, diagnostics, schema binding, hydration, and writing machinery visibly separated as implementation infrastructure rather than presenting it as ISO or EXPRESS domain semantics.
- Keep `ExchangeStructureSyntax` internal; public callers observe only complete validated mutable `ExchangeStructure` values or stage-specific failures.
- Preserve the ISO-neutral dependency direction and terminology.
- Expose direct generated entity interfaces on entity-valued properties; keep ISO instance names and reverse object/name lookup model-owned.
- Complete forward and cyclic reference binding through strictly parser-internal two-phase hydration before publishing any entity or typed model.
- Require mandatory properties for ordinary construction and represent `OPTIONAL` through nullability, while allowing public mutable editing to pass through temporarily invalid state until explicit/read/write validation.
- Treat any generated .NET mapping attribute as non-normative library metadata; the first contract does not require one.
- Express all EXPRESS entity inheritance through generated interfaces; generated mutable entity classes inherit only `Entity`, implement their own interface, and use reference identity.
- Every instantiable generated entity class is sealed; immutable defined-type and enumeration value wrappers may remain record structs where semantically appropriate.
- Provide ISO 10303-21 writing and semantic round-trip only; do not introduce JSON/XML dependencies or contracts.
- Keep `ToString()` non-recursive and diagnostic-only; route ISO 10303-21 text through explicit writer operations with diagnostics.
- Represent the document root as non-generic `ExchangeStructure`; do not assume one schema through a generic root type.
- Bind and validate every data section under its ISO governing `SchemaName`, including valid multi-schema populations and cross-schema references by final delivery.
- Require an owned `DataSection` on every public graph-registration operation; do not expose a section-omitting Add overload or infer a default section.
- Generate direct validation and reference enumeration without runtime schema reflection, dynamic code, or assembly scanning; prove runtime/generated paths through Native AOT publication and execution.
- Retain ANTLR visitors, suppress listeners, and use the visitors as the sole parse-tree transformation boundary.
- Generate source structurally with TedToolkit.RoslynHelper 2026.7.15 or a separately reviewed compatible version, and package its analyzer-time dependency without leaking it to generated code.
- Reject unsupported or invalid mappings with source-located diagnostics.
- Do not change grammar without clause-level evidence and focused conformance fixtures.
- Target the complete ISO 10303-21 Edition 3 clear-text grammar; stage advanced operational semantics through explicit capability diagnostics rather than narrowing valid syntax.
- Do not claim full schema conformance until supported EXPRESS constraints are evaluated.
- Keep syntax recognition, implemented operational semantics, and independently observed interoperability as separate evidence-backed claims.
- Preserve complete-result publication for generation and read, side-effect-free aggregate validation, and pre-output validation/projection for writing.
- Account explicitly for the current full-input, full-graph, and full-output buffering model when a change affects scale or latency.
- Do not introduce a thread-safety guarantee or concurrent mutation path without defining ownership, synchronization, atomicity, and validation consequences.
- Classify runtime API, generated API, diagnostic identity, and canonical-output compatibility before changing an observable contract.
- Keep declared deterministic outputs independent of unordered iteration, timestamps, machine paths, active network assets, and scheduling.

## Decision links and exceptions

- ADR-0001 is superseded by ADR-0004, which is superseded by ADR-0005.
- ADR-0002 records the maintainer-mandated TedToolkit.RoslynHelper source-composition direction.
- ADR-0003 selects the AOT-ready minimal validation contract and direct generated execution.
- ADR-0005 retains mutable reference-identity entities, exchange-structure-owned occurrence names, reflection-free `DirectReferences`, and boundary validation while requiring explicit data-section selection for graph registration.
- ADR-0006 requires one independently versioned optional package per maintained precompiled EXPRESS schema baseline, with explicit descriptor selection, provenance, compatibility classification, and bounded core-runtime dependency.
- ADR-0007 requires separate target-independent lexer and parser grammars while Part 21 URI and signature tokenization depends on ANTLR lexer modes.
- No principle exceptions are proposed.

## Review triggers

- ISO 10303-21 or ISO 10303-11 is revised or superseded.
- A valid EXPRESS inheritance or mapping construct cannot be represented by the interface/class metadata model.
- A consumer requires proxy/lazy reference resolution or non-ISO serialization.
- Generated code would need to reference generator-only dependencies.
- TedToolkit.RoslynHelper cannot structurally express a required declaration with an acceptably small, reviewed extension.
- A conformance claim expands to anchors, external references, signatures, archives, or full EXPRESS rule execution.
- Representative files make complete input, parser, graph, projection, or output materialization operationally unacceptable.
- A consumer requires concurrent mutation, lazy entity materialization, streaming read, or incremental write.
- A change breaks a protected runtime/generated API, diagnostic identity, or canonical semantic-output contract.
- An observable result declared deterministic varies with iteration order, machine state, network state, or scheduling.
- A maintained precompiled schema package is accepted, combined with another schema family, or made dynamically discoverable.
