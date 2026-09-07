# Schema populations and domain equivalence

The runtime implements the ISO 10303-21:2016 Annex E schema-population model without introducing an ISO 10303-22 repository dependency.

## Delivered behavior

- `SCHEMA_POPULATION` external-file triples retain location, optional last-visited timestamp, optional canonical Base64 digest, resolution status, and any resolved exchange structure. Content-only resources remain distinguishable without inventing a model.
- External structures are resolved only through the caller-supplied resource capability. Their populations are combined transitively with stable shared entity identity, including cycles.
- A supplied timestamp is verified only when it is deterministically later than the referenced structure creation timestamp.
- A supplied digest requires a signature on the declaring structure and uses the first signature section's digest algorithm over the referenced file bytes, including files that are not exchange structures. Mismatch, unavailable byte representations, and unsupported algorithms fail atomically.
- `FILE_POPULATION` implements `SECTION_BOUNDARY`, `INCLUDE_ALL_COMPATIBLE`, and `INCLUDE_REFERENCED`, for named sections or all sections.
- Governing-schema validation runs over the exact computed population.

## Domain-equivalence boundary

Cross-schema equivalence is opt-in through `ExchangeStructureReadOptions.WithDomainEquivalenceProvider`. The caller-owned provider supplies a complete symmetric and transitive relation using schema-qualified entity names and projects physical parameters between declared equivalent components. The runtime rejects duplicate, asymmetric, non-transitively closed, descriptor-unknown, null, failed, or identity-injecting projections before publishing a model.

The runtime owns allocation, hydration, canonical occurrence aliases, and target-schema validation for equivalent views. It does not infer equivalence from entity names or shapes and does not expose Part 22 repository objects.

## Proof

`SchemaPopulationConformanceTests` covers header round trips and malformed declarations, external resolution and cycles, timestamp and digest verification, all three population methods, governing-schema validation, the Annex E domain-equivalence example, reference validity, invalid relation metadata, and atomic write failures. Existing same-schema and multi-schema suites remain conditional regression gates.
