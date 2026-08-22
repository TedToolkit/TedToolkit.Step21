# SBRT-002: Complete Edition 3 clear-text grammar

## 📌 Status

Implemented

## 🚦 Delivery priority

- Priority: P2
- Rationale: Every later Part 21 behavior relies on a clause-traceable complete syntax boundary.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-001 supplies deterministic generated artifacts.
- Recommended order: Before syntax-model and binding items.
- Governing records: AP-003, EP-001, and the active schema-bound architecture pinned by the parent change.

## 🧩 Explicit governing constraints

ISO 10303-21:2016 Edition 3 WSN is authoritative. Corpus acceptance cannot override clause evidence, and compatibility extensions require an accepted ADR.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| `STEP.g4`, clause-traceability evidence, generated artifacts, grammar fixtures | Normative start rule recognizes every declared clear-text section form, enforces ordering, and reaches EOF | Operational semantics for anchors, external references, signatures, archives, or ECMAScript |

## 🔍 Current behavior and impact boundary

The current grammar proves only two classic minimal files and one missing `ENDSEC`; it does not evidence full Edition 3 section coverage, trailing-input rejection, or production-level ISO traceability.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-001 deterministic generation gate is complete | SBRT-001 completion evidence | Grammar changes cannot be accepted without reproducible derived artifacts |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-17 | A proposed `.g4` behavior change has corpus evidence but no ISO clause evidence | Review or implement the change | The change is rejected or explicitly routed through an accepted compatibility-extension ADR |
| BC-21 | Parse a valid complete exchange structure with all section forms in the declared conformance scope, followed by trailing non-separator input | Invoke the normative start rule | The valid structure reaches EOF; trailing input and invalid section order are rejected according to the cited WSN |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Normative syntax | Every production change cites clause/WSN evidence and has focused positive/negative fixtures | Existing valid repository and pinned-corpus syntax remains accepted unless ISO evidence says otherwise |
| Recognition versus operation | Valid advanced syntax parses independently of later runtime support | No false claim that syntactic support provides operational semantics |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-17 | Review-contract test/manual audit | Every changed production and fixture carries a normative citation or accepted compatibility ADR | Bounded production-to-WSN traceability audit |
| BC-21 | Grammar unit/corpus regression | All valid section forms reach EOF; trailing data and invalid order produce errors | Fast TUnit project; opted-in integration corpus where applicable |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Complete syntax scope | Traceability table and focused fixtures cover every declared Edition 3 clear-text production |
| Regression safety | Generated artifacts are deterministic and fast plus opted-in corpus tests pass |

## ⏱️ Workload estimate

- Planning range: 0.8–1.5 person-months.
- Confidence: Low.
- Assumptions and excluded work: The final public text is sufficient evidence; operational advanced-section behavior belongs to later items.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Starting SHA `0df61cf`. Changed the Part 21 grammar from one combined prototype to target-independent `STEPLexer.g4` and `STEPParser.g4`, regenerated the internal parser/lexer/visitor artifacts, updated both native generation scripts and their artifact contract test, changed both parser test adapters to call `exchangeFile`, and added clause-traced positive/negative fixtures plus conformance documentation. The split is an implementation factoring required to make signature Base64 contextual without target-language actions; it does not broaden the authorized outcome or add operational semantics. `validate-work-items.sh docs/changes/P2-schema-bound-round-trip` reported `Work-item delivery boundary: valid`. |
| Behavior-case proof | BC-17: `docs/conformance/part21-edition3-grammar.md` maps every Table 1/2/3/4 production family to clauses, grammar rules, and focused fixtures; no corpus-only extension was introduced. BC-21 Red first failed on all-section, zero-DATA/multiple-signature, trailing-input, ignored-control, invalid-Base64, valid lowercase URI escape, invalid URI-structure, and non-graphic-control cases as their boundaries were introduced. Green/Refactor: focused `FileTests` passed 20/20; the complete fast TUnit project passed 23/23. Valid fixtures prove all section forms, zero and multiple DATA/signature cardinalities, typed/untyped/omitted/list parameters, simple/complex records, all occurrence-name forms, RFC 2396 URI forms, tag/anchor forms, string directives, print controls, ignored controls inside/between tokens, and EOF. Negative fixtures prove ordering, required header order, trailing input, token case/shape, URI escaping/structure, tag/anchor constraints, DATA left-hand side, Base64, and Table 3 signature punctuation. |
| Regression proof | `dotnet run --no-restore --project tests/TedToolkit.Step21.IntegrationTests --configuration Release` passed 2/2 enabled policy/manifest cases; the network corpus case was explicitly skipped because `TEDTOOLKIT_STEP21_EXTERNAL_CORPUS` was not opted in. `dotnet build TedToolkit.Step21.slnx --configuration Release --no-restore` passed with 0 warnings and 0 errors. PowerShell and shell generation produced the same 16 paths and byte-identical SHA-256 content. |
| Migration and documentation | `README.md` states the exact complete Edition 3 clear-text recognition boundary and explicitly excludes advanced operation. `docs/conformance/part21-edition3-grammar.md` records production/token traceability, the WSN-authoritative no-semicolon signature decision, test evidence, and the distinction between recognition and operation. The internal visitor names change from combined-grammar `STEPVisitor`/`STEPBaseVisitor` to split-grammar `STEPParserVisitor`/`STEPParserBaseVisitor`; no public API changes. |
| Effort and variance | Approximately 3 elapsed implementation/verification hours, materially below the 0.8–1.5 person-month range because the final text, generation foundation, and small parser-test harness were already available. The main unplanned work was exact ignored-control handling, lexical overlap between keywords and tag names, context-bound RFC 4648 signature recognition, and context-bound RFC 2396 URI recognition. |
| Dependent-item unlock | `exchangeFile` now guarantees complete section ordering and EOF, and internal visitors expose every Edition 3 clear-text parse form. SBRT-003 may build its internal syntax model and advanced-operation diagnostics without encoding a subset grammar. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Existing grammar structure differs materially from WSN | Estimate growth | Return to work-plan approval if outcome/estimate changes materially; do not weaken ISO scope |
