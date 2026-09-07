# ISO21-003: 交付 CMS signature 能力

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-003 -->

<!-- approval-source: user-explicit-approve-and-continue-2026-09-06 -->

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
- Clause 14 evidence uses the public final text as a read-only normative reference: every signature covers the preceding Part 21 alphabet characters, including prior signatures, and CMS is detached/external content encoded with Base64; repository fixtures are independently authored.

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

## Completion evidence

- Verified on 2026-09-07 at candidate and fast-forward integration revision
  `b24584a0be2c21d58372e1968117363301a7f7f4` (baseline
  `5b4bd247f11964d24aef2ea24352219fb7679b66`). A fresh independent delivery-candidate review concluded
  **Ready to merge** with no blocking or important findings and no design deviations.
- Clause-14 coverage uses every preceding Part 21 alphabet character, including earlier signature sections; CMS is
  detached, canonical Base64, and structurally decoded even when verification is not requested. Tests cover multiple
  signatures, excluded control characters, malformed and attached CMS (including zero-length attached content),
  signer-less CMS, and hostile signer mutation with zero destination output.
- The explicit state/policy matrix covers `NotEvaluated`, cryptographically valid and invalid content, trusted and
  untrusted chains, unknown signer, expired, not-yet-valid, and caller-declared revoked signer states. Trust uses only
  caller-supplied roots/additional certificates plus CMS-embedded certificates, explicit time, no downloads, and no
  implicit machine intermediate or root acceptance.
- Complete signature reports retain stable resource identities for the root and every resolved external structure in
  deterministic resolution order, including policy-accepted untrusted results. Malformed signature syntax or CMS in
  any resource fails publication atomically.
- Exact focused integration proof selected 19 `CmsSignatureConformanceTests`, the cumulative public API snapshot, and
  the original four-parameter read-options constructor compatibility test; all `21/21` passed in Release after the
  fast-forward integration.
- Exact-candidate broad proof built the full Release solution with `0` warnings and `0` errors and ran `611` tests:
  `609` passed, with only the two baseline-proven unchanged QUERY-emitter failures at
  `ExpressExpressionEmitter.cs:1327`. Package inspection found `System.Security.Cryptography.Pkcs` direct only in the
  core runtime and transitive in AP203/AP214/AP242.
- The packed Native AOT consumer executed actual certificate creation, CMS signing, writing, rereading and
  verification, producing `PACKED_AOT_OK` and `NATIVE_AOT_PACKAGE_PROOF_OK` for win-x64.
- Supplied contract: ISO21-004 may consume complete per-resource signature and digest-verification results;
  ISO21-007 may compose the explicit trust/capability boundary; ISO21-008 may reuse the package, compatibility and
  Native AOT evidence.

## Risks and implementation notes

Cryptographic validity and caller trust acceptance are distinct; test names and public results must not collapse them into one Boolean.
