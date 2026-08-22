# Generated schema descriptors

Every valid supplied EXPRESS schema emits one reflection-free descriptor in `TedToolkit.Step21.Generated.<SchemaPascalCase>`. The descriptor is an ordinary public sealed `class`, not a `record`, because it represents singleton behavior and identity rather than a structurally comparable ISO value. Its private constructor and public static `Instance` property expose the only instance; `Name` preserves the nominal schema name.

The runtime `SchemaDescriptor` base is an abstract `class`. Its public surface contains only `SchemaName Name`; runtime-internal non-virtual dispatch forwards to protected allocation, hydration, validation, capability-diagnostic, and projection hooks. The hooks exchange only `Entity`, `ExchangeStructure`, diagnostics/results, ordered BCL component-name/`ParameterValue` lists, and an ordered BCL path/entity list for validation. `ExchangeStructure` derives that validation list from its private registration index, so generated consumer-assembly overrides need neither internal access nor a public registry. The hooks expose no parser syntax, context, resolver, writer callback, nested public type, reflection, or dynamic dispatch.

## Simple physical mapping

For inherited-free entities whose physical attributes are supported by this stage, the generated descriptor:

- allocates a generated entity for one exact physical entity name;
- requires one exact component and the exact physical parameter count;
- hydrates and projects scalars without narrowing `BigInteger`, `RealValue`, or `NumberValue`;
- preserves nominal defined-type wrappers, closed/extensible enumerations, SELECT typed alternatives, and resolved entity identity;
- maps an absent outer `OPTIONAL` attribute only from/to `$`;
- rejects `*` for an explicit attribute with `P21-BIND-PARAMETER`; and
- constructs literal-bounded `ARRAY`, `LIST`, `BAG`, and `SET` candidates with their declared bounds and `OPTIONAL`/`UNIQUE` metadata, preserving unset optional array slots.

Component, count, entity, and parameter mismatches return stable `P21-BIND-*` diagnostics. A structure lookup with no exact descriptor name returns `P21-BIND-SCHEMA`; lookup is nominal and case-sensitive. Descriptors supplied to manual structure construction are snapshotted by unique schema name, and duplicate names fail before construction completes.

## Structural validation

The generated descriptor dispatches each registered entity governed by its schema to directly generated structural checks. The checks cover mandatory/`OPTIONAL` presence, generated entity assignability, literal aggregate shape and bounds, required array slots, uniqueness, nested elements, nominal wrappers, enumerations, and SELECT alternatives. See the [structural validation boundary](structural-validation.md) for ordering, paths, constraint IDs, XML traceability, and staged exclusions.

## Current boundary

Generated descriptors now execute the accepted validation-reachable expression/rule closure and support the atomic
[simple typed-read boundary](atomic-simple-read.md). They do not map complex inherited/redeclared physical components,
discover descriptors, resolve parsed occurrence references, bind named or multiple data sections, or write an exchange
file. Those responsibilities remain with the reference, population, complex-mapping, and writer work items.
Unsupported entities are not guessed through reflection or property names.

Fast tests execute every scalar alternative and supported simple parameter form in both directions, including absence, invalid derived markers, nominal/enumeration/SELECT values, entity identity, and all four aggregate categories. Runtime API snapshots fix the abstract-class hook contract. The packed-consumer integration test compiles the generated sealed descriptor from the real package without a runtime dependency on analyzer implementation libraries.
