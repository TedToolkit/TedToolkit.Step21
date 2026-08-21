# EXPRESS generator host boundary

The `TedToolkit.Step21` package carries a C# incremental source generator under `analyzers/dotnet/cs`. The generator treats all `.exp` MSBuild `AdditionalFiles` as one closed EXPRESS schema set, delegates parsing and binding to the internal compiler, and never opens the logical paths supplied by Roslyn.

## Current emitted contract

For each independently valid schema, the generator emits one path-independent source with:

- hint name `ExpressSchema_<UPPERCASE_SCHEMA_NAME>.g.cs`;
- fixed namespace `TedToolkit.Step21.Generated`;
- internal sealed marker `ExpressSchema_<source_schema_name>`; and
- internal constant `SchemaName` preserving the source spelling.

This marker is a compiled proof of the generator/package boundary, not a public schema facade. Schema-bound entity, value, descriptor, mapping, and validation output belongs to later work items.

All source structure is composed with `TedToolkit.RoslynHelper` file, namespace, type, field, literal, and final emission primitives. The generator uses no text template, raw source fragment, or `SyntaxFactory` fallback.

## Determinism and failures

Equivalent schema text produces identical hint names and generated C# regardless of AdditionalFiles order or logical machine path. Diagnostics retain the original logical path and 1-based EXPRESS source evidence:

| Diagnostic | Meaning |
| --- | --- |
| `STEP21EXP001` | EXPRESS syntax is invalid. |
| `STEP21EXP002` | Closed-set EXPRESS binding is invalid. |
| `STEP21EXP003` | Roslyn could not read an EXPRESS AdditionalFile. |

Invalid schemas emit no marker. Valid schemas that are independent of another invalid schema remain eligible for emission according to the closed-set compiler contract.

## Package boundary

`TedToolkit.RoslynHelper`, `ZString`, and `System.Memory` are implementation dependencies colocated with the analyzer. They are not package dependencies or runtime assets of a consumer. Generated output references only framework types, and the runtime graph exposes no JSON/XML serialization dependency or adapter contract.

The packed-consumer integration test builds a real project from a locally produced `.nupkg`, compiles a non-IFC schema marker, inspects generated output, audits analyzer files, and confirms the consumer runtime assets graph excludes analyzer-only and JSON/XML dependencies.
