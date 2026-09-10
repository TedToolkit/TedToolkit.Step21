# Native AOT package proof

SBRT-025 proves the deployment boundary from a real NuGet consumer rather than from project references.

The representative consumer supplies two EXPRESS schemas as `AdditionalFiles` and uses only the public packaged API. It reads a multi-schema exchange structure containing simple and flat `ANDOR` complex instances plus representative M-01 through M-06 explicit attribute specializations. The executable observes inherited entity, SELECT, exact numeric, required, direct-entity covariant aggregate views, and live projected aggregate SELECT views over their unique narrow storage; edits values; validates; writes; and reads the result again. M-06 covers ARRAY, LIST, BAG, and SET across strict and identity-equal distinct-wrapper relations, including broad-only read rejection and zero-byte invalid-write rejection. It also executes validation-reachable singular inverse cases for one owner with repeated forward-role occurrences, zero owners, multiple distinct owners, function/repeated access, lazy short-circuiting, read rejection, and zero-byte write rejection. The fixture contains no reflection or dynamic-code fallback.

## Stable proof

Run the proof on Windows:

```powershell
./build/verify-native-aot.ps1
```

For a precompiled schema package, select its dedicated fixture journey:

```powershell
./build/verify-native-aot.ps1 -Ap203
./build/verify-native-aot.ps1 -Ap214
./build/verify-native-aot.ps1 -Ap242
```

These modes pack the selected schema and shared runtime, restore a package-only consumer from an
isolated local feed, and execute the same typed read/edit/validate/write/reread and negative checks
used by its package integration tests. AP214 additionally proves the raw AP214IS fixture's two
edition-rule failures before applying the documented test-only 2007-DIS migration. No validation
rule is disabled. AP242 executes the checked-in AP242DIS package-only journey and its invalid-edit
and unsupported-entity cases. The scripts do not publish packages to a remote feed.

Package byte reproducibility is a separate proof:

```powershell
./build/verify-schema-package-reproducibility.ps1 -Schema Ap214
./build/verify-schema-package-reproducibility.ps1 -Schema Ap242
```

It requires restored build dependencies, performs two clean non-incremental builds without restore,
and compares normalized package entries (including assembly, symbols and documentation) plus fixed
source, provenance, public-API and fixture inputs. It excludes only NuGet archive bookkeeping;
packing one existing assembly twice is not a substitute for this proof.

The Native AOT command restores its source projects, builds and packs the product, and bootstraps the
`win-x64` compiler packages from NuGet on a clean machine. It then copies the complete dependency and
native toolchain closure into an isolated local feed, restores the consumer from only that feed,
publishes with Native AOT and warnings-as-errors, executes the native artifact, and rejects
AOT/trimming warnings or forbidden runtime artifacts. A clean machine therefore needs NuGet access
for the bootstrap stage but does not need a pre-populated global package cache.

Successful runs remove their temporary proof directory. Failed runs retain it and print its exact
path, preserving the native object, linker response file and local packages for diagnosis. Remove
that directory only after the failure evidence is no longer needed.

`win-x64` is the executable conformance proof target, not an exclusive platform-support list. Other compatible runtime identifiers are neither rejected nor promised by this evidence.

The packed-consumer integration test independently builds the same package consumer twice in isolated directories and compares every emitted source path and byte. It also verifies the generated source, NuGet archive, and resolved runtime graph contain no RoslynHelper runtime edge, validation framework, or JSON/XML serialization contract.

`ExchangeStructure.Entities` is the minimal public population-navigation surface required after `Read`: it is a live read-only enumeration in registration order. It does not expose occurrence names, mutable registries, syntax nodes, parser contexts, or reader/writer facades.
