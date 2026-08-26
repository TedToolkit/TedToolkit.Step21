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

### Candidate completion evidence

| Evidence | Result |
| --- | --- |
| Candidate boundary | Baseline `3b4e4b1`; implementation `2f18b16`; exact reviewed range `3b4e4b1..2f18b16`. The only changed artifacts are the package-only AP203 fixture consumer and its integration assertions. |
| AC-06 edit and semantic signature | The concrete generated `Product.Name` is changed to `TedToolkit AP203 OCCT box 10x20x30 mm - edited`, followed by `Validate`, complete `Write`, and descriptor-bound reread. Before-write and after-reread signatures compare entity/category counts, edited product value, all sorted Cartesian coordinate tuples, sorted SI-unit names, shared edge-endpoint vertex degrees, shape-definition-to-representation identity, and representation-to-solid identity; physical instance numbers and formatting are excluded. The observed journey retains 200 entities, 6 faces, 12 edges, 8 vertices, 27 points, metre/radian/steradian units, and eight degree-3 shared vertices. |
| AC-07 invalid condition and atomicity | On the reread graph, the product `frame_of_reference` and one face `bounds` aggregate are cleared. Explicit validation returns 9 failures including the product and face lower-bound codes. Write throws `ExchangeStructureWriteValidationException`; its ordered `(code,path)` sequence exactly equals explicit validation, and the destination remains 0 bytes. |
| Primary proof | `dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release` passed all 6 enabled tests; the one network-backed external-corpus test was skipped by its opt-in contract. The package-only consumer output fixes `AP203_ROUND_TRIP_OK` and `AP203_INVALID_EDIT_REJECTED failures=9 output-bytes=0`. |
| Regression proof | `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release` passed 344/344 with 0 failed and 0 skipped. No reusable runtime/generator defect was exposed, so no production implementation changed. |
| Review and AP203-006 handoff | Read-only review of `3b4e4b1..2f18b16` concluded Ready with no blocking or important findings and no design deviation. `Ap203FixtureProgram` now contains the exact read, typed navigation, edit, validate, write, reread, semantic comparison, invalid validation, zero-byte rejection, and unsupported-extension journey to execute under the AP203-006 packed Native AOT/offline proof. |

## Risks and implementation notes

AP203 rule execution can expose an unsupported EXPRESS feature. Fix an already approved standards
semantic gap only when it stays inside the current contract; a new public concept or a broader
validation claim requires redesign.
