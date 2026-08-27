# 支持 EXPRESS 聚合元素收窄为 closed SELECT

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: draft -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: none -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

使用 TedToolkit.Step21 Analyzer 的 .NET 消费者能够生成 ISO 合法的显式属性重声明：aggregate kind、
bounds 与 flags 不变，而 element domain 从 entity 或 closed entity-valued SELECT 收窄为 closed
entity-valued SELECT。该能力补齐 AP214 `AUTOMOTIVE_DESIGN` 的两个真实声明，并保持一个物理 slot、
无损 entity identity、原子 read/write 与静态 Native AOT 边界。

现有 M-04 只支持 aggregate element 的 entity-to-subtype 收窄。对 closed SELECT 继续返回
`STEP21EXP005` 是明确但不完整的支持边界；直接复用 CLR covariance 不成立，因为 generated SELECT
是 value wrapper。需要一个清晰的 live read-only projection contract，而不是跳过声明或复制 storage。

<!-- section: scope -->
## Scope and non-goals

- In scope:
  - 新增 `M-06`：相同 ARRAY/LIST/BAG/SET kind、等价 bounds/flags 下，element 从 direct entity 或
    closed entity-valued SELECT 收窄为 closed entity-valued SELECT。
  - narrowed SELECT 的 flattened leaves 必须全部由 original domain 的相同 entity 或传递 subtype 覆盖，
    且关系为严格收窄；按 bound-symbol identity 判定，不使用名字或运行时反射。flattened coverage identity-equal
    时继续走现有 M-02 equivalent mapping，不属于 M-06。
  - concrete entity 只保存最窄 mutable aggregate；inherited interface 通过 live read-only projected view
    观察当前元素，并把每个 selected leaf 无损提升到原 entity/SELECT domain。
  - descriptor hydration、validation、writer、direct references、chain/diamond composition、XML/public API
    与 actual-package Native AOT 证明覆盖该映射。
- Non-goals:
  - 不处理 aggregate kind/bounds/flags 变化、nested aggregate、extensible/unknown SELECT、非 entity leaf、
    WHERE-domain implication 或 reverse/widening mapping。
  - 不新增 AP203/AP214/AP242 特判、第二份物理 slot、public mutable inherited view、runtime schema discovery、
    reflection 或 dynamic code。
  - AP214 的 dependency-cycle diagnostic、reachable function statement shape 与 complex construction 缺口
    仍由 AP214-001 的既有 schema-neutral implementation boundary 负责。
- Compatibility or deliberately preserved behavior:
  - M-01 至 M-05、普通 aggregate property、现有 invalid/unsupported diagnostic partition 保持不变。
  - direct entity M-04 继续让 covariant interface getter 与 concrete aggregate 引用相等；M-06 只保证
    projected view 实时反映同一 storage、元素 entity identity 和 EXPRESS metadata，不承诺 wrapper 引用相等。
  - inherited view 只读；mutation 只经最窄 concrete aggregate，并对后续所有 view 枚举立即可见。

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Aggregate element redeclaration | closed SELECT element 返回 `STEP21EXP005` | 合法 M-06 生成；invalid 与 unsupported 仍精确分类并原子 withholding | ISO specialization 是唯一语义来源 |
| OB-02 | Generated storage/view | CLR value-wrapper 无法协变，不能实现 inherited aggregate getter | 一个最窄 mutable storage + live read-only element projection | 一个 physical Part 21 slot，无复制 mutation surface |
| OB-03 | Schema-bound journey | 无法 hydrate/write M-06 schema | 合法值 read/edit/validate/write/reread；宽域专有值原子失败 | descriptor、validation、writer 与现有 runtime 边界不变 |
| OB-04 | Deployment | package/AOT 未证明 M-06 | actual packed trimmed Native AOT consumer 执行代表性 M-06 旅程 | 静态生成、无 Analyzer runtime asset |

<!-- acceptance-case: AC-01 -->
### AC-01 — M-06 合法性与 unsupported 边界确定

```gherkin
Scenario: 分类 aggregate closed-SELECT element specialization
  Given focused schemas 覆盖 direct entity 或 closed SELECT 到严格更窄 closed SELECT、identity-equal flattened coverage、未覆盖 leaf、non-entity/extensible leaf、metadata 变化和 nested aggregate
  When binder 与 generation plan 分析重声明
  Then strict subset/subtype replacement 的 M-06 生成，identity-equal coverage 保持 M-02 equivalent mapping，widening/unrelated 以 STEP21EXP002 拒绝，non-entity/extensible/nested/metadata-change 类别以 STEP21EXP005 拒绝，全部 source-located 且无 partial output
```

<!-- acceptance-case: AC-02 -->
### AC-02 — 一个 mutable storage 提供实时无损 inherited view

```gherkin
Scenario: 通过 inherited aggregate domain 观察 narrowed SELECT storage
  Given concrete entity 持有 ARRAY LIST BAG SET 的 narrowed closed-SELECT aggregate
  When consumer mutation 最窄 aggregate 并从 inherited interface 重复枚举
  Then projected view 保持 bounds flags order multiplicity slot state 与 selected entity identity，实时反映 mutation，且没有 inherited mutation 成员或第二份 storage
```

<!-- acceptance-case: AC-03 -->
### AC-03 — schema-bound round trip 保持 narrowed SELECT domain

```gherkin
Scenario: 读取写出合法 M-06 值并拒绝宽域专有值
  Given canonical Part 21 fixtures 分别只含 narrowed leaves 和只满足 inherited element domain 的 entity
  When read edit validate write reread
  Then 合法旅程保持 SELECT leaf、entity identity、共享引用和物理参数数量，非法 read 不发布模型且非法 write 输出零 bytes
```

<!-- acceptance-case: AC-04 -->
### AC-04 — topology、现有 mapping 与普通 surface 保持兼容

```gherkin
Scenario: 组合 M-06 与 optional tightening、chain 和 diamond redeclarations
  Given compatible chain/equivalent diamond、incomparable diamond、M-05 组合及不含 specialization 的批准 schemas
  When 生成并比较 public API、diagnostics 与 round-trip behavior
  Then 唯一 most-specific domain 使用一个 slot，incomparable topology 原子拒绝，M-01 至 M-05 和普通 surface 不变
```

<!-- acceptance-case: AC-05 -->
### AC-05 — packed trimmed Native AOT 路径保持静态

```gherkin
Scenario: 发布含 M-06 的 actual-package consumer
  Given 从候选 TedToolkit.Step21 package 恢复的干净 consumer 使用 focused M-06 schema
  When Release trimmed Native AOT publish 并执行成功和原子失败旅程
  Then 全部断言成功且无 attributable trim/AOT warning、reflection、dynamic discovery 或 Analyzer runtime asset
```

## Constraints and risks

- ISO 10303-11 的 redeclaration contract：新 domain 必须与 inherited domain 相同或为 specialization；
  aggregate 本身的 kind/bounds/flags 不变，element relation 递归判定。
- ISO 10303-21：重声明不新增 subtype physical parameter，writer 仍按 inherited physical slot 投影一次。
- [`ADR-0008`](../../adr/ADR-0008-covariant-aggregate-views-and-singular-inverse-validation.md) 与
  [`generated-entity-hierarchy.md`](../../conformance/generated-entity-hierarchy.md) 的一个 most-specific storage、
  原子 diagnostic 与无损 SELECT projection 继续治理本 change。
- M-06 leaf coverage 先 flatten closed SELECT 并按 bound symbol identity 去重；每个 narrowed leaf 必须等于或
  subtype 于 original coverage，且至少删除一个 original coverage 或进行严格 subtype replacement。
  identity-equal flattened sets 保持现有 M-02 equivalent mapping；新增未覆盖 leaf 是 ISO-invalid，
  non-entity/extensible/nested/metadata-change 仍是明确 unsupported。
- SELECT 是 generated value wrapper，不能依赖 CLR generic covariance。projected view 可由 generated private
  adapter 或 schema-neutral runtime adapter 实现，但 public contract 只承诺 read-only、live、metadata/identity
  preserving；不得缓存元素快照或暴露反向 mutation。
- Hydration 先按 physical inherited domain 绑定，再在发布模型前构造/验证 narrowed SELECT leaf；writer 和
  direct references 必须观察同一最窄 storage，不能通过 view 丢失 alternative 或 entity identity。
- Recovery: stable release 前可整体回退 M-06；发布后 generated surface、diagnostic partition 或 view semantics
  的破坏性变化必须按兼容性政策分类。
- Escalation triggers: extensible/unknown SELECT、non-entity leaf、nested aggregate、metadata changes、public
  mutable projection、第二份 storage、grammar/wire mapping、runtime reflection/discovery 或 AP-specific branch。

| External operational handoff | Owner | Completion evidence | Required before change closure? |
| --- | --- | --- | --- |
| None | N/A | N/A | No |

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: PRE-01 source=../P1-express-explicit-attribute-specialization/change.md contract=AC-01 -->
<!-- change-prerequisite: PRE-02 source=../P1-express-explicit-attribute-specialization/change.md contract=AC-03 -->
<!-- change-prerequisite: PRE-03 source=../P1-express-explicit-attribute-specialization/change.md contract=AC-04 -->
| ID | Required input or guarantee | Source change outcome | Required readiness evidence |
| --- | --- | --- | --- |
| PRE-01 | M-01 至 M-05 的 legality、invalid/unsupported diagnostic partition 与唯一 most-specific composition baseline | `../P1-express-explicit-attribute-specialization/change.md`, AC-01 | Source contract is Completed on the selected Git baseline |
| PRE-02 | direct entity/SELECT projection、chain/diamond topology 与一个 most-specific logical storage baseline | `../P1-express-explicit-attribute-specialization/change.md`, AC-03 | Source contract is Completed on the selected Git baseline |
| PRE-03 | 四 aggregate kinds 的一个 mutable storage、kind-specific covariant read-only view、mutation visibility 与一个 physical slot baseline | `../P1-express-explicit-attribute-specialization/change.md`, AC-04 | Source contract is Completed on the selected Git baseline |

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: 一次 bounded delivery 扩展 specialization classification、aggregate projected
  view、entity/descriptor projection与 focused compatibility/package evidence。
- Other real start conditions or resource prerequisites: .NET 10 SDK、本地可恢复依赖、Windows x64 Native AOT。
- Likely touchpoints (non-binding): generated type resolver、entity projection/emitter、aggregate view support、
  descriptor hydration/projection、direct references、focused generator/round-trip/packed-consumer tests和 conformance 文档。
- AP214 conditional consumer boundary: recovery baseline `18a95b13e6bf9b049c273fbf14706a92b4153677` 的
  `schemas/ap214/AP214E3_2010.exp` 中 `kinematic_frame_background_representation.items` 与
  `text_string_representation.items` 两个 `SET [1:?]` redeclarations。
- Private implementation choices left open: adapter 位于 generated code 或 schema-neutral runtime、是否按 getter
  创建轻量 view、内部 projection helper 组织，只要满足 live/read-only/one-slot/identity contract。

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-02 purpose=acceptance shape=component -->
<!-- primary-proof: AC-03 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-04 purpose=regression shape=contract -->
<!-- primary-proof: AC-05 purpose=journey shape=end-to-end -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | exact legal/invalid/unsupported matrix、diagnostic tuple 与 atomic withholding | 运行 focused binder/generator classification tests |
| AC-02 | Primary | 四 aggregate kinds 的 live projection、metadata、identity、mutation visibility 与 one-slot surface | 运行 generated component/API/XML contract matrix |
| AC-03 | Primary | legal read/edit/write/reread 与 broad-only read/write atomic rejection | 运行 canonical schema-bound round-trip matrix |
| AC-04 | Primary | chain/diamond/M-05 composition、M-01 至 M-05、ordinary snapshots 与 diagnostics 不变 | 运行 specialization compatibility与完整 compiler baseline tests |
| AC-05 | Primary | actual packed consumer trimmed Native AOT 执行 M-06 matrix | pack 至 isolated source，publish/run并审计 warnings/dependency graph |
| AP214 consumer gate | Conditional | `kinematic_frame_background_representation.items` 与 `text_string_representation.items` 不再产生 aggregate specialization diagnostic | 在 AP214-001 recovery baseline `18a95b13e6bf9b049c273fbf14706a92b4153677` 重跑 focused full-schema generation gate并逐项断言两个 qualified declarations |

<!-- section: completion-criteria -->
## Completion

AC-01 至 AC-05 必须绑定同一 exact candidate；public API/XML、descriptor、round-trip、diagnostic、完整 regression
与 actual-package Native AOT 证据一致；AP214 focused consumer gate确认本 change 的两个声明被 schema-neutral
支持；独立 implementation review通过；无 external operational handoff。完成后 AP214-001 只消费该已完成
outcome，并继续负责其余 compiler diagnostics。
