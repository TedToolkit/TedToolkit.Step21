# SBRT-008: Closed-set EXPRESS binding

## 📌 Status

Implemented

- Implemented from starting SHA `dc0807f` on 2026-08-21. A separately verified grammar correction discovered during binding review was committed as `9dddbc1`; SBRT-008 itself adds only Analyzer-internal binding IR/compiler behavior, focused tests, and closed-set documentation.

## 🚦 Delivery priority

- Priority: P2
- Rationale: Deterministic generation requires one explicit closed compilation set and complete binding diagnostics.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-007 supplies complete source-located EXPRESS IR.
- Recommended order: Before generator host and executable semantics.
- Governing records: AP-002/003, EP-003, and architecture closed-set compiler rules pinned by the parent change.

## 🧩 Explicit governing constraints

All `.exp` AdditionalFiles in one compilation form the entire schema universe. Binding performs no implicit disk, network, package, or environment lookup and aggregates source-located diagnostics deterministically.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Analyzer schema-set collection, name/import/type binding, immutable bound IR, diagnostics, tests | `USE FROM`/`REFERENCE FROM`, declarations, inheritance, and type references resolve across supplied files with conflicts/missing inputs diagnosed | Source generation, schema acquisition, general executable evaluation, or partial output for an invalid dependency closure |

## 🔍 Current behavior and impact boundary

The Analyzer contains only the generated parser. No AdditionalFiles pipeline, closed-set name binding, or schema diagnostics exist.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-007 complete immutable source IR | SBRT-007 completion evidence | Imports and name/type references would bind against incomplete syntax data |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-01A | Multiple `.exp` AdditionalFiles declare schemas connected by `USE FROM` or `REFERENCE FROM` | Build the consumer project | Imports resolve deterministically across the closed input set and generated declarations preserve the imported schema semantics |
| BC-01B | A supplied schema name is duplicated/conflicting or an imported schema is absent from the AdditionalFiles set | Build the consumer project | Source-located diagnostics identify every conflicting declaration or unresolved import; affected output is not emitted and no undeclared disk or network lookup occurs |
| BC-02 | An EXPRESS syntax or name-binding error | Build the consumer project | A source-located diagnostic identifies the schema error and affected invalid output is not emitted |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Closed inputs | Supplied files alone determine binding; ordering does not affect meaning or diagnostics | No hidden lookup or schema-name whitelist |
| Diagnostic atomicity | All detectable conflicts/missing/type errors are source-located and suppress affected bound output | Generator never crashes on user schema errors |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-01A | Multi-file compiler integration | Reordered input files produce equivalent bound imports/inheritance/types | Fast TUnit project |
| BC-01B | Negative compiler integration | Duplicate/conflicting/missing schemas produce all expected spans and no external access/output | Fast TUnit project with I/O-denied harness |
| BC-02 | Diagnostic contract | Syntax and semantic failures remain distinct, source-located, deterministic, and non-crashing | Fast TUnit project |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Closed binding | Positive and negative multi-file cases pass independent of AdditionalFiles order |
| Safe failure | Every affected invalid schema suppresses output while independent valid schemas follow the documented policy |

## ⏱️ Workload estimate

- Planning range: 0.5–0.9 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Inputs are already supplied by Roslyn; source generation is SBRT-009.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Recorded result |
| --- | --- |
| Delivery-boundary check | Starting SHA `dc0807f`. Added only Analyzer-internal closed-set source/compilation, immutable symbol/type/name/import/schema IR, deterministic binder diagnostics, focused TUnit cases, and the closed-set conformance document/README link. No public runtime API, generator host, source emission, external acquisition, interpreter, reflection, dynamic code, or dependency was added. The work-item validator reported `Work-item delivery boundary: valid`. |
| Behavior-case proof | BC-01A passed reordered explicit/full USE and REFERENCE imports, aliases, case-insensitive lookup, USE re-export/REFERENCE non-re-export, circular interfaces, inheritance, inverse/unique/qualified attributes, all declared type families, qualified expression names, lexical/query/repeat scopes, and nested algorithm declarations. BC-01B passed duplicate schemas/declarations, conflicting names, absent schemas/resources, wrong declaration kinds, invalid top-level/nested type and inheritance cycles, impossible logical paths, transitive invalid-closure suppression, and independent valid-schema publication. BC-02 passed distinct syntax/binding diagnostics plus unresolved name/type aggregation. Fast TUnit passed 109/109. |
| Migration and documentation | `docs/conformance/express-closed-set-binding.md` documents supplied-text-only compilation, case-insensitive identity, interface/re-export rules, legal cyclic imports, deterministic failure policy, immutable bound IR, and the execution boundary. No consumer migration applies because all new contracts are Analyzer-internal. |
| Dependent-item unlock | Valid schemas now expose deterministic imports, declarations, inheritance, attributes, complete declared types, and source-ordered resolved name targets for SBRT-009 through SBRT-013 and SBRT-016. Invalid dependency components publish no bound schema. |
| Regression and deployment proof | Release solution build passed with 0 warnings/errors; fast TUnit passed 109/109; integration TUnit passed 2/2 enabled tests with the explicit opt-in network corpus test skipped; pinned generated-artifact regeneration remained byte-stable. Production binding code contains no file/directory/environment/network/reflection/dynamic-code access and Analyzer dependencies remain unchanged. Strict read-only review found no blocking or advisory findings. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Import/redeclaration ambiguity differs from assumed name rules | Incorrect generated semantics | Normative EXPRESS evidence governs; material behavior change returns to change design |
