# Repository design principles

## Scope and precedence

- Governed scope: all production code, grammars, generated sources, public APIs, tests, and documentation in TedToolkit.Step21.
- External hard constraints that take precedence: applicable ISO 10303-21 and ISO 10303-11 requirements, licensing, and platform security requirements.
- Product intent: [`docs/product/README.md`](../product/README.md).
- Precedence: approved product intent guides principles; principles guide architecture design; approved architecture constrains change design; approved work items constrain implementation.

## Principle index

| ID | Title | Strength | Status | Owner | Review trigger | Document |
| --- | --- | --- | --- | --- | --- | --- |
| AP-001 | ISO 10303-21 defines the product boundary | Required | Active | repository maintainer | A public API or capability is proposed around one application protocol or non-ISO representation | [`architecture.md`](architecture.md) |
| AP-002 | Keep the runtime schema-neutral | Required | Active | repository maintainer | The runtime would depend on a generated schema, generator implementation, or domain package | [`architecture.md`](architecture.md) |
| AP-003 | Do not add domain concepts beyond ISO 10303-21 and EXPRESS | Required | Active | repository maintainer | A public domain type/member lacks a standard/schema source, or infrastructure is presented as standard/schema semantics | [`architecture.md`](architecture.md) |
| EP-001 | Change grammars only from standard evidence | Required | Active | repository maintainer | Any `.g4` production is added, removed, broadened, or narrowed | [`engineering.md`](engineering.md) |
| EP-002 | Preserve ISO semantics before convenience | Required | Active | repository maintainer | An ergonomic API would lose model-owned identity, schema mapping, unsupported behavior, or write-time validity | [`engineering.md`](engineering.md) |
| EP-003 | Prefer static contracts and keep runtime paths Native AOT-ready | Required | Active | repository maintainer | Runtime behavior would require unbounded reflection, assembly scanning, dynamic code generation, or an AOT-unverified dependency | [`engineering.md`](engineering.md) |
| EP-004 | Make conformance claims explicit and evidence-backed | Required | Active | repository maintainer | A syntax, mapping, validation, writing, or interoperability capability is added, broadened, narrowed, or reclassified | [`engineering.md`](engineering.md) |

## Exception route

A deviation from a Required principle needs an accepted ADR before implementation. The ADR must identify the narrower scope, the external constraint or evidence justifying the deviation, compatibility consequences, and an objective review trigger. Emergency security remediation may temporarily precede the ADR, but the maintainer must either record the exception or restore conformance before the next release.

## Maintenance

- Principle-set owner: repository maintainer
- Review triggers: an ISO baseline revision, a new serialization boundary, a runtime dependency-direction change, a domain-specific public abstraction, an unproven conformance claim, or a runtime dependency that cannot pass Native AOT analysis and execution evidence.
- Last reviewed: 2026-08-26
