# SBRT-020: Multiple same-schema data sections

## 📌 Status

Completed

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

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Started from `e8466a0`. Extended only the runtime-internal same-schema section binder/context, structure-wide allocation paths, focused generated-consumer fixtures, one obsolete capability fixture, public Read XML, and conformance/README guidance. Public `DataSection` still exposes only `SchemaName`; its decoded section name is retained as internal writer context. No public descriptor relationship/name member, multiple-descriptor binding, cross-schema population logic, writer, complex mapping, branch, remote, dependency, reflection, or dynamic code was added. |
| Behavior-case proof | Red: the focused suite failed to compile because parsed `DataSection` values retained no name context. Green/refactor: `SameSchemaDataSectionReadTests` passed 4/4 and the complete fast TUnit project passed 213/213. BC-13A proves two named sections bind under one descriptor, preserve their decoded names and registration membership, share a structure-global occurrence namespace, and hydrate a forward cross-section cycle by actual object identity. One negative fixture aggregates five malformed/duplicate section-context diagnostics plus a duplicate `#n` across otherwise valid sections. Further fixtures aggregate MISSING and TYPE references with exact `DataSections[s]` paths and generated EXPRESS failures from both sections. The original unnamed single-section suite remains green. |
| Normative and regression proof | ISO 10303-21:2016 Edition 3 clauses 11.1 and 11.2 were checked against the repository's pinned public final text: every section in a multi-section structure carries a unique STRING name and a one-STRING governing-schema list; occurrence names are unique across the exchange structure and may be referenced before definition. Release solution build passed with 0 warnings/errors. Runtime AOT/trim analyzer build passed with 0 warnings/errors. Integration TUnit passed 3/3 enabled cases with the explicit opt-in external corpus case skipped. Runtime dependency inspection still reports only `Antlr4.Runtime.Standard` 4.13.1; production scanning found no reflection, dynamic dispatch, or expression compilation. Public API snapshot diff is empty. |
| Migration and documentation | `docs/conformance/same-schema-data-sections.md` records the normative parameter forms, unique section/occurrence namespaces, internal write context, two-phase cross-section identity, exact failure boundary, and multi-schema staging. Atomic-read/reference guides, README, and public Read XML now describe one or more same-schema sections without claiming multiple governing schemas. |
| Independent review | Final read-only trace review compared the work item, Edition 3 clauses, governing decisions, reader/model diff, tests, public metadata, and documentation. No blocking or advisory findings remain. Review explicitly checked malformed-section exclusion, structure-global duplicate allocation, allocation of every valid section before any hydration, cross-section local/external lookup, actual section indexes for direct TYPE and nested MISSING evidence, validation dispatch per section, internal name retention, atomic failure precedence, and unchanged single-section behavior. |
| Effort and variance | Approximately 0.8 elapsed implementation/review hours, below the 0.4–0.7 person-month planning range because syntax already preserved section parameters, structure validation already dispatched section-qualified batches, and SBRT-019 already supplied global two-phase reference hydration. The only added model state is the ISO section name required by later canonical writing, retained internally to preserve the approved minimal public API. |
| Dependent-item unlock | Atomic same-schema multi-section context, structure-global identity, cross-section reference hydration, and section-qualified validation evidence are implemented and locally proven for SBRT-021 multi-schema binding and later canonical writing. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Section identity/path context is flattened | Ambiguous failures/output | Require section-qualified validation and round-trip assertions |
