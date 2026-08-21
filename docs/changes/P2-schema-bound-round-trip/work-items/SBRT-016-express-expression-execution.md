# SBRT-016: EXPRESS expression execution

## 📌 Status

Approved

## 🚦 Delivery priority

- Priority: P2
- Rationale: Validation/write-reachable schema rules cannot be correct without full expression semantics.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-007 supplies complete expression IR; SBRT-008 bound names/types; SBRT-009 generator host; SBRT-004 validation ABI.
- Recommended order: Before rule dependency closure and complex mapping.
- Governing records: AP-003, EP-003, ADR-0003, and complete reachable execution constraint in the parent change.

## 🧩 Explicit governing constraints

Compile statically reachable C# from bound EXPRESS semantics. No runtime interpreter API, reflection, dynamic code, expression compilation, or second validation generator is authorized.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Bound expression semantics, generated execution, diagnostics, conformance fixtures | Validation/write-reachable scalar, logical, aggregate, reference, navigation, and query expression families evaluate with EXPRESS semantics | General public procedure invocation, unrelated unreachable execution, or changing the minimal validation ABI |

## 🔍 Current behavior and impact boundary

Complete expression syntax and bound types exist after prerequisites, but executable semantics do not. Structural validation must remain unchanged.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| Complete typed expression IR and generator/validation contracts | SBRT-007/008/009/004 completion evidence | Generated evaluation would guess types, scope, or failure shape |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-02A | A valid EXPRESS schema contains `WHERE`, `UNIQUE`, derived expressions, queries, constants, or functions reachable from validation/writing | Build, validate, read, and write the consumer model | The relevant semantics are compiled into executable generated validation/mapping behavior and produce source/schema-traceable failures for every violated rule |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Semantic fidelity | Each reachable expression family matches EXPRESS typing, LOGICAL/UNKNOWN, aggregate, reference, and error semantics | Unsupported dependencies remain explicit and cannot allow conforming write |
| Execution boundary | Generated code is statically reachable and AOT-ready | No public interpreter/DSL or dynamic/reflection path |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-02A | Table-driven generated conformance tests | Representative valid/invalid inputs for every expression family produce expected values/failures and stable source traceability | Fast TUnit generator/runtime project; bounded ISO example comparison |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Expression-family coverage | Coverage inventory maps every validation/write-reachable expression IR form to passing positive/negative generated tests |
| AOT-safe generation | API/code audit finds no reflection/dynamic execution and Release build is warning-free |

## ⏱️ Workload estimate

- Planning range: 0.8–1.5 person-months.
- Confidence: Low.
- Assumptions and excluded work: Rule/function dependency closure is SBRT-017; no unrelated public procedure API.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA and IR/generator/test artifacts |
| Behavior-case proof | BC-02A expression-family matrix and commands/results |
| Migration and documentation | Maintainer semantics coverage documentation |
| Dependent-item unlock | Executable expression semantics for SBRT-017 and SBRT-023 |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| EXPRESS edge semantics exceed estimate | Delayed closure | Preserve approved semantic scope and request work-plan reapproval for material estimate change |
