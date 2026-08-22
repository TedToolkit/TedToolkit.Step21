# SBRT-021: Multi-schema typed binding

## 📌 Status

Completed

## 🚦 Delivery priority

- Priority: P2
- Rationale: Multi-schema typed binding is a final mandatory capability because ISO 10303-21 permits per-section governing schemas.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-017 supplies complete reachable rules; SBRT-020 supplies atomic multi-section behavior.
- Recommended order: Before final AOT/package and conformance closure.
- Governing records: AP-003, EP-003, architecture AD-10/11/12, and CD-33 pinned by the parent change.

## 🧩 Explicit governing constraints

`ExchangeStructure` remains non-generic and owns an immutable `SchemaName`→generated descriptor binding table. Every `DataSection` binds under its governing schema; ISO population/cross-schema-reference rules apply without a blanket multi-descriptor rejection.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Descriptor-set supply, per-section binding/validation, schema population and cross-schema reference behavior, tests/docs | Same exchange structure can atomically bind, validate, and retain sections governed by different supplied generated schemas | Implicit schema lookup, domain-specific schema facade, per-entity descriptor/container property, or external schema acquisition |

## 🔍 Current behavior and impact boundary

Multi-section binding after SBRT-020 accepts only one descriptor. This item expands descriptor selection and ISO population rules without changing entity or section domain shape.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-017 complete rule closure and SBRT-020 atomic multi-section model | Both completion records | Multi-schema validity/populations could be incomplete or non-atomic |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-13B | A syntactically valid exchange structure has data sections governed by different supplied generated schema descriptors | Bind, validate, and write the model | Every section binds under its governing schema, valid cross-schema references and schema populations follow ISO 10303-21, all failures aggregate atomically, and canonical output retains the governing-schema association without a blanket unsupported result |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Per-section descriptor dispatch | Exact governing `SchemaName` selects supplied descriptor and retains section association | No single-effective-schema restriction or implicit lookup |
| Population/cross-schema validity | Valid relationships bind; manually constructed cross-schema graphs can pre-register referenced entities in their governing sections before root Add; all invalid populations/references aggregate atomically with section/schema paths | Already registered entities retain their section; entity has no descriptor/container/name state; same-schema behavior remains unchanged |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-13B | End-to-end multi-schema contract | Valid two/three-schema structures bind with cross-schema identity and section context; a manually constructed graph preserves the pre-registered referenced entity's governing section during root Add; invalid descriptors/populations/references report all failures and no model | Fast TUnit generated-schema integration; representative corpus fixture |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Final typed capability | Positive multi-schema fixtures prove per-section binding, validation, references, populations, and write context |
| Atomic failure | Mixed multi-section/schema errors aggregate completely with no unsupported blanket or leaked graph |

## ⏱️ Workload estimate

- Planning range: 0.8–1.5 person-months.
- Confidence: Low.
- Assumptions and excluded work: Required descriptors are supplied explicitly; external schema resolution remains excluded.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Started from `2174615`. Extended the closed descriptor-set reader, per-section dispatch, internal population metadata, validation population views, generated descriptor compatibility, imported global-RULE population dispatch, focused tests, API approval, and conformance guidance. No branch, remote, implicit lookup, external acquisition, entity/container descriptor state, reflection, or dynamic code was added. |
| Behavior-case proof | Red: five new generated-consumer cases initially hit the former additional-header and multiple-`FILE_SCHEMA` capability boundaries. Green/refactor: valid three-schema binding preserves exact section descriptors, cross-schema reference identity, and manual pre-registration context; all three standard population methods are exercised; population-external OPTIONAL and required references follow unset semantics; unrelated lookalike targets fail TYPE; descriptor/population faults aggregate atomically; unknown algorithms fail at the capability boundary. Focused SBRT-021 and SBRT-020 regression suites pass. |
| Normative and regression proof | ISO 10303-21:2016 Edition 3 Annex E.1 and E.2 were checked against the pinned public final text. EXPRESS `USE`/`REFERENCE` is the closed-set cross-schema proof method; all three standard population determination methods are implemented. SDAI domain equivalence is explicitly excluded because it requires external ISO 10303-22 metadata. The complete fast Release test project passes 218/218; final Release/AOT/integration/package verification is recorded before commit. |
| Migration and documentation | `docs/conformance/multi-schema-populations.md` defines explicit descriptor supply, per-section binding, `FILE_POPULATION`, default section boundaries, outside-reference behavior, EXPRESS-interface validity, manual registration, diagnostics, and the domain-equivalence exclusion. Same-schema and public Read guidance now point at the expanded contract. The two protected ABI additions are the strongly typed, reflection-free descriptor compatibility and imported entity-constraint hooks approved in `SBRT-021.approved.txt`. |
| Independent review | Final read-only trace review covered the approved work item, Annex E evidence, reader/model/generator changes, population selection and detached unset projection, imported entity dispatch, diagnostics, focused/full tests, public API, and documentation. It found and corrected one important gap: imported entities were initially recognized by the governing descriptor but did not execute their declaring schema's entity-local WHERE/UNIQUE constraints. A dedicated generated entity-population dispatch now runs those constraints without executing unrelated foreign global RULE declarations; a negative imported-WHERE fixture proves it. No blocking or advisory findings remain. |
| Effort and variance | Implementation reused SBRT-019 two-phase identity and SBRT-020 section context. The main added complexity is non-mutating population visibility plus generated EXPRESS interface compatibility; actual elapsed effort remains below the original person-month estimate. |
| Dependent-item unlock | Multi-schema typed context and retained `FILE_POPULATION` metadata are available for canonical writing in SBRT-022 and representative closure in SBRT-025/SBRT-026. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Population/cross-schema interpretation lacks clause evidence | False conformance | ISO evidence is mandatory; material ambiguity returns to change design |
