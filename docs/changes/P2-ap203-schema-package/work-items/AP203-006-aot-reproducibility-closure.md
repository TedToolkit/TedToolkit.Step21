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

### Candidate completion evidence

| Evidence | Result |
| --- | --- |
| Candidate and review boundary | Baseline `e9a7346`; implementation `beb7aad`; review remediation `027b517`. Read-only review of `e9a7346..beb7aad` found that the first API fingerprint omitted enum values, exact type/accessor shape, and parameter modifiers and that the reproducibility command allowed an incremental producer build. Remediation added those dimensions and forced recompilation; read-only review of `beb7aad..027b517` concluded Ready with no remaining finding or design deviation. |
| Changed artifacts | `TedToolkit.Step21.Ap203.csproj` declares package metadata and the tested core range; the package README records identity, provenance, OCCT scope, unsupported boundaries, and compatibility classification; `PackageTests` plus `PublicApi.approved.sha256` enforce the real `.nupkg`/descriptor/source/fixture/API contract; `verify-ap203-package.ps1` and the AP203 mode of `verify-native-aot.ps1` provide bounded offline and AOT gates. No runtime reader, writer, model, generator, or schema source changed. |
| AC-11 Native AOT | `pwsh -NoProfile -File ./build/verify-native-aot.ps1 -Ap203` restored only from a temporary local feed, emitted no IL/ILC/AOT warning, published `win-x64`, and ran all four fixed journey markers: fixture read, semantic round trip, 9-failure/zero-byte invalid edit rejection, and positioned unsupported-extension rejection. Output ended `AP203_NATIVE_AOT_PACKAGE_PROOF_OK win-x64 executable-bytes=39154176 compiler-package=10.0.11`. The unchanged generic mode was also rerun and ended `NATIVE_AOT_PACKAGE_PROOF_OK` with a 3,520,000-byte executable. |
| AC-12 repeat clean/offline proof | `pwsh -NoProfile -File ./build/verify-ap203-package.ps1` was run twice. Each invocation forced a non-incremental AP203 rebuild, packed candidate runtime/AP203 packages, restored an empty isolated consumer cache from a `NuGet.Config` containing only the temporary candidate feed, and executed the complete fixture journey without OCCT or network. Both runs produced normalized public-package SHA-256 `2BEBC39E95BE86145DC35C85743ED522AC53141AE406136EA7B99180FF77996E`, canonical schema SHA-256 `19497DCA88C6FCFE763DA23772B68356BE4361668426954DE9863E4285D0C251`, and fixture SHA-256 `2F40CE06A8646B3AE33A8BD871181A356D413CDD6B864D9C8D484A3D1E127B62`. Required compiler/runtime packages are copied explicitly into the temporary AOT feed before the isolated restore; no implicit global source is available to that restore. |
| AC-13 package/version/API contract | The actual `TedToolkit.Step21.Ap203.1.0.0.nupkg` declares `TedToolkit.Step21 [1.0.0,2.0.0)` and contains the package README, LGPL-3.0-only metadata, and no schema/analyzer/RoslynHelper/JSON/XML runtime asset. Reflection over the packaged assembly confirms descriptor name `config_control_design`; the nullable-aware generated public-surface fingerprint, including enum constants, type shape, accessor visibility, and parameter modifiers/defaults, matches approved SHA-256 `CD3FE5BEC20E5990451DA1428E758889702EF1C768D90581308305EE7C45F1A8`. The package is 1,924,060 bytes and its generated assembly is 14,647,296 bytes, below the redesign threshold. |
| Regression gates | Fast TUnit passed 344/344. Final integration passed 7 enabled tests with 0 failures; the single network-backed external corpus remained explicitly skipped. The final Release solution build completed in 3m20s with 0 warnings and 0 errors. Both PowerShell scripts parse successfully, `git diff --check` is clean, and every proof-owned temporary directory was removed after its absolute path was validated under the OS temporary directory. |
| Documentation and dependency audit | The packaged README and project copy are byte-equal and agree with ADR-0006 on AP203 Amendment 1 identity, exact STEPcode/OCCT revisions, schema/fixture checksums, explicit descriptor use, supported core range, SemVer breaking rules, exclusions, and interoperability-not-conformance language. Package/deps inspection continues to show one AP203 package and one core runtime with no Analyzer, RoslynHelper, JSON, XML, reflection discovery, or second parser/writer runtime edge. |

## Risks and implementation notes

The generated assembly may materially increase package or AOT size. Measure and report it; if size,
publish time, or warnings require a new public packaging model or exceed the parent estimate, stop
and return to change design instead of adding reflection, dynamic code, or broad suppressions.
