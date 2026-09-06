# AP242-001: 固定 baseline 并交付预编译包

<!-- work-item-format: 2 -->
<!-- work-item-id: AP242-001 -->

<!-- approval-source: user-explicit-approval-2026-09-04 -->

## Outcome

检入可离线重现且有完整来源/许可证明的固定 AP242 MIM LF schema，并交付只需引用
`TedToolkit.Step21.Ap242` 即可使用的 descriptor、生成类型和候选 NuGet 包。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: AP242 schema/provenance、独立项目/NuGet 包、唯一
  descriptor、完整 public API snapshot、README 与 package-only consumer。
- In scope: 固定 N11521 source/hash/license；仅用共享 schema-neutral generator 生成；验证代表性继承、
  可空性、聚合、成员顺序和完整 API；明确同一 AP242 `.exp`/`AdditionalFiles` 与预编译包互斥。
- Non-goals: 不加入 runtime registry/discovery、AP facade、第二套 model/reader/writer，不实现 fixture journey、
  AOT closure、NuGet.org 发布或 stable release。
- Likely touchpoints (non-binding): `schemas/ap242/`、`src/TedToolkit.Step21.Ap242/`、solution、API/provenance
  contract tests、packed-consumer mode。

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| None | Approved parent fixes STEPcode revision, source/hash/license identity, `net10.0`, prerelease version and runtime range | Parent change constraints |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-01 | Owns | Package-only compile/run and runtime-asset boundary |
| AC-02 | Owns | Fixed source identity, descriptor fidelity and complete public API |
| AC-08 | Owns | README/template mutual exclusion for duplicate AP242 input |
| AC-05, AC-06, AC-07 | Supports | Supplies verified package/source/API inputs to closure |

<!-- work-item: delivery-constraints -->
## Constraints

- Keep the core runtime schema-neutral and preserve custom distinct EXPRESS Analyzer consumption.
- Only schema-neutral EXPRESS/compiler corrections are allowed; AP242-specific runtime branches are forbidden.
- Candidate version remains `1.0.0-*`; runtime dependency remains `[1.0.0,2.0.0)`.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-01 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-02 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-08 purpose=acceptance shape=contract -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Isolated package-only consumer compiles/runs with no `.exp`, Analyzer or RoslynHelper runtime asset | Pack to a local feed, restore/build/run the AP242 mode offline |
| AC-02 | Primary | Source hashes, descriptor, generated surface and public API match the approved baseline | Run focused AP242 provenance/descriptor/API contract tests |
| AC-08 | Primary | Packaged README and supported template exclude duplicate AP242 schema input while preserving distinct custom schemas | Inspect package/template and run the custom-schema consumer regression |

<!-- work-item: definition-of-done -->
## Done

- AC-01, AC-02 and AC-08 pass from checked-in inputs; source/license/provenance and API snapshot are complete.
- The verified package assembly and consumer boundary are supplied to AP242-002 and AP242-003.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record the exact candidate, changed artifacts, source/license hashes, generated surface/API result, package dependency
graph, proof commands/counts, resource prerequisites, and supplied outputs.

## Completion evidence

- Verified on stable `TedToolkit.Step21.Ap242` `1.0.0`, `net10.0`, runtime range `[1.0.0,2.0.0)`.
- Fixed STEPcode revision `9baa5dadaa1dcfcdc623220d865d36d61ea351e9`; upstream canonical-LF SHA-256
  `E7E93CF97880FD87D634E4B9EE58400DA0A1BE6C06A1DE76EC13807ECDC15CCB`; exact patched build input
  `221222ED7F92873D8A1BBDDAE569ED72C87730E09F56226A92E3F108FC9EB7A0`. `COPYING`, `AUTHORS`, and
  `INTENT.md` canonical-LF hashes remain recorded in `schemas/ap242/PROVENANCE.md`.
- Complete public API snapshot passed with SHA-256
  `7402DBECCCF19940E0ADCDE0494DD6FAF488A875DBB9AE1E25C81C2C59B09B21`; the full baseline suite passed
  19/19, including descriptor, provenance, source, compatibility, diagnostics, withholding, and API contracts.
- The stable package-only integration proof passed 1/1 and audited no schema source, Analyzer/RoslynHelper runtime
  asset, duplicate AP242 input, or schema-package dependency edge. The distinct custom-schema regression remained
  covered by the affected generator suites.
- Large SELECT representation is shared without changing its public surface: `classification_item` fell from 211
  private alternative fields and about 2.36 MB of source to one payload field and about 0.25 MB. Total AP242
  generated source fell from 220,661,524 to 190,443,559 bytes and the schema assembly from 67,813,376 to
  49,609,216 bytes.

## Risks and implementation notes

The AP242 schema is larger than AP214; generator corrections must retain every supported rule and be proven first on
focused synthetic regressions, then on the fixed full schema.
