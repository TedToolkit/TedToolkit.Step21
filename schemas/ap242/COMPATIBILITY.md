# AP242 baseline compatibility

The `1.0.0` package is generated from the byte-exact ISO N11521 / ISO/TS 10303-442 edition 7 MIM
long form recorded in `BASELINE.json`. There is no patched schema variant. Any compiler accommodation
must implement general EXPRESS semantics and retain invalid-neighbor diagnostics.

The third-party EXP is a verified local generation input only. It is never committed or packed.
Source identity changes, descriptor changes, or generated public/semantic incompatibility require
Major-version review under ADR-0006. Local package verification compares the manifest hash,
descriptor, public API snapshot, runtime range, and representative typed round trip.
