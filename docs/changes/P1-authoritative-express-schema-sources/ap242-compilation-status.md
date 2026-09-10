# AP242 official-source compilation status

- Sole source: ISO/TS 10303-442 edition 7 `mim_lf.exp`
- Pinned SHA-256: `E7E93CF97880FD87D634E4B9EE58400DA0A1BE6C06A1DE76EC13807ECDC15CCB`
- Checked: 2026-09-10
- Source identity: verified
- Generation status: enabled through audited compatibility transform

The source URL, downloaded bytes, and recorded hash agree. There is no source-provenance mismatch.

The official file contains type conflicts in expressions associated with `datum_target.the_datum`
and `valid_csg_2d_primitives`. They cannot be represented as type-safe C# without correcting the
schema expressions.

By maintainer decision on 2026-09-10, the ISO file remains the immutable source-identity anchor and
a separate audited generation input is permitted. `build/prepare-ap242-schema.ps1` requires the
official hash, checks unique anchors, applies the two previously verified semantic corrections, and
verifies output SHA-256
`00B6027C63671AAD36C943B7C65608773094CCDF87636B197EC634F30B306360`. Neither file is tracked or
packed; a publisher update fails closed and requires review.
