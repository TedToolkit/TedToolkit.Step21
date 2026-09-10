# ISO21-008: 关闭 PICS、conformance classes 与 AOT 证据

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-008 -->

<!-- approval-source: user-approved-recommended-iso21-005-split-and-continue-2026-09-09 -->
<!-- scope-revision: user-approved-source-exclusion-rule-2026-09-10 -->

## Outcome

交付可机械验证的 Annex D PICS/逐条 normative traceability 与真实 processor 旅程，证明 classes
`4;1`、`4;2`、来源已描述的 `4;3` facilities 及允许的 `2;1`/`3;1` 兼容在同一 candidate 上读写；
授权 ISO 11 文件未描述的完整 constant/evaluated-set 语义明确 source-excluded；schema-supplied short
names 保持正 PICS 且不推断缩写；同时保持 class-1 性能、旧 API/AP packages 和 Native AOT。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: machine-readable clause/PICS manifest、processor conformance report、implementation limits、declared-class end-to-end fixtures、consumer docs/API/package/AOT/performance evidence.
- In scope: clauses 4-14 and applicable normative Annexes A-G、read/write PICS、entity/value/constant and short-name encodings、all string encodings、reference/archive/signature/population/Annex-F rows、justified processor not-applicable rows，以及缺失 Part 11 输入的明确 source-excluded disposition。
- Non-goals: physical tape/diskette/multi-volume media operations、informative annex implementation、AP/B-rep/PMI semantics、other STEP parts、remote package publication or a broader platform promise than tested.
- Likely touchpoints (non-binding): conformance manifest/verifier、public report/limits、contract/integration fixtures、docs/conformance、packed consumer、allocation harness and AOT scripts.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| ISO21-001 through ISO21-004, ISO21-006 and ISO21-007 Verified | Every independently deliverable capability, binding and security boundary has exact candidate-bound proof and supplied manifests | Work-item completion evidence on the authoritative integration revision |
| ISO21-005 and ISO21-009 Verified before final closure | Schema-supplied physical mappings and the independently reviewed Part 11 clause/semantic/test profile classify every former `blocked-source` row as implemented or source-excluded without inference before ISO21-008 may become Verified | ISO21-005 and ISO21-009 completion evidence on the authoritative integration revision |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-01 | Owns | Zero source-described applicable unsupported/unmapped/unproven PICS or normative row; every missing-input row is explicit source-excluded |
| AC-02 | Owns | End-to-end declared classes 1/2, source-described class-3 facilities and edition compatibility read/write behavior |

<!-- work-item: delivery-constraints -->
## Constraints

- Every manifest row names normative source, applicability, implementation location and exactly one executable primary proof, justified not-applicable rationale, or missing-input source exclusion.
- A green AP corpus or syntax parser is never sufficient for a normative row; a full conformance claim remains absent while any applicable row is source-excluded.
- Measure the approved fixed class-1 scenario against `63b2757` on the same Release runtime: median 20-iteration allocation regression is at most `max(5%, 16 KiB)` and the ordinary overload call graph constructs no unused feature services. No artificial runtime factory or counter is added solely for the test.
- Preserve cumulative runtime/generated API snapshots, diagnostics, deterministic outputs, AP203/AP214/AP242 package proofs and core Native AOT execution.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-02 purpose=acceptance shape=contract -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Conformance verifier reports zero source-described applicable syntax-only, unsupported, unmapped or unproven clause/PICS row and enumerates every source exclusion | Run the repository ISO conformance verifier and manifest contract tests in Release |
| AC-02 | Primary | Positive and neighboring-invalid matrices for every declared supported implementation level/facility read, write and reread with exact semantics | Run the conformance-class processor contract suite in Release |
| Full compatibility/AOT | Conditional | Full solution, public/generated baselines, AP package/fixture/reproducibility proofs and core packed Native AOT journey pass | Run the Release test executables through `dotnet run`, the full Release solution build, package scripts and `pwsh -File build/verify-native-aot.ps1` |
| Class-1 feature isolation | Conditional | Fixed scenario stays within the approved allocation bound and ordinary overloads create zero unused feature-service objects | Run `build/verify-class1-allocation.ps1`; inspect the default read/write call boundary and Annex F dependency direction |

<!-- work-item: definition-of-done -->
## Done

- AC-01 and AC-02 pass on the same candidate as every prerequisite proof; no source-described applicable row remains unsupported or unproven, and source-excluded rows remain negative PICS answers rather than conformance claims.
- Durable conformance/architecture/consumer docs replace stale capability claims; API/SemVer, full regression, package, performance and Native AOT evidence pass.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record authoritative candidate revision; clause/PICS total, implemented and N/A counts; class/compatibility fixture counts;
all commands/results; API/SemVer/docs state; baseline/candidate allocation values; package graphs; AP regressions; and Native AOT publish/run identity.

## In-progress evidence — 2026-09-09

- Candidate is still an uncommitted working tree based on `6b811ca`; this is progress evidence, not an authoritative
  completion revision.
- The machine-readable trace contains 36 rows: 30 implemented, 2 processor-not-applicable and 4 explicitly
  `blocked-source`. The short-name/mapping input is assigned to draft ISO21-005; class-3 constant evaluation and
  validation-reachable Part 11 semantics are assigned to draft ISO21-009.
- Clause 5.6 and clause 13 were tightened after direct normative review: ignored control octets work inside every
  string directive and comment delimiter; print controls do not enter effective STRING values and are rejected
  throughout ANCHOR/REFERENCE; multi-file ZIP class selection applies to the root without contaminating subsidiary
  exchange-structure classification.
- Latest Release unit run: 706 total, 704 passed, 2 failed only at the recorded unresolved Part 11 `QUERY`
  aggregate-source semantics. Latest integration run: 36 total, 35 passed, 1 explicitly skipped external-download
  corpus, 0 failed; AP203/AP214/AP242 package consumers all passed.
- Latest full Release no-incremental solution build completed in 5:02.74 with 0 warnings and 0 errors. Latest packed
  Native AOT proof ended `PACKED_AOT_OK` and `NATIVE_AOT_PACKAGE_PROOF_OK` for win-x64 with a 6,863,872-byte
  executable and compiler package 10.0.11.
- ISO21-005 and ISO21-009 are now Verified. Final verification remains gated by the separately approved class-1
  allocation benchmark, durable README/manifest synchronization, final candidate-bound review, and a commit revision.

## Risks and implementation notes

Normative completeness is a positive proof obligation. Search-based absence of `unsupported` or a passing aggregate test command cannot replace row-by-row manifest coverage.

## Source-bounded closure evidence — 2026-09-10

- The Part 21 manifest contains 46 normative rows: 39 implemented, 2 processor-not-applicable, and 5
  source-excluded. Clause 12.2 is split into eleven independently evidenced rows. The exclusions are the complete
  class-3 claim, typed and constant-backed EXPRESS DATA hydration, complete Annex-B evaluated-set semantics, and
  missing Part 11 constant/validation semantics. Schema-supplied entity, SELECT, and enumeration short-name PICS
  answers are positive; no abbreviation is inferred. Source exclusions do not enter the conformance claim.
- The manifest contract passes 2/2 tests. The implementation-level suite passes 44/44 and covers `4;1`, `4;2`,
  source-described `4;3` facilities, and `2;1`/`3;1` compatibility partitions.
- Candidate `90d6458` adds the reproducible allocation gate. Against `63b2757`, the identical 20-iteration Release
  probe reports baseline 105,728 bytes, candidate 114,992 bytes, and an approved maximum of 122,112 bytes.
  The ordinary read/write overloads pass no resource or signing context; the optional Annex F assembly retains its
  one-way dependency on the core runtime.
- Product/test candidate `5daafb81d38566714512a95538451ec7e494e6da` passes the no-incremental Release solution
  build with zero warnings and errors. The resource-bounded full unit run passes 727/727 with zero skips; its public
  runtime/generated snapshots and AP203/AP214/AP242 surfaces are unchanged. The full integration run executes 36
  tests: 35 pass, zero fail, and only the explicit opt-in external-download corpus is skipped; all three AP package
  consumers pass. `build/verify-native-aot.ps1` reports `PACKED_AOT_OK` and
  `NATIVE_AOT_PACKAGE_PROOF_OK win-x64 executable-bytes=6863872 compiler-package=10.0.12`.
