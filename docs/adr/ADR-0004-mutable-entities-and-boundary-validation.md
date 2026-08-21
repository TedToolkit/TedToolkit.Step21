# ADR-0004: Use mutable generated entities with boundary validation

- Status: Accepted
- Date: 2026-08-21
- Decision owner: repository maintainer
- Decision scope: generated EXPRESS entity shape, identity and relationship ownership, mutation validity, graph registration, hydration, and validation timing
- Applicable product intent: [`../product/README.md`](../product/README.md)
- Applicable principles: [`AP-001`, `AP-002`, and `AP-003`](../principles/architecture.md); [`EP-002` and `EP-003`](../principles/engineering.md)
- Supersedes: ADR-0001
- Superseded by: None

## 📝 Clarification and decision log

| ID | Question and why it mattered | Recommended answer | User decision and source | Affected sections | Status |
| --- | --- | --- | --- | --- | --- |
| ADR-C-01 | Whether generated entity implementations remain immutable records | Use mutable classes with CLR reference identity while retaining generated interfaces for EXPRESS inheritance | The maintainer selected mutable classes on 2026-08-21 | Entity shape, identity, equality, and hydration | Resolved |
| ADR-C-02 | When mutable objects and aggregates are automatically schema-validated | Do not validate mutation; validate explicitly and automatically only at successful Part 21 read and before Part 21 write | The maintainer repeatedly confirmed that intermediate editing must remain unchecked on 2026-08-21 | Mutation, read/write boundaries, and exceptions | Resolved |
| ADR-C-03 | Whether an entity owns its Part 21 name or containing exchange structure | No; `ExchangeStructure` exclusively owns the bidirectional name/object index | The maintainer required entities to contain neither `InstanceName` nor `ExchangeStructure` on 2026-08-21 | Identity ownership, portability, and writing | Resolved |
| ADR-C-04 | How graph registration finds referenced entities without reflection | Generate a live one-level `DirectReferences` sequence and let `ExchangeStructure.Add` compose it transitively with reference-identity cycle detection | The maintainer approved the exact one-level reflection-free contract on 2026-08-21 | Generated entity infrastructure and registration | Resolved |
| ADR-C-05 | Which validation engine executes generated rules | Generate validation directly from bound EXPRESS IR behind the minimal Step21 result contract | The maintainer cancelled FluentValidation and approved direct AOT-ready generation on 2026-08-21 | Validation execution and dependencies | Resolved |

## 📌 Decision at a glance

Generate mutable reference-identity entity classes behind EXPRESS inheritance interfaces, keep Part 21 identity in `ExchangeStructure`, allow temporarily invalid editing, and enforce complete schema validity only through explicit validation and Part 21 read/write boundaries.

## 🧭 Context and decision question

ADR-0001 selected immutable records and required complete public state. Subsequent editing requirements established that consumers must freely change properties and EXPRESS aggregates through intermediate states that may temporarily violate mandatory values, bounds, uniqueness, references, or schema rules. Entity objects must also move between exchange structures without carrying serialization identity or container lifetime. This ADR replaces the entity-shape and validity-lifecycle decision while retaining direct entity references, two-phase parse binding, interface-based EXPRESS inheritance, and model-owned occurrence names.

## 🎯 Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Public domain concepts and relationships trace only to ISO 10303-21 or supplied EXPRESS declarations | AP-003 | Must |
| Hard constraint | Mutations permit temporary schema-invalid intermediate states | Maintainer direction, 2026-08-21 | Must |
| Hard constraint | Automatic schema validation occurs after read/bind and before write, not during editing | Maintainer direction, 2026-08-21 | Must |
| Hard constraint | Entity-valued properties remain direct generated entity interfaces and support cycles | Maintainer direction and ISO 10303-21 clause 11 | Must |
| Hard constraint | Part 21 occurrence names remain exchange-structure-owned and globally unique within that structure | ISO 10303-21 clause 11.2 and maintainer direction | Must |
| Hard constraint | Runtime and generated execution remain Native AOT-ready without reflection-based schema discovery | EP-003 and ADR-0003 | Must |
| Driver | One object graph can be registered in another exchange structure with independent names | Maintainer portability requirement | High |

## 🔎 Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Immutable generated records with complete construction | ADR-0001; high confidence for immutable publication | No | Cannot support the approved unchecked editing lifecycle and value equality is unsuitable for model identity | Rejected |
| Mutable classes with validation in setters and aggregate operations | Conventional guard approach; high confidence | No | Prevents valid multi-step edits and contradicts the approved boundary-only timing | Rejected |
| Mutable reference-identity classes with explicit/read/write validation | Directly represents approved editing and model-identity requirements; high confidence | Yes | Consumers may observe temporarily invalid state and must use explicit or boundary validation | Selected |

## ✅ Decision

Every EXPRESS entity generates an interface and a mutable class. Interfaces exclusively represent EXPRESS inheritance. Each generated class derives directly from runtime `Entity`, implements its own generated interface, and uses CLR reference identity; generated classes do not use record value equality or another generated entity class as a base.

Explicit attributes are mutable. Mandatory members are non-nullable and required in ordinary construction; `OPTIONAL` members are nullable according to EXPRESS. Setters and mutable `ExpressArray<T>`, `ExpressList<T>`, `ExpressBag<T>`, and `ExpressSet<T>` operations do not run schema validation. A consumer may therefore create temporary violations during a multi-step edit. Parser-only two-phase hydration may also use controlled incomplete state, but a schema-bound read publishes no typed graph until binding and complete validation succeed.

Entity-valued properties expose direct generated entity interfaces. `Entity` exposes neither an occurrence name nor an exchange-structure reference. `ExchangeStructure` exclusively owns `EntityInstanceName -> Entity` and reference-identity `Entity -> EntityInstanceName` mappings. Explicit name conflicts are rejected by registration because a bijective identity index cannot represent them; absence of a name permits allocation. This identity operation is not general schema validation.

Every generated entity supplies a live, non-recursive `DirectReferences` sequence of physical entity-reference occurrences in write order. It includes nested aggregate/select references and repeated occurrences, excludes null, `DERIVE`, and `INVERSE`, and uses generated code rather than reflection. `ExchangeStructure.Add(root)` composes the one-level relation transitively with a CLR-reference-identity visited set so cycles terminate and shared entities register once.

`ExchangeStructure.Validate()` is side-effect-free and returns the aggregate Step21 `ValidationResult` selected by ADR-0003. Part 21 read invokes complete validation before returning a typed graph; Part 21 write invokes it before producing output. Invalid boundaries throw their stage-specific aggregate exceptions. Property assignment, aggregate mutation, graph registration, replacement, and removal do not automatically invoke schema validation.

## 💡 Why this decision now

Mutable editing, portable entity graphs, and boundary-only validation change equality, construction, ownership, and failure timing across every generated schema. Preserving ADR-0001's immutable-record assumptions would make the architecture internally contradictory. The selected direction retains the ISO-visible relationships while treating editing, binding, validation, and identity indexes as explicitly named implementation infrastructure.

## 🔗 Evidence and links

- [ISO 10303-21:2016 official catalogue](https://www.iso.org/standard/63141.html)
- [ISO 10303-21 Edition 3 final text](https://www.steptools.com/stds/step/IS_final_p21e3.html)
- [ISO 10303-11:2004 official catalogue](https://www.iso.org/standard/38047.html)
- [`ADR-0003`](ADR-0003-aot-ready-schema-validation.md)
- [`schema-bound-round-trip.md`](../architecture/schema-bound-round-trip.md)

## ⚖️ Consequences and accepted trade-offs

- Ordinary consumers can edit complex graphs without ordering mutations around transient validity.
- Entity identity is unambiguous reference identity, while each exchange structure independently assigns serialization names.
- Temporarily invalid objects are an intentional public editing state; callers requiring feedback invoke `Validate()`.
- Read publication remains atomic and write emits no partial output after validation failure.
- Generated code carries direct reference enumeration and validation behavior, increasing generator responsibility while avoiding reflection and runtime discovery.
- Collection APIs retain invalid candidates, such as duplicate SET elements, so aggregate validation can report rather than silently discard them.

## 🛠️ Downstream delivery constraints

- Generated entity implementations are mutable classes with reference identity; interfaces alone encode EXPRESS inheritance.
- Generated domain members and relationships add no concept beyond ISO 10303-21 or the supplied EXPRESS declarations.
- Entities contain no exchange-structure reference or occurrence-name property.
- Direct entity properties and cycles require no wrapper, ID, proxy, or resolver call.
- `DirectReferences` is generated, live, one-level, physical, deterministic, and reflection-free.
- Registration enforces only identity-index operations needed to allocate or claim occurrence names; it does not become a schema-validation boundary.
- Setters and aggregate mutations do not automatically validate.
- Explicit validation aggregates without throwing; read/write boundaries validate and throw stage-specific exceptions with the complete result.
- Generated/runtime paths satisfy the repository Native AOT proof gate.

## 🔄 Exit requirements

A replacement must preserve ISO/EXPRESS-only domain semantics, unchecked multi-step editing, direct entity navigation, reference identity, exchange-structure-owned occurrence names, cycle-safe graph portability, aggregate failure reporting, atomic validated read publication, and pre-output validated writing.

## 📅 Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Reassess public mutation shape | repository maintainer | A valid EXPRESS construct cannot be edited without adding non-standard domain state | Open |
| Reassess boundary-only validation | repository maintainer | A required external conformance boundary cannot tolerate temporarily invalid in-memory state | Open |
