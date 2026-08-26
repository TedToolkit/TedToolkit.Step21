# 提供开箱即用的 AP203 schema 包

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: completed -->

## 📌 Status

Completed

- Prior approval evidence: 用户于 2026-08-23 批准原始变更，并于 2026-08-24 批准重复 nominal schema name 必须以 `P21-BIND-SCHEMA` 拒绝。该批准仍解释已完成候选实现的历史授权，不等于批准本次修订。
- Revision approval: 用户于 2026-08-26 先批准按 ADR-0006 → AP203 → Compiler modularization 的顺序执行，随后明确批准 ADR-0006 的 package/runtime/generated-surface 兼容策略；此前独立设计复审确认除 ADR 接受外无剩余文档缺陷。本 revision 因此获准成为新的 AP203 交付基线。
- Completion evidence: AP203-001 through AP203-006 are integrated and independently reviewed Ready. The final delivery revision is `2a0b4ce`; all AC-01 through AC-13 and the accepted ADR-0006 compatibility policy are closed with executable evidence.

## 🚦 Change priority

- Priority: P2
- Rationale: 无硬截止日期的计划性互操作能力，对普通 CAD STEP 消费者有直接价值。
- Directory: `docs/changes/P2-ap203-schema-package/`.

<!-- section: goal-rationale -->
## 🎯 Change goal

> When this change is complete, .NET 消费者可以只引用预编译的 AP203 包就读取、编辑、验证和写回 OCCT 风格的 `CONFIG_CONTROL_DESIGN` ISO 10303-21 交换结构，并由真实 AP203 样本的 schema-bound 读取与读-写-读语义等价证明。

This goal is unchanged unless the change is revised and approved again. Every behavior case,
completion criterion, and later work item must contribute to it.

<!-- section: scope -->
## 🎯 Intended outcome and scope

### Intended outcome

- 提供 NuGet/程序集包 `TedToolkit.Step21.Ap203`。
- 包含 AP203 Amendment 1 AIM long-form schema `config_control_design` 所生成的强类型 .NET 表示。
- 消费者无需自行下载 `.exp`、配置 `AdditionalFiles` 或直接引用 Analyzer 实现项目。
- 读写仍仅通过 `ExchangeStructure.Read/Write`；新包不重复实现 parser/writer。
- 补齐 schema 身份绑定：识别 ISO 10303-21 `file_schema.schema_identifiers` 的 schema name 与可选 OID，并把小写 header 接受明确限定为 OCCT/既有文件互操作容差；原始 `FILE_SCHEMA` 拼写仍被保留。

### Scope

- AP203 Amendment 1 `CONFIG_CONTROL_DESIGN` schema 包的构建、打包、文档和消费者边界。
- 生成的 `SchemaDescriptor`、Entity 接口与可变类、defined type、enumeration、select 和 EXPRESS aggregate 投影。
- 标准 schema identifier 对 descriptor 的归一化匹配。
- 一个由仓库自有几何输入生成、固定 OCCT 版本和导出参数的 AP203 互操 fixture，以及来源、许可和完整性记录。
- TUnit 快速/契约/集成测试、打包消费者与 Native AOT 发布/执行证明。

### Non-goals

- 不将 AP203 类型加入 schema-neutral `TedToolkit.Step21` 运行时程序集。
- 不提供 `TopoDS_Shape` 或其它 CAD kernel 模型转换。
- 不新增 `StepReader`、`StepWriter`、AP203 model facade、registry 或反射 schema 发现。
- 不交付 AP203 Edition 2，也不承诺 OCCT AP203 模式的所有可选模块扩展。
- 不在本 change 交付 AP214/AP242；二者属于后续独立可发布 change。
- 不承诺字节、注释、空白或原 token 拼写保留。

### Compatibility expectations

- 入口仍是 `ExchangeStructure.Read(TextReader, IReadOnlyCollection<SchemaDescriptor>)`。
- 包名为 `TedToolkit.Step21.Ap203`；现有生成器产生的 namespace 为 `TedToolkit.Step21.Generated.ConfigControlDesign`。
- `SchemaDescriptor.Instance.Name` 保持 EXPRESS nominal name；匹配可忽略大小写并识别本 change 支持的 canonical numeric-arc OID 后缀，但不将 AP214/AP242 误当 AP203。
- 引用 AP203 包的消费者不应再通过 `AdditionalFiles` 提供同名 schema，以避免重复公开类型。
- 包元数据和消费者文档必须声明固定的 AP203 source/edition、descriptor identity、兼容的 `TedToolkit.Step21` 版本范围，以及生成 public surface 的版本策略。
- 不得把不同 AP203 edition/baseline、破坏性生成 public surface 或不兼容 runtime 要求静默发布为兼容更新。

### Migration and rollback

- 这是可选新包，现有消费者无需迁移；只有主动引用 `TedToolkit.Step21.Ap203` 的客户获得新类型。
- schema identifier 归一化是对已记录架构契约的向后兼容完善；原始 header 值和现有精确匹配继续有效。
- 首次发布前可回退新包与归一化行为，不改变已发布 API；发布后如发现 schema 基线不可用，必须以包版本更正/废弃通知处理，不静默换入不同 AP203 edition。

## 🧾 Source intent and hard constraints

- Source request: 用户于 2026-08-22 请求先提供 AP203 库，并将 AP214/AP242 列入后续范围。
- User outcome: 普通 .NET 消费者无需管理 EXPRESS 源文件即可使用 OCCT 风格 AP203 文件。
- External hard constraints:
  - schema 基线是 STEPcode 提交 `9baa5dadaa1dcfcdc623220d865d36d61ea351e9` 的 [`data/ap203/ap203.exp`](https://github.com/stepcode/stepcode/blob/9baa5dadaa1dcfcdc623220d865d36d61ea351e9/data/ap203/ap203.exp)；该文件声明为 AP203 Amendment 1 AIM long form 的无语义影响修改。
  - 采入前记录 schema 校验值、上游提交和重分发许可证据；STEPcode collective work 标记为 BSD-3-Clause。
  - OCCT 互操作证据固定到提交 `7d2efad9c8a9a57ea96c4c8587134b34dd503cd8`，其写出选项含 AP203、AP214 和 AP242DIS。
  - Schema identifier 的规范基线为 ISO 10303-21:2016 Edition 3 clause 8.2.4：每个 identifier 是 schema name 加可选 OID，schema name 的物理字符串只允许大写。当前 OID 能力明确收窄为该条款 note/example 所示的 canonical、空格分隔、纯数字弧子集；其校验参照 ISO/IEC 8824-1:2015 / ITU-T X.680 (08/2015) clause 32 和 ISO/IEC 9834-1 / ITU-T X.660 的根弧限制。X.680 允许的命名弧等其它表示在本 change 中显式 unsupported。小写 header 接受是互操作容差，不是语法 conformance 声明。
  - 无硬截止日期或已声明预算。

## 🧩 Governing principles and decisions

- Product intent: `docs/product/README.md@5e5c5187ffc0857101df9f4fad67ef7bfbfff231`.
- Principles:
  - `docs/principles/architecture.md@ad80acfc31b8557991842e326dcee81ad7792bf9`: AP-001, AP-002, AP-003.
  - `docs/principles/engineering.md@9a28794f46ca41c3489bb402a86ddf3dd3f5a2b8`: EP-002, EP-003, EP-004.
- Accepted ADRs:
  - `docs/adr/ADR-0002-roslynhelper-source-composition.md@5021769553568e25aa8ae8e5b7af39cc2c9b1c82`.
  - `docs/adr/ADR-0003-aot-ready-schema-validation.md@7a45566e5dec353194c2124ee23a4b01e9cffa56`.
  - `docs/adr/ADR-0005-explicit-section-graph-registration.md@bd1e3d65da499cb849c3fb7082135e424865b594`.
- Accepted packaging decision: `docs/adr/ADR-0006-precompiled-schema-package-distribution.md@3eb14b4d8657f77da8f686d3f2ee0c25fbaa3179`，包含 package/runtime/generated-surface/provenance 版本兼容策略。
- Architecture: `docs/architecture/schema-bound-round-trip.md@9a28794f46ca41c3489bb402a86ddf3dd3f5a2b8`.
- Reapproval trigger: 上述任一 governing record 修订并改变本契约。

Resulting constraints:

- AP203 类型仅存在于可选 schema 包；运行时不反向依赖 AP203、Analyzer 实现或具体 schema。
- 公开领域类型必须可追溯到固定 EXPRESS 源，不添加 OCCT 或库自创领域概念。
- Entity 保持可变引用身份，EXPRESS 继承只由接口表示，实例名和 section 所有权仍属于 `ExchangeStructure`。
- 读取仅发布完整已验证图；编辑可临时无效；写入前聚合验证。
- 生成、绑定、验证和写入不得使用程序集扫描、反射 schema 发现或动态代码，并须通过 Native AOT 证明。
- schema identifier 归一化只影响 descriptor 选择，不改写原始 `FILE_SCHEMA` 值。
- 语法接受、schema 语义正确性和外部工具互操作性分别取证；OCCT fixture 只能证明固定导出器/参数/样本范围内的互操作，不得被表述为完整 AP203 conformance。
- 每个维护的 schema baseline 保持独立可选包与显式 descriptor；运行时不得自动发现、自动下载或根据 header 隐式选择包。

<!-- section: delivery-brief -->
## 🧭 Planned approach

`TedToolkit.Step21.Ap203` 是可选 schema 程序集/NuGet 包。构建时，现有 Step21 源生成器将固定 AP203 long-form `.exp` 编译为 descriptor、Entity/值类型和直接绑定/验证/投影代码。消费者仅获得已编译类型与 `TedToolkit.Step21` 运行时依赖，不重新生成 AP203 schema。

调用者仍显式向 `ExchangeStructure.Read` 传入 `TedToolkit.Step21.Generated.ConfigControlDesign.SchemaDescriptor.Instance`。新包不包装该调用、不自动扫描 descriptor，也不转换为 OCCT/CAD-kernel 对象。

为匹配真实 AP203 header，schema-neutral 绑定边界按 ISO 10303-21:2016 clause 8.2.4 分离 nominal schema name 与可选 OID，而非比较完整原字符串。标准路径支持大写 name 与该条款 note/example 所示的 canonical numeric-arc OID 子集；X.680:2015 clause 32 允许但本子集未覆盖的命名弧等形式显式 unsupported。大小写无关匹配是针对既有小写 header 的明确互操作容差，因为 clause 8.2.4 要求物理 `schema_name` 字符串使用大写。所有已接受路径都保留 header 原值，但文档不得把小写输入或其原样写回宣称为语法 conformance。不合法/unsupported OID、近似名称或其它协议标识仍产生现有 schema-binding 失败证据。

OID 在本 change 中只接受有界语法检查并保留原文；descriptor 仍仅按 nominal schema name 选择。本 change 不宣称验证 OID 到固定 AP203 baseline 的分配关系，也不使用 OID 自动选择 schema edition。若未来需要同 nominal name 的多个 baseline 或用 OID 强制版本身份，必须先由独立 architecture decision 扩展 descriptor identity 与失败契约。

构建和测试仅使用仓库内固定 schema/fixture，不从网络获取活动资产。OCCT fixture 由仓库自有的最小几何/产品输入通过固定 exporter 提交与 AP203 参数一次性生成后检入；验证时不要求安装 OCCT。包文档记录 schema 版本、来源、校验值、许可、`FILE_SCHEMA` 标识和已验证 OCCT 范围。

AP203 包遵循 ADR-0006 的“一份维护基线一个可选包”边界，并在首次公开发布前固定三层兼容信息：schema 来源/edition、所需核心 runtime 版本范围、生成 public surface 的破坏性变更规则。自定义 `.exp` 路径继续由通用 Analyzer 提供，不被预编译包取代。

Rejected alternatives:

- 每个消费者自行维护 `.exp`：仍是核心包的通用用法，但不能实现开箱即用目标。
- 将 AP203 合并到 `TedToolkit.Step21`：违反 schema-neutral 依赖方向。
- 手写 descriptor/Entity：产生与 EXPRESS 分离的第二份真相。
- 一包合并 AP203/AP214/AP242 并运行时扫描：放大体积/AOT 成本，阻塞独立发布，违反显式 descriptor 边界。

Target delivery artifacts: code, tests, configuration/build metadata, package contents, and consumer documentation.

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. This delivery did not consume another repository change outcome.

## 🔀 Delivery disposition and operational handoffs

- Target delivery artifacts: code, tests, configuration, build automation, package contents, documentation.
- No-delivery-change evidence: not applicable.

| External operational handoff | Owner | Completion evidence | Required before change closure? |
| --- | --- | --- | --- |
| None | N/A | N/A | No |

<!-- section: behavior-contract -->
## 🧪 Behavior cases

<!-- acceptance-case: AC-12 -->

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| AC-01 | 新 .NET 10 消费者只引用已打包的 AP203 包 | 编译并访问 AP203 生成类型 | 无需 `.exp`/`AdditionalFiles` 即可访问唯一 descriptor 和 schema 类型，运行时图不泄漏 Analyzer-only 依赖 |
| AC-02 | AP203 包已构建 | 检查 descriptor 和代表性实体/值类型 | descriptor nominal name 是 `config_control_design`；继承、可空性、aggregate 种类和成员顺序与固定 EXPRESS 一致 |
| AC-03 | `FILE_SCHEMA` 为标准大写 `CONFIG_CONTROL_DESIGN`，或为明确列入互操作容差的小写/混合大小写变体 | 用 AP203 descriptor 读取 | 都选中同一 descriptor并保留原 header；文档将非大写路径标为 compatibility tolerance，且不把其原样写回宣称为 ISO 10303-21 syntax-conforming output |
| AC-04 | `FILE_SCHEMA` 含 ISO 10303-21:2016 8.2.4 note/example 形状的 canonical numeric-arc OID，例如 `CONFIG_CONTROL_DESIGN { 1 0 10303 203 1 1 1 }`；或含 X.680:2015 clause 32 的命名弧等未支持形式 | 用 AP203 descriptor 读取 | 支持的数字弧子集按 nominal name 匹配并保留完整 header；其它形式以稳定 `P21-BIND-SCHEMA` 显式 unsupported；两者都不产生 OID-to-baseline identity 声明 |
| AC-05 | 固定 OCCT 版本以 AP203 模式导出的 B-rep/产品样本 | 用 AP203 descriptor 读取 | 原子地返回已验证结构；product、shape、topology、geometry、unit 和引用可通过生成类型观察 |
| AC-06 | 有效 AP203 结构的可编辑属性被修改 | 验证、写入、重读 | 修改后值、实体类型、参数、实例身份和引用关系在语义上保留 |
| AC-07 | 编辑后违反 AP203 结构或可达 EXPRESS 规则 | 显式验证或写入 | 验证聚合稳定失败；写入在任何字节输出前以完整结果失败 |
| AC-08 | `FILE_SCHEMA` 声明 AP214、AP242、未知 schema、不合法伪 AP203 后缀，或包含归一化后 nominal name 重复的不同原始标识 | 仅提供 AP203 descriptor 读取 | 产生稳定 `P21-BIND-SCHEMA` 证据，不发布部分模型、不误用 AP203，也不以 first-wins 或静默折叠消除歧义 |
| AC-09 | 文件声明 AP203 但包含固定 schema 未声明的 OCCT 扩展 | 读取 | 以带位置 schema/binding 证据显式失败，文档不将此范围宣称为已支持 |
| AC-10 | 消费者同时引用 AP203 包与核心包 | 检查编译和运行时依赖 | 只有一份兼容 Step21 运行时；不添加 JSON/XML、反射发现或第二套 parser/writer |
| AC-11 | 代表性 AP203 打包消费者以裁剪 Native AOT 发布 | 执行读取、导航、编辑、验证、写入和重读 | 发布/执行成功，无该包或生成路径导致的 trim/AOT 警告 |
| AC-12 | schema/fixture 都已检入仓库 | 在无网络的干净环境构建/验证 | 生成输出可重现，不依赖活动上游、本机 OCCT 或隐藏缓存 |
| AC-13 | 检查候选包元数据、依赖范围、生成 public surface 和消费者文档 | 比较固定 AP203 baseline 与声明的版本兼容策略 | source/edition、descriptor identity、核心 runtime 范围和 generated-surface 兼容规则均明确且一致；不同 baseline/edition 或破坏性 surface 不会被静默视为兼容更新 |

<!-- section: proof-plan -->
## 🧪 TUnit verification contract

| Contract | Level | Setup/resource strategy | Primary proof and observable assertions |
| --- | --- | --- | --- |
| TV-01 packaged surface | Contract/integration | 每测试独立临时 NuGet 目录和最小消费者，结束清理 | 仅 package reference 即可编译 descriptor/Entity；无 consumer `.exp`；运行时无 Analyzer-only/JSON/XML 资产 |
| TV-02 generated fidelity | Unit/contract | 真实生成类型，无 mock | descriptor name/singleton，代表性多继承 Entity、OPTIONAL/必填属性、SELECT、enumeration、aggregate 与 EXPRESS 一致 |
| TV-03 identifier matching | Unit/contract | 四组独立的内存 Part 21 数据：(a) clause 8.2.4 大写 schema name；(b) 明确标为互操作容差的大小写变体；(c) 8.2.4 note/example + X.680:2015 clause 32/X.660 约束内的 canonical numeric-arc OID 正负例；(d) 合法但未支持的命名弧形式、跨协议近似值与归一化重名 | 标准输入和容差输入均选中同一 descriptor并保留原文，但 conformance 报告区分二者；非法或未支持 OID、其它协议或歧义变体仅产生稳定 schema-binding 失败；测试不声称 OID 与 AP203 baseline identity 已验证 |
| TV-04 OCCT AP203 read | Integration/contract | 已检入、已校验、记录 OCCT 版本/参数的 fixture；不调用 OCCT/网络 | 读取 valid；指定 product/shape/topology/geometry/unit 数量、关键值和引用对象身份正确 |
| TV-05 semantic round trip | Integration/contract | 每测试独立 reader/writer 和对象图，不依赖顺序 | 修改后 Validate/Write/重读成功；schema/type/value/identity/reference 语义签名一致 |
| TV-06 atomic failures | Unit/integration | 分别构造 schema mismatch、未知 Entity、编辑后失败；独立写入 buffer | 读取不返回部分模型；异常有预期 code/location/result；可预检写失败保持输出长度为零 |
| TV-07 AOT consumer | End-to-end | 独立临时发布输出/本地包源；共享 pack/publish 仅用精确 parallel resource key | AOT 发布/执行成功；完整 AP203 旅程被执行；零可归因 trim/AOT 警告 |
| TV-08 reproducible source | Contract/integration | 只用检入 schema/fixture 与固定包资产；禁止测试下载 | schema 校验值/来源一致；两次干净构建产生等价公开程序集/包内容 |
| TV-09 versioned package contract | Contract/integration | 从候选 `.nupkg`、固定 API snapshot 和仓库文档读取真实数据，不复制测试专用元数据 | 包依赖范围、schema provenance/edition、descriptor identity 与文档一致；相对已批准基线的破坏性 generated-surface 差异要求显式不兼容版本处理 |

All TUnit assertions are awaited. Tests use real descriptors, values, readers, writers, packages,
and processes; no mock is justified. Integration resources are independently addressed and cleaned
in `finally`; tests do not use `[DependsOn]` or shared mutable model state.

<!-- section: completion-criteria -->
## ✅ Completion criteria

- `TedToolkit.Step21.Ap203` 可从干净、无网络仓库状态可重现地构建/打包。
- 客户可见表面仅由固定 AP203 EXPRESS 生成，且代表性形状对照通过。
- 最小消费者仅使用 package reference，无 `.exp`/`AdditionalFiles`，即可执行 AP203 读-编辑-验证-写-重读。
- 固定 OCCT AP203 fixture 通过 schema-bound 读取、关键值/引用断言和语义 round trip。
- clause 8.2.4 大写 name、明确标记的大小写互操作容差和受支持的 canonical numeric-arc OID 子集都选中同一 descriptor 而不改写 header；conformance 文档区分标准、容差和 unsupported 路径，并明确不验证 OID-to-baseline identity；其它协议、未知、不合法/未支持 OID 或归一化重名变体显式失败。
- 快速、集成、打包和 Native AOT 验证零错误、零警告，且预期测试均被发现/执行。
- 文档记录 AP203 版本、schema name、上游提交、校验值、许可、OCCT fixture 来源和未支持边界。
- ADR-0006 已 Accepted，包/runtime/generated-surface 兼容策略已记录，并由候选包元数据、API 对照和消费者文档共同证明。
- 依赖审计证明不将 Analyzer-only、RoslynHelper、JSON/XML 或反射发现资产引入运行时图。

## 📋 Completion evidence

- All six approved work items are `Implemented`; their contract tables assign AC-01 through AC-13 exactly once, and each item records a Ready implementation review. The final parent review traced the complete delivery history `c463d03..2a0b4ce` and found no blocking or advisory issue, no unapproved behavior, and no unresolved operational handoff.
- `TedToolkit.Step21.Ap203` 1.0.0 exposes the generated `config_control_design` descriptor/types from the pinned AP203 Amendment 1 long form, declares core runtime range `[1.0.0,2.0.0)`, and retains explicit descriptor selection with no runtime discovery or duplicate reader/writer stack.
- The checked-in OCCT sample executes package-only read, typed navigation, concrete edit, validation, canonical write, descriptor-bound reread, semantic comparison, invalid-edit aggregation, zero-byte write rejection, and positioned unsupported-extension rejection. Its supported observations remain 200 entities, 1 product, 6 faces, 12 edges, 8 vertices, 27 points, and metre/radian/steradian units.
- Two forced non-incremental offline package proofs produced the same normalized package SHA-256 `2BEBC39E95BE86145DC35C85743ED522AC53141AE406136EA7B99180FF77996E`; schema and fixture hashes match their provenance records. The actual packaged generated API matches nullable-aware approved SHA-256 `CD3FE5BEC20E5990451DA1428E758889702EF1C768D90581308305EE7C45F1A8`.
- The package is 1,924,060 bytes and its generated assembly is 14,647,296 bytes. The trimmed `win-x64` Native AOT package consumer is 39,154,176 bytes, emitted no attributable trim/AOT warning, and completed the full fixed AP203 journey. The generic AOT proof remains green.
- Final verification passed fast TUnit 344/344, integration 7/7 enabled tests with the one opt-in external-network corpus test explicitly skipped, and the Release solution build with 0 warnings and 0 errors. The package/runtime graph contains one core runtime and no Analyzer-only, RoslynHelper, JSON/XML, reflection discovery, or second parser/writer runtime edge.
- Durable package README, schema/fixture provenance, architecture, principles, and Accepted ADR-0006 remain in place. This completed Controlled change and its work items are retained as delivery history; AP214/AP242 remain separate future changes rather than implicit AP203 scope.

## ⏱️ Workload estimate

- Person-month basis: 一名熟悉 .NET、Roslyn source generator 和 schema-driven 测试的全职开发者的一个月；不换算为工作日/小时。
- Change delivery range: 0.8–1.6 person-months.
- Coordination, verification, migration, and rollout allowance: 0.3–0.6 person-months.
- Contingency: 0.4–1.0 person-months，用于大型 schema 首次暴露的生成器语义缺口、命名冲突、AOT 规模和 OCCT 扩展差异。
- Total planning range: 1.5–3.2 person-months.
- Confidence: Medium-Low.
- Assumptions/exclusions: 固定 schema 可依现有 EXPRESS 前端解析；只交付 `CONFIG_CONTROL_DESIGN`；AP203e2、OCCT 扩展、AP214/AP242、CAD-kernel 转换、NuGet.org 发布和外部法务周期均排除。
- Re-estimation trigger: 如果需要新公开运行时概念、新持久架构决策、修订现有验证契约，或总量超过 3.2 person-months，停止并重新设计/批准。

Documentation forecast: package/API 指南、schema 来源/许可、互操作和能力文档需持久保留；EP-004、architecture 和 ADR-0006 已改变本变更的治理基线，批准时必须固定 committed revisions；change 记录最终去留由实现评审时的人工决定。

## 🚧 Design blockers

| ID | Blocking item | Blocks | Next action | Status |
| --- | --- | --- | --- | --- |
| AP203-DB-01 | ADR-0006 与首次公开发布的 package/runtime/generated-surface 版本兼容策略必须成为 Accepted 决策 | 本修订的批准、AP203-006 启动和 change closure | 已在 `3eb14b4d8657f77da8f686d3f2ee0c25fbaa3179` 接受并固定 | Closed |
| AP203-DB-02 | 早期候选 `f66c33d` 的实现状态与证据需要迁移到当前 delivery baseline | AP203-004 后续交付和最终实现评审 | 已以 `57fea41..3b84aa3` 等价迁移、补齐证明并更新唯一 work-item status source | Closed |

## ⚠️ Risks and coordination

| Item | Impact | Next action |
| --- | --- | --- |
| 上游 schema 是对 AP203 Amd.1 的已声明无语义影响修改 | 不能宣称官方字节一致 | 固定提交/校验值，保留修改说明，仅宣称已验证范围 |
| schema/fixture 重分发许可证据不完整 | 包不得发布 | 采入前记录许可证据；无证据则停止交付 |
| 大型 AP203 schema 暴露 EXPRESS 绑定/执行缺口或 C# 命名冲突 | 无法编译或验证不完整 | 修正可追溯到标准语义的根因；若改变契约则返回 change-design |
| OCCT AP203 可包含基线外模块扩展 | 部分 AP203 标识文件仍失败 | 固定导出参数并文档化边界，扩展支持交给后续 change |
| 大量生成类型增加程序集、NuGet、IDE 和 AOT 成本 | 下载/构建成本过高 | 实测体积和 AOT；超预算时重新设计 |
| schema identifier 归一化过宽 | 不同 schema 被误绑定 | 仅实现标准规则，用跨协议/恶意近似输入负测试 |
| 消费者又提供同名 `.exp` | 重复公开类型 | 包指南明确禁止，并证明编译诊断可理解 |
| schema baseline、核心 runtime 范围与生成 public surface 独立演进 | 消费者可能在看似兼容的升级中遭遇编译或运行时破坏 | 在 ADR-0006 固定版本策略，以包元数据、API snapshot 和文档执行 AC-13/TV-09 |

## 🗺️ Follow-up package range

以下是独立可发布 change，不是 AP203 交付的依赖或 work item：

| Proposed package | Intended schema family | Entry condition |
| --- | --- | --- |
| `TedToolkit.Step21.Ap214` | OCCT-supported AP214 CD/DIS/IS `AUTOMOTIVE_DESIGN` variants | AP203 完成证据已评审，AP214 schema/version 打包策略已独立批准 |
| `TedToolkit.Step21.Ap242` | OCCT-supported AP242 `AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF` baseline | AP203 完成证据已评审，AP242 edition/DIS 来源、许可、生成规模和 PMI 范围已独立批准 |
