# AP214 baseline compatibility

`BASELINE.json` records the approved first-release input contract. The package remains a prerelease
until its package, semantic, reproducibility, Native AOT and independent review gates pass. This
document does not establish release acceptance or external publication.

## Classification

The intended version is **initial stable 1.0.0**, not a Patch or Minor update to an existing stable
AP214 package. The approved change declares the first stable contract; repository package history
contains the initial candidate and its prerelease correction, with no AP214 release tag or retained
previous stable baseline. Locally built stable-numbered candidates are not previous stable releases.

| Dimension | First-release input and required comparison |
| --- | --- |
| Source identity | STEPcode `9baa5dadaa1dcfcdc623220d865d36d61ea351e9`, `data/ap214e3/AP214E3_2010.exp`; compare canonical-LF SHA-256 with `BASELINE.json` and `PROVENANCE.md` |
| Edition/variant | `ISO/DIS 10303-214:2007`, source date `2009-06-30`; no substituted edition or vendor variant |
| Descriptor | Exactly `AUTOMOTIVE_DESIGN`, exposed through the descriptor named in `BASELINE.json` |
| Closed schema set | Exactly the single schema listed in `closedSchemas`; no added imported schema package |
| Runtime compatibility | `.csproj`, packed nuspec and restored dependency graph must agree on `[1.0.0,2.0.0)` |
| Generated API | The complete public/protected symbol and documentation snapshot must match `PublicApi.approved.sha256`; private shard layout is excluded |
| Generated semantics | All reachable schema rules remain enabled. The raw AP214IS fixture fails its two documented edition rules; the deterministic test-only migration passes typed read/edit/validate/write/reread, complete physical value/reference comparison and atomic failure checks |
| Redistribution/conformance | Retain the STEPcode and OCCT provenance/license evidence and the bounded fixture claim; do not infer full AP214 conformance |

## Bounded release comparison procedure

1. Run the AP214 source/descriptor/API contract tests and the isolated package-consumer test. Compare
   their actual source hash, nominal descriptor, generated API, nuspec and resolved runtime range
   against every corresponding manifest field. A disagreement blocks promotion; changing the
   manifest to another source or edition requires renewed approval.
2. Identify the latest accepted stable baseline of this package. For this first delivery there is
   none, as declared above; all manifest dimensions must match the approved initial contract. For
   later releases, retain and compare that stable manifest, API snapshot and semantic journey before
   choosing a version. An unknown previous baseline does not justify Patch or Minor classification.
3. A changed edition, descriptor, closed set, incompatible public surface or semantics, or narrowed
   runtime range requires Major. A changed source revision/hash is not automatically compatible:
   Patch requires explicit generated-API and semantic equivalence; otherwise classify conservatively
   as Major. Minor requires demonstrated backward-compatible additions in the same edition lineage.
   Ambiguous differences use the higher increment. Repeated package bytes alone prove none of these
   compatibility claims.
4. Attach the observed comparison to the exact reviewed candidate, together with both clean-build
   manifests, full regression and Native AOT results. Promote the initial prerelease to `1.0.0` only
   after those gates and the internal redistribution audit close. Never replace a published version.

The generated API and semantic tests supply the executable comparison; the delivery review owns the
candidate-bound classification result. See [ADR-0006](../../docs/adr/ADR-0006-precompiled-schema-package-distribution.md).
