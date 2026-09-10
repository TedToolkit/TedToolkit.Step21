# ADR-0011: Use schema-owned public namespaces for generated EXPRESS types

- Status: Accepted
- Date: 2026-09-10
- Decision owner: repository maintainer
- Decision scope: public CLR namespace identity for every EXPRESS schema compiled by TedToolkit.Step21, including consumer-supplied schemas and maintained precompiled schema packages
- Applicable product intent: [`../product/README.md`](../product/README.md)
- Applicable principles: [`AP-001`, `AP-002`, and `AP-003`](../principles/architecture.md)
- Supersedes: None
- Superseded by: None
- Approval source: the repository maintainer explicitly selected `TedToolkit.Step21.Schemas.<SchemaPascalCase>` and requested implementation on 2026-09-10; package-version changes were explicitly excluded from this delivery

## 📌 Decision at a glance

Emit every public EXPRESS-derived type under `TedToolkit.Step21.Schemas.<SchemaPascalCase>`, independent of the package or assembly that contains it.

## 🧭 Context and decision question

The generator currently emits public schema types under `TedToolkit.Step21.Generated.<SchemaPascalCase>`. That path is deterministic and packaging-neutral, but `Generated` describes an implementation mechanism rather than the public domain category represented by the types. Maintained AP packages make this mismatch more visible because consumers obtain already compiled schema types and do not run generation themselves.

The decision is whether the stable namespace should continue to expose generation provenance, follow each application-protocol package, enter the runtime root directly, or identify the types as schema-owned public contracts.

## 🎯 Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Public domain identities follow the supplied EXPRESS schema rather than an application protocol or package | AP-001 and AP-003 | Must |
| Hard constraint | The schema-neutral runtime remains independent of every generated schema and package | AP-002 and ADR-0006 | Must |
| Hard constraint | The same normalized schema name maps to one CLR namespace regardless of whether code is compiled into a consumer or a maintained package | Existing custom-schema and precompiled-package workflows | Must |
| Decision driver | The namespace communicates a stable public domain category rather than a replaceable implementation technique | Maintainer decision, 2026-09-10 | High |
| Decision driver | Runtime contracts and schema-derived contracts remain visibly separated | Current runtime and generated type architecture | High |

## 🔎 Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Keep `TedToolkit.Step21.Generated.<Schema>` | Documented current generator contract; high confidence | Partially | Packaging-neutral, but exposes generation provenance as public domain organization | Rejected |
| Use `TedToolkit.Step21.Schemas.<Schema>` | Existing schema-name normalization and descriptor identity provide the complete naming input; high confidence | Yes | Introduces a deliberate breaking namespace migration | Selected |
| Use `TedToolkit.Step21.<Schema>` | Namespace analysis; high confidence | Partially | Shorter, but mixes schema-derived types with schema-neutral runtime contracts | Rejected |
| Use `TedToolkit.Step21.Ap203.<Schema>` or another package-rooted path | ADR-0006 package/schema separation; high confidence | No | Couples one schema's CLR identity to an application protocol, edition, and distribution unit | Rejected |

## ✅ Decision

Every EXPRESS schema emits its public descriptor, entity interfaces and classes, defined values, enumerations, selects, and related generated helpers under:

```text
TedToolkit.Step21.Schemas.<SchemaPascalCase>
```

`Schemas` identifies the semantic ownership of the public surface. It does not imply runtime schema discovery, a registry, or a schema value inside an exchange structure. `<SchemaPascalCase>` continues to derive deterministically from the EXPRESS `SCHEMA` name and remains independent of NuGet package and assembly names.

No parallel public types are retained under `TedToolkit.Step21.Generated`; duplicating the generated graph would create distinct CLR type identities and ambiguous descriptor ownership. The namespace migration is therefore an intentional public API compatibility break. Package-version edits are outside the implementing delivery, while release classification remains governed by ADR-0006.

## 💡 Why this decision now

Maintained precompiled schema packages mean many consumers never observe generation, so organizing their domain API by the word `Generated` exposes an irrelevant production detail. `Schemas` preserves the existing schema-neutral dependency direction and deterministic schema-name identity while describing what the types represent. A package-rooted namespace would instead make distribution choices part of the domain type system, contrary to AP-001 and ADR-0006.

## 🔗 Evidence and links

- [Product intent](../product/README.md)
- [Architecture principles](../principles/architecture.md)
- [Schema-bound round-trip architecture](../architecture/schema-bound-round-trip.md)
- [ADR-0006: precompiled schema package distribution](ADR-0006-precompiled-schema-package-distribution.md)
- [Generator host contract](../conformance/express-generator-host.md)

## ⚖️ Consequences and accepted trade-offs

- Consumers use domain-oriented namespaces such as `TedToolkit.Step21.Schemas.ConfigControlDesign` and `TedToolkit.Step21.Schemas.AutomotiveDesign`.
- All custom and maintained schemas retain one naming rule and one CLR identity per generated assembly.
- Existing source code, reflection strings, public API baselines, and compiled references using `.Generated.` must migrate.
- Physical generated-source directories and internal ANTLR-generated implementation namespaces are unaffected because they are not schema public API identities.
- Maintained package names remain application-protocol distribution labels and do not become schema namespaces.

## 🛠️ Downstream delivery constraints

- Every schema-facing emitter and cross-schema type qualification uses the selected namespace root consistently.
- Descriptor names and schema matching continue to preserve the nominal EXPRESS `SCHEMA` identity.
- Generated entity shape, binding, validation, projection, writing, Native AOT behavior, and package isolation remain unchanged apart from fully qualified CLR names.
- Consumer documentation, schema manifests, public API baselines, and executable package examples use the selected namespace.
- No compatibility facade, duplicate schema graph, runtime registry, or package-specific generator path is introduced.

## 🔄 Exit requirements

Any replacement must keep schema CLR identity deterministic and packaging-neutral, preserve the schema-neutral runtime dependency direction, and provide an explicit migration boundary for the public API.

## 📅 Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Reassess the namespace root | repository maintainer | A non-EXPRESS public schema family or unavoidable CLR collision cannot be represented without ambiguity | Open |
