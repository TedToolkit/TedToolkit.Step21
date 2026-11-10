---
name: read-step21-ap214
description: >-
  Use TedToolkit.Step21.Ap214 when a Part 21 file declares the AP214 schema for automotive design.
---

# Read STEP AP214

- Check `FILE_SCHEMA` against the schema descriptor for the installed package before reading.
- Use `TedToolkit.Step21.Schemas.AutomotiveDesign.SchemaDescriptor.Instance` with `ExchangeStructure.Read`. Use the generated schema types for any edits.
- Validate and round-trip a representative file. Handle a schema mismatch before assuming the entity types of this protocol.
