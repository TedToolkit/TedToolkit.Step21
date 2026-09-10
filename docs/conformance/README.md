# Conformance and capability matrix

This index states what the current package delivers, what the grammars recognize without operational support, and what is outside the public contract. ISO clauses govern behavior; independently sourced corpora are regression evidence only and cannot broaden or override the normative grammar.

## Delivered

| Capability | Evidence |
| --- | --- |
| ISO 10303-21:2016 Edition 3 complete clear-text syntax | [Part 21 grammar traceability](part21-edition3-grammar.md) |
| ISO 10303-11:2004 Edition 2 EXPRESS syntax and closed-set binding | [EXPRESS grammar traceability](express-edition2-grammar.md), [closed-set binding](express-closed-set-binding.md) |
| Generated mutable entity classes, values, aggregates, and sealed class descriptors | [entity hierarchy](generated-entity-hierarchy.md), [values](generated-schema-values.md), [aggregates](generated-schema-aggregates.md), [descriptors](generated-schema-descriptors.md) |
| Documented statically generated validation-reachable EXPRESS rule closure and structural validation | [reachable rules](reachable-express-rules.md), [structural validation](structural-validation.md) |
| Atomic simple read, same-schema sections, local references, and multi-schema populations | [simple read](atomic-simple-read.md), [same-schema sections](same-schema-data-sections.md), [reference hydration](reference-hydration.md), [multi-schema populations](multi-schema-populations.md) |
| Edition 3 anchors, occurrence identities, UUID mapping, and schema-neutral reference declarations | [anchor/occurrence/UUID semantics](anchor-occurrence-uuid.md) |
| Explicit distributed reference, directory, and ZIP resolution | [distributed resource resolution](distributed-resource-resolution.md) |
| Detached CMS signature decoding, explicit signature verification and trust policy, and atomic signing | [CMS signatures](cms-signatures.md) |
| Annex E schema populations, all determination methods, and explicit domain equivalence | [schema populations](schema-populations.md) |
| Deterministic simple writing and source-bounded complex read-write-read mapping | [simple writing](canonical-simple-writing.md), [complex mapping](complex-mapping-round-trip.md), [pre-write validation](atomic-prewrite-validation.md) |
| Packed consumer and real `win-x64` Native AOT publish/run | [package/AOT proof](native-aot-package-proof.md) |
| Pinned class-1 allocation regression and zero unused feature-service construction | [class-1 performance](class1-performance.md) |

Repository-owned semantic round-trip fixtures compare entity identity, generated type, values, aggregate semantics, and reference identity after write/read. Canonical formatting may differ from the input; the opt-in external corpus is syntax evidence only.

## Source-excluded operations

| Facility | Observable boundary |
| --- | --- |
| EXPRESS constant occurrences | Constant occurrence names remain a separately tracked DATA-binding capability because the supplied ISO 10303-11 files do not describe the required evaluation contract. |
| Complete complex-instance evaluated sets | The bounded internal/external mapping profile works, including nested `ONEOF`/`ANDOR`, but complete valid evaluated-set membership is not claimed because ISO 10303-11 Annex B is absent from the supplied files. |

These are deliberate source exclusions, not syntax errors or pending inferred implementations. The public
`ExchangeStructure.Read` boundary returns the complete exact diagnostic set and never publishes a partial model.

## Outside the public contract

- General JSON or XML model serialization, attributes, or unrelated adapter frameworks.
- Annex F ECMAScript/JavaScript language binding, script engines, and script-host bridges; this repository delivers a C# library rather than a multi-language SDK.
- Byte-preserving round trips, comment retention, or original formatting retention.
- ISO 10303-22 repositories and inferred domain equivalence.
- General EXPRESS interpretation, arbitrary algorithm execution, or a public function/procedure invocation API.
- Public syntax nodes, ANTLR contexts, parser/reader/writer façades, or descriptor registries.

## Evidence governance

Normative evidence is recorded production-by-production in the [Part 21](part21-edition3-grammar.md) and [EXPRESS](express-edition2-grammar.md) traceability records. A grammar behavior change requires the applicable ISO edition/clause or production, focused positive and neighboring invalid fixtures, deterministic regeneration, and regression results. Corpus frequency alone is not authority.

Corpus regression evidence comes from repository-owned fast fixtures plus an explicit opt-in NIST/buildingSMART manifest. Every network artifact has an HTTPS download/source page, recorded license, exact byte size, SHA-256, and a Git-ignored cache. The independent corpus proves declared syntax compatibility; representative generated repository fixtures and the packed consumer prove schema-bound semantic read-write-read behavior.
