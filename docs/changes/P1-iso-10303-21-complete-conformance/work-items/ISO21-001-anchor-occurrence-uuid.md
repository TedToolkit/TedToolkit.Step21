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
| Existing class-1 API | Conditional | Existing simple read/write, identity and public API snapshots are unchanged except approved additive surface | Run `dotnet test tests/TedToolkit.Step21.Tests/TedToolkit.Step21.Tests.csproj -c Release --no-restore` |

<!-- work-item: definition-of-done -->
## Done

- AC-03 primary proof and the class-1/public-API conditional proof pass.
- The verified anchor/value/occurrence model is documented and supplied to ISO21-002, ISO21-004 and ISO21-006.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate revision, changed artifacts, AC-03, proof purpose/shape, exact commands, positive/neighbor-invalid counts,
diagnostic/API changes, AOT-relevant dependencies, and the supplied anchor/occurrence contract.

## Risks and implementation notes

Constant entity/value occurrences resolve through generated EXPRESS metadata; the runtime must not become schema-aware to represent their physical names.
