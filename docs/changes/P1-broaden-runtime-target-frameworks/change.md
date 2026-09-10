# 扩大 Step21 运行时包的目标框架范围

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: migration -->
<!-- change-status: in-progress -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: user-explicit-approve-and-implement-2026-09-10 -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

让仍受维护的 Step21 公开运行时包同时提供 `netstandard2.0` 与 `net8.0` 资产，使 .NET
Framework 4.7.2+ 与现代 .NET 消费者能使用同一包身份，并让兼容的 Unity/Mono 环境可获得 portable
asset，而不再把 .NET 10 作为不必要的最低运行时要求。当前核心与 AP203/AP214/AP242 包只发布
`net10.0`，其兼容范围明显窄于实际 ISO/runtime 合同所需。

<!-- section: scope -->
## Scope and non-goals

- In scope:
  - `TedToolkit.Step21`、`TedToolkit.Step21.Ap203`、`TedToolkit.Step21.Ap214` 与
    `TedToolkit.Step21.Ap242` 同时构建并打包 `netstandard2.0` 和 `net8.0`。
  - 为 .NET Standard 2.0 提供不泄漏到公开契约的兼容实现，并让生成 schema 代码在两个目标上
    保持相同的 public/protected API 与语义。
  - 更新包资产、baseline、消费者指南、产品当前事实和自动化验证，使 NuGet 选择及兼容承诺可执行。
  - 在真实 .NET Framework 4.7.2-or-later 消费者、.NET 8 消费者和 .NET 10 消费者上验证资产选择；
    现代消费者保留 trimming/Native AOT 证明。
- Non-goals:
  - 不发布没有目标专属实现或已证明收益的 `net10.0` library asset。
  - 不恢复当前工作区正在移除的 Annex F package/binding。
  - 不承诺 .NET Framework 4.6.1、已停止支持的 SDK/编译器、Unity/Mono runtime 行为、所有历史
    Unity 版本或超出实际验证环境的平台行为；Unity/Mono 仅声明兼容 asset 可供 NuGet 选择。
  - 不改变 ISO 10303-21/EXPRESS 语义、公开 API 含义、包身份、schema baseline、版本范围、parser、
    model、validator、writer、diagnostic 或安全策略。
- Compatibility or deliberately preserved behavior:
  - 两个 TFM 的 public/protected API、nullability、descriptor identity、generated schema surface、
    read/validate/edit/write 行为、诊断代码和失败原子性一致。
  - Analyzer 继续只面向 `netstandard2.0`；构建程序、测试与探针可以继续使用仓库 SDK，不作为消费包
    的最低 TFM。
  - AP 包继续只依赖一个 schema-neutral `TedToolkit.Step21` runtime，且不把 Analyzer-only 资产带入
    runtime graph。

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | NuGet runtime asset selection | 四个公开包只含 `lib/net10.0` | 四个公开包各含 `lib/netstandard2.0` 与 `lib/net8.0`，且不含冗余 `lib/net10.0`；兼容消费者自动选择可用的最佳资产 | 包 ID、版本语义、schema baseline 与依赖方向 |
| OB-02 | 跨 TFM runtime 行为 | 只有 .NET 10 行为获得验证 | portable 与 modern 资产对代表性 read/validate/edit/write、诊断、安全和原子失败场景给出等价结果 | ISO/EXPRESS 语义和既有成功/失败合同 |
| OB-03 | 公开表面、生成 schema 与部署边界 | 核心与 AP 包公开表面及 AOT 仅在 `net10.0` 包资产上证明 | 核心及 AP203/AP214/AP242 在两目标中保持相同 public/protected API 与 nullability，AP descriptor/generated surface 不变；.NET 8/.NET 10 package consumers 保留静态可达及 Native AOT 行为 | ADR-0003 与 ADR-0006 的 Analyzer 隔离、显式 descriptor 和有界 runtime graph |

<!-- acceptance-case: AC-01 -->
### AC-01 — 四个公开包提供正确的双目标资产

```gherkin
Scenario: 检查候选 NuGet 包
  Given 已打包的核心、AP203、AP214 与 AP242 候选
  When 检查 lib 目录、依赖组和包元数据
  Then 每个包只声明 netstandard2.0 与 net8.0 runtime assets，依赖组完整且没有 net10.0 library asset
```

<!-- acceptance-case: AC-02 -->
### AC-02 — portable 与 modern 消费者获得等价行为

```gherkin
Scenario: 在声明的消费者边界运行代表性 runtime 旅程
  Given .NET Framework 4.7.2-or-later、.NET 8 与 .NET 10 package-only consumers
  When 它们执行相同的代表性读取、验证、编辑、写出、诊断、有效 detached CMS 签名验证以及 malformed/invalid CMS 原子拒绝场景
  Then 每个消费者成功使用 NuGet 选择的资产并观察到等价公开结果和失败原子性
```

<!-- acceptance-case: AC-03 -->
### AC-03 — 四个包的公开表面与 Native AOT 边界保持稳定

```gherkin
Scenario: 比较并部署双目标候选包
  Given 核心、AP203、AP214 与 AP242 的双目标候选及各自既有批准 public API baselines
  When 把四个包每个 TFM 的 public/protected API 与 nullability 对照批准 baseline，并分别发布运行 net8.0 与 net10.0 package-only Native AOT consumers
  Then 四个包的公开表面、AP descriptor identity 与 runtime graph 符合 baseline，两个 AOT consumers 均无 Analyzer-only runtime 依赖或 AOT warning
```

## Constraints and risks

- Governing decision: [`ADR-0010`](../../adr/ADR-0010-netstandard2-and-net8-runtime-packages.md)
  要求双目标资产、跨目标公开语义一致、.NET Framework 4.7.2+ 实际运行证明及不发布冗余
  `net10.0` asset。
- `ADR-0003` 的 Native AOT 静态可达边界和 `ADR-0006` 的 schema package/public surface/runtime
  dependency 合同保持有效。
- 主要风险是 framework type polyfill 泄漏、跨 TFM API 漂移、较旧 BCL 的字符串/格式化行为差异、
  CMS/PKCS 平台差异、生成 record 编译支持以及 package asset 选择误判。
- 首次发布前可通过移除未通过证明的 portable asset 恢复到原始兼容边界；不得以静默降级功能、
  弱化诊断/安全或改变公开 API 来关闭兼容性错误。
- Escalation triggers: 需要新增或改变 public/protected API；任一 ISO/EXPRESS、安全、签名、原子性或
  schema 行为不能在 .NET Standard 2.0 等价实现；需要恢复 Annex F；需要新增运行时依赖、支持低于
  .NET Framework 4.7.2，或新增第三个 library TFM。

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from the approved baseline.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: 一次原子交付完成四个包的双目标编译、兼容实现、生成输出、包元数据、
  消费者验证与当前文档。
- Other real start conditions or resource prerequisites: Windows 上可运行的 .NET Framework 4.7.2+
  reference/runtime、.NET 8 runtime、.NET 10 SDK 与 Native AOT 工具链；NuGet dependencies 必须为
  两个目标提供兼容资产。
- Likely touchpoints (non-binding): 公共 package csproj、runtime 内部兼容层、Analyzer 生成支持、AP
  baseline/package tests、packed consumers、README/product/architecture documentation。
- Private implementation choices left open: polyfill 与私有 fallback 的组织、测试夹具布局、条件编译位置、
  package inspection helper 以及是否复用现有 packed-consumer scripts。

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=boundary shape=contract -->
<!-- primary-proof: AC-02 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-03 purpose=boundary shape=integration -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | 四个 nupkg 只含完整的 `netstandard2.0`/`net8.0` lib 资产和匹配依赖组 | 运行 integration package asset contract tests |
| AC-02 | Primary | net472-or-later、net8 与 net10 package-only consumers 完成相同 runtime 契约、有效 detached CMS 签名验证及 malformed/invalid CMS 原子拒绝，且结果等价 | 构建候选包后运行三目标 consumer compatibility matrix，明确覆盖 CMS success/rejection |
| AC-03 | Primary | 核心两个 TFM 均匹配累计批准 runtime API baseline，三个 AP 包各 TFM 均匹配其批准 API baseline；AP descriptor/runtime graph 不变；net8 与 net10 package-only Native AOT consumers 发布执行无警告 | 运行核心累计 API baseline、三个 AP package API baselines 与跨 TFM comparison，再分别运行 net8/net10 packed-consumer Native AOT verification |
| Existing runtime regression | Conditional | 现有单元、集成、conformance 与 package journeys 保持通过 | `dotnet build TedToolkit.Step21.slnx --configuration Release`; `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release`; `dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release` |

### Current implementation evidence — 2026-09-10

- `TedToolkit.Step21` 的两个目标均以 Release、零 warning/零 error 构建；严格 NuGet package validation
  通过，候选只含 `lib/netstandard2.0` 与 `lib/net8.0` 及各自依赖组。
- 全新包缓存中的 package-only consumer 证明资产选择为 net472 → `netstandard2.0`、net8 →
  `net8.0`、net10 → `net8.0`；三者都通过生成 descriptor、读写、detached CMS、显式信任及
  malformed/invalid CMS 拒绝旅程。
- net8 与 net10 package-only `win-x64` Native AOT publish/run 均零 warning 并输出
  `STEP21_COMPATIBILITY_OK`。
- 在 net472、netstandard2.0 与 net8 编译的 package-only schema generator probe 均零 warning/零
  error，证明旧目标不需要公开 compatibility polyfill。
- 基于 `2c96dd5` 的 staleness audit 保持 Ready：ADR-0011 的 `Schemas` namespace 迁移改变 AP API
  baseline 名称，但不改变本 change 的 TFM、资产选择、运行时语义或依赖方向。
- 四个公开项目和 package contract assertions 已迁移到双目标。完整 AP nupkg/API 回归仍等待当前独立
  authoritative-schema/generator 工作树恢复可构建；其 AP203 生成当前在两个目标上均产生既有 generator
  编译错误，因此本 change 保持 `in-progress`，不把该错误误记为 TFM 兼容失败。

<!-- section: completion-criteria -->
## Completion

AC-01 至 AC-03、受影响回归与候选绑定复审全部通过；产品意图、ADR、README、schema baseline 和 package
metadata 陈述同一双目标当前事实；没有临时 probe、公开 polyfill 泄漏、未解释的跨 TFM API 差异或待办
外部发布操作。终态 change record 在合并并由默认分支引用后按共享生命周期清理，不建立 completed archive。
