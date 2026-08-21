# SBRT-015: Structural validation and XML traceability

## 📌 Status

Approved

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

Generate validation directly from bound EXPRESS IR. `ExchangeStructure.Validate()` is side-effect-free, aggregates all detected failures, and does not throw for invalidity. Stable constraint IDs in failures and deterministic English XML must match.

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

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA and generated validation/runtime/test/XML artifacts |
| Behavior-case proof | Commands/results for BC-06A, BC-06B, and BC-16A |
| Migration and documentation | Explicit validation and XML constraint guidance |
| Dependent-item unlock | Structural validation/publication gate for SBRT-017/018/024 |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Graph/path traversal repeats cycles or changes state | Nontermination or corrupt edits | Reference-identity and immutability-of-validation assertions gate completion |
