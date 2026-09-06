# ISO21-005: 交付 EXPRESS mapping 与可达语义闭包

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-005 -->

<!-- approval-source: user-explicit-approval-2026-09-06 -->

## Outcome

使 Analyzer 对 Part 21 clause 12 的每个 EXPRESS 映射家族和 schema conformance 可达的 ISO 10303-11:2004
约束闭包完整生成、读取、验证、写出并重读，不跳过可达语义。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: clause-12 mapping descriptors/emission、complete complex mapping、short names、constant occurrences、reachable types/inheritance/redeclarations/functions/procedures/rules/statements and source-located unsupported diagnostics.
- In scope: all simple/aggregate/defined/enumeration/select types、entity mapping、attributes/inverse/derived/local rules、schema/constant/rule mappings and every Part-11 semantic family reachable from validation.
- Non-goals: general-purpose EXPRESS invocation/interpreter、unreachable program execution、EXPRESS-X、AP/B-rep/PMI semantics or runtime schema reflection.
- Likely touchpoints (non-binding): compiler bound IR、dependency closure、descriptor/emitter、complex mapping、conformance corpus and generated API baselines.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| None | Approved contract limits Part 11 execution to Part 21 mapping/schema-conformance reachability | Parent AC-07 and AP-004 Draft direction |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-07 | Owns | Complete clause-12 and validation-reachable Part-11 mapping/execution closure |
| AC-01, AC-02 | Supports | Supplies mapping rows and class/schema proof to final conformance closure |

<!-- work-item: delivery-constraints -->
## Constraints

- Every supported semantic family has a clause-bound positive and neighboring-invalid corpus partition; reachable unsupported behavior rejects generation rather than being skipped.
- Keep generated code direct, deterministic and AOT-ready; no runtime interpreter, reflection discovery, AP branch or generated dependency on compiler packages.
- Full schema baselines supplement but do not replace focused ISO clause fixtures.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-07 purpose=acceptance shape=contract -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-07 | Primary | Clause-manifest corpus covers every required mapping/reachable semantic family and all valid cases round-trip with zero skipped dependency | Run the focused compiler conformance corpus and generated-mapping contract suite in Release |
| Generator/AP compatibility | Conditional | Existing compiler baseline, public generated APIs and AP203/AP214/AP242 full baselines remain green | Run all `Express*`, AP schema baseline and reproducibility tests in Release |

<!-- work-item: definition-of-done -->
## Done

- AC-07 passes with a machine-readable mapping/semantic-family manifest and zero reachable unsupported row.
- Generated/public compatibility and all three maintained schema packages pass; the manifest is supplied to ISO21-008.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate revision, clause/Part-11 family counts, valid/invalid corpus counts, generated/API deltas, diagnostics,
commands, AP baseline/reproducibility results and supplied manifest identity.

## Risks and implementation notes

This is the largest compiler item; additions must remain shared across schemas and preserve the prior memory/source-size reductions.
