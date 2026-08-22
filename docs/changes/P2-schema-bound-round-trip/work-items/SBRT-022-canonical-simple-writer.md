# SBRT-022: Canonical simple writer

## 📌 Status

Implemented

## 🚦 Delivery priority

- Priority: P2
- Rationale: A simple explicit writer supplies the first independently usable ISO output slice.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-006 supplies names; SBRT-013 simple mapping; SBRT-018 validated model; SBRT-019 resolved references.
- Recommended order: Before complex round trip and final pre-write failure behavior.
- Governing records: AP-003, EP-003, ADR-0005, and explicit-writer architecture pinned by the parent change.

## 🧩 Explicit governing constraints

Only exact instance `ExchangeStructure.Write(TextWriter)` and `WriteEntity(TextWriter, Entity)` contracts emit ISO text. Generated descriptors project strong `ParameterValue` values; the runtime maps entity objects through the structure reverse identity map and owns final text emission. Diagnostic `ToString()` remains separate. Validation/capability failures use dedicated exceptions; underlying `TextWriter` I/O exceptions retain their behavior. No public writer/context/facade exists.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Exact structure instance write entry points, strong simple projection, header/data/entity parameter formatting, deterministic output tests/docs | Valid simple mapped models emit canonical structures/instance records with every SBRT-011 parameter form and structure-owned names without a public writer/context type | Complex entity mapping, byte preservation, JSON/XML, facade/nested public types, serialization from `ToString()`, or translation of I/O exceptions |

## 🔍 Current behavior and impact boundary

Descriptors can project simple parameters and identity maps hold names, but no public writer exists. Read behavior must remain unchanged.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-006, SBRT-013, SBRT-018, and SBRT-019 contracts complete | Their completion evidence | Writer cannot recover names, mapping, valid model state, or direct references safely |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-09 | A simple entity has explicit and absent OPTIONAL attributes | Read then write it | Generated properties receive typed values/absence, and output parameters retain standard order with `$` for absence |
| BC-05B | A schema and Part 21 values use INTEGER, REAL/NUMBER, STRING, BINARY, BOOLEAN, LOGICAL/UNKNOWN, enumeration, typed/untyped parameters, `$`, and `*` | Generate, read, edit, validate, and write the values | Each ISO/EXPRESS distinction has an AOT-ready schema-neutral representation, LOGICAL remains three-state, absence/derived markers are not conflated with CLR defaults, and canonical writing emits the corresponding parameter form |
| BC-11 | A new mutable entity is registered through the model API | Continue editing, then write the model | The exchange structure owns its occurrence name and reference-identity reverse mapping without modifying the entity; registration does not run schema validation, and the same object may be registered in another structure under a different name |
| BC-23 | Request an entity-instance record or complete exchange-structure string | Invoke the explicit writer with an `ExchangeStructure` | Context-dependent names and references are emitted canonically, or a write-stage aggregate exception contains all detected issues and no partial output |
| BC-23A | A consumer supplies generated descriptors and uses the public Part 21 boundary | Call `ExchangeStructure.Read`, `structure.Write`, or `structure.WriteEntity` | Only strong public values cross generated descriptor hooks; no public syntax/context/reader/writer facade or nested public type exists; duplicate descriptor schema names throw `ArgumentException` before input is consumed; stage failures use their dedicated complete-evidence exception; write validation/capability failures produce no output, while I/O failures retain their underlying exception behavior |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Canonical simple output | Header/section/entity/parameter/reference syntax is deterministic and complete for every SBRT-011 parameter form | No byte-for-byte claim and no invented schema/domain fields |
| Exact failure/API boundary | Instance signatures supply structure context intrinsically; strong projection, validation, and capability are preflighted before output; exceptions carry exact evidence and I/O remains unchanged | No public writer/context/facade, partial output for detectable domain/capability failure, `ToString()` substitute, or generic catch/reclassification |
| Identity/context | Same entity can serialize under independent names in different structures; instance-record API requires structure context | Entity state and `ToString()` remain serialization-free |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-09 | Writer/read-back integration | Present/absent OPTIONAL values emit correct ordered parameters and re-read equivalently | Fast TUnit project |
| BC-05B | Complete value round-trip integration | Every scalar/parameter category is generated, read, edited, explicitly validated, written canonically, and re-read without semantic conflation | Fast TUnit generated-schema read-write-read matrix |
| BC-11 | Identity writer contract | One object written from two structures uses each structure's independent name without mutation/validation on Add | Fast TUnit project |
| BC-23 | Explicit writer contract | Complete and per-instance APIs emit canonical valid text; diagnostic `ToString()` cannot substitute | Fast TUnit project |
| BC-23A | Minimal public write exception matrix | Exact instance signatures succeed; unsupported capability and invalid writing fail before a probe destination receives output; each uses dedicated complete evidence; throwing `TextWriter` behavior remains the underlying I/O exception; API has no writer/context/nested type | Fast TUnit project plus public API snapshot |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Simple canonical writing | Representative models/instance records covering every scalar/parameter form parse back with equivalent simple semantics |
| API/failure separation | Public/XML tests prove exact instance signatures, strong projection, absence of writer/context/facade/nested types, dedicated exceptions/evidence, I/O preservation, and diagnostic formatting separation |

## ⏱️ Workload estimate

- Planning range: 0.6–1.1 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Complete failure aggregation is SBRT-024; complex mappings are SBRT-023.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Started from `79953ed`. Added only the exact instance `Write(TextWriter)` and `WriteEntity(TextWriter, Entity)` methods, one runtime-internal buffered writer, focused generated/custom descriptor fixtures, API approval, and canonical-writing documentation. No public writer/context/facade/nested type, complex physical mapping, byte-preservation claim, JSON/XML contract, branch, remote, reflection, dynamic code, or new dependency was added. |
| Behavior-case proof | Red: `CanonicalSimpleWriterTests` failed to compile because both approved instance methods were absent. Green/refactor: focused writer tests pass 4/4 and the complete fast Release project passes 222/222. BC-05B/BC-09 read, edit, write, and re-read every generated simple scalar/typed/aggregate/OPTIONAL family with canonical `$`, exact LOGICAL UNKNOWN, character directives, binary padding, and stable parameter order. A strong custom projection proves `*`, recursive values, and reference-name formatting. BC-11 proves one graph writes under independent names in two structures without entity mutation. BC-23/BC-23A prove exact complete/record output, named multi-schema sections and `FILE_POPULATION`, parser acceptance, unregistered target validation, invalid graph/capability/projection/unnamed-section zero-output failures, original `IOException`, exact public surface, and serialization-free `ToString()`. |
| Normative and regression proof | ISO 10303-21:2016 Edition 3 Table 3 and clauses 7.1, 8, 11, and 12 govern header, parameter, data-section, and simple-record structure; Table 2 and clauses 6.4.1–6.4.6 govern canonical value tokens. Release solution and runtime AOT/trim analyzer builds passed with 0 warnings/errors. Integration TUnit passed 3/3 enabled cases including local packed consumption; the explicit opt-in external corpus case was skipped. Runtime dependency inspection still reports only `Antlr4.Runtime.Standard` 4.13.1. Production scanning found no reflection, dynamic dispatch, or expression compilation. Public API/XML tests pass, `git diff --check` passes, and the work-item validator reports `Work-item delivery boundary: valid`. |
| Migration and documentation | `docs/conformance/canonical-simple-writing.md` defines deterministic header/section/record order, fixed line separators, every simple parameter spelling, structure-local identity, preflight buffering, exact exceptions, and the I/O/non-byte-preservation boundary. README, generated-value, and multi-schema guidance now link the delivered writer. `SBRT-022.approved.txt` pins the two-method public expansion. |
| Independent review | Final read-only trace review covered the approved work item, clause evidence, writer/API/exception design, complete and per-entity request scoping, structure and entity buffering, projection/reference lookup, independent identity contexts, empty/header descriptor capability, multi-schema named sections and both population section forms, tests, public metadata, and documentation. It tightened descriptor preflight to use the header schema set for complete writing while keeping `WriteEntity` request-specific, and added explicit missing-descriptor, unprojectable-entity, unnamed-multi-section, and zero-registration capability evidence. No blocking or advisory findings remain. |
| Effort and variance | Existing descriptor projection, parameter formatter, header values, section context, and bidirectional identity maps made the simple writer substantially smaller than the planning range. The remaining complexity is intentionally staged to complex mapping and final aggregate preflight. |
| Dependent-item unlock | Canonical simple projection and atomic buffered emission are available for SBRT-023 complex mapping and SBRT-024 final pre-write aggregation. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Output begins before complete eligibility is known | Partial invalid output | SBRT-024 must own final atomic pre-write gate; this item buffers/structures output compatibly |
