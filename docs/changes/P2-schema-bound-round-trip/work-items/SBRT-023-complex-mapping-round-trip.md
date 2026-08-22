# SBRT-023: Complex mapping semantic round trip

## 📌 Status

Implemented

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

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Started from `b7efd20` on `main`. Changed only generated physical-component planning/emission, private read/write mapping, focused fixtures, and conformance documentation. The existing ordered BCL/strong-`ParameterValue` descriptor ABI is unchanged; no public DTO/context/factory, generated primary class base, reflection, branch, remote, or dependency was added. |
| Behavior-case proof | Red: the initial external-mapping fixtures failed at the former `P21-CAP-COMPLEX-ENTITY` gate; the first implementation was then rejected during clause review because a named subtype with one leaf must use internal mapping. Green/refactor: the corrected focused suite passes 9/9 and the complete fast Release project passes 231/231. BC-10 covers required one-leaf internal mapping, true flat-`ANDOR` multi-leaf external mapping, shared-ancestor de-duplication, explicit renamed redeclaration in its original storage component, empty/non-empty components, OPTIONAL, aggregates, BOOLEAN/LOGICAL, and references. Negative cases reject nonascending, incomplete, wrong-count, and derived-redeclaration mappings with source evidence; an incompatible reference reports `DataSections[0].#1.Components[2].Parameters[1]`, and inherited validation failures occur once. BC-14 writes and rereads both supported forms while comparing occurrence names, exact generated CLR types, component order, values, aggregate order, and normalized reference targets independently of text/record order. |
| Normative and regression proof | ISO 10303-21:2016 clauses 12.2.5.1–12.2.5.3 and 12.2.8 govern leaf-count form selection, internal parameter order, external partial values, uppercase/digit ascending collation, and explicit redeclaration storage. Release solution and runtime AOT/trim analyzer builds pass with 0 warnings/errors. Integration TUnit passes 3/3 enabled cases; the explicit external-network corpus case remains opt-in and skipped. Runtime dependency inspection still reports only `Antlr4.Runtime.Standard` 4.13.1. The cumulative SBRT-022 public API snapshot test passes unchanged, `git diff --check` passes, and the work-item validator reports `Work-item delivery boundary: valid`. |
| Migration and documentation | `docs/conformance/complex-mapping-round-trip.md` defines leaf-count form selection, the supported two-to-eight-factor flat-`ANDOR` boundary, partial-component ordering, explicit-redeclaration storage, internal synthetic representation, derived-redeclaration rejection, and semantic comparison. README and canonical-writing guidance link the delivered behavior. No public API was added. |
| Independent review | Final trace covered allocation ambiguity, exact component closure/collation, shared-ancestor de-duplication, redeclared storage ownership, internal/external form selection, collision-safe generated variables, empty partial values, diagnostic source/path translation, duplicate inherited validation, buffered complete/per-entity writing, semantic graph comparison, AOT reachability, public surface, dependencies, and documentation. It corrected the initially false one-leaf external model, replaced it with an internal multi-interface synthetic representation, bounded exponential flat-`ANDOR` generation, and made derived redeclarations an explicit rejection boundary. No blocking or advisory findings remain. |
| Effort and variance | Existing resolved inheritance, `StorageEntity`, strong parameter conversion, two-phase reference hydration, and buffered writer infrastructure reduced implementation below the original estimate. Derived redeclarations, more than eight flat factors, and nested evaluated-set forms remain explicit boundaries rather than reflection-based guesses. |
| Dependent-item unlock | Representative subtype/external mapping semantic round trip and deterministic generated projection are available for SBRT-024 final pre-write aggregation and SBRT-025/026 AOT/conformance closure. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Complex mapping scope reveals missing normative behavior | False round-trip claim | Resolved for flat `ANDOR` by selecting internal/external form from leaf count and representing supported multi-leaf values with an internal synthetic class; nested evaluated-set forms remain explicit future scope |
