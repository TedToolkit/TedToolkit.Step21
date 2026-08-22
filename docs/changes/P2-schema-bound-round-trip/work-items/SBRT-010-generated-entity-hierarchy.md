# SBRT-010: Generated mutable entity hierarchy

## 📌 Status

Implemented

## 🚦 Delivery priority

- Priority: P2
- Rationale: Mutable typed entities and interface-only EXPRESS inheritance are the central consumer model.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-006 supplies runtime Entity/identity contracts; SBRT-008 supplies bound inheritance/attributes; SBRT-009 supplies the generator host.
- Recommended order: Before descriptors, direct references, and validation.
- Governing records: AP-003, EP-003, and ADR-0005 pinned by the parent change.

## 🧩 Explicit governing constraints

Generate one interface and mutable class per EXPRESS entity. Interfaces exclusively encode EXPRESS inheritance; each class derives only from `Entity`, uses CLR reference identity, exposes mutable explicit attributes, and follows mandatory/OPTIONAL nullability without setter validation.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Entity code generation, public constructors/properties/interfaces, bounded diagnostics, XML docs, generated-consumer tests | Single/multiple inheritance, flattened storage, abstract/sealed shape, direct entity properties, nullability, unchecked editing, reference equality, and non-recursive `ToString()` | Record/value equality, generated class inheritance, IDs/container properties, wrappers/proxies, validation execution, or serialization through `ToString()` |

## 🔍 Current behavior and impact boundary

No schema types are generated today. Runtime `Entity` and schema binding inputs are supplied by prerequisites; generator output must remain deterministic and schema-derived.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-006, SBRT-008, and SBRT-009 completion contracts available | Their recorded completion evidence | Entity output cannot compile or preserve approved inheritance/identity semantics |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-03 | An EXPRESS entity with a single inheritance chain | Inspect/use generated declarations | One interface and one mutable class are generated per entity; interface inheritance preserves assignability and each instantiable class is sealed, derives only from `Entity`, and uses reference equality |
| BC-04 | An EXPRESS entity with multiple supertypes | Inspect/use generated declarations | Its interface inherits every direct supertype interface, its mutable class implements its own interface, storage is not duplicated, and no primary class base is selected |
| BC-06A | Consumer code constructs and incrementally edits a generated entity | Supply mandatory members, assign properties, and mutate aggregates | Mandatory members are non-nullable and required for normal construction, `OPTIONAL` members are nullable, no setter/mutation runs automatic validation, and any deliberately forced mandatory null is reported by later explicit/read/write validation |
| BC-22 | Call `ToString()` on an entity graph containing cycles | Format any generated entity | A bounded diagnostic string is returned without graph recursion; no ISO serialization claim is made |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Type/equality shape | Exact interface inheritance; classes derive only `Entity`, use reference equality, and store inherited explicit attributes once | No invented primary supertype or record semantics |
| Editing/diagnostics | Mandatory construction and nullable OPTIONAL API are compile-time visible; setters do not validate; `ToString()` is bounded | Entity has no name/container and `ToString()` is not Part 21 output |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-03 | Generated-code compile/reflection contract | Interface/class bases, sealed/abstract status, storage, and reference equality match schema | Fast TUnit generator tests |
| BC-04 | Multiple-inheritance generated-consumer test | All direct supertype assignments compile and storage appears once without a selected class base | Fast TUnit generator tests |
| BC-06A | Compile-time/runtime contract | Mandatory/OPTIONAL annotations and construction compile as specified; repeated mutation never invokes validation | Generated-consumer compile plus runtime assertions |
| BC-22 | Cyclic graph runtime test | Diagnostic output terminates, is bounded, and lacks ISO serialization claims | Fast TUnit project |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Entity projection | Representative single/multiple/abstract schemas compile and satisfy public API assertions |
| Editing and diagnostics | Nullability/unchecked mutation/cycle formatting tests pass and XML docs are warning-free |

## ⏱️ Workload estimate

- Planning range: 0.6–1.1 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Aggregate/value members consume later generated types; validation and hydration are separate outcomes.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA `c68899e`. Added structural entity projection/emission, atomic generation planning and diagnostics, generator-driver compile/runtime fixtures, analyzer release-ledger entries, and generated-entity conformance guidance. `validate-work-items.sh docs/changes/P2-schema-bound-round-trip` returned `Work-item delivery boundary: valid`. |
| Behavior-case proof | Red/Green generator tests cover abstract/sealed single and diamond inheritance, class-only `Entity` derivation, interface assignability, inherited storage once, renamed redeclaration aliasing, cross-schema qualification, source/type/member/keyword naming boundaries, invalid-dependency propagation, bounded unsupported narrowing, mandatory/OPTIONAL consumer diagnostics, unchecked mutation, reference equality, live direct entity occurrences, and cyclic bounded `ToString()`. Focused `EntityHierarchyTests` pass 10/10 and `GeneratorHostTests` pass 3/3. |
| Migration and documentation | `README.md`, `docs/conformance/express-generator-host.md`, and `docs/conformance/generated-entity-hierarchy.md` document namespace/type naming, constructors, mutability/nullability, staging boundaries, direct-reference scope, diagnostics, and non-serialization `ToString()`. |
| Regression and deployment proof | Release solution build passes with 0 warnings/errors; fast TUnit passes 122/122; integration passes 3/3 enabled tests with only the explicit opt-in external-network corpus case skipped; limited analyzer/test formatting verification reports no changes. |
| Dependent-item unlock | Public generated entity interfaces/classes and stable mutable storage now supply the entity contract required by SBRT-013–015; full schema values and aggregates remain owned by SBRT-011/012. |
| Actual effort and variance | Completed in one agent implementation session; the human person-month estimate is not directly comparable, and no scope-expanding variance was introduced. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Redeclaration flattening duplicates or hides storage | Resolved | Renamed direct-entity redeclaration tests prove one physical slot with inherited and renamed interface aliases; unsafe target-type narrowing is withheld with `STEP21EXP005`. |
