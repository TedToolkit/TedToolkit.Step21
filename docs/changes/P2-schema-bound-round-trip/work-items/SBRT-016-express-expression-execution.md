# SBRT-016: EXPRESS expression execution

## 📌 Status

Implemented

## 🚦 Delivery priority

- Priority: P2
- Rationale: Validation/write-reachable schema rules cannot be correct without full expression semantics.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-007 supplies complete expression IR; SBRT-008 bound names/types; SBRT-009 generator host; SBRT-004 validation ABI.
- Recommended order: Before rule dependency closure and complex mapping.
- Governing records: AP-003, EP-003, ADR-0003, and complete reachable execution constraint in the parent change.

## 🧩 Explicit governing constraints

Compile strongly typed static C# from immutable bound EXPRESS expression semantics. This item owns the typed expression IR and the expression-family compiler; SBRT-017 owns attachment to validation/writing roots, dependency closure, named rule identity, and aggregate failures. No runtime interpreter API, reflection, dynamic code, expression compilation, or second validation generator is authorized.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Immutable typed expression IR, strongly typed generated execution, diagnostics, conformance fixtures | Scalar, logical, aggregate, reference, navigation, and query expression families compile to static C# and evaluate with EXPRESS semantics | Attaching expressions to validation/writing roots, dependency closure, named failure aggregation, a general public procedure API, or changing the minimal validation ABI |

## 🔍 Current behavior and impact boundary

Complete source-located expression syntax plus bound declaration/name/type identities exist after prerequisites, but a typed expression tree and executable semantics do not. Structural validation must remain unchanged until SBRT-017 composes the resulting expression compiler into reachable rules.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| Complete source-located expression syntax, bound declaration/name/type identities, and generator/validation contracts | SBRT-007/008/009/004 completion evidence | Typed expression binding or generated evaluation would guess syntax, scope, type identity, or failure shape |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-02A | Valid EXPRESS expressions cover scalar, logical, aggregate, reference, navigation, query, and built-in expression families | Bind each expression, compile it into a strongly typed generated C# fixture, and execute representative positive/negative inputs | Every expression form has immutable typed IR, generated code compiles without dynamic/reflection facilities, and evaluation matches EXPRESS value/UNKNOWN/error semantics; rule reachability and failures remain SBRT-017 |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Semantic fidelity | Each expression family matches EXPRESS typing, LOGICAL/UNKNOWN, aggregate, reference, and error semantics | Unsupported expression forms are source-located generation errors; validation/write reachability is evaluated by SBRT-017 |
| Execution boundary | Generated code is statically reachable and AOT-ready | No public interpreter/DSL or dynamic/reflection path |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-02A | Table-driven generated conformance tests | Representative valid/invalid inputs for every expression family produce expected values/failures and stable source traceability | Fast TUnit generator/runtime project; bounded ISO example comparison |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Expression-family coverage | Coverage inventory maps every validation/write-reachable expression IR form to passing positive/negative generated tests |
| AOT-safe generation | API/code audit finds no reflection/dynamic execution and Release build is warning-free |

## ⏱️ Workload estimate

- Planning range: 0.8–1.5 person-months.
- Confidence: Low.
- Assumptions and excluded work: Rule/function dependency closure is SBRT-017; no unrelated public procedure API.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Started from `c623c66`. Added Analyzer-internal immutable typed expression IR, contextual expression binding, static C# emission context/results, exact finite REAL/NUMBER operations needed by generated semantics, focused public API snapshots, generated execution tests, and one conformance inventory. Updated CD-47 and SBRT-017 only to record the maintainer-approved expression-versus-reachability boundary. No validation/write attachment, rule dependency closure, named failure aggregation, public evaluator/DSL/context, reflection, dynamic execution, expression compilation, second validation generator, or new dependency was added. The work-item validator reported `Work-item delivery boundary: valid`. |
| Behavior-case proof | BC-02A binds every grammar expression shape to source-located immutable typed IR and compiles generated fixtures for all literal, unary, binary, logical, comparison, interval, navigation, indexing/slicing, aggregate/repetition, query, application, and built-in families. Positive, UNKNOWN/indeterminate, invalid domain/count/index/format/value, divisor-sign, sparse ARRAY, value-versus-instance equality, contextual nominal/aggregate, and source-location cases pass. Reused local names prove contextual types follow declaration identity. Complete fast TUnit passed 188/188. |
| Migration and documentation | `docs/conformance/express-expression-execution.md` maps every expression and built-in family to static execution evidence and records the normative edge decisions. The compiler remains Analyzer-internal; the document explicitly assigns reachable validation/write attachment and model traversal to SBRT-017 and complex material composition to SBRT-023. No consumer migration is required by this internal compiler boundary. |
| Regression and deployment proof | Release solution build passed with 0 warnings/errors. Integration TUnit passed all 3 enabled cases; only the explicit opt-in external-network corpus case was skipped. Runtime `IsAotCompatible`, trim, and AOT analyzer build passed with 0 warnings/errors. Runtime dependency inspection still reports only `Antlr4.Runtime.Standard` 4.13.1. Limited formatting and `git diff --check` passed. Static audit found no reflection, `dynamic`, `System.Linq.Expressions`, expression compilation, or activation path in the expression compiler/numeric support. Strict read-only review found no remaining blocking, important, suggestion, or design-deviation findings. |
| Dependent-item unlock | Immutable typed expressions and strongly typed static evaluation now supply the executable semantic leaves consumed by SBRT-017 reachable-rule attachment and model traversal callbacks, and by SBRT-023 complex-entity composition. |
| Actual effort and variance | Completed in one continuing agent implementation session; the human person-month estimate is not directly comparable. The approved boundary correction moved typed expression IR into this item because the prerequisite retained complete syntax and bound name/type identity but did not yet own semantic expression typing. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| EXPRESS edge semantics exceed estimate | Delayed closure | Preserve approved semantic scope and request work-plan reapproval for material estimate change |
