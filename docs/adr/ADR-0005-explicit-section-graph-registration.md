# ADR-0005: Use explicit-section graph registration with mutable boundary-validated entities

- Status: Accepted
- Date: 2026-08-21
- Decision owner: repository maintainer
- Decision scope: generated EXPRESS entity shape, identity and relationship ownership, data-section selection during graph registration, mutation validity, hydration, and validation timing
- Applicable product intent: [`../product/README.md`](../product/README.md)
- Applicable principles: [`AP-001`, `AP-002`, and `AP-003`](../principles/architecture.md); [`EP-002` and `EP-003`](../principles/engineering.md)
- Supersedes: ADR-0004
- Superseded by: None

## 📝 Clarification and decision log

| ID | Question and why it mattered | Recommended answer | User decision and source | Affected sections | Status |
| --- | --- | --- | --- | --- | --- |
| ADR-C-01 | Whether generated entity implementations remain mutable reference-identity classes | Retain ADR-0004's mutable class shape and interface-based EXPRESS inheritance | The maintainer retained mutable classes and unchecked intermediate editing on 2026-08-21 | Entity shape, identity, equality, and hydration | Resolved |
| ADR-C-02 | When mutable objects and aggregates are automatically schema-validated | Validate explicitly and automatically only at successful Part 21 read and before Part 21 write | The maintainer repeatedly confirmed boundary-only automatic validation on 2026-08-21 | Mutation, read/write boundaries, and exceptions | Resolved |
| ADR-C-03 | Whether an entity owns its Part 21 name or containing exchange structure | No; `ExchangeStructure` exclusively owns occurrence names and section membership | The maintainer required entities to contain neither `InstanceName` nor `ExchangeStructure` on 2026-08-21 | Identity ownership, portability, and writing | Resolved |
| ADR-C-04 | How graph registration chooses a data section | Require the caller to supply an owned `DataSection` on every Add operation; provide no section-omitting overload | The maintainer required omission of the section to be a compile-time API error rather than a runtime selection error on 2026-08-21 | Registration API boundary, multi-section editing, and graph membership | Resolved |
| ADR-C-05 | How graph registration finds referenced entities without reflection | Retain a live one-level `DirectReferences` sequence and compose it transitively with reference-identity cycle detection | The maintainer approved the one-level reflection-free contract on 2026-08-21 | Generated entity infrastructure and registration | Resolved |
| ADR-C-06 | Which validation engine executes generated rules | Generate validation directly from bound EXPRESS IR behind the minimal Step21 result contract | The maintainer cancelled FluentValidation and approved direct AOT-ready generation on 2026-08-21 | Validation execution and dependencies | Resolved |

## 📌 Decision at a glance

Generate mutable reference-identity entity classes, keep Part 21 identity and section membership in `ExchangeStructure`, require every graph-registration call to name its `DataSection`, allow temporarily invalid editing, and enforce complete schema validity only through explicit validation and Part 21 read/write boundaries.

## 🧭 Context and decision question

ADR-0004 established mutable entities, structure-owned occurrence names, reflection-free graph discovery, and boundary-only validation, but described graph registration as `ExchangeStructure.Add(root)`. ISO 10303-21 Edition 3 permits multiple data sections governed by different schemas, so a structure cannot infer one generally correct destination section. Selecting a default at runtime would either invent semantics or postpone an omitted argument to a runtime ambiguity. This ADR decides whether section selection is implicit or part of the statically visible registration operation while preserving ADR-0004's remaining drivers.

## 🎯 Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Public domain concepts and relationships trace only to ISO 10303-21 or supplied EXPRESS declarations | AP-003 | Must |
| Hard constraint | One exchange structure may contain multiple data sections with different governing schemas | ISO 10303-21 Edition 3 clause 11.1 and AP-001 | Must |
| Hard constraint | Registration cannot silently invent or infer a default data section | AP-001, AP-003, and maintainer direction | Must |
| Hard constraint | Omitting section selection must be rejected by the C# compiler | Maintainer direction, 2026-08-21 | Must |
| Hard constraint | Entity objects contain neither occurrence names nor an owning structure or section reference | Maintainer direction and EP-002 | Must |
| Hard constraint | Mutations permit temporary schema-invalid intermediate states; automatic schema validation remains at successful read and before write | Maintainer direction, 2026-08-21 | Must |
| Hard constraint | Runtime and generated execution remain Native AOT-ready without reflection-based schema discovery | EP-003 and ADR-0003 | Must |
| Decision driver | Registration remains convenient for cyclic and shared object graphs | Maintainer graph-registration requirement | High |

## 🔎 Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Section-omitting `Add(root)` | ADR-0004; documented previous direction | No | Cannot identify the intended destination in a multi-section structure without an invented default or runtime ambiguity | Rejected |
| Permit section omission only when one section exists | Ordinary overload plus runtime state check; high confidence | No | Source compiles and fails only when structure state changes, contrary to the required compile-time boundary | Rejected |
| Put owning section or structure on each entity | Common active-record ownership pattern; high confidence | No | Couples portable entities to one structure and adds a relationship not supplied by the EXPRESS entity declaration | Rejected |
| Require `DataSection` on every registration operation | The destination is an existing ISO exchange-structure value and C# overload resolution enforces its presence; high confidence | Yes | Callers must retain or select the destination section explicitly | Selected |

## ✅ Decision

Every public graph-registration operation requires an explicit `DataSection` owned by the receiving `ExchangeStructure`. One operation allocates an occurrence name for the supplied root; another claims an explicit occurrence name. No overload omits `DataSection`. Supplying a foreign section is rejected before the structure is mutated. Newly discovered graph members join the selected section; an entity already registered in the structure retains its existing section membership.

Every generated entity remains a mutable reference-identity class behind EXPRESS inheritance interfaces. It contains no occurrence name and no exchange-structure or data-section back-reference. A live, non-recursive `DirectReferences` sequence exposes one level of physical entity-reference occurrences in write order. Registration composes that sequence transitively with a CLR-reference-identity visited set so cycles terminate and shared entities register once.

Setters and mutable EXPRESS aggregate operations do not run schema validation. `ExchangeStructure.Validate()` remains side-effect-free and aggregates all detected failures. Successful Part 21 read validates before publishing a typed graph; Part 21 write validates before producing output and uses dedicated stage failures when invalid.

## 💡 Why this decision now

The previous section-omitting signature became ambiguous once complete Edition 3 multi-section and multi-schema support became mandatory. An explicit `DataSection` is already part of the ISO exchange structure, preserves strong typing, and turns omission into a compile-time error without adding a default-section concept. The remaining ADR-0004 direction continues to satisfy portable graph editing, direct entity navigation, validation aggregation, and Native AOT requirements.

Reconsideration requires either a normative rule that uniquely determines the destination section for every valid structure or a new consumer requirement that outweighs compile-time section selection without adding non-standard entity ownership.

## 🔗 Evidence and links

- [ISO 10303-21:2016 official catalogue](https://www.iso.org/standard/63141.html)
- [ISO 10303-21 Edition 3 final text](https://www.steptools.com/stds/step/IS_final_p21e3.html)
- [ISO 10303-11:2004 official catalogue](https://www.iso.org/standard/38047.html)
- [`ADR-0003`](ADR-0003-aot-ready-schema-validation.md)
- [`ADR-0004`](ADR-0004-mutable-entities-and-boundary-validation.md)
- [`schema-bound-round-trip.md`](../architecture/schema-bound-round-trip.md)

## ⚖️ Consequences and accepted trade-offs

- Source that omits a destination section does not compile; consumers must deliberately select a section.
- Multi-section and multi-schema editing does not depend on a mutable default or collection cardinality.
- A foreign section is detected as an identity/ownership error before mutation, not deferred to general schema validation.
- Entity graphs remain portable between exchange structures because entities carry no structure, section, or occurrence-name ownership.
- Ordinary editing may remain temporarily invalid, while successful read and write retain aggregate validation gates.
- Generated direct-reference enumeration and validation increase generator responsibility while avoiding reflection and runtime discovery.

## 🛠️ Downstream delivery constraints

- Generated entity implementations are mutable classes with reference identity; interfaces alone encode EXPRESS inheritance.
- Entities contain no exchange-structure reference, data-section reference, or occurrence-name property.
- Every public graph-registration operation requires an owned `DataSection`; no section-omitting overload is present.
- Registration rejects a foreign section atomically and preserves section membership for entities already registered in the receiving structure.
- `DirectReferences` is generated, live, one-level, physical, deterministic, and reflection-free.
- Registration enforces identity and section-membership operations but does not become a schema-validation boundary.
- Setters and aggregate mutations do not automatically validate.
- Explicit validation aggregates without throwing; read/write boundaries validate and use dedicated complete-evidence failures.
- Generated/runtime paths satisfy the repository Native AOT proof gate.

## 🔄 Exit requirements

A replacement must preserve ISO/EXPRESS-only domain semantics, explicit and unambiguous section membership, compile-time rejection of omitted section selection, unchecked multi-step editing, direct entity navigation, reference identity, exchange-structure-owned occurrence names, cycle-safe graph portability, aggregate failure reporting, atomic validated read publication, and pre-output validated writing.

## 📅 Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Reassess explicit section selection | repository maintainer | A normative rule uniquely determines the destination section for every supported exchange structure | Open |
| Reassess public mutation shape | repository maintainer | A valid EXPRESS construct cannot be edited without adding non-standard domain state | Open |
| Reassess boundary-only validation | repository maintainer | A required external conformance boundary cannot tolerate temporarily invalid in-memory state | Open |
