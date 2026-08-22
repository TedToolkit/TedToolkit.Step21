# SBRT-012: Generated aggregate projection

## 📌 Status

Implemented

## 🚦 Delivery priority

- Priority: P2
- Rationale: Generated schemas must retain each EXPRESS aggregate category and editable constraint state.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-005 supplies aggregate runtime contracts; SBRT-008 supplies bound declarations; SBRT-009 supplies the generator host.
- Recommended order: Before descriptors, direct references, and structural validation.
- Governing records: AP-003, EP-003, and ADR-0005 pinned by the parent change.

## 🧩 Explicit governing constraints

Generated properties/types use the matching `ExpressArray/List/Bag/Set<T>` category, preserve declared bounds/optional slots/nesting, and permit temporary violations without mutation-time validation.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Aggregate code generation, schema metadata, XML docs, generated-consumer tests | Every aggregate declaration projects to the correct runtime category and element/slot shape | Executing schema constraints, physical Part 21 mapping, or collapsing values to general collections |

## 🔍 Current behavior and impact boundary

Runtime categories arrive from SBRT-005, but no generator maps schema bounds, uniqueness, OPTIONAL slots, nesting, or entity/select elements to them.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-005, SBRT-008, and SBRT-009 completion outputs | Their completion evidence | Projection cannot compile or preserve schema distinctions |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-05 | A defined type, enumeration, select, or aggregate | Compile, edit, validate, and write generated values | Generated values preserve each schema distinction; temporary constraint violations are editable, and boundary validation reports every remaining violation before publication/output |
| BC-05A | Mutable `ARRAY`, `LIST`, `BAG`, and `SET` values contain the same elements in cases where order, multiplicity, bounds, optional slots, or uniqueness differ | Edit and explicitly validate each value | Runtime types retain the distinct aggregate category; `Validate` reports every schema violation without mutating the values, and write eligibility follows the corresponding EXPRESS semantics |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Projection fidelity | Category, nested element type, bounds, uniqueness, and optional slots match bound EXPRESS | No `List<T>` substitution or schema-reflection discovery |
| Editing | Generated aggregate members remain mutable and do not auto-validate | Runtime category behavior from SBRT-005 remains unchanged |
| Constraint ownership and construction | Generated members expose the matching raw `ExpressArray/List/Bag/Set<T>` type; generated XML and internal schema metadata retain the exact declaration, and the generated schema descriptor remains authoritative for later validation/read/write | No public aggregate factory, reflection-readable constraint attributes, or setter validation |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-05 | Generated-consumer compile test | Representative nested aggregate declarations expose exact strongly typed categories and editable members | Fast TUnit generator tests |
| BC-05A | Generated runtime integration | Same elements in four generated categories retain different state/validation inputs without mutation repair | Fast TUnit project |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Complete projection | Generated compile/runtime tests cover every category, bounded/unbounded form, nesting, OPTIONAL slots, and entity/select elements |
| Deterministic documentation | Generated XML states exact EXPRESS aggregate form and mutable validity timing |

## ⏱️ Workload estimate

- Planning range: 0.4–0.8 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Runtime aggregate semantics are complete; rule failures are SBRT-015.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Started from `0d672b9`. Added only recursive aggregate type resolution, deterministic aggregate-form XML composition, generated-consumer tests, and aggregate conformance documentation. Generated members expose the existing raw `ExpressArray/List/Bag/Set<T>` contracts; no public factory, constraint attribute, runtime API, descriptor implementation, rule execution, physical mapping, reflection, dynamic code, or dependency was added. `validate-work-items.sh docs/changes/P2-schema-bound-round-trip` reported `Work-item delivery boundary: valid`. |
| Behavior-case proof | BC-05 compiles exact generated `ARRAY`, `LIST`, `BAG`, and `SET` categories for nominal and inline declarations, recursive nesting, entity/SELECT elements, imported types, nullable outer attributes, bounded/unbounded/symbolic forms, and aggregate SELECT extensions. BC-05A executes generated wrappers around identical `[1, 1, 2]` candidates in all four categories; explicit validation retains each candidate and reports category-specific failures, while an optional ARRAY slot remains unset without a required-slot failure. Fast TUnit passed 140/140. Reordered and relocated additional files produced byte-identical generated snapshots. |
| Migration and documentation | `README.md` links `docs/conformance/generated-schema-aggregates.md`, which documents exact category mappings, recursive/nullable slot shape, direct construction, descriptor authority, mutation/validation timing, and the later descriptor/reference/read/write boundary. Generated nominal values and inline aggregate properties publish their normalized exact EXPRESS form and mutable validity timing in XML. Stale pre-SBRT-012 boundaries in the entity/value conformance documents were corrected. |
| Regression and deployment proof | Release solution build passed with 0 warnings/errors. Integration TUnit passed 3/3 enabled cases with only the explicit opt-in network corpus case skipped. Runtime `IsAotCompatible`, trim, and AOT analyzer build passed with 0 warnings/errors. Runtime dependency inspection still reports only `Antlr4.Runtime.Standard` 4.13.1. Limited formatter verification and `git diff --check` passed. Strict read-only review found no blocking, important, suggestion, or design-deviation findings and concluded Ready to merge. |
| Dependent-item unlock | Exact aggregate CLR projection and retained constraint evidence now supply SBRT-013 descriptors, SBRT-014 nested references, and SBRT-015 structural validation without committing those later behaviors early. |
| Actual effort and variance | Completed in one agent implementation session; the human person-month estimate is not directly comparable, and no scope-expanding variance was introduced. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Nested select/entity references lose physical occurrence order | Deferred by scope | Projection fixtures now retain the exact nested entity/SELECT element types; SBRT-014 owns live physical reference enumeration and order |
