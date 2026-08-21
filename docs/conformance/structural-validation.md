# Structural validation and XML traceability

`ExchangeStructure.Validate()` inspects the current mutable graph without changing it. Invalidity is returned as one `ValidationResult`; it is not thrown. Property assignment, aggregate mutation, Add, Remove, and replacement expressed as Remove followed by Add remain unchecked so consumers can represent an intermediate invalid edit and request feedback explicitly.

## Deterministic graph dispatch

The runtime snapshots `DataSections`, traverses its registered entities once by CLR reference identity, and constructs ordered path/entity pairs for each section. A path begins with the current section index and occurrence name, for example `DataSections[0].#3`. The runtime reports null or repeated section entries, missing descriptors, detached registrations, null direct-reference occurrences, and references to unregistered entities with stable `P21.STRUCTURE.*` codes.

Each generated schema descriptor is an ordinary sealed singleton class. The runtime passes it only the ordered pairs governed by that section's exact nominal `SchemaName`; the generated override dispatches directly to concrete generated entity classes without reflection, discovery, an ambient registry, or a public validation context. Results preserve data-section, registration, attribute, nested aggregate, and rule order.

## Generated structural rules

The generator derives executable checks and their XML documentation from the same bound EXPRESS projection. This stage checks:

- mandatory and `OPTIONAL` presence;
- concrete generated entity assignability, including entity alternatives inside SELECT values;
- literal `ARRAY`, `LIST`, `BAG`, and `SET` category, bounds, required slots, and uniqueness;
- nested aggregate element presence and entity assignability;
- nominal defined-type payload presence; and
- closed enumeration values and SELECT alternatives.

Every failure contains a complete deterministic path, a stable code, a caller-facing message, and the originating EXPRESS file leaf name plus 1-based line and column. Source file roots do not enter generated output, so relocating the same additional file does not alter emitted C#.

When a bound is symbolic, this stage still checks every independently known literal bound, metadata modifier, required slot, uniqueness condition, and nested element rule; only evaluation of the symbolic expression is deferred. General EXPRESS expressions, named `WHERE`/`UNIQUE` rules, derived evaluation, population rules, and subtype constraints are retained in the bound IR but are not claimed here. SBRT-016 and SBRT-017 own that executable closure.

## XML contract

Every generated attribute property has a summary, a `<value>` statement that explains its mutable candidate semantics, and remarks containing the source schema, normalized declaration, normalized requirement, and validation timing. Its constraint table uses the exact IDs emitted as `ValidationFailure.Code`. Descriptor remarks document the schema-level generated-entity assignability rule. Entity and defined-value type remarks identify their source schema and declaration.

The documentation is deterministic and well-formed XML. It does not imply that construction or mutation validates a value, and it distinguishes C# representation deliberately: mutable reference-identity entities and singleton descriptors are ordinary classes; immutable SELECT discriminated values use sealed record reference types for structural value equality.
