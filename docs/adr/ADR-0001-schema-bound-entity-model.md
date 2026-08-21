# ADR-0001: Represent schema-bound relationships as direct entity references

- Status: Superseded
- Date: 2026-08-21
- Decision owner: repository maintainer
- Decision scope: generated EXPRESS entity APIs, parse-time construction, runtime identity ownership, and ISO 10303-21 write-back
- Applicable product intent: [`../product/README.md`](../product/README.md)
- Applicable principles: [`AP-001`](../principles/architecture.md), [`AP-002`](../principles/architecture.md), [`AP-003`](../principles/architecture.md), and [`EP-002`](../principles/engineering.md)
- Supersedes: None
- Superseded by: ADR-0004

## 📝 Clarification and decision log

| ID | Question and why it mattered | Recommended answer | User decision and source | Affected sections | Status |
| --- | --- | --- | --- | --- | --- |
| ADR-C-01 | Whether generated entities should be interfaces or ordinary mutable classes | Use interfaces and records, sealing leaves | The maintainer selected interfaces/records and necessary sealing on 2026-08-21 | Decision and consequences | Resolved |
| ADR-C-02 | Whether an entity-valued property stores a wrapper or an entity object | Expose the generated entity interface directly | The maintainer rejected `EntityRef<TEntity>` and selected ordinary C# references on 2026-08-21 | Decision and consequences | Resolved |
| ADR-C-03 | How forward and cyclic references are created | Allocate/register all entities, then assign actual object references in a second phase | The maintainer selected two-step reference processing on 2026-08-21 | Hydration and model ownership | Resolved |
| ADR-C-04 | Whether incomplete hydration may be visible to ordinary callers | Restrict incompleteness to parser internals | The maintainer required on 2026-08-21 that users never observe a temporarily incomplete entity | Public construction and nullability | Resolved |
| ADR-C-05 | How to represent EXPRESS inheritance | Generate an interface and a record for each entity; interfaces alone express schema inheritance; records inherit only `Entity` | The maintainer confirmed the uniform interface-inheritance projection on 2026-08-21 | Decision and trade-offs | Resolved |
| ADR-C-06 | Whether the API may introduce convenience domain concepts absent from ISO/EXPRESS | No; implementation infrastructure must remain distinguishable from the generated domain surface | The maintainer established ISO semantics first and C# idioms second on 2026-08-21 | Decision, naming, and downstream constraints | Resolved |
| ADR-C-07 | Whether an entity's `ToString()` writes an entity-instance record | No; it is a non-recursive diagnostic representation because instance names and physical mapping require exchange-structure context | The maintainer accepted the separation on 2026-08-21 | Entity base contract and writer | Resolved |

## 📌 Decision at a glance

Generate entity-valued properties as direct generated entity interfaces, build parsed graphs through model-owned two-phase hydration, and retain ISO 10303-21 entity instance names exclusively in the raw/model identity maps needed for write-back.

## 🧭 Context and decision question

ISO 10303-21 stores entity identity and entity-valued attributes as occurrence names such as `#42`; definitions may follow their uses, and entity graphs may be cyclic. In normal C# use, however, consumers want an entity property to return the related entity rather than a reference wrapper or identifier. EXPRESS also permits multiple entity inheritance, while C# record classes permit one base class.

The decision is how to expose ordinary direct C# references without losing parse-time forward-reference support, ISO instance names, type validation, or the ability to write the model back.

## 🎯 Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Entity-valued generated properties expose entities, not Ref/ID wrappers | Maintainer direction, 2026-08-21 | Must |
| Hard constraint | Preserve ISO entity instance names for write-back | ISO 10303-21 clauses 6.4.4.3, 11, and 12.2.4 | Must |
| Hard constraint | Support references before definitions and cyclic graphs | ISO 10303-21 clause 11 permits reference before definition | Must |
| Hard constraint | Represent EXPRESS multiple inheritance without false assignability | ISO 10303-11 entity model and current EXPRESS grammar | Must |
| Hard constraint | Generated objects use interfaces or records, sealing where valid | Maintainer direction, 2026-08-21 | Must |
| Driver | Ordinary navigation is a property access with no resolution API | Maintainer ergonomics requirement, 2026-08-21 | High |
| Driver | Invalid incomplete state must not leak from parsing | Maintainer direction and ADR approval, 2026-08-21 | High |

## 🔎 Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Numeric IDs or `EntityRef<TEntity>` properties | Technically robust; high confidence | No | Violates the selected public ergonomics | Rejected |
| Lazy/proxy entity objects | Supports cycles; high confidence | Partial | Hides model lifetime and can make concrete record type tests misleading | Rejected |
| Eager one-pass direct records | Ordinary C# shape; high confidence | No | Cannot construct all valid forward/cyclic immutable graphs | Rejected |
| Two-phase direct record hydration | Standard identity index plus ordinary references; high confidence | Yes | Requires controlled temporary incompleteness inside the binder | Selected |

## ✅ Decision

An entity-valued explicit attribute is generated as the corresponding entity interface, for example `ILoop Bound`. Optional entity attributes use the nullable interface form after hydration. The public shape contains no `EntityRef<TEntity>` and need not expose an entity instance identifier.

Parsing and binding proceed as follows:

1. Parse the raw exchange structure, retaining occurrence names and raw parameters.
2. Allocate every generated record and register `EntityInstanceName -> object` plus a reference-identity reverse map in `ExchangeStructure`.
3. Bind scalar and aggregate values and assign actual target objects to entity-valued backing members through generated non-public hydration APIs.
4. Validate mandatory completeness, target-interface assignability, optionality, model membership, and schema mappings.
5. Publish the typed model only after hydration succeeds according to the result policy.

ISO 10303-21 does not define .NET attributes such as `P21ParameterAttribute` or `P21EntityReferenceAttribute`. The first public contract does not require generated mapping attributes. If a later consumer requirement introduces them, they are library-defined, non-normative descriptive metadata; generated schema descriptors remain authoritative for executable binding/writing, and writers do not discover the schema by reflection.

Parser-only private hydration machinery may temporarily hold missing backing values. It is inaccessible through ordinary construction and is never returned. Public construction requires all mandatory properties immediately, while OPTIONAL schema attributes alone may be represented as nullable after completion.

Every EXPRESS entity has one interface and one record. Its interface extends exactly the interfaces of its EXPRESS supertypes. Its record inherits directly from the runtime `abstract record Entity` and implements its own interface; it never inherits another generated entity record. Transitive supertype interfaces need not be repeated on the record. Abstract EXPRESS entities produce abstract records, while every instantiable generated entity record can be sealed. Inherited storage is projected once, without selecting a primary supertype.

Public generated entity names, relationships, optionality, inheritance, and value distinctions trace to the selected EXPRESS schema. `ExchangeStructure`, `EntityInstanceName`, sections, instances, and parameters trace to ISO 10303-21. Parser, binder, hydration, diagnostic, and writer contracts are implementation infrastructure and are named and documented as such; they do not become new generated domain concepts or marker supertypes.

Generated entity records override the default recursive record display behavior with a bounded diagnostic representation. It does not follow entity-valued properties or serialize an entity instance. `#123=ENTITY(...);` and complete exchange-structure output require the owning `ExchangeStructure`, generated physical mapping, and a diagnostic-capable writer.

## 💡 Why this decision now

The public relationship shape affects every generated property and the construction/writing pipeline. Two-phase hydration satisfies the requested natural C# API while preserving ISO instance names in the model, where they are needed for input binding and output serialization rather than ordinary domain navigation.

## 🔗 Evidence and links

- [ISO 10303-21:2016 official catalogue](https://www.iso.org/standard/63141.html)
- [ISO 10303-11:2004 official catalogue](https://www.iso.org/standard/38047.html)
- [Public final-text mirror: entity instance names and entity mapping](https://www.steptools.com/stds/step/IS_final_p21e3.html)
- [`schema-bound-round-trip.md`](../architecture/schema-bound-round-trip.md)

## ⚖️ Consequences and accepted trade-offs

- Consumers navigate `entity.RelatedEntity` as ordinary C# references.
- Forward and cyclic references require no proxy because all objects exist before reference assignment.
- The binder temporarily owns incomplete records, but no parser result, public constructor, setter, or builder exposes them.
- `ExchangeStructure` must use reference identity, not record value equality, for the reverse object-to-instance-name map.
- Writer references are obtained by looking up the target object in the model; a target from another model is diagnosed.
- Assigning reference properties after allocation changes record equality/hash inputs. Incomplete records must not be used as value-equality dictionary keys or published before hydration completes.
- EXPRESS inheritance is represented uniformly through interfaces, independent of whether the schema uses single or multiple inheritance.
- Generated metadata and hydration code carry more responsibility than direct constructor-only construction.

## 🛠️ Downstream delivery constraints

- No public generated entity property uses `EntityRef<TEntity>` or a numeric instance identifier for ordinary relationships.
- The raw/runtime model retains a standard-compliant `EntityInstanceName` representation and a bidirectional identity map.
- Hydration resolves raw occurrence names to actual generated objects and validates interface assignability before publication.
- Public construction requires all mandatory properties and cannot create a temporarily incomplete entity.
- Generated records do not retain lazy proxies, service providers, resolver delegates, or external serialization state.
- Writers map direct target objects back to occurrence names through model reference identity and reject foreign/unregistered targets.
- Interfaces alone determine EXPRESS assignability; generated records inherit only `Entity`, implement their own generated interface, and contain inherited storage exactly once.
- No public mapping attribute is required initially; any future attribute is documented as non-normative and generated schema descriptors control execution without reflection dependency.
- No library-defined marker interface, reference wrapper, or convenience abstraction is added to the generated domain type hierarchy unless it represents a concept required by ISO 10303-21 or the selected EXPRESS schema.
- `ToString()` is diagnostic-only and cannot be used as a conforming ISO 10303-21 serialization path.

## 🔄 Exit requirements

A replacement must preserve direct entity property ergonomics, serializable model-owned identity, forward/cyclic reference support, target validation, multiple-inheritance assignability, and deterministic ISO 10303-21 write-back.

## 📅 Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Reassess if a valid EXPRESS evaluated-set mapping cannot be represented | repository maintainer | First failing normative schema case | Open |
