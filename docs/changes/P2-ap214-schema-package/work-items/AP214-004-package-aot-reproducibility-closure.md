# AP214-004: 包共存、AOT 与可重现交付闭环

<!-- work-item-format: 2 -->
<!-- work-item-id: AP214-004 -->

<!-- approval-source: user-explicit-approval-2026-08-27 -->

## Outcome

同一 integrated candidate 证明 AP203/AP214 两包隔离共存、AP214 包及生成表面可离线重复构建并按
ADR-0006 正确分类、trimmed Native AOT 可执行完整 fixture 旅程，同时关闭内部重分发与最终集成证据。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: two-package consumer、offline reproducibility harness、
  version-classification manifest、Native AOT packed consumer、内部重分发 gate 与最终 integration evidence。
- In scope:
  - 用隔离消费者同时引用 AP203 与 AP214，显式选择 descriptor 并运行各自 fixture；检查只有一份
    compatible runtime，schema 包之间无依赖、扫描或第二套 reader/writer。
  - 在无网络、非增量条件下对固定输入执行两次构建，比较规范化 package/API/provenance 输出。
  - 以批准的首版 `1.0.0` 输入 manifest 和最新 stable baseline（存在时）对 source、edition、descriptor、
    closed set、runtime range、public surface 与语义差异作 ADR-0006 版本分类；只在全部 completion criteria
    满足后形成 stable `1.0.0` 证据。
  - Release trimmed Native AOT publish AP214 package-only consumer，并执行 AP214-003 的完整 fixture journey，
    确认无候选包/生成路径导致的 trim 或 AOT warning。
  - 核验 STEPcode 与 OCCT license/exception、逐文件 provenance、固定 hashes、package/fixture 文档，关闭
    repository maintainer 拥有的 internal redistribution gate，并在同一 candidate 上运行最终回归/审查输入。
- Non-goals:
  - 不发布到 NuGet.org，不申请外部法务审批，不改变 AP203 public contract。
  - 不放宽 warning、忽略不可重现差异、重建同一 stable version 或新增动态加载/跨包耦合。
- Likely touchpoints (non-binding): packed-consumer/integration tests、local package source、AOT publish harness、
  reproducibility/version manifests、package/fixture documentation 与 solution-level verification scripts。

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| AP214-002 Verified | 候选 AP214 package、package-only consumer、metadata 和无 schema-package dependency guarantee | AP214-002 verification record and supplied output |
| AP214-003 Verified | 固定 fixture、完整 semantic journey、negative/atomicity proof 与 bounded capability statement | AP214-003 verification record and supplied output |
| AP203 maintained package | 可与候选 AP214 一同恢复和运行的已完成 AP203 package baseline | parent PRE-01 and `../P2-ap203-schema-package/change.md` completion evidence |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-05 | Owns | 证明 AP203/AP214 显式选择、单 runtime、无 schema-package dependency 或扫描冲突 |
| AC-06 | Owns | 证明两次 offline build 规范化等价、provenance 一致且版本分类符合 ADR-0006 |
| AC-07 | Owns | 证明 trimmed Native AOT package-only consumer 完成固定 AP214 fixture 旅程且无 attributable warning |

<!-- work-item: delivery-constraints -->
## Constraints

- Public, persisted, compatibility, security, migration, governing, or preserved behavior:
  - 所有 primary evidence 必须绑定同一 integrated candidate 和实际 `.nupkg`；project reference 不能替代 package 证据。
  - 两个 schema 包只共享 compatible `TedToolkit.Step21` runtime，互不依赖且始终由消费者显式选择 descriptor。
  - offline proof 只使用已检入 schema/fixture；任何网络取得或未固定输入均使 AC-06 失败。
  - stable `1.0.0` 只能在 AC-01 至 AC-08、内部重分发 gate 和最终 implementation review 全部完成后成立。
  - baseline、descriptor identity、generated public surface、schema semantics 或 runtime range 的后续变化按 ADR-0006 分类，不能同版本重建。
- Private choices deliberately left to the implementer:
  - 双包 consumer 与 reproducibility harness 的内部组织、产物 normalization 实现和 CI 呈现，只要证据可重复且边界清楚。

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-05 purpose=acceptance shape=end-to-end -->
<!-- primary-proof: AC-06 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-07 purpose=acceptance shape=end-to-end -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-05 | Primary | 隔离 two-package consumer 对 AP203/AP214 显式绑定正确，各自 fixture 成功，dependency graph 只有一份 runtime 且两 schema 包无相互依赖或扫描 | 从 isolated local source 恢复/build/run two-package consumer，并检查 `.deps.json`、NuGet graph 与 assembly references |
| AC-06 | Primary | 两次无网络非增量构建的规范化 package/API/provenance 等价，固定 manifest 与 stable baseline 的差异得到符合 ADR-0006 的版本分类 | 清理各自隔离输出后执行两次 offline build/pack，比较 normalized artifacts/hashes，并运行 version-classification contract checks |
| AC-07 | Primary | Release trimmed Native AOT package-only consumer publish 无 attributable warning，执行 AP214-003 完整 fixture journey 成功 | 从实际候选 package 离线 restore，执行 Release Native AOT publish 并运行产物的 AP214 fixture mode |
| Internal redistribution gate | Conditional | STEPcode/OCCT 许可、exception、逐文件 provenance、固定 hashes 和 package/fixture 文档完整且适用性结论明确 | repository maintainer 按 parent change 固定证据执行 bounded file/hash/package audit |
| Integrated regression/review gate | Conditional | AC-01 至 AC-08 均绑定同一 candidate，solution Release build 与两个 TUnit suites 通过且无未批准耦合 | 执行 Release solution build、`dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release`、`dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release`，再提交 final implementation review |

<!-- work-item: definition-of-done -->
## Done

- AC-05、AC-06、AC-07 的 primary proof 在同一 integrated candidate 上通过。
- internal redistribution gate、solution Release build、完整 unit/integration regression 与最终 review 输入完成。
- parent AC-01 至 AC-08 的证据可追溯到同一 candidate；没有外部 operational handoff 或 NuGet.org 发布动作。

<!-- work-item: completion-evidence -->
## Verification result requirements

实现交接必须记录 integrated candidate revision、实际 package/consumer/harness/manifest/documentation 资产、
AC-05/AC-06/AC-07、acceptance purpose、execution shape、全部命令、dependency graph、artifact hashes、
版本分类、AOT warning/旅程与测试结果/计数、离线和 AOT 资源前提、内部重分发 gate 状态，以及 parent
closure 所需证据。可变状态和 candidate 结果只记录在 coordinator-owned orchestration state。

## Risks and implementation notes

- Native AOT 或大型 generated assembly 可能暴露体积/时长问题；只有影响已批准可用性或 warning-free
  contract 的根因修复属于本 change，新的 runtime concept 或分发策略必须升级设计。
- reproducibility 比较必须规范化已批准的非语义差异，但不得隐藏 source、API、metadata、provenance 或二进制变化。
