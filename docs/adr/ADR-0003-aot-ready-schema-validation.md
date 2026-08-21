# ADR-0003: Select an AOT-ready schema validation contract

- Status: Accepted
- Date: 2026-08-21
- Decision owner: repository maintainer
- Decision scope: public validation contracts and execution for the .NET 10 runtime and generated EXPRESS schema code over the library's supported lifetime
- Applicable product intent: [`../product/README.md`](../product/README.md)
- Applicable principles: [`AP-001`, `AP-002`, and `AP-003`](../principles/architecture.md); [`EP-002` and `EP-003`](../principles/engineering.md)
- Supersedes: validation portions of ADR-0001
- Superseded by: None

## 📝 Clarification and decision log

| ID | Question and why it mattered | Recommended answer | User decision and source | Affected sections | Status |
| --- | --- | --- | --- | --- | --- |
| ADR-C-01 | Whether Native AOT readiness may override the earlier FluentValidation dependency choice | Require AOT-safe runtime/generated paths and select FluentValidation only if a pinned version supplies adequate proof | The maintainer rejected relying on FluentValidation without AOT compatibility assurance on 2026-08-21 and requested AOT-ready alternatives | Drivers, options, decision, and exit requirements | Resolved |
| ADR-C-02 | Which AOT-ready validation contract replaces FluentValidation | Use a minimal Step21 result/exception contract and generate all validator execution directly from bound EXPRESS IR; do not add a second general-purpose validation source generator | The maintainer approved this direction and cancelled FluentValidation on 2026-08-21 | Options, public API, dependency footprint, and downstream constraints | Resolved |
| ADR-C-03 | How `ExchangeStructure.Validate()` reaches generated schema validation without reflection or an entity-to-container link | Let `ExchangeStructure` own an immutable standard-`schema_name`-to-generated-descriptor binding table; each `DataSection` retains only its ISO-defined governing `SchemaName`, and `Validate()` resolves and invokes the descriptor directly after schema-neutral checks | The maintainer approved the corrected ISO-first ownership and required removal of the unnecessary schema facade on 2026-08-21 | Decision details and generated/runtime boundary | Resolved |
| ADR-C-04 | How much context belongs directly on each `ValidationFailure` | Keep only stable `Code`, deterministic string `Path`, caller-facing `Message`, and optional constraint `SourceLocation`; treat all failures as invalidating errors and keep warnings in diagnostics rather than duplicating schema/entity/severity/object fields | The maintainer accepted the simplified failure design with no other concern on 2026-08-21 | Public validation ABI and diagnostics separation | Resolved |

## 📌 Decision at a glance

Use a minimal Step21 validation result and exception contract. Compile all executable validation directly from bound EXPRESS IR into generated schema code. Do not depend on FluentValidation or another general-purpose validation engine.

## 🧭 Context and decision question

Generated mutable entities require explicit and Part 21 boundary validation that aggregates every detected schema failure. The earlier direction selected FluentValidation as both engine and public contract. A new Required Native AOT direction makes an unverified reflection/expression-compilation dependency unacceptable. This ADR decides whether a platform or third-party validation contract can satisfy the full boundary, or whether generated Step21 validators must own the minimal missing contract.

## 🎯 Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Runtime and generated-schema paths publish and execute under .NET 10 Native AOT without reflection-based schema discovery or dynamic code generation | Proposed EP-003 and Microsoft Native AOT compatibility contract | Must |
| Hard constraint | One validation operation retains every detected failure through a stable code, deterministic model path, message, and available constraint source location | Maintainer direction, 2026-08-21 | Must |
| Hard constraint | Automatic validation occurs only after Part 21 read/bind and before Part 21 write; explicit validation remains side-effect-free | Maintainer direction, 2026-08-21 | Must |
| Hard constraint | Generated validators execute all validation/write-reachable EXPRESS semantics by final delivery | ISO 10303-11 schema constraints and maintainer direction | Must |
| Hard constraint | Runtime remains schema-neutral and generated code does not require generator-only dependencies | AP-002 | Must |
| Decision driver | Prefer a maintained existing package over inventing a parallel framework when it satisfies every hard constraint | Maintainer direction, 2026-08-21 | High |
| Decision driver | Public contract, license, dependency footprint, release cadence, documentation, and exit path remain supportable | Technology-selection policy | High |

## 🔎 Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| FluentValidation core | Its current project does not declare `IsAotCompatible`; its property accessor cache uses reflection metadata and expression compilation | No under the Required dependency-proof gate | Mature aggregate API, but Step21 would own unsupported AOT risk in a public core dependency | Reject unless upstream later supplies adequate proof and the full Step21 publish/run gate passes |
| `Microsoft.Extensions.Validation` | .NET 10 supplies Microsoft-maintained, source-generated recursive DataAnnotations validation and aggregates errors by property path | Partial | Best-supported AOT direction, but its public error shape is path-to-message arrays and does not carry the required stable EXPRESS code or constraint source location; library use also requires DataAnnotations-oriented generated metadata and .NET 10 evaluation APIs for some non-endpoint scenarios | Do not use as the Step21 public contract or execution engine |
| `Immediate.Validations` | MIT source generator; supports generated direct validation, aggregate `ValidationResult`, custom validations, and exceptions | Partial | Its public failure example exposes property name and message, not the complete Step21 failure identity; it is primarily designed for Immediate.Handlers and would make the Step21 generator emit input for another generator | Reject for semantic/API mismatch and redundant generation |
| `ZeroAlloc.Validation` | Upstream describes a reflection-free, Native AOT-safe source generator and current releases target .NET 8+ | Partial and not independently proven | AOT-first, but young and attribute-oriented; its shown failure contract is property name plus message and does not cover the required EXPRESS identity. It would also add a second generator | Reject as the core contract; retain only as a future comparison point |
| `Sannr` | Upstream package describes AOT-first source-generated validation with aggregation | Partial and not independently proven | Very young, broad web/validation/sanitization footprint, attribute-oriented semantics, and no demonstrated match for the Step21 failure contract | Reject for maturity, footprint, and semantic mismatch |
| Generated Step21 validators with a minimal public result contract | Step21 already compiles all EXPRESS declarations and constraints into bound IR and generates schema code. Direct rule emission needs no discovery, reflection, attribute interpretation, DI, or second generator | Yes by design, subject to publish/run proof | Step21 owns a small stable result/context/exception contract, but does not invent a rule DSL or general validation framework. This is the only option that represents every required EXPRESS rule and failure field without an adapter shadow contract | Recommend |

## ✅ Decision

Step21 owns only the schema-neutral contracts required to expose validation outcomes: an aggregate `ValidationResult`, immutable `ValidationFailure` values containing `Code`, string `Path`, `Message`, and optional `SourceLocation`, a non-public accumulation context, and read/write-stage validation exceptions that retain the aggregate result. Every failure invalidates the result; non-invalidating warnings remain diagnostics. This is not a public rule-building DSL and provides no runtime validator registration, discovery, assembly scanning, or expression compilation.

The EXPRESS compiler remains the only validation rule language front end. The existing generator emits direct C# for structural rules and every validation-reachable EXPRESS rule from the same bound IR that supplies schema descriptors and XML documentation. `ExchangeStructure.Validate()` is side-effect-free, returns the complete aggregate result, and does not throw merely because the result is invalid. Successful Part 21 read and write boundaries invoke the same generated path and throw their stage-specific aggregate exception when it is invalid.

`ExchangeStructure` stores generated descriptor bindings keyed by the standard `schema_name` values declared through `FILE_SCHEMA`. Each `DataSection` keeps its ISO-defined governing `SchemaName`; in the single unnamed-section form this is normalized from the sole `FILE_SCHEMA` entry. The descriptor remains generated mapping/validation infrastructure and is not presented as a new section value, entity relationship, or domain concept. No additional schema facade is generated: callers provide one or more generated descriptors directly to construction or binding entry points.

## 💡 Why this decision now

Validation is a public contract, a core dependency, and the execution boundary for every generated schema rule. Selecting it before AOT compatibility is proven would make either the public API or the deployment promise difficult to reverse.

## 🔗 Evidence and links

- [Microsoft Native AOT deployment and library compatibility](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
- [FluentValidation project file](https://github.com/FluentValidation/FluentValidation/blob/main/src/FluentValidation/FluentValidation.csproj)
- [FluentValidation accessor compilation implementation](https://github.com/FluentValidation/FluentValidation/blob/main/src/FluentValidation/Internal/AccessorCache.cs)
- [Microsoft.Extensions.Validation source generator](https://github.com/dotnet/aspnetcore/blob/main/src/Validation/gen/ValidationsGenerator.cs)
- [Microsoft unified validation design and aggregate error shape](https://github.com/dotnet/aspnetcore/issues/46349)
- [Immediate.Validations source and public usage contract](https://github.com/ImmediatePlatform/Immediate.Validations)
- [ZeroAlloc.Net validation overview](https://github.com/ZeroAlloc-Net)
- [Sannr package](https://www.nuget.org/packages/Sannr/)
- [ISO 10303-21 Edition 3 final text, clauses 8.2.4 and 11.1](https://www.steptools.com/stds/step/IS_final_p21e3.html)

## ⚖️ Consequences and accepted trade-offs

Step21 no longer gains FluentValidation's mature rule DSL and ecosystem. In return, consumers do not acquire an AOT-unverified runtime dependency, the public failure model can represent complete EXPRESS identity, and generated execution has no adapter or second-generator layer. Step21 must maintain the small result/exception ABI and prove every generated rule path through Native AOT publication and execution.

## 🛠️ Downstream delivery constraints

- Runtime validation and generated validator execution must remain statically reachable and Native AOT-safe.
- Validation must aggregate rather than stop after the first schema failure.
- Stable error codes and attribute paths must remain traceable to generated XML documentation and EXPRESS source metadata.
- No assembly scanning, runtime schema reflection, or dynamic validator discovery may be required.

## 🔄 Exit requirements

The selected contract must permit preserving stable failure identity and generated validator semantics if its package is later removed or replaced. A replacement requires equivalent Native AOT proof and no loss of EXPRESS validation coverage.

## 📅 Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Complete primary-source API/ecosystem comparison | repository maintainer | Before ADR approval | Complete |
| Define and approve descriptor-based validation dispatch | repository maintainer | Before ADR approval | Complete |
| Publish and execute a representative generated-schema Native AOT proof | repository maintainer | Before release of the affected runtime/generated validation capability | Open |
| Reassess the validation contract | repository maintainer | Native AOT platform contract, required failure identity, or EXPRESS execution boundary changes | Open |
