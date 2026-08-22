# SBRT-026: Conformance corpus and public capability documentation

## 📌 Status

Completed

## 🚦 Delivery priority

- Priority: P2
- Rationale: Final evidence and exact capability claims prevent accidental overstatement of ISO conformance.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-002 normative grammar; SBRT-003 advanced diagnostics; SBRT-021 multi-schema binding; SBRT-023 semantic round trip; SBRT-025 package/AOT proof.
- Recommended order: Final delivery row after all capability proofs it documents.
- Governing records: Product intent, AP-003, EP-001/003, architecture, and ADR-0002/0003/0005 pinned by the parent change.

## 🧩 Explicit governing constraints

Documentation must state the exact minimal non-nested public API plus syntax, binding, validation, writing, operational, AOT, and package boundaries. It must distinguish necessary strong ISO/evidence types from internal syntax/stage machinery. Corpus inputs remain independently sourced, license/provenance recorded, integrity pinned, opt-in when networked, and may not replace normative clause evidence.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Pinned corpus manifest/fixtures/tests, repository/package READMEs, API/conformance docs, build gates | Released documentation and regression evidence exactly match delivered complete syntax, typed multi-schema, validation/write, unsupported-operation, and AOT boundaries | New runtime capability, weakening earlier item verification, byte-perfect source preservation, or claiming unsupported operational facilities |

## 🔍 Current behavior and impact boundary

The repository has pinned NIST/buildingSMART corpus infrastructure and parser-only README claims. Final generated-model, validation, writer, multi-schema, and AOT behavior is not yet represented in corpus assertions or user documentation.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| All named prerequisite capability/evidence records are complete | SBRT-002/003/021/023/025 completion evidence | Documentation/corpus claims would speculate about undelivered behavior |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-14 | A valid supported model is written and read again | Compare schema-bound semantics | Entity identities, types, values, aggregate semantics, and references are equivalent despite canonical formatting differences |
| BC-17 | A proposed `.g4` behavior change has corpus evidence but no ISO clause evidence | Review or implement the change | The change is rejected or explicitly routed through an accepted compatibility-extension ADR |
| BC-18 | A consumer looks for JSON/XML support or dependencies | Inspect generated/runtime APIs and package graph | No such serialization contract, adapter hook, attribute, or dependency is present |
| BC-24 | A syntactically valid Edition 3 structure uses an advanced section whose operational semantics are not delivered yet | Parse it through the normative start rule and request the unsupported operation | Parsing preserves the standard structure; the later operation returns an explicit unsupported-capability diagnostic rather than a syntax error |
| BC-23A | A consumer supplies generated descriptors and uses the public Part 21 boundary | Call `ExchangeStructure.Read`, `structure.Write`, or `structure.WriteEntity` | Only strong public values cross generated descriptor hooks; no public syntax/context/reader/writer facade or nested public type exists; duplicate descriptor schema names throw `ArgumentException` before input is consumed; stage failures use their dedicated complete-evidence exception; write validation/capability failures produce no output, while I/O failures retain their underlying exception behavior |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Evidence integrity | Corpus artifacts have HTTPS provenance, license, size, SHA-256, ignored cache, and semantic assertions | Network tests remain explicit opt-in and normative evidence remains separate |
| Capability/API claims | README/API docs distinguish complete syntax from staged advanced operations, document the structure-centric read/write API and necessary strong/evidence types, and state multi-schema/AOT/write validation exactly | No public syntax/context/facade/registry/resolver/nested type or JSON/XML/domain-specific/byte-preservation promise |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-14 | Pinned corpus semantic integration | Representative licensed schemas/files pass generated read-write-read semantic comparison | Opted-in integration project plus fast repository fixtures |
| BC-17 | Traceability documentation audit | Every corpus-driven grammar expectation links normative evidence or accepted extension ADR | Bounded grammar/corpus traceability review |
| BC-18 | Public docs/package audit | Docs and dependency/API evidence explicitly exclude JSON/XML contracts | Release build, package inspection, README/API review |
| BC-24 | Capability-boundary integration/docs | Advanced valid syntax parses and documented unsupported operation returns exact capability diagnostic | Fast/integration fixtures plus documentation assertion |
| BC-23A | Minimal API documentation audit | Examples use only `SchemaDescriptor`, `ExchangeStructure`, `DataSection`, `Entity`, necessary strong values/evidence/exceptions, and no forbidden syntax/stage/facade/nested concept | README/API compilation plus public API snapshot comparison |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Full regression gate | Fast tests, opted-in pinned corpus, Native AOT proof, and Release build pass with zero errors/warnings |
| Truthful user contract | README/package/API/conformance docs match delivered behavior and every parent completion criterion has recorded evidence or explicit in-scope disposition |

## ⏱️ Workload estimate

- Planning range: 0.3–0.5 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Corpus licenses permit automated use; no new behavior is implemented here.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Completion record |
| --- | --- |
| Delivery-boundary check | Started from `c81ca25` on `main`. Added final repository/package/conformance documentation, documentation/package contract tests, public advanced-operation evidence, stronger packed-consumer semantic reread assertions, and one clause-backed EXPRESS empty-entity-constructor correction discovered by the pinned corpus. No branch, remote, public syntax/context/facade/registry/resolver, JSON/XML contract, reflection path, or Linux proof was added. |
| BC-14 semantic proof | Existing focused complex-mapping tests compare semantic signatures across read-write-read. The packed two-schema consumer now rereads canonical output and reasserts complex component types/values, aggregate contents, edited simple values, cross-schema reference identity, and validation before both normal execution and Native AOT execution report `PACKED_AOT_OK`. |
| BC-17 grammar governance | The buildingSMART EXPRESS corpus exposed valid zero-attribute `EntityName()` constructors. ISO 10303-11 Edition 2 production evidence distinguishes a non-empty `actual_parameter_list` from an entity constructor with an optional expression list. `Express.g4` therefore retains a separate empty-constructor production; focused tests accept empty entity construction, reject empty function application with `EXPRESS-BIND-EXPECTED-ENTITY-CONSTRUCTOR`, generate executable static construction, and preserve the neighboring invalid procedure-call case. Deterministic ANTLR regeneration and the complete pinned corpus pass. Corpus evidence confirmed the defect but did not define the fix. |
| BC-18 package boundary | Generated source, runtime assets, package entries, dependency graph, and documentation tests reject JSON/XML contracts and dependencies. Analyzer-only RoslynHelper dependencies remain under `analyzers/dotnet/cs`; the packaged project README is byte-equal to `src/TedToolkit.Step21/README.md`. |
| BC-23A and BC-24 public boundary | The cumulative public API snapshot remains exact. Package examples use only generated types/descriptors plus `ExchangeStructure`, `DataSection`, `Entity`, strong values, validation evidence, and dedicated exceptions. Public `ExchangeStructure.Read` tests prove valid anchor/signature syntax reaches exact capability diagnostics; internal syntax evidence also fixes the reference diagnostic, while public external entity use retains its more precise `P21.READ.REFERENCE.EXTERNAL` evidence. No partial model or raw syntax type is exposed. |
| Regression and deployment proof | Release solution build passes with 0 warnings/errors; fast TUnit passes 241/241; default integration passes 5/5 enabled with 1 explicit opt-in skip; opted-in pinned corpus/package/documentation integration passes 6/6; real packed-consumer `win-x64` Native AOT publish/run passes with 0 build/AOT/trimming/dynamic-code warnings and prints `NATIVE_AOT_PACKAGE_PROOF_OK win-x64`. Documentation links, package README content, work-item structure, and `git diff --check` are gated separately. |
| Migration and documentation | Root README is the repository router, `src/TedToolkit.Step21/README.md` is the packaged consumer guide, and `docs/conformance/README.md` is the delivered/syntax-only/outside-contract evidence matrix. Product, architecture, grammar, generator, generated-value/aggregate/descriptor/entity, read, reference, section, writing, and expression records no longer describe delivered work as pending. |
| Independent review | Final trace checked normative grammar authority, generated artifacts, binding/emission behavior, semantic round-trip strength, exact advanced-operation evidence, public/API/package dependency exclusions, documentation link/content truthfulness, external-corpus provenance/integrity, Release diagnostics, and Native AOT output. It corrected the public `REFERENCE` documentation to preserve the dedicated read-validation result rather than falsely promise one uniform capability code. No blocking or advisory finding remains. |
| Dependent-item unlock | Final change-closure evidence is complete; there is no dependent work item. |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Corpus passes syntax but lacks semantic diversity | Weak conformance evidence | Add only licensed, pinned fixtures that exercise declared semantics; do not substitute volume for coverage |
