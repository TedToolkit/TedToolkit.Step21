# SBRT-019: Reference hydration and read failures

## 📌 Status

Completed

## 🚦 Delivery priority

- Priority: P2
- Rationale: Ordinary direct relationship navigation requires atomic two-phase identity binding.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-014 supplies generated physical references; SBRT-018 supplies atomic typed reading.
- Recommended order: Before multi-section binding and writer.
- Governing records: AP-003, EP-003, ADR-0005, and two-phase hydration architecture pinned by the parent change.

## 🧩 Explicit governing constraints

Allocate/register every entity before resolving occurrence references. Public properties expose actual generated interfaces—never ID wrappers, lazy proxies, resolver calls, or partial entities. Missing, unsupported-external, and incompatible reference failures aggregate into the `ValidationResult` retained by `ExchangeStructureReadValidationException` rather than being reclassified as syntax or general binding diagnostics.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Generated reference mapping, two-phase binder, aggregate read failures, fixtures, docs | Forward/cyclic references hydrate to actual object identity; missing, unsupported external, and incompatible targets aggregate atomically | External resource resolution, lazy binding, public raw references, or multiple governing schemas |

## 🔍 Current behavior and impact boundary

Simple typed read exists after SBRT-018 but cannot resolve entity-valued parameters. Entity identity and generated reference occurrence contracts are already fixed.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-014 direct-reference projection and SBRT-018 atomic reader complete | Both completion records | Reference binding cannot prove physical mapping or publication safety |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-06 | A data section references `#n` before its definition or participates in a cycle | Read the model and access the generated property | Two-phase binding completes, and the property returns the actual target entity interface without a wrapper, proxy, or resolver call |
| BC-07 | A reference target exists but is not assignable to the generated property interface | Read and bind the model | The read boundary throws its aggregate validation exception containing a source-located incompatible-target issue and returns no `ExchangeStructure`, syntax graph, or generated entity from the failed attempt |
| BC-08 | A reference is missing or an external reference is unsupported | Read and bind the model | The aggregate exception distinguishes missing from unresolved-external across all detected occurrences and returns no `ExchangeStructure`, syntax graph, or generated entity |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Successful relationships | Direct generated interface property holds the same allocated target object across forward/cyclic paths | No wrapper, proxy, resolver, entity ID, or container back-reference |
| Failed reference validation | All detected reference failures carry stable source/path/type evidence in one `ExchangeStructureReadValidationException` result and return no public graph | Missing and unsupported-external remain distinct; no fail-fast partial publication |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-06 | End-to-end reference integration | Forward, backward, shared, and cyclic properties are reference-equal to registered targets | Fast TUnit project |
| BC-07 | Negative atomic read | Multiple incompatible targets aggregate source/type/path failures in one exact read-validation exception and expose no graph | Fast TUnit project |
| BC-08 | Negative atomic read | Multiple missing and external references are separately coded in one exact read-validation exception with no leaked state | Fast TUnit project |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Two-phase hydration | Identity/cycle/share fixtures pass with direct property navigation |
| Failure completeness | Incompatible/missing/external multi-failure fixtures prove atomic aggregate exceptions and no leaks |

## ⏱️ Workload estimate

- Planning range: 0.6–1.1 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Operational external-resource resolution remains a non-goal.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Started from `7d17f21`. Extended only the existing internal reader, syntax capability gate, generated descriptor hydration, focused generated-consumer tests, and reference/read conformance documentation. Every entity is allocated and registered before any occurrence parameter is resolved. No public wrapper, proxy, resolver, raw reference, parser graph, context object, external-resource loader, multi-section/schema binder, branch, remote, or package was added. `validate-work-items.sh docs/changes/P2-schema-bound-round-trip` reported `Work-item delivery boundary: valid`. |
| Behavior-case proof | Red: all three `ReferenceReadTests` initially failed with `ExchangeStructureCapabilityException` because entity occurrences were outside the typed-read boundary. Green/refactor: the focused tests passed 3/3 and the complete fast TUnit project passed 209/209. BC-06 proves forward, backward, shared, aggregate, and cyclic navigation returns the same registered generated objects through declared generated interfaces. BC-07 aggregates six incompatible direct/aggregate physical parameters, including an aggregate that also contains a missing occurrence, as source-located `P21.READ.REFERENCE.TYPE` evidence without reclassifying ordinary shape/value errors. BC-08 aggregates four missing and two declared-external occurrences with distinct stable codes, recursive paths, and exact `<reader>` coordinates. Failed reads expose only the retained `ValidationResult`. |
| Regression and dependency proof | Isolated Release solution build passed with 0 warnings and 0 errors. Integration TUnit passed 3/3 enabled cases with the explicit opt-in external corpus case skipped. Runtime `IsAotCompatible`, trim-analyzer, and AOT-analyzer build passed with 0 warnings/errors. Runtime dependency inspection reports only `Antlr4.Runtime.Standard` 4.13.1. Runtime production scanning found no reflection, dynamic dispatch, expression compilation, or assembly scanning. |
| Migration and documentation | `docs/conformance/reference-hydration.md` records the two-phase identity algorithm, exact failure codes/paths/source locations, atomic lifetime, generated static target checks, and staged external/multi-section/complex boundaries. Public Read XML, the simple-read boundary, generated-descriptor guide, and README now identify supported structure-local references without claiming external resolution. |
| Independent review | Final read-only trace review compared the work item, governing change decisions, runtime and generator diff, generated property/descriptor behavior, focused and regression tests, public surface, and documentation. No blocking or advisory findings remain. Review tightened target classification so only a resolved `Entity` object statically incompatible with the expected generated interface produces the internal indexed mismatch evidence; aggregate shape, omission, and non-reference value errors retain ordinary binding behavior, while mixed incompatible/missing aggregate evidence remains complete. |
| Effort and variance | Approximately 1.0 elapsed implementation/review hour, below the 0.6–1.1 person-month planning range because entity identity, generated reference properties, descriptor dispatch, atomic reading, and validation evidence contracts were already complete. Operational external acquisition remains explicitly staged. |
| Dependent-item unlock | Complete one-schema/one-data-section relationship allocation, identity binding, direct property hydration, and atomic reference-failure aggregation are implemented and locally proven for SBRT-020, SBRT-022, and SBRT-023. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Hydration uses temporary nulls unsafely | Leaks invalid typed state | Parser-only access and failure-lifetime tests gate completion |
