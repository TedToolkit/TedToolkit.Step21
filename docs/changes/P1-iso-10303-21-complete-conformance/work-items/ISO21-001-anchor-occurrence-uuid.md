# ISO21-001: 交付 anchor、occurrence 与 UUID 语义

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-001 -->

<!-- approval-source: user-explicit-approve-and-continue-2026-09-06 -->

## Outcome

在 schema-neutral runtime 中完整表达并双向映射 Part 21 anchor item、tag、entity/value/constant
occurrence 及 Annex G UUID，使其可解析、编辑、验证、写出并重读。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: `ExchangeStructure` 拥有的 anchor/occurrence/tag 状态、标准值联合、解析与写出边界、稳定诊断和 API snapshot。
- In scope: clause 6.4.4、6.5、9 及 Annex G 的所有形式；重复、非法类型、空值和引用身份；short/long occurrence spelling 的标准区分。
- Non-goals: 外部资源获取、ZIP、CMS、schema population、AP/domain 语义或 Annex F host。
- Likely touchpoints (non-binding): runtime 值/标识类型、raw syntax projection、reader/writer、public API baseline、Part21 fixture tests。

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| None | Approved parent and ADR-0009 define the schema-neutral, caller-owned boundary | Parent change and ADR-0009 |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-03 | Owns | Complete local anchor/occurrence/tag/UUID semantics and round trip |
| AC-02, AC-07, AC-08 | Supports | Supplies the class-3, clause-12 constant/value-name and Annex-F identity model |

<!-- work-item: delivery-constraints -->
## Constraints

- Preserve direct entity identity, model ownership, deterministic output, complete-result publication, existing class-1 APIs and Native AOT readiness.
- Public types use Part 21 terminology and do not embed schema descriptors, resource providers, script engines or AP concepts.
- Normative behavior is derived from ISO 10303-21:2016 clauses 6.4.4, 6.5 and 9 plus Annex G using the public final text as a read-only reference; repository evidence records clause identifiers and independently authored vectors, not copied standard prose.
- Private representation, collection layout and parser-projection organization remain implementation choices.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-03 purpose=acceptance shape=component -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-03 | Primary | Every standard anchor item, tag, occurrence and UUID partition round-trips; neighboring invalid combinations fail atomically with stable paths | Run the focused `AnchorOccurrenceConformanceTests` Release component suite |
| Existing class-1 API | Conditional | Existing simple read/write, identity and public API snapshots are unchanged except approved additive surface | Run the Release TUnit executable through `dotnet run`; this repository's .NET 10/Microsoft.Testing.Platform setup does not use `dotnet test` |

<!-- work-item: definition-of-done -->
## Done

- AC-03 primary proof and the class-1/public-API conditional proof pass.
- The verified anchor/value/occurrence model is documented and supplied to ISO21-002, ISO21-004 and ISO21-006.

<!-- work-item: completion-evidence -->
## Verification result requirements

Verified on 2026-09-07.

- Candidate and fast-forward integration source revision: `5d0a0575f3848a52d9b886a891539bc6e1f2efd1`
  (baseline `983f9afbde827d6e2590d7802ab5f0e43bdfd58f`). A fresh independent delivery-candidate
  review concluded **Ready** with no blocking or important findings.
- Changed runtime/grammar surface: schema-neutral anchor, tag, resource, entity/value/constant occurrence and UUID
  value types; anchor/reference collections and validation; reader/writer binding; generated constant lookup;
  Edition-3 grammar/generated artifacts; additive public API and AP214/AP242/compiler baselines; conformance guides.
- AC-03 primary component proof:
  `dotnet run --project tests/TedToolkit.Step21.Tests/TedToolkit.Step21.Tests.csproj --configuration Release --no-restore --disable-build-servers -- --treenode-filter "/*/*AnchorOccurrenceConformanceTests*/*/*" --minimum-expected-tests 15`
  passed `15/15`. Eight cases contain positive assertions, nine contain neighboring-invalid/editing assertions,
  and two intentionally cover both. The matrix includes every physical item family, empty/nested lists, all four
  occurrence categories, `_`/`_TAG`/`TAG_NAME`, opaque and relative-query URIs, UUID equality, direct local identity,
  public-edit collisions, constants, `.T./.F./.U.` enumeration symmetry, and zero-output atomic failures.
- Focused conditional gates passed: STEP parser file matrix `20/20`; Validation/API `16/16`; schema descriptor
  dispatch `2/2`; ANTLR reproducibility `1/1`; compiler baseline `1/1`; documentation contract `2/2`.
- Exact-candidate `dotnet build TedToolkit.Step21.slnx --configuration Release --no-restore
  --disable-build-servers --maxcpucount:1` passed with `0` warnings and `0` errors. The preceding cumulative candidate's
  full core run completed `565` tests with `563` passing; its only two failures were reproduced on the unchanged
  baseline at `ExpressExpressionEmitter.cs:1327` with `QUERY requires a resolved aggregate source`. The final delta
  was parser/test/documentation-only and passed the exact parser, grammar-generation, compatibility and build gates.
- New stable public-edit diagnostics are `P21.STRUCTURE.ANCHOR.*`, `P21.STRUCTURE.EXTERNAL_REFERENCE.*`, and
  `P21.STRUCTURE.OCCURRENCE.OVERLAP`; parsed failures retain source locations and writes validate the whole structure
  before publishing characters.
- No runtime package, reflection, implicit I/O, AP dependency, or Native-AOT-relevant dependency was added.
- Supplied contract: ISO21-002/004/006 may consume canonical arbitrary-precision occurrence identities,
  direct-object local entity anchors, schema-neutral external occurrence/resource associations, RFC-2396 URI text,
  ordered recursive anchor values/tags, generated constant-category lookup, and Annex-G UUID identity.

## Risks and implementation notes

Constant entity/value occurrences resolve through generated EXPRESS metadata; the runtime must not become schema-aware to represent their physical names.
