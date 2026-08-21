# Generated EXPRESS entity hierarchy

Each valid EXPRESS entity produces a public `I<EntityPascalCase>` interface and public `<EntityPascalCase>` class in `TedToolkit.Step21.Generated.<SchemaPascalCase>`. Generated interfaces inherit every direct EXPRESS supertype interface. Generated classes derive directly from `TedToolkit.Step21.Entity`, implement only their own entity interface, and never select a generated class as a primary base.

Concrete entity classes are sealed; abstract EXPRESS entities produce abstract classes. The classes retain ordinary CLR reference identity. Their bounded `ToString()` result is `<source-schema-name>.<source-entity-name>` and does not traverse relationships or claim ISO 10303-21 serialization.

## Mutable attribute projection

This delivery slice projects explicit attributes whose resolved type is an entity:

- interface properties are getter-only;
- class properties are public get/set properties and run no validation;
- inherited storage is flattened into each generated class once, including diamonds;
- a renamed redeclaration exposes its inherited and renamed interface properties over one physical slot;
- mandatory attributes are required constructor parameters and reject ordinary nullable calls at compile time;
- `OPTIONAL` attributes use nullable property types and accept `null`; and
- abstract-class constructors are protected, while concrete-class constructors are public.

Scalar, defined, SELECT, and aggregate attributes are projected by the generated-value stages. Schema descriptors, hydration, rule execution, and physical writing remain later boundaries and do not reinterpret the retained EXPRESS syntax.

Because generated files are treated specially by Roslyn's nullable analysis, the structural output pairs `T`/`T?` syntax with standard `System.Diagnostics.CodeAnalysis` nullability attributes. This preserves mandatory/OPTIONAL behavior for consuming C# compilations without setter guards.

## Direct references and naming

`Entity.DirectReferences` currently enumerates the live values of projected direct entity properties in flattened physical order. It removes null and non-`Entity` interface implementations, preserves repeated occurrences, and never recurses. SBRT-014 extends this same contract through select and aggregate values after those generated value shapes exist.

Schema, entity, and member identifiers map deterministically to PascalCase. If distinct EXPRESS names would collide in the generated C# namespace or member surface, `STEP21EXP004` is reported at the source name and every output for that schema is withheld. A redeclaration that changes its entity target type cannot safely implement both CLR interface properties in this slice, so it reports `STEP21EXP005` and withholds the schema instead of leaking a C# compiler error. Invalid generation state propagates through importing schemas; independent valid schemas remain eligible. Cross-schema inheritance and entity properties use the target schema's fully qualified generated namespace.

All output is composed through RoslynHelper structural nodes. No text templates, raw source fragments, `SyntaxFactory`, reflection discovery, or dynamic-code path participates in entity generation.
