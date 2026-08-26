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
## Completion evidence

| Evidence | Completion record |
| --- | --- |
| Delivery boundary | Reconciliation started from the completed AP203-003 baseline `57c77bb`. Candidate `113ab7a64c04ea3037f118e25c4cfca1e0f56713` contains migrated commits `b10bb5b`, `1c0147b`, `cadd705`, `f40ce0e`, `12c018f`, and `113ab7a`. Together they add the checked-in AP203 source/evidence, reusable EXPRESS binding and generation corrections, and generated-fidelity/regression tests. They add no AP203-specific production branch, hand-written AP203 domain type, public/protected API, runtime class, or package shell, and no commit is pushed by this work item. |
| Asset identity | `schemas/ap203/ap203.exp` is derived from STEPcode commit `9baa5dadaa1dcfcdc623220d865d36d61ea351e9`, path `data/ap203/ap203.exp`. The pinned upstream SHA-256 is `020B4D25DBD0B6EE7D15099B978E3448F6699A72CB862D381E416E32187562F1`; the corrected canonical-LF file SHA-256 is `19497DCA88C6FCFE763DA23772B68356BE4361668426954DE9863E4285D0C251`. `.gitattributes` fixes EXPRESS schema checkouts to LF. The fidelity hash decodes UTF-8 and canonicalizes only its hash input to LF, while the compiler receives the actual text read from disk unchanged. `PROVENANCE.md` records the only two comma-to-repetition-separator corrections. Copied evidence hashes are `COPYING` `C787486F3E1358CF1CB4456B56E00862DE9C0433E7D49F5501D1289FF8BEF37E`, `AUTHORS` `619EE3D3D9CE6DB690B4A20F36AB30616CB8F1FB8616FAEB85D9685AFDFD15FB`, and `INTENT.md` `B10C7DCC9C269B383C944ACC139F787CCA050D497142CA03ED90C49EF41CE23F`. |
| Changed artifacts | Production changes remain inside the existing EXPRESS bound-name/type/expression compiler and Roslyn generation pipeline. Independent-review remediation is confined to reachable-rule flow/emission, focused reachable-rule and order-fidelity tests, this evidence record, and the Roslyn composition architecture/conformance records. Tests cover binding, expression generation, entity/complex projections, reachable rules, and one full-schema `GeneratedFidelityTests` contract. `TedToolkit.Step21.Tests.csproj` supplies the checked-in schema as an offline test asset. No generated C# output is checked in. |
| AC-02 and behavior proof | The full pinned schema generates with zero warning/error diagnostics and the fidelity contract asserts schema name, entity inheritance, OPTIONAL nullability, aggregate kinds, enumeration members, SELECT surfaces, and order-sensitive constructor/member sequences. Stage 3 affected Release proof passed 132/132 tests across binding, expression generation, complex mapping, entity hierarchy, and reachable rules; the separate full-schema fidelity contract passed 1/1. The complete fast Release project passed 308/308. |
| Build and integration proof | The reviewed AP203-001 chain was serialized after AP203-003 as the six current integration commits recorded above. `dotnet build TedToolkit.Step21.slnx --configuration Release --no-incremental` passed with 0 warnings/errors. `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release --no-build` passed 336/336; default Release integration passed 5 enabled tests with 1 expected external-network corpus test skipped. Independent implementation review of `57c77bb..28ba730` concluded `Ready` with no blocking findings or design deviations. The skipped test is not an AP203 package or fixture proof and AP203-002/AP203-004 remain responsible for those boundaries. |
| Composition boundary | Source declarations, members, ordinary locals, assignments, `IF`/`CASE`, returns, and compound statements use the existing RoslynHelper structural boundary. The pinned helper lacks a general loop, lambda, pattern/presence, and switch-expression node; the reviewed custom surface is limited to the whole-loop fragment for general EXPRESS `REPEAT`, necessary lambdas, and necessary pattern/presence/switch expressions. The implementation adds no production type or caller-visible API. |
| Independent-review remediation | Lexical spellings remain stable storage while scalar projections use a separate generation-only override map. Assignment kills overrides; branch, `CASE`, and `REPEAT` fall-through facts join per structural key; qualified writes clear safe-index paths and downgrade only previously proven SELECT paths to their declared invalidated marker so aliasing cannot retain a stale alternative. Lexical and qualified-path SELECT facts now share one generic per-key join whose entity/declared-marker least-upper-bound preserves a conservative runtime proof without retaining a stale entity alternative. Dynamic SELECT-to-entity applications participate in UNKNOWN fixed-point propagation, and top-level emission stops after non-fall-through statements. Ordinary statement emission uses RoslynHelper nodes; production retains one whole-statement `Custom` location for general EXPRESS `REPEAT`, two private generic fact-intersection methods, and one generic SELECT-fact join method. Ordering claims use order-sensitive assertions. No temporary instrumentation or `.tmp` artifact remains. |
| Dependent input | AP203-002 receives the offline source/license/provenance asset and verified generated surface. AP203-004 receives the same generated AP203 entity/value contract; neither package delivery nor OCCT fixture execution is claimed by this candidate. |

## Risks and implementation notes

The first full AP203 compile exposed multiple symptoms of shared compiler gaps. The implementation
corrects their existing semantic boundaries and retains focused controls for UNKNOWN, SELECT,
aggregate, entity identity/value equality, and flow narrowing. The independently reviewed candidate
passed serialized integration with AP203-003; package delivery and the OCCT fixture remain downstream.
