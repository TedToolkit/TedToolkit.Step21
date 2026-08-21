# SBRT-005: Mutable EXPRESS aggregate runtime values

## 📌 Status

Approved

## 🚦 Delivery priority

- Priority: P2
- Rationale: EXPRESS aggregate distinctions must survive temporary invalid edits and later validation.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-004 supplies the validation result contract.
- Recommended order: Before generated aggregate projection.
- Governing records: AP-003, EP-003, and ADR-0005 pinned by the parent change.

## 🧩 Explicit governing constraints

Expose schema-neutral mutable `ExpressArray<T>`, `ExpressList<T>`, `ExpressBag<T>`, and `ExpressSet<T>`. Mutation never runs schema validation or silently repairs invalid candidates.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Runtime aggregate APIs, validation hooks/results, XML docs, and tests | Four distinct mutable categories retain order, bounds, multiplicity, uniqueness candidates, and optional slots for explicit validation | Generated schema mapping, validation in Add/setters, or replacement with ordinary `List<T>` |

## 🔍 Current behavior and impact boundary

No aggregate runtime exists. The implementation must add only EXPRESS distinctions and keep invalid edit candidates observable for aggregate reporting.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-004 validation result ABI is complete | SBRT-004 completion evidence | Aggregate validation cannot expose the approved failure shape |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-05A | Mutable `ARRAY`, `LIST`, `BAG`, and `SET` values contain the same elements in cases where order, multiplicity, bounds, optional slots, or uniqueness differ | Edit and explicitly validate each value | Runtime types retain the distinct aggregate category; `Validate` reports every schema violation without mutating the values, and write eligibility follows the corresponding EXPRESS semantics |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Editing lifecycle | Add/remove or indexed assignment accepts temporary violations and performs no automatic validation | `ARRAY` exposes fixed index/slot semantics; other categories retain their EXPRESS distinctions |
| Explicit validation | Reports all applicable bounds/uniqueness/slot issues without mutation | Validation is deterministic and AOT-ready |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-05A | Runtime unit/property-style tests | Identical element inputs yield category-specific ordering/multiplicity/bounds/slot results; invalid values remain unchanged after validation | Fast TUnit project |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Complete category contract | Tests cover valid and temporarily invalid state for all four aggregate categories |
| Public documentation | XML explains mutation timing, bounds, optional slots, multiplicity, and explicit validation |

## ⏱️ Workload estimate

- Planning range: 0.3–0.5 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Schema-specific element rules and generated declarations belong to SBRT-012/SBRT-015.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA and runtime/test/docs artifacts |
| Behavior-case proof | BC-05A focused command/results for each category |
| Migration and documentation | XML/API guidance or explicit migration not applicable |
| Dependent-item unlock | Stable aggregate contracts for SBRT-012 and SBRT-015 |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| SET implementation silently deduplicates | Lost validation evidence | Tests must prove duplicate candidates remain reportable |
