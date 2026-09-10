# AP214 baseline compatibility

The `1.0.0` package remains bound to the AP214 Edition 3 `AUTOMOTIVE_DESIGN` schema published by
MBx-IF. `BASELINE.json` records its exact identity. MBx-IF is an industry source, not ISO, and no
equivalence with another AP214 edition or AP242 is implied.

The third-party EXP is a verified local generation input only. It is never committed or packed.
Source identity changes, descriptor changes, or generated public/semantic incompatibility require
Major-version review under ADR-0006. Local package verification must compare the manifest hash,
descriptor, public API snapshot, runtime range, and representative typed round trip.
