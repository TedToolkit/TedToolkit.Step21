# SBRT-023: Complex mapping semantic round trip

## 📌 Status

Approved

## 🚦 Delivery priority

- Priority: P2
- Rationale: Subtype/complex entity instances complete the approved schema-bound mapping semantics.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-017 supplies executable mapping dependencies; SBRT-019 reference hydration; SBRT-022 canonical writer.
- Recommended order: Before final AOT/conformance proof.
- Governing records: AP-003, EP-001/003, architecture physical-mapping rules, and ADR-0005 pinned by the parent change.

## 🧩 Explicit governing constraints

ISO clause-12 simple/complex mapping and EXPRESS inheritance/redeclaration determine physical parameter form/order. Generated descriptor metadata consumes and projects the approved ordered BCL component-name/strong-`ParameterValue`-list shape without reflection, another public projection/context type, or a chosen primary generated class base.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Bound complex physical mapping, generated descriptor component hydration/projection, read/write fixtures, semantic comparison | Supported subtype and complex entity instances consume/produce the approved ordered component shape and read-write-read with equivalent types, values, aggregates, identities, and references | Another public projection/context/factory type, byte-equivalent output, unsupported application-protocol semantics, or reflection guessing |

## 🔍 Current behavior and impact boundary

Simple mapping/read/write and interface inheritance exist after prerequisites. Complex physical components, inherited/redeclared ordering, and semantic round-trip evidence remain absent.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-017, SBRT-019, and SBRT-022 completed guarantees | Their completion records | Complex mapping cannot execute derived dependencies, references, or canonical writing |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-10 | A supported subtype/complex entity mapping contains inherited or redeclared attributes | Read then write it | Generated metadata applies the clause-12 mapping and physical parameter order without reflection guessing |
| BC-14 | A valid supported model is written and read again | Compare schema-bound semantics | Entity identities, types, values, aggregate semantics, and references are equivalent despite canonical formatting differences |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Physical mapping | Inheritance/redeclaration/component order follows cited standard rules through the ordered BCL/`ParameterValue` descriptor contract | Interface-only inheritance/storage contract remains unchanged; no projection DTO/context, primary class base, or reflection |
| Semantic round trip | Comparison covers identity graph, exact generated types, values, aggregate distinctions, and references | Formatting/source ordering differences are explicitly ignored |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-10 | Clause-traced integration fixtures | Representative subtype/redeclaration/complex instances map exact ordered component-name/strong-parameter groups in both directions without another public type | Fast TUnit project plus mapping traceability/public API audit |
| BC-14 | Semantic end-to-end comparison | Read-write-read produces isomorphic identity/reference graph and equivalent typed values/aggregates | Fast TUnit project; pinned representative corpus where licensed |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Complex mapping correctness | Positive/negative clause-12 fixtures cover inheritance, redeclaration, multiple components, OPTIONAL, aggregates, and references |
| Round-trip semantics | Automated graph/value comparator proves equivalence independent of canonical formatting |

## ⏱️ Workload estimate

- Planning range: 0.8–1.5 person-months.
- Confidence: Low.
- Assumptions and excluded work: Declared supported complex mapping scope matches normative Edition 3 requirements in the parent change.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA and mapping/generator/reader/writer/test artifacts |
| Behavior-case proof | Commands/results for BC-10 and BC-14 |
| Migration and documentation | Supported mapping/canonical comparison boundary |
| Dependent-item unlock | Representative semantic round trip for SBRT-025/026 |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Complex mapping scope reveals missing normative behavior | False round-trip claim | Return material behavior gaps to change design; do not narrow silently |
