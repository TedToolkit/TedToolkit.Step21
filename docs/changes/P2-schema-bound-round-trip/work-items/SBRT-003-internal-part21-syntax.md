# SBRT-003: Internal Part 21 syntax graph

## 📌 Status

Implemented

## 🚦 Delivery priority

- Priority: P2
- Rationale: Atomic typed publication needs a complete internal parse/bind handoff that consumers never see.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-002 supplies the complete normative parse tree.
- Recommended order: Before any typed read/bind item.
- Governing records: AP-003, EP-003, architecture AD-12, and ADR-0005 pinned by the parent change.

## 🧩 Explicit governing constraints

`ExchangeStructureSyntax` is immutable internal infrastructure only. It must preserve standard syntax and source locations but never appear in public APIs, generated code, or XML documentation.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Part 21 visitor, internal syntax values, diagnostics, capability-exception integration, focused tests | Every recognized section/parameter form becomes an internal source-located syntax graph; unsupported operations throw `ExchangeStructureCapabilityException` with complete diagnostics after parsing | A public raw model, typed binding, schema validation, or complete advanced operational semantics |

## 🔍 Current behavior and impact boundary

Current tests invoke generated parser rules directly; no semantic visitor or internal complete syntax representation exists. Existing parsers remain internal and valid syntax remains accepted.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-002 proves complete normative section parsing | SBRT-002 completion evidence | A syntax graph built from a partial grammar would encode a false completeness boundary |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-19 | A generated public type or member is proposed without an ISO 10303-21 or selected EXPRESS-schema source | Review generated API and metadata | The proposal is rejected or reclassified and isolated as explicitly named implementation infrastructure |
| BC-24 | A syntactically valid Edition 3 structure uses an advanced section whose operational semantics are not delivered yet | Parse it through the normative start rule and request the unsupported operation | Parsing preserves the standard structure; the later operation returns an explicit unsupported-capability diagnostic rather than a syntax error |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Visibility boundary | Syntax graph and visitor implementation remain internal and absent from generated/public XML | Public consumers receive only later typed models or diagnostics/exceptions |
| Syntax preservation/capability boundary | All recognized standard sections and parameter forms retain deterministic values/source locations; unavailable operations use the exact capability exception/evidence | No schema interpretation, syntax rejection, or operational guessing occurs here |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-19 | API/metadata contract audit | Public API and generated XML contain no syntax graph or invented domain relationship | Build plus public API/XML inspection |
| BC-24 | Visitor/capability integration | Advanced valid syntax creates the preserved internal node; requesting unsupported behavior throws `ExchangeStructureCapabilityException` with applicable complete diagnostics, not a syntax error | Fast TUnit project with one fixture per advanced section family |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Complete internal handoff | Focused visitor tests cover every syntax family, source span, and exact unsupported-capability exception/evidence |
| Encapsulation | Public-surface audit proves no `ExchangeStructureSyntax` exposure |

## ⏱️ Workload estimate

- Planning range: 0.5–1.0 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Immutable syntax storage is private; typed hydration and public read exceptions are later outcomes.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Started from `7a45566`. Added only the internal `Syntax/` graph, parser/visitor/error collector, focused tests, and three advanced-section fixtures; no public raw model, schema binding, validation, or writer behavior was introduced. The existing split `STEPLexer.g4`/`STEPParser.g4` remains necessary because ANTLR lexer modes used for URI and signature tokenization are legal only in lexer grammars. |
| Behavior-case proof | BC-19 passed both exported-type inspection and generated runtime XML inspection: no `TedToolkit.Step21.Syntax` type/member is public or documented in the published XML artifact. BC-24 passed one fixture for each ANCHOR, REFERENCE, and SIGNATURE family plus an aggregate-order case; parsing preserves each section and later capability checking throws exact ordered `P21-CAP-*` diagnostics at source locations. Focused syntax tests passed 13/13. |
| Migration and documentation | No public migration applies because the handoff is internal. Focused ordinary source comments record the atomic parser boundary, physical-syntax-only responsibility, half-open span convention, and generated-tree removal boundary without publishing internal XML documentation. |
| Dependent-item unlock | The immutable graph now preserves every recognized section, header/entity record, parameter/value kind, zero/multiple cardinality, normalized ignored controls, and one-based source locations for SBRT-018. Lexer/parser failures aggregate deterministic `ExchangeStructureSyntaxException` evidence and publish no graph. |
| Regression and deployment proof | Release solution build passed with 0 warnings/errors; fast TUnit passed 52/52; integration TUnit passed 2/2 enabled tests with the opt-in network corpus test skipped; runtime `IsAotCompatible`, trim, and AOT analyzers passed with 0 warnings/errors. Limited whitespace/style/analyzer formatting completed for all touched C# files. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Internal model accidentally becomes a second public domain model | Violates AP-003 | Implementation review must fail any public exposure |
