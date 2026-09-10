# OCCT AP203 fixture provenance

`occt-box-10x20x30-ap203.step` is a repository-owned fixture generated from the adjacent C++ source.
The source constructs a 10 × 20 × 30 mm box and exports it as a manifold solid B-rep using OCCT's
AP203 writer. Verification reads the checked-in STEP file and does not require OCCT or network access.

- Upstream: Open CASCADE Technology (OCCT), commit
  `7d2efad9c8a9a57ea96c4c8587134b34dd503cd8` (`8.1.0-dev1`).
- Exporter: `STEPControl_Writer`, `STEPControl_ManifoldSolidBrep`, schema `AP203`, millimetres;
  surface curves, color, names, layers, properties, metadata, and material export disabled.
- Geometry source: `occt-box-10x20x30-generator.cxx` in this directory.
- Fixture SHA-256: `3FB1C5D2E3C972946223072F5E5539E5B970FEF6DDBEFDF6F07D10FE456FB58C`.
- Protocol migration: the normalized fixture declares the official ISO/TS 10303-403 MIM-LF
  identifier, uses its application-protocol name and year, removes the OCCT-only
  `PRODUCT_CATEGORY_RELATIONSHIP`, and narrows `CC_DESIGN_APPROVAL.items` to the supported security
  classification. The referenced product categories and all geometry, topology, product, unit,
  ownership, and approval objects remain. Package proof separately verifies that the former
  `CONFIG_CONTROL_DESIGN` identifier is rejected rather than treated as an alias.
- Deterministic normalization: `FILE_NAME` path, timestamp, author, organization, exporter, system,
  and authorization text were replaced after export. Exporter-generated `PERSON`, `ORGANIZATION`,
  `LOCAL_TIME`, and UTC-offset values were also replaced with fixed neutral values; geometry,
  product, topology, representation, and unit values are unchanged.
- License evidence: OCCT is LGPL-2.1 with the OCCT exception. At the pinned commit,
  `LICENSE_LGPL_21.txt` has SHA-256
  `E237FA56668030E928551DDD60F05DF5FE957F75EAB874BBD017E085ED722E7C`, and
  `OCCT_LGPL_EXCEPTION.txt` has SHA-256
  `04580A884EA6CEA294402649FF7B5CBB167D47462D1340A4ED33E550DB10A81B`.

Pinned upstream evidence:

- <https://github.com/Open-Cascade-SAS/OCCT/tree/7d2efad9c8a9a57ea96c4c8587134b34dd503cd8>
- <https://github.com/Open-Cascade-SAS/OCCT/blob/7d2efad9c8a9a57ea96c4c8587134b34dd503cd8/LICENSE_LGPL_21.txt>
- <https://github.com/Open-Cascade-SAS/OCCT/blob/7d2efad9c8a9a57ea96c4c8587134b34dd503cd8/OCCT_LGPL_EXCEPTION.txt>

Supported proof boundary: the fixture's entities are accepted by the pinned official AP203 MIM-LF
descriptor. Entity keywords absent from that descriptor remain unsupported and fail atomically with
binding evidence. The normalized unsupported-extension fixture has SHA-256
`DE3428DF58D2F861F8583F37A5C101C4DDAD8B18D234026F421AF265FB60A0F8`.
