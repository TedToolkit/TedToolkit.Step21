# ISO21-004: 交付 Annex E schema populations

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-004 -->

<!-- approval-source: user-explicit-approval-2026-09-06 -->

## Outcome

完整执行 Annex E 的 reference-validity 和 schema-population determination methods，包括调用方显式提供且由
runtime 验证的 schema-neutral SDAI-domain-equivalence 关系。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: population declaration/model、section boundary/include-all-compatible/include-referenced-instance methods、interface/domain-equivalence validity、timestamp/verification state and diagnostics.
- In scope: local/multi-section/multi-schema/external structures、ordered deterministic populations、missing/asymmetric/non-transitive/contradictory equivalence metadata and cross-schema type checks.
- Non-goals: ISO 10303-22 objects or repository、name/shape heuristics、database populations、AP compatibility tables.
- Likely touchpoints (non-binding): header model、schema descriptors、validation population builder、domain-equivalence capability and multi-schema tests.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| ISO21-001 Verified | Stable local occurrence/value identities | ISO21-001 completion evidence |
| ISO21-002 Verified | Stable external exchange-structure identity and schema-bound reference result | ISO21-002 completion evidence |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-06 | Owns | All Annex E validity and population determination behavior |
| AC-02, AC-08, AC-09 | Supports | Supplies class behavior, Annex-F population objects and population-security boundary |

<!-- work-item: delivery-constraints -->
## Constraints

- Domain equivalence is caller-supplied data, validated as an equivalence relation, and required only when the input selects that method.
- Do not depend on Part 22 or infer equivalence by names/schema shapes; retain deterministic ordering and complete validation failures.
- Preserve existing same/multi-schema results that already conform.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-06 purpose=acceptance shape=component -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-06 | Primary | Every Annex E method computes the exact population and rejects missing or invalid equivalence declarations atomically | Run the focused `SchemaPopulationConformanceTests` Release component suite |
| Existing multi-schema behavior | Conditional | Current same-schema and multi-schema read/validation journeys retain their approved outputs | Run existing `SameSchemaDataSectionReadTests` and `MultiSchemaReadTests` in Release |

<!-- work-item: definition-of-done -->
## Done

- AC-06 and multi-schema regressions pass with documented public inputs and diagnostics.
- The verified population model is supplied to ISO21-006, ISO21-007 and ISO21-008.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate revision, method/equivalence partitions, expected and actual populations, diagnostics, commands,
API changes and the population contract supplied to dependents.

## Risks and implementation notes

The standard names an SDAI concept, but this product accepts only the minimal schema-neutral relation data needed to execute Annex E.
