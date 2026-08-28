# ADR-0008：以协变聚合视图保持 EXPRESS 收窄重声明与 singular inverse 语义

- Status: Accepted
- Date: 2026-08-27
- Decision owner: repository maintainer
- Decision scope: 在 TedToolkit.Step21 支持期内，EXPRESS 聚合属性收窄重声明的 generated public contract、物理 slot identity，以及 validation-reachable singular inverse 的求值和失败边界
- Applicable product intent: [`../product/README.md`](../product/README.md)
- Applicable principles: [`AP-001`、`AP-002`、`AP-003`](../principles/architecture.md)；[`EP-002`、`EP-003`、`EP-004`](../principles/engineering.md)
- Supersedes: None
- Superseded by: None

## 📌 决策摘要

用由同一可变聚合实例实现的、保留 EXPRESS 元数据的协变只读聚合视图表达收窄重声明；singular inverse 保持为模型上下文中的计算语义，只在唯一候选时求值，否则通过聚合验证失败报告基数违例。

## 🧭 背景与决策问题

当前 generated entity interface 直接返回可变的 `ExpressList<T>`、`ExpressSet<T>`、
`ExpressBag<T>` 或 `ExpressArray<T>`。这些类型保留 EXPRESS kind、bounds、order、multiplicity、
optional slot 和 `UNIQUE` 候选状态，但 CLR 可变泛型不协变。当合法 EXPRESS subtype 把一个继承
物理 slot 从 `LIST OF supertype` 收窄重声明为 `LIST OF subtype` 时，同一 slot 必须同时满足两个
entity interface；当前 mapping 只能拒绝生成。

复制聚合、在 getter 中逐次转换或维护两份同步集合虽然能让 C# 类型检查通过，却会让两个
EXPRESS attribute view 不再指向同一物理 slot，或者让 mutation、validation 与 writer 观察到
不同状态。把收窄声明静默提升回基类型则丢失 schema 类型约束。

EXPRESS inverse attribute 由模型中指向当前实例的 forward role 计算，不是 Part 21 中的序列化
slot。singular inverse 把该反向关系约束为一个 entity 值；可变候选图仍可能暂时没有匹配或出现
多个匹配。当前 generator 不应把这种状态选择为 first-wins、抛出非聚合导航异常，或改成一个
公开的可变集合。本 ADR 决定 CLR aggregate view 与 singular inverse validation 如何同时保持
schema fidelity、单一物理 identity、可编辑候选状态和原子验证边界。

## 🎯 决策驱动与约束

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | 收窄重声明仍表示一个继承物理 slot；所有 generated interface view 必须观察同一实例和同一 mutation | [`schema-bound-round-trip.md`](../architecture/schema-bound-round-trip.md) 的 generated type、storage 与 redeclaration 边界 | Must |
| Hard constraint | 聚合 mapping 保留 EXPRESS kind、bounds、order、multiplicity、optional slot 与 `UNIQUE` 语义 | [`generated-schema-aggregates.md`](../conformance/generated-schema-aggregates.md) 和 AP-003 | Must |
| Hard constraint | 现有成功生成的非收窄 schema public surface 不因支持新 construct 而被全局重写 | 当前 generated API snapshot/compatibility 边界与 ADR-0006 | Must |
| Hard constraint | inverse 不成为序列化属性；缺失或多个 singular candidate 不被猜测、截断或在验证外以 first-wins 发布 | EP-002、EP-004 和当前聚合验证边界 | Must |
| Hard constraint | runtime 保持 schema-neutral；generated 路径静态可达、Native AOT-ready，且无 reflection discovery | AP-002、EP-003、ADR-0003 | Must |
| Decision driver | 公开接口仍可自然导航，而具体 entity class 保留批准的可变编辑模型 | 当前 interface/class generated type architecture | High |
| Decision driver | 新增的 runtime contract 只表达 EXPRESS aggregate observation，不引入 application-protocol domain 概念 | AP-001、AP-003 | High |
| Decision driver | 同一候选状态产生稳定、可聚合、可追溯的 inverse cardinality evidence | ADR-0003 validation result contract | High |

## 🔎 方案与证据

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| 维持 invariant mutable aggregate，并继续拒绝收窄重声明 | 当前实现和 [`generated-entity-hierarchy.md`](../conformance/generated-entity-hierarchy.md) 已明确该诊断边界；高置信 | No | 不改变 API，但合法 EXPRESS schema 仍无法生成 | Rejected |
| 为每个 interface getter 复制或转换一个 mutable aggregate | CLR 类型可通过；语义分析为高置信 | No | 两个对象不能保持一个 physical slot 的 identity、mutation 和 validation 状态 | Rejected |
| 只使用标准 `IReadOnlyCollection<T>` / `IReadOnlyList<T>` | .NET covariance documented；高置信 | Partial | 可协变但不能统一保留 EXPRESS kind、bounds、`UNIQUE` 与 ARRAY index metadata | Rejected |
| kind-specific covariant read-only EXPRESS views，由现有 mutable aggregates 直接实现 | CLR covariance 与现有 aggregate ownership 可组合；高置信，交付仍需 public API/AOT proof | Yes | 新增少量 public runtime infrastructure；收窄 slot 的 generated interface 只读，但具体 class 仍可变 | Selected |
| 非泛型共享存储加多个 typed mutable views | 语义上可行；中等置信 | Yes | 增加最多 public abstraction、同步/类型检查规则和维护面，超出当前需要 | Rejected |

singular inverse 的 status quo 是对 validation-reachable 非 aggregate inverse 报 unsupported。
把它映射为公开集合会改变 schema cardinality；把多个候选截断为一个或在 getter 中抛出异常会绕过
已有 aggregate validation contract。选择在 generated validation 内保留完整候选集合，仅在候选数
为一时产生 singular value，并把其它基数作为稳定验证失败。

## ✅ 决策

runtime 增加按 EXPRESS aggregate kind 区分的协变只读 view contract。view 只暴露观察操作以及
kind-specific 的不可变 schema metadata；任何会接受 `T`、改变 cardinality、替换 element 或暴露
mutable comparer 的成员都不进入协变 contract。direct entity element 收窄时，现有 mutable aggregate
concrete types 直接实现对应 view；generated SELECT value-wrapper 无法使用 CLR covariance 时，使用
schema-neutral、惰性逐元素 adapter。adapter 不复制 element 或 storage，也不提供第二个 mutation surface。

当且仅当一个 explicit physical slot 参与 element/domain 收窄重声明时，该 slot 的 generated
entity interface 属性使用相应的协变 EXPRESS view。direct entity mapping 的继承和重声明 getter 返回同一
concrete aggregate 实例；SELECT wrapper mapping 的 inherited getter 返回观察该实例的 live adapter，
并逐 leaf 保持 entity identity，但不承诺 adapter 或 value-wrapper reference identity。generated concrete entity class 仍只
拥有一个最窄有效类型的 mutable storage，并保留 ordinary construction、editing、validation 与
writer 所需的 concrete mutable property。没有参与这种重声明的现有成功 schema 继续生成当前
concrete aggregate interface surface，避免无关 source compatibility 变化。

inverse attribute 不成为 Part 21 serialization slot，也不因本决定新增 public mutable generated
property。validation-reachable singular inverse 由 generated code 在当前 `ExchangeStructure` population
中静态枚举完整 forward-role candidate set：恰有一个兼容候选时才向依赖表达式提供 entity value；
零个或多个候选时记录稳定、source-traceable 的 inverse-cardinality validation failure，并让依赖求值
保持不可满足/indeterminate，而不是选择一个候选、抛出验证结果之外的异常或发布部分状态。

aggregate view、inverse lookup、cardinality check、rule evaluation、descriptor projection 与 writer
均保持 source-generated/static dispatch。runtime 不扫描 generated property，不依赖具体 schema，
也不增加 OID/package discovery 或 application-protocol facade。

## 💡 为何现在做此决定

当前 architecture 的客观 review trigger——合法 EXPRESS inheritance/mapping 无法由 interface/class
模型表达——已经成立。协变只读 view 把 subtype assignability 放在观察边界，同时把 mutation 留在
唯一 concrete storage，因而同时满足 EP-002 的语义优先、AP-002 的 schema-neutral dependency 和
EP-003 的静态/AOT 路径。

标准 .NET 只读接口不能完整表达 EXPRESS aggregate metadata；复制 aggregate 又违反 physical slot
identity。非泛型共享 storage 虽可保留多个 mutable view，但新增复杂度和公开概念没有当前证据支持。
若未来必须通过每个 inherited interface 进行 mutation，或 view 无法表达新的 normative aggregate
语义，应重新评估而不是扩张本决定。

## 🔗 证据与链接

- [ISO 10303-11:2004（当前 EXPRESS Edition 2 标准）](https://www.iso.org/standard/38047.html)
- [ISO 10303-21 Edition 3：redeclared/derived attribute 的物理映射](https://www.steptools.com/stds/step/IS_final_p21e3.html)
- [Schema-bound round-trip architecture](../architecture/schema-bound-round-trip.md)
- [Generated entity hierarchy conformance boundary](../conformance/generated-entity-hierarchy.md)
- [Generated aggregate conformance boundary](../conformance/generated-schema-aggregates.md)
- [ADR-0003：AOT-ready schema validation](ADR-0003-aot-ready-schema-validation.md)
- [ADR-0005：explicit-section graph registration](ADR-0005-explicit-section-graph-registration.md)
- [ADR-0006：precompiled schema package distribution](ADR-0006-precompiled-schema-package-distribution.md)

## ⚖️ 后果与接受的代价

- 合法 aggregate 收窄重声明能够保持一个 physical slot、一个 mutable candidate 和多个 live interface view；SELECT wrapper adapter 本身不等同于 storage。
- 新 runtime view 是公开、版本化、schema-neutral 的 infrastructure API，必须有 XML documentation、public API snapshot、compatibility 与 Native AOT proof。
- 仅新支持的收窄 slot interface 暴露 read-only view；调用者通过 generated concrete class 的最窄属性继续编辑。现有成功生成的普通 aggregate interface surface 保持不变。
- singular inverse 的无候选/多候选状态进入完整 `ValidationResult`，而非导航异常；稳定 failure identity 成为 compatibility surface。
- inverse candidate enumeration 可能增加 validation 成本；本决定不承诺新的复杂度或索引。若代表性 workload 证明全 population enumeration 不可接受，再决定静态索引策略。
- 本决定不扩张 schema conformance 声明；只有通过 focused construct、round-trip、regression 和 AOT evidence 的语义才可标为 delivered。

## 🛠️ 下游交付约束

- 每个收窄 redeclared aggregate 必须保留一个且仅一个 mutable physical storage；direct entity view 保持 storage 引用身份，SELECT wrapper view 保持实时观察与 leaf entity identity。
- covariant view 必须保留相应 EXPRESS aggregate kind 的 bounds、order/index、multiplicity、optional-slot 与 uniqueness observation，且不能提供接受 `T` 的 mutation。
- 非收窄且当前成功生成的 schema public aggregate surface 必须保持 source/API snapshot compatibility。
- generated construction、hydration、validation、direct-reference enumeration、projection 和 writing 必须观察同一 storage，不得建立同步副本。
- singular inverse 的完整 candidate set 只由 model context 的 forward roles 计算；恰有一个时求值，零个或多个时产生稳定聚合验证证据。
- inverse failure 不得 first-wins、静默丢弃、发布 partial model/output，或要求 runtime reflection、schema registry 和动态 discovery。
- 新 public runtime/generated contract 与 validation diagnostic identity 必须通过完整 API、semantic regression、package consumer 和 Native AOT compatibility 分类。

## 🔄 退出要求

替代方案必须同时保留 EXPRESS physical slot identity、aggregate metadata、收窄 assignability、可变候选编辑、
singular inverse 完整 cardinality evidence、schema-neutral dependency 和静态 Native AOT 路径。任何删除或
重塑已发布 view/diagnostic contract 的方向必须有明确 compatibility/migration 边界，不能通过同版本重建。

## 📅 后续事项与复审触发

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| 批准、修订或拒绝本 aggregate/inverse projection 方向 | repository maintainer | 2026-08-27 明确批准继续，并要求所有更改遵循适用 ISO 要求 | Closed |
| 交付 generated/API/round-trip/validation/Native AOT evidence | owning delivery change | 在把该能力标为 delivered 前 | Open |
| 复审 inherited-interface mutation | repository maintainer | 真实消费者必须通过较宽 inherited view 直接修改同一 slot | Open |
| 复审 inverse indexing | repository maintainer | 代表性 schema/population 证明静态枚举无法满足已批准资源边界 | Open |
| 复审 view 完整性 | repository maintainer | 新 normative aggregate construct 无法由只读 metadata/view 无损表达 | Open |
