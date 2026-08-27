# 执行 validation-reachable EXPRESS singular inverse

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: approved -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: user 2026-08-27 -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

使用 TedToolkit.Step21 Analyzer 的 .NET 消费者能够在 `ExchangeStructure.Validate()` 及复用该路径的
read/write 边界中执行 validation-reachable singular inverse：完整 population 恰有一个 forward-role
candidate 时提供该 entity 值；零个或多个 candidate 时产生一次稳定、可定位、可聚合的 cardinality failure。

ISO 10303-11 的 entity-valued inverse 表示 1:1 inverse relationship，不是 Part 21 physical slot。当前 generator
因可变 candidate graph 可能缺失或多值而统一返回 `STEP21EXP006`。first-wins、公开 mutable inverse property
或验证外异常都会改变 EXPRESS 语义；一概拒绝则阻塞 AP214 的 validation-reachable declarations。

<!-- section: scope -->
## Scope and non-goals

- In scope:
  - 对 reachable WHERE/function control flow 实际求值到的 singular inverse，从本次 validation 的完整静态 population
    按现有 forward-role matching 枚举不同 owner entity identities。
  - 唯一 candidate 作为强类型 entity 值继续 rule evaluation；零/多 candidate 记录一次 declaration-specific
    `INVERSE_CARDINALITY` failure，并让依赖该 unavailable value 的当前 rule 不再产生误导性 WHERE failure。
  - 在一次 descriptor validation invocation 内按 current entity CLR reference identity + inverse declaration identity 去重。
  - 固定 direct、repeated、function-mediated、short-circuited/non-evaluated access 的 count、path、source 和顺序。
  - 更新 generated XML/conformance 文档，并证明实际 package consumer 与 trimmed Native AOT 路径。
- Non-goals:
  - 不生成 public inverse property、mutable collection、physical parameter、hydration storage 或 writer projection。
  - 不在 property assignment、aggregate mutation、registration 或 `DirectReferences` 枚举时主动维护/验证 inverse。
  - 不改变 aggregate-valued SET/BAG inverse 的现有语义，不实现 inverse redeclaration 新类别。
  - 不新增 AP203/AP214/AP242 特判、runtime schema discovery、reflection evaluator、dynamic code 或第二套 validation engine。
  - 不处理 dependency cycles、unsupported algorithm statements、complex construction 或其它 AP214 generator gap。
- Compatibility or deliberately preserved behavior:
  - validation 仍 side-effect-free、aggregate、按输入 population 与 source rule order确定；property/collection mutation 仍 unchecked。
  - structural attribute、defined-type WHERE、entity WHERE、UNIQUE、global RULE 以及 structure relationship failures 的现有相对阶段不变。
  - 未在实际 EXPRESS control flow 中求值的 inverse 不执行查询、不产生 cardinality failure。
  - read/write 继续原子，validation failure 不发布部分模型或 library-produced bytes。

<!-- section: behavior-contract -->
## Behavior contract

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | Reachable singular inverse | generator 返回 `STEP21EXP006` | 唯一 candidate 参与求值；零/多 candidate 进入 aggregate validation | inverse 是 computed relationship，不是 storage |
| OB-02 | Evaluation timing | 无执行语义 | 只在 existing lazy/short-circuit control flow 的实际 access 点查询 | EXPRESS boolean/control-flow semantics 不改 |
| OB-03 | Failure identity/count | 无 runtime failure | 每 validation invocation、current entity、inverse declaration 最多一个稳定 failure | ValidationResult 有序、完整、无异常 |
| OB-04 | Deployment | 未证明新 generated helper 的 AOT 边界 | packed trimmed Native AOT consumer 执行 1/0/many 与 lazy matrix | schema-neutral 静态 descriptor、无反射 |

<!-- acceptance-case: AC-01 -->
### AC-01 — 唯一 forward-role candidate 提供强类型 singular value

```gherkin
Scenario: WHERE 直接或经 function 读取唯一 candidate 的 singular inverse
  Given 完整 validation population 中恰有一个不同的类型兼容 owner entity 以声明 forward role 一次或多次引用 current entity
  When reachable control flow 求值该 inverse
  Then 求值获得同一 candidate entity identity、继续执行 dependent expression，且不产生 INVERSE_CARDINALITY failure
```

<!-- acceptance-case: AC-02 -->
### AC-02 — 零或多个 candidate 各产生一个稳定 cardinality failure

```gherkin
Scenario: 同一 singular inverse 在一个或多个 reachable rules/functions 中被重复求值
  Given population 分别有零个和多个不同的兼容 forward-role owner entities
  When Validate 执行全部 reachable rules
  Then 每个 current-entity/inverse-declaration pair 恰有一个 <SCHEMA>.<DECLARING_ENTITY>.<INVERSE>.INVERSE_CARDINALITY failure，path 指向 current entity 的 generated inverse member，source 指向 inverse declaration，message 包含实际 candidate count 和 role
  And 每个已访问 unavailable inverse 的 dependent rule 不再附加 WHERE failure，其它独立 rules 继续执行
```

<!-- acceptance-case: AC-03 -->
### AC-03 — lazy、short-circuit 与重复访问边界确定

```gherkin
Scenario: 比较 direct、repeated、function-mediated 与 non-evaluated inverse references
  Given TRUE OR inverse-dependent、FALSE AND inverse-dependent、direct repeated access、跨 rule repeated access 和 nested function access 样本
  When Validate 每个模型
  Then TRUE OR 样本无 inverse/WHERE failure，FALSE AND 样本只有原 WHERE failure且不查询 inverse，所有实际 repeated/function accesses 对同一 pair 合计一个 inverse failure
```

<!-- acceptance-case: AC-04 -->
### AC-04 — failure 顺序嵌入现有确定性 validation traversal

```gherkin
Scenario: 同一 population 同时包含 structural、inverse、WHERE、UNIQUE、global RULE 和 relationship failures
  Given 固定 data-section/entity/rule source order 的模型
  When 对相同模型重复调用 Validate 并通过 writer preflight 调用同一路径
  Then failures 完整序列逐项相同：section failures 在前；每个 entity 的 explicit structural/defined-type failures 在其 entity WHERE 前；inverse failure 位于首次实际 access 点并早于该 entity 的后续 rule failures；随后是 UNIQUE、global RULE；relationship failures仍在 schema population failures 后
```

<!-- acceptance-case: AC-05 -->
### AC-05 — read/write 原子边界复用同一 inverse validation contract

```gherkin
Scenario: validated read/write 遇到零或多 candidate singular inverse
  Given 可 hydrate 但 inverse cardinality 无效的 canonical Part 21 model
  When 调用使用 schema validation 的 read boundary 或 writer
  Then 返回/抛出的 ValidationResult 包含与显式 Validate 相同 code/path/source/count/order，reader 不发布模型且 writer 产生零 bytes
```

<!-- acceptance-case: AC-06 -->
### AC-06 — packed trimmed Native AOT consumer 保持静态可用

```gherkin
Scenario: 发布含 singular inverse rule 的实际 package consumer
  Given 从候选 TedToolkit.Step21 package 恢复的干净消费者使用 focused generated schema
  When Release trimmed Native AOT publish 并执行 1/0/many 与 lazy matrix
  Then 全部断言成功且无 attributable trim/AOT warning、reflection/dynamic discovery 或 Analyzer runtime asset
```

## Constraints and risks

- Governing standards and records:
  - ISO 10303-11:2004 9.2.1.3：entity-valued inverse 表示恰好一个使用 current instance 的 forward-role entity；SET/BAG inverse 是不同 aggregate 形状。
  - ISO 10303-21 与当前 architecture：inverse 不占 physical parameter，不参与 hydration storage 或 writer projection。
  - [`ADR-0008`](../../adr/ADR-0008-covariant-aggregate-views-and-singular-inverse-validation.md) 与
    [`schema-bound-round-trip.md`](../../architecture/schema-bound-round-trip.md)：singular inverse 唯一时求值，零/多时通过 aggregate validation 报告。
  - [`reachable-express-rules.md`](../../conformance/reachable-express-rules.md)：reachable closure 必须全执行或原子拒绝；WHERE 只接受 TRUE；顺序与 source traceability 是兼容性表面。
- Candidate enumeration 使用 validation invocation 已选择的完整 ordered population 和现有 `USEDIN` role matching；
  不扫描 CLR properties，也不跨越 descriptor population boundary。cardinality 单位是不同 owner entity 的 CLR
  reference identity：同一 owner 同一 role 的一个或多个 matching occurrences 只贡献一个 candidate；不同 owners
  分别贡献 candidates。该规则不改变 aggregate-valued BAG/SET inverse 的 multiplicity semantics。
- Failure code 固定为 `<SCHEMA>.<DECLARING_ENTITY>.<INVERSE>.INVERSE_CARDINALITY`（全部 uppercase）；path
  固定为 current registration path + `.` + generated inverse member name；source 固定为 inverse declaration span。
- Dedup key 固定为 `(current entity reference identity, inverse declaration bound-symbol identity)`，作用域只是一轮 descriptor
  validation invocation；第一次实际 access 决定 failure 的序列位置，后续 access 复用 unavailable state且不追加 failure。
- Evaluation matrix 固定为：
  - 唯一 candidate：正常执行，dependent rule FALSE/UNKNOWN 时按现有 WHERE contract 报告；
  - 零/多 candidate 且实际 access：报告 inverse failure；本次 rule evaluation 若读取该 unavailable value则不再报告该 rule 的 WHERE failure；
  - short-circuit 未 access：不查询、不缓存、不报告 inverse failure，rule 自己按实际 boolean result 决定 WHERE failure；
  - 同一 entity 的其它未读取该 inverse 的 rules 继续执行并保留 failure。
- Existing order 固定为：structure section failures；按 population input order 的 entity-local validation（每个 entity 先 explicit structural/defined-type，再 inherited/source-ordered WHERE，inverse 在首个 access 点）；随后 UNIQUE、global RULE；最后 structure relationship failures。本文不新增后置排序。
- Native AOT proof 必须使用实际 package；project reference 或仅成功编译不能替代 publish/run。
- Recovery: stable release 前可回退 generated singular helper；发布后 code/path/order/message identity 或 execution timing 改变必须按兼容性政策分类。
- Escalation triggers: public inverse navigation、mutation-time enforcement、aggregate inverse 行为变化、跨 descriptor population、validation ABI/phase reorder、reflection/interpreter、并发语义或本 change 外 EXPRESS construct。

| External operational handoff | Owner | Completion evidence | Required before change closure? |
| --- | --- | --- | --- |
| None | N/A | N/A | No |

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from the approved baseline.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: 一次 bounded delivery 更新 reachable-rule plan/emitter 的 singular inverse expression lowering、generated validation context/dedup state、failure documentation 与 focused package/AOT evidence。
- Other real start conditions or resource prerequisites: .NET 10 SDK、本地可恢复依赖、Windows x64 Native AOT toolchain。
- Likely touchpoints (non-binding): reachable-rule plan/emitter、structural validation emitter/context、generated XML、focused rule/validation/read-write tests、packed consumer 与 `docs/conformance/`。
- Private implementation choices left open: generated helper/result carrier、dedup storage 和 emitter decomposition，只要遵守上述 timing、key、count、order、atomicity 与静态路径。

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=component -->
<!-- primary-proof: AC-02 purpose=acceptance shape=component -->
<!-- primary-proof: AC-03 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-04 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-05 purpose=acceptance shape=end-to-end -->
<!-- primary-proof: AC-06 purpose=acceptance shape=end-to-end -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | 一个 owner 的单次/重复 role occurrences 都返回同一唯一 entity 并正常执行 dependent rule | 运行 focused in-process generated validation tests，断言 owner identity、occurrence partition、rule result 与零 cardinality failure |
| AC-02 | Primary | 0/多个不同 owners 与 repeated access 各 pair 恰一 failure，code/path/source/message/count 固定且无 dependent WHERE duplicate | 运行 focused in-process full-population validation matrix并逐项比较 failures |
| AC-03 | Primary | short-circuit non-access 不查询；direct/cross-rule/function repeat 共享 invocation-local dedup | 运行 instrumented generated-source/behavior contract matrix，断言 exact query/failure counts |
| AC-04 | Primary | mixed failures 的完整序列重复稳定且保持现有 phase order | 对固定 mixed model 重复 Validate 与 writer preflight，逐项比较 code/path/source/message 序列 |
| AC-05 | Primary | invalid inverse read 不发布模型、write 输出零 bytes且 evidence 与 Validate 相同 | 运行 canonical Part 21 read/write negative end-to-end tests |
| AC-06 | Primary | 实际 packed consumer 的 Release trimmed Native AOT 1/0/many/lazy matrix 通过 | pack 至 isolated source，offline restore/publish/run并审计 `.deps.json`/publish diagnostics |
| Shared regression gate | Conditional | reachable-rule/validation 修改未破坏现有规则和 schema-bound journey | 运行 compiler baseline、Release solution build、完整 unit 与 integration suites |

<!-- section: completion-criteria -->
## Completion

AC-01 至 AC-06 必须绑定同一 exact candidate；generated XML/conformance、package/AOT 与完整 regression 证据一致；
独立 implementation review 通过；无 external handoff。完成后 AP214 source change 只能消费本文 singular inverse outcome，
其 dependency-cycle、algorithm 或 complex-construction gap 仍由独立 scope 处理。
