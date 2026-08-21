# SBRT-014: Generated one-level direct references

## 📌 Status

Approved

## 🚦 Delivery priority

- Priority: P2
- Rationale: Graph registration and writing need exact physical reference occurrences without reflection.

## 🔗 Delivery context

- Parent change: `../change.md`
- Parent change goal: When this change is complete, a .NET consumer can supply an EXPRESS schema, read or construct a schema-bound strongly typed model, navigate relationships as ordinary generated entity properties, and write the model as an ISO 10303-21 exchange structure, proven by deterministic generated-code compilation and semantic read-write-read conformance cases.
- Logical prerequisites: SBRT-006 supplies graph Add; SBRT-010 supplies entities; SBRT-011 supplies selects; SBRT-012 supplies aggregates.
- Recommended order: Before reference hydration and writer graph integration.
- Governing records: AP-003, EP-003, and ADR-0005 pinned by the parent change.

## 🧩 Explicit governing constraints

`DirectReferences` is live, generated, one-level, non-recursive, physical-write-order, repeat-preserving, and reflection-free. It includes nested aggregate/select references and excludes null, `DERIVE`, and `INVERSE`.

<!-- work-item: scope -->
## 🎯 Outcome, scope, and non-goals

| Expected affected area or exact public contract | Authorized outcome | Explicit non-goal |
| --- | --- | --- |
| Generated entity `IEnumerable<Entity> DirectReferences`, generator metadata, XML docs, graph tests | Physical direct occurrences re-enumerate current mutable state and compose correctly with `ExchangeStructure.Add` | Recursive traversal inside the property, cached snapshots, inverse/derived navigation, or reflection |

## 🔍 Current behavior and impact boundary

The runtime Add contract can consume one-level references, but generated entities do not yet provide them. Ordinary typed relationship properties remain unchanged.

<!-- work-item: start-conditions -->
## 🚧 Start conditions and blockers

| Start condition or blocker | Evidence or owner | Effect if unmet |
| --- | --- | --- |
| SBRT-006 and generated entity/value/aggregate contracts complete | SBRT-006, SBRT-010, SBRT-011, SBRT-012 evidence | Physical occurrence enumeration cannot be proven end-to-end |

<!-- work-item: behavior-cases -->
## 🧪 Behavior cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-11A | A root entity has physical references through direct attributes, nested aggregates/selects, shared objects, repeated occurrences, or cycles, including references added after the root was registered | Inspect `DirectReferences`, then add or re-add the root to an exchange structure | `DirectReferences` yields only one live level in physical write order without reflection and preserves repeated occurrences; every Add re-enumerates the graph, preserves existing registrations, registers each newly reachable object once by CLR reference identity, terminates cycles, and assigns names only in that exchange structure |

<!-- work-item: delivery-constraints -->
## 🛡️ Delivery constraints

| Observable boundary or governing constraint | Required result | Compatibility or invariant |
| --- | --- | --- |
| Occurrence view | Exact one-level physical order and repetitions; edits are visible on re-enumeration | No recursive/cached/unique projection and no inverse/derived/null values |
| Registration composition | Add traverses generated occurrences transitively with CLR-reference cycle safety | Entity remains unaware of structure/name and mutation does not validate |

<!-- work-item: verification-plan -->
## ✅ Verification plan

| Behavior case | Proof intent and appropriate level | Observable assertion | Stable command or bounded manual procedure, if known |
| --- | --- | --- | --- |
| BC-11A | Generated graph integration | Direct/nested/select references appear in physical order with repeats; edits change enumeration; Add registers a cyclic/shared graph once, and re-Add registers newly reachable objects without renaming existing ones | Fast TUnit generator/runtime tests with reflection prohibition audit |

<!-- work-item: definition-of-done -->
## 🏁 Definition of done

| Criterion | Required evidence |
| --- | --- |
| Direct view | Generated fixtures prove every inclusion/exclusion/order/live-state rule |
| Add integration | Cycle/share/repeat/re-Add tests prove transitive registration, preserved existing membership, and per-structure naming |

## ⏱️ Workload estimate

- Planning range: 0.4–0.7 person-months.
- Confidence: Medium.
- Assumptions and excluded work: Physical attribute order is available in bound metadata; reader reference resolution is SBRT-019.

<!-- work-item: completion-evidence -->
## 📋 Completion evidence

| Evidence | Required record |
| --- | --- |
| Delivery-boundary check | Starting SHA and generator/test/XML artifacts |
| Behavior-case proof | BC-11A command/results and reflection/API audit |
| Migration and documentation | `DirectReferences` exact one-level XML contract |
| Dependent-item unlock | Physical reference occurrence contract for SBRT-019 and SBRT-022 |

## ⚠️ Risks and open questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Convenience deduplication loses repeated occurrences | Incorrect physical writing | Repetition assertions are mandatory completion evidence |
