# AP242-002: 固定 OCCT fixture 的 AP242 语义旅程

<!-- work-item-format: 2 -->
<!-- work-item-id: AP242-002 -->

<!-- approval-source: user-explicit-approval-2026-09-04 -->

## Outcome

固定 OCCT AP242DIS box fixture 通过候选 package 完成 typed read/edit/validate/write/reread，并让 descriptor
歧义、错误 schema、baseline 外结构和无效编辑沿既有边界原子失败。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: 固定 fixture/provenance/license、package-only semantic
  journey 与 negative/atomicity matrix。
- In scope: 按 parent 固定 revision、box、writer mode、units 和 disabled extras 生成/规范化 fixture；断言
  product、6 faces、12 edges、8 vertices、10×20×30 mm extents、units、identity/shared references；编辑产品名并
  语义重读；覆盖重复 descriptor、错误 schema、未知实体和规则失败的零部分模型/字节行为。
- Non-goals: 不提供 CAD converter，不扩展到 PMI/tessellation/kinematics/colour/layer，不声明完整 conformance。
- Likely touchpoints (non-binding): AP242 fixture assets、integration tests、packed-consumer fixture mode。

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| AP242-001 Verified | Candidate package, descriptor and package-only consumer are stable inputs | AP242-001 verification record |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-03 | Owns | Complete fixed semantic round trip |
| AC-04 | Owns | Stable schema/binding/validation diagnostics and atomic failure |
| AC-07 | Supports | Supplies the exact journey used by AOT closure |

<!-- work-item: delivery-constraints -->
## Constraints

- Preserve the parent fixture source, normalization allow-list, hashes and bounded capability claim.
- Use the existing explicit descriptor and `ExchangeStructure` API; do not create AP242 runtime special cases.
- Report real baseline/fixture edition gaps; do not suppress validation or remove semantic assertions.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-03 purpose=acceptance shape=integration -->
<!-- primary-proof: AC-04 purpose=acceptance shape=integration -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-03 | Primary | Fixed fixture preserves the approved typed semantic signature through edit/write/reread | Run focused AP242 packed-consumer integration journey |
| AC-04 | Primary | Every approved negative case produces stable evidence and zero partial model/output | Run focused AP242 negative/atomicity matrix |
| Shared generator/runtime | Conditional | Any shared correction preserves existing schema-bound behavior | Run affected focused suites and full Release suite when runtime changes |

<!-- work-item: definition-of-done -->
## Done

- AC-03 and AC-04 pass; fixture/provenance/license and bounded capability documentation are checked in.
- The same verified journey is supplied unchanged to AP242-003 Native AOT proof.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate, fixture/generator/license hashes, commands/counts, complete semantic signature, negative evidence,
resource prerequisites, documentation state and AP242-003 handoff.

## Completion evidence

- The fixed OCCT 7.9.3.1 AP242DIS fixture SHA-256 is
  `88DA6C164CC685A881A4A52AC7D0BA90E27D649EE9183810887F1A8930AEFD30`; its unsupported-extension companion is
  `2781891BE750A0B04921B7094AB6908F03B1CA46CC5DA5593FFD620B8D95A2F4`. LGPL-2.1 and OCCT-exception hashes are
  `E237FA56668030E928551DDD60F05DF5FE957F75EAB874BBD017E085ED722E7C` and
  `04580A884EA6CEA294402649FF7B5CBB167D47462D1340A4ED33E550DB10A81B`.
- The stable package integration proof passed 1/1: 170 entities, one product, six faces, twelve edges, eight
  vertices, 27 points, three units, 10 x 20 x 30 mm extents, preserved identity/shared references, edited product
  name, validation, write, and semantic reread.
- Invalid edit produced eight validation failures and zero output bytes. The unsupported entity was rejected with
  `P21-BIND-ENTITY` at line 8, column 6. No validation rule was disabled.
- The exact same journey passed as the stable `win-x64` Native AOT executable, supplying AP242-003 unchanged.
