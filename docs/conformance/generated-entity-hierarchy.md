# Generated EXPRESS entity hierarchy

Each valid EXPRESS entity produces a public `I<EntityPascalCase>` interface and public `<EntityPascalCase>` class in `TedToolkit.Step21.Generated.<SchemaPascalCase>`. Generated interfaces inherit every direct EXPRESS supertype interface. Generated classes derive directly from `TedToolkit.Step21.Entity`, implement only their own entity interface, and never select a generated class as a primary base.

Concrete entity classes are sealed; abstract EXPRESS entities produce abstract classes. The classes retain ordinary CLR reference identity. Their bounded `ToString()` result is `<source-schema-name>.<source-entity-name>` and does not traverse relationships or claim ISO 10303-21 serialization.

## Mutable attribute projection

Generated entities project every supported explicit entity, scalar, defined, SELECT, and aggregate attribute:

- interface properties are getter-only;
- class properties are public get/set properties and run no validation;
- inherited storage is flattened into each generated class once, including diamonds;
- a renamed redeclaration exposes its inherited and renamed interface properties over one physical slot;
- a supported explicit specialization stores the unique most-specific domain once and implements inherited
  interfaces with statically generated, lossless getter projections;
- mandatory attributes are required constructor parameters and reject ordinary nullable calls at compile time;
- `OPTIONAL` attributes use nullable property types and accept `null`; and
- abstract-class constructors are protected, while concrete-class constructors are public.

Schema descriptors hydrate and project supported simple and flat-`ANDOR` complex physical mappings, execute the supported validation-reachable rule closure, and project canonical physical records for writing without reinterpreting the retained EXPRESS syntax.

Because generated files are treated specially by Roslyn's nullable analysis, the structural output pairs `T`/`T?` syntax with standard `System.Diagnostics.CodeAnalysis` nullability attributes. This preserves mandatory/OPTIONAL behavior for consuming C# compilations without setter guards.

## Direct references and naming

`Entity.DirectReferences` is a deferred, live, one-level physical occurrence view. Re-enumerating even the same returned `IEnumerable<Entity>` reads the current attribute values and current aggregate contents. It recursively unwraps nominal defined values, the selected SELECT alternative, and nested `ARRAY`/`LIST`/`BAG`/`SET` containers, but it never follows a referenced entity's own `DirectReferences`. Physical attribute/container order and repeated occurrences are preserved; unset optional ARRAY slots, nulls, non-`Entity` interface implementations, `DERIVE`, and `INVERSE` are excluded without reflection or deduplication.

`ExchangeStructure.Add` consumes that one-level view transitively. Its structure-owned reference-identity traversal terminates cycles, registers a shared object once, preserves existing names, and re-enumerates a root on every Add so newly reachable objects receive names without renaming prior registrations. Generated entities remain unaware of their structure, section, or occurrence name.

Schema, entity, and member identifiers map deterministically to PascalCase. If distinct EXPRESS names would collide in the generated C# namespace or member surface, `STEP21EXP004` is reported at the source name and every output for that schema is withheld. Explicit redeclarations support direct entity subtype domains, closed entity-valued SELECT narrowing, exact `NUMBER` to `INTEGER`/`REAL`, same-kind entity aggregate narrowing with identical metadata, same-kind aggregate narrowing from entity or closed entity-valued SELECT elements to a covered closed SELECT, and `OPTIONAL` to required tightening. Direct entity aggregate covariance preserves the concrete aggregate reference. SELECT-valued aggregate projection instead exposes a live kind-specific read-only adapter: it preserves bounds, flags, ARRAY slot state, order, multiplicity, selected leaf entity identity, and mutation visibility, but does not promise adapter or value-wrapper reference identity. If both inherited and most-specific aggregate attributes are `OPTIONAL`, an unset storage projects as `null` rather than constructing an adapter; a later non-null assignment is projected live. Numeric projections preserve the exact alternative without binary floating point. ISO-invalid redeclarations report source-located `STEP21EXP002` with `EXPRESS-BIND-INVALID-REDECLARATION`; legal-but-unimplemented or incomparable mappings report `STEP21EXP005`. Either result withholds the schema atomically. Invalid generation state propagates through importing schemas; independent valid schemas remain eligible. Cross-schema inheritance and entity properties use the target schema's fully qualified generated namespace.

Declarations, members, and ordinary statements are composed through RoslynHelper structural nodes. Reviewed custom fragments are limited to the whole-loop fragment for general EXPRESS `REPEAT`, necessary lambdas, and necessary pattern/presence/switch expressions. No text template, whole-source string emission, `SyntaxFactory`, reflection discovery, or dynamic-code path participates in entity generation.
