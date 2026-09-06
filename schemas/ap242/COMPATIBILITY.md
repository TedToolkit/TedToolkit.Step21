# AP242 baseline compatibility

`BASELINE.json` records the approved first-release input contract. It distinguishes the exact patched
build input from its upstream STEPcode source, so the two documented type corrections cannot be hidden
by changing only a hash. This document does not establish NuGet.org publication or full AP242 conformance.

## Classification

The intended version is **initial stable 1.0.0**, not a Patch or Minor update to an existing stable AP242
package. There is no previous stable baseline. Locally built prerelease candidates are verification inputs,
not previous stable releases.

| Dimension | First-release input and required comparison |
| --- | --- |
| Source identity | STEPcode `9baa5dadaa1dcfcdc623220d865d36d61ea351e9`, `data/ap242/242_mim_lf.exp`; compare both upstream and exact patched canonical-LF SHA-256 values with `BASELINE.json` and `PROVENANCE.md` |
| Edition/variant | ISO TC184/SC4/WG12 N11521, ISO/TS 10303-442 AP242 MIM long form; no substituted edition or vendor variant |
| Local corrections | Only the two type-correcting rewrites documented in `PROVENANCE.md`; no declaration, rule, alternative or public function removal |
| Descriptor | Exactly `AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF`, exposed through the descriptor named in `BASELINE.json` |
| Closed schema set | Exactly the single schema listed in `closedSchemas`; no added schema package or runtime registry |
| Runtime compatibility | `.csproj`, packed nuspec and restored dependency graph agree on `[1.0.0,2.0.0)` |
| Generated API | The complete public/protected symbol and documentation snapshot matches `PublicApi.approved.sha256`; private shared storage, TYPEOF helpers and shard layout are excluded |
| Generated semantics | All reachable rules remain enabled; the fixed AP242DIS fixture passes typed read/edit/validate/write/reread and atomic failure checks |
| Redistribution/conformance | Retain STEPcode and OCCT provenance/license evidence and the bounded fixture claim; do not infer full AP242 conformance |

## Bounded release comparison procedure

1. Run the AP242 source/descriptor/API contract tests and isolated package-consumer test. Compare the
   source hashes, nominal descriptor, generated API, nuspec and resolved runtime range with every manifest
   field. A disagreement blocks promotion; a different source, edition or correction set requires renewed
   approval.
2. For this first delivery, compare against the approved initial contract because `previousStableBaseline`
   is null. Later releases must retain and compare the accepted stable manifest, API snapshot and semantic
   journey. An unknown previous baseline never justifies Patch or Minor classification.
3. A changed edition, descriptor, closed set, incompatible public surface or semantics, or narrowed runtime
   range requires Major. A source revision/hash change is Patch only when generated-API and semantic
   equivalence are explicitly demonstrated; otherwise classify conservatively as Major. Minor requires
   demonstrated backward-compatible additions in the same edition lineage.
4. Attach the normalized repeat-package comparison, AP203/AP242 coexistence graph, regression results and
   Native AOT proof to the exact reviewed candidate. Never replace an already published version.

See [ADR-0006](../../docs/adr/ADR-0006-precompiled-schema-package-distribution.md).
