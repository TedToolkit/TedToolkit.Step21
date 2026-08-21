# Generated schema descriptors

Every valid supplied EXPRESS schema emits one reflection-free descriptor in `TedToolkit.Step21.Generated.<SchemaPascalCase>`. The descriptor is an ordinary public sealed `class`, not a `record`, because it represents singleton behavior and identity rather than a structurally comparable ISO value. Its private constructor and public static `Instance` property expose the only instance; `Name` preserves the nominal schema name.

The runtime `SchemaDescriptor` base is an abstract `class`. Its public surface contains only `SchemaName Name`; runtime-internal non-virtual dispatch forwards to protected allocation, hydration, validation, capability-diagnostic, and projection hooks. The hooks exchange only `Entity`, `ExchangeStructure`, diagnostics/results, and ordered BCL component-name/`ParameterValue` lists. They expose no parser syntax, context, resolver, registry, writer callback, nested public type, reflection, or dynamic dispatch.

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

## Current boundary

This stage does not execute schema rules, map complex inherited/redeclared physical components, evaluate symbolic aggregate bounds, discover descriptors, bind parsed instance references, or write an exchange file. Those responsibilities remain with the validation, complex-mapping, typed-read, reference, and writer work items. Unsupported entities are not guessed through reflection or property names.

Fast tests execute every scalar alternative and supported simple parameter form in both directions, including absence, invalid derived markers, nominal/enumeration/SELECT values, entity identity, and all four aggregate categories. Runtime API snapshots fix the abstract-class hook contract. The packed-consumer integration test compiles the generated sealed descriptor from the real package without a runtime dependency on analyzer implementation libraries.
