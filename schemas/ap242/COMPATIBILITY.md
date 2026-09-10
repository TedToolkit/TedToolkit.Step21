# AP242 baseline compatibility

The `1.0.0` package is generated from the ISO N11521 / ISO/TS 10303-442 edition 7 MIM long form
through the hash-pinned compatibility transform recorded in `BASELINE.json`. The unmodified ISO
source and the separate generation input each have an exact SHA-256. The transform corrects only
the two documented internal type contradictions and fails if the official hash or patch anchors
change.

The third-party EXP is a verified local generation input only. It is never committed or packed.
Source identity changes, descriptor changes, or generated public/semantic incompatibility require
Major-version review under ADR-0006. Local package verification compares the manifest hash,
descriptor, public API snapshot, runtime range, and representative typed round trip.
