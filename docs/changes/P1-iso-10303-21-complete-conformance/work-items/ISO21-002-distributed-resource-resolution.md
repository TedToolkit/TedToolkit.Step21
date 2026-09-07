# ISO21-002: 交付 distributed resource resolution

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-002 -->

<!-- approval-source: user-explicit-approve-and-continue-2026-09-06 -->

## Outcome

在调用方显式提供资源与配额的边界内，完整解析 Part 21 local/external references、
directory content 与 compressed archive content，并将外部 entity/value 纳入相同的 schema、identity 和 validation 规则。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: per-read resource/base-URI/limit capabilities、resource identity/cache lifetime、clause-10 resolution、Annex A.4/A.5 archive/directory transport 和 API/diagnostic baselines。
- In scope: relative/absolute URI、fragment/UUID/entity/value target、other-format conversion hook、nested resources、missing target、clause-10.2 null cycle、ZIP root `ISO-10303.p21`、subsidiary content and no-disk extraction.
- Non-goals: implicit HTTP/filesystem access、machine cache、CMS trust、Part 22 repository、streaming/lazy entity proxies or AP-specific resolvers.
- Likely touchpoints (non-binding): read options/capability contracts、resource graph state machine、archive reader、hydration、in-memory integration fixtures.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| ISO21-001 Verified | Stable anchor/occurrence identities and URI/value mappings | ISO21-001 completion evidence |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-04 | Owns | External/directory/ZIP resolution, identity, null-cycle and schema behavior |
| AC-02, AC-06, AC-09 | Supports | Supplies class-2 transport, external populations and resource-security boundary |

<!-- work-item: delivery-constraints -->
## Constraints

- Follow ADR-0009: no implicit I/O or resolver-supplied bound entities; runtime owns URI/archive/anchor/schema semantics.
- Standard reference cycles resolve to null; archive recursion, provider re-entry and quota exhaustion are distinct atomic failures.
- Preserve model publication atomicity and do not create persistent proxies or reverse the runtime-to-generated dependency direction.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-04 purpose=boundary shape=integration -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-04 | Primary | In-memory local/external/directory/ZIP matrices resolve exact entity/value identity, null and schema outcomes without implicit I/O | Run the focused `DistributedResourceResolutionTests` Release integration suite |
| Resource/API compatibility | Conditional | Existing inputs needing no resource capability keep identical class-1 behavior and additive API classification | Run core Release tests and public API snapshot checks |

<!-- work-item: definition-of-done -->
## Done

- AC-04 passes for positive, missing, cycle, type/schema, directory and archive partitions.
- The verified resource graph and external-structure input are supplied to ISO21-004 and ISO21-007.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate revision, APIs, fixtures, resource identities, archive forms, limit settings, result/diagnostic counts,
exact proof commands and the supplied external-resolution contract.

## Completion evidence

- Verified on 2026-09-07 at candidate `012ac0c8505019a74868f57b159f827efc6bcdda`; the independent final
  implementation review concluded **Ready** with no blocking or important findings and no design deviations.
- Public contract: the options overload of `ExchangeStructure.Read` accepts immutable per-read base URI, explicit
  `IPart21ResourceProvider` / `IPart21ResourceConverter` capabilities, and `Part21ResourceLimits`; resource content
  distinguishes clear text, ZIP, directory, and caller-converted representations; references expose unresolved,
  resolved, or ISO null status without granting implicit I/O.
- Identity and fixture partitions: local/external URI, UUID registry, numeric/entity/value fragments, forwarding,
  missing targets, cycles, canonical provider/converter aliases, directory and ZIP subsidiaries, nested archives,
  invalid external structures, schema/type mismatches, and converted subsidiaries were exercised. Per-read caches
  share canonical CLR models, isolate container and converted identities with non-addressable internal keys, preserve
  receiving-schema compatibility, and roll back aliases atomically after failed nested loads.
- Archive partitions: in-memory directories and PKZip 2.04g archives require exact root `ISO-10303.p21`; tests cover
  path escape/root aliases, stored/deflated entries, CRC/size/offset agreement, signed and unsigned data descriptors,
  overlapping ranges, comments containing a structurally shaped false EOCD, and rejection of encryption, Unicode
  filenames, ZIP64, Deflate64/other methods, malformed roots, and excessive nesting.
- Limits: defaults and explicit boundary fixtures cover 64 resources, reference depth 16, archive depth 8,
  256 MiB total supplied bytes, 4096 entries, 256 MiB uncompressed archive/directory bytes, and per-ZIP-entry
  compression ratio 100; quota, callback re-entry, and archive-integrity failures remain atomic exceptions.
- Exact focused proof: `dotnet run --project tests/TedToolkit.Step21.Tests/TedToolkit.Step21.Tests.csproj -c Release
  --no-build -- --treenode-filter "/*/*DistributedResourceResolutionTests*/*/*" --maximum-parallel-tests 1` passed
  26/26. Legacy `ReferenceReadTests` passed 3/3, the ISO21-002 public API snapshot passed 1/1, documentation contracts
  passed 2/2, Release builds completed with 0 warnings/errors, and the acceptance-specification validator passed.
- Exact broad proof: `dotnet run --project tests/TedToolkit.Step21.Tests/TedToolkit.Step21.Tests.csproj -c Release
  --no-build -- --maximum-parallel-tests 2` completed 592 tests in 15m29s: 590 passed and only the two baseline-proven,
  unchanged QUERY-emitter failures remained at `ExpressExpressionEmitter.cs:1327`; there were no new failures.
- Supplied contract: ISO21-004 may consume stable schema-bound external structures and shared resource identity;
  ISO21-007 may compose the explicit provider/converter/archive boundary with its integrated adversarial matrix.

## Risks and implementation notes

ZIP parsing is an untrusted-input boundary; this item proves standard behavior while ISO21-007 owns the integrated adversarial quota matrix.
