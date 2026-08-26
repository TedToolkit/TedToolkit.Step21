<!-- delivery-map -->
## Delivery map

This file is the only mutable work-item status source. Item contracts remain stable after approval.

Status evidence as of this revision: AP203-003 and AP203-001 are integrated with complete proof and
independent `Ready` reviews; the reviewed AP203-001 range ends at `28ba730`. Candidate revision
`f66c33d` still contains the AP203-002 completion evidence and the start of AP203-004; those artifacts
await reconciliation onto this delivery baseline. `Implemented` means the item has complete evidence;
`Implementing` means implementation exists or has started but its authoritative completion record or
review is not yet closed.

| ID | Outcome | Contract ownership | Real prerequisites and supplied input | Primary proof | Status | Document |
| --- | --- | --- | --- | --- | --- | --- |
| AP203-001 | Fix and compile the reproducible AP203 schema baseline | Owns AC-02 / Supports AC-12 | None | Generated-fidelity contract tests against the pinned schema | Implemented | `work-items/AP203-001-reproducible-schema-baseline.md` |
| AP203-002 | Deliver the precompiled AP203 package boundary | Owns AC-01 / Owns AC-10 / Supports AC-11 / Supports AC-12 | AP203-001: checked-in schema, provenance, and verified generated surface | Package-only consumer contract test | Implemented | `work-items/AP203-002-precompiled-package.md` |
| AP203-003 | Match standard schema identifiers without changing retained header text | Owns AC-03 / Owns AC-04 / Owns AC-08 / Supports AC-05 | None | In-memory identifier compatibility contract tests | Implemented | `work-items/AP203-003-schema-identifier-matching.md` |
| AP203-004 | Read the fixed OCCT AP203 sample atomically through generated types | Owns AC-05 / Owns AC-09 / Supports AC-06 / Supports AC-11 / Supports AC-12 | AP203-001: generated AP203 surface; AP203-002: usable package assembly; AP203-003: verified identifier matching | Checked-in OCCT fixture integration tests | Implementing | `work-items/AP203-004-occt-schema-bound-read.md` |
| AP203-005 | Preserve AP203 semantics through edit, validation, write, and reread | Owns AC-06 / Owns AC-07 / Supports AC-11 | AP203-004: verified typed fixture graph and observable semantic baseline | Semantic round-trip and atomic-write integration tests | Approved | `work-items/AP203-005-semantic-round-trip.md` |
| AP203-006 | Prove the versioned packaged AP203 journey is offline-reproducible and Native AOT safe | Owns AC-11 / Owns AC-12 / Owns AC-13 | AP203-002: package-only consumer boundary; AP203-005: complete verified journey and fixture; Accepted ADR-0006: version compatibility policy | Clean offline package proof, package compatibility audit, and trimmed Native AOT execution | Approved | `work-items/AP203-006-aot-reproducibility-closure.md` |
