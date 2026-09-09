# ISO21-005: 交付 Part 21 短名与物理映射输入

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-005 -->

<!-- approval-source: user-approved-revised-iso21-005-query-proof-and-continue-2026-09-09 -->

## Outcome

使 schema 定义文档能够显式、确定地向 Analyzer 提供 Part 21 clause 12 的 entity、defined/select type 和
enumeration-value 短名及物理映射元数据，并由生成代码完成长名/短名读取与 canonical 写出，不引入 AP 语义或
依赖 Part 11 算法求值。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: schema-supplied short-name input、generated descriptor lookup、typed-parameter/entity/enumeration physical keyword selection and source-located collision diagnostics.
- In scope: long-name/short-name bidirectional lookup for entity types, simple defined/enumeration types used by SELECT values, and enumeration values; case/collision/unknown-name validation; canonical writer selection controlled by explicit schema metadata.
- Non-goals: inventing short names、parsing AP-specific annex prose、constant evaluation、QUERY/functions/procedures/rules、general-purpose EXPRESS execution、EXPRESS-X、AP/B-rep/PMI semantics or runtime reflection discovery.
- Likely touchpoints (non-binding): Analyzer options/additional inputs、bound schema metadata、descriptor/emitter、typed-parameter/entity/enumeration codecs、focused generated mapping fixtures and API baselines.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| ISO21-001 Verified | Stable physical occurrence/constant-name contract and generated constant-lookup seam | ISO21-001 completion evidence |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-07 | Supports | Supplies the schema-defined physical-name/mapping metadata consumed by ISO21-009 |
| AC-01, AC-02 | Supports | Supplies short-name rows and class/schema proof to final conformance closure |

<!-- work-item: delivery-constraints -->
## Constraints

- Short names are accepted only from an explicit caller-owned schema-definition input; absence means long-name-only behavior and never heuristic abbreviation.
- Reject duplicate, case-insensitive ambiguous, cross-category-invalid or long-name-colliding declarations before generation.
- Keep generated code direct, shared across schemas, deterministic and AOT-ready; no runtime interpreter, reflection discovery, AP branch or generated dependency on compiler packages.
- The mapping inventory records Part 21 clause identifiers and independently authored fixtures, not copied standard prose.

<!-- work-item: proof-plan -->
## Proof

| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-07 supplied mapping input | Conditional | Explicit entity/type/enumeration short names read and write the same values as long names; absent/invalid/colliding metadata fails deterministically | Run the focused generated short-name mapping contract suite in Release |
| Generator/AP compatibility | Conditional | Existing compiler baseline, public generated APIs and AP203/AP214/AP242 full baselines have zero failures outside the two unchanged QUERY gaps owned by ISO21-009 | Tag exactly those two tests with `ISO21WorkItem=ISO21-009`; run `dotnet run --project tests/TedToolkit.Step21.Tests/TedToolkit.Step21.Tests.csproj --configuration Release --no-restore --disable-build-servers -- --treenode-filter "/*/*/*/*[ISO21WorkItem!=ISO21-009]" --minimum-expected-tests 708`, plus the three AP surface/reproducibility gates in Release |

<!-- work-item: definition-of-done -->
## Done

- The schema-supplied short-name contract and neighboring-invalid matrix pass without heuristic or AP-specific behavior.
- Generated/public compatibility and all three maintained schema packages have zero failures outside exactly two explicitly tagged,
  unchanged QUERY gaps owned by ISO21-009; verified mapping metadata is supplied to ISO21-009 and ISO21-008.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate revision, supported short-name categories, valid/invalid corpus counts, generated/API deltas, diagnostics,
commands, the exact 708-test filtered result, evidence that exactly two ordinary tests carry the ISO21-009 ownership tag,
AP baseline/reproducibility results and the mapping metadata contract supplied to dependents.

## Risks and implementation notes

The physical mapping layer must remain schema-neutral. Application-protocol packages may supply metadata, but the Analyzer and runtime must not encode AP-specific name tables or semantics. The ISO21-009 property makes ownership of two pre-existing QUERY gaps machine-verifiable; it is not a skip, disablement or conformance waiver.
