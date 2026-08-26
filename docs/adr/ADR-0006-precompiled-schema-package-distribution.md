# ADR-0006: Distribute maintained precompiled schemas as separate optional packages

- Status: Accepted
- Date: 2026-08-26
- Decision owner: repository maintainer
- Decision scope: package, dependency, discovery, provenance, and versioning boundaries for maintained precompiled EXPRESS schema distributions over the supported lifetime of TedToolkit.Step21
- Applicable product intent: [`../product/README.md`](../product/README.md)
- Applicable principles: [`AP-001`, `AP-002`, and `AP-003`](../principles/architecture.md); [`EP-002`, `EP-003`, and `EP-004`](../principles/engineering.md)
- Supersedes: None
- Superseded by: None

## 📌 Decision at a glance

Distribute each independently maintained EXPRESS schema baseline as a separate optional package containing precompiled generated types and explicit descriptors for its closed schema set, while keeping the core runtime schema-neutral and avoiding runtime discovery or a combined application-protocol bundle.

## 🧭 Context and decision question

The core package currently lets each consumer supply one closed set of EXPRESS `AdditionalFiles`; the packaged Analyzer compiles that set and emits schema types into the consumer assembly. This is the general schema-neutral path, but it makes every consumer acquire, license, pin, configure, and regenerate commonly used schema sources.

A maintained precompiled schema distribution can remove that setup for a supported baseline, but its package boundary is durable. Putting schema types in the core would let an application protocol shape the runtime. Combining unrelated schema families would couple their size, compatibility, provenance, and release cadence. Runtime discovery would reverse the static descriptor direction and weaken Native AOT proof. This ADR decides whether maintained schemas remain consumer-generated, enter the core or a bundle, load dynamically, or ship as independently versioned optional packages.

The selected direction is an intended package architecture. This record does not claim that any maintained schema package has been delivered.

## 🎯 Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | ISO 10303-21 remains the product boundary; an application protocol does not define a second core model | AP-001 and approved product intent | Must |
| Hard constraint | The runtime remains schema-neutral and does not depend on a generated schema or generator implementation | AP-002 and the active architecture record | Must |
| Hard constraint | Generated public concepts trace only to the distributed EXPRESS sources | AP-003 | Must |
| Hard constraint | Runtime and generated paths remain statically reachable and Native AOT-ready without assembly scanning or dynamic code | EP-003, ADR-0003, and the packed-consumer AOT proof | Must |
| Hard constraint | A maintained distribution records its exact source baseline, integrity, and redistribution license before publication | EP-004 and repository provenance practice | Must |
| Decision driver | A consumer can use a maintained schema without acquiring `.exp` files or configuring `AdditionalFiles` | Proposed maintained-schema consumer boundary | High |
| Decision driver | The schema package reuses the one runtime parser, model, validator boundary, and writer | AP-002 and current package architecture | High |
| Decision driver | Unrelated schema families can evolve, deprecate, and release without forcing a combined upgrade | Package compatibility isolation | High |
| Decision driver | Descriptor selection remains explicit and deterministic | ADR-0003 and current public read boundary | High |
| Decision driver | Package versions communicate generated API, schema-semantic, provenance, and runtime compatibility without requiring lockstep core/schema releases | SemVer 2.0.0 and NuGet dependency-range behavior | High |

## 🔎 Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Keep only consumer-supplied `AdditionalFiles` | Documented current package guide and packed-consumer proof; high confidence | Partially | Preserves the current architecture but leaves source acquisition, provenance, configuration, and repeat generation to every consumer | Rejected for maintained baselines; retained as the general custom-schema path |
| Put maintained schema types in `TedToolkit.Step21` | Architecture analysis; high confidence | No | Gives one package immediate types but makes the schema-neutral runtime depend on application-protocol surface and release pressure | Rejected |
| Ship one combined package containing several unrelated schema families | Package-boundary analysis; documented direction, unmeasured size cost | Partially | Reduces package count but couples payload, versioning, provenance, compatibility, and deprecation across independent schemas | Rejected |
| Ship one optional package per independently maintained schema baseline | Existing generated descriptor boundary and package proof demonstrate the static dependency shape; high confidence for architecture, delivery proof still required | Yes | Creates more packages and requires provenance and compatibility maintenance for each baseline | Selected |
| Load schema definitions or generated assemblies dynamically at runtime | EP-003 and Native AOT evidence; high confidence | No | Enables late discovery but introduces scanning/loading, weaker static reachability, ambiguous version selection, and a second runtime activation path | Rejected |

## ✅ Decision

Each independently maintained EXPRESS schema baseline is distributed in its own optional assembly and NuGet package. A distribution unit may contain the complete closed set of EXPRESS schemas required by that baseline, but it must not combine independently versioned application protocols merely for convenience. The package name follows `TedToolkit.Step21.<SchemaFamily>` unless a separately accepted naming decision establishes another stable convention.

The package compiles generated entity interfaces and classes, value types, validation and mapping behavior, and the descriptors for its closed schema set into the schema assembly from pinned EXPRESS sources during the repository build. Consumers obtain those compiled public types without supplying the maintained `.exp` files. Custom and unmaintained schemas continue to use the existing `AdditionalFiles` generator path.

Every schema package depends on the schema-neutral `TedToolkit.Step21` runtime and reuses its parser, exchange model, validation contracts, and writer. It does not duplicate those facilities or add a domain facade. Analyzer and source-composition dependencies may participate in producing the schema assembly, but they do not become runtime dependencies of the generated object graph.

Consumers select schema behavior explicitly by passing the applicable generated descriptor or descriptors through the existing runtime boundary. No global registry, automatic assembly scan, service-provider lookup, convention-based activation, or runtime schema compilation is introduced.

A schema package is versioned independently under SemVer 2.0.0; it does not inherit the core package version merely because both are built from one repository. Before its first stable release it uses a prerelease suffix. The first stable public contract is `1.0.0`.

The versioned public contract of a maintained schema package includes:

- its package identifier and declared target frameworks;
- the generated public/protected API, including type kinds, names, inheritance, generic constraints, constructors, members, nullability, aggregate shapes, enumeration/select alternatives, and caller-visible XML documentation;
- the observable generated schema semantics used for binding, hydration, validation, projection, diagnostics, and canonical writing;
- its pinned schema-baseline manifest: upstream identity, declared edition/variant, source paths and integrity hashes, closed schema set, normalized descriptor identities, redistribution license, and stated conformance scope;
- its declared dependency range on `TedToolkit.Step21`.

Package version increments are classified against the most recent stable release of the same package:

| Increment | Permitted change |
| --- | --- |
| Patch | Backward-compatible bug or documentation/provenance correction with no generated public-shape removal/change, no descriptor-identity change, no semantic incompatibility, and no narrowing of the supported core-runtime range. A source-byte or upstream-revision correction is Patch only when generated public shape and declared semantics remain equivalent and the new provenance is explicit. |
| Minor | Backward-compatible added generated API or schema behavior within the same declared edition/variant lineage, or deprecation of existing API, while existing valid consumer source/model journeys remain valid and the core-runtime range is not narrowed. |
| Major | Removed, renamed, retyped, reordered, newly required, less nullable, or otherwise source/binary/semantic-breaking generated surface; changed descriptor identity or closed schema set; incompatible validation/mapping/writer behavior; a different schema edition or vendor variant; or a narrowed core-runtime range. |

An ambiguous difference is classified at the higher increment. A different schema edition or vendor variant is always Major even if an API comparison happens to be empty, because the conformance identity changed. A released package version is immutable and is never rebuilt with different schema input, generator behavior, dependencies, or artifacts.

Each schema package declares an inclusive minimum tested `TedToolkit.Step21` version and an exclusive next-major upper bound, for example `[1.0.0,2.0.0)`. Raising the minimum or lowering the maximum narrows compatibility and therefore requires a schema-package Major version. Widening the range without changing behavior may be Patch after package-consumer and Native AOT evidence. Schema packages do not share a release number or cadence with the core runtime.

The package's generated surface is compared with the latest stable baseline before release. Package metadata, provenance documentation, descriptor identity, generated API comparison, runtime dependency range, and declared compatibility classification must agree. A prerelease may explore an unproven baseline, but it cannot be promoted to stable until that evidence exists.

Each published baseline records the exact upstream identity, normalized schema identity, source integrity, redistribution license, generator/runtime compatibility, and supported conformance scope. Replacing the source with another edition or vendor variant is an explicit Major compatibility decision, not a silent package rebuild. An OID retained from `FILE_SCHEMA` is not package identity unless a later accepted decision adds an OID-to-baseline registry and verification contract.

## 💡 Why this decision now

The existing generator and descriptor bridge already separate schema semantics from the runtime and prove a real package consumer without runtime discovery. A maintained precompiled distribution can therefore remove repeated consumer setup without changing the public read/write model. Selecting the package boundary before the first maintained schema ships avoids turning one initial application protocol into an accidental core dependency or universal bundle.

Separate optional packages conform to the schema-neutral dependency direction and localize source provenance, payload, compatibility, and deprecation. The status quo remains available for custom schemas. A core bundle and runtime discovery violate required principles; a combined package has no evidenced cross-schema consumer requirement strong enough to justify coupled releases and payload.

Reconsideration is justified if measured dependency duplication across schema packages becomes material, consumers require one coordinated distribution unit, NuGet analyzer transitivity cannot preserve the intended build/runtime graph, or a future .NET deployment model supplies statically analyzable late binding with equivalent Native AOT evidence.

## 🔗 Evidence and links

- [Product intent](../product/README.md)
- [Design principles](../principles/README.md)
- [Schema-bound round-trip architecture](../architecture/schema-bound-round-trip.md)
- [Package and API guide](../../src/TedToolkit.Step21/README.md)
- [Native AOT package proof](../conformance/native-aot-package-proof.md)
- [ADR-0003: AOT-ready schema validation](ADR-0003-aot-ready-schema-validation.md)
- [ADR-0005: Explicit-section graph registration](ADR-0005-explicit-section-graph-registration.md)
- [Semantic Versioning 2.0.0](https://semver.org/)
- [NuGet package versioning and dependency ranges](https://learn.microsoft.com/nuget/concepts/package-versioning)
- [.NET library versioning guidance](https://learn.microsoft.com/dotnet/standard/library-guidance/versioning)

## ⚖️ Consequences and accepted trade-offs

- Common schemas become usable through a package reference and explicit descriptor without consumer-managed schema sources.
- The core runtime remains independent of every application protocol and retains one parser/model/writer implementation.
- Each maintained baseline adds a package, provenance record, compatibility surface, conformance boundary, payload, and release/deprecation obligation.
- Consumers that also generate the same schema declarations can create duplicate public type names; package guidance must define those inputs as mutually exclusive.
- Several schema packages may contain generated code for shared imported declarations when their closed baselines require independent ownership. Deduplication is not allowed to create a runtime registry or cross-package release lock without a superseding decision.
- Explicit descriptor selection adds one deliberate caller step and prevents hidden schema/version selection.
- Generated types are available at runtime without dynamic activation, preserving trimming and Native AOT analysis.
- The package-per-baseline direction does not promise one common version number or release schedule across the core and all schema packages.
- Conservative core-runtime upper bounds can require a new schema-package release before consumers adopt a future core major; this deliberate restore-time failure is accepted instead of claiming unproved binary/runtime compatibility.
- Treating every edition/vendor-variant change as Major may increase major-version frequency, but makes the conformance identity visible even when generated API happens to look unchanged.

## 🛠️ Downstream delivery constraints

- A maintained package contains only types and behavior traceable to its pinned closed EXPRESS source set plus clearly named implementation infrastructure.
- The core runtime does not reference or automatically discover a schema package.
- A schema package reuses the core descriptor, exchange model, validation, diagnostic, and writer boundaries and introduces no alternate reader, writer, model root, or domain facade.
- Consumers can use the compiled schema surface without providing the maintained source set as build input.
- Runtime assets contain no Analyzer-only, source-composition, dynamic-compilation, general serialization, or reflection-discovery dependency introduced by the schema package.
- Descriptor identity, source provenance, integrity, license, generator/runtime compatibility, conformance scope, and unsupported boundary are declared for every published baseline.
- Packaging and deployment evidence covers the actual NuGet graph and a representative trimmed Native AOT consumer before release.
- Duplicate consumer generation of the same schema surface is rejected or clearly prevented; it is never silently resolved by first-wins selection.
- Schema source replacement, edition change, public generated-shape change, or descriptor-identity change is classified through the declared compatibility policy.
- Stable releases follow SemVer 2.0.0 against the complete declared schema-package public contract; prerelease identifiers are used until the baseline, package graph, generated surface, conformance scope, and Native AOT boundary are proven.
- A schema package declares a bounded core-runtime dependency range from its minimum tested version to the next core major; a narrower range is a schema-package Major change.
- Patch and Minor classification require evidence against the latest stable package baseline; uncertainty is classified as Major rather than silently accepted.

## 🔄 Exit requirements

A replacement distribution model must preserve the schema-neutral runtime, explicit and deterministic schema selection, source provenance and licensing, generated public-contract traceability, one parser/model/writer implementation, and an equivalent trimming and Native AOT proof. Existing schema-package consumers must have an explicit compatibility and migration boundary before packages are combined, renamed, dynamically loaded, or absorbed into another distribution.

## 📅 Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Accept, revise, or reject this package boundary | repository maintainer | Approved explicitly on 2026-08-26 | Closed |
| Approve the generated-API, schema-semantic, provenance, and core-runtime compatibility policy in this ADR | repository maintainer | Approved explicitly on 2026-08-26 | Closed |
| Reassess package-per-baseline isolation | repository maintainer | Measured shared payload or coordinated-consumer requirements make independent packages materially harmful | Open |
| Reassess explicit descriptor selection | repository maintainer | A statically analyzable discovery model supplies equivalent determinism and Native AOT evidence | Open |
| Reassess provenance obligations | repository maintainer | Upstream licensing, redistribution terms, or schema publication policy changes | Open |
