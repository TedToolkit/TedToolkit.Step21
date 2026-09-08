# ISO21-004: 交付 Annex E schema populations

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-004 -->

<!-- approval-source: user-approved-revised-delivery-map-2026-09-07 -->

## Outcome

完整执行 Annex E 的 reference-validity 和 schema-population determination methods，包括调用方显式提供且由
runtime 验证的 schema-neutral SDAI-domain-equivalence 关系与参数投影。

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
| ISO21-003 Verified | Stable CMS digest verification and signature-order result | ISO21-003 completion evidence |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-06 | Owns | All Annex E validity and population determination behavior |
| AC-02, AC-08, AC-09 | Supports | Supplies class behavior, Annex-F population objects and population-security boundary |

<!-- work-item: delivery-constraints -->
## Constraints

- Domain equivalence is supplied through an explicit caller-owned provider that declares the relation and projects source physical parameters into the target schema; runtime validates relation closure and every projected target view.
- Do not depend on Part 22 or infer equivalence by names/schema shapes; retain deterministic ordering and complete validation failures.
- Runtime owns allocation and canonical occurrence aliasing for projected strong views; providers cannot replace canonical entities or use reflection/dynamic proxies through the runtime contract.
- Recursive cross-resource projections normalize to the ultimate physical occurrence and hydrate to a deterministic fixed point; transaction fallback restores projection identity, readiness and deferred work together.
- Generated descriptors register one shared, allocation-free physical-type matcher through the additive protected `ConfigureEntityTypeIdentity` infrastructure seam. Large schemas use bounded static shards; generated AP public surfaces remain unchanged.
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

The runtime API disposition for this work item is additive: `SchemaDescriptor.ConfigureEntityTypeIdentity` is a
protected source-generator/custom-descriptor optimization seam and adds no ISO domain concept. Existing subclasses
remain source and binary compatible through the projection fallback; generated AP203/AP214/AP242 public API hashes
must remain unchanged.

## Completion evidence

- Implementation candidate: `059436286a51f2d4afd07cdacb5a9fc0242e5c8b`.
- Independent implementation review: **Ready to merge** against baseline
  `5531365b47c69278d0b0ad7adc37527558b713e7`; no blocking or important candidate findings.
- Annex E/schema-population component proof: 28 passed, 0 failed. This includes every determination method,
  external resources and digests, deterministic cycle failures, recursive cross-schema projected references,
  final physical-occurrence identity, cross-owner canonicalization, and projection-created transaction rollback.
- Compatibility proof: same-schema reads 4/4; multi-schema reads 5/5; AP214 contracts 14/14; AP242 contracts 4/4;
  public API contracts 16/16; compiler generated-source manifest 1/1.
- Full Release solution build: 0 warnings, 0 errors. Main suite: 638/640 passed; the only two failures are the
  unchanged pre-candidate QUERY aggregate-source defects outside ISO21-004. Full integration/package/Native AOT:
  15 passed, 0 failed, 1 explicitly skipped external-network corpus.
- Scale and allocation disposition: generated type identity uses one configured static matcher, performs no entity
  value projection on generated paths, and partitions at 256 sequential branches. Real AP242 generation/compilation
  passes without the former compiler-complexity failure; AP203/AP214/AP242 generated public surfaces are preserved.

## Risks and implementation notes

The standard names an SDAI concept, but this product accepts only the minimal schema-neutral provider needed to execute Annex E: explicit relation data plus schema-neutral physical-parameter projection. It does not expose a Part 22 repository. AP-004 governs the durable preference for shared schema-neutral mechanisms without reducing required ISO behavior; this work item does not create a duplicate principle.
