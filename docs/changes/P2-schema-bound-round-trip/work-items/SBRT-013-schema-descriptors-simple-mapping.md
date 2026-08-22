# SBRT-013: Schema descriptors and simple physical mapping

## 📌 Status

Implemented

## 🚦 Delivery priority

- Priority: P2
- Rationale: Binder and writer need a generated, reflection-free schema identity and physical mapping contract.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-010 supplies entities; SBRT-011 supplies scalar/select values; SBRT-012 supplies aggregates.
- Recommended order: Before typed read, direct validation, and writer items.
- Governing records: AP-003, EP-003, ADR-0003, and schema-descriptor architecture pinned by the parent change.

## 🧩 Explicit governing constraints

Descriptors are generated mapping/validation infrastructure, not a new ISO domain object. The public base is an abstract `class` and every schema descriptor is an ordinary sealed `class`, not a `record`: descriptors have singleton identity and behavior but no value/structural-equality semantics. The public base exposes `SchemaName Name`, runtime-internal non-virtual dispatch, and exact protected `AllocateEntityCore`/`HydrateEntityCore`/`ValidateCore`/`GetCapabilityDiagnosticsCore`/`ProjectEntityCore` hooks over strong public/BCL values; every schema generates one sealed descriptor with `Instance`. Hydration/projection use ordered component-name/strong-parameter-list pairs. As corrected by CD-46 for SBRT-015, validation additionally receives an ordered path/entity pair list derived solely by `ExchangeStructure` from its private registration index, because a generated override in a consumer assembly cannot inspect runtime-internal registrations. No public context, raw syntax, projection DTO, resolver, registry, entity enumeration, or writer callback exists. `DataSection` retains only its governing `SchemaName`; dispatch never uses reflection/discovery. Schema-bound manual construction snapshots descriptors and rejects duplicate names before returning a structure.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Exact context-free `SchemaDescriptor` internal-dispatch/protected-core contract, sealed generated singleton descriptors, schema-bound construction, simple strong parameter metadata/factories/projection, diagnostics, tests | Cross-assembly statically dispatched descriptors expose deterministic lookup and every simple parameter form/order without leaking syntax or adding stage concepts | Public raw syntax/tree/graph, context/resolver/registry/writer facade, mutable parser state, public rule builder, complex mapping, reflection discovery, or an `AutomotiveDesign` facade |

## 🔍 Current behavior and impact boundary

Generated types have no binder/writer metadata. This item supplies the smallest reflection-free descriptor proving one complete simple mapping and schema mismatch behavior.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-010, SBRT-011, and SBRT-012 generated contracts compile | Their completion evidence | Descriptor factories and physical values cannot be type-safe |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-09 | A simple entity has explicit and absent OPTIONAL attributes | Read then write it | Generated properties receive typed values/absence, and output parameters retain standard order with `$` for absence |
| BC-13 | `FILE_SCHEMA` does not match the generated schema metadata | Bind or write the model | A schema-mismatch diagnostic prevents accidental binding/writing under the wrong schema |
| BC-11D | A consumer creates an exchange structure manually with a header and zero, one, or duplicate generated descriptors | Construct the structure and edit its data-section collection | Descriptor-free construction permits temporarily unbound editing; schema-bound construction snapshots unique descriptors; duplicate schema names throw `ArgumentException` before construction succeeds; header and data sections remain ISO values, and adding/removing sections performs no automatic validation |
| BC-23A | A consumer supplies generated descriptors and uses the public Part 21 boundary | Call `ExchangeStructure.Read`, `structure.Write`, or `structure.WriteEntity` | Only strong public values cross generated descriptor hooks; no public syntax/context/reader/writer facade or nested public type exists; duplicate descriptor schema names throw `ArgumentException` before input is consumed; stage failures use their dedicated complete-evidence exception; write validation/capability failures produce no output, while I/O failures retain their underlying exception behavior |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Descriptor dispatch/accessibility | Exact abstract base `class`/internal dispatch/protected core/name/singleton and generated sealed `class` compile across an ordinary packed consumer and map/project an ordered simple one-component strong `ParameterValue` shape directly | Neither descriptor is a `record`; `ExchangeStructure` remains non-generic; no public context/projection DTO, syntax/raw graph, resolver, registry, facade, nested type, reflection, or dynamic dispatch |
| Construction binding | Generated descriptors snapshot into the structure by unique nominal schema name; duplicate names fail before construction succeeds | Descriptor collection mutation after construction cannot change dispatch and no section edit validates |
| Simple mapping | Inherited-free explicit parameters retain physical order and every SBRT-011 typed/untyped/absence/derived form | No reflection/property-name guessing or schema-specific runtime dependency |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-09 | Generated descriptor contract | A representative simple entity maps typed present/absent values to exact physical slots and back | Fast TUnit generator/runtime tests |
| BC-13 | Negative descriptor lookup | Wrong/missing `SchemaName` produces stable mismatch diagnostics and no fallback mapping | Fast TUnit project |
| BC-11D | Descriptor-bound construction contract | Unique generated descriptors are snapshotted; later source-collection mutation is inert; duplicates fail before a structure is returned; section edits do not validate | Fast TUnit generated-consumer tests |
| BC-23A | Minimal public descriptor/cross-assembly contract | Generated descriptor is sealed/singleton/assignable; runtime-internal dispatch reaches every protected core override through strong values in a packed consumer; API contains no syntax/context/resolver/registry/facade or nested public type | Generator compile/runtime tests, packed-consumer build, public API/XML snapshot |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Descriptor contract | Packed generated-consumer/API tests prove exact base/internal-dispatch/protected-core signatures, sealed singleton, deterministic snapshot/duplicate behavior, strong direct dispatch/projection, and zero public stage/syntax concepts |
| Simple physical mapping | Round-direction parameter tests prove order, every scalar/parameter form, and no reflection |

## ⏱️ Workload estimate

- Planning range: 0.5–1.0 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Complex mappings and complete read/write boundaries are SBRT-018/SBRT-023.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Started from `1974230`. Extended only the approved abstract `SchemaDescriptor` protected-core/runtime-dispatch ABI, exact schema lookup diagnostics, generated ordinary sealed singleton descriptor classes, simple physical allocation/hydration/projection, reserved descriptor-name collision checking, tests, packed-consumer proof, and descriptor documentation. Neither base nor generated descriptor is a `record`; no public context, syntax, projection DTO, resolver, registry, writer facade, reflection, dynamic dispatch, schema-specific runtime dependency, rule execution, read boundary, or write boundary was added. The work-item validator reported `Work-item delivery boundary: valid`. |
| Behavior-case proof | BC-09 round-trips mandatory/absent OPTIONAL values, every raw scalar without narrowing, nominal wrappers, closed/extensible enumerations, typed SELECT and entity alternatives, resolved entity identity, and literal-bounded `ARRAY`/`LIST`/`BAG`/`SET` candidates with exact bound/`OPTIONAL`/`UNIQUE` metadata; `*` on an explicit attribute returns `P21-BIND-PARAMETER`. BC-11D proves descriptor-free editing, unique ordered snapshotting, inert source-list mutation, and pre-construction duplicate rejection. BC-13 proves exact case-sensitive lookup and stable `P21-BIND-SCHEMA`. BC-23A fixes the exact public/protected/internal ABI, directly executes all five non-virtual dispatch paths, proves a sealed non-record singleton with no nested type, rejects the reserved `SchemaDescriptor` generated-name collision atomically with `STEP21EXP004`, and compiles the descriptor from the real packed package. Fast TUnit passed 151/151. |
| Migration and documentation | `README.md` links `docs/conformance/generated-schema-descriptors.md`, which documents why descriptors are classes rather than records, exact singleton/dispatch shape, strong simple mapping, diagnostics, aggregate constraint authority, and deferred complex/read/validation/write behavior. Generator-host and aggregate conformance documents were updated from the former marker/later-descriptor boundary. Runtime and generated caller-visible members have XML summaries; `SBRT-013.approved.txt` pins the complete public/protected base-class surface. |
| Regression and deployment proof | Release solution build passed with 0 warnings/errors. Integration TUnit passed all 3 enabled cases and compiled an ordinary packed consumer against the generated descriptor; only the explicit opt-in network corpus case was skipped. Runtime `IsAotCompatible`, trim, and AOT analyzer build passed with 0 warnings/errors. Runtime dependency inspection still reports only `Antlr4.Runtime.Standard` 4.13.1. Limited formatting, `git diff --check`, and work-item validation passed. Strict read-only review found and corrected an extreme ARRAY-domain overflow risk, a reserved descriptor-name collision gap, and repeat-run packed-fixture isolation; the final review found no remaining blocking, important, suggestion, or design-deviation findings. |
| Dependent-item unlock | The exact reflection-free descriptor ABI and simple strong physical mapping now supply SBRT-014 direct-reference enumeration, SBRT-015 structural validation, SBRT-018 atomic simple typed read, and SBRT-022 canonical projection without exposing stage machinery. |
| Actual effort and variance | Completed in one continuing agent implementation session; the human person-month estimate is not directly comparable. Supporting literal-bounded simple aggregate mapping was included because SBRT-011/012 parameter forms are part of this item's approved physical mapping; symbolic/nested aggregate execution and complex components remain explicitly deferred. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Descriptor is presented as an ISO section value | Violates AP-003 | Keep it explicitly infrastructure and absent from entity/domain relationships |
