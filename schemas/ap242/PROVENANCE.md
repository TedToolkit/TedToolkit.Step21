# AP242 schema provenance

- Authority: ISO
- Authority status: official publication
- Standard: [ISO 10303-242:2025 AP242 Edition 4](https://www.iso.org/standard/84300.html)
- Download: [ISO/TS 10303-442 edition 7 MIM long form](https://standards.iso.org/iso/ts/10303/-442/ed-7/tech/express/mim_lf.exp)
- Unmodified local source: `schemas/.cache/ap242/mim_lf.exp`
- Local generation input: `schemas/.cache/ap242/mim_lf.compat.exp`
- Retrieved: 2026-09-10
- SHA-256: `E7E93CF97880FD87D634E4B9EE58400DA0A1BE6C06A1DE76EC13807ECDC15CCB`
- Generation-input SHA-256: `00B6027C63671AAD36C943B7C65608773094CCDF87636B197EC634F30B306360`
- Declared schema: `Ap242_managed_model_based_3d_engineering_mim_lf`

The cached source file is the unmodified ISO publication. After that exact hash is verified,
`build/prepare-ap242-schema.ps1` creates a separate, Git-ignored generation input with two audited
corrections: `datum_target.the_datum` projects qualifying relationships to their related datum, and
recursive 2D CSG validation accepts `boolean_operand_2d`. The transform checks unique anchors and
the pinned output hash, so a changed publisher file cannot be patched silently.

The EXP file is ignored, untracked, intended only for local code generation, and excluded from
packages and source archives. This record establishes identity, not ownership or permission. The
operator must confirm that applicable ISO terms cover download, local processing, and generation;
generated source and compiled output must not be published without a separate rights review.
