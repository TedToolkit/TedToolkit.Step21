# ADR-0014: Permit audited compatibility transforms for authoritative schemas

- Status: Accepted
- Date: 2026-09-10
- Decision owner: repository maintainer
- Decision scope: provenance and generation-input policy for maintained EXPRESS schema packages
- Applicable product intent: [`../product/README.md`](../product/README.md)
- Applicable principles: [`AP-001`, `AP-002`, and `AP-003`](../principles/architecture.md)
- Supersedes: ADR-0013
- Superseded by: None
- Approval source: the repository maintainer explicitly approved repairing the two documented AP242 conflicts and explaining the policy in related files on 2026-09-10

## 📌 Decision at a glance

An authoritative schema remains immutable and hash-pinned, while a separately hash-pinned,
Git-ignored generation input may be produced by an explicit fail-closed compatibility transform.

## 🧭 Context and decision question

The official ISO/TS 10303-442 edition 7 AP242 MIM long form has two internally inconsistent typed
expressions. Direct generation therefore fails, while treating either conflict as general EXPRESS
semantics would weaken the schema-neutral compiler. The decision is whether builds remain blocked
or may use a transparent repository-authored correction without obscuring official source identity.

## 🎯 Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Official bytes and authority identity remain independently verifiable | Publisher URL and pinned SHA-256 | Must |
| Hard constraint | A publisher update cannot receive an old correction silently | Exact input hash, unique anchors, and exact output hash | Must |
| Hard constraint | AP-specific corrections do not become general compiler semantics | AP-002 and AP-003 | Must |
| Hard constraint | Third-party and transformed EXP bytes remain untracked and unpacked | Repository distribution policy | Must |
| Driver | Maintained AP packages must be reproducibly buildable | Full AP242 compilation evidence | High |

## 🔎 Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Keep pristine input as the direct build input | Documented: AP242 compilation fails at two contradictory expressions | No | Preserves byte identity but leaves the maintained package unbuildable | Rejected |
| Generalize the compiler to accept the conflicts | Documented: one query returns relationships where datums are declared, and one function recursively passes an operand to a solid parameter | No | Would accept invalid neighboring schemas or require AP-specific compiler behavior | Rejected |
| Apply an audited compatibility transform after source verification | Measured: the corrected generation input compiles for both package target frameworks | Yes | Maintains a repository-authored schema variant that must be reviewed on source updates | Selected |

## ✅ Decision

The official publication is stored unchanged and verified against its recorded hash. A maintained
schema may use a separate compatibility-transformed generation input only when the transform is
explicit, requires the exact official hash, validates unique replacement anchors, records an exact
output hash, and fails closed on any mismatch. Both inputs remain local-only and excluded from Git
and packages. This exception does not authorize inference-based rewriting or AP-specific behavior
inside the general compiler.

## 💡 Why this decision now

The package cannot be built from the official AP242 bytes because the two conflicts express no
type-correct general EXPRESS behavior. The selected boundary preserves AP-001 provenance and AP-002
schema neutrality while making the AP package reproducible. It is reconsidered if ISO publishes a
corrected file or if the generated semantic/public contract changes.

## 🔗 Evidence and links

- [ISO/TS 10303-442 edition 7 MIM long form](https://standards.iso.org/iso/ts/10303/-442/ed-7/tech/express/mim_lf.exp)
- [ISO 10303-242:2025](https://www.iso.org/standard/84300.html)
- [Superseded source policy](ADR-0013-authoritative-express-schema-sources.md)

## ⚖️ Consequences and accepted trade-offs

- Source authority and generation semantics are now two separately identified artifacts.
- Every transform requires long-term maintenance, explicit semantic explanation, and two hashes.
- The general EXPRESS compiler remains schema-neutral and continues rejecting incompatible types.
- Publication of generated or compiled output still requires the separate rights review.

## 🛠️ Downstream delivery constraints

- Transforms must be deterministic, local-only, versioned, auditable, and fail closed.
- Official source identity must remain visible in package provenance and compatibility records.
- A transform may correct only documented contradictions; it must not remove declarations or relax
  unrelated validation.
- CI must verify both hashes before generation.

## 🔄 Exit requirements

Replace the transform only with an equal-or-more-authoritative corrected source or a new explicitly
approved compatibility decision. Preserve the previous identity and compatibility record.

## 📅 Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Reassess AP242 transform | repository maintainer | ISO publishes a corrected/superseding MIM long form | Open |
| Reassess redistribution rights | repository maintainer | Before publishing generated or compiled AP packages | Open |
