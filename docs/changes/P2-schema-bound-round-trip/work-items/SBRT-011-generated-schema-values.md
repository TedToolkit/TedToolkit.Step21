# SBRT-011: Complete schema scalar and union values

## 📌 Status

Implemented

## 🚦 Delivery priority

- Priority: P2
- Rationale: Every non-aggregate ISO/EXPRESS scalar and physical-parameter distinction needs one complete generated/runtime owner.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-006 supplies runtime `Entity`; SBRT-008 supplies bound type declarations; SBRT-009 supplies the generator host.
- Recommended order: Independent of entity generation after shared prerequisites; before descriptors/direct references.
- Governing records: AP-003, EP-003, and the generated type-system architecture pinned by the parent change.

## 🧩 Explicit governing constraints

INTEGER, REAL/NUMBER, STRING, BINARY, BOOLEAN, three-state LOGICAL, enumeration, typed/untyped parameters, occurrence references, OPTIONAL absence `$`, and derived marker `*` use schema-neutral AOT-ready representations. One non-nested public `ParameterValue` union carries physical parameter forms between the internal parser, generated descriptor, and runtime writer without exposing syntax. Defined types use nominal readonly wrappers and selects use typed discriminated values; no distinction collapses to a CLR default, alias, or `object`.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Exact non-nested `ParameterValue` plus runtime scalar values, value-type generation, XML docs, and generated-consumer tests | Complete primitive/parameter/occurrence-reference/defined/enumeration/select values compile, edit, compare, and expose strong canonical mapping inputs | Syntax nodes/contexts, aggregate projection, general expression execution, entity mapping orchestration, or reflection-based union discovery |

## 🔍 Current behavior and impact boundary

No public schema scalar/parameter value system is implemented. Internal Part 21 syntax may preserve tokens, but generated consumers still require exact typed representations that do not conflate LOGICAL UNKNOWN, `$`, `*`, or untyped values with CLR defaults.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-006, SBRT-008, and SBRT-009 completed contracts | Completion evidence from all three items | Resolved entity-reference alternatives, schema type alternatives, or output composition would be unstable |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-05 | A defined type, enumeration, select, or aggregate | Compile, edit, validate, and write generated values | Generated values preserve each schema distinction; temporary constraint violations are editable, and boundary validation reports every remaining violation before publication/output |
| BC-05B | A schema and Part 21 values use INTEGER, REAL/NUMBER, STRING, BINARY, BOOLEAN, LOGICAL/UNKNOWN, enumeration, typed/untyped parameters, `$`, and `*` | Generate, read, edit, validate, and write the values | Each ISO/EXPRESS distinction has an AOT-ready schema-neutral representation, LOGICAL remains three-state, absence/derived markers are not conflated with CLR defaults, and canonical writing emits the corresponding parameter form |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Primitive/parameter fidelity | Numeric/string/binary/bool/LOGICAL and typed/untyped/`$`/`*` values remain distinguishable and canonical | No conflation with null/default, lossy numeric narrowing, or stringly typed fallback |
| Minimal parameter bridge | `ParameterValue` carries only ISO parameter distinctions, recursively represents aggregate parameters, and can represent a resolved runtime `Entity` for internal-to-generated hydration/projection | No public syntax token/tree, parser context, `object` payload, nested public type, or resolver state |
| Nominal schema values | Defined/enumeration/select types cannot be accidentally interchanged and preserve declared alternatives | No aliases, C# enum limitation leak, or `object` fallback |
| AOT/determinism | Construction and matching are statically generated | No reflection, dynamic code, or generator-only runtime dependency |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-05 | Generated-consumer compile/runtime contract | Valid values compile and match; invalid alternative construction is rejected or represented for later validation according to the approved type contract | Fast TUnit generator tests |
| BC-05B | Runtime/generated read-write contract | Boundary/extreme numeric, escaped string/binary, bool/three LOGICAL states, typed/untyped, `$`, and `*` remain distinct and emit/reparse the canonical parameter form | Fast TUnit project with table-driven lexical/value fixtures |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Complete scalar/parameter system | Public API and positive/boundary/negative tests cover every BC-05B category through one non-nested strong `ParameterValue` contract without lossy/default-value conflation or syntax leakage |
| Nominal/union contract | XML and compile/runtime tests cover defined construction, known/extensible symbols, and exhaustive select matching |

## ⏱️ Workload estimate

- Planning range: 0.4–0.9 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Aggregate categories are SBRT-012; SBRT-013/018/022 consume the mapping forms. General EXPRESS operator semantics remain SBRT-016.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Started from `d466ec1`. Added only schema-neutral scalar/physical-parameter values, internal canonical parameter formatting, non-aggregate generated value projection, supported scalar entity properties, generated-name collision coverage, focused tests, public API/XML audits, and generated-value documentation. Schema aggregates, descriptors, hydration, validation execution, and public writing remain in their later work items. The architecture syntax-object table was corrected from the stale combined entity/SELECT `Record` row to the already-approved ordinary entity `Class` and SELECT `Record` mapping; no governing decision changed. |
| Behavior-case proof | BC-05 generated-consumer tests compile and execute nominal nested defined values, closed/extensible enumerations, closed-set extension symbols, sealed record SELECT unions, exhaustive matching, value equality, cross-schema qualification, entity scalar properties, atomic transformed-name collisions, and safe deferral when a SELECT extension still depends on SBRT-012. BC-05B runtime tests distinguish arbitrary INTEGER, exact REAL/NUMBER, decoded/empty/escaped STRING, exact/empty/leading-zero BINARY, both BOOLEAN states, all three LOGICAL states, enumeration, typed/untyped, recursive aggregate, resolved entity-reference, `$`, and `*` values; canonical forms reparse through the Part 21 grammar. Negative tests reject null/invalid union payloads, invalid extensible symbols, inaccessible closed-enumeration construction, syntax/object/nested-public leakage, and lossy/default-value conflation. |
| Migration and documentation | `README.md` links the generated-value capability. `docs/conformance/generated-schema-values.md` documents exact scalar mappings, nominal/enumeration/SELECT construction and equality, closed-set extension behavior, `ParameterValue`, collision/AOT rules, and the SBRT-012/descriptor/read/validation/write staging boundary. Runtime and generated caller-facing contracts have XML documentation; `SBRT-011.approved.txt` pins the exact added runtime public API. |
| Regression and deployment proof | Release solution build passed with 0 warnings/errors. Fast TUnit passed 137/137. Integration TUnit passed 3/3 enabled cases; only the explicit opt-in external-network corpus case was skipped. Runtime `IsAotCompatible`, trim, and AOT analyzer build passed with 0 warnings/errors. Runtime dependency inspection still reports only `Antlr4.Runtime.Standard` 4.13.1. Limited whitespace/analyzer verification passed after normalizing the final test-file newline. The final strict read-only implementation review found no blocking, important, suggestion, or design-deviation findings and concluded Ready to merge. |
| Dependent-item unlock | Complete scalar/parameter/defined/enumeration/SELECT contracts and strong generated entity properties now supply the non-schema-aggregate values required by SBRT-013, SBRT-014, SBRT-016, SBRT-018, and SBRT-022. |
| Actual effort and variance | Completed in one agent implementation session; the human person-month estimate is not directly comparable, and no scope-expanding variance was introduced. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Numeric/binary representation narrows ISO/EXPRESS values | Resolved | `BigInteger`, exact decimal `RealValue`, and bit-accurate `BinaryValue` plus boundary emit/reparse fixtures avoid narrowing |
| Extensible enumeration semantics exceed a fixed representation | Resolved | Nominal record structs accept canonical extensible symbols and publish every known closed-set extension symbol without using a C# enum |
