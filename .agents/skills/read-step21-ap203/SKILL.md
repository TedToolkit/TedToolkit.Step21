---
name: read-step21-ap203
description: >-
  Use TedToolkit.Step21.Ap203 when a Part 21 file declares the AP203 schema for configuration-controlled design.
---

# Read STEP AP203

- Check `FILE_SCHEMA` against the schema descriptor for the installed package before reading.
- Use `TedToolkit.Step21.Schemas.Ap203ConfigurationControlled3dDesignOfMechanicalPartsAndAssembliesMimLf.SchemaDescriptor.Instance` with `ExchangeStructure.Read`. Use the generated schema types for any edits.
- Validate and round-trip a representative file. Handle a schema mismatch before assuming the entity types of this protocol.
