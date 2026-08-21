# Generated schema values

The incremental generator maps scalar and nominal value declarations in the supplied closed EXPRESS schema set to strong C# values. These declarations are generated under `TedToolkit.Step21.Generated.<SchemaPascalCase>` and do not require runtime reflection, dynamic code, or a runtime reference to `TedToolkit.RoslynHelper`. Aggregate projection is documented separately in [generated schema aggregates](generated-schema-aggregates.md).

## Scalar mapping

| EXPRESS value | Generated/runtime C# value | Preserved distinction |
| --- | --- | --- |
| `INTEGER` | `System.Numerics.BigInteger` | Arbitrary precision |
| `REAL` | `RealValue` | Exact finite decimal significand and exponent |
| `NUMBER` | `NumberValue` | Strong INTEGER/REAL alternatives |
| `STRING` | `string` | Decoded Unicode text |
| `BINARY` | `BinaryValue` | Exact length, leading zeroes, and bits |
| `BOOLEAN` | `bool` | Two states |
| `LOGICAL` | `LogicalValue` | `False`, `Unknown`, and `True` |
| Entity reference | Generated entity interface | Declaring-schema type and runtime reference identity |

Every EXPRESS defined type produces a nominal `readonly record struct` whose `Value` property retains the mapped underlying type. A wrapper around another defined type retains that wrapper rather than collapsing to its underlying CLR value.

An EXPRESS enumeration produces a nominal `readonly record struct` with a string `Value` and generated static properties for every known symbol. A closed enumeration has no public value-taking constructor. An extensible enumeration has a public constructor that accepts canonical EXPRESS identifiers. In the closed supplied schema set, an extensible base also exposes symbols declared by its bound extensions.

An EXPRESS SELECT produces a sealed record and a companion `<Name>Kind` enum. The record exposes one typed `From<Alternative>` factory and `TryGet<Alternative>` method per alternative plus an exhaustive `Match<TResult>` method. Inherited alternatives and the bound extensions of an extensible base participate in this generated closed-set union.

For example:

```express
TYPE positive_count = INTEGER;
END_TYPE;
TYPE colour = ENUMERATION OF (red, green);
END_TYPE;
ENTITY item;
END_ENTITY;
TYPE choice = SELECT (positive_count, colour, item);
END_TYPE;
```

```csharp
var count = new PositiveCount(BigInteger.One);
var selected = Choice.FromPositiveCount(count);
var text = selected.Match(
    value => value.Value.ToString(),
    colour => colour.Value,
    item => item.ToString());
```

Generated-name collisions include entity interfaces/classes, defined values, SELECT companion enums, and enumeration members. A collision reports `STEP21EXP004` at the EXPRESS source and withholds the affected schema and its importing schemas atomically.

## Physical parameter bridge

The non-nested public `ParameterValue` union is the schema-neutral bridge used by later generated descriptors and the runtime writer. Its alternatives preserve:

- `$` and `*` as `Omitted` and `Derived` singleton values;
- arbitrary INTEGER and exact REAL values;
- decoded STRING, bit-accurate BINARY, BOOLEAN, and three-state LOGICAL values;
- untyped enumeration symbols and keyword-qualified typed parameters;
- resolved `Entity` instances by reference identity; and
- recursively nested aggregate parameters as an immutable snapshot.

The union exposes typed factories and `TryGet` methods. It has no `object` payload, public nested type, syntax node, parser context, resolver, or writer state. Its internal canonical formatter is covered by emit-and-reparse fixtures for every physical alternative, but canonical exchange-file writing remains a later work item.

## Current boundary

Schema-bound hydration, schema-rule validation, and public ISO 10303-21 writing remain later stages. Generated values therefore permit editable temporary states where the CLR representation allows them; later boundary validation is responsible for reporting all remaining schema violations before output.
