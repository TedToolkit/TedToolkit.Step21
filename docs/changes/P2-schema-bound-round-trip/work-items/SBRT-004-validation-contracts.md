# SBRT-004: Diagnostics, validation, and stage-exception ABI

## 📌 Status

Approved

## 🚦 Delivery priority

- Priority: P2
- Rationale: Parsers, binders, generated validation, and read/write boundaries need one exact AOT-safe public evidence contract.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: None.
- Recommended order: Before aggregate, descriptor, validation, and boundary items.
- Governing records: EP-003 and ADR-0003/0005 pinned by the parent change.

## 🧩 Explicit governing constraints

The minimal non-nested public evidence ABI is exact: `Step21Diagnostic` has `Code`, `Severity`, `Message`, and optional `SourceLocation`; severity is Information/Warning/Error. `ValidationFailure` has `Code`, deterministic string `Path`, `Message`, and optional `SourceLocation`; `ValidationResult` aggregates it. Dedicated syntax/binding/read-validation/write-validation/capability exceptions retain complete diagnostics or result. No public context, accumulator, common exception base, validator DSL, reflection discovery, dynamic code, or validation package is authorized.

The exact exception types are `ExchangeStructureSyntaxException`, `ExchangeStructureBindingException`, `ExchangeStructureReadValidationException`, `ExchangeStructureWriteValidationException`, and `ExchangeStructureCapabilityException`.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Exact top-level runtime diagnostics/validation values, source location, five exception types, tests, and XML docs | Consumers can inspect complete diagnostics/results and catch each necessary stage-specific exception without a validation framework or public stage context | Nested/public context or accumulator, common exception base added only for taxonomy, rule registration, fluent APIs, schema execution, read/write implementation, warning severity in `ValidationFailure`, or translation of I/O exceptions |

## 🔍 Current behavior and impact boundary

No runtime semantic types currently exist. The new ABI must stay schema-neutral and must not introduce domain concepts beyond ISO/EXPRESS traceability.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| Revised exact ABI is recorded in the parent change | CD-39 and public-boundary section in `../change.md` | Any extra public field, severity, exception, or validator API returns to change design |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-06B | A consumer wants feedback before writing a temporarily invalid edited model | Invoke `ExchangeStructure.Validate()` | Validation has no side effects, returns one Step21 `ValidationResult` containing all detected entity/path failures, and does not throw solely because the result is invalid |
| BC-23A | A consumer supplies generated descriptors and uses the public Part 21 boundary | Call `ExchangeStructure.Read`, `structure.Write`, or `structure.WriteEntity` | Only strong public values cross generated descriptor hooks; no public syntax/context/reader/writer facade or nested public type exists; duplicate descriptor schema names throw `ArgumentException` before input is consumed; stage failures use their dedicated complete-evidence exception; write validation/capability failures produce no output, while I/O failures retain their underlying exception behavior |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Diagnostic/failure ABI | Exact approved fields and severity are immutable and deterministic; result represents all failures | Every `ValidationFailure` invalidates; warnings use `Step21Diagnostic`, not the failure ABI |
| Exception boundary | Syntax/binding/capability exceptions retain diagnostics; read/write validation exceptions retain the complete result | Constructing/inspecting invalid evidence never throws; I/O exceptions are not reclassified |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-06B | Public contract unit tests | Multiple supplied failures remain ordered/inspectable, invalid result inspection has no side effect or throw, and exceptions retain the same complete result | Fast TUnit project plus generated XML compilation |
| BC-23A | Minimal public exception/diagnostic contract tests | Exact necessary top-level types, fields, severities, inheritance/evidence properties, absence of context/facade/nested extras, and I/O non-translation behavior match the parent contract | Fast TUnit project plus public API snapshot |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Stable minimal ABI | Public API/XML tests prove exact diagnostic/failure fields, severities, immutability, aggregation, and all five dedicated exceptions |
| Dependency boundary | Package graph contains no validation engine and build has no trimming/AOT warnings introduced by the ABI |

## ⏱️ Workload estimate

- Planning range: 0.3–0.5 person-months.
- Confidence: High.
- Assumptions and excluded work: Rule generation and `ExchangeStructure` traversal are delivered by dependent items.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA and runtime/test/XML artifacts |
| Behavior-case proof | Contract test commands/results and BC-06B/BC-23A ABI assertions |
| Migration and documentation | Caller-visible XML contract and exception guidance |
| Dependent-item unlock | Versioned validation ABI for SBRT-005, SBRT-006, SBRT-015, and SBRT-016 |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Convenience additions grow into a framework | Public lock-in and AOT risk | Reject additions not required by the approved ABI |
