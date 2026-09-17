# 提供显式的 INTEGER 到 REAL 读取兼容

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: implemented -->
<!-- delivery-shape: single -->

- Priority: P2
<!-- approval-source: user-approved-opt-in-compatibility-and-requested-implementation-2026-09-16 -->
<!-- candidate-binding: commit:502400629d2e9895b2923b06283855a9fc4a7d86 -->

<!-- section: goal-rationale -->
## Goal and rationale

需要读取供应商以物理 `INTEGER` 编码 EXPRESS `REAL` 的交换结构的 .NET 消费者，可以显式启用 schema-aware 精确提升，而无需复制实体参数顺序或改写输入文本；未启用兼容的调用方继续获得严格绑定结果。

<!-- section: scope -->
## Scope and non-goals

- In scope:
  - 增加默认关闭的 schema-neutral 读取兼容选项。
  - 在 generated descriptor 的直接属性、定义类型、聚合和 SELECT hydration 中精确提升整数。
  - 将选项传播到一次读取的完整资源图，并记录兼容一致性边界。
- Non-goals:
  - 不修改 Part 21 或 EXPRESS grammar，不改变物理 token 的 `ParameterValueKind`。
  - 不引入 RI2、SPF 或其他供应商/schema 专用概念。
  - 不放宽其他物理类型与 EXPRESS 目标域的绑定，也不发布 NuGet 包。
- Compatibility or deliberately preserved behavior:
  - 现有 `Read` 重载和默认读取严格拒绝物理 `INTEGER` 到 EXPRESS `REAL` 的绑定。
  - `NUMBER`、真正的物理 `REAL`、验证、写出和资源安全边界保持不变。

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | generated EXPRESS schema 读取以 `INTEGER` 编码的 `REAL` | 所有读取均以 `P21-BIND-PARAMETER` 拒绝 | 仅显式兼容读取将整数精确提升为 `RealValue` | 默认读取仍拒绝；物理值分类和 grammar 不变 |

<!-- acceptance-case: AC-01 -->
### AC-01 — 默认读取保持严格

```gherkin
Scenario: 未启用兼容时读取 INTEGER 表示的 REAL
  Given 一个把属性声明为 EXPRESS REAL 的 generated schema
  And 一个在对应位置使用物理 INTEGER 的交换结构
  When 调用默认 Read API
  Then 读取以 P21-BIND-PARAMETER 失败
```

<!-- acceptance-case: AC-02 -->
### AC-02 — 显式兼容读取精确提升全部 REAL hydration 形态

```gherkin
Scenario: 启用整数到 REAL 兼容
  Given 一个包含直接 REAL、REAL 定义类型、REAL 聚合和 REAL SELECT 分支的 generated schema
  And 对应位置使用任意精度物理 INTEGER 的交换结构
  When 使用显式兼容读取选项读取
  Then 所有值均绑定为数值完全相同且 exponent 为零的 RealValue
  And 写出后使用默认严格读取仍可成功解析
```

<!-- acceptance-case: AC-03 -->
### AC-03 — 兼容选项不改变物理分类或其他目标域

```gherkin
Scenario: 启用兼容后读取其他数值映射
  Given 一个同时声明 INTEGER、REAL 和 NUMBER 的 generated schema
  When 使用显式兼容读取选项读取不同物理数值种类
  Then ParameterValue 仍把物理 INTEGER 报告为 INTEGER
  And NUMBER 中的物理 INTEGER 仍保持 NUMBER 的 INTEGER alternative
  And 物理 REAL 到 EXPRESS INTEGER 仍以 P21-BIND-PARAMETER 失败
```

<!-- acceptance-case: AC-04 -->
### AC-04 — 兼容选项传播到完整资源图

```gherkin
Scenario: 外部资源使用 INTEGER 表示 REAL
  Given 根交换结构通过显式 provider 引用一个外部交换结构
  And 外部实体在 EXPRESS REAL 位置使用物理 INTEGER
  When 根读取显式启用兼容选项
  Then 外部实体也以相同规则绑定为精确 RealValue
```

## Constraints and risks

- [ADR-0015](../../adr/ADR-0015-opt-in-numeric-representation-compatibility.md) 要求默认严格、显式 opt-in、schema-neutral、任意精度和完整 hydration 形态覆盖。
- 公共 API 只能增加兼容入口，不能改变或替换现有方法签名。
- generated code 仍只能依赖 runtime，不得引入反射、动态代码或具体 schema 依赖。
- 主要风险是只修复直接属性而遗漏聚合或 SELECT helper；契约测试必须覆盖全部路径。
- 若实现需要无条件放宽、修改 grammar、改变现有 API 签名或增加供应商概念，则停止并重新审批。

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from the approved baseline.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: runtime 读取选项、generated descriptor hydration、契约测试和当前 conformance 文档形成一个可独立发布的兼容能力。
- Other real start conditions or resource prerequisites: .NET SDK、现有 TUnit 测试项目和 generated schema 测试基础设施可用。
- Likely touchpoints (non-binding): `ExchangeStructureReadOptions`、读取上下文、`SchemaDescriptor`、`ExpressSchemaDescriptorEmitter`、atomic read 和 resource resolution 测试、generated descriptor conformance 文档。
- Private implementation choices left open: 公共选项的具体类型形状、runtime 内部传播方式和 generated helper 的组织，只要不扩大批准的兼容语义。

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=regression shape=contract -->
<!-- primary-proof: AC-02 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-03 purpose=regression shape=contract -->
<!-- primary-proof: AC-04 purpose=boundary shape=integration -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | 默认公开读取对 INTEGER→REAL 返回 `P21-BIND-PARAMETER` | `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release -- --treenode-filter "/*/*/*/Should_reject_integer_parameters_when_real_is_declared_by_default"` |
| AC-02 | Primary | 显式选项覆盖直接、别名、聚合、SELECT、任意精度整数、零 exponent 及严格 read-write-read | `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release -- --treenode-filter "/*/*/*/Should_promote_integer_parameters_when_real_compatibility_is_enabled"` |
| AC-03 | Primary | 物理 INTEGER 分类与 NUMBER 的 INTEGER alternative 保持不变，REAL→INTEGER 仍被拒绝 | `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release -- --treenode-filter "/*/*/*/Should_preserve_other_numeric_bindings_when_real_compatibility_is_enabled"` |
| AC-04 | Primary | 根读取选项使外部资源中的 INTEGER→REAL 使用同一精确提升 | `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release -- --treenode-filter "/*/*/*/Should_apply_real_compatibility_to_external_resources"` |
| OB-01 | Conditional | 完整 Release 构建与现有测试不出现兼容回归 | 运行仓库 Build 项目及完整 `TedToolkit.Step21.Tests` |

<!-- section: completion-criteria -->
## Completion

AC-01 至 AC-04 在同一 candidate 上通过，完整 Release 构建与测试成功；现有公共 API 签名与默认行为保持不变，public API snapshot 仅新增经批准的兼容入口；一致性文档明确标记 opt-in extension，未发布包且没有临时或调试资产。
