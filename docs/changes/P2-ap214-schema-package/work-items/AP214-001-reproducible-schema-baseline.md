# AP214-001: 固定可重现的 AP214 schema baseline

<!-- work-item-format: 2 -->
<!-- work-item-id: AP214-001 -->

<!-- approval-source: user-explicit-approval-2026-08-27 -->

## Outcome

仓库从已批准的固定 STEPcode source 离线生成一份忠实的 `AUTOMOTIVE_DESIGN` public surface，
并以可审计的 provenance、许可、descriptor identity 与完整 API snapshot 固定该 baseline，供后续
AP214 包装配和重复构建直接使用。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: AP214 EXPRESS source、生成代码、唯一 descriptor、
  public API snapshot，以及 source/license provenance。
- In scope:
  - 检入 STEPcode commit `9baa5dadaa1dcfcdc623220d865d36d61ea351e9` 的
    `data/ap214e3/AP214E3_2010.exp`，保持 parent change 固定的 upstream-byte 与 canonical-LF SHA-256。
  - 保留 STEPcode `COPYING`、`AUTHORS`、`INTENT.md` 及逐文件来源记录，并核对 parent change 中的
    固定 hash、edition、日期、nominal name 与 registration identity。
  - 让完整 AP214 source 通过现有 EXPRESS compiler/generator，交付唯一显式 descriptor 和完整
    generated public API snapshot；补齐仅由大型 schema 暴露、且可追溯到 EXPRESS 语义的
    schema-neutral compiler/generator 根因缺口。
  - 记录规范化输入和生成 surface，使后续离线重复构建能够判断等价性。
- Non-goals:
  - 不创建 NuGet package shell、消费者模板、OCCT fixture 或 AP214 专属 runtime/parser。
  - 不维护多个 AP214 edition，不引入 OID registry、runtime discovery 或跨 schema 包依赖。
  - 不以跳过声明、手改生成代码或弱化语义验证绕过 compiler/generator 缺口。
- Likely touchpoints (non-binding): `schemas/`、source/provenance 资产、generator/compiler、
  `src/TedToolkit.Step21.Ap214/` 的 schema 生成输入、public API approval 与 focused contract tests。

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| Parent PRE-01 | 仓库已有只使用检入 schema/fixture 的无网络可重现构建模式 | `../P2-ap203-schema-package/change.md`, AC-12 已完成 |
| Fixed source and redistribution evidence | parent change 中指定的 STEPcode revision、source 路径、hash 与 BSD-3-Clause 证据保持可核验 | parent change 的 Fixed AP214 baseline 与 STEPcode redistribution evidence |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-02 | Owns | 证明 descriptor、代表性继承/可空性/聚合/成员顺序与完整 public API snapshot 忠实于固定 source |
| AC-06 | Supports | 提供固定输入、provenance、规范化规则和可比较的生成 surface |

<!-- work-item: delivery-constraints -->
## Constraints

- Public, persisted, compatibility, security, migration, governing, or preserved behavior:
  - nominal schema 必须保持 `AUTOMOTIVE_DESIGN`；OID 只作为 source/header 数据保留，不作为 package identity。
  - fixed source、edition、hash、许可适用性或生成 public surface 若需改变，必须回到 parent change 重新批准。
  - 只允许 schema-neutral 且能由 EXPRESS 语义解释的 compiler/generator 修复；不得添加 AP214 特判。
  - 完整生成 surface 是后续 SemVer 判断输入，不能只用少量代表性类型替代 snapshot。
- Private choices deliberately left to the implementer:
  - snapshot 的稳定排序、辅助检查器组织和内部生成阶段修复方式，只要不改变已批准的 public/runtime 边界。

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-02 purpose=acceptance shape=contract -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-02 | Primary | 固定 AP214 source 的 hash、许可、schema identity、descriptor 与完整 generated public API snapshot 一致，代表性继承、可空性、聚合和成员顺序忠实 | 运行 focused AP214 provenance、descriptor-fidelity 与完整 public-API contract checks |
| Parent regression gate | Conditional | schema-neutral compiler/generator 修复未破坏现有 schema 与 runtime 合同 | `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release`；若改动共享生成路径，再执行 solution Release build |

<!-- work-item: definition-of-done -->
## Done

- AC-02 的 primary proof 通过，固定 source、许可、provenance、descriptor 与完整 API snapshot 已检入。
- 所有为 AP214 暴露的 compiler/generator 缺口均以 schema-neutral 根因修复并通过相关回归。
- 已向 AP214-002 提供可直接装配的已验证 generated surface，并向 AP214-004 提供规范化重现输入。

<!-- work-item: completion-evidence -->
## Verification result requirements

实现交接必须记录 candidate revision、实际变更资产、AC-02、acceptance purpose、contract shape、
执行的命令或步骤、hash/snapshot/测试结果、资源前提、许可与 provenance 状态，以及提供给
AP214-002/AP214-004 的具体输出。可变状态和 candidate 结果只记录在 coordinator-owned orchestration state。

## Risks and implementation notes

- AP214 规模可能暴露命名冲突、生成时间、程序集大小或尚未覆盖的 EXPRESS 语义；必须修根因并保留
  bounded evidence，不能用 schema-specific suppression 缩小输入。
- 若 STEPcode 授权适用性无法由已固定证据闭合，本工作项停止并升级 parent change，而不是换源或豁免。
