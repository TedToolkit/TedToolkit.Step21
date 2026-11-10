---
name: read-step21-ap242
description: >-
  Use TedToolkit.Step21.Ap242 when a Part 21 file declares the AP242 schema for managed model-based 3D engineering.
---

# Read STEP AP242

- Check `FILE_SCHEMA` against the schema descriptor for the installed package before reading.
- Use `TedToolkit.Step21.Schemas.Ap242ManagedModelBased3dEngineeringMimLf.SchemaDescriptor.Instance` with `ExchangeStructure.Read`. Use the generated schema types for any edits.
- Validate and round-trip a representative file. Handle a schema mismatch before assuming the entity types of this protocol.
