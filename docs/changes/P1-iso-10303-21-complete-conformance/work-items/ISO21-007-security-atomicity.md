# ISO21-007: 关闭 distributed processor 安全与原子性

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-007 -->

<!-- approval-source: user-explicit-approve-and-continue-2026-09-06 -->

## Outcome

对 external resources、archives、schema populations、signatures 和 Annex-F boundary 执行一致的显式配额、
输入验证与原子发布规则，使恶意 distributed exchange structure 无法获得隐式 I/O、部分模型或部分输出。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: unified limits/policy、quota accounting、path/URI/archive validation、recursion/re-entry handling、signature-policy atomicity and security diagnostics.
- In scope: traversal、ZIP bomb、entry/count/size/depth limits、archive/resource recursion、provider re-entry、malicious URI、invalid signature and failure-before-publication/output matrices.
- Non-goals: clause-10.2 normative reference cycle rejection、network sandbox、OS ACL、key storage、general script sandbox or thread-safe concurrent mutation.
- Likely touchpoints (non-binding): public read/write limits、resource/archive/CMS adapters、transactional staging、security fixture generator and integration tests.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| ISO21-002 Verified | Resource/archive behavior and identity boundary | ISO21-002 completion evidence |
| ISO21-003 Verified | CMS result and acceptance/publication policy | ISO21-003 completion evidence |
| ISO21-004 Verified | Population and domain-equivalence input boundary | ISO21-004 completion evidence |
| ISO21-006 Verified | Annex-F/core isolation boundary | ISO21-006 completion evidence |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-09 | Owns | Integrated resource, archive, trust, quota and publication threat matrix |
| AC-01, AC-02 | Supports | Supplies security/PICS and class-2/3 failure evidence |

<!-- work-item: delivery-constraints -->
## Constraints

- Standard reference cycles remain null semantics; only non-normative archive/provider recursion or exceeded resource limits fail.
- Defaults are finite, public and testable; caller relaxation remains bounded by representable limits and never enables implicit I/O or trust.
- All library-controlled failures occur before complete model/output publication and produce deterministic bounded diagnostics.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-09 purpose=boundary shape=integration -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-09 | Primary | Every threat/limit partition fails deterministically with zero implicit access, partial model or library-produced output, while normative cycles retain null behavior | Run the focused `DistributedProcessorSecurityTests` Release integration suite |
| Existing atomicity | Conditional | Current read/write/validation atomicity suites retain all approved results | Run existing atomic read and prewrite validation suites in Release |

<!-- work-item: definition-of-done -->
## Done

- AC-09 passes with explicit default/custom limit matrices and bounded diagnostics.
- Existing atomicity passes; security evidence and public defaults are supplied to ISO21-008.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate revision, threat/limit partitions, default/custom limits, diagnostics, publication/output observations,
exact commands and supplied security manifest identity.

## Risks and implementation notes

Adversarial fixtures must remain small and generate expansion/recursion behavior in bounded test-controlled form.
