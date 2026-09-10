# ADR-0013: Use authoritative pristine EXPRESS schema sources

- Status: Accepted
- Date: 2026-09-10
- Decision owner: repository maintainer
- Decision scope: provenance, storage, compilation, and redistribution metadata for maintained EXPRESS schema packages
- Applicable product intent: [`../product/README.md`](../product/README.md)
- Applicable principles: [`AP-001`, `AP-002`, and `AP-003`](../principles/architecture.md)
- Supersedes: None
- Superseded by: None
- Approval source: the repository maintainer explicitly accepted breaking changes, selected ISO authority over STEPcode, and restricted EXP files to non-distributed local code-generation inputs on 2026-09-10

## 📌 Decision at a glance

Maintained schema packages generate from pristine authoritative EXPRESS files fetched into a Git-ignored local cache, adapt the compiler rather than patch those files, and never commit or pack the EXP bytes.

## 🧭 Context and decision question

The maintained AP203, AP214, and AP242 packages were built from files copied from STEPcode and accompanied by STEPcode licensing material. STEPcode is a legitimate open-source STEP implementation descended from the NIST STEP Class Library, but it is not ISO and cannot establish the normative identity or licensing terms of ISO schema publications. Two active schema files also contained local edits made to accommodate the generator.

The decision is whether maintained schema packages should continue treating an implementation repository as their source, vendor locally corrected copies, or bind directly to pristine publications from the standards authority.

## 🎯 Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Schema identity and semantics must be traceable to the organization that publishes the standard | AP-001 and AP-003; ISO publication endpoints | Must |
| Hard constraint | Downloaded schema bytes remain unmodified; compiler limitations are fixed in the compiler | Maintainer decision, 2026-09-10 | Must |
| Hard constraint | Source identity, retrieval URL, edition, and cryptographic hash remain auditable | Existing package provenance contract | Must |
| Hard constraint | Public availability does not imply ownership or redistribution permission | Copyright boundary identified by the maintainer | Must |
| Hard constraint | EXP files are local code-generation inputs only and are neither committed to Git nor included in source archives or packages | Maintainer distribution restriction, 2026-09-10 | Must |
| Hard constraint | Local-only software use does not itself establish licence permission; the operator must confirm applicable rights before download, use, or generation | ISO End Customer Licence Agreement checked 2026-09-10 | Must |
| Decision driver | A withdrawn schema without an ISO-hosted download may use the strongest public interoperability source if its non-ISO status is explicit | ISO AP214 status and MBx-IF schema catalog | High |

## 🔎 Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Continue sourcing schemas and license claims from STEPcode | Existing repository provenance; high confidence | No | Reproducible, but an implementation project is presented as standards and licensing authority | Rejected |
| Copy ISO files and retain local schema edits | Existing AP203/AP242 corrections; high confidence | Partially | Keeps the current generator working but destroys byte identity with the authoritative publication | Rejected |
| Commit pristine authoritative files and adapt the generator | ISO and MBx-IF published bytes plus recorded hashes; high confidence | Partially | Preserves byte identity but distributes third-party files through the repository | Rejected |
| Fetch pristine authoritative files into an ignored local cache and adapt the generator | ISO and MBx-IF endpoints plus pinned hashes; high confidence | Yes | Requires an explicit build prerequisite, compiler work, and an accepted breaking AP203 API migration | Selected |
| Remove every schema lacking an ISO-hosted EXP | ISO does not currently host an AP214 download; high confidence | Partially | Maximizes publisher purity but removes a maintained interoperability package unnecessarily | Rejected |

## ✅ Decision

AP203 and AP242 use byte-exact EXPRESS files downloaded from ISO-controlled publication endpoints. AP214 uses the byte-exact AP214 Edition 3 file published by the MBx Interoperability Forum because no current ISO-hosted AP214 EXPRESS download is available; its provenance must state that MBx-IF is an industry authority rather than ISO. A reproducible fetch step verifies every hash and stores the files only in a Git-ignored local cache used by code generation.

STEPcode may be consulted as implementation history or corroborating evidence, but it is not the source identity, standards authority, or license authority for maintained schema assets. Authoritative schema files are never changed to accommodate the parser, binder, emitter, or CLR type system.

Each maintained AP has exactly one EXPRESS build input identified in `schemas/SOURCES.json`. Patched copies, source overlays, and silent fallbacks are prohibited. If a publisher file produces a compiler conflict that cannot yet be resolved faithfully as general EXPRESS semantics, the affected build remains blocked until the publisher supplies a corrected authoritative file or a faithful compiler implementation is available.

## 💡 Why this decision now

The maintainer requires a source chain that distinguishes standards authority from an implementation repository and accepts the compatibility cost of correcting that boundary. Pristine authoritative bytes provide a stronger, independently reproducible identity than a downstream copy. Moving generator accommodations into the compiler also ensures future official schemas exercise the same language implementation instead of accumulating unreviewable local forks.

Reconsideration is justified if ISO publishes a corrected or superseding file, withdraws public access, or supplies explicit redistribution terms that change the packaging boundary.

## 🔗 Evidence and links

- [ISO SMRL version 12](https://standards.iso.org/iso/10303/smrl/v12/tech/smrlv12.zip)
- [ISO/TS 10303-403:2010](https://www.iso.org/standard/56238.html)
- [ISO 10303-242:2025](https://www.iso.org/standard/84300.html)
- [ISO/TS 10303-442 edition 7 MIM long form](https://standards.iso.org/iso/ts/10303/-442/ed-7/tech/express/mim_lf.exp)
- [ISO 10303-214:2010 withdrawn status](https://www.iso.org/standard/43669.html)
- [MBx-IF EXPRESS schema catalog](https://www.mbx-if.org/home/mbx/resources/express-schemas/)

## ⚖️ Consequences and accepted trade-offs

- AP203 adopts the official ISO/TS 10303-403 MIM long form and intentionally breaks the generated public schema contract based on the previous Amendment 1 source.
- AP242 removes its two local source corrections; the compiler must represent the official expressions without weakening diagnostics for genuinely invalid consumer schemas.
- AP242 has no secondary corrected input or errata overlay. Known compilation failures in the pinned ISO bytes remain visible rather than creating a repository-owned schema variant.
- AP214 remains available but is permanently labelled as an industry-authoritative fallback until an ISO-hosted publication is available.
- STEPcode-specific `COPYING`, `AUTHORS`, and `INTENT.md` files no longer accompany maintained schema sources.
- Each schema retains a publisher URL, edition identity, retrieval date, and exact SHA-256. Download identity and redistribution permission remain separate review questions.
- A clean checkout must run the documented fetch step before building maintained schema packages. These third-party AP EXP bytes are absent from Git, source archives, NuGet packages, and other published artifacts; repository-authored conformance fixtures are unaffected.
- Generated source and compiled packages may still be derivative material; their publication remains blocked until a separate rights review approves it.
- ISO's current terms identify digital integration, transformation, and operationalization in software as uses that may require additional rights. The explicit fetch acknowledgement records review of that boundary but neither grants rights nor substitutes for a licence.

## 🛠️ Downstream delivery constraints

- Package builds consume the recorded pristine bytes directly from an ignored local cache; no tracked or patched schema copy may become the build input.
- `schemas/SOURCES.json` identifies exactly one EXP input per package; no overlay, fallback source, or second generated working copy is permitted.
- Builds fail clearly when the verified cache is absent and direct users to the fetch procedure; builds do not download from the network implicitly.
- Compiler accommodations must implement general EXPRESS semantics and retain focused regression evidence.
- Public API baselines and package documentation identify the nominal `SCHEMA` declared by the selected publication.
- Source notices must not imply that a downstream project's license governs an ISO publication.
- No source archive or package includes third-party AP schema bytes. Generated code and compiled packages are not published until their redistribution rights are independently confirmed.
- The operator must establish that the applicable publisher terms cover download, local processing, and code generation before running the fetch or generator. “Not distributed” is a repository restriction, not a legal conclusion that the use is licensed.

## 🔄 Exit requirements

Any replacement source must be at least as authoritative, retain reproducible byte identity, preserve explicit non-ISO labelling where applicable, and treat resulting public API changes as a versioned migration.

## 📅 Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Reassess AP214 source | repository maintainer | ISO publishes an accessible AP214 schema or a successor package removes the need for AP214 | Open |
| Reassess redistribution boundary | repository maintainer | Before publishing generated source or a compiled package derived from the schema bytes | Open |
