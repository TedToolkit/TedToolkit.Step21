# SBRT-022: Canonical simple writer

## 📌 Status

Approved

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

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA and writer/test/docs artifacts |
| Behavior-case proof | Commands/results for BC-05B, BC-09, BC-11, BC-23, and BC-23A writer assertions |
| Migration and documentation | Explicit writer/canonical formatting guidance |
| Dependent-item unlock | Simple writer for SBRT-023 and SBRT-024 |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Output begins before complete eligibility is known | Partial invalid output | SBRT-024 must own final atomic pre-write gate; this item buffers/structures output compatibly |
