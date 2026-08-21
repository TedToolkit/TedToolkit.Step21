# SBRT-026: Conformance corpus and public capability documentation

## 📌 Status

Approved

## 🚦 Delivery priority

- Priority: P2
- Rationale: Final evidence and exact capability claims prevent accidental overstatement of ISO conformance.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-002 normative grammar; SBRT-003 advanced diagnostics; SBRT-021 multi-schema binding; SBRT-023 semantic round trip; SBRT-025 package/AOT proof.
- Recommended order: Final delivery row after all capability proofs it documents.
- Governing records: Product intent, AP-003, EP-001/003, architecture, and ADR-0002/0003/0005 pinned by the parent change.

## 🧩 Explicit governing constraints

Documentation must state the exact minimal non-nested public API plus syntax, binding, validation, writing, operational, AOT, and package boundaries. It must distinguish necessary strong ISO/evidence types from internal syntax/stage machinery. Corpus inputs remain independently sourced, license/provenance recorded, integrity pinned, opt-in when networked, and may not replace normative clause evidence.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Pinned corpus manifest/fixtures/tests, repository/package READMEs, API/conformance docs, build gates | Released documentation and regression evidence exactly match delivered complete syntax, typed multi-schema, validation/write, unsupported-operation, and AOT boundaries | New runtime capability, weakening earlier item verification, byte-perfect source preservation, or claiming unsupported operational facilities |

## 🔍 Current behavior and impact boundary

The repository has pinned NIST/buildingSMART corpus infrastructure and parser-only README claims. Final generated-model, validation, writer, multi-schema, and AOT behavior is not yet represented in corpus assertions or user documentation.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| All named prerequisite capability/evidence records are complete | SBRT-002/003/021/023/025 completion evidence | Documentation/corpus claims would speculate about undelivered behavior |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-14 | A valid supported model is written and read again | Compare schema-bound semantics | Entity identities, types, values, aggregate semantics, and references are equivalent despite canonical formatting differences |
| BC-17 | A proposed `.g4` behavior change has corpus evidence but no ISO clause evidence | Review or implement the change | The change is rejected or explicitly routed through an accepted compatibility-extension ADR |
| BC-18 | A consumer looks for JSON/XML support or dependencies | Inspect generated/runtime APIs and package graph | No such serialization contract, adapter hook, attribute, or dependency is present |
| BC-24 | A syntactically valid Edition 3 structure uses an advanced section whose operational semantics are not delivered yet | Parse it through the normative start rule and request the unsupported operation | Parsing preserves the standard structure; the later operation returns an explicit unsupported-capability diagnostic rather than a syntax error |
| BC-23A | A consumer supplies generated descriptors and uses the public Part 21 boundary | Call `ExchangeStructure.Read`, `structure.Write`, or `structure.WriteEntity` | Only strong public values cross generated descriptor hooks; no public syntax/context/reader/writer facade or nested public type exists; duplicate descriptor schema names throw `ArgumentException` before input is consumed; stage failures use their dedicated complete-evidence exception; write validation/capability failures produce no output, while I/O failures retain their underlying exception behavior |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Evidence integrity | Corpus artifacts have HTTPS provenance, license, size, SHA-256, ignored cache, and semantic assertions | Network tests remain explicit opt-in and normative evidence remains separate |
| Capability/API claims | README/API docs distinguish complete syntax from staged advanced operations, document the structure-centric read/write API and necessary strong/evidence types, and state multi-schema/AOT/write validation exactly | No public syntax/context/facade/registry/resolver/nested type or JSON/XML/domain-specific/byte-preservation promise |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-14 | Pinned corpus semantic integration | Representative licensed schemas/files pass generated read-write-read semantic comparison | Opted-in integration project plus fast repository fixtures |
| BC-17 | Traceability documentation audit | Every corpus-driven grammar expectation links normative evidence or accepted extension ADR | Bounded grammar/corpus traceability review |
| BC-18 | Public docs/package audit | Docs and dependency/API evidence explicitly exclude JSON/XML contracts | Release build, package inspection, README/API review |
| BC-24 | Capability-boundary integration/docs | Advanced valid syntax parses and documented unsupported operation returns exact capability diagnostic | Fast/integration fixtures plus documentation assertion |
| BC-23A | Minimal API documentation audit | Examples use only `SchemaDescriptor`, `ExchangeStructure`, `DataSection`, `Entity`, necessary strong values/evidence/exceptions, and no forbidden syntax/stage/facade/nested concept | README/API compilation plus public API snapshot comparison |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Full regression gate | Fast tests, opted-in pinned corpus, Native AOT proof, and Release build pass with zero errors/warnings |
| Truthful user contract | README/package/API/conformance docs match delivered behavior and every parent completion criterion has recorded evidence or explicit in-scope disposition |

## ⏱️ Workload estimate

- Planning range: 0.3–0.5 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Corpus licenses permit automated use; no new behavior is implemented here.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA and corpus/test/docs/build artifacts |
| Behavior-case proof | Commands/results for BC-14, BC-17, BC-18, BC-23A, and BC-24 |
| Migration and documentation | Final README/API/conformance state and documentation disposition proposal |
| Dependent-item unlock | None; supplies final change-closure evidence |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Corpus passes syntax but lacks semantic diversity | Weak conformance evidence | Add only licensed, pinned fixtures that exercise declared semantics; do not substitute volume for coverage |
