# Replace downstream EXPRESS schemas with authoritative pristine publications

<!-- change-format: 3 -->
<!-- workflow-profile: controlled -->
<!-- change-kind: migration -->
<!-- change-status: in-progress -->
<!-- delivery-shape: single -->
<!-- candidate-binding: none -->
<!-- approval-source: user-explicit-delete-switch-local-generation-only-and-audited-ap242-repair-2026-09-10 -->

## Goal and rationale

<!-- section: goal-rationale -->

Make the maintained AP203, AP214, and AP242 packages consume the strongest available authoritative EXPRESS publication from a verified, Git-ignored local cache, with no STEPcode-owned schema or licensing material and no changes to the cached official bytes. AP242 uses a separate audited, hash-pinned compatibility generation input for two documented publisher-file contradictions. The maintainer explicitly accepts the resulting breaking AP203 contract and restricts EXP files to non-distributed code-generation inputs.

The governing decision is [ADR-0014](../../adr/ADR-0014-audited-schema-compatibility-transforms.md), which supersedes [ADR-0013](../../adr/ADR-0013-authoritative-express-schema-sources.md).

## Scope

<!-- section: scope -->

- Replace the active AP203 input with the byte-exact ISO SMRL v12 ISO/TS 10303-403 MIM long form.
- Replace the active AP242 input with the byte-exact ISO/TS 10303-442 edition 7 MIM long form.
- Replace the active AP214 input with the byte-exact MBx-IF AP214 Edition 3 long form and label it as non-ISO industry authority.
- Remove STEPcode schema copies, source URLs, and STEPcode-specific `COPYING`, `AUTHORS`, and `INTENT.md` files from the maintained schema packages.
- Add an explicit fetch-and-hash-verification procedure; ignore the downloaded EXP cache and fail builds clearly when it is absent rather than downloading implicitly.
- Adapt general EXPRESS parsing, binding, or emission where pristine official constructs expose unsupported semantics.
- Update package inputs, manifests, provenance, documentation, public API expectations, and focused schema/package evidence.

Non-goals:

- Claiming ownership of, relicensing, or approving redistribution of ISO or MBx-IF material.
- Committing or packaging the authoritative EXP files, or claiming that generated code is cleared for publication.
- Preserving the former AP203 generated public API or adding compatibility aliases for it.
- Modernizing withdrawn AP214 to AP242 or claiming MBx-IF is ISO.
- Changing runtime package versions or unrelated Part 21 behavior.

## Current and expected state

After migration, official sources are hash-verified downloads in an ignored local cache. AP203 and AP214 consume those sources directly; AP242 consumes a separately hash-pinned compatibility input produced only after official-source verification. The tracked repository contains the fetch manifest, deterministic transform, provenance, and build guard—not the EXP bytes.

## Compatibility, constraints, and risks

- AP203 intentionally changes from `CONFIG_CONTROL_DESIGN` Amendment 1 to the official ISO/TS 10303-403 MIM-LF schema and therefore changes generated names and public API.
- `TedToolkit.Step21.Ap203` is classified as a Major migration to `2.0.0`; package metadata, manifest, provenance, descriptor identity, and public API evidence must agree. AP214 and AP242 remain on their existing `1.0.0` classification unless implementation reveals an additional incompatible public or semantic change.
- The pristine AP242 edition 7 file produces two type conflicts also reproduced from ISO SMRL v12. By maintainer decision, the package uses the audited transform governed by ADR-0014; the official and generation inputs retain separate identities and exact hashes. See [`ap242-compilation-status.md`](ap242-compilation-status.md).
- AP242-specific corrections remain outside the schema-neutral compiler and must fail closed when the official hash, unique anchors, or transformed hash changes.
- AP214 has no ISO-hosted EXP located; its MBx-IF authority and withdrawn ISO status remain explicit.
- The official bytes must remain outside Git and retain their downloaded byte identity in the local cache.
- Existing unrelated working-tree changes are outside this delivery and must not be overwritten.
- EXP distribution is prohibited by repository policy. Under the ISO terms checked on 2026-09-10, local digital integration or transformation may itself require additional rights. The operator must establish applicable rights before download, use, or generation; publication of generated source or compiled packages requires a separate review. Provenance and the fetch acknowledgement are not licence grants.

## Start conditions

<!-- section: start-conditions -->
<!-- change-prerequisite: none -->

No cross-change prerequisite. The ISO and MBx-IF files and their expected SHA-256 values are already present in the ignored workspace cache. Continued use is conditional on the operator's applicable publisher rights; this change does not grant or adjudicate those rights.

## Behavior contract

<!-- section: behavior-contract -->
<!-- behavior-change: OB-01 -->

### AC-01 — Active inputs have authoritative byte identity

<!-- acceptance-case: AC-01 -->

```gherkin
Scenario: Audit maintained schema sources
  Given the maintained AP203, AP214, and AP242 schema build inputs
  When their local bytes, Git status, package contents, and provenance are inspected
  Then AP203 and AP242 match their recorded ISO publications, AP214 matches its recorded MBx-IF publication, no third-party AP schema EXP is tracked or packed, and no active source attributes the schemas or their licenses to STEPcode
```

### AC-02 — Verified publications compile through the approved generation boundary

<!-- acceptance-case: AC-02 -->

```gherkin
Scenario: Generate maintained schema packages
  Given the byte-exact authoritative EXP files and the hash-pinned AP242 compatibility transform
  When the repository compiler and source generator process all three schemas
  Then AP203 and AP214 use the verified official bytes directly, AP242 uses only the audited prepared input, and generation plus C# compilation complete without error diagnostics while neighboring invalid compiler inputs retain deterministic diagnostics
```

### AC-03 — AP203 exposes the official MIM-LF contract

<!-- acceptance-case: AC-03 -->

```gherkin
Scenario: Consume the migrated AP203 package
  Given a consumer references the rebuilt AP203 package
  When it resolves the generated descriptor and representative schema types and exercises an official-MIM-LF Part 21 fixture
  Then package version `2.0.0`, descriptor identity, generated API, typed read, validation, write, and reread follow `AP203_CONFIGURATION_CONTROLLED_3D_DESIGN_OF_MECHANICAL_PARTS_AND_ASSEMBLIES_MIM_LF`, while the former `CONFIG_CONTROL_DESIGN` fixture is rejected as an unsupported schema rather than advertised as compatible
```

### AC-04 — Existing AP214 and AP242 package journeys remain valid

<!-- acceptance-case: AC-04 -->

```gherkin
Scenario: Exercise maintained AP214 and AP242 fixtures
  Given the existing representative Part 21 package fixtures
  When they are read, validated, projected, and written with the migrated packages
  Then their existing schema-appropriate observable results remain valid against the approved schema contracts
```

### AC-05 — Acquisition and missing-cache behavior are explicit

<!-- acceptance-case: AC-05 -->

```gherkin
Scenario: Prepare or build without a valid local cache
  Given an authoritative AP schema input is absent or its bytes do not match the pinned hash
  When the explicit fetch verifier or a maintained schema package build runs
  Then the fetch verifier rejects unverified bytes before cache promotion, and the build fails with the exact preparation command without performing an implicit network request
```

## Delivery brief

<!-- section: delivery-brief -->

This is one atomic delivery because authoritative bytes, fetch/cache policy, compiler support, generated public identities, package metadata, and proofs must agree on the same candidate. Likely touchpoints are `schemas/`, the fetch/build guard, the three maintained schema projects and READMEs, EXPRESS compiler/generator implementation, focused schema contract tests, package integration tests, and public API baselines. Private parser/binder/emitter choices remain open, but schema-name or file-name exceptions are prohibited.

Recovery is a Git revert plus the previous package version; there is no persisted-data migration. The breaking surface must be called out in release notes before publication.

## Publication handoff

- Owner: repository maintainer.
- Current state: blocked; this delivery does not publish generated source, assemblies, NuGet packages, or source archives, and does not assert that local code generation is licensed.
- Required before unblocking: an independently recorded rights review must explicitly authorize the intended generated or compiled artifact and destination. Source provenance or a successful build is not sufficient evidence.
- Delivery proof: the scoped diff introduces no publish invocation or release enablement, packed test artifacts contain no third-party AP schema EXP, and all generated/package outputs remain local verification artifacts.

## Proof plan

<!-- section: proof-plan -->
<!-- primary-proof: AC-01 purpose=structural shape=contract -->
<!-- primary-proof: AC-02 purpose=acceptance shape=component -->
<!-- primary-proof: AC-03 purpose=acceptance shape=contract -->
<!-- primary-proof: AC-04 purpose=regression shape=integration -->
<!-- primary-proof: AC-05 purpose=boundary shape=integration -->

| Contract | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-01 | Primary | Local inputs equal recorded SHA-256 values; third-party AP schema EXP files are ignored, untracked, and absent from packages; provenance contains authoritative links and no STEPcode source/license attribution remains | Run the fetch verifier, inspect `git check-ignore`, `git ls-files`, and packed contents, and search maintained schema/package files for `STEPcode` |
| AC-02 | Primary | AP203 and AP214 compile from verified official bytes; AP242 compiles from the separately verified compatibility input; near-miss invalid compiler forms retain their expected diagnostics | Run focused AP203, AP214, and AP242 compiler/generator tests, including positive official-expression and neighboring-invalid regression cases, then build the three package projects |
| AC-03 | Primary | AP203 package version `2.0.0`, manifest, descriptor, namespace, and public API evidence match the official MIM-LF schema; a representative MIM-LF fixture completes typed read/validate/write/reread and the old `CONFIG_CONTROL_DESIGN` fixture fails with the unsupported-schema boundary | Run AP203 schema contract and packed-consumer/integration tests; inspect package metadata and the regenerated public API baseline |
| AC-04 | Primary | Existing AP214/AP242 representative package fixtures retain their asserted round-trip and validation results | Run the focused AP214 and AP242 package integration tests |
| AC-05 | Primary | Verified downloads are promoted atomically; a copied-and-mutated cache fails verification; an absent-cache package build fails with the documented preparation command and performs no download | Run the fetch procedure into an empty temporary cache, verify all hashes, run verify-only against a deliberately mismatched temporary copy, and invoke the build guard with a nonexistent cache path while network access is not requested |
| Conditional | Structural | No unrelated working-tree edits are included and repository projects remain buildable | Inspect the scoped diff, run `git diff --check`, then run the repository build if focused gates pass |

## Completion criteria

<!-- section: completion-criteria -->

- AC-01 through AC-05 have candidate-bound passing evidence.
- No tracked third-party AP schema EXP, patched duplicate, or temporary download archive remains; only the ignored verified cache contains those schema bytes.
- The scoped diff contains only this migration and does not overwrite unrelated work.
- Durable provenance and ADR documentation are current.
- EXP distribution is prohibited; redistribution of generated source or compiled packages remains explicitly unapproved until separately confirmed.
- No publishing or release configuration is enabled; the publication handoff remains blocked with a named owner and explicit unblocking evidence.
