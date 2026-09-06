# ISO21-003: 交付 CMS signature 能力

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-003 -->

<!-- approval-source: user-explicit-approval-2026-09-06 -->

## Outcome

实现 clause 14 定义的一个或多个 CMS signature sections，使消费者可以在显式时间、证书、
撤销和 acceptance policy 下签署、验证并分别观察结构、密码学和信任结果。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: signature result/state model、per-call verifier/signer/trust/time policy、covered-byte definition、Base64/CMS diagnostics、atomic read/write and API snapshots.
- In scope: malformed、not-evaluated、cryptographically invalid/valid、unknown signer、expired、revoked、untrusted/trusted、multiple signatures and deterministic local certificate fixtures.
- Non-goals: implicit machine certificate store、online revocation/network lookup、certificate issuance、key storage UI or non-CMS signatures.
- Likely touchpoints (non-binding): signature section model、reader/writer buffering、cryptography adapter、test certificates and packed consumer.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| None | ADR-0009 fixes the signature/trust/publication matrix and explicit capability boundary | Accepted ADR-0009 |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-05 | Owns | Exact covered bytes, CMS signing/verification, trust report and publication matrix |
| AC-02, AC-09 | Supports | Supplies class-3 signature behavior and signature attack boundary |

<!-- work-item: delivery-constraints -->
## Constraints

- Malformed syntax/CMS always fails atomically; `NotEvaluated` may publish only a complete model/report; evaluated states obey only the explicit per-call policy.
- Signing without explicit capability fails before destination-visible bytes; no hidden machine/network trust inputs are permitted.
- Use AOT-compatible BCL or bounded optional dependencies and preserve deterministic test time/certificates.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-05 purpose=boundary shape=integration -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-05 | Primary | Every signature state produces the approved report/publication result and signing covers exact standard bytes atomically | Run the focused `CmsSignatureConformanceTests` Release integration suite with checked-in deterministic fixtures |
| Package/AOT boundary | Conditional | Cryptography dependencies do not enter generated schema packages or break core Native AOT publication | Run package dependency audit and `pwsh -File build/verify-native-aot.ps1` |

<!-- work-item: definition-of-done -->
## Done

- AC-05 and conditional package/AOT proofs pass with stable public/diagnostic baselines.
- The verified signature policy/result boundary is supplied to ISO21-007 and ISO21-008.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate revision, exact covered-byte vectors, certificate/time/policy fixtures, state/publication matrix counts,
commands, package graph and Native AOT result.

## Risks and implementation notes

Cryptographic validity and caller trust acceptance are distinct; test names and public results must not collapse them into one Boolean.
