---
name: read-and-write-step21-models
description: >-
  Use TedToolkit.Step21 to read, edit, validate, or write Part 21 files with generated EXPRESS models, including AP203, AP214, and AP242.
---

# Read and Write STEP Part 21 Models

Read `FILE_SCHEMA` before selecting a generated `SchemaDescriptor.Instance`. Use [application protocol selection](references/protocols.md) for precompiled AP203, AP214, and AP242 types.

## Preserve the exchange structure

1. Pass the matching descriptor to `ExchangeStructure.Read`. Check the schema and sections before interpreting entity types.
2. Modify through the generated typed model. Preserve instance references and data-section membership and update the header only when the intended exchange contract changes.
3. Run `Validate()` and handle diagnostics according to the caller’s policy before writing. Keep invalid data visible to the caller.
4. Re-read the written file with the same descriptor and compare affected values and instance references.

```csharp
using var input = File.OpenText("model.stp");
var structure = ExchangeStructure.Read(input, [SchemaDescriptor.Instance]);
var validation = structure.Validate();
```

Import the namespace for the chosen generated schema before using `SchemaDescriptor`. See the library guide for a complete write example.
