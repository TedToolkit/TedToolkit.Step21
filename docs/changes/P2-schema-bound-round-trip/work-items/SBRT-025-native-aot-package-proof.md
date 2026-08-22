# SBRT-025: Native AOT and package boundary

## 📌 Status

Completed

## 🚦 Delivery priority

- Priority: P2
- Rationale: Native AOT is mandatory and must be demonstrated by a real packed generated-schema consumer.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-021 supplies representative multi-schema binding; SBRT-023 semantic round trip; SBRT-024 final validation boundary.
- Recommended order: Before final conformance/documentation closure.
- Governing records: AP-002/003, EP-003, ADR-0002/0003/0005 pinned by the parent change.

## 🧩 Explicit governing constraints

The proof must consume the packed product, execute representative generated schema read/validate/edit/write behavior, and publish/run Native AOT for the executable proof RID `win-x64`. No reflection discovery, dynamic code, validation framework, JSON/XML contract, RoslynHelper runtime edge, generator-only dependency, public syntax/stage context, reader/writer facade, registry/resolver, or nested public type may leak. This proof RID is not an exclusive platform-support declaration.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Packed-consumer/AOT fixture, package layout/dependency gates, Windows CI automation, tests/docs | Representative simple/complex/multi-schema generated consumer packs, publishes, runs, validates, and round-trips for `win-x64` | Claiming this is the only supported RID, adding a validation package, or expanding serialization/domain APIs to satisfy the fixture |

## 🔍 Current behavior and impact boundary

The runtime targets net10.0 and packages a netstandard2.0 Analyzer, but no packed generated consumer or Native AOT publish/run evidence exists.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-021, SBRT-023, and SBRT-024 representative final paths complete | Their completion records | AOT proof would exercise only a toy path and leave core generated execution unproven |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-16 | The same `.exp` and options are built repeatedly | Compare generated sources | Hint names and generated C# are deterministic and compile without RoslynHelper references in consumer output |
| BC-16B | A packed consumer generates representative simple, complex, and multi-schema models | Publish and run it with Native AOT for `win-x64` | The native application executes read, edit, validate, and write successfully with zero trimming/AOT/dynamic-code warnings and no forbidden runtime dependency; the RID is a proof target rather than an exclusive support list |
| BC-18 | A consumer looks for JSON/XML support or dependencies | Inspect generated/runtime APIs and package graph | No such serialization contract, adapter hook, attribute, or dependency is present |
| BC-19 | A generated public type or member is proposed without an ISO 10303-21 or selected EXPRESS-schema source | Review generated API and metadata | The proposal is rejected or reclassified and isolated as explicitly named implementation infrastructure |
| BC-23A | A consumer supplies generated descriptors and uses the public Part 21 boundary | Call `ExchangeStructure.Read`, `structure.Write`, or `structure.WriteEntity` | Only strong public values cross generated descriptor hooks; no public syntax/context/reader/writer facade or nested public type exists; duplicate descriptor schema names throw `ArgumentException` before input is consumed; stage failures use their dedicated complete-evidence exception; write validation/capability failures produce no output, while I/O failures retain their underlying exception behavior |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Real deployment proof | Packed consumer publishes and runs for `win-x64` through generated binding, validation, editing, and writing | No trim/AOT/dynamic-code warnings are waived/suppressed and the proof target does not imply exclusive support |
| Dependency/public API audit | Runtime/generated graph contains only authorized dependencies, necessary top-level evidence/infrastructure contracts, and ISO/EXPRESS-derived strong public values | No nested public type, syntax/context/facade/registry/resolver, JSON/XML/validation-framework/RoslynHelper runtime leak, or avoidable public concept |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-16 | Packed generator determinism | Two consumer builds from the same schema/options emit identical sources and run without RoslynHelper runtime assembly | Pack, restore local package, build twice, compare generated output |
| BC-16B | Native AOT integration | The `win-x64` packed consumer publishes with zero relevant warnings and executes representative simple/complex/multi-schema behavior successfully | Run `build/verify-native-aot.ps1` on Windows; it packs, publishes with `-r win-x64`, and executes the artifact |
| BC-18 | Package/API dependency audit | NuGet contents, deps graph, generated sources, and public API contain no JSON/XML or validation engine contract | Bounded package inspection plus Release build |
| BC-19 | Public API provenance audit | Every generated public domain member maps to ISO/EXPRESS; every remaining top-level infrastructure/evidence type has a demonstrated consumer need | Generated XML/API provenance procedure |
| BC-23A | Minimal surface and boundary execution | Packed consumer calls only the structure read/write entry points; descriptor dispatch uses strong values; API snapshot proves every forbidden public context/facade/nested type absent | Packed-consumer runtime plus public API snapshot |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Native AOT gate | `win-x64` publishes and runs the representative packed consumer with zero AOT/trimming/dynamic-code warnings |
| Clean package boundary | Analyzer loads from package; runtime/generated dependencies and the minimal non-nested public API pass forbidden-edge/provenance audits |

## ⏱️ Workload estimate

- Planning range: 0.5–0.9 person-months.
- Confidence: Medium.
- Assumptions and excluded work: A Windows CI agent provides Native AOT prerequisites; Linux/additional RID proof and an exclusive platform-support promise are excluded.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Started from `40a3a26` on `main`. Changed only the packed consumer, package/determinism/AOT gates, the minimal public entity-population view required by a real read consumer, API evidence, CI, package README metadata, and conformance documentation. No branch, remote, validation framework, serialization contract, public syntax/context/facade/registry/resolver, or nested public type was added. |
| Behavior-case proof | BC-16 builds the same two-schema package consumer twice in isolated directories and compares every generated hint path and source byte before running it. BC-16B's stable `build/verify-native-aot.ps1` command packs, restores, publishes `win-x64`, and executes the native artifact with zero build/AOT/trimming/dynamic-code warnings, producing `PACKED_AOT_OK` and `NATIVE_AOT_PACKAGE_PROOF_OK win-x64`. BC-18 rejects JSON/XML/validation-framework entries in generated source, package contents, and the runtime graph. BC-19 confirms public generated schema members come from the two `.exp` inputs while the complex composition class remains internal. BC-23A exercises only public `ExchangeStructure` boundaries and the cumulative API snapshot. |
| Package and API boundary | The packed consumer resolves no RoslynHelper runtime library; RoslynHelper and its generator-only dependencies remain under `analyzers/dotnet/cs`. Package inspection rejects RoslynHelper under `lib`, JSON/XML, and FluentValidation artifacts, and confirms the package README. `ExchangeStructure.Entities` is a live read-only registration-order population view required to navigate the result of public `Read`; focused tests prove later Add/Remove changes are reflected without exposing names, sections, or mutable registration indexes. |
| Regression and deployment proof | Release solution build passes with 0 warnings/errors; fast TUnit passes 237/237; integration TUnit passes all 3 enabled cases with the network corpus case intentionally opt-in; the runtime AOT/trim analyzer build passes with 0 warnings/errors; work-item validation and `git diff --check` pass. |
| Migration and documentation | `docs/conformance/native-aot-package-proof.md`, README, package README metadata, and the Windows proof script document the `win-x64` executable gate, packed boundary, dependency exclusions, and non-exclusive platform-support meaning. CD-40 records the maintainer's removal of the Linux proof requirement. |
| Independent review | Final trace covered generated-source determinism, simple/complex/multi-schema execution, package analyzer loading, runtime dependency isolation, Native AOT publish/run, warning policy, public API provenance, read-result navigation, JSON/XML exclusions, zero reflection/dynamic-code fallback, CI wiring, and documentation claims. It corrected the missing real-consumer navigation surface, removed the superseded Linux requirement consistently, and added the missing package README; no blocking or advisory findings remain. |
| Dependent-item unlock | The real `win-x64` Native AOT package proof unlocks SBRT-026 final conformance/documentation closure. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| AOT warning reveals reflection in a core path | Mandatory capability blocked | Fix root cause within approved boundaries; technology change returns to architecture/change approval |
