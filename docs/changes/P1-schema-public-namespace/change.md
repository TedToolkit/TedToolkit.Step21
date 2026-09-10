# 将 EXPRESS 公开类型迁移到 Schemas 命名空间

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: migration -->
<!-- change-status: in-progress -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: user-explicit-rename-and-implement-2026-09-10 -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

所有 EXPRESS 生成的公开类型使用 `TedToolkit.Step21.Schemas.<SchemaPascalCase>`，让 CLR 命名空间表达 Schema 领域归属，而不是代码生成方式。当前 `Generated` 对预编译 AP 包的消费者是无关的实现细节，且已成为公开类型名的一部分。

<!-- section: scope -->
## Scope and non-goals

- In scope:
  - 更改生成器对 descriptor、Entity、定义类型、枚举、SELECT 和跨 Schema 引用的公开命名空间。
  - 迁移自定义 Schema 消费者、AP203/AP214/AP242 预编译包、公开 API/schema baseline、契约测试与当前消费者文档。
  - 将 ADR-0011 的公开 Schema 命名边界反映到当前架构与 conformance 记录。
- Non-goals:
  - 不更改 NuGet 包名、程序集名、包版本或 runtime 依赖范围。
  - 不保留 `.Generated.` 下的重复公开类型，不增加 facade、registry、反射发现或 AP 专用生成路径。
  - 不重命名内部 ANTLR 生成目录或 parser 实现命名空间。
- Compatibility or deliberately preserved behavior:
  - 这是明确的源码、二进制和反射公开 API 破坏性迁移；发布分类仍由 ADR-0006 管理，本 change 不修改版本、不发布或重新发布任何包。
  - EXPRESS nominal schema 名、descriptor 选择、Entity 结构、引用身份、绑定、校验、写出及 Native AOT 特性保持不变。

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | 对任意有效 EXPRESS `SCHEMA` 生成公开 CLR 类型 | 类型位于 `TedToolkit.Step21.Generated.<SchemaPascalCase>` | 类型仅位于 `TedToolkit.Step21.Schemas.<SchemaPascalCase>` | Schema PascalCase 映射、公开类型形状和运行时语义不变 |

<!-- acceptance-case: AC-01 -->
### AC-01 — 生成器产出新的统一 Schema 命名空间

```gherkin
Scenario: 自定义 Schema 公开类型仅使用 Schemas 根
  Given 一组可以完整绑定和生成的 EXPRESS schema
  When 增量生成器编译该输入
  Then descriptor、Entity、值类型及跨 schema 引用仅出现在 `TedToolkit.Step21.Schemas.<SchemaPascalCase>`
  And 将命名空间根归一化后，完整 public/protected API 清单与迁移前基线零差异
```

<!-- acceptance-case: AC-02 -->
### AC-02 — 预编译 Schema 包保持强类型读写能力

```gherkin
Scenario: AP203、AP214 和 AP242 包消费者使用 Schemas 类型
  Given 仅引用维护的预编译 Schema 包的 .NET 消费者
  When 它使用 `Schemas` descriptor 读取并查询强类型 Entity
  Then 现有 schema 身份、绑定、校验和读写行为保持成功
```

## Constraints and risks

- ADR-0011 要求命名空间由 Schema 而非 package/AP 身份所有，且禁止保留第二套重复 CLR 类型。
- ADR-0006 要求预编译包继续复用同一 runtime/generator 边界，不得引入包专用 facade 或发现机制。
- 主要风险是遗漏嵌入测试源码、跨 Schema 全限定名或 manifest 中的旧 namespace；必须用全库扫描和契约测试关闭。
- 公开 API baseline 必须从实现前生成器产出完整清单，仅归一化 `TedToolkit.Step21.Generated`/`TedToolkit.Step21.Schemas` 根后比较；不得通过盲目重录新哈希接受其他类型、成员、继承、nullability 或文档变化。
- 未来若发布该破坏性公开 API，必须独立地按 ADR-0006 与上一个稳定 baseline 分类；本交付不授权用现有稳定版本重新发布。
- 若实现需要变更 package/assembly 身份、版本、schema 名或运行时语义，则超出已批准边界并停止。

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from the approved baseline.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: 生成器、生成 API 契约、预编译包消费者、schema baseline 和当前公开文档在同一交付中完成命名空间迁移。
- Other real start conditions or resource prerequisites: 保留当前工作树中与 Annex F 移除有关的用户修改；不覆盖、回退或重写其证据。
- Likely touchpoints (non-binding): EXPRESS emitters/type resolver、generator/contract tests、packed consumers、AP package README/schema manifests、conformance 和当前 architecture 文档。
- Private implementation choices left open: 共享 namespace 常量、测试组织和批量文本迁移方式，只要不修改非 Schema 公开生成边界。

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-02 purpose=boundary shape=integration -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | 代表性自定义/跨 Schema 输入仅生成 `Schemas` 类型，且 AP203/AP214/AP242 与自定义输入的完整 public/protected API 在仅归一化 namespace 根后与实现前清单零差异 | 运行 `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release -- --treenode-filter "/*/*/ExpressIncrementalGeneratorTests/*"`，以及 AP203/AP214/AP242 的完整 namespace-normalized API baseline 契约测试 |
| AC-02 | Primary | AP203/AP214/AP242 包消费者在新 namespace 下编译，并保持强类型语义旅程 | 先运行 `dotnet build TedToolkit.Step21.slnx --configuration Release --no-incremental -m:1 --disable-build-servers`，再运行 `dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release --no-build -- --treenode-filter "/*/*/PackedConsumerTests/*"` |
| OB-01 | Conditional | 受影响解决方案在 Release 配置下完整编译，且当前契约中无遗留的公开 `.Generated.` schema 名 | `dotnet build TedToolkit.Step21.slnx --configuration Release --no-incremental -m:1 --disable-build-servers` 并扫描非历史交付文档 |

<!-- section: completion-criteria -->
## Completion

AC-01 和 AC-02 在同一 candidate 上通过，namespace-normalized 完整公开 API 除根替换外零差异，Release 解决方案构建成功，ADR-0011、当前 architecture/conformance 与消费者文档均只声明 `Schemas` 公开边界，且未修改包版本、未发布或重新发布包，也未改动现有非相关工作树变更。
