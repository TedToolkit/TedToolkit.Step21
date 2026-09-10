# ADR-0012：不提供 Annex F ECMAScript binding

- Status: Accepted
- Date: 2026-09-10
- Decision owner: repository maintainer
- Decision scope: TedToolkit.Step21 的语言绑定、包边界和 ISO 10303-21 Annex F 一致性声明
- Applicable product intent: [TedToolkit.Step21 product intent](../product/README.md)
- Applicable principles: [AP-004](../principles/architecture.md)、[EP-003 和 EP-004](../principles/engineering.md)
- Supersedes: [ADR-0009](ADR-0009-explicit-part21-resource-and-trust-boundaries.md) 中关于交付可选 Annex F language binding 的部分
- Superseded by: None
- Approval source: user explicit removal decision, 2026-09-10

## 决定概览

TedToolkit.Step21 是面向 .NET 消费者的 C# 库，只交付 C#/.NET API；它不交付 ECMAScript/JavaScript language binding、脚本模块或脚本 host bridge，Annex F 在当前产品一致性边界中明确记为不适用。

## 背景与决策问题

仓库的产品是一个 C# 库，其消费者通过 .NET API 读取、编辑、验证和写出 Part 21 数据。ISO 10303-21 同时描述 Annex F ECMAScript binding，但标准中存在另一种语言的绑定，并不使该语言自动成为 C# 库的产品责任。仓库曾把这一 binding 作为单独可选包交付；它没有对应的 JavaScript 消费者需求，却扩大了代码、测试、发布和安全维护面。需要决定它是否属于本 C# 库的产品边界。

## 决策驱动与约束

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | 产品只需要 .NET 中的 Part 21 读取、编辑、验证和写出 | 维护者 2026-09-10 的明确决定 | Must |
| Hard constraint | 对外集成面是 C#/.NET API，而不是多语言 SDK | Product intent | Must |
| Hard constraint | 一致性声明必须与实际交付能力一致 | EP-004 | Must |
| Decision driver | 避免无消费者的脚本、桥接、测试和发布维护面 | 现有独立 Annex F 包及集成测试 | High |
| Constraint | 核心 ISO 10303-21 模型、读写和现有非脚本能力保持不变 | Product intent | Must |

## 方案与证据

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| 保留独立可选 Annex F 包 | Documented：不污染核心依赖，但仍需维护和发布 | No | 保留无人使用的功能面 | Rejected |
| 把 binding 合入核心 runtime | Documented：扩大动态执行、安全和 AOT 风险 | No | 与产品需求直接冲突 | Rejected |
| 删除 binding 并收窄一致性声明 | Documented：核心模型不依赖该包 | Yes | 不再声明 Annex F ECMAScript 支持 | Selected |

## 决定

删除所有 ECMAScript 源码、bridge、包和专属测试。核心 runtime 不暴露替代脚本 API；一致性清单把 Annex F language binding 标为当前产品不适用。

## 现在作出决定的原因

语言绑定属于目标语言的 SDK 表面，不是 Part 21 文件语义本身。一个 C# 库不应仅因为 ISO 文本包含 ECMAScript 附录，就承担 JavaScript API、运行时桥接和发布责任。该决定是 AP-004 的有意例外：选定的 C# processor contract 不包含其他语言的 binding；EP-004 要求同时收窄公开声明。若未来产品明确升级为多语言 SDK，再重新评估，而不能仅因为 Annex F 存在就自动恢复实现。

## 证据与链接

- [Product intent](../product/README.md)
- [Current conformance statement](../conformance/iso-10303-21-2016.md)
- [Architecture boundary](../architecture/schema-bound-round-trip.md)

## 后果与接受的权衡

- 减少一个 NuGet 包、一套 JavaScript 资产、桥接代码和 Node 集成测试。
- 核心 Part 21 读写、anchors、schema populations、signatures 和外部资源能力不受影响。
- 消费者通过 C#/.NET API 使用本库；本仓库不提供 Annex F `P21` JavaScript 对象模型。
- ISO conformance 文档必须明确该语言绑定不在产品适用范围内。

## 后续交付约束

- 核心或其他包不得重新引入脚本引擎、ECMAScript 资产或通用脚本桥接接口。
- 构建、包清单、测试和文档不得声称交付 Annex F ECMAScript binding。
- 保留 ISO 10303-21 clear-text syntax 与其他已支持的非脚本语义。

## 退出条件

只有维护者决定把产品从 C# 库扩展为多语言 SDK，并重新批准包边界、安全边界和一致性目标后，才能替换本决定。

## 后续事项与复审触发条件

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| 重新评估语言绑定 | repository maintainer | 出现明确的 JavaScript/ECMAScript 消费需求 | Deferred |
