# AP203-004: Read the fixed OCCT AP203 sample atomically through generated types

<!-- work-item-format: 2 -->

- Approval: 用户于 2026-08-24 在本任务中明确要求开始执行；批准的完整工作项映射内容 SHA-256 为 `FFEEE360860BF3B5A891B61B3D91C9D047667C300712C7DCB46BDF8BE3006249`。

## Outcome

A checked-in, provenance-recorded OCCT AP203 B-rep/product fixture binds atomically to the generated
AP203 graph, with representative product, shape, topology, geometry, unit, value, and reference
identity observations; an out-of-baseline OCCT extension fails explicitly with location evidence.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area: fixed OCCT test assets and the existing generated schema-binding path.
- In scope: fixture provenance/license/checksum, deterministic typed observations, root-cause
  mapping corrections exposed by the supported sample, and an unsupported-extension failure case.
- Non-goals: invoking OCCT during verification, accepting every OCCT AP203 extension, CAD-kernel
  conversion, sample-specific runtime code, or weakening atomic validation.
- Likely touchpoints (non-binding): AP203 integration test assets, generated binding code, and
  package interoperability documentation.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| AP203-001 | Full pinned schema and representative generated surface | Schema checksum and passing fidelity proof |
| AP203-002 | Compiled AP203 assembly with the approved runtime dependency boundary | Package-consumer proof |
| AP203-003 | Standard AP203 header identifiers select the descriptor without text loss | Identifier contract proof |
| Fixture redistribution evidence | The fixture and originating repository-owned geometry may be checked in with source/export metadata | Recorded OCCT commit, exporter parameters, license, and checksum |

If fixture redistribution evidence is incomplete, stop before treating the asset as deliverable proof.

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Verified input or primary-proof intent |
| --- | --- | --- |
| AC-05 | Owns | The fixed OCCT fixture returns one validated typed graph with agreed semantic observations |
| AC-09 | Owns | A declared-AP203 file using a fixed unsupported extension fails with stable code and location |
| AC-06, AC-11, AC-12 | Supports | Supplies the checked-in typed fixture graph and semantic baseline used by later journey proofs |

<!-- work-item: delivery-constraints -->
## Constraints

- Pin OCCT commit `7d2efad9c8a9a57ea96c4c8587134b34dd503cd8`, export mode/parameters,
  originating geometry, license evidence, and fixture checksum.
- Verification uses no network or installed OCCT and must assert stable semantic facts, not incidental instance numbers.
- Binding fixes must generalize from EXPRESS/Part 21 semantics; do not add fixture-name switches or OCCT domain classes.
- Unsupported extensions remain an explicit documented boundary.

<!-- work-item: proof-plan -->
## Proof

| Contract or gate | Evidence purpose | Execution shape | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-05 | Acceptance and real-boundary proof | Integration/contract | Read succeeds atomically and the agreed typed graph counts, key values, units, and reference identities match | `dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release` |
| AC-09 | Acceptance and regression | Integration/contract | The fixed extension case returns schema/binding code plus source location and no partial model | `dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release` |

<!-- work-item: definition-of-done -->
## Done

- Fixture source, export parameters, license evidence, checksum, and supported boundary are documented.
- AC-05 and AC-09 integration proofs pass without network or OCCT installation.
- Any runtime/generator correction is general, minimal, and protected by a narrower regression where useful.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, fixture checksum and provenance, actual changed artifacts, AC-05/AC-09
commands, observable graph/failure assertions, test counts, resource prerequisites, documented
unsupported boundary, and the typed fixture baseline supplied to AP203-005 and AP203-006.

## Risks and implementation notes

An OCCT file can legitimately exceed the pinned schema. Do not broaden the package claim to make a
fixture pass. If the chosen fixture is outside the approved baseline, replace it with the approved
fixed export rather than adding extensions to this work item.
