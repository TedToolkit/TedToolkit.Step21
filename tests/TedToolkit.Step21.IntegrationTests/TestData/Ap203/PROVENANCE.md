# OCCT AP203 fixture provenance

`occt-box-10x20x30-ap203.step` is a repository-owned fixture generated from the adjacent C++ source.
The source constructs a 10 × 20 × 30 mm box and exports it as a manifold solid B-rep using OCCT's
AP203 writer. Verification reads the checked-in STEP file and does not require OCCT or network access.

- Upstream: Open CASCADE Technology (OCCT), commit
  `7d2efad9c8a9a57ea96c4c8587134b34dd503cd8` (`8.1.0-dev1`).
- Exporter: `STEPControl_Writer`, `STEPControl_ManifoldSolidBrep`, schema `AP203`, millimetres;
  surface curves, color, names, layers, properties, metadata, and material export disabled.
- Geometry source: `occt-box-10x20x30-generator.cxx` in this directory.
- Fixture SHA-256: `2F40CE06A8646B3AE33A8BD871181A356D413CDD6B864D9C8D484A3D1E127B62`.
- Deterministic normalization: `FILE_NAME` path, timestamp, author, organization, exporter, system,
  and authorization text were replaced after export. Exporter-generated `PERSON`, `ORGANIZATION`,
  `LOCAL_TIME`, and UTC-offset values were also replaced with fixed neutral values; geometry,
  product, topology, representation, unit, and approval relationships are unchanged.
- License evidence: OCCT is LGPL-2.1 with the OCCT exception. At the pinned commit,
  `LICENSE_LGPL_21.txt` has SHA-256
  `E237FA56668030E928551DDD60F05DF5FE957F75EAB874BBD017E085ED722E7C`, and
  `OCCT_LGPL_EXCEPTION.txt` has SHA-256
  `04580A884EA6CEA294402649FF7B5CBB167D47462D1340A4ED33E550DB10A81B`.

Pinned upstream evidence:

- <https://github.com/Open-Cascade-SAS/OCCT/tree/7d2efad9c8a9a57ea96c4c8587134b34dd503cd8>
- <https://github.com/Open-Cascade-SAS/OCCT/blob/7d2efad9c8a9a57ea96c4c8587134b34dd503cd8/LICENSE_LGPL_21.txt>
- <https://github.com/Open-Cascade-SAS/OCCT/blob/7d2efad9c8a9a57ea96c4c8587134b34dd503cd8/OCCT_LGPL_EXCEPTION.txt>

Supported proof boundary: the fixture's AP203 entities are accepted. Entity keywords absent from the
pinned `CONFIG_CONTROL_DESIGN` descriptor remain unsupported and fail atomically with binding evidence.
