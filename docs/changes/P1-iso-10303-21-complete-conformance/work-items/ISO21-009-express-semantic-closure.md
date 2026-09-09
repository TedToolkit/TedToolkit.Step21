# ISO21-009: 交付验证可达的 Part 11 语义闭包

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-009 -->

<!-- approval-source: user-approved-authorized-iso-source-derived-profile-revision-2026-09-09 -->

## Outcome

依据用户提供且有权授权 AI 使用的 ISO 10303-11:2004 文档或相关条款摘录，生成不复制标准原文的
条款—语义—测试追踪表，并使 Analyzer 对 Part 21 映射和 schema conformance 可达的 types、inheritance、
redeclarations、constants、functions、procedures、rules 与 algorithm statements 完整绑定、静态生成和执行，
不跳过任何可达语义家族；独立审查负责验证追踪表覆盖和实现/测试充分性。

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
| Authorized ISO source | User-supplied ISO 10303-11:2004 document or relevant excerpts that the user is entitled to authorize for AI use; the supplied scope is sufficient to determine every validation-reachable Part 11 family | User-supplied local path plus explicit AI-use authorization |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-07 | Owns | Complete clause-12 and validation-reachable Part 11 mapping/execution closure |
| AC-01, AC-02 | Supports | Supplies constant, class-3 and schema-conformance proof to final closure |

<!-- work-item: delivery-constraints -->
## Constraints

- Derive a non-verbatim profile that records, for every validation-reachable semantic family, its clause ID, valid result, UNKNOWN/error/boundary behavior, result type/category/bounds and neighboring-invalid examples; do not commit copyrighted standard prose or the supplied licensed source unless the user separately authorizes redistribution.
- Every semantic family in the derived profile has a clause-bound positive and neighboring-invalid corpus partition; reachable unsupported behavior rejects generation rather than being skipped.
- Preserve EXPRESS three-valued logic, result types, aggregate category/bounds/order/uniqueness, evaluation order, scoping and source-located diagnostics exactly as established from the authorized source.
- Keep generated code direct, shared, deterministic and AOT-ready; no runtime interpreter, reflection discovery, AP branch or generated dependency on compiler packages.
- Full AP schemas supplement but never replace focused semantic-family fixtures; no AP/B-rep/PMI behavior may enter the implementation contract.
- A fresh independent review must verify the derived profile's source coverage, clause traceability, implementation correctness and test adequacy before ISO21-009 may be integrated.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-07 purpose=acceptance shape=contract -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-07 | Primary | Independently reviewed semantic-family manifest and corpus cover every validation-reachable row derived from the authorized source; valid schemas generate and execute exact results while neighboring-invalid/reachable-unsupported cases fail deterministically | Run the focused compiler conformance corpus and generated-mapping contract suite in Release |
| Generator/AP compatibility | Conditional | Existing compiler baseline, public generated APIs and AP203/AP214/AP242 full baselines remain green without adding AP-specific logic | Run all `Express*`, AP schema baseline, package and reproducibility tests in Release |
| Native AOT | Conditional | Generated semantic execution remains reflection-free and the packed consumer publishes and runs under Native AOT | Run the packed consumer and `build/verify-native-aot.ps1` |

<!-- work-item: definition-of-done -->
## Done

- AC-07 passes with a machine-readable semantic-family manifest, independent source-coverage review and zero derived-profile row skipped or unsupported.
- Generated/public compatibility, all maintained schema packages and Native AOT pass; the verified manifest is supplied to ISO21-008.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate revision; authorized-source identity/digest and user authorization without redistributing licensed text;
derived-profile identity/digest; independent coverage-review conclusion; semantic-family counts; valid/invalid corpus
counts; generated/API deltas; diagnostics; commands/results; AP baseline/reproducibility results; Native AOT identity;
and the manifest supplied to ISO21-008.

## Risks and implementation notes

This remains the largest compiler boundary. Missing or ambiguous authorized-source material blocks only the affected
semantic family; it must not be guessed, silently skipped or represented as full ISO conformance. The existing
implementation inventory is evidence about current behavior, not a substitute for the authorized normative source.

## Partial delivery evidence — 2026-09-09

- Source search found the 255-page ISO 10303-11:2004 catalogue record and two lawful 13-page previews. The previews
  contain the contents, foreword and introduction, but not the normative text needed to derive constant evaluation or
  the remaining validation-reachable semantic families. Those families are therefore left pending: they are not
  implemented from inference, waived, or counted as conforming.
- Public EXPRESS Language Foundation material identifies ISO 10303-11:2004 Edition 2, shows the four aggregate kinds,
  and explicitly presents the `QUERY(variable <* source | predicate)` construct. This bounded source evidence permits
  repair of the already-isolated direct aggregate-source defect, but does not substitute for the complete semantic
  profile required by this work item's Done criteria.
- Candidate `cc777b5` changes the private emitter so absence of the optional schema-aware defined-type resolver is not
  misclassified as a non-aggregate carrier. Projection is still required when a present resolver positively identifies
  a non-aggregate carrier; generated code remains direct, shared and AOT-ready.
- Proof on the candidate: the two `ISO21WorkItem=ISO21-009` tests changed from 2/2 failed to 2/2 passed; the full Release
  unit suite passed 710/710; the Release integration suite passed 35/36 with only the opt-in external-download test
  skipped; `build/verify-native-aot.ps1` reported `PACKED_AOT_OK` and `NATIVE_AOT_PACKAGE_PROOF_OK` for `win-x64`.
- A public JIS reproduction states that JIS B 3700-11-1996 translated ISO 10303-11:1994 without changing its technical
  content or standard form. Its clauses 13.1 and 13.2 supply bounded semantics for null and ALIAS statements, and its
  Annex A supplies their grammar. The current candidate therefore adds no-op null execution, scoped direct/entity/
  attribute ALIAS lowering, inferred alias types, and deterministic rejection for constant sources, leaked names and
  not-yet-implemented indexed aliases. This is partial Edition-1-derived evidence only; Edition-2 additions and the
  remaining validation-reachable families stay pending.
- A separately located ISO 10303-11:2004 excerpt covers clauses 13.1 through 13.9.3. Clause 13.4 directly establishes
  first-TRUE CASE selection, FALSE/UNKNOWN/non-value non-selection, `OTHERWISE` for an indeterminate selector, and
  no action when no label matches and `OTHERWISE` is absent. The CASE candidate adds TRUE-only guarded comparison,
  explicit indeterminate-label handling, once-only selector evaluation and an indeterminate path through otherwise
  statically exhaustive closed-enumeration cases. No semantics outside the excerpt are inferred from it.
- CASE proof changed the new focused test from one generator exception to pass, then covered the optional closed-
  enumeration fall-through neighbor. The candidate passes 4/4 `ISO21WorkItem=ISO21-009` tests and all 133
  `ReachableRuleTests`; the preceding ALIAS/null candidate also passed the full 711-test Release suite and the complete
  Release solution build with zero warnings or errors.
- The same ISO 10303-11:2004 excerpt explicitly requires that a REPEAT body shall not modify its implicitly declared
  NUMBER control variable (13.9.1(f)). The binder now rejects direct assignment, mutation through a procedure `VAR`
  parameter, and mutation through an ALIAS of the control variable before source generation, using the stable
  `EXPRESS-BIND-REPEAT-VARIABLE-MUTATION` reason. ALIAS origins are stored only for declared aliases and traversed
  without per-check collections. The focused red test first demonstrated that all direct and `VAR` cases were
  previously accepted; the corrected candidate passes 5/5 `ISO21WorkItem=ISO21-009` tests and all 134
  `ReachableRuleTests`. The complete Release unit suite passes 713/713, including the checked-in AP203, AP214 and
  AP242 binding and generated-surface baselines.
- Clause 13.8 requires actual procedure parameters to agree with the declared formal count, order and assignment-
  compatible types. The binder now rejects missing and excess actual parameters using
  `EXPRESS-BIND-PROCEDURE-ARGUMENT-COUNT`, instead of allowing the reachable-plan fallback to classify the entire
  algorithm as unsupported. The focused red test captured both prior fallbacks; the corrected candidate passes 6/6
  `ISO21WorkItem=ISO21-009` tests and all 135 `ReachableRuleTests`. Type compatibility remains pending because the
  located 2004 excerpts expose the 12.11 heading but not its normative body; the older JIS translation is used only as
  supporting evidence and is not assumed to cover Edition-2 SELECT changes.

Source identities:

- <https://www.iso.org/standard/38047.html>
- <https://webstore.ansi.org/preview-pages/ISO/preview_ISO%2B10303-11-2004.pdf>
- <https://www.ps-ent-2023.de/fileadmin/prod-preview/ISO_10303-11_2004_shortversion.pdf>
- <https://www.expresslang.org/languages/express>
- <https://www.expresslang.org/docs/documents/express-pretty/document.html>
- <https://kikakurui.com/b3/B3700-11-2002-01.html>
- <https://www.elecenghub.com/NewSamples/ISO/174364973/ISO-10303-11-2004-2.pdf>
- <https://www.elecenghub.com/NewSamples/ISO/174364973/ISO-10303-11-2004-1.pdf>
