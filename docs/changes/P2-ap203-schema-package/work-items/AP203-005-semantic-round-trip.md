# AP203-005: Preserve AP203 semantics through edit, validation, write, and reread

<!-- work-item-format: 2 -->

- Approval: 用户于 2026-08-24 在本任务中明确要求开始执行；批准的完整工作项映射内容 SHA-256 为 `FFEEE360860BF3B5A891B61B3D91C9D047667C300712C7DCB46BDF8BE3006249`。

## Outcome

The supported OCCT AP203 graph can be edited through generated mutable types, validated, written,
and reread with the approved semantic signature preserved; invalid edits aggregate stable failures
and produce no output bytes.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area: existing generated validation/writing behavior exercised by the AP203 graph.
- In scope: one meaningful typed edit, semantic-signature comparison, AP203 structural/rule failure
  coverage, and atomic prewrite behavior; correct reusable generator/runtime gaps at their source.
- Non-goals: byte/token/whitespace preservation, a new AP203 model facade, transaction APIs, CAD
  conversion, or exhaustive validation of application-domain intent beyond reachable EXPRESS rules.
- Likely touchpoints (non-binding): AP203 integration tests and existing generated validation,
  mapping, or writer components only when the real graph exposes a root-cause gap.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| AP203-004 | A verified typed fixture graph, stable semantic observations, and documented supported boundary | AP203-004 completion evidence and fixture checksum |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Verified input or primary-proof intent |
| --- | --- | --- |
| AC-06 | Owns | Valid edit/write/reread preserves type, value, identity, and reference semantics |
| AC-07 | Owns | Invalid edits aggregate deterministic validation and atomic-write failures |
| AC-11 | Supports | Supplies the exact complete journey later executed by the packed AOT consumer |

<!-- work-item: delivery-constraints -->
## Constraints

- Continue to use `ExchangeStructure.Validate/Write/Read` and generated mutable types; add no parallel workflow API.
- Semantic comparison excludes physical instance numbering, whitespace, comments, and token spelling.
- Failure ordering and paths remain deterministic; a failed write leaves the destination length at zero.
- Prefer direct test-local observations over a reusable semantic-signature class unless several proofs truly share it.

<!-- work-item: proof-plan -->
## Proof

| Contract or gate | Evidence purpose | Execution shape | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-06 | Acceptance and regression | Integration/contract | Edit, validate, write, and reread succeed and the approved semantic signature and edited value match | `dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release` |
| AC-07 | Acceptance and atomicity | Integration/contract | Invalid edit yields complete deterministic failures; write fails before emitting a byte | `dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release` |
| Affected fast suite | Regression | Component | Existing generated read/write/validation behavior remains green | `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release` |

<!-- work-item: definition-of-done -->
## Done

- AC-06 and AC-07 pass against the fixed AP203 graph.
- The semantic signature is documented by observable fields/relationships and avoids physical formatting.
- Any discovered reusable runtime/generator defect is corrected at its root with proportional regression proof.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, actual changed artifacts, edited field and invalid condition, AC-06/AC-07
commands and observable assertions, discovered/passed/failed/skipped counts, output-length evidence,
and the full journey supplied to AP203-006.

## Risks and implementation notes

AP203 rule execution can expose an unsupported EXPRESS feature. Fix an already approved standards
semantic gap only when it stays inside the current contract; a new public concept or a broader
validation claim requires redesign.
