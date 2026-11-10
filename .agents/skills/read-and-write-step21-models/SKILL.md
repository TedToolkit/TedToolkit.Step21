---
name: read-and-write-step21-models
description: >-
  Use TedToolkit.Step21 when reading, modifying, validating, or writing a Part 21 exchange structure with generated EXPRESS schema types.
---

# Read and Write STEP Part 21 Models

- Choose the correct generated `SchemaDescriptor.Instance` for the exchange file. Pass the descriptor to `ExchangeStructure.Read` and preserve the header’s schema identity.
- Use the typed data model for edits and maintain references and section membership. Validate the structure before emitting it.
- Read the written file back with the same descriptor and check the intended values and references, including relevant validation failures.
