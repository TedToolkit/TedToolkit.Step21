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

## Completion evidence

- Candidate: `31785bbe188249ea20b0d4d22fad3375d1142ad9`, independently reviewed against baseline
  `422e33dd4d885cdfe86e47729d11115aed86b730`; final implementation review concluded `Ready to merge`
  with no blocking or important findings.
- Security identity: `distributed-processor-security.json` contains 16 mechanically bound threat rows and SHA-256
  `9B3726B189B60D790DF55E97AADFB891BDFB1F6C65F9073EF53F78214D2CBC78`. Shared finite defaults cover root input,
  output, URI, signature count/per-envelope/aggregate bytes, CMS signer count, nesting, items and archive-entry bytes.
- Boundary proof: Release `SecurityBoundaryTests` passed 15/15. It covers generic and partially consumed exact
  readers; input/base/provider/converter URI limits before canonicalization/cache keys; resource/archive count, bytes,
  depth, traversal, compression and recursion; provider/converter/signer re-entry; read/write signature count,
  per-envelope/aggregate CMS bytes and signer count; malformed/trust policy; capped whole/entity/Annex-F output;
  Annex-F input/item/depth/URI; zero publication; and normative reference-cycle null semantics.
- Regression proof: resource resolution 26/26, CMS signatures 19/19, Annex F 4/4, schema populations 28/28,
  canonical parameter formatting 1/1, canonical simple writer 4/4, complex mapping 17/17, atomic prewrite 5/5 and
  public API 1/1 passed in Release.
- Build proof: exact production candidate dependency build covering core, Analyzer, Annex F, AP203, AP214, AP242 and
  integration host passed with 0 warnings and 0 errors in 7:09.30. The immediately preceding full solution build also
  passed with 0 warnings and 0 errors in 7:07.78; the final candidate adds only the two reciprocal CMS test branches.
- Supplied contract: ISO21-008 may rely on explicit caller-owned resource, conversion, signing, certificate, time and
  revocation inputs; bounded work before library-controlled materialization/decoding; zero partial model/output; and
  an executable AC-09 threat/default manifest.

## Risks and implementation notes

Adversarial fixtures must remain small and generate expansion/recursion behavior in bounded test-controlled form.
