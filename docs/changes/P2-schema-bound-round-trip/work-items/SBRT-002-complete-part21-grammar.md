# SBRT-002: Complete Edition 3 clear-text grammar

## 📌 Status

Approved

## 🚦 Delivery priority

- Priority: P2
- Rationale: Every later Part 21 behavior relies on a clause-traceable complete syntax boundary.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-001 supplies deterministic generated artifacts.
- Recommended order: Before syntax-model and binding items.
- Governing records: AP-003, EP-001, and the active schema-bound architecture pinned by the parent change.

## 🧩 Explicit governing constraints

ISO 10303-21:2016 Edition 3 WSN is authoritative. Corpus acceptance cannot override clause evidence, and compatibility extensions require an accepted ADR.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| `STEP.g4`, clause-traceability evidence, generated artifacts, grammar fixtures | Normative start rule recognizes every declared clear-text section form, enforces ordering, and reaches EOF | Operational semantics for anchors, external references, signatures, archives, or ECMAScript |

## 🔍 Current behavior and impact boundary

The current grammar proves only two classic minimal files and one missing `ENDSEC`; it does not evidence full Edition 3 section coverage, trailing-input rejection, or production-level ISO traceability.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-001 deterministic generation gate is complete | SBRT-001 completion evidence | Grammar changes cannot be accepted without reproducible derived artifacts |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-17 | A proposed `.g4` behavior change has corpus evidence but no ISO clause evidence | Review or implement the change | The change is rejected or explicitly routed through an accepted compatibility-extension ADR |
| BC-21 | Parse a valid complete exchange structure with all section forms in the declared conformance scope, followed by trailing non-separator input | Invoke the normative start rule | The valid structure reaches EOF; trailing input and invalid section order are rejected according to the cited WSN |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Normative syntax | Every production change cites clause/WSN evidence and has focused positive/negative fixtures | Existing valid repository and pinned-corpus syntax remains accepted unless ISO evidence says otherwise |
| Recognition versus operation | Valid advanced syntax parses independently of later runtime support | No false claim that syntactic support provides operational semantics |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-17 | Review-contract test/manual audit | Every changed production and fixture carries a normative citation or accepted compatibility ADR | Bounded production-to-WSN traceability audit |
| BC-21 | Grammar unit/corpus regression | All valid section forms reach EOF; trailing data and invalid order produce errors | Fast TUnit project; opted-in integration corpus where applicable |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Complete syntax scope | Traceability table and focused fixtures cover every declared Edition 3 clear-text production |
| Regression safety | Generated artifacts are deterministic and fast plus opted-in corpus tests pass |

## ⏱️ Workload estimate

- Planning range: 0.8–1.5 person-months.
- Confidence: Low.
- Assumptions and excluded work: The final public text is sufficient evidence; operational advanced-section behavior belongs to later items.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA, grammar/generated/test artifacts, and any scope deviation |
| Behavior-case proof | Clause references and commands/results for BC-17 and BC-21 |
| Migration and documentation | Exact supported syntax boundary documented |
| Dependent-item unlock | Complete parse-tree and section guarantee for SBRT-003 |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Existing grammar structure differs materially from WSN | Estimate growth | Return to work-plan approval if outcome/estimate changes materially; do not weaken ISO scope |
