# SBRT-001: Reproducible internal ANTLR artifacts

## 📌 Status

Implemented

- Implemented from starting SHA `c1c0b32` on 2026-08-21. The repository maintainer required local per-work-item commits and prohibited creating a branch; because this repository has no remote, hosted-runner execution remains a future CI regression gate rather than current completion evidence.

## 🚦 Delivery priority

- Priority: P2
- Rationale: Both language pipelines depend on reproducible, non-public generated parser artifacts.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: None.
- Recommended order: First parser-infrastructure item; it may run independently of runtime contracts.
- Governing records: AP-003 and EP-003, architecture and ADR-0002/0003/0005 pinned by the parent change.

## 🧩 Explicit governing constraints

ANTLR source grammars are authoritative; derived C# is never hand-edited. Windows and Unix generation retain visitors, suppress listeners, internalize generated top-level types, and use the centrally pinned ANTLR version.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| ANTLR generation scripts, generated artifacts, focused build tests, Windows/Linux CI jobs | Deterministic parser/lexer/visitor/base-visitor output through PowerShell on `windows-latest` and shell on `ubuntu-latest` | Changing either language grammar or exposing generated ANTLR APIs publicly |

## 🔍 Current behavior and impact boundary

Both scripts request visitors and suppress listeners, but no repository gate proves artifact completeness, public visibility, listener absence, or deterministic regeneration. Existing parser tests must remain green.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| Parent change is Approved and current 5-test parser baseline passes | `../change.md`; `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release` | Stop if the baseline regresses independently of this item |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-20 | Regenerate both ANTLR grammars on Windows or Unix | Run the repository generation script | Parser, lexer, visitor, and base visitor are deterministic and internal; no listener is emitted |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Generated artifact contract | Expected artifacts exist, are internal, and repeat generation is byte-stable | Package/runtime consumers gain no ANTLR parser API |
| Cross-platform scripts | PowerShell on `windows-latest` and shell on `ubuntu-latest` produce the same normalized artifacts | Central version pin and generated namespaces remain unchanged |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-20 | Build-contract integration | On the exact Windows/Linux CI environments, run each native script twice from a clean generated snapshot; files/bytes match, visitors exist, listeners/public declarations do not | `build/generate-antlr.ps1` on `windows-latest`; `build/generate-antlr.sh` on `ubuntu-latest`; fast TUnit project |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Reproducible generation | Focused automated checks pass for both grammar outputs on both exact CI environments and compare normalized artifacts |
| Baseline preservation | Release build and fast parser tests pass with zero warnings/errors |

## ⏱️ Workload estimate

- Planning range: 0.2–0.3 person-months.
- Confidence: High.
- Assumptions and excluded work: Java/ANTLR remain available; normative grammar changes belong to SBRT-002.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Starting SHA `c1c0b32`; changed `.gitattributes`, `build/generate-antlr.ps1`, `build/generate-antlr.sh`, `.github/workflows/build.yml`, `README.md`, and `tests/TedToolkit.Step21.Tests/AntlrGenerationTests/ArtifactsTests.cs`. Regenerated parser artifacts are byte-equivalent to the pinned sources and contain no semantic diff. |
| Behavior-case proof | Red: the focused Release test failed because isolated `-OutputRoot` generation was unavailable. Green: focused TUnit passed 1/1. Full fast TUnit passed 6/6. PowerShell and shell each regenerated 16 artifacts repeatedly; SHA-256 snapshots were byte-identical across scripts, required parser/lexer/visitor/base-visitor sources were present, listener files were absent, and top-level generated types were internal. The workflow now repeats this proof on `windows-latest` and `ubuntu-latest` and compares their uploaded bytes. |
| Regression proof | `dotnet build TedToolkit.Step21.slnx --configuration Release --no-restore` passed with 0 warnings and 0 errors. Integration tests passed 2/2 enabled cases with the network corpus case explicitly skipped. |
| Migration and documentation | `README.md` now documents UTF-8/LF normalization, visitor/listener/internal visibility guarantees, and isolated output-root usage. |
| Effort and variance | Approximately 0.5 elapsed implementation/verification hours, materially below the 0.2–0.3 person-month range because both native generation paths and authoritative artifacts already existed; work concentrated on isolation, normalization, tests, and CI gates. |
| Dependent-item unlock | Deterministic internal visitor artifact guarantee is implemented and locally proven for SBRT-002 and SBRT-007. The checked-in workflow will repeat the proof on exact hosted runners once a remote repository exists. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Platform tools normalize text differently | False nondeterminism | Implementer must prove semantic and byte-level output policy on both scripts |
