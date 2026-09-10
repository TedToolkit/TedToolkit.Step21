# ADR-0010: Target .NET Standard 2.0 and .NET 8 for runtime packages

- Status: Accepted
- Date: 2026-09-10
- Decision owner: repository maintainer
- Approval source: the repository maintainer explicitly approved `netstandard2.0` and `net8.0` and requested implementation on 2026-09-10
- Decision scope: target-framework and compatibility boundaries for the TedToolkit.Step21 runtime and maintained schema packages until a material runtime-specific capability requires reassessment
- Applicable product intent: [`../product/README.md`](../product/README.md)
- Applicable principles: [`AP-001` and `AP-002`](../principles/architecture.md); [`EP-002`, `EP-003`, and `EP-004`](../principles/engineering.md)
- Related decisions: [`ADR-0003`](ADR-0003-aot-ready-schema-validation.md), [`ADR-0006`](ADR-0006-precompiled-schema-package-distribution.md)
- Supersedes: None
- Superseded by: None

## 📌 Decision at a glance

Publish the schema-neutral runtime and every maintained precompiled schema package for
`netstandard2.0` and `net8.0`, while keeping build-time Roslyn components on `netstandard2.0` and
adding no `net10.0` library asset until a target-specific implementation provides evidenced value.

## 🧭 Context and decision question

The runtime and maintained AP203, AP214, and AP242 packages currently target only `net10.0`; the
Roslyn Analyzer already targets `netstandard2.0`. Requiring .NET 10 prevents otherwise compatible
.NET Framework, Unity, and earlier modern .NET consumers from restoring the runtime packages.

The decision is whether consumer-facing packages should retain the .NET 10-only baseline, publish
separate .NET Standard 2.0, .NET 8, and .NET 10 assets, or use .NET Standard 2.0 as the portable
fallback plus .NET 8 as the modern runtime implementation.

## 🎯 Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | ISO behavior, public API meaning, diagnostics, security boundaries, and schema-neutral dependency direction cannot be weakened to reach an older target | Product intent, AP-002, EP-002, ADR-0003, and ADR-0006 | Must |
| Hard constraint | Runtime and generated schema paths remain trimming- and Native AOT-ready on modern .NET | EP-003 and ADR-0003 | Must |
| Hard constraint | Maintained schema packages expose the same generated public contract and bounded runtime dependency on every declared TFM | ADR-0006 | Must |
| Decision driver | Consumers needing .NET Framework or broad cross-runtime compatibility have a portable asset | Microsoft .NET Standard guidance | High |
| Decision driver | Modern consumers retain current APIs, analyzers, performance opportunities, and deployment proof | Microsoft cross-platform library guidance | High |
| Decision driver | Every additional package asset has a concrete compatibility or implementation purpose | Package size and verification-matrix cost | High |
| Decision driver | The support statement distinguishes TFM compatibility from historical compiler and runtime servicing support | .NET and NuGet compatibility rules | Medium |

## 🔎 Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Keep only `net10.0` | Documented current project state; high confidence | No | Minimizes the build matrix but unnecessarily excludes compatible consumers | Rejected |
| Target only `netstandard2.0` | Documented platform reach; high confidence | Partially | Maximizes fallback reach but removes a modern target for runtime-specific APIs, analyzers, trimming, and AOT declarations | Rejected |
| Target `netstandard2.0`, `net8.0`, and `net10.0` | Documented NuGet asset selection; high confidence | Yes | The current source has no .NET 10-specific implementation, so the third assembly duplicates build, package, API, and verification cost | Rejected until a .NET 10-specific benefit is evidenced |
| Target `netstandard2.0` and `net8.0` | A local .NET SDK 10.0.401 probe compiled the current core source as `net8.0` with zero warnings or errors; a `netstandard2.0` probe exposed bounded compatibility gaps including records, nullable attributes, `Rune`, `ISpanFormattable`, and `IReadOnlySet<T>` | Yes, subject to delivery proof | Requires compatibility implementations and a wider test matrix, while NuGet lets .NET 10 consumers select the `net8.0` asset | Selected |

## ✅ Decision

`TedToolkit.Step21` and every maintained precompiled schema package target
`netstandard2.0;net8.0`. The two assets expose equivalent supported public behavior and public API
shape; target-specific private implementations may differ.

The Roslyn Analyzer remains `netstandard2.0` because compiler-host compatibility is a separate
build-time boundary. Repository build programs, tests, probes, and other non-consumer tools may
target the current repository SDK when their TFM does not constrain package consumers.

No `net10.0` runtime or schema assembly is published while it would duplicate the `net8.0` asset.
.NET 9 and .NET 10 consumers use NuGet's compatible `net8.0` asset. A future .NET-specific asset is
added only when a required API, behavior, performance result, trimming contract, or deployment
capability cannot be supplied by the existing modern asset.

The portable compatibility commitment starts at .NET Framework 4.7.2 for .NET Framework consumers.
The package does not claim that every historical compiler, SDK, operating system, or runtime still
receiving a compatible asset is actively serviced; source-generator tooling requirements are
declared and verified separately from runtime TFM compatibility.

## 💡 Why this decision now

The package family has not yet established a broad runtime baseline, so widening compatibility now
avoids making .NET 10 an accidental permanent minimum. .NET Standard 2.0 is the portable boundary
that includes .NET Framework, while a .NET 8 asset preserves a modern implementation and deployment
surface. The existing core compiles for .NET 8 without source changes, and NuGet already provides
the asset-selection behavior needed by later modern runtimes.

The current .NET Standard compile failures are compatibility work rather than evidence that the ISO
or schema contracts require .NET 10. The decision rejects any shim or conditional path that would
change public semantics, security behavior, failure atomicity, generated schema meaning, or the
schema-neutral/AOT architecture merely to make the older target compile.

## 🔗 Evidence and links

- [Microsoft: .NET Standard](https://learn.microsoft.com/dotnet/standard/net-standard)
- [Microsoft: Cross-platform targeting for .NET libraries](https://learn.microsoft.com/dotnet/standard/library-guidance/cross-platform-targeting)
- [Microsoft: Multi-targeting for NuGet packages](https://learn.microsoft.com/nuget/create-packages/supporting-multiple-target-frameworks)
- [Product intent](../product/README.md)
- [Schema-bound round-trip architecture](../architecture/schema-bound-round-trip.md)
- [ADR-0003: AOT-ready schema validation](ADR-0003-aot-ready-schema-validation.md)
- [ADR-0006: Precompiled schema package distribution](ADR-0006-precompiled-schema-package-distribution.md)

## ⚖️ Consequences and accepted trade-offs

- .NET Framework 4.7.2+, compatible Unity/Mono environments, and modern .NET consumers can restore a
  suitable runtime asset from the same package identity.
- Portable implementations may need local compatibility types, alternate private algorithms, or
  additional package dependencies; these costs are accepted only when public semantics remain
  identical.
- Every consumer-facing package gains a two-TFM build, API comparison, packaging, and regression
  obligation.
- A .NET 10 consumer currently receives the `net8.0` asset and therefore cannot use a future
  compile-time-only .NET 10 optimization until an evidenced target is added.
- Supporting a TFM does not extend Microsoft's servicing lifetime for the consumer's runtime.

## 🛠️ Downstream delivery constraints

- Runtime and maintained schema packages contain exactly the portable and modern assets selected by
  this decision unless another accepted decision adds a justified target.
- Public and protected API shape, nullability, schema descriptors, validation, diagnostics,
  read/write results, security boundaries, and failure atomicity remain equivalent across targets.
- Compatibility code remains private or compiler-support infrastructure and must not create
  duplicate framework type identities in consumer code.
- The portable asset must execute on a representative .NET Framework 4.7.2-or-later consumer; a
  compile-only `netstandard2.0` result is insufficient.
- Modern package consumers retain trimming and Native AOT proof through the existing static
  descriptor and generated-code boundary.
- Analyzer/runtime dependency isolation and package version ranges remain those required by
  ADR-0006.

## 🔄 Exit requirements

Dropping `netstandard2.0` or raising the modern minimum requires an explicit compatibility decision
and a package-version classification. Adding another modern TFM requires evidence that the existing
asset cannot provide a required capability or that a measured benefit justifies its continuing
package and verification cost.

## 📅 Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Deliver and verify the two-target package family without public semantic drift | repository maintainer | Before the next package release | Open |
| Reassess the portable target | repository maintainer | Required behavior cannot be implemented safely on .NET Standard 2.0 or a supported dependency drops the target | Open |
| Reassess the modern target | repository maintainer | A required runtime API, deployment capability, or measured material benefit requires a newer TFM | Open |
