# SBRT-017: Complete reachable EXPRESS rule closure

## 📌 Status

Approved

## 🚦 Delivery priority

- Priority: P2
- Rationale: Final validity and write eligibility require every reachable schema dependency to execute.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-015 supplies structural validator/XML path; SBRT-016 supplies expression semantics.
- Recommended order: Before final multi-schema, complex mapping, and pre-write validation.
- Governing records: AP-003, EP-003, ADR-0003, and CD-22 completion rule pinned by the parent change.

## 🧩 Explicit governing constraints

Reachability begins from validation and Part 21 writing. Every reachable WHERE, UNIQUE, derived expression, query, constant, function, and dependency executes by completion; unrelated declarations remain source-located IR and do not create a public general execution API.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Reachability/dependency analysis, generated rules/functions, failure/XML traceability, tests | Complete dependency closure executes and every violated named/unnamed rule aggregates a stable failure | Public procedure/function invocation, executing unreachable declarations, or accepting a validation/write-relevant unsupported marker |

## 🔍 Current behavior and impact boundary

Structural rules and individual expressions can execute after prerequisites, but no closure yet proves all reachable declarations or suppresses false conformance when a dependency is unsupported.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-015 traceability and SBRT-016 expression execution complete | Both completion records | Dependency closure cannot produce correct execution or failure identity |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-02A | A valid EXPRESS schema contains `WHERE`, `UNIQUE`, derived expressions, queries, constants, or functions reachable from validation/writing | Build, validate, read, and write the consumer model | The relevant semantics are compiled into executable generated validation/mapping behavior and produce source/schema-traceable failures for every violated rule |
| BC-02B | A valid EXPRESS schema contains executable declarations that cannot be reached from validation or Part 21 writing | Build the consumer project | The declarations remain represented in source-located IR, no unrelated public execution API is generated, and their presence does not create a false validation gap |
| BC-16A | A schema declaration has aggregate bounds, optionality, a named `WHERE`/`UNIQUE` rule, or a generated unnamed structural rule | Inspect generated XML documentation and trigger the rule | The documented schema/declaration, normalized requirement, validation boundary, and stable constraint ID match the generated `ValidationFailure.Code` and contain valid deterministic XML |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Reachable closure | Every reachable dependency executes or generation fails source-located; no relevant unsupported gap survives | Unreachable declarations remain IR-only and do not expand public API |
| Failure/XML parity | Named/unnamed stable IDs and normalized requirements match executable behavior | Results aggregate; no fail-fast or warning downgrade |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-02A | Generated dependency-closure integration | Nested rule/function/query/constant/derived dependencies execute and every violation is reported | Fast TUnit project with coverage inventory |
| BC-02B | Negative/public-surface contract | Unreachable executable declarations remain IR-only and generate no public invocation facade | Generator output/API inspection |
| BC-16A | Rule/XML contract | Named WHERE/UNIQUE and generated rules produce matching deterministic XML IDs/requirements/source | XML compilation and generated runtime tests |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Closure completeness | Automated reachability inventory reports zero validation/write-relevant unsupported dependency |
| Traceable aggregate behavior | Positive/negative nested-rule tests and XML parity pass with all failures retained |

## ⏱️ Workload estimate

- Planning range: 0.6–1.2 person-months.
- Confidence: Low.
- Assumptions and excluded work: General-purpose unrelated procedure invocation remains a non-goal.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA and analysis/generator/test/XML artifacts |
| Behavior-case proof | Commands/results for BC-02A, BC-02B, and BC-16A |
| Migration and documentation | Exact executable-semantics boundary documented |
| Dependent-item unlock | Complete reachable rule guarantee for SBRT-021, SBRT-023, and SBRT-024 |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Recursive dependencies or evaluation cycles | Incorrect/nonterminating validation | EXPRESS semantics and focused cycle fixtures govern completion |
