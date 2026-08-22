# SBRT-024: Atomic pre-write validation

## 📌 Status

Completed

## 🚦 Delivery priority

- Priority: P2
- Rationale: Boundary-only validation is safe only when writing aggregates all final-state failures before output.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-015 structural validation; SBRT-017 complete rules; SBRT-022 explicit writer.
- Recommended order: Before Native AOT/package proof.
- Governing records: EP-003, ADR-0003/0005, and boundary-only validation contract pinned by the parent change.

## 🧩 Explicit governing constraints

Property/aggregate mutations, Add, and Remove never auto-validate. Before output, the writer completes supported-capability preflight and validation of the final graph, including dangling references left by non-cascading Remove. It aggregates every detectable validation issue into `ExchangeStructureWriteValidationException`; unsupported operations throw `ExchangeStructureCapabilityException`; neither failure emits a character or byte.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Writer eligibility/capability gate, output atomicity, aggregate exception behavior, mutation fixtures/docs | Valid supported final edits write canonically; invalid final graphs yield one complete result, unsupported operations yield complete capability diagnostics, and both fail with zero partial output | Setter guards, fail-fast validation, silent repair, warning-only invalidity, or concurrent mutation support |

## 🔍 Current behavior and impact boundary

Explicit validation and a simple writer exist after prerequisites, but their final-state integration and zero-output failure guarantee are not yet proven.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-015, SBRT-017, and SBRT-022 complete | Their completion records | Writer cannot know complete validity or provide the approved exception/output contract |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-12 | Properties and aggregates on registered entities are changed repeatedly | Continue editing, then write | Intermediate mutations do not validate; pre-write validation observes the final graph, reports all remaining violations together, or permits canonical serialization |
| BC-15 | Foreign-model objects, invalid aggregates, values incompatible with a supported mapping, or unresolved/dangling references exist in multiple entities | Attempt conforming write | One `ExchangeStructureWriteValidationException` identifies every detected entity/path issue and no partial or falsely conforming output is produced; duplicate explicit-name claims have already failed atomically during Add, while an unavailable mapping capability uses its dedicated capability exception instead |
| BC-23A | A consumer supplies generated descriptors and uses the public Part 21 boundary | Call `ExchangeStructure.Read`, `structure.Write`, or `structure.WriteEntity` | Only strong public values cross generated descriptor hooks; no public syntax/context/reader/writer facade or nested public type exists; duplicate descriptor schema names throw `ArgumentException` before input is consumed; stage failures use their dedicated complete-evidence exception; write validation/capability failures produce no output, while I/O failures retain their underlying exception behavior |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Editing timing | Repeated setter/aggregate/Add/Remove operations execute without validation; only explicit read/write boundaries validate automatically | Temporary invalid/dangling state remains supported and model is not thread-safe during validation/write |
| Atomic write failure | Capability support and all detectable final validation issues are determined before output begins | No partial bytes/chars from domain/capability failures, fail-fast validation, or falsely conforming output |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-12 | Mutation/lifecycle integration | Instrumented repeated edits cause no validation; final valid state writes, final invalid state aggregates only then | Fast TUnit project |
| BC-15 | Multi-failure writer contract | Duplicate/foreign/aggregate/mapping/reference issues, including a target removed non-cascadingly, all appear in one `ExchangeStructureWriteValidationException` and target output remains length zero | Fast TUnit project with `TextWriter` sinks |
| BC-23A | Writer preflight exception boundary | Unsupported capability and invalid writing use their exact complete-evidence exceptions before every public writer sink receives output; throwing `TextWriter` I/O remains untranslated | Fast TUnit project with probe/throwing `TextWriter` sinks |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Boundary timing | Instrumented tests prove no mutation/Add validation and one complete pre-write validation |
| Failure atomicity | Multi-category validation and unsupported-capability tests prove exact complete evidence and zero output for all writer entry points |

## ⏱️ Workload estimate

- Planning range: 0.4–0.7 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Callers exclude concurrent mutation during validation/writing.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Started from `59643b8` on `main`. Changed only private pre-write orchestration, existing public write XML, focused tests, and boundary documentation. No branch, remote, dependency, public type/member/signature, validation framework, context, or facade was added. |
| Behavior-case proof | Red: focused TUnit passed the existing timing case but failed 2/3 because `WriteEntity` hid its unregistered-target failure behind earlier graph failures and projected dangling references escaped as fail-fast `InvalidOperationException`. Green/refactor: focused tests pass 5/5 and the complete fast Release project passes 236/236. BC-12 proves property/list/Add/Remove edits invoke zero validation and each write attempt invokes the complete structure validator once. BC-15 returns property, aggregate, foreign-entity, non-cascading-remove reference, unregistered target, and recursively nested projected-reference failures as complete deterministic results. BC-23A proves both `Write` and `WriteEntity` return all capability diagnostics with zero writes; the existing throwing-writer test continues to preserve the original `IOException`. |
| Failure atomicity | Probe destinations remain at zero writes for every validation and capability failure in both entry points. Complete writes recursively inspect all projected simple/complex, aggregate, and typed parameters before building/delivering text. Unregistered `WriteEntity` evidence is appended to the complete structure result. Valid structures alone proceed to capability/projection checks, avoiding projection of invalid mandatory/aggregate state. |
| Regression and deployment proof | Release solution and runtime AOT/trim analyzer builds pass with 0 warnings/errors. Integration TUnit passes 3/3 enabled cases; the explicit external-network corpus case remains opt-in and skipped. Runtime dependency inspection still reports only `Antlr4.Runtime.Standard` 4.13.1. The cumulative public API snapshot remains unchanged and `git diff --check` passes. |
| Migration and documentation | `docs/conformance/atomic-prewrite-validation.md` defines unchecked editing, one final validation call, complete exception evidence, projection-reference defense, validation-before-capability precedence for invalid graphs, zero domain-output, non-thread-safe mutation timing, and unchanged external I/O exceptions. README, canonical-writing guidance, and both public method XML remarks link or state the delivered boundary. |
| Independent review | Final trace covered validation call count, deterministic failure ordering, invalid-state projection avoidance, unregistered record targets, hidden/nested projected references, simple and complex parameter paths, complete capability diagnostics, both public sinks, buffered formatting, public API, AOT, and documentation. It corrected the two fail-fast/sequential gaps found by Red tests; no blocking or advisory findings remain. |
| Dependent-item unlock | The final supported write boundary now supplies complete zero-output validation/capability evidence for SBRT-025 package/AOT proof. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Writer streams output before validation completes | Irrecoverable partial output | Zero-length sink assertions are mandatory for every public writer overload |
