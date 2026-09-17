# ADR-0015：以显式读取选项隔离数值表示兼容

- Status: Accepted
- Date: 2026-09-16
- Decision owner: repository maintainer
- Decision scope: TedToolkit.Step21 在 EXPRESS schema 绑定阶段处理非标准数值物理表示的扩展边界
- Applicable product intent: [TedToolkit.Step21 product intent](../product/README.md)
- Applicable principles: [AP-001、AP-002 和 AP-004](../principles/architecture.md)、[EP-001、EP-002 和 EP-004](../principles/engineering.md)
- Supersedes: None
- Superseded by: None
- Approval source: user explicit approval of opt-in compatibility direction, 2026-09-16

## 决定概览

TedToolkit.Step21 保持默认 ISO 严格绑定，同时允许调用方通过显式、默认关闭的读取选项，在 schema 绑定阶段把物理 `INTEGER` 参数精确提升为 EXPRESS `REAL` 值。

## 背景与决策问题

部分外部生产系统会在 EXPRESS 目标域为 `REAL` 时输出没有小数点的物理 `INTEGER` 参数。默认绑定当前会拒绝这种输入；消费者若在调用前重写文本，则必须重复实体字段位置、嵌套聚合和 SELECT 类型知识，容易与实际 schema 漂移。需要决定兼容机制应由消费者预处理、由核心无条件放宽，还是由 Step21 在保持严格默认值的前提下提供显式扩展。

## 决策驱动与约束

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | ISO 10303-21 仍定义默认读取和一致性声明边界 | Product intent、AP-001、EP-004 | Must |
| Hard constraint | 兼容行为不得静默改变现有调用方的绑定结果 | EP-002、EP-004 | Must |
| Hard constraint | runtime 保持 schema-neutral，具体 schema 和供应商不得进入核心 | AP-002 | Must |
| Decision driver | 类型提升必须由同时掌握物理参数种类和 EXPRESS 目标域的绑定层完成 | 当前 generated descriptor 绑定边界 | High |
| Decision driver | 整数提升必须保持任意精度，不经过二进制浮点 | 当前 `BigInteger` 与 `RealValue` 精确数值模型 | High |

## 方案与证据

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| 保持消费者文本预处理 | Documented：消费者必须复制实体参数位置和嵌套类型知识 | No | schema 漂移会造成错误重写 | Rejected |
| 核心无条件接受 `INTEGER` 作为 `REAL` | Documented：此前实现会改变所有现有读取调用 | No | 破坏严格默认行为和一致性边界 | Rejected |
| 把受影响 schema 的 `REAL` 改为 `NUMBER` | Documented：生成公开类型会从 `RealValue` 变为 `NumberValue` | No | 为物理表示偏差削弱领域类型 | Rejected |
| 显式启用 schema-aware 整数到 REAL 提升 | Documented：generated descriptor 已拥有目标域信息，读取选项已是每次调用的能力边界 | Yes | 增加一个受控公共兼容选项 | Selected |

## 决定

新增一个 schema-neutral、显式、默认关闭的读取兼容选项。启用后，generated descriptor 仅在目标域最终解析为 EXPRESS `REAL` 时接受物理 `INTEGER`，并以整数作为 significand、零作为十进制 exponent 构造精确 `RealValue`。该规则覆盖直接属性、定义类型、聚合元素和 SELECT 分支，并应用于同一次读取解析的资源图。

不改变 Part 21 grammar，不改变 `ParameterValue` 对物理 token 种类的报告，不引入供应商或具体 schema 名称。除物理 `INTEGER` 到 EXPRESS `REAL` 外，不放宽任何物理种类与目标域映射；`NUMBER` 的现有映射保持不变。写出仍使用目标类型的规范物理表示。

## 现在作出决定的原因

兼容需求已经由真实消费者输入触发，而文本预处理把 schema 绑定责任泄漏到了调用方。generated descriptor 是唯一同时拥有物理参数和 EXPRESS 目标域的边界，因此能避免硬编码字段位置。显式选项保留 AP-001 和 EP-004 要求的严格默认值；schema-neutral 的提升机制满足 AP-002。若未来标准证据证明该映射属于默认必需语义，应另行修订一致性结论，而不是继续把标准行为标记为兼容扩展。

## 证据与链接

- [Product intent](../product/README.md)
- [Architecture principles](../principles/architecture.md)
- [Engineering principles](../principles/engineering.md)
- [Generated schema value mapping](../conformance/generated-schema-values.md)
- [Generated schema descriptors](../conformance/generated-schema-descriptors.md)

## 后果与接受的权衡

- 现有无选项读取保持严格，避免静默扩大一致性声明。
- 兼容调用方不再维护文本重写器或实体参数索引。
- 新公共选项增加 API 维护面，但它属于明确命名的 parser/binder 基础设施，不增加交换结构领域语义。
- 启用兼容时，输入的原始整数 token 会被强类型模型规范化为 `RealValue`；后续写出不承诺保留原 token 拼写。

## 后续交付约束

- 默认读取必须继续拒绝物理 `INTEGER` 到 EXPRESS `REAL` 的绑定。
- 兼容必须由每次 `Read` 的显式选项启用，并传播到该次读取的完整资源图。
- 提升必须精确保留任意精度整数值，并覆盖直接、别名、聚合和 SELECT hydration 路径。
- grammar 和 `ParameterValue` 物理值分类不得因该选项改变。
- 一致性文档必须把该行为标为 opt-in compatibility extension，而不是 ISO 默认行为。

## 退出条件

若规范证据要求默认接受该映射，应以新的 ADR 重新分类为标准行为；若兼容需求消失，也只能在公开弃用和版本策略明确后移除选项。

## 后续事项与复审触发条件

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| 复审默认语义 | repository maintainer | 获得改变 INTEGER/REAL 映射结论的规范证据 | Deferred |
| 复审兼容选项范围 | repository maintainer | 出现第二种需要 schema-aware coercion 的非标准表示 | Deferred |
