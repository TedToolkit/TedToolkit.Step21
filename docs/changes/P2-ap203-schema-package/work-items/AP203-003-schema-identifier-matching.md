# AP203-003: Match standard schema identifiers without changing retained header text

<!-- work-item-format: 2 -->

- Approval: 用户于 2026-08-24 在本任务中明确要求开始执行；批准的完整工作项映射内容 SHA-256 为 `FFEEE360860BF3B5A891B61B3D91C9D047667C300712C7DCB46BDF8BE3006249`。
- Revision approval: 用户于 2026-08-24 明确选择方案 A，要求归一化 header 重名以 binding diagnostic 拒绝。

## Outcome

Descriptor selection recognizes the standard uppercase nominal name, an explicit case-insensitive
interoperability tolerance, and the approved canonical numeric-arc OID subset while the public header
and written output retain the exact supplied identifier; close names, other protocols, malformed or
unsupported suffixes, and normalized header collisions fail atomically.

Normative claim boundary: ISO 10303-21:2016 Edition 3 clause 8.2.4 defines a schema name followed by
an optional ISO/IEC 8824-1 object identifier and requires uppercase physical schema-name strings.
This item supports only the canonical, space-delimited numeric-arc subset illustrated by that clause's
note/example, checked against ISO/IEC 8824-1:2015 / ITU-T X.680 (08/2015) clause 32 and the root-arc
constraints of ISO/IEC 9834-1 / ITU-T X.660. Named-arc and other otherwise legal ASN.1 forms are
explicitly unsupported. Accepting lowercase or mixed-case header strings is an interoperability
tolerance, not an ISO 10303-21 syntax-conformance claim. OID text is syntax-checked and retained;
it is not validated as the identity of the packaged AP203 baseline.

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area: schema-neutral descriptor selection during Part 21 binding.
- In scope: nominal-identifier extraction/comparison, duplicate/mismatch handling at that boundary,
  retained-header behavior, and focused positive/negative contract tests.
- Non-goals: changing `SchemaName` value equality globally, rewriting `FILE_SCHEMA`, aliases outside
  the declared tolerance, full ASN.1 OID notation, OID-to-schema-baseline identity validation,
  automatic descriptor discovery, or AP203-specific matching code.
- Likely touchpoints (non-binding): exchange-structure read/binding code and focused in-memory tests.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| None | The existing descriptor-injection and atomic-read boundaries are available | Approved parent architecture and current runtime tests |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Verified input or primary-proof intent |
| --- | --- | --- |
| AC-03 | Owns | Case variants bind identically while retained spelling survives writeback |
| AC-04 | Owns | The supported canonical numeric-arc OID suffix binds by nominal name and remains intact; other legal but unsupported ASN.1 forms fail with `P21-BIND-SCHEMA` |
| AC-08 | Owns | Other protocols, unknown names, malformed lookalikes, and normalized header collisions produce atomic `P21-BIND-SCHEMA` failure |
| AC-05 | Supports | Supplies identifier behavior needed by the real AP203 fixture |

<!-- work-item: delivery-constraints -->
## Constraints

- Normalize only descriptor selection; retain `FileSchema.SchemaIdentifiers` and writer output text.
- Keep existing exact identifiers valid and preserve explicit descriptor injection.
- Use clause 8.2.4 plus X.680:2015 clause 32/X.660 constraints only for the declared canonical numeric-arc subset, not prefix matching, brace trimming, or AP203 aliases; reject other OID forms explicitly and keep case-insensitive header acceptance separately identified as an interoperability tolerance.
- Reject two different raw `FILE_SCHEMA` identifiers that normalize to one nominal name; never choose first or silently fold them.
- Add no public registry, facade, or new public normalization API unless the change is redesigned.
- Private helper placement is open; avoid a new class or function unless it removes real duplication or owns an invariant.

<!-- work-item: proof-plan -->
## Proof

| Contract or gate | Evidence purpose | Execution shape | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- | --- |
| AC-03, AC-04 | Acceptance and regression | Unit/contract | Clause 8.2.4 uppercase name and supported canonical numeric-arc OID variants select one descriptor; named-arc/unsupported forms fail; case variants exercise the separately documented compatibility tolerance; accepted inputs retain the original identifier without claiming OID-to-baseline validation | `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release` |
| AC-08 | Acceptance and security-like boundary | Unit/contract | Cross-protocol, unknown, malformed, and normalized-collision inputs yield stable `P21-BIND-SCHEMA` evidence and no model | `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release` |

<!-- work-item: definition-of-done -->
## Done

- AC-03, AC-04, and AC-08 have deterministic positive and adversarial proof.
- Existing exact-match, multi-schema, and canonical-writing regressions remain green.
- The implementation changes no public value equality or retained header data.

<!-- work-item: completion-evidence -->
## Completion evidence requirements

Record the candidate revision, actual changed artifacts, contract IDs, test inputs representing each
accepted/rejected family, commands, observable assertions, test counts, and the matching guarantee
supplied to AP203-004.

Completion evidence at candidate `8ac78b00486890660608d673523ab9d6d57b4f05`:

- Commits `193cf87`, `300243b`, and `8ac78b0` change the schema-neutral exchange-structure binding,
  focused read tests, and the generated/same-schema/multi-schema conformance contracts.
- AC-03 and AC-04 accept the standard uppercase identifier, the separately documented lower/mixed-case
  interoperability tolerance, and canonical numeric-arc OID suffixes while retaining exact input and
  writeback text. AC-08 rejects other protocols, close names, malformed suffixes, normalized collisions,
  and the legal-but-unsupported ASN.1 named-arc form with atomic `P21-BIND-SCHEMA` evidence.
- `dotnet build TedToolkit.Step21.slnx --configuration Release --no-incremental` completed with zero
  warnings and errors. `dotnet run --project tests/TedToolkit.Step21.Tests --configuration Release
  --no-build` passed 269 of 269 tests with no failures or skips.
- Independent implementation review of `8970a5b..8ac78b0` concluded `Ready` with no blocking findings or
  design deviations. AP203-004 may rely on OID-qualified AP203 headers selecting the explicitly supplied
  nominal descriptor without changing retained header text; the OID is not a package-baseline identity.

## Risks and implementation notes

Over-broad normalization can silently bind the wrong application protocol. Treat every unproven
lookalike as invalid; if the standard suffix form cannot be determined from authoritative evidence,
stop rather than inventing a grammar.
