# 完成 ISO 10303-21:2016 processor conformance

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: in-progress -->
<!-- delivery-shape: multi-item -->

## Status

Approved

- Priority: P1
<!-- approval-source: user-approved-authorized-iso-source-derived-profile-revision-2026-09-09 -->
<!-- candidate-binding: none -->

## Route

- Evidence: public protocol/API、外部资源、密码学信任、归档安全、跨 runtime/Analyzer/generated code 的映射以及难以回退的 conformance claim 均被改变，故最低安全 profile 为 Controlled。
- Artifacts: 本 `change.md`；批准并继续后由 `plan-work-items` 产生单独审批的最小 work-item map。
- Approval gate: 本文 contract、ADR-0009、AP-004 原则与修订后的完整 work-item map 均已获用户明确批准，实施已获授权。
- Escalation triggers: ISO edition/目标 conformance class 改变、隐式联网或机器信任、脚本引擎进入 core、AP/domain 语义、非 AOT runtime 依赖、弱化原子性或无法逐条关闭 PICS。

<!-- section: goal-rationale -->
## Goal and rationale

让 .NET 消费者能够通过一个 schema-neutral runtime 读取、验证、编辑并写出满足 ISO 10303-21:2016
Edition 3 syntactical conformance classes 1、2、3 和 schema conformance 的交换结构。完成定义以标准 Annex D
PICS 及全部适用于 processor 的规范性条款逐项有证据为准，而不是以 AP/B-rep fixture 或“语法能解析”为准。

当前 grammar 已识别完整 clear-text syntax，但 anchor/reference/signature 操作、ZIP/directory transport、
value/constant occurrence、short names 的部分映射、完整 complex mapping、部分 Annex E population 和所需
EXPRESS 约束仍返回 capability/unsupported diagnostics，因此不能声明完整 processor conformance。

<!-- section: scope -->
## Scope and non-goals

- In scope: ISO 10303-21:2016 clauses 4–14 与 normative annexes A–G 中适用于软件 processor 的读取、写出、映射、验证、安全和 PICS 能力；补齐这些映射与 schema conformance 所必需的 ISO 10303-11:2004 types、inheritance、constraints、constants、functions/procedures/rules 语义。
- In scope: conformance classes `4;1`、`4;2`、`4;3` 以及标准允许的 edition-1/2 compatibility levels；entity/value/constant occurrences；short names；全部 string encodings；anchor/tags；local/external/directory/ZIP references；CMS signatures；schema populations；完整 internal/external complex mapping；Annex F binding；UUID anchor mapping；PICS/implementation limits。
- Non-goals: AP203/AP214/AP242、B-rep、PMI、CAD/BIM 业务含义；ISO 10303-22 SDAI API、Part 28 XML、Part 14 EXPRESS-X 或任意其他分册；数据库/ORM/JSON；通用脚本执行环境；物理磁带、软盘和多卷介质管理。
- Normative disposition: 非本 .NET processor 的物理介质协议或 informative annex 必须在 PICS/traceability 中说明不适用及理由；任何适用于读取/写出的 normative processor requirement 不得以“unsupported”关闭。
- Compatibility: 既有 class-1 API、直接实体引用、mutable editing、atomic read/write、确定性 canonical output、schema-neutral dependency、诊断可定位性、包隔离和 Native AOT 均保留。新增能力默认不执行隐式网络、文件系统或机器证书访问。

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Edition 3 read/write and conformance report | 完整语法，但若干合法 standard facilities 显式 unsupported | Annex D PICS 中声明支持的 class 1/2/3 read/write 项全部工作；其余 normative 条款均有 implemented 或 justified-not-applicable evidence | 既有 class-1 成功/失败结果与原子性 |
| OB-02 | External resource and trust boundary | 无 resolver/signature trust API | 调用方显式提供资源、base URI、配额、证书/信任和时间；runtime 执行 ISO resolution/archive/CMS 语义且从不隐式 I/O | schema-neutral、deterministic、AOT-ready |
| OB-03 | EXPRESS-to-exchange mapping and schema conformance | 部分合法 mapping/validation-reachable semantics 被拒绝 | 每个 clause 11/12 mapping 和完成 schema conformance 所需约束均可静态生成、读取、验证和写回 | 无 general-purpose EXPRESS invocation API |

<!-- acceptance-case: AC-01 -->
### AC-01 — 条款与 PICS 完整闭环

```gherkin
Scenario: 每项适用的规范要求都有可执行证据
  Given ISO 10303-21:2016 的规范性 clauses、annexes 与 Annex D PICS
  When repository conformance verifier 检查 read、write、mapping、limits 和 not-applicable dispositions
  Then 零适用项处于 syntax-only、unsupported、unmapped 或无 primary proof 状态
```

<!-- acceptance-case: AC-02 -->
### AC-02 — 三个 syntactical conformance class 双向工作

```gherkin
Scenario: class 1、2、3 交换结构按声明读写
  Given 覆盖 4;1、4;2、4;3 及合法 2;1/3;1 compatibility 的标准条款 fixture
  When 消费者读取、写出并重读每个 fixture
  Then implementation_level、occurrences、short names、strings、sections 和语义均满足对应 class
```

<!-- acceptance-case: AC-03 -->
### AC-03 — Anchor、value/constant occurrence 与 UUID 完整保真

```gherkin
Scenario: 所有 anchor item、tag 与 occurrence 形式可解析和写回
  Given entity、value、constant、simple、list、null、resource、tag 和 UUID anchor combinations
  When 读取、解析引用、编辑、写出并重读
  Then identity、type、ordered values、tags、URI 和 null semantics 保持且非法重复/类型组合原子失败
```

<!-- acceptance-case: AC-04 -->
### AC-04 — 外部、目录和 ZIP reference 安全解析

```gherkin
Scenario: caller-supplied resources 按标准递归解析
  Given 相对/绝对 URI、local fragment、directory、multi-file ZIP、nested reference 和 normative reference-cycle cases
  When 以显式 base URI、resource provider 和 resource limits 读取
  Then 有效引用获得 schema-compatible entity/value，缺失或标准 reference 环按 clause 10.2 成为 null，危险 archive/resource 在发布模型前失败
```

<!-- acceptance-case: AC-05 -->
### AC-05 — CMS signature 生成、验证和信任结果明确

```gherkin
Scenario: 一个或多个 signature sections 覆盖标准定义的内容
  Given 有效、篡改、未知 signer、过期/不受信任和 malformed CMS fixtures
  When 分别在无 verifier、显式 trust/time/revocation 与 accept/reject policy、显式 signing capability 下读取或签署
  Then malformed 始终原子失败，未评估可带 `NotEvaluated` 报告完整发布，其余每种 validity/trust 状态按显式 policy 接受完整模型或拒绝且零发布，签署覆盖精确标准字节且写出原子化
```

<!-- acceptance-case: AC-06 -->
### AC-06 — Annex E schema populations 全部执行

```gherkin
Scenario: 所有标准 determination methods 产生正确 population
  Given 多 section、多 schema、跨 schema references、external populations、timestamps、signatures 和调用方显式提供的 schema-neutral domain-equivalence 等价类
  When structure 被绑定和验证
  Then section-boundary、include-all-compatible、include-referenced-instance 和 domain-equivalence 规则得到确定 population 与完整约束结果，缺失、非对称、非传递或矛盾等价声明原子失败且不依赖 Part 22
```

<!-- acceptance-case: AC-07 -->
### AC-07 — Clause 12 全映射和所需 EXPRESS 约束

```gherkin
Scenario: 每种 EXPRESS-to-Part-21 mapping 均可完成 schema-conformant round trip
  Given 覆盖所有 simple/aggregate/defined/enumeration/select types、constants、inheritance evaluated sets、internal/external complex mapping、redeclarations，以及约束可达的 function/procedure/rule 依赖与所需 algorithm statements 的 schema
  When Analyzer 生成类型并由 runtime 读取、验证、写出和重读实例
  Then 值、identity、physical attribute order、constraints 和 mapping selection 与标准一致，每个 Part-11 可达语义家族都在 corpus 中闭环且没有 function、procedure、rule 或 statement 被跳过
```

<!-- acceptance-case: AC-08 -->
### AC-08 — Annex F binding 与核心隔离

```gherkin
Scenario: ECMAScript binding 映射所有 anchor values 和 P21 operations
  Given 一个包含标准 anchor/value/population forms 的 exchange structure
  When 可选 Annex F adapter 投影、修改并写回 P21 model
  Then 标准定义的 valueOf、toString、toP21String 和 model operations 行为成立且核心读取/AOT 不依赖脚本引擎
```

<!-- acceptance-case: AC-09 -->
### AC-09 — 安全、资源和原子边界不因完整能力退化

```gherkin
Scenario: 不受信任的 distributed exchange structure 被有界处理
  Given path traversal、ZIP bomb、archive/resource-provider 递归或重入、超限数量/大小/深度、恶意 URI 和无效 signature inputs
  When 通过 public read/write APIs 处理
  Then 这些非 clause-10.2 reference-cycle 的威胁均按显式配额和 caller-owned policy 原子失败，库不隐式访问外部状态且不发布部分模型或部分输出
```

## Constraints and risks

- Governing records: product intent、AP-001/AP-002/AP-003/AP-004、EP-001 至 EP-004、schema-bound architecture、ADR-0003/0005/0007/0008，以及已批准的 ADR-0009。
- Normative authority: ISO 10303-21:2016 Edition 3 及其 normative references；ISO 10303-11:2004 仅在 Part 21 mapping/schema conformance 所需范围内，不扩张为通用 interpreter 产品。
- Security and determinism: 遵循 ADR-0009；网络、文件、证书、时间和撤销状态必须显式注入并可测试。archive 必须有解压前后配额、entry/path 验证和循环边界。
- Compatibility: 新增 public API 与 packages 需要完整 API snapshot/SemVer 分类；不能通过更改 approved outputs 掩盖既有回归。
- Performance: 以 committed baseline `63b2757` 在同一 Release runtime 上读取并写出固定普通 class-1 fixture 为基线；预热后 20 次中位线线程分配不得增加超过 `max(5%, 16 KiB)`，且跟踪 factory 证明未使用时零 resolver/archive/CMS/Annex-F 对象创建；具体 fixture 和命令由 work-item map 绑定。
- Recovery: 每个能力保持独立 feature boundary；若某 tranche 失败，保留已验证的前一能力并继续对未交付项报告准确 capability evidence，不伪造 full conformance。

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from committed baseline `63b2757`; Active AP-004 and the approved parent contract are present. A user-supplied ISO 10303-11:2004 source that the user is entitled to authorize for AI use remains an external start condition only for the Part 11 semantic-closure item. The implementation may derive the non-verbatim clause/semantic/test profile from that source, subject to independent coverage review.

<!-- section: delivery-brief -->
## Delivery disposition

这是 multi-item Controlled change。PICS/traceability baseline、class-2 distributed resources、class-3
occurrence/signature/Annex-F、完整 clause-12/EXPRESS mapping 与 integrated security/AOT proof 是可独立验证且存在
真实依赖的交付边界。批准并继续后，`plan-work-items` 创建最小 map；不得将 AP fixture 或文档勾选代替
normative unit/contract evidence。

- Resource prerequisites: 可合法用于实现核对的 ISO 10303-21/11 文本或获授权摘录；RFC 2396/3986、4122、4648、5652、PKZIP 2.04g/ISO 21320-1；本地确定性证书与 archive fixtures；不依赖在线测试服务。
- Likely touchpoints (non-binding): conformance/PICS records、Part21 syntax/raw model、ExchangeStructure public options、reader/writer、generated descriptor/mapping、EXPRESS binder/emitter、optional Annex F adapter、unit/contract/integration/AOT tests。
- Private choices left open: 类型名、文件组织、resource cache、CMS provider、archive reader、adapter representation 和测试分组，只要满足公共、安全、AOT 与条款合同。

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-02 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-03 purpose=acceptance shape=component -->
<!-- primary-proof: AC-04 purpose=boundary shape=integration -->
<!-- primary-proof: AC-05 purpose=boundary shape=integration -->
<!-- primary-proof: AC-06 purpose=acceptance shape=component -->
<!-- primary-proof: AC-07 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-08 purpose=boundary shape=integration -->
<!-- primary-proof: AC-09 purpose=boundary shape=integration -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | PICS/traceability verifier reports zero applicable unsupported/unproven row | Run repository ISO conformance verifier and clause-manifest contract tests |
| AC-02 | Primary | Class 1/2/3 plus compatible 2;1/3;1 positive/neighbor-invalid matrix reads and writes exactly as declared | Run Part21 conformance-class contract suite |
| AC-03 | Primary | Every anchor/item/tag/occurrence/UUID partition round-trips and invalid combinations fail atomically | Run anchor and occurrence component suites |
| AC-04 | Primary | Local/external/directory/ZIP references resolve with correct identity/null/cycle/schema behavior and resource controls | Run isolated in-memory resource-provider integration suite |
| AC-05 | Primary | Multiple CMS signatures distinguish malformed/not-evaluated/invalid/untrusted/trusted results, apply the explicit publication matrix, and sign exact covered bytes atomically | Run deterministic certificate/CMS integration suite |
| AC-06 | Primary | Every Annex E determination method computes the expected ordered population and validates caller-supplied equivalence as an equivalence relation | Run multi-schema population component suite |
| AC-07 | Primary | Complete clause-12 schema corpus generates and performs semantic read-write-read without skipped reachable constraints, function/procedure/rule dependencies, or required algorithm statements | Run compiler conformance corpus and generated-mapping contract suite |
| AC-08 | Primary | Annex F adapter satisfies every mapped value/model operation without entering the core dependency graph | Run adapter integration suite and package dependency audit |
| AC-09 | Primary | Quota, traversal, URI, recursion, signature and atomicity attacks fail deterministically with zero partial publication/output | Run security boundary integration suite |
| Existing compatibility | Conditional | Current runtime/generated public APIs, AP203/AP214/AP242 packages and class-1 semantic journeys remain green | Run full Release tests, public/generated baselines, package proofs and Native AOT journeys |
| Class-1 feature isolation | Conditional | Fixed class-1 read/write allocation remains within `max(5%, 16 KiB)` of `63b2757` and unused Edition-3 services create zero feature-specific objects | Run the pinned Release allocation scenario for baseline and candidate plus tracking-factory isolation tests |

<!-- section: completion-criteria -->
## Completion

完成需要 AC-01 至 AC-09 在同一 candidate binding 上全部通过；AP-004 为 Active；Annex D PICS 和逐条 normative traceability
没有适用的 syntax-only/unsupported/unproven 项；ADR-0009 为 Accepted；所有新增 public/package surface 有
API snapshot、SemVer 和 migration 文档；现有 class-1、三个 AP 包、确定性、atomicity 和 Native AOT 证明保持
绿色；安全与实现评审均 Ready；无外部部署、证书或网络 handoff。临时 change/work-item records 在 merge 与
reference release 后依共享 lifecycle 清理，持久 conformance/PICS、architecture、ADR 和消费者文档保留。
