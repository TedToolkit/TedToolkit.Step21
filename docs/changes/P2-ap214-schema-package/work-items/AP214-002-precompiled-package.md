# AP214-002: 交付独立的 AP214 预编译包

<!-- work-item-format: 2 -->
<!-- work-item-id: AP214-002 -->

<!-- approval-source: user-explicit-approval-2026-08-27 -->

## Outcome

一个干净的 .NET 10 消费者只引用候选 `TedToolkit.Step21.Ap214` 包即可显式取得 AP214 descriptor
与生成类型；包文档和消费者模板同时保留 distinct custom schema Analyzer 路径，并明确排除重复
`AUTOMOTIVE_DESIGN` `.exp`/`AdditionalFiles` 输入。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: `TedToolkit.Step21.Ap214` assembly/NuGet package、
  package metadata、package-only consumer、README 与消费者模板。
- In scope:
  - 创建独立 `net10.0` schema project/package，装配 AP214-001 交付的固定 generated surface 与唯一 descriptor。
  - 固定候选 `1.0.0-*` metadata、runtime dependency `[1.0.0,2.0.0)`、source/edition/compatibility 信息，
    确保运行时资产不包含 Analyzer-only 依赖。
  - 增加隔离的 local-pack package-only consumer，证明无需 `.exp` 或 `AdditionalFiles` 即可编译、运行并访问代表性类型。
  - 编写 package README 和模板，使预编译 AP214 与相同 schema 的 Analyzer 输入明确互斥；证明 distinct custom schema consumer 仍可用。
- Non-goals:
  - 不实现 fixture 语义旅程、Native AOT closure、NuGet.org 发布或 stable release。
  - 不让 AP214 依赖 AP203，不增加联合包、runtime registry、程序集扫描、动态加载或 package-aware Analyzer 诊断。
  - 不以 first-wins、静默去重或生成顺序将重复 AP214 输入声明为受支持用法。
- Likely touchpoints (non-binding): `src/TedToolkit.Step21.Ap214/`、solution/project metadata、
  `tests/TedToolkit.Step21.PackedConsumer/`、`tests/TedToolkit.Step21.IntegrationTests/PackedConsumerTests/`、package README/template。

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| AP214-001 Verified | 已检入的固定 source/provenance、唯一 descriptor 与验证通过的完整 generated public surface | AP214-001 verification record and supplied output |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-01 | Owns | 证明 package-only consumer 无需 schema source/Analyzer 输入即可编译和运行 |
| AC-08 | Owns | 证明 distinct custom schema 路径保留，README/template 明确预防重复 AP214 surface |
| AC-05 | Supports | 提供无 AP203 依赖的独立 AP214 package 与显式 descriptor |
| AC-07 | Supports | 提供可供 Native AOT consumer 使用的 package-only 程序入口和资产边界 |

<!-- work-item: delivery-constraints -->
## Constraints

- Public, persisted, compatibility, security, migration, governing, or preserved behavior:
  - 核心 runtime 保持 schema-neutral，调用者继续显式传入 `SchemaDescriptor`；AP214 包不复制 reader/writer/model/validator。
  - 包只维护一份已批准 baseline，且不得依赖任何其它 schema package。
  - 首版 TFM 为 `net10.0`，候选使用 `1.0.0-*`，runtime range 固定为 `[1.0.0,2.0.0)`。
  - README/template 的互斥规则是本 change 已批准的预防边界；不得擅自扩大为新的 Analyzer diagnostic 或 build-failure contract。
- Private choices deliberately left to the implementer:
  - package project 的内部组织、consumer mode 的命名和文档排版，只要可独立验证且不改变 package/public 边界。

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-08 purpose=acceptance shape=contract -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | 从隔离 local source 恢复的 package-only consumer 可编译、运行并访问 descriptor/代表性类型，且没有 `.exp`、`AdditionalFiles` 或 Analyzer runtime asset | pack 候选包至隔离 local source，离线 restore/build/run AP214 packed-consumer mode，并检查资产图 |
| AC-08 | Primary | distinct custom EXPRESS consumer 继续成功；AP214 README/template 明确禁止且不包含重复 `AUTOMOTIVE_DESIGN` `.exp`/`AdditionalFiles` | 运行 isolated custom consumer regression，并执行 focused README/template contract checks |
| Package structural gate | Conditional | package metadata、TFM、runtime range、唯一 descriptor 与无 schema-package dependency 符合 parent contract | 检查 `.nupkg`、NuGet metadata、assembly/project references 与 package asset manifest |

<!-- work-item: definition-of-done -->
## Done

- AC-01 与 AC-08 的 primary proof 通过，候选包、隔离消费者、README 和模板均已交付。
- package manifest 不包含 Analyzer-only runtime asset，也不包含 AP203 或其它 schema-package 依赖。
- 已向 AP214-003 提供已验证的 package assembly/consumer 边界，并向 AP214-004 提供 package-only 与共存/AOT 输入。

<!-- work-item: completion-evidence -->
## Verification result requirements

实现交接必须记录 candidate revision、实际变更资产、AC-01/AC-08、acceptance purpose、contract shape、
local-pack/restore/build/run 与文档检查步骤、结果和资产计数、资源前提、package metadata、文档状态，
以及提供给 AP214-003/AP214-004 的具体 package 和 consumer 输入。可变状态和 candidate 结果只记录在
coordinator-owned orchestration state。

## Risks and implementation notes

- package consumer 验证必须使用实际打包产物和隔离 source，不能由 project reference 冒充 package-only 证据。
- README/template 只预防重复输入；若实现过程中发现必须自动拒绝该组合，应停止并另行设计 schema-neutral 诊断合同。
