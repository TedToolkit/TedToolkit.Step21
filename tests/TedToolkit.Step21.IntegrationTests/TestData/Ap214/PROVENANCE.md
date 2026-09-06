# OCCT AP214 fixture provenance

`occt-box-10x20x30-ap214.step` is a repository-owned fixture generated from the adjacent C++ source.
The source constructs a 10 × 20 × 30 mm box at the origin and exports it as a manifold solid B-rep
using OCCT's AP214IS writer. Verification reads the checked-in STEP file and does not require OCCT or
network access.

- Upstream: Open CASCADE Technology (OCCT), commit
  `7d2efad9c8a9a57ea96c4c8587134b34dd503cd8` (`8.1.0-dev1`).
- Exporter: `STEPControl_Writer`, `STEPControl_ManifoldSolidBrep`,
  `WriteMode_StepSchema_AP214IS`, millimetres; surface curves, color, names, layers, properties,
  metadata, and material export disabled.
- Geometry source: `occt-box-10x20x30-generator.cxx` in this directory.
- Generator SHA-256: `C3C6F7460FAD35AE964E41CCBA412655F5DC6DB3769C776C2E79ECDFD0E3F505`.
- Raw exporter output SHA-256 before allowed normalization:
  `374009C00D82194A6359C167D5903E2262EE44BE3B32E5E51D0A92E9D783967F`.
- Checked-in fixture SHA-256 after allowed normalization:
  `84B04D7AEFF27157B0FBEE09D681977E516C4F16C6CD6C4EF41E34FE8C4EC722`.
- Unsupported-extension negative fixture SHA-256:
  `ABE4ECFA37BBCC7A79FB641FD8E39C93D38111FAF752AED1571174A428E0D17C`.
- Deterministic normalization changed only the `FILE_NAME` name, timestamp, author, organization,
  preprocessor, originating system, and authorization fields. The exporter emitted no `PERSON`,
  `ORGANIZATION`, `LOCAL_TIME`, or UTC-offset instances. Geometry, product, topology, representation,
  units, entity ordering, and references are unchanged.
- The checked-in AP214IS bytes intentionally remain unchanged even though the package baseline is
  2007-DIS. Package verification first proves that the raw file is rejected atomically by exactly
  `APPLICATION_PROTOCOL_DEFINITION_REQUIRED.WR1` and `PRODUCT_REQUIRES_ID_OWNER.WR1`. It then applies
  a deterministic test-only migration in memory: the AIM name/year become
  `AUTOMOTIVE_DESIGN_LF`/`2007`, and one organization, `id owner` role, and applied assignment are
  added for the existing product. No runtime rule is suppressed, and the migration is not package behavior.
- License evidence: the copied `LICENSE_LGPL_21.txt` has SHA-256
  `E237FA56668030E928551DDD60F05DF5FE957F75EAB874BBD017E085ED722E7C`; the copied
  `OCCT_LGPL_EXCEPTION.txt` has SHA-256
  `04580A884EA6CEA294402649FF7B5CBB167D47462D1340A4ED33E550DB10A81B`.

Pinned upstream evidence:

- <https://github.com/Open-Cascade-SAS/OCCT/tree/7d2efad9c8a9a57ea96c4c8587134b34dd503cd8>
- <https://github.com/Open-Cascade-SAS/OCCT/blob/7d2efad9c8a9a57ea96c4c8587134b34dd503cd8/LICENSE_LGPL_21.txt>
- <https://github.com/Open-Cascade-SAS/OCCT/blob/7d2efad9c8a9a57ea96c4c8587134b34dd503cd8/OCCT_LGPL_EXCEPTION.txt>

Supported proof boundary: the fixture proves only the manifold B-rep, product, topology, geometry,
unit, value, identity, and shared-reference subset exercised by this box. It does not establish
complete AP214 conformance or equivalence between the AP214IS exporter schema and the package's fixed
2007-DIS baseline. Colour/layer, PMI, tessellation, kinematics, and other structures are outside this
fixture's claim.
