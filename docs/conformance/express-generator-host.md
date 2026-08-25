# EXPRESS generator host boundary

The `TedToolkit.Step21` package carries a C# incremental source generator under `analyzers/dotnet/cs`. The generator treats all `.exp` MSBuild `AdditionalFiles` as one closed EXPRESS schema set, delegates parsing and binding to the internal compiler, and never opens the logical paths supplied by Roslyn.

## Current emitted contract

For each independently valid schema, the generator retains one path-independent descriptor source with:

- hint name `ExpressSchema_<UPPERCASE_SCHEMA_NAME>.g.cs`;
- fixed namespace `TedToolkit.Step21.Generated.<SchemaPascalCase>`;
- public sealed class `SchemaDescriptor` with a private constructor and static `Instance`; and
- overridden `SchemaName Name` preserving the nominal source name.

The descriptor is generated mapping infrastructure, not an ISO domain value or public schema facade. Its exact boundary is documented by [generated schema descriptors](generated-schema-descriptors.md). Each entity additionally emits `ExpressEntity_<UPPERCASE_SCHEMA_NAME>_<UPPERCASE_ENTITY_NAME>.g.cs`; its public contract is documented by the [generated entity hierarchy boundary](generated-entity-hierarchy.md). The descriptor directly executes the supported validation-reachable schema-rule closure.

Declarations, members, ordinary locals, assignments, `IF`/`CASE`, returns, and compound statements are composed with `TedToolkit.RoslynHelper` structural primitives. The pinned helper has no general loop, lambda, pattern/presence, or switch-expression object, so reviewed custom fragments are limited to the whole-loop fragment for a general EXPRESS `REPEAT`, necessary lambdas, and necessary pattern/presence/switch expressions. The generator uses no text template, whole-source string emission, or `SyntaxFactory` fallback.

## Determinism and failures

Equivalent schema text produces identical hint names and generated C# regardless of AdditionalFiles order or logical machine path. Diagnostics retain the original logical path and 1-based EXPRESS source evidence:

| Diagnostic | Meaning |
| --- | --- |
| `STEP21EXP001` | EXPRESS syntax is invalid. |
| `STEP21EXP002` | Closed-set EXPRESS binding is invalid. |
| `STEP21EXP003` | Roslyn could not read an EXPRESS AdditionalFile. |
| `STEP21EXP004` | Two EXPRESS names map to the same generated C# schema, type, or member name. |
| `STEP21EXP005` | A valid EXPRESS entity shape cannot safely implement the required C# interface contracts in this delivery slice. |

Invalid schemas emit neither a descriptor nor entity sources. Valid schemas that are independent of another invalid schema remain eligible for emission according to the closed-set compiler contract.

## Package boundary

`TedToolkit.RoslynHelper`, `ZString`, and `System.Memory` are implementation dependencies colocated with the analyzer. They are not package dependencies or runtime assets of a consumer. Generated output references only framework types, and the runtime graph exposes no JSON/XML serialization dependency or adapter contract.

The packed-consumer integration test builds a real project from a locally produced `.nupkg`, compiles a non-IFC schema descriptor and entity class, inspects generated output, audits analyzer files, and confirms the consumer runtime assets graph excludes analyzer-only and JSON/XML dependencies.
