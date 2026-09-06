# AP242 schema provenance

## Fixed source

- Repository: [STEPcode](https://github.com/stepcode/stepcode)
- Revision: `9baa5dadaa1dcfcdc623220d865d36d61ea351e9`
- Path: [`data/ap242/242_mim_lf.exp`](https://github.com/stepcode/stepcode/blob/9baa5dadaa1dcfcdc623220d865d36d61ea351e9/data/ap242/242_mim_lf.exp)
- Upstream-byte and canonical-LF SHA-256: `E7E93CF97880FD87D634E4B9EE58400DA0A1BE6C06A1DE76EC13807ECDC15CCB`
- Repository patched canonical-LF SHA-256: `221222ED7F92873D8A1BBDDAE569ED72C87730E09F56226A92E3F108FC9EB7A0`

The source identifies itself as ISO TC184/SC4/WG12 N11521, ISO/TS 10303-442 AP242 managed
model based 3d engineering EXPRESS MIM long form, superseding N11273. Its nominal schema is
`Ap242_managed_model_based_3d_engineering_mim_lf`; EXPRESS identifiers are case-insensitive.
All declarations are retained. Two local, semantics-preserving corrections make upstream expressions
whose declared and actual types contradict one another executable by a statically typed generator:

- `datum_target.the_datum` calls `get_datums_for_datum_target`, which performs the intended projection
  from each qualifying `shape_aspect_relationship` to its `related_shape_aspect` datum. Upstream declared
  `SET OF datum` while its `QUERY` returned the relationship elements themselves.
- `valid_csg_2d_primitives` delegates recursion to `valid_csg_2d_operand`. Upstream recursively passed a
  `boolean_operand_2d` to the public function's `csg_solid_2d` parameter even though the function body
  dereferenced the latter's `tree_root_expression`.

No entity, attribute, type alternative, validation condition, or public function entry point was removed.
The upstream hash above remains the source-identity anchor; the patched hash is the exact build input.

This is one fixed baseline, not a promise of compatibility with every AP242 edition or vendor
variant. A `FILE_SCHEMA` OID is retained as exchange data, not used as package identity or for
automatic schema selection.

## Redistribution evidence

The pinned STEPcode revision provides its collective work under BSD-3-Clause. The retained
attribution files have these canonical-LF SHA-256 values:

- `COPYING`: `C787486F3E1358CF1CB4456B56E00862DE9C0433E7D49F5501D1289FF8BEF37E`
- `AUTHORS`: `619EE3D3D9CE6DB690B4A20F36AB30616CB8F1FB8616FAEB85D9685AFDFD15FB`
- `INTENT.md`: `B10C7DCC9C269B383C944ACC139F787CCA050D497142CA03ED90C49EF41CE23F`

The package retains these notices and this provenance record. This is repository-internal
redistribution evidence, not external legal approval. If a later audit finds the schema outside
the grant, use and distribution must stop pending an approved source decision.
