# Native AOT package proof

SBRT-025 proves the deployment boundary from a real NuGet consumer rather than from project references.

The representative consumer supplies two EXPRESS schemas as `AdditionalFiles` and uses only the public packaged API. It reads a multi-schema exchange structure containing simple and flat `ANDOR` complex instances plus representative M-01 through M-05 explicit attribute specializations. The executable observes inherited entity, SELECT, exact numeric, required, and all four covariant aggregate views over their unique narrow storage; edits values; validates; writes; and reads the result again. The fixture contains no reflection or dynamic-code fallback.

## Stable proof

Run the proof on Windows:

```powershell
./build/verify-native-aot.ps1
```

The command builds and packs the product, restores the consumer from that local package, publishes it for `win-x64` with Native AOT and warnings-as-errors, executes the native artifact, and rejects AOT/trimming warnings or forbidden runtime artifacts.

`win-x64` is the executable conformance proof target, not an exclusive platform-support list. Other compatible runtime identifiers are neither rejected nor promised by this evidence.

The packed-consumer integration test independently builds the same package consumer twice in isolated directories and compares every emitted source path and byte. It also verifies the generated source, NuGet archive, and resolved runtime graph contain no RoslynHelper runtime edge, validation framework, or JSON/XML serialization contract.

`ExchangeStructure.Entities` is the minimal public population-navigation surface required after `Read`: it is a live read-only enumeration in registration order. It does not expose occurrence names, mutable registries, syntax nodes, parser contexts, or reader/writer facades.
