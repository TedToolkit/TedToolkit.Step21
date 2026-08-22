# SBRT-007: Complete EXPRESS syntax IR

## 📌 Status

Completed

## 🚦 Delivery priority

- Priority: P2
- Rationale: Generation and validation require a complete source-located ISO 10303-11 representation.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-001 supplies deterministic internal EXPRESS visitor artifacts.
- Recommended order: Before schema binding and every generator item.
- Governing records: AP-003, EP-001/003, and the active architecture pinned by the parent change.

## 🧩 Explicit governing constraints

Recognize complete ISO 10303-11:2004 syntax, preserve source locations and unsupported executable declarations in immutable IR, and use the generated visitor rather than grammar actions or reflection.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| EXPRESS grammar where evidence requires, visitor, immutable IR, syntax diagnostics, and fixtures | Every valid declaration/expression form is represented with deterministic source spans | Name/type binding, executable semantics, generated public schema code, or a public procedure API |

## 🔍 Current behavior and impact boundary

The current repository proves only a minimal schema and a missing terminator. No semantic IR or complete declaration/expression coverage exists.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-001 proves reproducible internal visitors | SBRT-001 completion evidence | Visitor/IR work cannot safely update derived parser artifacts |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-02 | An EXPRESS syntax or name-binding error | Build the consumer project | A source-located diagnostic identifies the schema error and affected invalid output is not emitted |
| BC-02B | A valid EXPRESS schema contains executable declarations that cannot be reached from validation or Part 21 writing | Build the consumer project | The declarations remain represented in source-located IR, no unrelated public execution API is generated, and their presence does not create a false validation gap |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Syntax coverage | All standard declaration/expression forms survive visitor transformation with source spans | Unsupported execution is preserved, never silently discarded or falsely accepted as executed |
| Boundary | IR remains Analyzer-internal and immutable | Runtime/public generated code gains no general EXPRESS interpreter API |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-02 | Grammar/visitor diagnostics | Invalid syntax reports exact source location and produces no valid IR for the affected schema | Fast TUnit project |
| BC-02B | IR contract fixtures | Unreachable functions/procedures/expressions are present with spans and no public execution surface appears | Fast TUnit project plus public API inspection |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Complete source IR | Focused fixtures cover every declaration and expression family with stable spans |
| Honest staging | Unsupported execution remains explicit in IR and absent from public APIs |

## ⏱️ Workload estimate

- Planning range: 0.7–1.3 person-months.
- Confidence: Low.
- Assumptions and excluded work: Existing EXPRESS grammar is a usable foundation; semantic binding is SBRT-008.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Started from `774aaa0`. Replaced the former IFC-oriented grammar deviations with the ISO 10303-11:2004 Edition 2 production boundary, regenerated only the pinned internal ANTLR artifacts, and added only Analyzer-internal syntax IR/parser/visitor/diagnostics plus focused fixtures and conformance documentation. No name/type binding, execution, generated schema API, or public procedure surface was introduced. |
| Behavior-case proof | BC-02 passed invalid-type and trailing-input cases: deterministic `EXPRESS-SYNTAX` evidence retains the logical path and 1-based line/column, and `Root` is null whenever lexer/parser diagnostics exist. BC-02B passed declaration, type, expression, statement, Edition 2, lexical, immutability, span, visitor, and public-surface assertions in the fast TUnit suite (91/91). Functions, procedures, rules, locals, and all statement families remain ordered syntax IR and are never executed. |
| Migration and documentation | No public migration applies because every new type is Analyzer-internal. `docs/conformance/express-edition2-grammar.md` records the standard baseline, production families, fixtures, half-open source spans, and the deliberate syntax/name-binding boundary. A machine comparison audited 219 BNF productions with zero missing non-lexical, non-binding-dependent productions; the six syntactically indistinguishable semantic aliases are losslessly retained as `namedApplication`/`namedReference` for SBRT-008. |
| Dependent-item unlock | `syntax` now accepts one or more complete schemas through EOF and the generated visitor transforms every rule/terminal into immutable ordered IR with original token spelling and deterministic spans. This supplies SBRT-008 and SBRT-016 with complete source evidence while withholding invalid partial trees. |
| Regression and deployment proof | Clean pinned regeneration matched every checked-in ANTLR artifact byte-for-byte. Release solution build passed with 0 warnings/errors; fast TUnit passed 91/91; integration TUnit passed 2/2 enabled tests with the explicit opt-in network corpus test skipped; runtime AOT/trim analysis passed with 0 warnings/errors; runtime dependency inspection still shows only `Antlr4.Runtime.Standard` 4.13.1; the work-item validator reported `Work-item delivery boundary: valid`. Strict read-only review found no blocking or advisory findings. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Grammar gaps emerge during IR coverage | Estimate growth | Preserve full scope; seek work-plan reapproval for material estimate change |
