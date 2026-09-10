# Authoritative EXPRESS source manifest

The machine-readable contract is [`SOURCES.json`](SOURCES.json). The EXP bytes themselves are kept
only in the ignored local cache and are not distributable repository assets.

## AP203

- Authority: ISO (official)
- Standard: [ISO/TS 10303-403:2010](https://www.iso.org/standard/56238.html)
- Download: [ISO SMRL v12 ZIP](https://standards.iso.org/iso/10303/smrl/v12/tech/smrlv12.zip)
- Archive member: `smrlv12/data/modules/ap203_configuration_controlled_3d_design_of_mechanical_parts_and_assemblies/mim_lf.exp`
- Local cache: `.cache/ap203/mim_lf.exp`
- Retrieved: 2026-09-10
- SHA-256: `255EAFFD5984373F5FE2F41369088B6FD07F970EB5915CE9920F0A5F339DDD44`

## AP214

- Authority: MBx Interoperability Forum (industry-authoritative, not ISO)
- ISO status: [ISO 10303-214:2010 is withdrawn](https://www.iso.org/standard/43669.html)
- Source index: [MBx-IF EXPRESS Schemas](https://www.mbx-if.org/home/mbx/resources/express-schemas/)
- Download: [AP214 Edition 3 ZIP](https://www.mbx-if.org/home/wp-content/uploads/2024/07/AP214E3_2010.zip)
- Archive member: `AP214E3_2010.exp`
- Local cache: `.cache/ap214/AP214E3_2010.exp`
- Rights notice: [MBx-IF imprint](https://www.mbx-if.org/home/imprint/)
- Retrieved: 2026-09-10
- SHA-256: `71AB140FE7F774321BEEE6A31E6FEE2AFC3973FD60350AE2018C74C211FB4295`

No current ISO-hosted AP214 EXP download was found. This fallback must never be described as an ISO
publication.

## AP242

- Authority: ISO (official)
- Standard: [ISO 10303-242:2025, AP242 Edition 4](https://www.iso.org/standard/84300.html)
- Download: [ISO/TS 10303-442 edition 7 MIM long form](https://standards.iso.org/iso/ts/10303/-442/ed-7/tech/express/mim_lf.exp)
- Local cache: `.cache/ap242/mim_lf.exp`
- Retrieved: 2026-09-10
- SHA-256: `E7E93CF97880FD87D634E4B9EE58400DA0A1BE6C06A1DE76EC13807ECDC15CCB`
- Generation input: `.cache/ap242/mim_lf.compat.exp`
- Generation input SHA-256: `00B6027C63671AAD36C943B7C65608773094CCDF87636B197EC634F30B306360`
- Compatibility transform: `build/prepare-ap242-schema.ps1`

The official file contains two internally inconsistent expressions. The audited transform projects
`datum_target.the_datum` through each relationship's `related_shape_aspect` and delegates recursive
2D CSG validation to a `boolean_operand_2d` helper. It runs only after official-source hash
verification and produces a separately pinned, Git-ignored generation input.

## Legal boundary

These links and hashes prove source identity only. They do not transfer copyright or grant a
licence. Review the [ISO End Customer Licence Agreement](https://www.iso.org/terms-conditions-licence-agreement.html)
and [ISO copyright notice](https://www.iso.org/copyright.html) before acquisition or use. As checked
on 2026-09-10, the ISO agreement treats digital integration, transformation, and operationalization
in software as uses that may require additional licence rights. Local-only code generation is not
declared permitted merely because the input or output is not distributed.

Repository policy therefore requires the operator to establish applicable rights before download,
use, or generation; prohibits distributing the EXP files; and requires a separate review before
publishing generated source or compiled output. The fetch acknowledgement is an audit guard, not a
licence grant. MBx-IF publishes the AP214 download but does not state redistribution permission on
the schema index; its rights must likewise be established independently.
