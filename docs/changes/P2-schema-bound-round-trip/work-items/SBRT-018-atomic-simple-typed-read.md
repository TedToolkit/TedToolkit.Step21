# SBRT-018: Atomic simple typed reading

## 📌 Status

Completed

## 🚦 Delivery priority

- Priority: P2
- Rationale: This is the first complete public parse-bind-validate publication slice.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-003 supplies internal syntax; SBRT-013 simple mapping; SBRT-015 structural validation.
- Recommended order: Before reference and multi-section binding.
- Governing records: AP-003, EP-003, ADR-0003/0005, and atomic publication architecture pinned by the parent change.

## 🧩 Explicit governing constraints

`ExchangeStructure.Read(TextReader, IReadOnlyCollection<SchemaDescriptor>)` returns only a complete validated mutable structure. Duplicate `SchemaName` values in the descriptor collection throw `ArgumentException` before the source is consumed. Syntax, binding, and invalid-publication outcomes throw their exact dedicated exceptions with complete diagnostics/result; underlying `TextReader` I/O exceptions retain their behavior. Internal syntax/context and partially hydrated entities never escape, and no public reader facade exists.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Exact static `ExchangeStructure.Read` entry point, descriptor collection, strong parameter allocation/hydration, dedicated read exceptions, fixtures, docs | One simple data section binds every simple scalar/parameter/OPTIONAL/aggregate value, validates, and publishes atomically without a public reader/context type | Entity references/cycles, multiple sections/schemas, complex mappings, public raw parse/context/result, reader facade, or translation of I/O exceptions |

## 🔍 Current behavior and impact boundary

Parser tests return parser diagnostics only. Prerequisites supply internal syntax, generated mappings, and validation; this item joins them without exposing a schema-free public model.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-003, SBRT-013, and SBRT-015 completion outputs | Their completion records | Typed read cannot preserve syntax, mapping, validation, and atomicity together |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-09 | A simple entity has explicit and absent OPTIONAL attributes | Read then write it | Generated properties receive typed values/absence, and output parameters retain standard order with `$` for absence |
| BC-13 | `FILE_SCHEMA` does not match the generated schema metadata | Bind or write the model | A schema-mismatch diagnostic prevents accidental binding/writing under the wrong schema |
| BC-23A | A consumer supplies generated descriptors and uses the public Part 21 boundary | Call `ExchangeStructure.Read`, `structure.Write`, or `structure.WriteEntity` | Only strong public values cross generated descriptor hooks; no public syntax/context/reader/writer facade or nested public type exists; duplicate descriptor schema names throw `ArgumentException` before input is consumed; stage failures use their dedicated complete-evidence exception; write validation/capability failures produce no output, while I/O failures retain their underlying exception behavior |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Exact atomic public read | The approved static signature succeeds with a complete validated model; duplicate descriptor names fail before input consumption; syntax/bind/validation errors use their dedicated complete-evidence exception | Syntax/context remains internal, no reader facade or partial entity returns, and I/O exceptions remain I/O exceptions |
| Simple hydration | Typed parameters and OPTIONAL absence follow descriptor order | Public generated entities remain mutable only after successful publication |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-09 | End-to-end typed-read integration | Present/absent OPTIONAL and scalar/aggregate parameters hydrate exact generated members | Fast TUnit project |
| BC-13 | Read-boundary negative integration | Wrong/missing descriptor yields schema-mismatch exception/diagnostics and no returned model/syntax/entity | Fast TUnit project with leak/public API assertions |
| BC-23A | Minimal public read exception matrix | Exact static signature/descriptor collection succeeds; duplicate descriptor names throw before a probe source is consumed; syntax, binding, and read-validation failures expose complete correct evidence; throwing `TextReader` behavior is not translated; API has no reader/context/nested type | Fast TUnit project plus public API snapshot |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| First typed read | Exact public signature reads representative simple values into a validated generated model |
| Failure atomicity/API | Syntax, binding/mismatch, and read-validation exceptions match exact types/evidence, expose no internal/partial state or facade/context, and preserve I/O exceptions |

## ⏱️ Workload estimate

- Planning range: 0.6–1.1 person-months.
- Confidence: Medium.
- Assumptions and excluded work: References and multi-section/schema populations are subsequent items.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Started from `7e72a0f`. Added only the exact static `ExchangeStructure.Read(TextReader, IReadOnlyCollection<SchemaDescriptor>)` method, runtime-internal atomic parse/bind/hydrate orchestration and lexical value decoding, the generated BOOLEAN/LOGICAL physical-symbol mapping needed by that boundary, focused tests/API snapshot, and conformance/README updates. No public reader, syntax/raw/result/context/registry type, nested public type, reference hydration, multi-section/schema population, complex mapping, writer, reflection, dynamic code, branch, remote, or new package was added. `validate-work-items.sh docs/changes/P2-schema-bound-round-trip` reported `Work-item delivery boundary: valid`. |
| Behavior-case proof | Red: the focused test compilation failed because `ExchangeStructure.Read` did not exist. Green/refactor: `AtomicSimpleReadTests` passed 4/4 and the complete fast TUnit project passed 206/206. BC-09 reads and projects arbitrary-precision integer/real/number, full Part 21 STRING directives and ISO 8859 pages, bit-accurate binary, BOOLEAN/LOGICAL, enumeration, typed SELECT alternatives, recursive aggregates, present/absent OPTIONAL values in physical order, canonical occurrence identity, post-publication mutation, and a valid result. BC-13 proves exact `FILE_SCHEMA` mismatch evidence. BC-23A proves descriptor duplicates fail before a probe reader is consumed; syntax, aggregated binding, invalid physical value/name, read-validation, and capability failures use their exact evidence-bearing exceptions; reader I/O remains `IOException`; diagnostics use `<reader>` with positive coordinates; and no processing API leaks. |
| Regression and dependency proof | Isolated Release solution build passed with 0 warnings and 0 errors. Integration TUnit passed 3/3 enabled cases with the explicit opt-in external corpus case skipped. Runtime `IsAotCompatible`, trim-analyzer, and AOT-analyzer build passed with 0 warnings/errors. Runtime dependency inspection reports only `Antlr4.Runtime.Standard` 4.13.1. Production scanning found no reflection, dynamic dispatch, expression compilation, assembly scanning, or reader/writer facade. |
| Migration and documentation | Public XML documentation records atomic publication, duplicate preflight, `<reader>` diagnostics, exact exceptions, capability staging, and unchanged reader failures. `docs/conformance/atomic-simple-read.md` supplies the public usage example, supported value/mapping boundary, failure matrix, staged exclusions, and proof; README and descriptor conformance text now link the delivered boundary. The cumulative public API snapshot contains only the one approved static method addition. |
| Independent review | Final read-only trace review compared the work item, architecture constraints, production diff, generated dispatch, tests, public metadata, documentation, and verification evidence. No blocking or advisory findings remain. During review, binding was corrected to continue aggregating hydration diagnostics after allocation failures, invalid occurrence and lexical edge cases were translated to dedicated binding evidence, and BC-09 was strengthened to prove both present/absent OPTIONAL state and mutability after publication. |
| Effort and variance | Approximately 1.2 elapsed implementation/review hours, materially below the 0.6–1.1 person-month planning range because the parser syntax graph, descriptor dispatch, validation closure, test host, and evidence ABI were already complete. The only local contract choice was the deterministic `<reader>` logical source name required by a pathless `TextReader` signature; it was fixed in XML, tests, and conformance documentation. |
| Dependent-item unlock | The atomic private staging/publication boundary, complete simple parameter conversion, allocation-before-hydration structure, source-located aggregate binding evidence, exact public entry point, and exception matrix are implemented and locally proven for SBRT-019 reference hydration and SBRT-020 same-schema multi-section binding. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Internal hydration state leaks through exceptions/callbacks | Violates atomicity | Public-surface and failed-read lifetime tests gate completion |
