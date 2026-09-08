# ISO21-006: 交付 Annex F ECMAScript binding

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-006 -->

<!-- approval-source: user-explicit-approve-and-continue-2026-09-06 -->

## Outcome

以可选、单向依赖核心模型的语言绑定交付 Annex F 定义的 `P21` object、anchor value mappings
与 model/population methods，而不让核心 runtime 嵌入或执行任意 ECMAScript。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: optional Annex-F package/artifact、caller-supplied ECMAScript-host bridge、`P21` value/object surface、model/population operations、package dependency and version baseline.
- In scope: F.2 required properties、F.3 integer/real/string/enumeration/binary/EID/VID/CIN/CVN/null/list/URI mappings and `valueOf`/`toString`/`toP21String`、F.4 uri/name/schema-population get/set/verification methods.
- Non-goals: core dependency on a JS engine、arbitrary script evaluation API、browser/Node product support promise、AP-specific object projection or a second Part 21 parser.
- Likely touchpoints (non-binding): optional adapter project/package、embedded/generated ECMAScript module、host-neutral bridge、actual-engine integration harness and dependency audit.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| ISO21-001 Verified | Stable anchor/value/occurrence model and standard textual mapping | ISO21-001 completion evidence |
| ISO21-004 Verified | Stable schema-population objects and verification state | ISO21-004 completion evidence |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-08 | Owns | Every Annex-F property/value/method mapping and core dependency isolation |
| AC-02, AC-09 | Supports | Supplies class-3 ECMAScript behavior and no-script-engine security boundary |

<!-- work-item: delivery-constraints -->
## Constraints

- The adapter may require a caller-selected standards-conforming ECMAScript host, but the core runtime/package graph must not include that host or dynamic-code dependency.
- One shared Part 21 model remains authoritative; the adapter must not reparse, fork identity or invent domain objects.
- Package/public surface, generated/embedded module bytes and observable ECMAScript results are deterministic and versioned.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-08 purpose=boundary shape=integration -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-08 | Primary | An actual standards-conforming ECMAScript engine observes every F.2-F.4 mapping and mutation result while core read/write remains engine-free | Run the focused `AnnexFEcmaScriptBindingTests` Release integration suite and package dependency audit |
| AOT/core isolation | Conditional | Core package Native AOT publish/run succeeds without the optional adapter or any ECMAScript engine asset | Run `pwsh -File build/verify-native-aot.ps1` and inspect packed core assets |

<!-- work-item: definition-of-done -->
## Done

- AC-08 passes against an actual engine with all Annex-F rows represented in a machine-readable manifest.
- Core package dependency/AOT proofs remain green; the verified isolation result is supplied to ISO21-007/008.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate revision, package/module/API identity, engine/version prerequisite, Annex-F row and assertion counts,
commands, dependency graph, AOT result and supplied isolation contract.

## Completion evidence

- Candidate: `2543facef1878c6df760c3af9273164687201403`, reviewed against baseline
  `080e6f77d42d4c160e68ba7fb6aec8f85c330154`; independent implementation review concluded `Ready to merge`
  with no blocking or important findings.
- Identity: package `TedToolkit.Step21.AnnexF` version `1.0.0`, bridge format `1`, embedded/packed
  `AnnexF.js` SHA-256 `132759A4A580B6DB36203729A75AF47D20E944E964E1534C082E51CC25E47017`, public API snapshot
  SHA-256 `80DE549D91FC895091A83533D11EB969C9030191F5390AF71FEA83F8FB97AF15`.
- Engine proof: Node.js `v24.14.1`; focused Release suite passed 4/4 with 89 actual-engine assertions and exact
  22/22 manifest-ID coverage. It covers every F.2-F.4 mapping/method, all wrapper method results, URI null,
  transactional rejection, canonical write-back, name collisions, population verification lifecycle, package contents,
  dependency isolation and API/source identity.
- Compatibility proof: solution Release build passed with 0 warnings and 0 errors; anchor 15/15,
  schema-population 28/28 and core public validation-contract 16/16 suites passed. Full integration passed 19/20 with
  0 failures and one explicitly environment-gated external-network corpus test skipped.
- Dependency graph: `TedToolkit.Step21.AnnexF -> TedToolkit.Step21`; the core project/package has no reverse adapter or
  ECMAScript-engine dependency. The adapter exposes no general script evaluator and targets ECMAScript 5.1 or later.
- Native AOT: `build/verify-native-aot.ps1` passed with `NATIVE_AOT_PACKAGE_PROOF_OK`; the real `win-x64` core package
  consumer executable was 6,742,016 bytes using compiler package `10.0.11`.
- Supplied contract: ISO21-007/008 may rely on a caller-owned, explicit transactional host boundary, deterministic
  LF-pinned module bytes, no implicit I/O or dynamic-code dependency in core, and full Annex-F row traceability.
- Authoritative integration: fast-forwarded to
  `0de98230a6fcaa49bd81fd9542c045ef7fb079f0`; integrated Annex F 4/4, schema-population 28/28 and core public
  validation-contract 16/16 suites passed before the work item advanced to `Verified`.

## Risks and implementation notes

The test engine is boundary evidence, not permission to make it a transitive runtime dependency or to expose general script execution.
