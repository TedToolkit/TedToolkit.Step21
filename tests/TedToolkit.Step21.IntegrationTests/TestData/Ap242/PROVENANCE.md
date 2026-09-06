# OCCT AP242 fixture provenance

The repository-owned input is a 10 × 20 × 30 mm box at the origin. The checked-in fixture was
generated on Windows x64 by `cadquery-ocp` `7.9.3.1.1` (OCCT `7.9.3.1`) in
`STEPControl_ManifoldSolidBrep` mode with `write.step.schema=AP242DIS`, millimetres, and surface
curves, colour, layer, subshape names, and properties disabled.

The exact CPython 3.10 Windows wheel is identified by SHA-256
`B52931A6786F9A1949BCAC7EF49C8A83426C4198D6847CD13A8CC40795207E09`.
The retained generator `build/generate-ap242-fixture.py` has SHA-256
`13A099C24BAAD5020525CD61115253061CFAAF701DF14149CC0578C0681ED19B`.

Two deterministic header/edition normalizations remove machine/time variability and align OCCT's
short AP242 application name with the pinned N11521 long-form contract:

- `FILE_NAME` is fixed to repository-owned values and timestamp.
- `APPLICATION_PROTOCOL_DEFINITION.application_interpreted_model_schema_name` is expanded from
  `ap242_managed_model_based_3d_engineering` to
  `ap242_managed_model_based_3d_engineering_mim_lf`.

No DATA-section geometry, topology, units, entity identifiers, values, or references are otherwise
changed. The valid fixture SHA-256 is
`88DA6C164CC685A881A4A52AC7D0BA90E27D649EE9183810887F1A8930AEFD30`; it contains 170 entities,
one product, six advanced faces, twelve edge curves, eight vertex points, twenty-seven Cartesian
points, and millimetre/radian/steradian units. The unsupported-entity negative fixture SHA-256 is
`2781891BE750A0B04921B7094AB6908F03B1CA46CC5DA5593FFD620B8D95A2F4`.

OCCT is LGPL-2.1 with the OCCT exception. The retained evidence hashes are:

- `LICENSE_LGPL_21.txt`: `E237FA56668030E928551DDD60F05DF5FE957F75EAB874BBD017E085ED722E7C`
- `OCCT_LGPL_EXCEPTION.txt`: `04580A884EA6CEA294402649FF7B5CBB167D47462D1340A4ED33E550DB10A81B`

Verification consumes only the checked-in STEP files and packages; it does not require Python,
OCCT, the wheel, an installed CAD application, or network access. This fixture proves only the
recorded manifold B-rep journey, not full AP242 conformance.
