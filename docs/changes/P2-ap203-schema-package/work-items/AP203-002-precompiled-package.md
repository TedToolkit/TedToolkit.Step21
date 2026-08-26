# AP203-002: Deliver the precompiled AP203 package boundary

<!-- work-item-format: 2 -->

- Approval: 用户于 2026-08-24 在本任务中明确要求开始执行；批准的完整工作项映射内容 SHA-256 为 `FFEEE360860BF3B5A891B61B3D91C9D047667C300712C7DCB46BDF8BE3006249`。

## Outcome

A consumer can reference `TedToolkit.Step21.Ap203` and compile against the one generated
`config_control_design` descriptor and schema types without supplying EXPRESS inputs or acquiring
an Analyzer-only runtime dependency.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area: the optional AP203 project, its package contents, and package-consumer proof.
- In scope: build and pack the precompiled generated assembly, expose only the intended runtime
  dependency, include required package documentation, and prove a clean package-only consumer.
- Non-goals: a reader/writer facade, schema registry, reflection discovery, a second parser/writer,
  AP214/AP242, or AP203 types in the core runtime assembly.
- Likely touchpoints (non-binding): `src/TedToolkit.Step21.Ap203`, solution/package metadata,
  integration package tests, and a minimal temporary consumer.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| AP203-001 | Checked-in schema compiles and its expected generated surface is verified | AP203-001 completion evidence and schema checksum |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Verified input or primary-proof intent |
| --- | --- | --- |
| AC-01 | Owns | A new consumer compiles from only a package reference and can access descriptor/types |
| AC-10 | Owns | The consumer has one Step21 runtime and no prohibited runtime assets or duplicate stack |
| AC-11 | Supports | Supplies the exact package boundary used by the AOT journey |
| AC-12 | Supports | Supplies an offline-packable project from the fixed schema baseline |

<!-- work-item: delivery-constraints -->
## Constraints

- Package/assembly identity is `TedToolkit.Step21.Ap203`; generated namespace remains
  `TedToolkit.Step21.Generated.ConfigControlDesign`.
- The package depends on the compatible `TedToolkit.Step21` runtime; it must not place Analyzer,
  RoslynHelper, JSON/XML, schema-source generation, or a second parser/writer in the runtime graph.
- Consumers must not need `.exp` files or `AdditionalFiles` and must not receive duplicate public types.
- Private MSBuild layout and test organization remain open.

<!-- work-item: proof-plan -->
## Proof

| Contract or gate | Evidence purpose | Execution shape | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-01 | Acceptance and boundary | Integration/contract | A clean temporary app with only the AP203 package reference compiles and accesses the descriptor and representative types | `dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release` |
| AC-10 | Compatibility and structural | Integration/contract | Package/deps inspection shows one compatible runtime and none of the prohibited runtime assets | `dotnet run --project tests/TedToolkit.Step21.IntegrationTests --configuration Release` |
| Repository build | Structural verification | Component | The new project participates in the Release solution build with zero warnings/errors | `dotnet build TedToolkit.Step21.slnx --configuration Release` |

<!-- work-item: definition-of-done -->
## Done

- The package-only consumer proofs for AC-01 and AC-10 pass.
- Package contents and runtime dependency closure match the approved boundary.
- Package documentation states schema identity, fixed source, normal usage, and the duplicate-AdditionalFiles prohibition.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, produced package identity, actual changed artifacts, AC-01/AC-10
commands and assertions, test counts, inspected dependency/package contents, documentation state,
and the package output supplied to AP203-004 and AP203-006.

## Risks and implementation notes

Do not add a class merely to make the package look like a product surface: the generated descriptor
and types are the surface. If precompilation cannot use the existing generator boundary without a
new public runtime contract, return to change design.
