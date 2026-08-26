# AP203-001: Fix and compile the reproducible AP203 schema baseline

<!-- work-item-format: 2 -->

- Approval: 用户于 2026-08-24 在本任务中明确要求开始执行；批准的完整工作项映射内容 SHA-256 为 `FFEEE360860BF3B5A891B61B3D91C9D047667C300712C7DCB46BDF8BE3006249`。

## Outcome

The pinned STEPcode AP203 Amendment 1 long-form schema is checked in with provenance, license, and
checksum evidence and compiles deterministically into the representative public shape required by
the parent contract. Standards-derived compiler corrections belong here; AP203-specific hand-written
domain types do not.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area: fixed AP203 source assets and the existing EXPRESS compiler/generator boundary.
- In scope: ingest the exact approved upstream file, record redistribution evidence, correct root-cause
  parser/binder/emitter gaps exposed by it, and verify representative inheritance, nullability,
  aggregates, selects, enumerations, and member order.
- Non-goals: the NuGet package shell, OCCT fixtures, runtime schema discovery, or invented AP203 APIs.
- Likely touchpoints (non-binding): a repository schema-assets location, analyzer generation code,
  generated-fidelity tests, and source/provenance documentation.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| Redistribution evidence | STEPcode commit and the applicable BSD-3-Clause evidence can be recorded beside the source | Pinned source and license links in the parent change |

If redistribution evidence cannot be established, stop before checking the schema into a deliverable package.

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Verified input or primary-proof intent |
| --- | --- | --- |
| AC-02 | Owns | Generated descriptor and representative types match the pinned EXPRESS source |
| AC-12 | Supports | Supplies checked-in, checksummed schema input that requires no build-time network access |

<!-- work-item: delivery-constraints -->
## Constraints

- The source identity is STEPcode commit `9baa5dadaa1dcfcdc623220d865d36d61ea351e9`,
  `data/ap203/ap203.exp`; do not silently substitute an edition or modified schema.
- Preserve the existing generated namespace and nominal schema name contracts.
- Correct reusable EXPRESS semantics at their existing boundary; add no AP203-only parser or generator path.
- Private type, method, and test organization remains open to the implementer.

<!-- work-item: proof-plan -->
## Proof

| Contract or gate | Evidence purpose | Execution shape | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-02 | Acceptance and regression | Contract | The descriptor name and selected entity/value projections match source declarations, including ordering and aggregate kinds | `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release` |
| Schema compilation | Structural verification | Component | The complete pinned schema compiles without generator diagnostics or hand-written generated output | `dotnet build TedToolkit.Step21.slnx --configuration Release` |

<!-- work-item: definition-of-done -->
## Done

- The pinned source, license evidence, upstream identity, checksum, and modification statement are present.
- Full-schema compilation and representative fidelity proof pass without warnings or errors.
- Any compiler correction is standards-derived and protected by the narrowest useful regression test.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, ingested asset checksum, actual changed artifacts, AC-02 evidence
purpose and execution shape, commands, observable assertions, discovered/passed/failed/skipped
counts, and the verified schema input supplied to AP203-002 and AP203-004.

## Risks and implementation notes

The first full AP203 compile may expose several symptoms of one compiler gap. Prefer one root-cause
correction over AP203 name lists, special cases, or extra abstractions. A required new public runtime
concept or a schema-baseline change returns to change design.
