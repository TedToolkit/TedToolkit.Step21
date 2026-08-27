# AP214-003: 固定 OCCT fixture 的 AP214 语义旅程

<!-- work-item-format: 2 -->
<!-- work-item-id: AP214-003 -->

<!-- approval-source: user-explicit-approval-2026-08-27 -->

## Outcome

仓库检入一份来源和生成参数固定的 OCCT AP214IS box fixture，并通过候选 AP214 package 完成
原子读取、typed navigation、编辑、验证、写出和语义重读；descriptor 歧义、错误 schema、
baseline 外结构和无效编辑均沿既有稳定边界原子失败。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: 固定 OCCT `.step` fixture、fixture provenance/license、
  AP214 integration journey 与 negative/atomicity matrix。
- In scope:
  - 使用 OCCT commit `7d2efad9c8a9a57ea96c4c8587134b34dd503cd8`、仓库自有
    `BRepPrimAPI_MakeBox(10.0, 20.0, 30.0)` 原点 box、`STEPControl_Writer`、
    `STEPControl_ManifoldSolidBrep`、`WriteMode_StepSchema_AP214IS` 与 millimetres 生成并检入 fixture。
  - 关闭 surface curves、color、name、layer、properties、metadata 与 material；只规范化 parent change
    明确允许的 header/exporter 字段，并记录 generator/fixture SHA-256 与 OCCT license/exception。
  - 读取 `AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }` fixture，将 product name 改为
    `TedToolkit AP214 OCCT box 10x20x30 mm - edited`，验证、写出并重读。
  - 断言 1 product、6 faces、12 edges、8 vertices、10 × 20 × 30 mm extents、
    millimetre/radian/steradian units、实体类型/数量、实例身份和共享引用关系。
  - 覆盖重复 nominal descriptor、不同 schema、baseline 外实体和违反可达规则的编辑，验证配置歧义在
    消费输入前抛出 `ArgumentException`，其余失败提供稳定 binding/validation 证据且不发布部分模型/字节。
- Non-goals:
  - 不提供 OCCT/CAD object converter，不声明完整 AP214 conformance，也不覆盖 colour/layer、PMI、
    tessellation、kinematics 或 fixture 外能力。
  - 不把 AP214IS fixture OID 当作 package baseline identity，不宣称 AP214IS 输出与固定 2007-DIS baseline 等同。
  - 不改变 OCCT revision、export 参数、geometry、normalization allow-list 或 capability boundary。
- Likely touchpoints (non-binding): AP214 fixture/provenance/license assets、
  `tests/TedToolkit.Step21.IntegrationTests/`、AP214 packed-consumer fixture mode 与 shared atomicity helpers。

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| AP214-002 Verified | 已验证的 AP214 package assembly、唯一 descriptor 与 package-only consumer 边界 | AP214-002 verification record and supplied output |
| Fixed exporter contract | OCCT revision、box input、writer mode、unit、disabled extras、normalization allow-list 与 license hashes 均与 parent change 一致 | parent change Fixed fixture contract |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-03 | Owns | 证明固定 fixture 的 typed read/edit/validate/write/reread 与完整语义签名 |
| AC-04 | Owns | 证明 descriptor/schema/binding/validation 失败的稳定诊断和读写原子性 |
| AC-07 | Supports | 提供可由 Native AOT packed consumer 原样执行的完整 fixture journey |

<!-- work-item: delivery-constraints -->
## Constraints

- Public, persisted, compatibility, security, migration, governing, or preserved behavior:
  - 必须使用 AP214-002 的显式 descriptor 和现有 `ExchangeStructure` API，不得引入第二套 AP214 reader/writer/model。
  - fixture 只证明批准的 B-rep/product/topology/geometry/unit/value/identity/reference 子集；文档不得外推 conformance。
  - 读失败不得发布部分模型；写失败不得产生任何 library-produced partial bytes。
  - source、fixture、OCCT revision/参数、normalization 范围或 edition/variant 改变均须重新批准。
- Private choices deliberately left to the implementer:
  - integration helper 的内部拆分、语义 signature 的比较数据结构和临时输出位置，只要断言完整且不扩大能力声明。

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-03 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-04 purpose=acceptance shape=integration -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-03 | Primary | 固定 OCCT fixture 完成 typed read/edit/validate/write/reread，并保持已批准的名称、extents、units、实体数量、身份和共享引用语义签名 | 运行 `TedToolkit.Step21.IntegrationTests` 中 focused AP214 semantic journey |
| AC-04 | Primary | descriptor 歧义在消费前抛出 `ArgumentException`；不同 schema、baseline 外结构和无效编辑产生稳定 schema/binding/aggregate-validation 证据，且没有部分模型或输出字节 | 运行 focused AP214 negative/atomicity matrix 并检查 diagnostic position（可得时）与零部分输出 |
| Shared runtime regression gate | Conditional | AP214 fixture 覆盖未破坏现有 schema-bound read/write/validation 行为 | 运行受影响的 `TedToolkit.Step21.Tests` focused suites；若修改共享 runtime，再执行完整 Release suite |

<!-- work-item: definition-of-done -->
## Done

- AC-03 与 AC-04 的 primary proof 通过，fixture、provenance、license 和 bounded capability 文档已检入。
- 语义旅程与 negative matrix 均使用 AP214-002 的候选 package boundary，而非测试内替代 schema。
- 已向 AP214-004 提供可原样用于 Native AOT 的 fixture journey 和已验证失败边界。

<!-- work-item: completion-evidence -->
## Verification result requirements

实现交接必须记录 candidate revision、fixture/generator/license/provenance 与测试资产、AC-03/AC-04、
acceptance purpose、integration shape、实际命令、语义签名与 negative-case 结果/计数、OCCT 资源前提、
normalization 范围、文档状态，以及提供给 AP214-004 的完整 journey 入口。可变状态和 candidate 结果只记录在
coordinator-owned orchestration state。

## Risks and implementation notes

- fixture 文本中的可变 exporter 字段只能使用 parent change 的 allow-list 规范化；任何额外改写都会削弱互操作证据。
- 若 fixture 使用了固定 baseline 未支持的结构，应报告真实 binding gap；不得删减语义断言来制造通过结果。
