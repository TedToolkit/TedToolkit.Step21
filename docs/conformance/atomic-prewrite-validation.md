# Atomic pre-write validation

`ExchangeStructure.Write` and `WriteEntity` are final-state boundaries. Property setters, aggregate mutations, `Add`,
and non-cascading `Remove` never invoke schema validation. Each write attempt invokes complete structure validation once
after those edits and before projection or destination output. Callers must not mutate the non-thread-safe structure
concurrently with validation or writing.

## Complete validation evidence

`Write` throws one `ExchangeStructureWriteValidationException` containing the complete ordered structure result.
`WriteEntity` adds an unregistered target failure to that same result instead of hiding existing graph failures. After
a valid graph is projected, the writer recursively checks every entity occurrence in simple/complex, aggregate, and
typed parameters; all projected references missing from the structure are returned together with physical parameter
paths. These checks defend the public descriptor extension boundary as well as generated descriptors.

An invalid graph is not projected because invalid mandatory or aggregate state need not be safely projectable.
Generated `DirectReferences` exposes every physical reference, so model-level dangling references are already included
in the complete structure result. Projection-level checking covers a descriptor that publishes additional physical
references. Duplicate explicit occurrence-name claims remain atomic `Add` argument failures and therefore cannot exist
in a published structure.

## Capability and output boundary

For a valid graph, the writer gathers descriptor, data-section, and physical-mapping capability diagnostics before
formatting. Any unsupported operation throws one `ExchangeStructureCapabilityException` with the complete diagnostics.
Both writer entry points buffer all domain-controlled text and call the supplied `TextWriter` only after validation,
capability, projection, and formatting succeed. Validation/capability failures therefore produce zero characters.
Once delivery begins, an exception raised by the caller's `TextWriter` is propagated unchanged; external I/O rollback
is not claimed.
