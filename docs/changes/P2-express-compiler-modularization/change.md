# 降低 EXPRESS Compiler 内部耦合且保持所有可观察行为

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-preserving-refactor -->
<!-- change-status: approved -->
<!-- delivery-shape: single -->

- Priority: P2
<!-- approval-source: user-explicit-approval-2026-08-26 -->
<!-- candidate-binding: none -->

- Approval: 用户于 2026-08-26 明确批准；此前已明确要求按 ADR-0006 → AP203 → compiler modularization 的顺序继续执行。

<!-- section: goal-rationale -->
## Goal and rationale

在 AP203 交付完成并形成稳定基线后，将 EXPRESS syntax、closed-set binding、expression/flow analysis、generation planning 与 source emission 的内部责任整理为可独立理解和修改的阶段，同时保持生成源码、诊断、公开 API、运行时语义、包内容边界与 Native AOT 行为不变。

当前 Analyzer 的非生成实现约 1.3 万行，其中 `ExpressSchemaCompiler`、结构验证、表达式和 descriptor emitter 集中了多种独立责任；完整 AP203 schema 已证明一次标准语义修正可能同时穿过 binding、flow facts、planning 和 emission。该结构增加回归判断和后续 schema 支持成本，但尚无证据支持拆分公开程序集或改变生成契约。

<!-- section: scope -->
## Scope and non-goals

- In scope: 明确 Analyzer 内部阶段的输入、输出、所有权和单向依赖；分解同时承担多阶段责任的私有类型；消除跨阶段对可变草稿状态或 emitter 私有表示的隐式依赖；保留一个确定性的 incremental-generator 入口。
- Non-goals: 修复或扩展 EXPRESS 语义、改变 grammar、生成类型形状、诊断、运行时 API、SchemaDescriptor 合约、包拓扑、RoslynHelper 选择，或拆分新的程序集/项目。
- Compatibility: 固定 EXPRESS 输入、生成器版本和工具链下的生成源码路径与字节、诊断完整内容与顺序、Runtime public API、XML 文档、Part 21 语义和包/AOT 边界全部保持不变。
- Delivery relationship: 本 change 独立于 AP203 产品交付，但以 AP203 完成并合并后的真实 schema、包和 AOT 证明作为开始基线；不得与尚未完成的 AP203 compiler 修正并行实施。

<!-- section: invariants -->
## Preserved invariants

<!-- preserved-invariant: INV-01 -->
- INV-01: 对固定的仓库 EXPRESS 输入集（包含完成后的 AP203 baseline），每个 generated hint path 和源码字节与批准基线完全相同。

<!-- preserved-invariant: INV-02 -->
- INV-02: 对固定的有效和无效 EXPRESS 输入集，syntax/binding/generation diagnostics 的 code、severity、message、文件位置、顺序和受影响 schema 的原子 withholding 行为完全相同。

<!-- preserved-invariant: INV-03 -->
- INV-03: `TedToolkit.Step21` runtime public/protected API、生成 public 类型的继承、nullability、构造函数、成员顺序、XML documentation 和 descriptor identity 完全相同。

<!-- preserved-invariant: INV-04 -->
- INV-04: 已交付的读取、引用 hydration、显式验证、canonical writing、复杂映射和语义 read-write-read 行为及失败边界完全相同。

<!-- preserved-invariant: INV-05 -->
- INV-05: NuGet asset layout、runtime dependency closure、Analyzer-only dependency isolation、trimming 和 Native AOT publish/run 行为完全相同。

<!-- preserved-invariant: INV-06 -->
- INV-06: 重构不引入新的公开/受保护 API、运行时依赖、反射发现、动态代码、线程安全承诺、性能承诺或 application-protocol 特例。

### Required structural completion gate

- Analyzer 内形成 syntax → closed-set binding → expression/flow analysis → generation planning → source emission 的显式单向阶段。每个阶段有可定位的 owner、输入和输出；上游阶段不依赖下游阶段，阶段间不共享可变 draft，syntax tree 不泄漏到 planning/emission，emitter-private representation 不被其它阶段读取。私有类型名、文件布局和阶段 DTO 仍由实现决定。

## Constraints, alternatives, and risks

- Governing records: 遵循 [product intent](../../product/README.md)、[AP-002/AP-003](../../principles/architecture.md)、[EP-002/EP-003/EP-004](../../principles/engineering.md)、[schema-bound architecture](../../architecture/schema-bound-round-trip.md)、[ADR-0002](../../adr/ADR-0002-roslynhelper-source-composition.md) 和 [ADR-0003](../../adr/ADR-0003-aot-ready-schema-validation.md)。
- Hard boundary: 这是结构重构，不得以更新 approved snapshot、generated baseline 或 conformance 文档的方式接受差异。
- If behavior differs: 若发现现有缺陷或需要新的标准行为，停止受影响重构，先建立并完成独立 bug-fix/behavior-change，再重新固定基线。
- Recovery: 保持小步可回退的内部提交；任一 invariant 失败时回退最近的结构步骤，而不是加入兼容分支、双实现或 suppressions。
- Escalation: 新程序集/项目边界、新依赖、公开 API、生成输出变化、诊断变化、SchemaDescriptor 合约变化或无法保持字节确定性时，返回 change/architecture design 并重新批准。

| Alternative | Disposition | Reason |
| --- | --- | --- |
| 保持现状 | Rejected | AP203 已使跨 binding/planning/emission 的修改和证明成本成为重复维护问题 |
| 仅按文件长度机械拆类 | Rejected | 文件数量不能建立阶段所有权，可能保留同样的可变耦合 |
| 拆分 Analyzer 为多个程序集 | Rejected | 尚无独立发布、复用或构建证据，且会扩大 package 与加载边界 |
| 在一个 Analyzer 程序集中建立显式内部阶段 | Selected | 能降低责任耦合，同时保持包、运行时和生成契约不变 |

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: PRE-01 source=../P2-ap203-schema-package/change.md contract=AC-12 -->
| ID | Required input or guarantee | Source change outcome | Required readiness evidence |
| --- | --- | --- | --- |
| PRE-01 | 固定 AP203 schema/fixture 可在无网络、无本机 OCCT 和无隐藏缓存条件下重现生成结果，并可作为 compiler 重构的真实大型 schema 基线 | `../P2-ap203-schema-package/change.md`, AC-12 | 所选 implementation baseline 中的源 change 为 `completed`，且声明 AC-12；开始 production 修改前以 `--require-ready --baseline <exact-sha>` 验证 |

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target area: 在 `TedToolkit.Step21.Analyzer` 内完成一次单交付重构；不创建 work-item map。
- Other start conditions: 完整 Release build、fast/integration tests、package proof 和 AOT proof 为绿色。首个 production 结构修改之前，先以单独的 proof-only commit 固定 baseline revision、.NET SDK/Roslyn/package versions、按 ordinal 排序的输入 manifest、每个 hint/source UTF-8 SHA-256，以及完整有序 diagnostic tuple（code、severity、message、path、span、withholding result）；该 commit 的稳定命令必须能在干净检出中重建并校验基线。
- Likely touchpoints (non-binding): EXPRESS binding/compiler、expression binder、generation plans/projections/emitters、incremental-generator composition，以及既有 generator/contract tests。
- Private choices left open: 私有 namespace、类型名、阶段 DTO、文件拆分、不可变集合选择、局部算法和测试组织，只要全部 invariants 成立。

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: INV-01 purpose=regression shape=contract -->
<!-- primary-proof: INV-02 purpose=regression shape=contract -->
<!-- primary-proof: INV-03 purpose=regression shape=contract -->
<!-- primary-proof: INV-04 purpose=regression shape=integration -->
<!-- primary-proof: INV-05 purpose=boundary shape=end-to-end -->
<!-- primary-proof: INV-06 purpose=structural shape=component -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| INV-01 | Primary | 固定工具链和输入 manifest 下，每个 generated hint path 与 UTF-8 source SHA-256 的完整集合相同，零新增、零缺失、零 byte diff | 执行 proof-only commit 提供的 `verify-express-compiler-baseline` 有界入口；候选不得重写 approved baseline |
| INV-02 | Primary | 固定有效/无效 EXPRESS corpus 的完整有序 diagnostic tuple 与 invalid-schema withholding 结果相同 | 执行 `verify-express-compiler-baseline`，再运行 `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release` |
| INV-03 | Primary | runtime API snapshot 与固定 schema generated public-shape snapshot 无差异 | `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release` |
| INV-04 | Primary | 全部 schema-bound read/validate/write/read 行为和失败边界通过 | `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release`; `dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release` |
| INV-05 | Primary | package/dependency audit 与 trimmed Native AOT journey 保持绿色 | `pwsh ./build/verify-native-aot.ps1`，并执行 AP203 package/AOT proof |
| INV-06 | Primary | Release build 零 warning/error，API/package audit 无新增 surface、runtime dependency 或动态路径 | `dotnet build TedToolkit.Step21.slnx --configuration Release --no-restore --no-incremental` 和既有 package audit |
| Baseline freeze gate | Conditional | 在任何 production 重构前，独立 proof-only commit 固定并可重建 revision/toolchain/input/source/diagnostic manifest，候选实现不能重写该基线 | 先执行新增的 `verify-express-compiler-baseline` 并记录 manifest SHA-256；独立评审通过后才修改 production compiler |
| Structural completion gate | Conditional | 候选存在五阶段 owner/input/output 清单；依赖检查拒绝反向依赖、跨阶段 mutable draft、planning/emission 对 syntax tree 的依赖及上游对 emitter-private representation 的依赖 | 运行 proof-only commit 建立的内部依赖规则/architecture test，并由独立 implementation review 逐项核对阶段清单和例外表 |

<!-- section: completion-criteria -->
## Completion

完成需要：六项 invariant 与 required structural completion gate 在同一候选 revision 上全部通过；baseline proof-only commit 在首个 production 修改前已独立评审，baseline/candidate 生成与诊断比较可从干净检出重复执行；完整 Release、fast、integration、package 和 AOT gates 为绿色；无 snapshot 被用于掩盖差异；实际内部阶段边界和仍然存在的例外在 maintainer documentation 中准确记录；独立 implementation review 确认无行为、API、package 或 architecture deviation。
