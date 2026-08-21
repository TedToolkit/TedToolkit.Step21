# Repository design principles

## 📝 Clarification and decision log

| ID | Question and why it mattered | Recommended answer | User decision and source | Affected sections | Status |
| --- | --- | --- | --- | --- | --- |
| DP-01 | Whether the standard or a sampled application protocol governs recurring design choices | Make ISO 10303-21 the required boundary | The maintainer explicitly selected ISO 10303-21 as the repository-wide scope on 2026-08-21 | AP-001, EP-001, EP-002 | Resolved |
| DP-02 | Which serialization contracts generated types must support | Require ISO 10303-21 semantic writing only | The maintainer excluded JSON and XML contracts on 2026-08-21 | EP-002 | Resolved |
| DP-03 | How standard fidelity and C# conventions are ordered | ISO/EXPRESS semantics first; idiomatic C# only among semantically equivalent representations | The maintainer established this repository-wide priority on 2026-08-21 and prohibited library-invented domain concepts | AP-001, AP-003, EP-002 | Resolved |

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
| AP-003 | ISO semantics first, idiomatic C# second | Required | Active | repository maintainer | A public domain type/member lacks a standard/schema source, or a C# convenience changes semantics | [`architecture.md`](architecture.md) |
| EP-001 | Change grammars only from standard evidence | Required | Active | repository maintainer | Any `.g4` production is added, removed, broadened, or narrowed | [`engineering.md`](engineering.md) |
| EP-002 | Preserve ISO semantics behind a natural entity API | Required | Active | repository maintainer | An ergonomic API would lose model-owned identity, schema mapping, unsupported behavior, or write-time validity | [`engineering.md`](engineering.md) |

## Exception route

A deviation from a Required principle needs an accepted ADR before implementation. The ADR must identify the narrower scope, the external constraint or evidence justifying the deviation, compatibility consequences, and an objective review trigger. Emergency security remediation may temporarily precede the ADR, but the maintainer must either record the exception or restore conformance before the next release.

## Maintenance

- Principle-set owner: repository maintainer
- Review triggers: an ISO baseline revision, a new serialization boundary, a runtime dependency-direction change, or a domain-specific public abstraction.
- Last reviewed: 2026-08-21
