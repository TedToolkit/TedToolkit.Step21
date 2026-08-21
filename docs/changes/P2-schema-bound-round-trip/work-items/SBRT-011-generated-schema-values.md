# SBRT-011: Complete schema scalar and union values

## 📌 Status

Approved

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
| Delivery-boundary check | Starting SHA and generated/test/XML artifacts |
| Behavior-case proof | BC-05 and BC-05B focused compile/runtime/read-write results |
| Migration and documentation | Generated value usage documentation |
| Dependent-item unlock | Complete scalar/parameter/union contracts for SBRT-013, SBRT-014, SBRT-016, SBRT-018, and SBRT-022 |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Numeric/binary representation narrows ISO/EXPRESS values | Round-trip loss | Boundary fixtures must prove supported ranges or diagnose unsupported values before publication |
| Extensible enumeration semantics exceed a fixed representation | Loss of valid symbols | Preserve EXPRESS semantics; do not fall back to C# enum |
