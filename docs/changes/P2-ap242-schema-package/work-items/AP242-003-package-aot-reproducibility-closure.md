# AP242-003: 包、AOT 与可重现性交付闭环

<!-- work-item-format: 2 -->
<!-- work-item-id: AP242-003 -->

<!-- approval-source: user-explicit-approval-2026-09-04 -->

## Outcome

在同一候选上证明 AP203/AP242 共存、两次离线重现、版本/分发契约与 trimmed Native AOT 旅程，并形成
首个 stable `1.0.0` 所需的最终交付证据。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: 双包消费者、离线 verification、AOT mode、package/API
  reproducibility、版本/许可/README closure。
- In scope: 隔离 local feed 与空缓存；审计依赖图只有一份 runtime 且 schema 包无互相依赖；两次非增量构建
  比较规范化包/API/source/fixture；trimmed Native AOT 运行 AP242-002 journey；核验全部分发 hashes/metadata。
- Non-goals: 不发布 NuGet.org，不执行外部法务或部署，不扩大 fixture capability claim。
- Likely touchpoints (non-binding): package/AOT verification scripts、two-package consumer、package metadata/README、
  final integration documentation。

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| AP242-001 Verified | Candidate package, pinned inputs and API contract | AP242-001 verification record |
| AP242-002 Verified | Exact fixture journey and negative boundary | AP242-002 verification record |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-05 | Owns | Explicit AP203/AP242 coexistence and one-runtime dependency graph |
| AC-06 | Owns | Offline repeatability, normalized equivalence and version classification |
| AC-07 | Owns | Trimmed Native AOT publish/run of the complete fixture journey |

<!-- work-item: delivery-constraints -->
## Constraints

- Verification may use only checked-in schema/fixture, candidate local packages and explicitly copied cached compiler
  packages; no network, installed OCCT, hidden schema cache or source-tree project reference may satisfy the proof.
- Close the parent internal redistribution gate with exact STEPcode/OCCT license and provenance hashes.
- Stable `1.0.0` is allowed only after all parent ACs pass on one candidate.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-05 purpose=acceptance shape=end-to-end -->
<!-- primary-proof: AC-06 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-07 purpose=acceptance shape=end-to-end -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-05 | Primary | Isolated two-package consumer binds both fixtures correctly with one runtime and no schema-package edge | Restore/build/run from the local feed and inspect assets/deps/assembly references |
| AC-06 | Primary | Two clean offline runs produce equivalent normalized package/API and identical pinned hashes | Run the bounded AP242 package verifier twice and compare outputs |
| AC-07 | Primary | Release trimmed Native AOT publishes without attributable warnings and runs the exact AP242 journey | Run the AP242 mode of the Native AOT verifier |
| Integrated change | Conditional | Union of item gates and proportional Release regressions remains green | Run final solution/package integration verification |

<!-- work-item: definition-of-done -->
## Done

- AC-05, AC-06 and AC-07 pass on the integrated candidate; redistribution/version/documentation gates are closed.
- Final implementation review finds no unapproved runtime discovery, AP-specific shared-runtime branch or cross-package coupling.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record exact candidate, artifacts, two normalized digests, dependency graph, AOT RID/compiler version/warnings/output,
license/provenance audit, commands/counts, environment prerequisites, limitations and final review disposition.

## Completion evidence

- Stable package-only integration passed 1/1 and repeated packing produced identical normalized entries. Its isolated
  AP203/AP242 consumer selected both descriptors and fixtures correctly, resolved one shared runtime, and contained no
  sibling schema-package dependency.
- `verify-schema-package-reproducibility.ps1 -Schema Ap242` completed two clean, non-incremental, no-restore,
  single-node builds with 0 warnings and 0 errors. Normalized package SHA-256 was
  `81B8B858B86E81416BF43C514CBFA7C85C35B9708EEAA613779372A58F493907`; fixed-input SHA-256 was
  `35833AEDFAFB765094E48B0F76F69357C1B2D347949EAD46A6BAAD3457ABDAF7`; rounds took 4:38 and 4:21.
- `verify-native-aot.ps1 -Ap242` passed the complete fixture and negative journey for `win-x64` with no attributable
  AOT/trimming warning. Compiler package was `10.0.11`; native executable size was 138,115,072 bytes.
- Final review found no runtime discovery, reflection fallback, AP-specific shared-runtime branch, removed SELECT
  alternative, or cross-package coupling. Schema-level shared SELECT payload and `TYPEOF` dispatch reduced generated
  code/AOT cost while retaining the approved public API hash and the 131/131 reachable-rule regression result.
- Redistribution evidence is closed by the checked-in STEPcode and OCCT provenance/license hashes. This release does
  not publish to NuGet.org and does not claim AP242 capabilities beyond the documented fixed manifold B-rep journey.
