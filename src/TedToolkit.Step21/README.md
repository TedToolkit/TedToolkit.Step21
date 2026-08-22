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

For an EXPRESS schema named `catalog`, generated types are placed in `TedToolkit.Step21.Generated.Catalog`, with its descriptor available as `SchemaDescriptor.Instance`.

## Read, navigate, edit, validate, and write

```csharp
using TedToolkit.Step21;
using TedToolkit.Step21.Generated.Catalog;
using CatalogSchemaDescriptor = TedToolkit.Step21.Generated.Catalog.SchemaDescriptor;

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

Use `structure.WriteEntity(writer, entity)` when only one registered entity-instance record is required. Both write operations validate the final graph before producing output.

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
- Closed-set schema binding, generated scalar/nominal/SELECT/aggregate/entity types, descriptor-based hydration/projection, and validation-reachable static EXPRESS execution.
- Simple mappings plus the documented supported flat `ANDOR` complex mapping, including inherited/redeclared components.
- Named same-schema and governed multi-schema populations, local cross-section references, deterministic canonical writing, and semantic read-write-read equivalence.
- A reflection-free, dynamic-code-free package graph verified by a real `win-x64` Native AOT consumer publish/run.

## Explicit limits

- Anchors, external resource acquisition/resolution, and signature verification are syntactically recognized but return explicit unsupported evidence when their operation is required. A declared external entity occurrence used by a model is reported specifically as `P21.READ.REFERENCE.EXTERNAL`.
- Complex mappings outside the documented flat `ANDOR` form and SDAI domain-equivalence metadata are unsupported.
- This is not a general EXPRESS interpreter and exposes no public parser context, raw syntax model, reader/writer façade, registry, or resolver.
- There is no JSON or XML serialization contract, extension hook, attribute model, or dependency.
- Writing is canonical and semantically equivalent; it is not byte-preserving and does not retain comments or original formatting.

The package targets .NET 10. The analyzer is packaged as build-time infrastructure; analyzer implementation libraries do not become consumer runtime assets. Native AOT proof currently executes on `win-x64`, without implying that this is the only usable runtime identifier.
