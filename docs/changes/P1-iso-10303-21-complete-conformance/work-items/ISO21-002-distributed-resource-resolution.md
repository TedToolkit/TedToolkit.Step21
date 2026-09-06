# ISO21-002: 交付 distributed resource resolution

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-002 -->

<!-- approval-source: user-explicit-approve-and-continue-2026-09-06 -->

## Outcome

在调用方显式提供资源与配额的边界内，完整解析 Part 21 local/external references、
directory content 与 compressed archive content，并将外部 entity/value 纳入相同的 schema、identity 和 validation 规则。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: per-read resource/base-URI/limit capabilities、resource identity/cache lifetime、clause-10 resolution、Annex A.4/A.5 archive/directory transport 和 API/diagnostic baselines。
- In scope: relative/absolute URI、fragment/UUID/entity/value target、other-format conversion hook、nested resources、missing target、clause-10.2 null cycle、ZIP root `ISO-10303.p21`、subsidiary content and no-disk extraction.
- Non-goals: implicit HTTP/filesystem access、machine cache、CMS trust、Part 22 repository、streaming/lazy entity proxies or AP-specific resolvers.
- Likely touchpoints (non-binding): read options/capability contracts、resource graph state machine、archive reader、hydration、in-memory integration fixtures.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| ISO21-001 Verified | Stable anchor/occurrence identities and URI/value mappings | ISO21-001 completion evidence |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-04 | Owns | External/directory/ZIP resolution, identity, null-cycle and schema behavior |
| AC-02, AC-06, AC-09 | Supports | Supplies class-2 transport, external populations and resource-security boundary |

<!-- work-item: delivery-constraints -->
## Constraints

- Follow ADR-0009: no implicit I/O or resolver-supplied bound entities; runtime owns URI/archive/anchor/schema semantics.
- Standard reference cycles resolve to null; archive recursion, provider re-entry and quota exhaustion are distinct atomic failures.
- Preserve model publication atomicity and do not create persistent proxies or reverse the runtime-to-generated dependency direction.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-04 purpose=boundary shape=integration -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-04 | Primary | In-memory local/external/directory/ZIP matrices resolve exact entity/value identity, null and schema outcomes without implicit I/O | Run the focused `DistributedResourceResolutionTests` Release integration suite |
| Resource/API compatibility | Conditional | Existing inputs needing no resource capability keep identical class-1 behavior and additive API classification | Run core Release tests and public API snapshot checks |

<!-- work-item: definition-of-done -->
## Done

- AC-04 passes for positive, missing, cycle, type/schema, directory and archive partitions.
- The verified resource graph and external-structure input are supplied to ISO21-004 and ISO21-007.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate revision, APIs, fixtures, resource identities, archive forms, limit settings, result/diagnostic counts,
exact proof commands and the supplied external-resolution contract.

## Risks and implementation notes

ZIP parsing is an untrusted-input boundary; this item proves standard behavior while ISO21-007 owns the integrated adversarial quota matrix.
