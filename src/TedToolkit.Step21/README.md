# TedToolkit.Step21 package guide

TedToolkit.Step21 generates strongly typed C# from one or more EXPRESS schemas, then reads, edits, validates, and writes ISO 10303-21 exchange structures through a schema-neutral runtime. Generated entities are mutable classes with reference identity; generated schema descriptors are sealed singleton classes.

## Install and generate a schema

```shell
dotnet add package TedToolkit.Step21
```

Add each `.exp` file to the consumer project as an MSBuild additional file:

```xml
<ItemGroup>
  <AdditionalFiles Include="Schemas/catalog.exp" />
</ItemGroup>
```

The generator consumes all supplied schemas as one closed set. Invalid EXPRESS syntax, binding, unsupported validation-reachable execution, or generated C# name collisions produce source-located build diagnostics and withhold the affected generated schema atomically.

For an EXPRESS schema named `catalog`, generated types are placed in `TedToolkit.Step21.Schemas.Catalog`, with its descriptor available as `SchemaDescriptor.Instance`.

## Read, navigate, edit, validate, and write

```csharp
using TedToolkit.Step21;
using TedToolkit.Step21.Schemas.Catalog;
using CatalogSchemaDescriptor = TedToolkit.Step21.Schemas.Catalog.SchemaDescriptor;

using var input = File.OpenText("catalog.p21");
var structure = ExchangeStructure.Read(
    input,
    [CatalogSchemaDescriptor.Instance]);

foreach (var product in structure.Entities.OfType<Product>())
{
    product.Name = product.Name.Trim();
}

var validation = structure.Validate();
if (!validation.IsValid)
{
    foreach (var failure in validation.Failures)
    {
        Console.Error.WriteLine($"{failure.Code}: {failure.Path}: {failure.Message}");
    }

    return;
}

using var output = File.CreateText("catalog-edited.p21");
structure.Write(output);
```

`ExchangeStructure.Read` accepts the complete explicitly supplied descriptor set, performs parse/bind/hydrate/validation atomically, and returns no partial model. `structure.Entities` is a live read-only population enumeration. Generated entity-valued properties expose the actual generated entity interfaces, including forward, shared, cyclic, aggregate-contained, same-schema, and supported multi-schema references.

Distributed references use the additive overload with `ExchangeStructureReadOptions`. Supply an absolute base URI and
an `IPart21ResourceProvider` that returns only caller-authorized bytes or in-memory directory entries. The runtime
resolves URI/anchor chains, ZIP or directory roots, shared entity identity, and `$` outcomes; it never opens a path or
network connection implicitly. `Part21ResourceLimits` bounds the complete per-read graph, and
`IPart21ResourceConverter` is the explicit hook for another source format.
`Part21ProcessingLimits.Default` is a shared finite policy for root input, URI/archive-entry size, CMS work, and
atomic output. Use `ExchangeStructureReadOptions.WithProcessingLimits(...)` or the write-options overload to apply a
stricter immutable policy without enabling any additional I/O or trust capability.

`SCHEMA_POPULATION` resources use the same shared resolver graph and expose transitive population entities through
`structure.SchemaPopulationEntities`. `FILE_POPULATION` supports all three Annex E determination methods. For
cross-schema domain equivalence, use `ExchangeStructureReadOptions.WithDomainEquivalenceProvider` with a caller-owned
provider for the complete relation and physical-parameter projection; the runtime performs target-schema allocation,
hydration, identity-alias control, and validation without
requiring a Part 22 repository or inferring compatibility by name or shape. Population message digests require a
signature, use the first signature section's digest algorithm over the referenced file bytes, and can verify
content-only resources without materializing a false exchange model.

Use `structure.WriteEntity(writer, entity)` when only one registered entity-instance record is required; its overload
accepts explicit `Part21ProcessingLimits`. All write operations validate and size-check the final staged output before
producing destination characters.

## Construct a structure

Construction uses the same generated types and descriptor; every registration names its destination `DataSection` explicitly:

```csharp
var descriptor = CatalogSchemaDescriptor.Instance;
var header = new HeaderSection(
    new FileDescription(["generated"], "3;1"),
    new FileName(
        "catalog.p21",
        "2026-08-22T00:00:00",
        ["author"],
        ["organization"],
        "TedToolkit.Step21",
        "consumer",
        ""),
    new FileSchema([descriptor.Name.Value]));
var structure = new ExchangeStructure(header, [descriptor]);
var section = new DataSection(descriptor.Name);
structure.DataSections.Add(section);

var product = new Product("name");
structure.Add(section, product);
using var output = File.CreateText("catalog-created.p21");
structure.Write(output);
```

Property, aggregate, registration, and removal edits may temporarily make the graph invalid. Call `structure.Validate()` for side-effect-free feedback; read/write boundaries enforce validation themselves.

## Failure boundaries

| Stage | Public evidence |
| --- | --- |
| Part 21 syntax | `ExchangeStructureSyntaxException.Diagnostics` |
| Schema/physical binding | `ExchangeStructureBindingException.Diagnostics` |
| Read-time model validation | `ExchangeStructureReadValidationException.ValidationResult` |
| Unsupported operation | `ExchangeStructureCapabilityException.Diagnostics` |
| Write-time validation | `ExchangeStructureWriteValidationException.ValidationResult`; no output is produced |
| Destination I/O | The underlying `TextWriter` exception propagates unchanged |

Diagnostics use stable codes and optional `SourceLocation` values containing only file path plus 1-based line and column. Duplicate descriptor schema names throw `ArgumentException` before input is consumed.

## Delivered boundary

- Complete ISO 10303-21:2016 Edition 3 clear-text syntax and ISO 10303-11:2004 Edition 2 EXPRESS syntax.
- Closed-set schema binding, generated scalar/nominal/SELECT/aggregate/entity types, descriptor-based hydration/projection, and the documented statically generated validation-reachable EXPRESS subset.
- Simple mappings plus the documented source-bounded complex mapping profile, including nested `ONEOF`/`ANDOR`
  expressions and inherited/redeclared components.
- Named same-schema and governed multi-schema populations, local cross-section references, deterministic canonical writing, and semantic read-write-read equivalence.
- A reflection-free, dynamic-code-free package graph verified by a real `win-x64` Native AOT consumer publish/run.

## Explicit limits

- Physical anchor items, tags, all four occurrence-name categories, UUID anchor identity, and schema-neutral `REFERENCE` declarations can be read, edited, validated, canonically written, and reread; see the [conformance record](https://github.com/TedToolkit/TedToolkit.Step21/blob/main/docs/conformance/anchor-occurrence-uuid.md).
- External resource acquisition is opt-in through per-read capabilities; local fragments, external clear text, in-memory directories, ZIP roots/subsidiaries, UUID registry responses, and other-format conversion are supported. The legacy overload keeps unresolved-reference diagnostics, and no overload performs implicit I/O.
- Signature sections are decoded as detached CMS. Optional per-read verification accepts trust only through explicit time, signer-certificate revocation input, roots, additional or embedded certificates; `SignatureReports` retains results for every signed resource. Signed writing validates callback output against an independent canonical content snapshot and remains zero-output atomic on failure.
- The complex mapping profile is bounded to at most eight concrete leaves and 256 candidate combinations per root.
  Complete evaluated-set validity beyond that profile is not claimed because the supplied ISO 10303-11 files omit
  Annex B. Domain equivalence is explicit and schema-qualified; ISO 10303-22 repositories and inferred compatibility
  are outside the package contract.
- This is not a general EXPRESS interpreter: arbitrary algorithmic `RULE`/function bodies and cross-schema executable dependencies are outside the delivered subset. The package exposes no public parser context, raw syntax model, reader/writer façade, registry, or resolver.
- There is no JSON or XML serialization contract, extension hook, attribute model, or dependency.
- Writing is canonical and semantically equivalent; it is not byte-preserving and does not retain comments or original formatting.

The package ships `netstandard2.0` and `net8.0` runtime assets. Later .NET consumers select the `net8.0` asset. The analyzer is packaged as build-time infrastructure; analyzer implementation libraries do not become consumer runtime assets. Native AOT proof currently executes on `win-x64`, without implying that this is the only usable runtime identifier.
