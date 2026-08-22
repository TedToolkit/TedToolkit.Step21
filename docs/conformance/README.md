# Conformance and capability matrix

This index states what the current package delivers, what the grammars recognize without operational support, and what is outside the public contract. ISO clauses govern behavior; independently sourced corpora are regression evidence only and cannot broaden or override the normative grammar.

## Delivered

| Capability | Evidence |
| --- | --- |
| ISO 10303-21:2016 Edition 3 complete clear-text syntax | [Part 21 grammar traceability](part21-edition3-grammar.md) |
| ISO 10303-11:2004 Edition 2 EXPRESS syntax and closed-set binding | [EXPRESS grammar traceability](express-edition2-grammar.md), [closed-set binding](express-closed-set-binding.md) |
| Generated mutable entity classes, values, aggregates, and sealed class descriptors | [entity hierarchy](generated-entity-hierarchy.md), [values](generated-schema-values.md), [aggregates](generated-schema-aggregates.md), [descriptors](generated-schema-descriptors.md) |
| Static validation-reachable EXPRESS rules and structural validation | [reachable rules](reachable-express-rules.md), [structural validation](structural-validation.md) |
| Atomic simple read, same-schema sections, local references, and multi-schema populations | [simple read](atomic-simple-read.md), [same-schema sections](same-schema-data-sections.md), [reference hydration](reference-hydration.md), [multi-schema populations](multi-schema-populations.md) |
| Deterministic simple writing and supported flat `ANDOR` complex read-write-read mapping | [simple writing](canonical-simple-writing.md), [complex mapping](complex-mapping-round-trip.md), [pre-write validation](atomic-prewrite-validation.md) |
| Packed consumer and real `win-x64` Native AOT publish/run | [package/AOT proof](native-aot-package-proof.md) |

Semantic round-trip evidence compares entity identity, generated type, values, aggregate semantics, and reference identity after write/read. Canonical formatting may differ from the input.

## Syntax recognized; operation unsupported

| Facility | Observable boundary |
| --- | --- |
| `ANCHOR` sections | Parsing succeeds; requesting anchor resolution reports `P21-CAP-ANCHOR`. |
| `REFERENCE` sections and external value occurrences | Syntax is retained. A model use of a declared unresolved external entity reports `P21.READ.REFERENCE.EXTERNAL`; other unsupported external-reference/value operations report `P21-CAP-REFERENCE`. No public external resource acquisition or resolver is exposed. |
| `SIGNATURE` sections | Parsing succeeds; signature verification reports `P21-CAP-SIGNATURE`. |

These are operational capability failures, not syntax errors. The public `ExchangeStructure.Read` boundary returns the complete exact diagnostic set and never publishes a partial model.

## Outside the public contract

- JSON or XML serialization, adapters, attributes, extension hooks, or dependencies.
- Byte-preserving round trips, comment retention, or original formatting retention.
- Complex mapping beyond the supported flat `ANDOR` hierarchy and SDAI domain-equivalence metadata.
- General EXPRESS interpretation, arbitrary algorithm execution, or a public function/procedure invocation API.
- Public syntax nodes, ANTLR contexts, parser/reader/writer façades, descriptor registries, or resource resolvers.

## Evidence governance

Normative evidence is recorded production-by-production in the [Part 21](part21-edition3-grammar.md) and [EXPRESS](express-edition2-grammar.md) traceability records. A grammar behavior change requires the applicable ISO edition/clause or production, focused positive and neighboring invalid fixtures, deterministic regeneration, and regression results. Corpus frequency alone is not authority.

Corpus regression evidence comes from repository-owned fast fixtures plus an explicit opt-in NIST/buildingSMART manifest. Every network artifact has an HTTPS download/source page, recorded license, exact byte size, SHA-256, and a Git-ignored cache. The independent corpus proves declared syntax compatibility; representative generated repository fixtures and the packed consumer prove schema-bound semantic read-write-read behavior.
