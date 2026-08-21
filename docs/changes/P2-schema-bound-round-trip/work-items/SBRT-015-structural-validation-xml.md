# SBRT-015: Structural validation and XML traceability

## 📌 Status

Completed

## 🚦 Delivery priority

- Priority: P2
- Rationale: Consumers need complete explicit feedback for mandatory, type, and aggregate structure before full EXPRESS execution.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-004 supplies validation ABI; SBRT-005 aggregate checks; SBRT-010 entities; SBRT-013 descriptors.
- Recommended order: Before typed publication and complete rule closure.
- Governing records: AP-003, EP-003, ADR-0003/0005, and approved XML format in the parent change.

## 🧩 Explicit governing constraints

Generate validation directly from bound EXPRESS IR. `ExchangeStructure.Validate()` is side-effect-free, aggregates all detected failures, and does not throw for invalidity. Stable constraint IDs in failures and deterministic English XML must match. Per CD-46, the runtime owns registration traversal and passes each generated descriptor an ordered `IReadOnlyList<KeyValuePair<string, Entity>>`; keys are complete paths such as `DataSections[0].#1`, values retain entity identity, and no public context, registry, resolver, reflection, or ambient state is introduced.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Generated structural validators, descriptor dispatch, `ExchangeStructure.Validate`, XML docs, tests | Mandatory/OPTIONAL, type/assignability, aggregate bounds/slots/uniqueness/element failures aggregate with deterministic path/code/source traceability | WHERE/UNIQUE expression execution, public validator DSL, attributes, reflection, mutation-time validation, or warnings in `ValidationFailure` |

## 🔍 Current behavior and impact boundary

Validation values and structural schema metadata exist after prerequisites, but no complete graph traversal or generated structural rules execute yet.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| Validation, aggregate, entity, and descriptor prerequisite contracts complete | SBRT-004/005/010/013 evidence | Rule dispatch/path/code/XML equivalence cannot be proven |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-06A | Consumer code constructs and incrementally edits a generated entity | Supply mandatory members, assign properties, and mutate aggregates | Mandatory members are non-nullable and required for normal construction, `OPTIONAL` members are nullable, no setter/mutation runs automatic validation, and any deliberately forced mandatory null is reported by later explicit/read/write validation |
| BC-06B | A consumer wants feedback before writing a temporarily invalid edited model | Invoke `ExchangeStructure.Validate()` | Validation has no side effects, returns one Step21 `ValidationResult` containing all detected entity/path failures, and does not throw solely because the result is invalid |
| BC-16A | A schema declaration has aggregate bounds, optionality, a named `WHERE`/`UNIQUE` rule, or a generated unnamed structural rule | Inspect generated XML documentation and trigger the rule | The documented schema/declaration, normalized requirement, validation boundary, and stable constraint ID match the generated `ValidationFailure.Code` and contain valid deterministic XML |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Validation lifecycle | Traversal visits registered entities by reference identity, aggregates, has no side effects, and never validates setters/Add | Invalid models remain editable after inspection |
| Traceability | Code/path/message/source and XML schema/declaration/requirement/boundary are deterministic and derived from one IR | No annotations, reflection, FluentValidation, or second generator |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-06A | Generated/runtime validation test | Forced mandatory null and invalid aggregate remain editable, then appear in explicit result only | Fast TUnit project |
| BC-06B | Graph validation integration | Multiple entities/paths produce one complete deterministic result, validation does not mutate or throw | Fast TUnit project |
| BC-16A | Generated XML/behavior contract | Every structural failure code matches one valid deterministic XML constraint entry and normalized requirement | Generator tests plus XML compiler/warning gate |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Complete structural rules | Representative graphs trigger all structural categories and aggregate every failure deterministically |
| Documentation parity | Generated XML is well formed, warning-free, deterministic, and code-for-code equivalent to rules |

## ⏱️ Workload estimate

- Planning range: 0.6–1.1 person-months.
- Confidence: Medium.
- Assumptions and excluded work: General EXPRESS expressions are SBRT-016/017.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Started from `81bd022`. Added the public side-effect-free `ExchangeStructure.Validate()` boundary, corrected the generated-consumer descriptor ABI through CD-46, emitted direct structural checks and XML from one bound projection, added type/declaration traceability, focused runtime/generated tests, one public API snapshot, and conformance guidance. No expression evaluator, named `WHERE`/entity `UNIQUE` execution, mutation-time validation, reflection, dynamic code, public context/registry/DSL, read/write implementation, or dependency was added. The work-item validator reported `Work-item delivery boundary: valid`. |
| Behavior-case proof | BC-06A/06B construct deliberately invalid mutable generated graphs and prove that setters, aggregate edits, Add, and Remove remain unchecked; two explicit validations return the same ordered result, visit each registered object once by reference identity, retain complete section/occurrence/property paths, report detached/null/unregistered relationships, and do not mutate sections, registrations, or aggregates. BC-16A executes mandatory/optional, entity, nominal, closed-enumeration, SELECT, all four aggregate categories, wrong ARRAY shape, required slots, uniqueness, nested elements, literal and symbolic-bound cases, and unknown schema entities. Generated-source failure literals and XML constraint terms are compared as equal sets, every XML fragment parses, relocated sources generate identical output, and every source location is a leaf path with 1-based line/column. Complete fast TUnit passed 160/160. |
| Migration and documentation | `README.md` links `docs/conformance/structural-validation.md`; descriptor, aggregate, value, and architecture guidance now states the explicit validation lifecycle, staged symbolic-expression boundary, deterministic evidence/XML contract, and C# representation rule. Mutable entities and singleton descriptors are ordinary classes; only immutable SELECT discriminated values use sealed record reference types for value equality. Generated entity/defined-value remarks identify schema/declaration, properties publish `<value>` plus normalized constraint tables, and descriptors link the generated entity values they validate. |
| Regression and deployment proof | Release solution build passed with 0 warnings/errors. Integration TUnit passed all 3 enabled cases; only the explicit opt-in external-network corpus case was skipped. Runtime `IsAotCompatible`, trim, and AOT analyzer build passed with 0 warnings/errors. Runtime dependency inspection still reports only `Antlr4.Runtime.Standard` 4.13.1. Limited formatting and `git diff --check` passed. Strict read-only review corrected the cross-assembly registration-access contradiction, partial symbolic-bound validation gap, XML/executable set-parity proof, ARRAY wrong-shape safety, and descriptor/entity documentation links; the final pass found no remaining blocking, important, suggestion, or design-deviation findings. |
| Dependent-item unlock | Structural validation and its deterministic path/code/source/XML contract now gate SBRT-016 expression execution, SBRT-017 complete rule closure, SBRT-018 atomic typed publication, and SBRT-024 atomic pre-write validation. |
| Actual effort and variance | Completed in one continuing agent implementation session; the human person-month estimate is not directly comparable. The protected validation hook gained the smallest approved ordered BCL path/entity input because generated overrides live in consumer assemblies and cannot inspect runtime-internal registrations. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Graph/path traversal repeats cycles or changes state | Resolved | Reference-identity dispatch and before/after immutability assertions pass for shared, repeated, cyclic-capable registrations |
