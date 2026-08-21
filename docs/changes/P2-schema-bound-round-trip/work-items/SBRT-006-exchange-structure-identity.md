# SBRT-006: Exchange-structure values, identity, and graph registration

## 📌 Status

Approved

## 🚦 Delivery priority

- Priority: P2
- Rationale: Manual construction, portable entity graphs, and canonical writing require editable ISO structure values plus complete structure-owned occurrence identity and section membership semantics.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: None.
- Recommended order: Before generated entities/direct references and writer items.
- Governing records: AP-003, EP-003, architecture identity rules, and ADR-0005 pinned by the parent change.

## 🧩 Explicit governing constraints

`ExchangeStructure(HeaderSection)` permits temporarily unbound construction; `ExchangeStructure(HeaderSection, IReadOnlyCollection<SchemaDescriptor>)` snapshots schema descriptors by unique `Name`, rejecting duplicates with `ArgumentException`. `Header` and mutable `DataSections` expose only ISO values, and collection edits never validate. This item supplies the minimal descriptor identity contract; generated mapping hooks arrive in SBRT-013.

`Entity` has neither `InstanceName` nor an `ExchangeStructure` reference. `ExchangeStructure` owns the bidirectional reference-identity index and each registration's `DataSection` membership. Every Add requires an explicit section. Automatic-name Add returns the smallest unused positive name; explicit identical pairs are idempotent; conflicts throw `InvalidOperationException` before mutation; a section not owned by the receiving structure throws `ArgumentException` before mutation. Every Add re-enumerates current live references even when its root is registered. Remove is non-cascading and non-validating; no Replace API exists.

The exact public composition surface is `ExchangeStructure(HeaderSection)`, `ExchangeStructure(HeaderSection, IReadOnlyCollection<SchemaDescriptor>)`, read-only `Header`, and mutable `IList<DataSection> DataSections`. The only public Add operations are `Add(DataSection, Entity)` and `Add(DataSection, EntityInstanceName, Entity)`; the former returns the root `EntityInstanceName` and the latter returns `void`. `Remove(Entity)` and `Remove(EntityInstanceName)` return `bool`. No overload omits `DataSection`.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Runtime ISO header/data-section values, minimal descriptor identity, `Entity`, `EntityInstanceName`, exact `ExchangeStructure` construction/Add/Remove surface, XML docs, tests | Consumers can assemble temporary or descriptor-bound structures; names and section memberships are deterministic; graph Add follows live one-level references; Remove affects one registration only | Generated descriptor mapping hooks, generated reference enumeration, binder/writer, validation during mutation, entity-to-container navigation, cascading removal, or standalone Replace |

## 🔍 Current behavior and impact boundary

No runtime model exists. The contract must make manual ISO structure composition possible, support very large positive instance names without assuming `long`, canonicalize leading zeros, reject zero, reuse the smallest current gap, and let one object graph receive independent names/sections in another structure.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| Revised parent change defines the exact Add/Remove overloads and section rules | CD-35, CD-36, and CD-37 in `../change.md` | Stop if implementation would need a default section, cascading mutation, or different public signature |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-11 | A new mutable entity is registered through the model API | Continue editing, then write the model | The exchange structure owns its occurrence name and reference-identity reverse mapping without modifying the entity; registration does not run schema validation, and the same object may be registered in another structure under a different name |
| BC-11A | A root entity has physical references through direct attributes, nested aggregates/selects, shared objects, repeated occurrences, or cycles, including references added after the root was registered | Inspect `DirectReferences`, then add or re-add the root to an exchange structure | `DirectReferences` yields only one live level in physical write order without reflection and preserves repeated occurrences; every Add re-enumerates the graph, preserves existing registrations, registers each newly reachable object once by CLR reference identity, terminates cycles, and assigns names only in that exchange structure |
| BC-11B | A structure has name gaps, an already registered pair, a conflicting name/object pair, or a registered entity that the consumer removes | Invoke automatic/explicit Add or Remove | Automatic Add returns the smallest unused positive name; the identical explicit pair is idempotent; either-side conflicts throw `InvalidOperationException`; Remove affects one registration and section membership without cascading or validation, returns whether it removed anything, and makes the name reusable |
| BC-11C | A caller adds a graph with an owned or foreign data section, or omits the section argument in source | Compile and invoke the exact Add overloads | Only `Add(DataSection, Entity)` and `Add(DataSection, EntityInstanceName, Entity)` exist, so omitted-section calls fail compilation; an owned section receives new graph members root-first depth-first in physical-reference order, registered members keep their section, and a foreign section throws before mutation |
| BC-11D | A consumer creates an exchange structure manually with a header and zero, one, or duplicate generated descriptors | Construct the structure and edit its data-section collection | Descriptor-free construction permits temporarily unbound editing; schema-bound construction snapshots unique descriptors; duplicate schema names throw `ArgumentException` before construction succeeds; header and data sections remain ISO values, and adding/removing sections performs no automatic validation |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Identity ownership | Bidirectional mappings and one section membership are bijective per structure and use CLR reference identity | Entity state contains no name/container property and moving graphs needs no rewrite |
| Manual structure composition | Constructors retain the ISO header, snapshot descriptor identity, and expose mutable data sections; duplicate descriptors fail before publication | Section collection edits, including temporarily inconsistent removal, do not validate or invent ownership on entities |
| Registration/removal | Smallest-gap allocation, exactly two section-required Add overloads, idempotence/conflicts, root-first depth-first order, foreign-section atomicity, and one-item Remove follow CD-35–37 | No omitted-section overload, schema validation, reflection, cascade, default-section invention, or Replace occurs |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-11 | Runtime contract unit tests | Allocation, explicit-name preservation/conflict, leading-zero equality, large names, and independent structure mappings match the contract | Fast TUnit project |
| BC-11A | Graph integration with representative test entities | Repeats remain visible one level; transitive Add registers shared/cyclic references once, and re-Add registers only newly reachable objects while preserving existing names/sections | Fast TUnit project |
| BC-11B | Runtime public-contract tests | Gaps/reuse, identical pair, both conflict directions, both Remove overloads, dangling references, return values, and zero validation calls match the contract | Fast TUnit project plus public API snapshot |
| BC-11C | Section-required API and graph integration | A compile probe proves omitted-section calls have no matching overload; both owned-section overloads assign names/membership in exact order; foreign-section Add throws with byte-for-byte unchanged model state | Fast TUnit project plus public API/compile snapshot |
| BC-11D | Manual composition/public-contract tests | Both constructors, header identity, mutable section edits, descriptor snapshotting, duplicate rejection, and zero automatic validation match the exact contract | Fast TUnit project plus public API snapshot |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Identity/mutation contract | Tests prove smallest-gap names/reuse, bijection, exact overloads, idempotence/conflicts, non-cascading Remove, portability, and reference equality |
| Structure composition | Tests prove exact constructors/properties, ISO-only values, descriptor snapshot/duplicates, freely editable sections, and no validation calls |
| Graph/section registration | Tests prove physical traversal/re-Add order, explicit one/multi-section selection, omitted-overload absence, foreign-section atomicity, cycles, sharing, repeats, and no validation calls |

## ⏱️ Workload estimate

- Planning range: 0.5–0.9 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Generated `DirectReferences` is supplied by SBRT-014; test entities provide the smallest proving consumer here. Cross-schema validity is later validation, not Add-time behavior.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA and runtime/test/XML artifacts |
| Behavior-case proof | Commands/results for BC-11, BC-11A, BC-11B, BC-11C, and BC-11D |
| Migration and documentation | Public identity/Add XML contract |
| Dependent-item unlock | Entity/identity/section/Add/Remove guarantee for SBRT-010, SBRT-014, SBRT-020, and SBRT-022 |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Numeric representation truncates standard names | Non-conforming identity | Test values above `long.MaxValue` and canonical formatting |
