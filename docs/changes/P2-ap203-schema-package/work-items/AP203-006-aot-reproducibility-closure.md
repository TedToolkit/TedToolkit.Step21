# AP203-006: Prove the packaged AP203 journey is offline-reproducible and Native AOT safe

<!-- work-item-format: 2 -->

- Prior approval: 用户于 2026-08-24 批准原 AC-11/AC-12 工作项；该历史批准不覆盖本次新增的 AC-13。
- Revision approval: 用户于 2026-08-26 批准严格按 ADR-0006 → AP203 → Compiler modularization 的顺序执行，并明确接受 ADR-0006 compatibility policy；父 change 修订现已获批。

## Outcome

A clean consumer built only from locally produced packages executes the complete supported AP203
journey as a trimmed Native AOT binary, and repeat clean/offline verification proves that schema,
fixture, package, and documentation inputs are repository-owned and reproducible. The same closure
also proves that schema provenance, runtime dependency range, descriptor identity, and generated
public-surface compatibility are declared consistently and cannot drift silently.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area: packaged consumer, clean/offline verification automation, AOT gate, and final consumer documentation.
- In scope: package from the candidate revision, publish/run the fixed read-navigate-edit-validate-write-reread
  journey, inspect warnings, repeat clean build/package proof, audit package/runtime/generated-surface
  compatibility, and close source/fixture/support documentation.
- Non-goals: NuGet.org publication, platform matrix expansion beyond the repository's selected proof RID,
  benchmarking, live upstream downloads, or a second consumer API.
- Likely touchpoints (non-binding): packed-consumer project, build verification script, package tests,
  solution/build metadata, and AP203 README/conformance documentation.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| AP203-002 | Locally packable AP203 package and audited runtime dependency closure | Package path/identity and passing consumer proof |
| AP203-005 | Fixed fixture and complete verified semantic journey | AP203-005 completion evidence and fixture checksum |
| ADR-0006 | Accepted precompiled-schema packaging and version compatibility policy | Accepted ADR revision pinned by the parent change |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Verified input or primary-proof intent |
| --- | --- | --- |
| AC-11 | Owns | The trimmed Native AOT package consumer publishes and runs the full AP203 journey warning-free |
| AC-12 | Owns | Clean verification uses only checked-in schema/fixture and local candidate packages and is repeatable offline |
| AC-13 | Owns | Package metadata, dependency range, generated API comparison, provenance, and consumer documentation enforce one consistent compatibility claim |

<!-- work-item: delivery-constraints -->
## Constraints

- The consumer references packages, not source projects or consumer-supplied `.exp`/`AdditionalFiles`.
- Publish and execute a representative fixed RID using the repository's existing AOT-proof conventions.
- Treat attributable trim/AOT warnings as failures; do not suppress them without correcting the root cause.
- Verification must not require network, OCCT, hidden global packages, reflection discovery, or dynamic code.
- Reuse the existing packed-consumer shape or minimally extend it; do not create a framework around one journey.
- Do not infer compatibility solely from package compilation; inspect the real `.nupkg`, generated public surface, fixed schema identity, and documented policy together.

<!-- work-item: proof-plan -->
## Proof

| Contract or gate | Evidence purpose | Execution shape | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-11 | Journey and structural verification | End-to-end | Candidate packages publish and run a trimmed Native AOT binary that completes read, navigate, edit, validate, write, and reread with zero attributable warnings | `pwsh ./build/verify-native-aot.ps1` or its minimal AP203-capable replacement |
| AC-12 | Acceptance and reproducibility | Integration/contract | Two clean validations use no network/OCCT/consumer schema input and produce equivalent public package contents and identical source/fixture checksums | A checked-in bounded AP203 reproducibility command created by this item |
| AC-13 | Compatibility and release boundary | Contract/integration | Candidate package metadata and documentation identify the same schema source/edition and descriptor, declare the tested core runtime range, and classify breaking generated-surface differences as incompatible | Inspect the real candidate `.nupkg`; compare the generated public API against the approved baseline; validate documentation metadata from the same manifest |
| Change regression gate | Structural verification | Component | Release solution build and both TUnit executables complete with zero warnings/errors | `dotnet build TedToolkit.Step21.slnx --configuration Release`; run both test projects in Release |

<!-- work-item: definition-of-done -->
## Done

- AC-11, AC-12, and AC-13 pass from local candidate packages in a clean environment.
- AOT output executes the complete fixed AP203 journey with no attributable trim/AOT warning.
- Package/source/fixture checksums, license/provenance, supported OCCT range, unsupported extensions,
  schema identifiers, usage, dependency boundary, core runtime range, and generated-surface compatibility policy are documented.
- The full Release build, fast tests, integration tests, package proof, and AOT proof are green.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision/RID, produced package and executable identities, actual changed
artifacts, AC-11/AC-12/AC-13 commands and assertions, warning and test counts, offline resource controls,
checksum and public-API comparisons, documentation state, and final package/dependency audit result.

## Risks and implementation notes

The generated assembly may materially increase package or AOT size. Measure and report it; if size,
publish time, or warnings require a new public packaging model or exceed the parent estimate, stop
and return to change design instead of adding reflection, dynamic code, or broad suppressions.
