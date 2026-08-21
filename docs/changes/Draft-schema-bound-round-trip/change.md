# Generate and round-trip schema-bound ISO 10303-21 models

## 📌 Status

Draft

- Approval evidence: pending maintainer approval of ADR-0001, this behavioral contract, and priority.

## 📝 Clarification and decision log

| ID | Question and why it mattered | Recommended answer | User decision and source | Affected sections | Status |
| --- | --- | --- | --- | --- | --- |
| CD-01 | Which serialization contract is required | ISO 10303-21 semantic writing only | The maintainer excluded JSON/XML and their extension dependencies on 2026-08-21 | Goal, scope, cases, completion | Resolved |
| CD-02 | Whether one application protocol defines supported types | Keep runtime generic and compile supplied EXPRESS schemas | The maintainer selected repository-wide ISO 10303-21 scope on 2026-08-21 | Goal, scope, compatibility | Resolved |
| CD-03 | Which generator composition library to use | TedToolkit.RoslynHelper | The maintainer required it on 2026-08-21 | Planned approach, constraints | Resolved |
| CD-04 | Whether generated entity properties expose wrappers or entities | Expose direct generated entity interfaces and bind them in a second phase | The maintainer rejected `EntityRef<TEntity>` and selected direct C# references with optional mapping attributes on 2026-08-21 | Goal, scope, behavior cases, completion | Resolved |
| CD-05 | Whether incomplete objects may be publicly constructed or observed | Keep incomplete hydration strictly parser-internal and require complete public construction | The maintainer required on 2026-08-21 that users never observe temporarily missing mandatory properties | Public construction, nullability, failure behavior | Resolved |
| CD-06 | Delivery priority | P2 because this is planned public capability without an external deadline | Pending | Priority and directory | Open |
| CD-07 | Whether C# convenience may add concepts beyond ISO/EXPRESS | No; use idiomatic C# only for semantically equivalent representation choices | The maintainer established ISO semantics first and C# idioms second on 2026-08-21 | Scope, principles, generated API, and completion | Resolved |
| CD-08 | Whether generated ANTLR visitors are needed | Retain visitors for immutable transformation and continue suppressing listeners | The maintainer requested review on 2026-08-21 and accepted plan adjustment based on need | Planned approach, scripts, and verification | Resolved |
| CD-09 | How `ToString()` and ISO writing are separated | Custom non-recursive diagnostics on records; explicit writer for ISO text | The maintainer accepted the separation on 2026-08-21 | Generated API, writing, and cases | Resolved |
| CD-10 | Whether the exchange-structure root carries one schema as a generic argument | No; use non-generic `ExchangeStructure` to preserve the standard's multi-section/multi-schema possibilities | The maintainer accepted the correction on 2026-08-21 | Runtime root and binder | Resolved |
| CD-11 | Whether `.g4` recognizes only the first runtime subset or complete Edition 3 syntax | Recognize the complete ISO 10303-21 Edition 3 clear-text grammar and stage advanced operational semantics separately | The maintainer approved this boundary on 2026-08-21 | Scope, approach, cases, risks, and estimate | Resolved |

## 🚦 Change priority

- Priority: Unknown
- Rationale: the capability is material and public, but no outage, deadline, or release commitment was stated. Recommended priority is P2.
- Directory: `docs/changes/Draft-schema-bound-round-trip/`; rename to `P2-schema-bound-round-trip` if the recommendation is approved.

## 🎯 Change goal

> When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.

## 🎯 Intended outcome and scope

The delivery adds a public schema-neutral exchange-structure model and writer, an EXPRESS semantic compiler, an incremental generator, immutable generated schema types and metadata, a schema-bound model, typed entity-reference resolution, and diagnostics sufficient for structural ISO 10303-21 data-section mapping.

In scope:

- complete ISO 10303-21 Edition 3 clear-text syntactic recognition, an immutable raw representation of recognized standard constructs, and canonical writing for the constructs in the declared writer capability set;
- arbitrary valid EXPRESS schema identifiers and the structural declarations needed for entities, inheritance, defined types, enumerations, selects, aggregates, optionality, and physical attribute mapping;
- one interface and one record per EXPRESS entity, with all schema inheritance expressed by interfaces and every generated entity record inheriting only `Entity`;
- model-owned `EntityInstanceName` identity, direct generated entity properties, two-phase forward/cyclic reference hydration, construction, replacement, validation, and deterministic identity-based writing;
- simple and complex entity instance mappings supported by the implemented EXPRESS subset;
- source-located parser, schema, binding, resolution, and writer diagnostics;
- internal ANTLR visitors that transform Part 21 parse trees to `ExchangeStructure` and EXPRESS parse trees to immutable schema IR, with listeners suppressed;
- deterministic source composition through TedToolkit.RoslynHelper;
- semantic round-trip verification against repository fixtures and independently pinned corpora.

Non-goals for this change:

- JSON, XML, ORM, database, or general object serialization and related extension points;
- domain-specific STEP application protocol, IFC, CAD, or BIM APIs;
- byte-for-byte preservation of comments, whitespace, line wrapping, leading-zero spelling, or source ordering;
- evaluating general EXPRESS functions, procedures, WHERE rules, UNIQUE rules, or derived expressions;
- operational resolution of external resources, digital-signature creation/verification, compressed archive transport, ECMAScript execution, or a claim that every syntactically recognized Edition 3 facility already has complete runtime semantics;
- public reference/ID wrappers, lazy proxies, or resolver state attached to generated records.
- public constructors, builders, setters, or parse results that expose temporarily incomplete entities.
- treating library-defined .NET mapping attributes as ISO-standard constructs or using reflection as the writer's schema source.

Compatibility expectations:

- existing parser consumers remain source-compatible where current generated parser visibility permits;
- generated ANTLR files remain reproducible and are never edited manually;
- generated schema code references only public runtime contracts;
- unsupported standard or schema features produce diagnostics and are not silently accepted as conforming.

## 🧾 Source intent and hard constraints

- Source request: maintainer design direction in this task on 2026-08-21.
- User outcome: a strongly typed, low-friction, read/write ISO 10303-21 environment generated from EXPRESS.
- External hard constraints: ISO 10303-21:2016, ISO 10303-11:2004, C# single class inheritance, Analyzer .NET Standard 2.0 compatibility, LGPL-3.0 repository licensing, and the explicit absence of JSON/XML contracts.

## 🧩 Governing principles and decisions

- Applicable product intent: [`docs/product/README.md`](../../product/README.md), approved in the working tree; full Git revision pending.
- Applicable principles: [`AP-001`, `AP-002`, `AP-003`, `EP-001`, and `EP-002`](../../principles/README.md), active in the working tree; full Git revision pending.
- Related accepted ADR: [`ADR-0002`](../../adr/ADR-0002-roslynhelper-source-composition.md), accepted by explicit maintainer direction; commit pin pending.
- Related proposed ADR: [`ADR-0001`](../../adr/ADR-0001-schema-bound-entity-model.md), acceptance required before this change can be approved.
- Related architecture record: [`schema-bound-round-trip.md`](../../architecture/schema-bound-round-trip.md), Draft.
- Reapproval trigger: any governing record changes, the standard baseline changes, or generated records acquire a non-ISO serialization contract.

Resulting constraints:

- ISO 10303-21 and the selected EXPRESS schema are the only sources of public domain semantics; C# conventions may improve representation but may not add, remove, or reinterpret a concept.
- Parser, binder, hydration, diagnostic, metadata, and writer APIs are explicitly implementation infrastructure and do not appear as invented domain relationships or supertypes.
- The runtime is ISO- and schema-neutral; generated code depends on the runtime, never the reverse.
- Interfaces alone define EXPRESS inheritance and assignability; generated entity records inherit only `Entity` and implement their own interface.
- Entity-valued properties expose the generated entity interface directly; the model owns instance names and performs two-phase hydration before publication.
- Writing is schema-aware and diagnostic; invalid/unsupported mappings are not guessed.
- Grammar behavior changes require clause-level evidence and focused fixtures.
- The grammar recognizes complete Edition 3 clear-text syntax; unsupported advanced runtime behavior is reported as a capability diagnostic instead of rejecting standard syntax.
- TedToolkit.RoslynHelper structurally composes and finally emits all generator output.

## 🧭 Planned approach

The existing grammar/parser foundation is first reconciled production by production with the complete ISO 10303-21 Edition 3 clear-text WSN. The resulting visitor creates schema-neutral immutable exchange-structure values for every recognized standard syntactic form. Advanced operational facilities may remain explicitly unsupported after parsing. The Analyzer reads `.exp` AdditionalFiles, converts ANTLR trees into a bound immutable schema IR, and reports syntax/semantic diagnostics. An incremental generator converts the IR through TedToolkit.RoslynHelper into entity interfaces, immutable records, record structs, schema descriptors, factories, and writers.

The Part 21 ANTLR visitor transforms a complete parse tree into a non-generic `ExchangeStructure`. Generated schema metadata then binds raw data-section records: it first allocates and registers all generated entity objects, after which generated hydration code resolves raw occurrence names and assigns the actual objects to direct entity-valued properties. The writer validates the exchange structure and uses its reference-identity reverse map to recover occurrence names before reversing the generated physical mappings into canonical ISO 10303-21 text.

The EXPRESS ANTLR visitor separately transforms schema parse trees into immutable IR. Name binding, inheritance closure, redeclaration analysis, and physical mapping are later semantic stages and do not become grammar actions.

Incomplete records exist only inside the parser's generated hydration boundary. A successful typed parse publishes fully bound entities. Public construction requires every mandatory property at construction time; only EXPRESS `OPTIONAL` attributes may remain null in a complete public entity.

The current two-package direction is retained unless implementation evidence proves a third public package is necessary: the runtime owns public contracts; the Analyzer owns EXPRESS compilation and generation. Generator-time dependencies are packed alongside the Analyzer and never leak into generated code.

Alternatives rejected:

- generating directly from ANTLR contexts, because parsing does not resolve schema semantics or physical attribute mappings;
- reference wrappers or lazy proxies, because they violate the selected ordinary C# property shape; model-owned two-phase hydration preserves instance identity without exposing them;
- text-template generation or raw Roslyn SyntaxFactory, because TedToolkit.RoslynHelper is a hard constraint;
- domain-specific models, because they violate the approved product boundary.

Target delivery artifacts: production code, generated code, grammar only where normative evidence requires it, tests and fixtures, package/build configuration, and consumer/maintainer documentation.

ANTLR generation continues to use `-visitor -no-listener` in both platform scripts. The scripts gain checks that the expected parser, lexer, visitor, and base-visitor artifacts were regenerated and that listener artifacts were not emitted. Generated files remain derived artifacts and are never edited directly.

## 🔀 Delivery disposition and operational handoffs

- Target delivery artifacts: code, tests, configuration, build automation, and documentation.

| External operational handoff | Owner | Completion evidence | Required before change closure? |
| --- | --- | --- | --- |
| None | N/A | N/A | No |

## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-01 | A valid `.exp` AdditionalFile with a non-IFC schema name | Build the consumer project | Deterministic schema types and metadata are generated without a schema-name whitelist |
| BC-02 | An EXPRESS syntax or name-binding error | Build the consumer project | A source-located diagnostic identifies the schema error and affected invalid output is not emitted |
| BC-03 | An EXPRESS entity with a single inheritance chain | Inspect/use generated declarations | One interface and one record are generated per entity; interface inheritance preserves assignability and each instantiable record is sealed and derives only from `Entity` |
| BC-04 | An EXPRESS entity with multiple supertypes | Inspect/use generated declarations | Its interface inherits every direct supertype interface, its record implements its own interface, storage is not duplicated, and no primary record base is selected |
| BC-05 | A defined type, enumeration, select, or aggregate | Compile and construct generated values | Strong record-based values preserve the schema distinction and invalid alternatives/constraints are rejected or diagnosed |
| BC-06 | A data section references `#n` before its definition or participates in a cycle | Read the model and access the generated property | Two-phase binding completes, and the property returns the actual target entity interface without a wrapper, proxy, or resolver call |
| BC-06A | Consumer code constructs a generated entity | Call its public construction API | Every mandatory scalar and entity-valued property is required; no temporarily incomplete entity can be obtained |
| BC-07 | A reference target exists but is not assignable to the generated property interface | Bind the model | The binder reports an incompatible-target diagnostic and does not publish a falsely complete typed model |
| BC-08 | A reference is missing or an external reference is unsupported | Bind or validate it | The outcome distinguishes missing from unresolved-external and prevents conforming write claims |
| BC-09 | A simple entity has explicit and absent OPTIONAL attributes | Read then write it | Generated properties receive typed values/absence, and output parameters retain standard order with `$` for absence |
| BC-10 | A supported subtype/complex entity mapping contains inherited or redeclared attributes | Read then write it | Generated metadata applies the clause-12 mapping and physical parameter order without reflection guessing |
| BC-11 | A new complete entity record is registered through the model API | Write the model | A unique valid entity instance name is assigned, and direct references write using the model's reverse identity map |
| BC-12 | A record is replaced with `with` and reinserted into the model | Validate and write | Identity remains stable, affected constraints are revalidated, and the changed value is serialized |
| BC-13 | `FILE_SCHEMA` does not match the generated schema metadata | Bind or write the model | A schema-mismatch diagnostic prevents accidental binding/writing under the wrong schema |
| BC-14 | A valid supported model is written and read again | Compare schema-bound semantics | Entity identities, types, values, aggregate semantics, and references are equivalent despite canonical formatting differences |
| BC-15 | A duplicate instance name, foreign-model entity object, invalid aggregate, unsupported mapping, or unresolved reference remains | Attempt conforming write | The writer returns diagnostics and does not silently emit output claimed to conform |
| BC-16 | The same `.exp` and options are built repeatedly | Compare generated sources | Hint names and generated C# are deterministic and compile without RoslynHelper references in consumer output |
| BC-17 | A proposed `.g4` behavior change has corpus evidence but no ISO clause evidence | Review or implement the change | The change is rejected or explicitly routed through an accepted compatibility-extension ADR |
| BC-18 | A consumer looks for JSON/XML support or dependencies | Inspect generated/runtime APIs and package graph | No such serialization contract, adapter hook, attribute, or dependency is present |
| BC-19 | A generated public type or member is proposed without an ISO 10303-21 or selected EXPRESS-schema source | Review generated API and metadata | The proposal is rejected or reclassified and isolated as explicitly named implementation infrastructure |
| BC-20 | Regenerate both ANTLR grammars on Windows or Unix | Run the repository generation script | Parser, lexer, visitor, and base visitor are deterministic and internal; no listener is emitted |
| BC-21 | Parse a valid complete exchange structure with all section forms in the declared conformance scope, followed by trailing non-separator input | Invoke the normative start rule | The valid structure reaches EOF; trailing input and invalid section order are rejected according to the cited WSN |
| BC-22 | Call `ToString()` on an entity graph containing cycles | Format any generated entity | A bounded diagnostic string is returned without graph recursion; no ISO serialization claim is made |
| BC-23 | Request an entity-instance record or complete exchange-structure string | Invoke the explicit writer with an `ExchangeStructure` | Context-dependent names and references are emitted canonically, or diagnostics explain why writing failed |
| BC-24 | A syntactically valid Edition 3 structure uses an advanced section whose operational semantics are not delivered yet | Parse it through the normative start rule and request the unsupported operation | Parsing preserves the standard structure; the later operation returns an explicit unsupported-capability diagnostic rather than a syntax error |

## ✅ Completion criteria

- The public runtime exposes schema-neutral immutable exchange-structure values, model-owned identity, two-phase direct-reference hydration, model validation, and ISO 10303-21 writing for the declared supported scope.
- A valid representative EXPRESS schema produces interfaces, records/record structs, and metadata that compile deterministically.
- Every behavior case has automated observable evidence, including compile-time generator verification and semantic read-write-read coverage.
- Simple and supported complex mappings follow cited ISO clause behavior for parameter form and order.
- Generated entity-valued properties expose entity interfaces directly; no public `EntityRef<TEntity>`, numeric ID relationship, lazy proxy, or resolver call is needed for navigation.
- Temporarily incomplete entities are confined to parser internals; every public constructor and successful parse result exposes complete mandatory state.
- Invalid schemas, wrong/missing references, schema mismatch, foreign-model object references, unsupported mappings, and invalid models produce stable diagnostics and prevent false conformance claims.
- TedToolkit.RoslynHelper is centrally pinned, used structurally through final emission, packaged for analyzer execution, and absent from generated/runtime consumer dependencies.
- Grammar behavior changes included in delivery have clause citations, focused positive/negative fixtures, regenerated ANTLR output, and corpus regression evidence.
- The Part 21 grammar has production-level traceability to the complete Edition 3 clear-text WSN and accepts each valid section form independently of staged operational support.
- README and public API documentation state the ISO 10303-21 scope and the exact implemented conformance boundary.
- Every public generated domain type/member has a traceable ISO 10303-21 or selected EXPRESS-schema source; library infrastructure is not presented as domain semantics.
- The Part 21 and EXPRESS parse-tree transformations use generated visitors; generation scripts verify visitor presence and listener absence on both supported script platforms.
- `Entity.ToString()` and `ExchangeStructure.ToString()` are bounded diagnostics; only explicit writer APIs emit ISO 10303-21 text.
- Release build and fast tests complete with zero errors and zero warnings; opted-in pinned corpus tests pass for the declared supported scope.

## ⏱️ Workload estimate

- Person-month basis: one full-time month of an experienced .NET/Roslyn developer familiar with parser and type-system work; no conversion to calendar days is assumed.
- Change delivery range: 3.5–6.0 person-months.
- Coordination, verification, migration, and rollout allowance: 0.5–1.0 person-months.
- Contingency: 1.5–2.5 person-months for normative grammar reconstruction, EXPRESS inheritance/redeclaration, complex mapping, and helper API gaps.
- Total planning range: 5.5–9.5 person-months.
- Confidence: Low.
- Assumptions and excluded work: includes complete Edition 3 clear-text syntactic recognition but excludes general EXPRESS rule execution, complete operational anchor/reference/signature/archive behavior, performance promises, and non-ISO serializers; assumes the EXPRESS grammar remains a usable foundation while the Part 21 grammar requires substantial normative reconciliation.
- Re-estimation trigger and approval threshold: reassess after binding and round-tripping one representative multi-inheritance schema plus one NIST data file; return for approval if the total estimate changes by more than 30% or any excluded standard capability becomes required.

## 🚧 Design blockers

| ID | Blocking item | Blocks | Next action | Status |
| --- | --- | --- | --- | --- |
| PB-02 | Change priority is unknown | Stable change directory and approval | Maintainer accepts P2 or supplies another priority | Open |
| PB-03 | Approved governing records are not committed and therefore cannot be pinned by full Git SHA | Approval-ready traceability | Commit through the repository's authorized workflow, then pin revisions | Open |

## ⚠️ Risks and coordination

| Item | Impact | Next action |
| --- | --- | --- |
| EXPRESS inheritance/redeclaration is flattened incorrectly into a generated record | Duplicated storage or incorrect write-back | Derive assignability from interfaces and verify flattened physical mappings with normative conformance cases |
| Current grammar accepts/rejects constructs for corpus compatibility rather than normative syntax | False conformance claims | Apply EP-001 evidence gate to every grammar delta |
| Incomplete hydrated records participate in value equality or hashing | Hash instability and invalid observable state | Use reference identity inside the model and do not publish records until hydration completes |
| TedToolkit.RoslynHelper API lacks a required construct | Generator stalls or falls back to unsafe strings | Validate the syntax-object map before implementation and revise design for any custom fragment |
| Writer validates structure but not general EXPRESS rules | Consumers may overread “conforming” | Publish precise syntax/structural/schema constraint capability metadata and diagnostics |
| Analyzer dependency packaging is incomplete | Generator load failure in consumer builds | Verify package contents and generator execution from a packed-package integration test |

Documentation-disposition forecast: product intent and principles remain durable; the architecture record and accepted ADRs remain durable; README and API documentation evolve with delivered capabilities; the maintainer decides whether to retain this process-only change record after final review.
