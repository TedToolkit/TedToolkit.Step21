# ADR-0009：显式控制 ISO 10303-21 外部资源与信任边界

- Status: Accepted
- Date: 2026-09-06
- Owner: repository maintainer
- Governing scope: ISO 10303-21 Edition 3 anchor/reference、压缩归档、signature、schema population 与 ECMAScript binding 所需的资源、信任和依赖方向
- Approval source: user explicit approval, 2026-09-06
- Superseded in part by: [ADR-0012](ADR-0012-exclude-ecmascript-binding.md), which replaces only the Annex F language-binding decision

## Decision at a glance

TedToolkit.Step21 负责 ISO 10303-21 定义的 URI 解析语义、引用循环/空值规则、归档结构校验、CMS
signature 输入与结果、schema-population 验证和确定性诊断；调用方通过显式、每次读取或写入所绑定的
能力对象提供资源字节、基 URI、证书/信任、时间以及 schema-neutral SDAI-domain-equivalence
关系。核心库不得根据 schema 名称或结构推断 domain equivalence，也不得隐式访问网络、当前目录、机器证书库或
环境代理，也不得把 resolver、代理或 I/O 状态存入生成实体。

Annex F 的 ECMAScript binding 作为依赖核心标准值与 anchor 模型的可选语言绑定交付；核心 runtime
不嵌入脚本引擎，不执行任意脚本，语言绑定不得反向成为 Part 21 读取、验证或写出的依赖。

## Context and decision drivers

ISO 10303-21:2016 的 conformance classes 2/3 要求处理 reference、多文件 ZIP、value instances、EXPRESS
constants 和可选 ECMAScript binding；signature 与 schema population 又依赖 CMS、时间、证书和外部资源。
当前 runtime 对这些合法语法返回 capability failure。补齐行为会首次引入外部 I/O 和信任决策。

适用原则：AP-001/AP-003 要求公共语义可追溯到 ISO，AP-002 要求 runtime 保持 schema-neutral，EP-002
禁止隐藏 identity/ownership，EP-003 要求 Native AOT，EP-004 要求条款级证据与显式失败。

Hard constraints:

- 支持标准语义不能等价于默认联网、默认文件系统访问或信任本机证书库。
- 同一输入字节、显式资源集合、信任策略和时间必须产生确定结果；失败不得发布部分结构或部分输出。
- 外部 entity/value 最终必须参与与本地值相同的 schema/type/reference/validation 规则。
- runtime 不依赖具体 schema、Analyzer、脚本引擎或运行时反射发现；生成实体保持直接引用和可移植性。
- URI/ZIP/CMS 处理必须具备大小、深度、数量、循环、路径穿越和解压膨胀边界。

## Considered alternatives

| Alternative | Disposition | Decision evidence and trade-off |
| --- | --- | --- |
| 保持 capability failure | Rejected | 不满足完整 Edition 3 processor/PICS 目标 |
| runtime 自动访问 HTTP、文件系统和机器证书库 | Rejected | 结果依赖机器/网络，扩大 SSRF、路径访问和隐式信任面，破坏确定性与测试隔离 |
| 每个生成实体保存 lazy resolver/proxy | Rejected | 改变 EXPRESS 直接引用、identity 和模型可移植性，并反转 schema-neutral 依赖 |
| 调用方显式提供资源与信任能力，runtime 执行标准状态机 | Selected | 保留 ISO 语义和可测试性；调用方需显式组织 I/O、证书和时间 |
| 核心 runtime 嵌入 ECMAScript 引擎 | Rejected | 标准映射不要求任意脚本执行；会扩大依赖、攻击面、动态代码和 AOT 风险 |
| 可选 Annex F language binding 单向依赖核心模型 | Selected | 能交付标准绑定且不污染所有 .NET 消费者的核心运行路径 |

## Consequences and delivery constraints

- 读取/写出必须增加显式 options/capabilities 边界；省略能力时，只有确实需要外部资源或信任的输入才
  产生稳定 capability diagnostic，既有本地 class-1 旅程保持兼容。
- resolver 返回不透明字节与已解析媒体身份；runtime 独占 URI 相对解析、archive root/subsidiary 选择、
  anchor 链与 `$` 结果语义。标准 reference 图的环按 clause 10.2 解析为 null 并产生可定位诊断；
  archive 嵌套、resource-provider 重入或超过配额的递归不属于该语义，必须原子失败。禁止 resolver
  注入已绑定实体来绕过 schema validation。
- domain-equivalence provider 提供稳定 domain identity 及其等价类；runtime 先验证自反、对称、传递和无矛盾，
  再执行 Annex E determination。仅在输入实际选用该方法时要求此能力；缺失或非法声明原子失败，
  且不得通过依赖 ISO 10303-22 对象或名称/形状启发式补齐。
- signature API 独立报告 `Malformed`、`NotEvaluated`、`CryptographicallyInvalid`、
  `CryptographicallyValid` 与 trust/time/revocation 结果。语法或 CMS 结构 malformed 始终使读取原子失败；
  缺少 verifier 时允许完整读取并报告 `NotEvaluated`；已评估后的密码学无效、未知 signer、过期、
  撤销或不受信任状态由每次调用的显式 acceptance policy 决定接受还是拒绝。接受时只发布完整模型
  及完整报告；拒绝时不发布模型。写出 signature 必须显式提供 signing capability，否则在任何
  目标字节可观察前失败。
- archive 输入必须在解压前后执行配额与路径安全检查；不得将 entry 自动写入磁盘。
- Annex F adapter 必须可被不需要它的消费者完全排除，并保留核心 package 的 Native AOT 证明。
- 新公共能力遵循 SemVer；不删除既有 overload/diagnostic，除非独立迁移获得批准。

## Evidence and review triggers

Normative basis is ISO 10303-21:2016 clauses 4.3, 6.5, 8.2.5, 9, 10, 14 and normative annexes A.4/A.5,
D, E, F and G, plus their cited URI/UUID/Base64/CMS specifications. Repository clause/PICS tests must bind each
claim to focused positive, neighboring-invalid, cycle, quota, trust and atomicity evidence.

Reassess this decision if ISO 10303-21 is superseded, a target platform cannot provide explicit CMS/URI/archive
primitives with AOT support, Annex F cannot be delivered without a script engine, or a required standard operation
cannot be expressed without storing resolver/trust state in domain entities.
