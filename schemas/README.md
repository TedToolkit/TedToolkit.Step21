# Maintained EXPRESS schema inputs

AP203, AP214, and AP242 are generated from third-party EXPRESS publications downloaded directly
from the authority recorded in [`SOURCES.md`](SOURCES.md). Run this explicit preparation step before
building a maintained schema package:

```powershell
pwsh -NoProfile -File build/fetch-express-schemas.ps1 -AcknowledgeThirdPartyTerms
```

The command downloads to `schemas/.cache/`, verifies the pinned SHA-256 before atomic promotion, and
never changes an official file. The acknowledgement switch confirms only that the operator reviewed
the publisher terms; it does not create or expand any licence. Package builds do not access the
network implicitly and fail with the preparation command when the cache is absent.

## Distribution policy

- `schemas/.cache/` is ignored by Git. The third-party AP schema bytes must not be committed.
- The EXP files are local code-generation inputs only. They must not enter source archives, NuGet
  packages, or other published artifacts.
- The repository records links, identity, and hashes; it does not claim ownership or a license to
  redistribute ISO or MBx-IF material.
- ISO's current End Customer Licence Agreement says that digital integration, transformation, or
  operationalization in software may require additional licence rights. “Local” and “not
  distributed” do not by themselves establish permission to generate code from an ISO publication.
- Before downloading, using, or generating from these files, the operator is responsible for
  confirming that the applicable publisher licence covers the intended use. Generated source and
  compiled packages require a separate review before publication.
- Repository-authored miniature EXPRESS test fixtures are unaffected by this third-party policy.

See [ADR-0013](../docs/adr/ADR-0013-authoritative-express-schema-sources.md) for the governing
decision.
