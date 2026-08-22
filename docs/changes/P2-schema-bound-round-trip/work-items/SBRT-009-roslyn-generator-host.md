# SBRT-009: Deterministic Roslyn generator host

## 📌 Status

Implemented

## 🚦 Delivery priority

- Priority: P2
- Rationale: A minimal proving generator and packed consumer de-risk all later generated features.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-008 supplies a deterministic bound schema set.
- Recommended order: Before generated type and validator items.
- Governing records: AP-002/003, EP-003, ADR-0002, and architecture generator boundaries pinned by the parent change.

## 🧩 Explicit governing constraints

Use TedToolkit.RoslynHelper structurally through final emission. Generated code depends only on Step21 runtime contracts; RoslynHelper and Analyzer-only dependencies must not leak into the consumer runtime graph.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Incremental generator host, RoslynHelper dependency/packaging, deterministic hint/source tests, packed-consumer fixture | An arbitrary valid schema produces a minimal deterministic compiled schema artifact from AdditionalFiles | Full entity/value/mapping generation, runtime schema lookup, or text-template/raw-fragment fallback |

## 🔍 Current behavior and impact boundary

The Analyzer project has no source generator or RoslynHelper dependency; the runtime package copies the Analyzer and ANTLR runtime only.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-008 deterministic valid bound schema | SBRT-008 completion evidence | A generator host cannot prove stable inputs or user-error behavior |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-01 | A valid `.exp` AdditionalFile with a non-IFC schema name | Build the consumer project | Deterministic schema types and metadata are generated without a schema-name whitelist |
| BC-16 | The same `.exp` and options are built repeatedly | Compare generated sources | Hint names and generated C# are deterministic and compile without RoslynHelper references in consumer output |
| BC-18 | A consumer looks for JSON/XML support or dependencies | Inspect generated/runtime APIs and package graph | No such serialization contract, adapter hook, attribute, or dependency is present |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Generator determinism | Equivalent schema sets/options produce identical hint names and sources | Input order and machine paths do not affect output |
| Package boundary | Packed consumer loads generator and compiles; runtime graph excludes RoslynHelper, Analyzer internals, JSON/XML | Analyzer remains netstandard2.0-compatible |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-01 | Generator-driver/consumer integration | A non-IFC schema emits and compiles the expected minimal artifact | Fast TUnit project and packed-consumer build |
| BC-16 | Determinism contract | Reordered/repeated runs produce identical hint/source bytes and no consumer RoslynHelper reference | Generator-driver tests plus package dependency inspection |
| BC-18 | API/package audit | Generated/runtime public surface and dependency graph contain no JSON/XML contract or package | `dotnet build TedToolkit.Step21.slnx --configuration Release`; package inspection |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Working generator boundary | Generator-driver and packed-consumer tests pass for arbitrary schema names and user diagnostics |
| Deterministic safe packaging | Repeated-output comparison and dependency inspection pass with zero warnings |

## ⏱️ Workload estimate

- Planning range: 0.4–0.7 person-months.
- Confidence: Medium.
- Assumptions and excluded work: RoslynHelper supports required basic declarations; later items add semantic output.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Started from `db6a746`; added the incremental generator, structural RoslynHelper emitter, source-located diagnostic bridge, release tracking, analyzer dependency packaging, generator-driver tests, and a packed-consumer fixture. The Analyzer remains `netstandard2.0`. |
| Behavior-case proof | BC-01: generator-driver and real local-package consumer compile `lunar_catalog`. BC-16: reordered inputs and unrelated Windows-style logical roots produce identical hint/source snapshots. BC-18: package entries and consumer `project.assets.json` prove analyzer-only dependencies remain outside `lib` and the runtime graph contains no RoslynHelper or JSON/XML packages. Release verification passed 112/112 fast tests and 3/3 enabled integration tests; the explicit external-network case remained skipped. |
| Migration and documentation | Root README documents the `AdditionalFiles` item and build diagnostics; `docs/conformance/express-generator-host.md` records the current marker, determinism, diagnostics, structural-composition, and package boundaries. |
| Dependent-item unlock | `ExpressIncrementalGenerator` now provides the stable AdditionalFiles, closed compilation, diagnostics, structural emission, hint naming, and packed-analyzer host used by SBRT-010–017. `dotnet build TedToolkit.Step21.slnx --configuration Release --no-restore` passed with 0 warnings and 0 errors; analyzer-format verification passed; `validate-work-items.sh docs/changes/P2-schema-bound-round-trip` reported `Work-item delivery boundary: valid`. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| RoslynHelper lacks a required basic composition primitive | Generator boundary blocked | Route a material technology-direction change through architecture approval |
