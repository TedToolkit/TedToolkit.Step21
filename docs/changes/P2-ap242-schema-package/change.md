# 提供开箱即用的 AP242 schema 包

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

.NET 消费者可以只引用 `TedToolkit.Step21.Ap242`，无需自行管理 `.exp` 或
`AdditionalFiles`，即可通过现有 `ExchangeStructure` API 读取、编辑、验证、写出并重读固定
OCCT AP242DIS 互操作样本。

AP203 已证明预编译 schema 包的消费者价值与可执行交付模式；当前 AP242 仍只能由消费者自行取得并
编译 EXPRESS。独立 AP242 包补齐这一常用 application protocol 的开箱即用能力，同时保持核心
runtime schema-neutral，并让 AP242 的 baseline、public surface、版本和发布节奏独立演进。

<!-- section: scope -->
## Scope and non-goals

- In scope:
  - 新增独立程序集/NuGet 包 `TedToolkit.Step21.Ap242`，维护一份固定的
    `AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF` EXPRESS baseline。
  - 记录 schema 的 source identity、edition/variant、完整性与重分发许可证据，并由该固定源生成
    descriptor、public schema types、API snapshot、包元数据和消费者 README。
  - 通过固定 OCCT AP242DIS fixture 证明 package-only 的 typed
    read/edit/validate/write/reread 旅程、失败原子性、离线重现和 trimmed Native AOT 可用性。
  - 复用现有 generator、descriptor、`ExchangeStructure.Read/Write`、验证和诊断边界；仅允许修复
    AP242 暴露且可追溯到 EXPRESS 的 schema-neutral 根因。
- Non-goals:
  - 不把 AP242 加入核心包，不创建 application-protocol facade、runtime registry、程序集扫描、
    自动下载、隐式 descriptor 选择、CAD-kernel/OCCT 对象转换或第二套 reader/writer。
  - 不同时维护多个 AP242 edition 或 vendor variant；替换 baseline 按 ADR-0006 作为显式兼容性变更。
  - 不宣称完整 AP242 标准 conformance。首版互操作声明仅覆盖固定 fixture 中的 manifold B-rep、
    product、topology、geometry、unit、值、实例身份和引用；PMI、tessellation、kinematics、
    colour/layer 及其它未进入 fixture 的能力均不在声明范围内。
  - 不包含 NuGet.org 发布或外部法务审批。
- Compatibility or deliberately preserved behavior:
  - 调用者继续显式把 `SchemaDescriptor` 传给 `ExchangeStructure.Read/Write`；核心 runtime 不依赖
    AP242 包，现有自定义 `.exp` + Analyzer 工作流继续可用。
  - `SchemaDescriptor.Instance.Name` 保持 EXPRESS nominal name，原始 `FILE_SCHEMA` 值继续保留。
    OID 不作为 package baseline identity；同 nominal name 的其它 edition 不会仅凭 header 被识别或
    拒绝，只有使用 baseline 外结构时才走现有显式 schema/binding 失败路径。
  - AP242 包独立遵循 ADR-0006 的 SemVer、发布节奏和有界 runtime 依赖策略；README 和消费者模板
    必须把相同预编译 schema 的 `.exp`/`AdditionalFiles` 声明为互斥输入，不把重复生成包装成受支持用法。

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | AP242 package consumption | 消费者必须自行取得并编译 AP242 EXPRESS | 单独引用 `TedToolkit.Step21.Ap242` 即可访问唯一 descriptor 和生成类型 | 仍显式选择 descriptor 并使用同一核心 read/write model |
| OB-02 | 固定 schema contract | 没有受维护的 AP242 package baseline | 包、descriptor、public API 与固定 N11521/ISO/TS 10303-442 long form 一致 | 不以 OID 或未验证 edition 扩大身份声明 |
| OB-03 | 真实协议旅程 | 仓库没有 AP242 schema-bound round-trip 证据 | 固定 OCCT AP242DIS 样本完成原子读取、typed navigation、编辑、验证、写出和语义重读 | 失败不发布部分模型，写失败不输出部分字节 |
| OB-04 | 部署与生成边界 | 通用 Analyzer 路径尚无预编译 AP242 包共存边界 | package-only/AOT 消费者可用，README/模板明确预防重复 AP242 输入 | distinct custom schema 生成与 schema-neutral runtime 保持可用 |

<!-- acceptance-case: AC-01 -->
### AC-01 — AP242 独立包可直接消费

```gherkin
Scenario: 仅引用 AP242 预编译包
  Given 一个干净的 .NET 10 消费者只引用候选 TedToolkit.Step21.Ap242 包
  When 它编译并访问 AP242 descriptor 与代表性生成类型
  Then 无需 .exp 或 AdditionalFiles 即可成功，且运行时资产不包含 Analyzer-only 依赖
```

<!-- acceptance-case: AC-02 -->
### AC-02 — 固定 AP242 baseline 与生成表面忠实一致

```gherkin
Scenario: 检查候选包的 schema 身份和 public contract
  Given 已检入且记录来源、校验值和许可的固定 AP242 EXPRESS baseline
  When 比较 descriptor、代表性继承可空性聚合成员顺序和完整 public API snapshot
  Then 包只声明 AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF，且生成表面与固定源一致
```

<!-- acceptance-case: AC-03 -->
### AC-03 — 固定 OCCT AP242DIS 样本完成语义 round trip

```gherkin
Scenario: 处理固定 OCCT AP242DIS 样本
  Given 固定样本声明 AP242 MIM LF canonical OID、一个 10 × 20 × 30 mm 原点 box、1 个 product、6 个 face、12 个 edge 和 8 个 vertex
  When 用 AP242 descriptor 读取，将 product name 改为 `TedToolkit AP242 OCCT box 10x20x30 mm - edited`，验证、写出并重读
  Then product name、box extents、millimetre/radian/steradian units、实体种类与数量、实例身份和共享引用关系保持该固定语义签名
```

<!-- acceptance-case: AC-04 -->
### AC-04 — 错误 schema、baseline 外结构和无效编辑原子失败

```gherkin
Scenario: 输入不属于所选固定 baseline
  Given descriptor 集合含重复 nominal name、文件声明不同 schema 或含 baseline 外实体、或编辑后违反可达 schema 规则
  When 调用读取、验证或写出
  Then 配置歧义在消费输入前抛出无位置要求的 ArgumentException，其它失败产生稳定 schema/binding 或聚合验证证据且仅在存在源位置时携带位置，并且不发布部分模型或写出部分字节
```

<!-- acceptance-case: AC-05 -->
### AC-05 — AP242 与既有 schema 包共存且保持隔离

```gherkin
Scenario: 一个消费者同时引用 AP203 和 AP242
  Given 两个独立 schema 包及兼容的 TedToolkit.Step21 runtime
  When 消费者显式选择各自 descriptor 并检查编译与运行时依赖图
  Then 每个 fixture 绑定到正确 schema，只有一份 runtime，且 schema 包之间没有依赖或扫描
```

<!-- acceptance-case: AC-06 -->
### AC-06 — 包、来源和生成表面可离线重现并按策略版本化

```gherkin
Scenario: 从固定输入重复构建候选包
  Given schema 与 fixture 已检入且元数据声明 source identity、descriptor、runtime range 和兼容策略
  When 在无网络的干净环境执行两次非增量构建并比较包及 public API
  Then 规范化产物等价、元数据与 provenance 一致，且不兼容变化不能被视为兼容更新
```

<!-- acceptance-case: AC-07 -->
### AC-07 — 独立消费者保持 Native AOT 可用

```gherkin
Scenario: 发布 AP242 trimmed Native AOT 消费者
  Given 消费者只引用候选 AP242 schema 包
  When 以 Release Native AOT 发布并执行固定样本旅程
  Then 旅程成功且没有候选包或生成路径导致的 trim/AOT 警告
```

<!-- acceptance-case: AC-08 -->
### AC-08 — 包输入明确预防重复 AP242 surface

```gherkin
Scenario: 检查预编译 AP242 schema 的受支持 build inputs
  Given AP242 package README 与受支持消费者模板
  When 检查其 documented build inputs
  Then README 明确禁止为 AP242 MIM LF 再配置 .exp/AdditionalFiles，且模板不包含该重复输入
```

## Constraints and risks

- Governing records:
  - Design baseline: `30b13187cd5df71eb99d5cba6a8cb65111ce28f9`。
  - `docs/product/README.md@5e5c5187ffc0857101df9f4fad67ef7bfbfff231`。
  - `docs/principles/architecture.md@ad80acfc31b8557991842e326dcee81ad7792bf9`：AP-001、AP-002、AP-003。
  - `docs/principles/engineering.md@9a28794f46ca41c3489bb402a86ddf3dd3f5a2b8`：EP-002、EP-003、EP-004。
  - `docs/adr/ADR-0006-precompiled-schema-package-distribution.md@3eb14b4d8657f77da8f686d3f2ee0c25fbaa3179`。
  - `docs/architecture/schema-bound-round-trip.md@94728d649d63b85633bd375b296b87f808609377`。
- Fixed source: STEPcode commit `9baa5dadaa1dcfcdc623220d865d36d61ea351e9`,
  [`data/ap242/242_mim_lf.exp`](https://github.com/stepcode/stepcode/blob/9baa5dadaa1dcfcdc623220d865d36d61ea351e9/data/ap242/242_mim_lf.exp)。上游 byte 与仓库
  canonical-LF SHA-256 均为
  `E7E93CF97880FD87D634E4B9EE58400DA0A1BE6C06A1DE76EC13807ECDC15CCB`。源头身份是
  `ISO TC184/SC4/WG12 N11521`、`ISO/TS 10303-442` AP242 managed model based 3d
  engineering EXPRESS MIM long form，supersedes `N11273`；nominal schema 为
  `AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF`。
- Redistribution evidence: 该 STEPcode revision 把 collective work 置于 BSD-3-Clause；包须保留
  `COPYING`（canonical-LF SHA-256
  `C787486F3E1358CF1CB4456B56E00862DE9C0433E7D49F5501D1289FF8BEF37E`）、`AUTHORS`
  （`619EE3D3D9CE6DB690B4A20F36AB30616CB8F1FB8616FAEB85D9685AFDFD15FB`）、
  `INTENT.md`（`B10C7DCC9C269B383C944ACC139F787CCA050D497142CA03ED90C49EF41CE23F`）
  和逐文件 provenance。若后续审计不能证明该 schema 在此重分发授权内，则停止交付，不得静默换源。
- Fixture contract: 固定到 `cadquery-ocp` `7.9.3.1.1`（OCCT `7.9.3.1`）CPython 3.10 Windows wheel
  `B52931A6786F9A1949BCAC7EF49C8A83426C4198D6847CD13A8CC40795207E09` 和
  `BRepPrimAPI_MakeBox(10.0, 20.0, 30.0)` 在原点创建的仓库自有 box；使用 `STEPControl_Writer`、
  `STEPControl_ManifoldSolidBrep`、millimetres 与 AP242DIS schema mode，关闭
  surface curves、color、name、layer、properties、metadata 和 material。输出必须声明
  `AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF { 1 0 10303 442 1 1 4 }`；只允许规范化
  `FILE_NAME` 的 name、timestamp、author、organization、preprocessor、originating system、authorization，
  以及 exporter 生成的 `PERSON`、`ORGANIZATION`、`LOCAL_TIME` 和 UTC-offset 值，并记录
  generator/fixture SHA-256。许可固定为该 revision
  的 LGPL-2.1 + OCCT exception；`LICENSE_LGPL_21.txt` SHA-256 为
  `E237FA56668030E928551DDD60F05DF5FE957F75EAB874BBD017E085ED722E7C`，
  `OCCT_LGPL_EXCEPTION.txt` SHA-256 为
  `04580A884EA6CEA294402649FF7B5CBB167D47462D1340A4ED33E550DB10A81B`。改变 revision、参数、
  fixture 能力边界或 edition/variant 必须重新批准。该固定稳定发行版替代原设计中的 OCCT
  `8.1.0-dev1` source build；用户在获知构建成本与固定 wheel 方案后明确要求继续实施。
- Package contract: 首版 TFM 为 `net10.0`；候选验证使用 `1.0.0-*` prerelease，只有本 change
  完成后才形成首个 stable `1.0.0`；`TedToolkit.Step21` runtime 依赖范围固定为
  `[1.0.0,2.0.0)`。
- OID 不属于 package baseline identity。本 change 不增加 OID-to-baseline registry，也不宣称仅凭
  共享 nominal name 能区分 edition；支持范围由固定 source、package version、provenance 与 fixture
  共同界定。
- Analyzer boundary: 本文选择 ADR-0006 允许的 package-guidance prevention，不新增 package-aware
  Analyzer diagnostic、runtime discovery 或构建失败保证。未来若要求自动拒绝同 surface 输入，必须
  另行设计 schema-neutral 诊断合同。
- 大型 AP242 schema 可能暴露 compiler semantic gap、C# naming collision、生成 API/程序集/AOT
  体积或构建时长问题。仅 schema-neutral 且可追溯到 EXPRESS 的根因修复留在本 change 内；新的
  runtime public concept、discovery、跨包耦合或 architecture decision 需要重新设计与批准。
- Recovery: 首个 stable release 前可移除可选候选包；发布后错误 baseline 必须按 ADR-0006 通过
  新版本和 deprecation 纠正，不得以相同版本替换内容。
- Escalation triggers: source/fixture/license identity 改变、互操作声明扩大到 PMI/tessellation/
  kinematics 等未覆盖能力、public runtime/schema identity 新概念、runtime dependency range 收窄、
  package coupling、非确定性或不能满足 Native AOT 的执行路径。

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

这是一个 multi-item Controlled change：固定 source/provenance 与生成 public package contract、真实
AP242DIS runtime journey，以及 package/reproducibility/AOT release evidence 是至少两个必要且可独立
验证的交付边界。change 获得明确批准并继续后，`plan-work-items` 才创建单独审批的最小 work-item
map，并为 schema-neutral compiler 修复、最终 package surface、fixture journey 和集成证据指定唯一
owner。本 Draft 不授权产品代码、schema、fixture、项目或包资产变更。

- Other real start conditions or resource prerequisites: 已固定且可重分发的 EXPRESS source、固定
  OCCT generator revision/参数、仓库自有 fixture 输入，以及足够完成大型生成与 AOT 验证的构建资源。
- Likely touchpoints (non-binding): `schemas/ap242/`、`src/TedToolkit.Step21.Ap242/`、现有 generator
  与 schema-neutral runtime、集成/packed-consumer 测试、package metadata、API snapshot、README 与
  conformance 文档。
- Private implementation choices left open: 具体文件、类型、算法、work-item 数量与顺序，以及不改变
  本契约的 schema-neutral compiler 修复组织方式。

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
| AC-01 | Primary | package-only consumer 编译执行成功且无 schema source/Analyzer runtime asset | Pack 到隔离本地源，offline restore/build/run AP242 packed-consumer mode |
| AC-02 | Primary | descriptor、生成 API、source identity、hash 与批准 snapshot 一致 | 运行 AP242 descriptor-fidelity、provenance 和 public-API contract tests |
| AC-03 | Primary | 固定 AP242DIS fixture 完成 typed read/edit/validate/write/reread 并保留语义签名 | 在 `TedToolkit.Step21.IntegrationTests` 运行 focused AP242 journey |
| AC-04 | Primary | Descriptor 配置冲突、文件绑定失败和无效编辑分别保留既有异常、诊断与零输出边界 | 运行 focused negative matrix，断言 pre-consumption `ArgumentException`、positioned-when-available diagnostics、聚合验证和零部分输出 |
| AC-05 | Primary | AP203/AP242 显式选择正确、仅一份 runtime 且无 schema-package 依赖边 | 构建并运行隔离双包消费者，再审计候选 package dependency assets |
| AC-06 | Primary | 两次强制 offline build 等价且候选版本分类符合 ADR-0006 | 比较规范化 package/API/provenance，并依据批准的 `1.0.0` input manifest 与存在时的最新 stable baseline 分类 source/edition/descriptor/closed-set/runtime-range/public/semantic 差异 |
| AC-07 | Primary | AP242 trimmed Native AOT publish 无可归因警告并执行完整固定旅程 | Release Native AOT publish/run AP242 packed-consumer mode |
| AC-08 | Primary | 受支持 AP242 模板与指南明确排除重复 schema 输入 | 断言 README/template 对 AP242 MIM LF `.exp`/`AdditionalFiles` 的互斥规则 |
| AC-08 | Conditional | distinct custom EXPRESS 的 Analyzer 消费路径保持成功 | 构建并运行现有隔离 custom-schema consumer regression |

<!-- section: completion-criteria -->
## Completion

本 change 仅在所有 AC-01 至 AC-08 于同一集成候选上获得充分 primary evidence、AP242 source/
fixture/license provenance 与 generated API/version/conformance 文档持久保留、候选包从
`1.0.0-*` 满足 stable `1.0.0` 门槛、internal redistribution gate 关闭且不存在外部 operational handoff，
并由最终实现评审确认
没有未批准 runtime 或跨包耦合后完成。临时 change/work-item 记录在 merge 与 reference release 后按
共享 workflow lifecycle 清理，不建立 completed-change archive。

## Completion evidence

All AC-01 through AC-08 passed on stable `TedToolkit.Step21.Ap242` `1.0.0`:

- AC-01/AC-02/AC-08: package-only consumption, fixed source/provenance/descriptor, complete public API snapshot
  (`7402DBECCCF19940E0ADCDE0494DD6FAF488A875DBB9AE1E25C81C2C59B09B21`), mutual-exclusion guidance, and the
  19/19 baseline contract suite passed.
- AC-03/AC-04: the fixed 170-entity AP242DIS fixture completed typed edit/validate/write/reread; invalid edit returned
  eight failures with zero output bytes and the unsupported entity returned positioned `P21-BIND-ENTITY` evidence.
- AC-05: the isolated AP203/AP242 consumer passed with one runtime and no schema-package dependency edge.
- AC-06: two clean non-incremental builds produced normalized package SHA-256
  `81B8B858B86E81416BF43C514CBFA7C85C35B9708EEAA613779372A58F493907` and fixed-input SHA-256
  `35833AEDFAFB765094E48B0F76F69357C1B2D347949EAD46A6BAAD3457ABDAF7`.
- AC-07: `win-x64` Native AOT passed the same journey without attributable warnings; compiler package `10.0.11`,
  executable 138,115,072 bytes.

The final implementation review found no unapproved runtime/discovery surface or cross-package coupling. Shared
SELECT payload storage and schema-level `TYPEOF` dispatch preserved every alternative and the approved public API
while reducing AP242 generated source by 13.69% and the schema assembly by 26.8% from the pre-optimization candidate.
