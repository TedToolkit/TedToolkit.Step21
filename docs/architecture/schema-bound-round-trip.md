# Schema-bound ISO 10303-21 round-trip architecture

- Status: Active
- Owner: repository maintainer
- Scope and system boundary: the long-lived boundary among ISO 10303-21 parsing/writing, EXPRESS schema compilation, generated .NET types, and schema-bound model navigation
- Applicable product intent: [`docs/product/README.md`](../product/README.md), approved at `5021769553568e25aa8ae8e5b7af39cc2c9b1c82`
- Governing principles: [`docs/principles/architecture.md`](../principles/architecture.md) and [`docs/principles/engineering.md`](../principles/engineering.md), active; dependent delivery records pin the applicable committed revision
- Related ADRs: [`ADR-0001`](../adr/ADR-0001-schema-bound-entity-model.md) and [`ADR-0004`](../adr/ADR-0004-mutable-entities-and-boundary-validation.md), superseded; [`ADR-0002`](../adr/ADR-0002-roslynhelper-source-composition.md), accepted at `5021769553568e25aa8ae8e5b7af39cc2c9b1c82`; [`ADR-0003`](../adr/ADR-0003-aot-ready-schema-validation.md) and [`ADR-0005`](../adr/ADR-0005-explicit-section-graph-registration.md), accepted
- Last approved revision: approved by the repository maintainer on 2026-08-21; dependent delivery records pin the committed revision

## 📝 Clarification and decision log

| ID | Question and why it mattered | Recommended answer | User decision and source | Affected sections | Status |
| --- | --- | --- | --- | --- | --- |
| AD-01 | Whether the core follows ISO 10303-21 or a domain format | Follow ISO 10303-21 | The maintainer confirmed the repository-wide ISO scope on 2026-08-21 | All boundaries and terminology | Resolved |
| AD-02 | Which serialization contract generated objects require | ISO 10303-21 writing only | The maintainer excluded JSON/XML and their extension dependencies on 2026-08-21 | Runtime model, writer, non-goals | Resolved |
| AD-03 | How generated C# is composed | Use TedToolkit.RoslynHelper structurally | The maintainer mandated TedToolkit.RoslynHelper on 2026-08-21; NuGet availability through version 2026.7.15 was verified the same day | Generator boundary and packaging | Resolved |
| AD-04 | How generated declarations represent EXPRESS entities and inheritance | Generate one interface and one mutable reference-identity class for every EXPRESS entity; express all schema inheritance through interfaces; generated classes inherit only runtime `Entity` | The maintainer originally selected records, then retained mutable classes through ADR-0005 on 2026-08-21 | Generated model shape | Resolved |
| AD-05 | Whether generated entity properties expose references or entities | Expose the generated entity interface directly and bind it in a second phase | The maintainer rejected `EntityRef<TEntity>` on 2026-08-21 and selected ordinary C# references plus property attributes when mapping metadata is useful | Reference and hydration boundary | Resolved |
| AD-06 | Whether incomplete hydration is visible to ordinary consumers | Keep incomplete state strictly parser-internal and publish only fully bound models | The maintainer required on 2026-08-21 that users never observe a temporarily incomplete entity | Construction and public API | Resolved |
| AD-07 | Which authority governs public concepts and C# representation choices | ISO 10303-21 and the selected EXPRESS schema define the semantics; C# idioms choose only among equivalent representations | The maintainer established the ISO-first, C#-second rule and prohibited library-invented domain concepts on 2026-08-21 | Terminology, generated API, runtime infrastructure, and review gates | Resolved |
| AD-08 | Whether `ToString()` is an ISO serialization entry point | No; generate bounded non-recursive diagnostic strings and keep conforming text output on an explicit writer | The maintainer accepted the separation on 2026-08-21 | Generated records, diagnostics, and writing | Resolved |
| AD-09 | Whether generated ANTLR visitors are retained | Retain visitors and suppress listeners; visitors perform parse-tree-to-model/IR transformation | The maintainer requested review on 2026-08-21; repository use and the planned immutable transformations support retaining them | Parser generation and semantic pipeline | Resolved |
| AD-10 | Whether the exchange-structure root is generic | Use the non-generic standard concept `ExchangeStructure`; schema binding belongs to sections, populations, descriptors, and entity types | The maintainer accepted this correction on 2026-08-21 | Runtime root model and writing | Resolved |
| AD-11 | Whether the Part 21 grammar targets a subset or the complete Edition 3 syntax | Target the complete ISO 10303-21 Edition 3 clear-text grammar while delivering advanced runtime semantics in declared stages | The maintainer approved this boundary on 2026-08-21 | Grammar baseline, raw model, diagnostics, testing, and estimates | Resolved |
| AD-12 | Whether the schema-free parse graph is a public exchange-structure model | No; use internal immutable `ExchangeStructureSyntax` solely for parse/bind staging and publish only complete validated mutable `ExchangeStructure` values | The maintainer explicitly prohibited consumer access to the syntax representation on 2026-08-21 | Parser/binder boundary, public API, atomicity, and diagnostics | Resolved |
| AD-13 | How graph registration selects a destination data section | Require an owned `DataSection` on every public Add operation and provide no section-omitting overload | The maintainer required omission of the section to be a compile-time error and approved the resulting minimal strong API on 2026-08-21 | Model ownership, registration, multi-section editing, and public API | Resolved |

## Current architecture

### Baseline and standards

The runtime project currently hosts an ANTLR parser for a subset of ISO 10303-21 exchange structures. The Analyzer project hosts an ANTLR EXPRESS parser and is packaged beside the runtime. Tests currently prove parser completion against small repository fixtures and opt-in NIST/buildingSMART corpora; they do not yet prove a public semantic model, generated types, or writing.

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

`ExchangeStructure.Validate()` is the side-effect-free validation entry point. It first performs schema-neutral section, identity-index, membership, and reference checks. It then resolves each data section's governing `SchemaName` through the structure-owned descriptor bindings and invokes direct generated code for entity-local and applicable population-level EXPRESS rules. It returns one immutable `ValidationResult`; each `ValidationFailure` contains only a stable `Code`, deterministic string `Path`, caller-facing `Message`, and optional EXPRESS constraint `SourceLocation`. Every failure invalidates the result, while non-invalidating information remains in the separate diagnostics channel. Read and write boundaries reuse this path and convert an invalid result into their stage-specific aggregate exceptions. No property or collection mutation performs automatic validation.

### ISO 10303-21 writing

Writing is a model operation driven by generated schema metadata, not general object serialization. The writer:

1. validates the selected schema and implemented conformance scope;
2. validates the model-owned object/name identity map and direct entity references across applicable sections;
3. chooses and applies the permitted simple/complex entity mapping from ISO 10303-21 clause 12;
4. emits explicit attributes in the standard-defined physical order, emits `$` for absent OPTIONAL values, handles typed parameters for selects/defined types, and handles required derived markers through metadata;
5. emits deterministic canonical text and reports diagnostics instead of silently dropping unsupported values;
6. supports write-read-write semantic equivalence without promising lexical equivalence.

The standard permits references before definitions and does not require entity instances to be ordered. The default canonical writer orders local entity instances by canonical instance name for reproducibility; this is an output policy, not a semantic requirement.

The first schema-bound delivery may state a narrower implemented conformance set than the full Edition 3 document structure. Anchor resolution, external resource retrieval, signatures, archive handling, and full EXPRESS constraint execution remain separate capabilities until implemented; their absence must be visible in capability metadata and diagnostics.

### Grammar governance

Grammar is limited to standard syntax. Each behavior-changing grammar proposal must include:

1. the exact ISO edition, clause, and WSN/EXPRESS production or semantic statement;
2. a minimal positive fixture and a neighboring invalid fixture;
3. a statement of whether the rule is lexical/syntactic or belongs in schema binding;
4. regenerated ANTLR outputs produced by the repository script;
5. corpus evidence showing that the narrower normative change does not regress supported files.

Examples of semantic requirements that must not be solved only in `.g4` include `#001 == #1`, uniqueness of occurrence names, type compatibility of a referenced entity, ordering of inherited attributes, aggregate bounds, and schema constraints. Current grammar changes in the working tree, including generic EXPRESS schema names, form part of the baseline but require the same evidence before acceptance.

The current `STEP.g4` is a prototype subset rather than the normative grammar baseline: its root currently requires one header and one data section, omits Edition 3 section forms, and contains token rules whose accepted languages do not exactly match the ISO WSN. Delivery therefore begins with a production-by-production audit against ISO 10303-21 Table 2, Table 3, and the relevant semantic clauses. The resulting grammar targets the complete Edition 3 clear-text syntax, and its start rule consumes the complete exchange structure and end of input. Compatibility fixtures cannot justify syntax that conflicts with the standard.

Complete syntactic recognition does not imply that every optional facility has complete operational semantics in the first release. The raw model and visitor retain all recognized standard section/value forms. External resource retrieval, signature validation, archive transport, ECMAScript execution, and comparable facilities may return explicit unsupported-capability diagnostics until implemented; they are not made syntactically invalid merely because their runtime behavior is staged.

### ANTLR traversal strategy

Parser generation retains `-visitor -no-listener` for both grammars:

- the Part 21 base visitor transforms the parse tree into internal immutable `ExchangeStructureSyntax` for atomic schema binding;
- the EXPRESS base visitor transforms the parse tree into an immutable syntax/semantic IR that is subsequently name-bound and validated;
- application behavior remains outside `.g4` actions, keeping the grammars standard-focused and target-independent;
- generated parser, lexer, visitor, and base-visitor types remain internal implementation details.

The visitors are currently unconsumed generated scaffolding, but they are required by the approved transformation pipeline. Listener generation would duplicate traversal infrastructure without a planned consumer, so it remains disabled. The generation scripts already select the correct artifacts; implementation adds reproducibility assertions rather than changing to listener generation.

### TedToolkit.RoslynHelper syntax-object map

| Generated requirement | TedToolkit.RoslynHelper public syntax object |
| --- | --- |
| Source unit and emission | `File()` / `SourceFile.Generate(context, hintName)` |
| Namespace and imports | `NameSpace`, `Using` |
| Entity contracts | `Interface` |
| Entity implementations and select wrappers | `Record` |
| Defined types, enumeration symbols, and standard value representations | `RecordStruct` |
| Schema metadata and factories | `Class`, `Method`, `Constructor`, `Field`, `Property` |
| Generic schema and aggregate constraints | `DataType`, type parameters, and constraint APIs |
| Attributes and generated markers | `SourceComposer<TGenerator>` factories and `Attribute` conversion/composition |
| Construction, validation, and dispatch | object-creation, invocation, return, conditional, loop, and switch syntax objects |
| Caller-facing XML documentation | description syntax objects |

Schema names are converted to `DataType` objects; source strings are not concatenated to form declarations, generic types, statements, punctuation, indentation, or directives. Source remains structural until one final `Generate` call per stable, collision-free hint name. No custom source fragment is currently expected. If a required C# construct is unsupported by the pinned helper version, the design must be revised or the smallest fragment explicitly approved before generator code is edited.

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

## Decision links and exceptions

- ADR-0001 is superseded by ADR-0004, which is superseded by ADR-0005.
- ADR-0002 records the maintainer-mandated TedToolkit.RoslynHelper source-composition direction.
- ADR-0003 selects the AOT-ready minimal validation contract and direct generated execution.
- ADR-0005 retains mutable reference-identity entities, exchange-structure-owned occurrence names, reflection-free `DirectReferences`, and boundary validation while requiring explicit data-section selection for graph registration.
- No principle exceptions are proposed.

## Review triggers

- ISO 10303-21 or ISO 10303-11 is revised or superseded.
- A valid EXPRESS inheritance or mapping construct cannot be represented by the interface/class metadata model.
- A consumer requires proxy/lazy reference resolution or non-ISO serialization.
- Generated code would need to reference generator-only dependencies.
- TedToolkit.RoslynHelper cannot structurally express a required declaration with an acceptably small, reviewed extension.
- A conformance claim expands to anchors, external references, signatures, archives, or full EXPRESS rule execution.
