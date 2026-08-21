# SBRT-020: Multiple same-schema data sections

## 📌 Status

Approved

## 🚦 Delivery priority

- Priority: P2
- Rationale: Edition 3 multi-section population behavior must work before introducing multiple schema descriptors.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-018 supplies atomic typed read; SBRT-019 reference hydration.
- Recommended order: Before multi-schema binding.
- Governing records: AP-003, EP-003, and per-data-section governing-schema architecture pinned by the parent change.

## 🧩 Explicit governing constraints

Each `DataSection` retains its ISO section context and governing `SchemaName`; descriptor association is structure-owned infrastructure. All sections participate in one atomic typed model and validation result.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Multi-data-section binder/model context, validation/writer eligibility tests | Multiple sections resolving to one descriptor bind and validate atomically with section-aware paths and preserved write context | Multiple descriptors, cross-schema population rules, or public schema descriptor property on `DataSection` |

## 🔍 Current behavior and impact boundary

The first typed reader handles one simple data section. This item expands only section multiplicity while retaining one governing descriptor.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-018 and SBRT-019 prove atomic typed values/references | Both completion records | Multi-section failures could otherwise leak or lose identity |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-13A | A syntactically valid exchange structure has multiple data sections that all resolve to one generated schema descriptor | Bind, validate, and write the model | Every data section participates in one atomic typed model, is validated under its governing schema, and retains its section context during canonical writing |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Section context | Validation paths and later writing distinguish each section while sharing one structure identity space as ISO requires | `DataSection` stores `SchemaName`, not `SchemaDescriptor` |
| Atomic population | Any section error invalidates publication and all detected section failures aggregate | Single-section behavior remains unchanged |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-13A | Multi-section integration | Valid sections bind into one model with correct context/references; invalid sections aggregate section-qualified failures and no model returns | Fast TUnit project |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Same-schema population | Positive and multi-error fixtures prove all sections, identity, governing schema, and context |
| Canonical eligibility | Section context is available to writer contract tests without a descriptor-domain relationship |

## ⏱️ Workload estimate

- Planning range: 0.4–0.7 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Different governing descriptors and cross-schema semantics are SBRT-021.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA and model/binder/test/docs artifacts |
| Behavior-case proof | BC-13A command/results |
| Migration and documentation | Multi-section governing-schema behavior documented |
| Dependent-item unlock | Atomic multi-section population guarantee for SBRT-021 |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Section identity/path context is flattened | Ambiguous failures/output | Require section-qualified validation and round-trip assertions |
