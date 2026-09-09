# ISO21-009: 交付验证可达的 Part 11 语义闭包

<!-- work-item-format: 2 -->
<!-- work-item-id: ISO21-009 -->

<!-- approval-source: user-approved-authorized-iso-source-derived-profile-revision-2026-09-09 -->

## Outcome

依据用户提供且有权授权 AI 使用的 ISO 10303-11:2004 文档或相关条款摘录，生成不复制标准原文的
条款—语义—测试追踪表，并使 Analyzer 对 Part 21 映射和 schema conformance 可达的 types、inheritance、
redeclarations、constants、functions、procedures、rules 与 algorithm statements 完整绑定、静态生成和执行，
不跳过授权 ISO 文件中已描述的任何可达语义家族；文件未描述的行为明确记录为 source-excluded，
不推断、不实现且不阻塞本工作项。独立审查负责验证追踪表覆盖和实现/测试充分性。

<!-- work-item: scope -->
## Scope and non-goals

- Target delivery area or exact public/persisted contract: validation-reachable Part 11 bound IR、dependency closure、static emitter、constant evaluation、complete complex mapping and source-located unsupported diagnostics.
- In scope: all simple/aggregate/defined/enumeration/select types、inheritance/evaluated sets、attribute redeclarations、constants、functions/procedures/rules、validation-reachable statements/built-ins including QUERY, and internal/external complex mapping decisions required by Part 21.
- Non-goals: copying ISO prose into the repository、general-purpose EXPRESS invocation/interpreter、unreachable program execution、EXPRESS-X、EXPRESS-G、AP/B-rep/PMI semantics or runtime schema reflection.
- Likely touchpoints (non-binding): compiler bound IR and closure、expression/statement emitters、schema descriptor generation、constant hydration、semantic-family manifest、focused corpus and generated API baselines.

<!-- work-item: start-conditions -->
## Start conditions

| Prerequisite or blocker | Concrete input or guarantee | Evidence |
| --- | --- | --- |
| ISO21-001 Verified | Stable physical occurrence/constant-name contract and generated lookup seam | ISO21-001 completion evidence |
| ISO21-005 Verified | Stable schema-supplied short-name and Part 21 physical-mapping metadata | ISO21-005 completion evidence |
| Authorized ISO source | User-authorized ISO 10303-11:2004 document or relevant excerpts; only behavior described by that file enters the implementation contract | Source identity/digest plus explicit AI-use authorization |

<!-- work-item: contract-coverage -->
## Contract responsibility

| Parent contract | Responsibility | Contribution or supplied input |
| --- | --- | --- |
| AC-07 | Owns | Complete clause-12 and validation-reachable Part 11 mapping/execution closure |
| AC-01, AC-02 | Supports | Supplies constant, class-3 and schema-conformance proof to final closure |

<!-- work-item: delivery-constraints -->
## Constraints

- Derive a non-verbatim profile that records, for every validation-reachable semantic family, its clause ID, valid result, UNKNOWN/error/boundary behavior, result type/category/bounds and neighboring-invalid examples; do not commit copyrighted standard prose or the supplied licensed source unless the user separately authorizes redistribution.
- Every semantic family described by the authorized file has a clause-bound positive and neighboring-invalid corpus partition; reachable unsupported behavior rejects generation rather than being skipped. A behavior absent from that file is recorded as source-excluded and omitted without inference.
- Preserve EXPRESS three-valued logic, result types, aggregate category/bounds/order/uniqueness, evaluation order, scoping and source-located diagnostics exactly as established from the authorized source.
- Keep generated code direct, shared, deterministic and AOT-ready; no runtime interpreter, reflection discovery, AP branch or generated dependency on compiler packages.
- Full AP schemas supplement but never replace focused semantic-family fixtures; no AP/B-rep/PMI behavior may enter the implementation contract.
- A fresh independent review must verify the derived profile's source coverage, clause traceability, implementation correctness and test adequacy before ISO21-009 may be integrated.

<!-- work-item: proof-plan -->
## Proof

<!-- primary-proof: AC-07 purpose=acceptance shape=contract -->
| Contract or gate | Role | Observable assertion | Command or bounded procedure |
| --- | --- | --- | --- |
| AC-07 | Primary | Independently reviewed semantic-family manifest and corpus cover every validation-reachable row derived from the authorized source; valid schemas generate and execute exact results while neighboring-invalid/reachable-unsupported cases fail deterministically | Run the focused compiler conformance corpus and generated-mapping contract suite in Release |
| Generator/AP compatibility | Conditional | Existing compiler baseline, public generated APIs and AP203/AP214/AP242 full baselines remain green without adding AP-specific logic | Run all `Express*`, AP schema baseline, package and reproducibility tests in Release |
| Native AOT | Conditional | Generated semantic execution remains reflection-free and the packed consumer publishes and runs under Native AOT | Run the packed consumer and `build/verify-native-aot.ps1` |

<!-- work-item: definition-of-done -->
## Done

- AC-07 passes with a machine-readable semantic-family manifest, independent source-coverage review and zero source-described row skipped or unsupported; source-excluded rows are not conformance claims.
- Generated/public compatibility, all maintained schema packages and Native AOT pass; the verified manifest is supplied to ISO21-008.

<!-- work-item: completion-evidence -->
## Verification result requirements

Record candidate revision; authorized-source identity/digest and user authorization without redistributing licensed text;
derived-profile identity/digest; independent coverage-review conclusion; semantic-family counts; valid/invalid corpus
counts; generated/API deltas; diagnostics; commands/results; AP baseline/reproducibility results; Native AOT identity;
and the manifest supplied to ISO21-008.

## Risks and implementation notes

This remains the largest compiler boundary. Missing or ambiguous authorized-source material excludes only the affected
behavior from this delivery; it must not be guessed or represented as an ISO conformance claim. The existing
implementation inventory is evidence about current behavior, not a substitute for the authorized normative source.

## Partial delivery evidence — 2026-09-09

- `docs/conformance/iso-10303-11-validation-semantics.json` is the machine-readable, non-verbatim semantic-family
  profile. It uses `implemented`, `partial` and `pending-source` rather than a skipped state, records a concrete blocker
  for every incomplete row, and keeps the overall claim blocked. `ConformanceManifestTests` mechanically verifies
  unique rows, allowed states, proof file/member existence and blocker presence.
- Source search found the 255-page ISO 10303-11:2004 catalogue record and two lawful 13-page previews. The previews
  contain the contents, foreword and introduction, but not the normative text needed to derive constant evaluation or
  the remaining validation-reachable semantic families. Those families are therefore left pending: they are not
  implemented from inference, waived, or counted as conforming.
- Public EXPRESS Language Foundation material identifies ISO 10303-11:2004 Edition 2, shows the four aggregate kinds,
  and explicitly presents the `QUERY(variable <* source | predicate)` construct. This bounded source evidence permits
  repair of the already-isolated direct aggregate-source defect, but does not substitute for the complete semantic
  profile required by this work item's Done criteria.
- Candidate `cc777b5` changes the private emitter so absence of the optional schema-aware defined-type resolver is not
  misclassified as a non-aggregate carrier. Projection is still required when a present resolver positively identifies
  a non-aggregate carrier; generated code remains direct, shared and AOT-ready.
- Proof on the candidate: the two `ISO21WorkItem=ISO21-009` tests changed from 2/2 failed to 2/2 passed; the full Release
  unit suite passed 710/710; the Release integration suite passed 35/36 with only the opt-in external-download test
  skipped; `build/verify-native-aot.ps1` reported `PACKED_AOT_OK` and `NATIVE_AOT_PACKAGE_PROOF_OK` for `win-x64`.
- A public JIS reproduction states that JIS B 3700-11-1996 translated ISO 10303-11:1994 without changing its technical
  content or standard form. Its clauses 13.1 and 13.2 supply bounded semantics for null and ALIAS statements, and its
  Annex A supplies their grammar. The current candidate therefore adds no-op null execution, scoped direct/entity/
  attribute ALIAS lowering, inferred alias types, and deterministic rejection for constant sources, leaked names and
  not-yet-implemented indexed aliases. This is partial Edition-1-derived evidence only; Edition-2 additions and the
  remaining validation-reachable families stay pending.
- A separately located ISO 10303-11:2004 excerpt covers clauses 13.1 through 13.9.3. Clause 13.4 directly establishes
  first-TRUE CASE selection, FALSE/UNKNOWN/non-value non-selection, `OTHERWISE` for an indeterminate selector, and
  no action when no label matches and `OTHERWISE` is absent. The CASE candidate adds TRUE-only guarded comparison,
  explicit indeterminate-label handling, once-only selector evaluation and an indeterminate path through otherwise
  statically exhaustive closed-enumeration cases. No semantics outside the excerpt are inferred from it.
- CASE proof changed the new focused test from one generator exception to pass, then covered the optional closed-
  enumeration fall-through neighbor. The candidate passes 4/4 `ISO21WorkItem=ISO21-009` tests and all 133
  `ReachableRuleTests`; the preceding ALIAS/null candidate also passed the full 711-test Release suite and the complete
  Release solution build with zero warnings or errors.
- The same ISO 10303-11:2004 excerpt explicitly requires that a REPEAT body shall not modify its implicitly declared
  NUMBER control variable (13.9.1(f)). The binder now rejects direct assignment, mutation through a procedure `VAR`
  parameter, and mutation through an ALIAS of the control variable before source generation, using the stable
  `EXPRESS-BIND-REPEAT-VARIABLE-MUTATION` reason. ALIAS origins are stored only for declared aliases and traversed
  without per-check collections. The focused red test first demonstrated that all direct and `VAR` cases were
  previously accepted; the corrected candidate passes 5/5 `ISO21WorkItem=ISO21-009` tests and all 134
  `ReachableRuleTests`. The complete Release unit suite passes 713/713, including the checked-in AP203, AP214 and
  AP242 binding and generated-surface baselines.
- Clause 13.8 requires actual procedure parameters to agree with the declared formal count, order and assignment-
  compatible types. The binder now rejects missing and excess actual parameters using
  `EXPRESS-BIND-PROCEDURE-ARGUMENT-COUNT`, instead of allowing the reachable-plan fallback to classify the entire
  algorithm as unsupported. The focused red test captured both prior fallbacks; the corrected candidate passes 6/6
  `ISO21WorkItem=ISO21-009` tests and all 135 `ReachableRuleTests`. Type compatibility remains pending because the
  located 2004 excerpts expose the 12.11 heading but not its normative body; the older JIS translation is used only as
  supporting evidence and is not assumed to cover Edition-2 SELECT changes.
- Clauses 13.6 and 13.9 allow a REPEAT with no finite, WHILE or UNTIL control and define ESCAPE as the immediate
  transfer to the statement following its enclosing REPEAT. The reachable-plan gate no longer rejects an empty
  `repeat_control`; the renamed non-increment emitter reuses the existing static loop-transfer path and emits no
  interpreter or runtime wrapper. The focused red test first captured the prior STEP21EXP006 rejection, then proves
  execution resumes after the loop while the statement after ESCAPE inside the loop is unreachable. A neighboring
  ESCAPE outside REPEAT remains rejected. The candidate passes 7/7 `ISO21WorkItem=ISO21-009` tests and all 136
  `ReachableRuleTests`.
- Clause 13.7 routes only TRUE through an IF `THEN`; FALSE, UNKNOWN and indeterminate route through ELSE or fall
  through when ELSE is absent. Clause 13.9.2 likewise requires a logical WHILE value and re-evaluates it before each
  iteration, terminating for every non-TRUE result. Explicit indeterminate controls are now lowered directly to a
  false generated predicate rather than being sent to the context-dependent expression type emitter, which previously
  crashed generation. Reachable IF/WHILE/UNTIL controls whose static type is neither BOOLEAN nor LOGICAL now receive
  a source-located STEP21EXP006 failure before emission. The focused corpus also proves the clause-13.5 compound body
  retains its enclosing scope. The candidate passes 8/8 `ISO21WorkItem=ISO21-009` tests and all 137
  `ReachableRuleTests`.
- Clause 13.9.1 defines finite REPEAT controls as numeric values captured on entry, with a default increment of one;
  an indeterminate initial, final or increment value executes no body, and positive, negative and zero increments
  determine the applicable direction or zero iterations. The emitter now removes a finite loop whose literal control
  is indeterminate instead of passing it to the context-dependent type emitter, while all determinate controls remain
  captured once in the generated `for` initializer. A pre-emission control-type check rejects non-numeric lower,
  upper and increment expressions with source-located STEP21EXP006 diagnostics. The focused corpus covers default,
  positive, negative and zero increments, mismatched directions, once-only capture, all three indeterminate positions
  and all three non-numeric positions. The candidate passes 9/9 `ISO21WorkItem=ISO21-009` tests and all 138
  `ReachableRuleTests`.
- Clause 13.9.1(g) also establishes a local scope for the implicit NUMBER loop variable: it hides a surrounding name
  and is unavailable after END_REPEAT. A focused valid/invalid pair proves the existing binder and static emitter
  already preserve both boundaries, so no duplicate runtime or generated representation was introduced.
- Clause 13.3.2 requires an element-qualified assignment index to evaluate to an integer. Assignment indexing through
  a NUMBER no longer truncates its REAL branch: generated code evaluates the index once, requires a present INTEGER
  alternative through `TryGetInteger`, and otherwise produces a deterministic execution error. Statically REAL,
  STRING and indeterminate indices now fail before emission with source-located STEP21EXP006 diagnostics. The test
  deliberately isolates this rule from a neighboring INTEGER-to-NUMBER parameter/initializer adaptation gap, which
  remains queued under assignment compatibility rather than being hidden by the test. Together these additions pass
  11/11 `ISO21WorkItem=ISO21-009` tests and all 140 `ReachableRuleTests`.
- Clauses 13.8 and 13.3.2 require procedure actual parameters to be assignment-compatible with their formals. The
  shared procedure-call boundary now applies the explicitly compatible direct-scalar promotions INTEGER-to-NUMBER,
  REAL-to-NUMBER, INTEGER-to-REAL and BOOLEAN-to-LOGICAL without approximation or an interpreter. Direct scalar pairs
  that are statically incompatible, such as STRING-to-INTEGER, fail at the actual argument with STEP21EXP006. The
  checker intentionally defers SELECT, defined-type and aggregate cases until their complete compatibility paths are
  proved; it does not classify them from incomplete evidence. The candidate passes 12/12
  `ISO21WorkItem=ISO21-009` tests and all 141 `ReachableRuleTests`.
- Clause 13.3.2 explicitly permits range-qualified assignment only for STRING and BINARY, uses one-based inclusive
  bounds, and permits the replacement length to change the carrier length. Direct STRING/BINARY locals now share one
  generated reconstruction path that captures the carrier and both exact-integer indices once, checks
  `1 <= lower <= upper <= length`, replaces the inclusive range and writes the complete value back. BINARY preserves
  exact leading bits through `BinaryValue`; no runtime helper or mutable buffer is retained. LIST range assignment is
  rejected at the range source location. Defined-type, SELECT and multi-qualifier range carriers remain explicitly
  unsupported until their complete wrapper/dynamic write-back semantics are proved. The candidate passes 13/13
  `ISO21WorkItem=ISO21-009` tests and all 142 `ReachableRuleTests`.
- The same clause defines single-element STRING/BINARY assignment as replacement of one character or bit. Direct
  scalar element assignment now uses the range reconstruction path with identical one-based bounds checks and an
  additional replacement-length-of-one guard. Carrier, index and replacement are each captured once in the generated
  tuple pattern; the former scalar-to-aggregate cast and generator exception are eliminated without adding runtime
  state. The expanded range fixture remains green together with the source-gated manifest proof.
- Full AP242 generation exposed a C# pattern-variable collision when one NUMBER-indexed entity-attribute assignment
  was expanded across many concrete physical projections. Candidate `4ac96fc` replaces the copied inline conversion
  with schema-private exact-index helpers and records their need in the reachable plan, so schemas without the
  corresponding reachable index form emit no helper. A focused abstract-base/two-projection regression proves the
  shared path. The final Release build has zero warnings and errors; `ISO21WorkItem=ISO21-009` passes 14/14; the
  compiler baseline passes with only AP203's expected descriptor-source hash update; the approved AP242 surface gate
  passes; and the complete Release unit suite passes 722/722 with zero skips. The 189.5 MB diagnostic-only generated
  source directory was removed after inspection.
- Clauses 13.3.1 and 13.3.2 establish aggregate value assignment, while the shared copy boundary previously excluded
  ARRAY and therefore aliased its mutable slot storage. The focused test first compiled but returned FALSE after a
  source-slot mutation also changed the assigned ARRAY. Candidate `12ff113` adds one shared `ExpressArray<T>` value-
  copy constructor and routes ARRAY assignments through it, preserving the declared domain, OPTIONAL/UNIQUE metadata,
  unset slots and an available concrete-source comparer. The runtime-copy test, public API snapshot, source-gated
  manifest and `ISO21WorkItem=ISO21-009` suite (15/15) pass; the compiler baseline changes only AP203's expected
  descriptor-source hash; and the complete Release unit suite passes 724/724 with zero skips. Compatibility between
  unlike aggregate declarations remains pending with clause 12.11 rather than being inferred from this value-copy
  implementation.
- A line-by-line recheck of clause 13.3.2 found that element and range assignment constrain the declared carrier
  type, so candidate `b7ca3cc` had incorrectly treated defined STRING/BINARY wrappers as direct carriers. Corrective
  candidate `052455c` removes that inferred behavior, validates the carrier at every qualified path segment, and
  rejects defined scalar, SET and BAG element carriers plus LIST and defined-scalar range carriers with source-located
  STEP21EXP006 diagnostics. Direct STRING/BINARY element and range replacement still executes successfully;
  `ISO21WorkItem=ISO21-009` passes 15/15 and all 143 `ReachableRuleTests` pass. SELECT carriers are explicitly admitted
  by the source but remain withheld with a deterministic diagnostic until their runtime alternative and copy-back
  semantics are generated.
- Clause 13.3.2 also permits an element or range qualifier when the declared carrier is a SELECT using ARRAY, BINARY,
  LIST or STRING. Candidate `1abdc07` dispatches through the generated SELECT `Match` API, reconstructs immutable
  STRING/BINARY alternatives, updates bounded ARRAY/LIST alternatives, rebuilds the selected factory path, and throws
  only when the runtime alternative is not one of the permitted carrier shapes. A multi-alternative runtime proof
  covers all four permitted shapes; `ISO21WorkItem=ISO21-009` passes 16/16 and all 144 `ReachableRuleTests` pass. The
  compiler-baseline test itself passes unchanged. The wrapper script cannot certify its pinned toolchain on this host
  because it requires SDK 10.0.400 while the available SDK is 10.0.401; candidate `0bd1eb5` separately repairs the
  baseline manifest SHA-256 that had not been synchronized with the earlier AP203 ARRAY hash update. At this candidate,
  SELECT-qualified paths with a qualifier after the selected element were still explicitly withheld and are closed by
  the later candidates recorded below.
- Authorized Edition 2 source files are now identified without redistribution by SHA-256
  `8701F9D80C107AF81DDDE4924FFCC046FE04497E38370B9A3EB312D1329DD800` (front matter) and
  `E167DAA3DB99892104E124B01739355D428B2182F5E6E84E1E8BADEE8465F06F` (pages 121–130). Per the
  user-approved source rule, behavior absent from those files is recorded as `source-excluded`, not inferred and not
  treated as a conformance claim.
- Candidates `4e0a28a`, `cbfe5bc`, `5541ae9` and `50b444f` extend source-described 13.3.2 qualification through nested
  SELECT indices, selected entity attributes, attribute aggregate elements, and direct SELECT attribute/group paths.
  Candidate `3a06872` replaces the previous indexed-ALIAS rejection with static ARRAY/LIST element alias evaluation.
- Candidate `5819a4d` applies one shared assignment-compatibility classifier to direct assignments and procedure
  arguments, adapts defined simple, SELECT and aggregate-initializer values, preserves existing atomic entity/SET
  runtime narrowing, and rejects five neighboring incompatible type families before C# generation. It also proves the
  sourced UNTIL ordering and TRUE/FALSE/UNKNOWN behavior. The Release build has zero warnings and errors;
  `ISO21WorkItem=ISO21-009` passes 18/18 and all 146 `ReachableRuleTests` pass.

Source identities:

- <https://www.iso.org/standard/38047.html>
- <https://webstore.ansi.org/preview-pages/ISO/preview_ISO%2B10303-11-2004.pdf>
- <https://www.ps-ent-2023.de/fileadmin/prod-preview/ISO_10303-11_2004_shortversion.pdf>
- <https://www.expresslang.org/languages/express>
- <https://www.expresslang.org/docs/documents/express-pretty/document.html>
- <https://kikakurui.com/b3/B3700-11-2002-01.html>
- <https://www.elecenghub.com/NewSamples/ISO/174364973/ISO-10303-11-2004-2.pdf>
- <https://www.elecenghub.com/NewSamples/ISO/174364973/ISO-10303-11-2004-1.pdf>
