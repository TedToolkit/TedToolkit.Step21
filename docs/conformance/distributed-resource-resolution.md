# Distributed Part 21 resource resolution

`ExchangeStructure.Read(..., ExchangeStructureReadOptions)` implements the ISO 10303-21:2016 Edition 3
reference state machine inside one atomic read. The caller supplies every external byte explicitly; the runtime owns
URI resolution, anchor traversal, occurrence identity, ZIP/directory interpretation, schema binding, and validation.
The overload without options retains the pre-existing unresolved-reference behavior and never performs I/O.

Normative navigation uses the public [Edition 3 final text](https://www.steptools.com/stds/step/IS_final_p21e3.html).
Repository fixtures are minimal original examples, not copied standard examples.

| Normative area | Runtime contract | Focused evidence |
| --- | --- | --- |
| 10.2 | `Part21Reference.ResolutionStatus` and `TryGetResolvedValue` | Relative/absolute resources, local anchors, canonical-alias identity, external simple value, missing target, and circular forwarding |
| 10.2.1 | Fragmentless references resolve to `$` without acquisition | Provider request log remains empty for the fragmentless entry |
| 10.2.2 and Annex G | Fragment-only UUIDs are passed to the explicit provider as registry requests; the returned structure must contain the UUID anchor | UUID registry fixture |
| 10.2.3, 10.2.5–10.2.7 | Local anchor, entity/value category, named anchor, and numeric legacy entity target selection | Local/entity/value/type matrix, including direct/nested/typed `@n` binding and external numeric fragments |
| 10.2.4 | `IPart21ResourceConverter` returns schema-neutral clear text, ZIP, or directory content | Other-format conversion fixture |
| Annex A.4 | `Part21ResourceContentKind.ZipArchive`; PKZip 2.04g exclusions; exact `ISO-10303.p21` root; scoped subsidiary paths; in-memory extraction | ZIP root/subsidiary, ZIP64/encryption/Unicode-name/Deflate64 rejection, missing-root, recursion, and escape fixtures |
| Annex A.5 | Directory entry snapshot follows the same root and relative-address rules | Directory root/subsidiary fixture |

The per-read graph caches each resolved resource identity once, so repeated fragments share the same parsed model and
CLR entity identity. Resolved external entities remain outside local data registrations but participate in generated
reference-type hydration and their own resource document's complete schema validation. The receiving structure owns
only the local occurrence alias used to write the `REFERENCE` association; no resolver, proxy, cache, or I/O state is
stored in a generated entity.

No provider is invoked for local non-UUID fragments or fragmentless references. Relative external paths require an
explicit absolute base URI. A missing delivered resource, absent anchor, category mismatch, or standard reference
cycle produces the null result. Missing provider/converter capability, provider re-entry, quota exhaustion, archive
recursion, invalid archive/root, compression-ratio violation, and archive path escape are atomic capability failures
with distinct `P21-CAP-RESOURCE-*` or `P21-RESOURCE-*` diagnostic codes.

`Part21ResourceLimits` bounds distinct provider resources, reference depth, nested archives, total supplied bytes,
archive/directory entries, uncompressed archive bytes, and per-entry compression ratio. ZIP and directory content is
processed in memory and is never extracted to disk. Directory bytes are shared as `ReadOnlyMemory<byte>` during the
read; ZIP entries allocate only their uncompressed in-memory representation. Other-format input and its converted
output both count toward the total-byte limit, and over-limit opaque input is rejected before conversion.

Focused proof is the `DistributedResourceResolutionTests` suite plus the cumulative public-API snapshot. Signature
trust, SDAI domain equivalence, constant occurrences, and the integrated adversarial quota matrix belong to their
separately approved work items and are not claimed here.
