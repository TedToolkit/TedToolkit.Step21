# 支持 EXPRESS 显式属性数据类型特化

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: behavior-change -->
<!-- change-status: in-progress -->
<!-- delivery-shape: single -->

- Priority: P1
<!-- approval-source: user 2026-08-27 -->
<!-- candidate-binding: none -->

<!-- section: goal-rationale -->
## Goal and rationale

使用 TedToolkit.Step21 Analyzer 的 .NET 消费者能够生成、编辑并往返本 change 明确支持的
ISO 10303-11 合法显式属性数据类型特化，同时仍只有一个继承物理 slot。较宽和较窄的 generated
interface 必须无损观察同一逻辑值；聚合重声明还必须观察同一 concrete mutable aggregate 实例。

当前 generator 对任何改变 generated value type 的显式重声明统一返回 `STEP21EXP005`。AP214 基线由此
产生 72 个诊断，覆盖实体子类型、封闭 SELECT 特化、`NUMBER` 到 `INTEGER`/`REAL` 以及同-kind
entity-valued aggregate 特化。只修复其中一个 CLR 形状不能解除真实 schema 阻塞，也会把 CLR 可表示性
错误地当成 ISO 合法性。

<!-- section: scope -->
## Scope and non-goals

- In scope:
  - 在 binder/generation plan 中先按 ISO 10303-11:2004 9.2.3.4 与 9.2.7 区分数据类型特化，只有下述
    `M-01` 到 `M-05` 进入本次成功映射；不合法或本次未支持的合法形式均原子拒绝。
  - 验证 qualified redeclaration 确实指向当前 entity 继承的原 physical slot；可解析但非 supertype/origin 的
    qualifier 不能降级生成新 slot。
  - 以一个最窄 mutable storage 生成 direct entity、封闭 entity-valued SELECT、精确 numeric 和
    same-kind aggregate 特化；inherited interface 只提供无损 getter projection。
  - 增加由现有 concrete aggregate 直接实现的 `IExpressArray<out T>`、`IExpressList<out T>`、
    `IExpressBag<out T>`、`IExpressSet<out T>` 四个只读协变 view。
  - 让 construction、hydration、mutation、direct references、validation、descriptor projection 和 writer
    共同观察一个物理 slot，并保持 ISO 10303-21 中显式重声明不增加物理参数的编码边界。
  - 更新 public API/XML documentation、generated compatibility snapshots 与 conformance 文档，并证明实际
    package consumer 和 trimmed Native AOT 路径。
- Non-goals:
  - 不宣称实现 ISO 10303-11 的全部 attribute specialization；不支持的合法形式继续有界失败。
  - 不新增 attribute kind 改变；不支持 aggregate kind/bounds/ARRAY OPTIONAL/UNIQUE 或 LIST UNIQUE 改变、
    nested aggregate element 特化、value/defined-type WHERE implication 证明、
    extensible SELECT specialization 或非 entity-valued aggregate element 特化。
  - 不新增 AP203/AP214/AP242 特判、schema registry、runtime discovery、反射或动态代码。
  - 不改变 grammar、普通非特化 attribute 的 generated surface、现有 aggregate mutation/validation 时机，
    或 derived/inverse 的 storage/wire mapping。
- Compatibility or deliberately preserved behavior:
  - 类型等价的重声明继续使用现有 mapping；只改名的重声明继续由 inherited 与 renamed getter 观察同一 slot。
  - 现有 explicit-to-DERIVE redeclaration、其 Part 21 `*` marker 和 validation behavior 保持不变；inverse
    redeclaration 不在本 change 中新增成功或失败类别。
  - 当前成功生成且不含本次特化的 schema 继续返回相同 concrete property types；四个 view 是 additive runtime API，
    不能触发普通 aggregate property 的全局 surface rewrite。
  - 特化 concrete class 的 public mutable property 使用最窄类型；较宽 inherited interface getter 不获得 mutation。
  - read/write 继续原子；不兼容 narrowed domain 的输入不得发布部分模型或 library-produced bytes。

<!-- section: behavior-contract -->
## Behavior contract

| Mapping | Accepted source relation | Generated/storage contract |
| --- | --- | --- |
| `M-01` | direct entity domain 从原 entity 收窄为其相同类型或传递 subtype | 最窄 entity interface storage；较宽 getter 做 identity-preserving upcast |
| `M-02` | direct closed entity-valued domain 收窄为一个兼容 entity，或收窄为 closed SELECT 且每个 flattened leaf 都是原 entity/closed SELECT domain 的相同类型或 subtype，且 leaf 集合为真子集 | 最窄 entity/SELECT storage；generated exhaustive adapter 只 upcast 或按已选 leaf 无损重建较宽 SELECT wrapper，底层 entity identity 不变 |
| `M-03` | direct `NUMBER` 收窄为 `INTEGER` 或 `REAL` | 最窄 `BigInteger`/`RealValue` storage；较宽 getter 精确创建保留同一 numeric alternative 的 `NumberValue`，不得经 binary floating point |
| `M-04` | ARRAY/LIST/BAG/SET kind 相同，全部 bounds 和 kind flags 文本规范化后等价，且 element 从 entity 收窄为其相同类型或传递 subtype | 一个最窄 concrete mutable aggregate storage；全部较宽 getter 经对应 covariant view 返回同一 CLR object |
| `M-05` | explicit attribute 从 `OPTIONAL` 收紧为 required，且 domain 类型等价或同时满足 `M-01` 至 `M-04` | 最窄 required storage；原 inherited optional getter 无损返回同一值；required 不得放宽为 OPTIONAL |

| Rejected partition | Required result |
| --- | --- |
| widening、unrelated domain、空/增加且不受原 domain 覆盖的 SELECT leaf、required 到 OPTIONAL | source-located `STEP21EXP002` + `EXPRESS-BIND-INVALID-REDECLARATION`，说明不满足 ISO specialization，schema 无任何 generated output |
| aggregate kind、bounds、ARRAY OPTIONAL/UNIQUE、LIST UNIQUE 改变 | source-located `STEP21EXP005`；即使某形式可能是 ISO 合法特化，本次也明确标为 unsupported，不能借 covariance 接受 |
| nested aggregate、scalar/defined/value/SELECT 关系超出 `M-01` 至 `M-05`、需要 WHERE-domain implication 的关系 | source-located `STEP21EXP005`，说明 ISO-legal-but-unsupported 或未证明的 mapping 类别，schema 无 partial output |
| qualified origin 不是当前 entity 的 supertype、member 不是从该 origin 继承的 physical slot、或 redeclaration chain 回指错误 slot | source-located `STEP21EXP002` + `EXPRESS-BIND-INVALID-REDECLARATION`，schema 无 partial output |

对一个最终 generated entity 中汇合的同一 physical slot，所有 explicit redeclarations 必须按上述 supported
specialization relation 形成一个 total order，并存在唯一 most-specific domain equivalence class。链式收窄、两条
diamond path 收窄为等价 domain、或一条 path 进一步收窄另一条 path 均选择该 most-specific storage；两个互不
可比的合法收窄仍以 source-located `STEP21EXP005` 原子拒绝，不能依遍历顺序选 storage 或复制 slot。

<!-- behavior-change: OB-01 -->
| ID | Observable boundary | Current | Expected | Preserved |
| --- | --- | --- | --- | --- |
| OB-01 | EXPRESS redeclaration legality | binder 不分析兼容性，generator blanket-reject 改变 value type 的重声明 | binder/plan 只让 `M-01` 至 `M-05` 进入生成；ISO-invalid 为 STEP21EXP002，合法但未支持为 STEP21EXP005 | ISO schema 是语义来源；unsupported 不猜测 |
| OB-02 | Generated logical slot | 不同 CLR property type 无法同时实现 inherited/redeclared interface | 最窄 mutable storage + 无损 inherited getter projection，writer 仍只投影原 inherited physical parameter | renamed/equivalent redeclaration 与 physical order 不变 |
| OB-03 | Aggregate identity | invariant mutable aggregate 无法表达 element narrowing | concrete aggregate 实现 kind-specific covariant read-only view，全部 interface getter 引用相等 | bounds/order/multiplicity/optional-slot/uniqueness 与 mutation 不变 |
| OB-04 | Deployment | 未证明新 mapping 的 package/AOT 边界 | packed consumer 在 trimmed Native AOT 中执行 representative specialization round trip | runtime schema-neutral、静态、无 Analyzer runtime dependency |

<!-- acceptance-case: AC-01 -->
### AC-01 — ISO 合法性与本次支持矩阵互不混淆

```gherkin
Scenario: 分类显式属性重声明
  Given focused schemas 分别覆盖 M-01 至 M-05、类型等价、widening、unrelated、SELECT leaf 替换/增删、required-to-OPTIONAL、aggregate metadata、nested、WHERE-constrained、错误 qualified origin，以及 compatible chain/equivalent diamond/incomparable diamond
  When binder 与 generation plan 分析每个 qualified inherited slot
  Then 类型等价、M-01 至 M-05 和有唯一 most-specific equivalence class 的组合生成；ISO-invalid/origin-invalid 样本以 STEP21EXP002 + EXPRESS-BIND-INVALID-REDECLARATION 拒绝，合法但 unsupported/incomparable 样本以 STEP21EXP005 拒绝，全部 source-located 且无 partial output
```

<!-- acceptance-case: AC-02 -->
### AC-02 — public aggregate view 只读、协变并保留 EXPRESS metadata

```gherkin
Scenario: 通过较宽 element interface 观察 mutable aggregate
  Given 每种 concrete aggregate 含其 bounds、order/index、multiplicity、optional-slot 或 uniqueness state
  When 同一对象转换为对应 IExpressArray/List/Bag/Set 的较宽协变 view
  Then view 与 concrete object 引用相等、元素和 metadata 相同，且 public view 没有接受 T 或执行 mutation 的成员
```

<!-- acceptance-case: AC-03 -->
### AC-03 — direct entity、SELECT 与 numeric 特化无损共享一个逻辑 slot

```gherkin
Scenario: 编辑较窄 concrete property 并从 inherited interface 观察
  Given M-01 entity、M-02 entity/closed-SELECT、M-03 NUMBER-to-INTEGER/REAL、M-05 optional-to-required，以及 compatible chain/diamond 的最小 schemas
  When consumer 构造或编辑最窄 property 并读取每个 inherited/redeclared interface
  Then entity identity、SELECT selected leaf、任意精度 numeric value/alternative 和 required presence 均无损一致，每个 topology 选择唯一 most-specific storage，且 descriptor 与 writer 只观察一个 inherited physical slot
```

<!-- acceptance-case: AC-04 -->
### AC-04 — 四种 aggregate element 特化保持同一 mutable 实例

```gherkin
Scenario: subtype specialization of ARRAY LIST BAG SET entity elements
  Given M-04 的四种最小 schemas
  When concrete class mutation 该最窄 aggregate 并从 inherited/redeclared interfaces 重新枚举
  Then 所有 getter 引用同一对象、各自 element type 正确、mutation 全部可见，且 physical parameter 数量仍为一
```

<!-- acceptance-case: AC-05 -->
### AC-05 — hydrate、validate、write、reread 保持 narrowed domain

```gherkin
Scenario: 往返合法值并拒绝只满足原 domain 的值
  Given 覆盖 M-01 至 M-05 的 canonical Part 21 fixtures
  When read edit validate write reread，并另行读取一个满足原 domain 但违反 narrowed domain 的 fixture
  Then 合法旅程保持等价语义和引用关系，非法旅程在发布模型前返回 positioned schema/binding evidence，且 writer 失败时输出零 bytes
```

<!-- acceptance-case: AC-06 -->
### AC-06 — 普通 surface 与失败边界保持兼容

```gherkin
Scenario: 比较现有成功 schema 和相邻 unsupported specialization
  Given 当前批准的 runtime/generated API snapshots、普通 aggregate schemas 与 existing redeclaration diagnostics
  When 使用候选 runtime 和 Analyzer 重复生成并编译
  Then 普通 snapshots 和 concrete property types 不变，invalid/unsupported 样本保持各自 STEP21EXP002/005 原子边界，renamed/equivalent、optional-to-required 和 existing explicit-to-DERIVE round trip 行为不变
```

<!-- acceptance-case: AC-07 -->
### AC-07 — packed trimmed Native AOT consumer 保持静态可用

```gherkin
Scenario: 发布包含代表性 specialization 的实际 package consumer
  Given 从候选 TedToolkit.Step21 package 恢复的干净消费者使用 M-01 至 M-05 focused schema
  When Release trimmed Native AOT publish 并执行 AC-03 至 AC-05 的成功旅程
  Then 程序成功且无 attributable trim/AOT warning、reflection/dynamic discovery 或 Analyzer runtime asset
```

## Constraints and risks

- Governing standards and records:
  - ISO 10303-11:2004 9.2.3.4 与 9.2.7：重声明保持一个 inherited attribute，其新 domain 必须相同或为原 domain 的 specialization；本 change 只声明上述有证据的实现子集。
  - ISO 10303-21 的 explicit redeclaration encoding 边界：重声明不新增 subtype physical parameter，值按原 supertype slot 表示。
  - [`ADR-0008`](../../adr/ADR-0008-covariant-aggregate-views-and-singular-inverse-validation.md) 与
    [`schema-bound-round-trip.md`](../../architecture/schema-bound-round-trip.md)：aggregate specialization 必须使用同一 concrete storage 的 kind-specific covariant read-only view。
  - [`architecture.md`](../../principles/architecture.md) 与 [`engineering.md`](../../principles/engineering.md)：ISO/EXPRESS 是 public semantics 来源；unsupported mapping 必须明确、原子且可追溯。
- Public view contract 固定为：
  - `IExpressArray<out T> : IEnumerable<T>`：`LowerIndex`、`UpperIndex`、`Count`、`IsOptional`、
    `IsUnique`、get-only EXPRESS-indexed `this[int]`、`IsSet(int)`、`Validate(string)`；
  - `IExpressList<out T> : IReadOnlyList<T>`：`LowerBound`、`UpperBound`、`IsUnique`、`Validate(string)`；
  - `IExpressBag<out T> : IReadOnlyCollection<T>` 与 `IExpressSet<out T> : IReadOnlyCollection<T>`：
    `LowerBound`、`UpperBound`、`Validate(string)`。
- C# covariance 只适用于 reference type argument；因此 `M-04` 明确限于 generated entity interface elements。
- `M-02` 先递归 flatten closed SELECT 并按 bound symbol identity 去重。每个 narrowed leaf 必须等于或传递
  subtype 于至少一个 original leaf；任一未覆盖 leaf 是 ISO-invalid widening/unrelated。domain strictness 在删除
  original coverage 或至少一个 leaf 被 strict subtype 替换时成立，因此“相同 leaf 数量、每个 leaf 分别换成 strict
  subtype”是可接受 specialization；identity-equal flattened sets 走现有 equivalent mapping。未知/extensible leaf
  原子失败，不使用字符串、runtime `TYPEOF` 猜测或反射。
- `M-03` 必须经 `NumberValue.FromInteger`/`FromReal` 的精确 alternative projection；不得使用 `double`、截断或舍入。
- Hydration 必须按原 physical supertype domain 解析后验证 narrowed domain；不能先发布较宽对象再在 validation 中发现类型错误。
- Native AOT proof 必须使用实际 package；project reference 或仅成功编译不能替代 publish/run。
- Recovery: stable release 前可整体回退候选 mapping/view；发布后 public view、generated surface 或 diagnostic identity 的删除/改变必须按兼容性政策分类。
- Multi-redeclaration composition 使用同一 bound-symbol specialization relation；“唯一 most-specific”按 domain
  equivalence class 判断，不按声明遍历顺序。incomparable classes 即使各自对 origin 合法，也保持 STEP21EXP005。
- Escalation triggers: 需要接受本矩阵外 specialization、改变 existing explicit-to-DERIVE/optional-to-required
  行为、修改普通 generated surface、public mutable inherited view、第二份 slot/storage、runtime discovery/reflection、
  grammar/wire mapping、现有 NumberValue semantics 或 validation ABI。

| External operational handoff | Owner | Completion evidence | Required before change closure? |
| --- | --- | --- | --- |
| None | N/A | N/A | No |

<!-- section: start-conditions -->
## Start conditions

<!-- change-prerequisite: none -->

None. Ready from the approved baseline.

<!-- section: delivery-brief -->
## Delivery brief

- Outcome and target delivery area: 一次 bounded delivery 更新 binder legality partition、generated entity/storage projection、runtime aggregate views、descriptor read/write/validation 与 focused conformance evidence。
- Other real start conditions or resource prerequisites: .NET 10 SDK、本地可恢复依赖、Windows x64 Native AOT toolchain；AP214 全量分类 proof 可能运行超过五分钟，focused matrix 是主证明，AP214 source change 负责完整大 schema gate。
- Likely touchpoints (non-binding): EXPRESS bound type/redeclaration analysis、entity generation plan/emitter、aggregate contracts/concrete types、descriptor hydration/projection、direct references、public API/XML snapshots、focused generator/round-trip/packed-consumer tests与 `docs/conformance/`。
- Private implementation choices left open: adapter helper 的内部组织、plan/emitter decomposition 与缓存，只要不扩大 M-01 至 M-04、保持一个 slot、无损投影和静态/AOT 路径。

<!-- section: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-02 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-03 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-04 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-05 purpose=acceptance shape=end-to-end -->
<!-- primary-proof: AC-06 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-07 purpose=acceptance shape=end-to-end -->
| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | M-01 至 M-05、SELECT replacement、错误 origin、chain/diamond 的精确 success/rejection matrix、diagnostic tuple 与 atomic withholding | 运行 focused binder/generator legality/composition matrix，断言每个样本的 STEP21EXP002/005、internal code/message/location 和 output count |
| AC-02 | Primary | 四种 concrete aggregate 实现正确只读协变 metadata contract 且引用相等 | 运行 runtime contract、reflection surface、API snapshot 与 XML documentation checks |
| AC-03 | Primary | entity/SELECT/numeric/required inherited getter 与 compatible chain/diamond 无损观察唯一最窄 storage 和一个 physical slot | 运行 focused generated hierarchy + descriptor projection integration tests，包含超大 INTEGER、精确 REAL 和多路径 identity |
| AC-04 | Primary | ARRAY/LIST/BAG/SET specialization 编译、identity-equal 且 mutation-visible | 运行四 kind generated interface/concrete identity matrix |
| AC-05 | Primary | 合法 read/edit/write/reread 等价；narrowed-domain mismatch 不发布模型/bytes | 运行 canonical fixture positive/negative semantic round-trip matrix |
| AC-06 | Primary | 普通 snapshots、concrete aggregate surfaces、renamed/equivalent、optional-to-required、explicit-to-DERIVE 与各自 diagnostic 原子边界兼容 | 运行 existing hierarchy/complex-mapping/aggregate/generator baseline suites并比较批准 snapshots |
| AC-07 | Primary | 实际 packed runtime consumer Release trimmed Native AOT publish/run 成功且依赖图静态 | pack 至 isolated source，offline restore/publish/run focused consumer并审计 `.deps.json` 与 publish diagnostics |
| Shared regression gate | Conditional | binder/generator/runtime 修改未破坏完整现有语义 | 运行 compiler baseline、Release solution build、完整 unit 与 integration suites |

<!-- section: completion-criteria -->
## Completion

AC-01 至 AC-07 必须绑定同一 exact candidate；public API/XML、generated snapshots、conformance docs、package/AOT
证据一致；完整 regression 与独立 implementation review 通过；无 external handoff。完成后 AP214 source change
只能消费本文明确验证的 outcome，不能把 AP214 的其它 generator gap 偷渡进本 change。

## Implementation evidence

- Provisional implementation commit: `229eb6b43bdeb34d540ef7ab87c6cc3a138a512f`.
- M-01 through M-05 focused generation, projection, diagnostics, topology, aggregate identity, and atomic
  read/write tests pass, including overlapping SELECT alternatives selecting the unique most-specific leaf.
- Public API snapshot and complete runtime XML documentation checks pass; the compiler manifest changed only
  the expected AP203 schema-descriptor hash while retaining 492 generated sources and zero diagnostics.
- Release solution build passes with 0 warnings and 0 errors; the complete unit/generator suite passes 363/363.
- The complete integration suite passes 7/7 enabled tests with the one opt-in network corpus test skipped.
- The actual packed consumer passes deterministic package generation and executes representative specialization
  read/edit/validate/write/reread. A pre-candidate trimmed `win-x64` Native AOT run emitted no attributable warning
  and reported `NATIVE_AOT_PACKAGE_PROOF_OK` with a 3,584,000-byte executable; the exact-commit AOT rerun remains
  pending until verified formatter-only worktree noise can be removed safely.
