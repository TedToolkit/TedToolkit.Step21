# ISO21-009: 交付验证可达的 Part 11 语义闭包

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-009 -->

<!-- approval-source: user-approved-recommended-iso21-005-split-and-continue-2026-09-09 -->

## Outcome

依据标准负责人独立编写且允许 AI 使用的 ISO 10303-11:2004 语义结果清单，使 Analyzer 对 Part 21 映射和
schema conformance 可达的 types、inheritance、redeclarations、constants、functions、procedures、rules 与
algorithm statements 完整绑定、静态生成和执行，不跳过任何可达语义家族。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: validation-reachable Part 11 bound IR、dependency closure、static emitter、constant evaluation、complete complex mapping and source-located unsupported diagnostics.
- In scope: all simple/aggregate/defined/enumeration/select types、inheritance/evaluated sets、attribute redeclarations、constants、functions/procedures/rules、validation-reachable statements/built-ins including QUERY, and internal/external complex mapping decisions required by Part 21.
- Non-goals: copying ISO prose into the repository、general-purpose EXPRESS invocation/interpreter、unreachable program execution、EXPRESS-X、EXPRESS-G、AP/B-rep/PMI semantics or runtime schema reflection.
- Likely touchpoints (non-binding): compiler bound IR and closure、expression/statement emitters、schema descriptor generation、constant hydration、semantic-family manifest、focused corpus and generated API baselines.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| ISO21-001 Verified | Stable physical occurrence/constant-name contract and generated lookup seam | ISO21-001 completion evidence |
| ISO21-005 Verified | Stable schema-supplied short-name and Part 21 physical-mapping metadata | ISO21-005 completion evidence |
| Authorized semantic profile | Independently authored, AI-usable checklist covers every validation-reachable Part 11 family with clause ID, valid result, UNKNOWN/error/boundary behavior, result type/category/bounds and neighboring-invalid examples | User-supplied repository path plus explicit usage authorization |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-07 | Owns | Complete clause-12 and validation-reachable Part 11 mapping/execution closure |
| AC-01, AC-02 | Supports | Supplies constant, class-3 and schema-conformance proof to final closure |

<!-- work-item: delivery-constraints -->
## Constraints

- Every semantic family in the authorized profile has a clause-bound positive and neighboring-invalid corpus partition; reachable unsupported behavior rejects generation rather than being skipped.
- Preserve EXPRESS three-valued logic, result types, aggregate category/bounds/order/uniqueness, evaluation order, scoping and source-located diagnostics exactly as specified by the authorized profile.
- Keep generated code direct, shared, deterministic and AOT-ready; no runtime interpreter, reflection discovery, AP branch or generated dependency on compiler packages.
- Full AP schemas supplement but never replace focused semantic-family fixtures; no AP/B-rep/PMI behavior may enter the implementation contract.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-07 purpose=acceptance shape=contract -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-07 | Primary | Semantic-family manifest and corpus cover every authorized-profile row; valid schemas generate and execute exact results while neighboring-invalid/reachable-unsupported cases fail deterministically | Run the focused compiler conformance corpus and generated-mapping contract suite in Release |
| Generator/AP compatibility | Conditional | Existing compiler baseline, public generated APIs and AP203/AP214/AP242 full baselines remain green without adding AP-specific logic | Run all `Express*`, AP schema baseline, package and reproducibility tests in Release |
| Native AOT | Conditional | Generated semantic execution remains reflection-free and the packed consumer publishes and runs under Native AOT | Run the packed consumer and `build/verify-native-aot.ps1` |

<!-- work-item: definition-of-done -->
## Done

- AC-07 passes with a machine-readable semantic-family manifest and zero authorized-profile row skipped or unsupported.
- Generated/public compatibility, all maintained schema packages and Native AOT pass; the verified manifest is supplied to ISO21-008.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate revision, authorized profile identity/digest, semantic-family counts, valid/invalid corpus counts,
generated/API deltas, diagnostics, commands/results, AP baseline/reproducibility results, Native AOT identity and the
manifest supplied to ISO21-008.

## Risks and implementation notes

This remains the largest compiler boundary. Missing or ambiguous profile rows block only the affected semantic family;
they must not be guessed, silently skipped or represented as full ISO conformance.
