# AP203 schema provenance

## Upstream identity

- Repository: [STEPcode/stepcode](https://github.com/stepcode/stepcode)
- Commit: [`9baa5dadaa1dcfcdc623220d865d36d61ea351e9`](https://github.com/stepcode/stepcode/commit/9baa5dadaa1dcfcdc623220d865d36d61ea351e9)
- Path: [`data/ap203/ap203.exp`](https://github.com/stepcode/stepcode/blob/9baa5dadaa1dcfcdc623220d865d36d61ea351e9/data/ap203/ap203.exp)
- Upstream file SHA-256: `020B4D25DBD0B6EE7D15099B978E3448F6699A72CB862D381E416E32187562F1`

## Local correction

The checked-in `ap203.exp` has SHA-256
`19497DCA88C6FCFE763DA23772B68356BE4361668426954DE9863E4285D0C251`.
It differs from the pinned upstream file only at these two aggregate initializer repetitions:

- Line 4618: `[lis[1],n]` was corrected to `[lis[1] : n]`.
- Line 4645: `[list_to_array(lis[1],low2,u2),(u1 - low1) + 1]` was corrected to
  `[list_to_array(lis[1],low2,u2) : (u1 - low1) + 1]`.

Both changes replace a comma with the EXPRESS repetition separator while preserving the existing
element and repetition-count expressions. No other semantic modification was made.

## Redistribution evidence

STEPcode's `COPYING` identifies the collective work as BSD-3-Clause and is retained beside the
schema together with `AUTHORS` and `INTENT.md`. Their copied-byte SHA-256 values are:

- `COPYING`: `C787486F3E1358CF1CB4456B56E00862DE9C0433E7D49F5501D1289FF8BEF37E`
- `AUTHORS`: `619EE3D3D9CE6DB690B4A20F36AB30616CB8F1FB8616FAEB85D9685AFDFD15FB`
- `INTENT.md`: `B10C7DCC9C269B383C944ACC139F787CCA050D497142CA03ED90C49EF41CE23F`
