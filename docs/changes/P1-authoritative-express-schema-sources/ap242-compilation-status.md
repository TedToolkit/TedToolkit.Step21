# AP242 official-source compilation status

- Sole source: ISO/TS 10303-442 edition 7 `mim_lf.exp`
- Pinned SHA-256: `E7E93CF97880FD87D634E4B9EE58400DA0A1BE6C06A1DE76EC13807ECDC15CCB`
- Checked: 2026-09-10
- Source identity: verified
- Generation status: blocked

The source URL, downloaded bytes, and recorded hash agree. There is no source-provenance mismatch.

The current compiler reports type conflicts in expressions associated with `datum_target.the_datum`
and `valid_csg_2d_primitives`. This record does not conclude whether those conflicts require a
publisher correction or additional faithful EXPRESS semantics in this compiler.

By maintainer decision, the ISO file remains the sole AP242 EXPRESS input. It is not modified, and
no corrected copy, overlay, downstream source, or silent fallback is permitted. Generation remains
blocked until the same official bytes can be processed faithfully or ISO publishes a superseding
authoritative file.
