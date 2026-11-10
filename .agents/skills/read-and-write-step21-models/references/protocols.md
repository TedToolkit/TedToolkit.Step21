# Select a STEP application protocol

## Read STEP AP203

- Check `FILE_SCHEMA` against the schema descriptor for the installed package before reading.
- Use `TedToolkit.Step21.Schemas.Ap203ConfigurationControlled3dDesignOfMechanicalPartsAndAssembliesMimLf.SchemaDescriptor.Instance` with `ExchangeStructure.Read`. Use the generated schema types for any edits.
- Validate and round-trip a representative file. Handle a schema mismatch before assuming the entity types of this protocol.

## Read STEP AP214

- Check `FILE_SCHEMA` against the schema descriptor for the installed package before reading.
- Use `TedToolkit.Step21.Schemas.AutomotiveDesign.SchemaDescriptor.Instance` with `ExchangeStructure.Read`. Use the generated schema types for any edits.
- Validate and round-trip a representative file. Handle a schema mismatch before assuming the entity types of this protocol.

## Read STEP AP242

- Check `FILE_SCHEMA` against the schema descriptor for the installed package before reading.
- Use `TedToolkit.Step21.Schemas.Ap242ManagedModelBased3dEngineeringMimLf.SchemaDescriptor.Instance` with `ExchangeStructure.Read`. Use the generated schema types for any edits.
- Validate and round-trip a representative file. Handle a schema mismatch before assuming the entity types of this protocol.

Match the exact `FILE_SCHEMA` name exposed by the installed schema descriptor. Similar entity names do not establish that the schema is interchangeable.
