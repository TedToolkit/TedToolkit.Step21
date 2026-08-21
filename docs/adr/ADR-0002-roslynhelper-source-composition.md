# ADR-0002: Compose generated C# structurally with TedToolkit.RoslynHelper

- Status: Accepted
- Date: 2026-08-21
- Decision owner: repository maintainer
- Decision scope: all C# emitted by the EXPRESS incremental source generator
- Applicable product intent: [`../product/README.md`](../product/README.md)
- Applicable principles: [`AP-002`](../principles/architecture.md) and [`EP-002`](../principles/engineering.md)
- Supersedes: None
- Superseded by: None

## 📝 Clarification and decision log

| ID | Question and why it mattered | Recommended answer | User decision and source | Affected sections | Status |
| --- | --- | --- | --- | --- | --- |
| ADR-C-01 | Which source-composition abstraction the generator must use | Use TedToolkit.RoslynHelper and keep output structural until final emission | The maintainer explicitly required TedToolkit.RoslynHelper on 2026-08-21 | Entire decision | Resolved |

## 📌 Decision at a glance

The EXPRESS incremental generator shall compose files, declarations, members, statements, expressions, types, attributes, and documentation with TedToolkit.RoslynHelper syntax objects and emit once per stable hint name.

## 🧭 Context and decision question

The project needs to generate interfaces, records, record structs, schema metadata, factories, validation, and XML documentation from semantic EXPRESS declarations. Generated code must remain deterministic, correctly qualified, nullable-aware, and independent of generator-only libraries.

The decision is whether to concatenate source text, use Roslyn `SyntaxFactory` directly, or use the maintainer-selected TedToolkit.RoslynHelper structural model.

## 🎯 Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Use TedToolkit.RoslynHelper | Maintainer direction, 2026-08-21 | Must |
| Hard constraint | Analyzer remains compatible with .NET Standard 2.0 | Current Analyzer target and TedToolkit repository documentation | Must |
| Hard constraint | Generated code does not reference generator-only packages | AP-002 | Must |
| Driver | Preserve C# structure until final emission | TedToolkit.RoslynHelper usage contract | High |
| Driver | Avoid custom fragments and source-string concatenation | Generator correctness and maintainability | High |

## 🔎 Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Source-string templates | Common approach; high confidence | No | Violates the maintainer constraint and makes punctuation/qualification correctness manual | Rejected |
| Raw Roslyn SyntaxFactory | Official Roslyn API; high confidence | No | Structural but substantially more verbose and bypasses the selected helper | Rejected |
| TedToolkit.RoslynHelper | Package availability through 2026.7.15 verified from NuGet; API and netstandard2.0 intent documented by TedToolkit | Yes | Adds an analyzer-time package and packaging obligation | Selected |

## ✅ Decision

Pin `TedToolkit.RoslynHelper` 2026.7.15 initially through central package management. Use `SourceComposer<TGenerator>` factories for generated declarations and members so generated-code attributes are applied consistently. Use non-generic `SourceComposer` factories for files, namespaces, imports, types, and supporting syntax.

Compose `SourceFile -> NameSpace -> declarations -> members -> statements/expressions`, then call `Generate(context, stableHintName)` exactly once per source unit. Do not use source text for types, generic arguments, braces, punctuation, indentation, or directives. Use `Custom` only after a documented public-API gap is confirmed and the smallest fragment is reviewed.

TedToolkit.RoslynHelper and its required runtime dependencies are packaged beside the Analyzer assembly. They are not dependencies of generated code or TedToolkit.Step21 runtime APIs.

## 💡 Why this decision now

The generator's composition abstraction shapes every emitter and test. The maintainer selected the helper before implementation, and the package is compatible with the Analyzer's target framework. Establishing one structural path prevents parallel template and syntax-tree emitters from emerging.

## 🔗 Evidence and links

- [`externals/TedToolkit/README.md`](../../externals/TedToolkit/README.md), including the helper purpose and .NET Standard 2.0 compatibility statement
- NuGet package search performed 2026-08-21, latest visible version `2026.7.15`
- [`schema-bound-round-trip.md`](../architecture/schema-bound-round-trip.md), syntax-object map

## ⚖️ Consequences and accepted trade-offs

- Generator code is expressed in the helper's syntax vocabulary rather than hand-written C# text.
- Analyzer packaging must carry the helper dependency because source generators execute inside the compiler.
- Generated consumer code remains free of helper references.
- A helper API gap may require an upstream helper enhancement or a separately reviewed minimal custom fragment.
- Upgrading the helper requires generator snapshot/compilation verification because formatting or syntax behavior may change.

## 🛠️ Downstream delivery constraints

- Map every required construct to a public syntax object before emitter implementation.
- Prefer non-positional records with explicit syntax-object constructors/properties if primary-record support is incomplete.
- Use `DataType` rather than rendered or concatenated type-name strings.
- Generate stable, collision-free hint names derived from normalized schema identity and declaration identity.
- Verify qualification, nullability, attributes, generic constraints, record modifiers, and generated-source compilation.

## 🔄 Exit requirements

A replacement requires a superseding ADR and must preserve structural composition, deterministic emission, generated-code independence, and coverage of all required declaration and statement forms.

## 📅 Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Confirm the pinned package API can represent every item in the architecture syntax-object map | generator maintainer | Before generator implementation | Open |
| Reassess the pinned version | repository maintainer | Security advisory, incompatible Roslyn dependency, or missing required syntax | Open |
