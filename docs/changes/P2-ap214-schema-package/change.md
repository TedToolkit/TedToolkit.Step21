# 提供开箱即用的 AP214 schema 包

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: completed -->
<!-- delivery-shape: multi-item -->

- Priority: P2
<!-- approval-source: user-explicit-approve-and-implement-2026-08-27 -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

.NET 消费者只需引用 `TedToolkit.Step21.Ap214`，无需自行取得 `.exp` 或配置
`AdditionalFiles`，即可通过现有 `ExchangeStructure` API 读取、编辑、验证、写出并重读一份
固定的 OCCT AP214 互操作样本。

AP203 已证明预编译 schema 包、固定来源和 package-only 消费者的交付模式；当前 AP214 仍只能走
通用 Analyzer 路径。独立 AP214 包补齐常用 `AUTOMOTIVE_DESIGN` baseline，同时保持核心 runtime
与 application protocol 解耦。

<!-- section: scope -->
## Scope and non-goals

- In scope:
  - 新增独立程序集/NuGet 包 `TedToolkit.Step21.Ap214`，维护一份固定的
    `AUTOMOTIVE_DESIGN` EXPRESS baseline。
  - 包内交付该 baseline 的生成 public API 与唯一显式 descriptor，并记录来源、edition、完整性、
    重分发许可、生成 surface snapshot、包元数据和消费者指南。
  - 检入由固定 OCCT revision、固定导出参数和仓库自有几何输入产生的 AP214 fixture，证明
    schema-bound 原子读取、typed navigation、编辑、验证、写出和语义重读。
  - 复用现有 Analyzer、descriptor、`ExchangeStructure.Read/Write`、验证和诊断边界；固定 schema
    暴露的缺口仅可作可追溯到 EXPRESS 语义的 schema-neutral 根因修复。
  - 证明 AP214 包可单独消费，也可与现有 AP203 包共存而无类型、descriptor、依赖或 runtime
    选择冲突。
- Non-goals:
  - 不创建联合 application-protocol 包，不让 AP214 包依赖其它 schema 包，也不改变 AP203 的
    public contract。
  - 不同时维护 AP214 CD/DIS/IS 的多个 baseline；首版只对应本文固定 source。增加或替换
    edition/vendor variant 按 ADR-0006 作为显式兼容性变更处理。
  - 不提供 CAD-kernel/OCCT 对象转换、application-protocol facade、runtime registry、程序集扫描、
    自动下载或根据 `FILE_SCHEMA` 隐式加载包。
  - 不宣称完整 AP214 conformance；首版互操作范围只覆盖固定 fixture 中的 manifold B-rep、
    product、topology、geometry、unit、值、身份和引用。Colour/layer、PMI、tessellation、
    kinematics 及其它未进入 fixture 的能力不在声明范围内。
  - 不包含 NuGet.org 发布或外部法务审批。
- Compatibility or deliberately preserved behavior:
  - 调用者继续显式传入 `SchemaDescriptor` 并使用同一核心 read/write model；核心 runtime 不依赖
    AP214 包，AP214 包也不实现第二套 parser、model、validator 或 writer。
  - `SchemaDescriptor.Instance.Name` 保持 EXPRESS nominal name；原始 `FILE_SCHEMA` 值继续保留。
    OID 不作为 package baseline identity，也不用于自动选择 edition。
  - 同 nominal name 的其它 edition 不会仅凭 header 被识别或拒绝；它们不属于支持声明，并在
    使用 baseline 外结构时沿用现有显式 schema/binding 失败路径。
  - 不同自定义 `.exp` 的 Analyzer 工作流继续可用；包指南和消费者模板必须把同一预编译 schema
    的 `.exp`/`AdditionalFiles` 声明为互斥输入，且不得以 first-wins 或静默消解把该组合包装成受支持用法。

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | AP214 package consumption | 消费者必须自行取得并编译 AP214 EXPRESS | 只引用 `TedToolkit.Step21.Ap214` 即可访问唯一 AP214 descriptor 和生成类型 | 仍显式选择 descriptor 并使用同一 schema-neutral runtime |
| OB-02 | 固定 baseline 与生成表面 | 仓库没有受维护的 AP214 source、provenance 或 public API baseline | 包的 descriptor、生成 API、元数据和来源记录共同固定一份 `AUTOMOTIVE_DESIGN` baseline | 不把保留的 OID 或近似 header 当作 baseline identity |
| OB-03 | 真实 AP214 旅程 | 仓库没有 AP214 schema-bound round-trip 证据 | 固定 OCCT fixture 完成原子读取、typed navigation、编辑、验证、写出和语义重读 | 读失败不发布部分模型，写失败不输出部分 library-produced bytes |
| OB-04 | 包隔离和生成边界 | 只有 AP203 维护包已证明 package coexistence；AP214 尚无预编译包输入指南 | AP203 与 AP214 可共存且只解析到一份 runtime；AP214 README/模板明确预防重复 `.exp` 输入 | distinct custom schema Analyzer 路径、静态 descriptor 选择和 Native AOT 方向保持不变 |

<!-- acceptance-case: AC-01 -->
### AC-01 — AP214 独立包可直接消费

```gherkin
Scenario: 仅引用 AP214 预编译包
  Given 一个干净的 .NET 10 消费者只引用候选 TedToolkit.Step21.Ap214 包
  When 它编译并访问 AP214 descriptor 与代表性生成类型
  Then 无需 .exp 或 AdditionalFiles 即可成功，且运行时资产不包含 Analyzer-only 依赖
```

<!-- acceptance-case: AC-02 -->
### AC-02 — 固定且忠实地声明 AP214 baseline

```gherkin
Scenario: 检查候选包的 schema 身份和生成表面
  Given 已检入且记录来源版本校验值许可和 schema identifier 的固定 AP214 EXPRESS source
  When 比较 descriptor、代表性继承可空性聚合成员顺序和完整 public API snapshot
  Then 包只声明 AUTOMOTIVE_DESIGN，且生成表面与固定 source 一致
```

<!-- acceptance-case: AC-03 -->
### AC-03 — 固定 OCCT AP214 样本完成语义 round trip

```gherkin
Scenario: 处理固定 OCCT AP214 样本
  Given 固定样本声明 AUTOMOTIVE_DESIGN canonical OID、一个 10 × 20 × 30 mm 原点 box、1 个 product、6 个 face、12 个 edge 和 8 个 vertex
  When 原样读取先证明两个已知 edition 规则差异，再确定性补齐 2007-DIS AIM identity 与唯一 id-owner assignment，将 product name 改为 `TedToolkit AP214 OCCT box 10x20x30 mm - edited`，验证、写出并重读
  Then 原始 byte/hash 保持不变、原始读取不发布部分模型，迁移后的 product name、box extents、millimetre/radian/steradian units、实体种类与数量、实例身份和共享引用关系保持该固定语义签名
```

<!-- acceptance-case: AC-04 -->
### AC-04 — 错误 schema、baseline 外结构和无效编辑原子失败

```gherkin
Scenario: 输入或编辑不属于所选 AP214 baseline
  Given descriptor 集合含重复 nominal name、文件声明不同 schema 或含 baseline 外实体、或编辑后违反可达 schema 规则
  When 调用读取、验证或写出
  Then 配置歧义在消费输入前抛出 ArgumentException，其它失败产生稳定 schema/binding 或聚合验证证据，且不发布部分模型或写出部分字节
```

<!-- acceptance-case: AC-05 -->
### AC-05 — AP203 与 AP214 包可共存且保持隔离

```gherkin
Scenario: 一个消费者同时引用 AP203 和 AP214
  Given 两个 schema 包及兼容的 TedToolkit.Step21 runtime
  When 消费者显式选择各自 descriptor 并检查编译与运行时依赖图
  Then 每个 fixture 绑定到正确 schema，只有一份 runtime，且 schema 包之间没有依赖、扫描或第二套 reader/writer
```

<!-- acceptance-case: AC-06 -->
### AC-06 — 包、来源和生成表面可离线重现并按策略版本化

```gherkin
Scenario: 从固定输入重复构建候选包
  Given schema 与 fixture 已检入且候选元数据声明 source edition descriptor runtime range 和兼容策略
  When 在无网络的干净环境执行两次非增量构建并比较包及 public API
  Then 规范化产物等价且 provenance 一致，baseline 或破坏性 surface 变化不能被视为兼容更新
```

<!-- acceptance-case: AC-07 -->
### AC-07 — AP214 package-only 消费者保持 Native AOT 可用

```gherkin
Scenario: 发布 AP214 trimmed Native AOT 消费者
  Given 一个只引用候选 AP214 schema 包的消费者
  When 以 Release Native AOT 发布并执行完整固定样本旅程
  Then 旅程成功且没有候选包或生成路径导致的 trim 或 AOT 警告
```

<!-- acceptance-case: AC-08 -->
### AC-08 — 通用 Analyzer 路径保留且包输入明确预防重复 AP214 surface

```gherkin
Scenario: 检查自定义 schema 与预编译 AP214 schema 的生成边界
  Given 一个使用 distinct custom EXPRESS 的 Analyzer 消费者及 AP214 package README/消费者模板
  When 构建 custom consumer 并检查 AP214 的 documented build inputs
  Then custom schema 消费者继续成功，README 明确禁止为 AUTOMOTIVE_DESIGN 再配置 .exp/AdditionalFiles，且模板不包含该重复输入
```

## Constraints and risks

- Governing records:
  - `docs/product/README.md@5e5c5187ffc0857101df9f4fad67ef7bfbfff231`：AP214 仅作为
    selected EXPRESS schema 与互操作证据，不定义第二套核心领域模型。
  - `docs/principles/architecture.md@ad80acfc31b8557991842e326dcee81ad7792bf9`：AP-001、
    AP-002、AP-003 要求 ISO 10303-21 product boundary、schema-neutral runtime 和可追溯生成概念。
  - `docs/principles/engineering.md@9a28794f46ca41c3489bb402a86ddf3dd3f5a2b8`：EP-002、
    EP-003、EP-004 要求 ISO 语义、静态/AOT 路径和有界证据声明。
  - `docs/adr/ADR-0006-precompiled-schema-package-distribution.md@3eb14b4d8657f77da8f686d3f2ee0c25fbaa3179`：
    每份维护 baseline 独立成包、显式 descriptor、固定 provenance、SemVer 兼容分类和有界 runtime 依赖。
  - `docs/architecture/schema-bound-round-trip.md@94728d649d63b85633bd375b296b87f808609377`：
    复用现有原子 read/edit/validate/write、descriptor、静态生成与单一 runtime 边界。
- Fixed AP214 baseline: STEPcode commit `9baa5dadaa1dcfcdc623220d865d36d61ea351e9` 的
  [`data/ap214e3/AP214E3_2010.exp`](https://github.com/stepcode/stepcode/blob/9baa5dadaa1dcfcdc623220d865d36d61ea351e9/data/ap214e3/AP214E3_2010.exp)；
  upstream-byte SHA-256 为 `71AB140FE7F774321BEEE6A31E6FEE2AFC3973FD60350AE2018C74C211FB4295`，
  按仓库 `/schemas/**/*.exp text eol=lf` 规范化后的 canonical-LF SHA-256 为
  `9516315F0A8CBB9A4F6598D92FCE36BEE5189A28D1ACEA1D87E2C411266211B7`。源头声明为
  `ISO/DIS 10303-214:2007` AIM long form，日期 `2009-06-30`，nominal schema
  `AUTOMOTIVE_DESIGN`，registration identity 为 part 214/version 3/object 1/schema 1。
- STEPcode redistribution evidence: 该 revision 将 collective work 声明为 BSD-3-Clause；包保留
  `COPYING`（SHA-256 `C787486F3E1358CF1CB4456B56E00862DE9C0433E7D49F5501D1289FF8BEF37E`）、
  `AUTHORS`（`619EE3D3D9CE6DB690B4A20F36AB30616CB8F1FB8616FAEB85D9685AFDFD15FB`）、
  `INTENT.md`（`B10C7DCC9C269B383C944ACC139F787CCA050D497142CA03ED90C49EF41CE23F`）和逐文件
  provenance。若后续审计证明 schema 文件不在该授权内，交付停止而不是静默换源或豁免。
- Fixed fixture contract: fixture 固定到 OCCT commit
  `7d2efad9c8a9a57ea96c4c8587134b34dd503cd8`（`8.1.0-dev1`）和仓库自有的
  `BRepPrimAPI_MakeBox(10.0, 20.0, 30.0)` 在原点创建的 10 × 20 × 30 mm box，使用
  `STEPControl_Writer`、`STEPControl_ManifoldSolidBrep`、
  `WriteMode_StepSchema_AP214IS` 与 millimetres，并关闭 surface curves、color、name、layer、
  properties、metadata 和 material。输出必须声明
  `AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }`；只允许规范化 `FILE_NAME` 的 name、timestamp、
  author、organization、preprocessor、originating system、authorization，以及 exporter 生成的
  `PERSON`、`ORGANIZATION`、`LOCAL_TIME` 和 UTC-offset 值，并记录 generator/fixture SHA-256。
  该 AP214IS/2000-2002 输出只证明固定 2007-DIS package baseline 可接受的 B-rep 子集，不证明两个
  edition 等同或 OID-to-baseline identity。OCCT 许可为该 revision 的 LGPL-2.1 + OCCT exception；
  `LICENSE_LGPL_21.txt` SHA-256 为
  `E237FA56668030E928551DDD60F05DF5FE957F75EAB874BBD017E085ED722E7C`，
  `OCCT_LGPL_EXCEPTION.txt` SHA-256 为
  `04580A884EA6CEA294402649FF7B5CBB167D47462D1340A4ED33E550DB10A81B`。
  改变 source、fixture 能力边界、OCCT revision/参数或 edition/variant 必须重新批准。
- Package contract: 首版 TFM 固定为 `net10.0`；候选验证使用 `1.0.0-*` prerelease，本文全部
  completion criteria 满足后才可形成首个 stable `1.0.0`；对 `TedToolkit.Step21` 的依赖范围固定为
  `[1.0.0,2.0.0)`。发布后的 baseline、descriptor identity、生成 public surface、schema 语义或
  runtime 范围变更按 ADR-0006 分类，不能以同版本重建。
- OID boundary: 保留的 OID 不是 package baseline identity；本文不新增 OID-to-baseline registry，
  也不宣称仅凭同 nominal name header 区分 edition。支持边界由固定 source、package version、
  provenance 和 fixture 共同界定。
- Analyzer boundary: 自定义 schema 的 `.exp` + Analyzer 工作流继续存在，但不得与 AP214 包同时
  生成同一 schema surface。本文选择 ADR-0006 允许的“package guidance 明确预防”合同，不新增
  package-aware Analyzer 诊断、runtime discovery 或构建失败保证；未来若要求自动拒绝，必须另行设计
  schema-neutral 诊断合同。不得通过静默去重、生成顺序或 package-first 规则宣称该组合受支持。
- Large-schema risk: AP214 可能暴露 compiler semantic gap、C# 命名冲突、过大的生成 API/assembly/AOT
  体积或不可接受的构建时间。仅 schema-neutral 且可追溯到 EXPRESS 的根因修复可留在本文范围；
  新 public runtime concept、runtime discovery、跨 schema 包耦合或新的持久架构选择要求重新设计和批准。
- Recovery: 首个 stable release 前可移除可选候选包；稳定发布后若 baseline 错误，按 ADR-0006
  通过版本升级和弃用恢复，不得重建同一版本。
- Profile and delivery evidence: 独立版本化的 generated public API、schema/provenance 与互操作声明
  构成 Controlled 触发。固定 source/许可与生成 fidelity、package-only public contract、OCCT 语义旅程、
  offline/AOT/版本闭环分别是必要且可独立验证的交付边界，因此使用 `multi-item`。
- Escalation triggers: source/许可或 OCCT 参数证据变化；新增/改变 public runtime contract、descriptor
  identity、OID identity、生成 API 兼容策略、conformance scope、schema-package dependency；要求动态
  discovery、外部下载、CAD facade、稳定发布或其它外部操作。

- Internal redistribution gate: repository maintainer owns closure. AC-02/AC-06 必须核验检入的 schema、
  STEPcode `COPYING`/`AUTHORS`/`INTENT.md`、OCCT license/exception、逐文件 provenance 与上述 hashes，
  并在 package/fixture 文档中保留适用性结论；这不是外部法务审批。

| External operational handoff | Owner | Completion evidence | Required before change closure? |
| --- | --- | --- | --- |
| None | N/A | N/A | No |

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: PRE-01 source=../P2-ap203-schema-package/change.md contract=AC-12 -->
| ID | Required input or guarantee | Source change outcome | Required readiness evidence |
| --- | --- | --- | --- |
| PRE-01 | 仓库已有只使用检入 schema/fixture 的无网络可重现构建模式 | `../P2-ap203-schema-package/change.md`, AC-12 | 所选 Git baseline 上 source change 为 Completed，且其离线重现证明可执行 |

<!-- section: delivery-brief -->
## Delivery disposition

这是一个 multi-item Controlled change。本文获明确批准后，`plan-work-items` 创建并单独提交审批的最小
work-item map；该 map 必须为 source/provenance 与生成 fidelity、package-only 消费和重复生成边界、
OCCT 语义旅程与失败原子性、offline/AOT/版本兼容和最终集成证据分配唯一 owner。此 Draft 不授权
修改产品代码、schema、fixture、项目或包资产。

真实启动资源为固定且可重分发的 EXPRESS source、固定 OCCT exporter revision/参数、仓库自有输入
生成的 fixture，以及足够完成大型生成和 AOT 验证的构建资源。具体类型、算法、文件拆分和执行顺序
由获批 work-item map 决定。

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-02 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-03 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-04 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-05 purpose=acceptance shape=end-to-end -->
<!-- primary-proof: AC-06 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-07 purpose=acceptance shape=end-to-end -->
<!-- primary-proof: AC-08 purpose=acceptance shape=contract -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | AP214 package-only consumer compiles and executes without schema source or Analyzer runtime assets | Pack to an isolated local source, then restore, build, and run the AP214 packed-consumer mode offline |
| AC-02 | Primary | Descriptor, generated API, metadata and provenance match the pinned AP214 source and approved snapshot | Run focused descriptor-fidelity, provenance and complete public-API contract checks |
| AC-03 | Primary | Raw AP214IS fixture proves its two known edition-rule failures atomically; deterministic 2007-DIS migration then completes typed read/edit/validate/write/reread with the approved semantic signature | Run the focused AP214 journey in `TedToolkit.Step21.IntegrationTests` |
| AC-04 | Primary | Descriptor ambiguity, binding failure and invalid edit preserve the approved exception/diagnostic/atomic-output boundaries | Run the focused schema-bound negative matrix and assert pre-consumption `ArgumentException`, positioned-when-available diagnostics, aggregate validation and zero partial output |
| AC-05 | Primary | AP203 and AP214 coexist with explicit selection, one runtime and no schema-package dependency edge | Build and run an isolated two-package consumer, then inspect candidate dependency assets |
| AC-06 | Primary | Two forced offline builds are equivalent and the candidate version classification matches ADR-0006 | Compare normalized package/API/provenance outputs, then evaluate source/edition/descriptor/closed-set/runtime-range/public/semantic differences against the approved `1.0.0` input manifest and latest stable baseline when one exists |
| AC-07 | Primary | AP214 trimmed Native AOT consumer publishes and executes its fixed fixture journey without attributable warnings | Publish the AP214 packed-consumer mode in Release Native AOT and execute it |
| AC-08 | Primary | Distinct custom EXPRESS remains usable and supported AP214 templates/guidance contain no duplicate schema input | Build the isolated custom consumer and assert README/template mutual-exclusion rules for `AUTOMOTIVE_DESIGN` `.exp`/`AdditionalFiles` |

<!-- section: completion-criteria -->
## Completion

The change completes only when AC-01 through AC-08 have primary evidence on one integrated candidate; the
package has independent source/license/fixture provenance, versioning, generated API snapshot and consumer
documentation; the internal redistribution gate is closed and no external operational handoff exists; and final implementation review
confirms no unapproved runtime or cross-package coupling. Durable package README, provenance, API snapshot and
capability/conformance documentation remain after delivery; this temporary change and work-item records follow
the shared workflow cleanup policy after merge and reference release.
