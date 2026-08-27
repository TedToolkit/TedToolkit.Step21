# Generated schema aggregates

The incremental generator projects EXPRESS `ARRAY`, `LIST`, `BAG`, and `SET` declarations to the matching schema-neutral runtime type. It never substitutes `List<T>`, discovers schema rules through reflection, or introduces a public aggregate factory.

| EXPRESS category | Generated C# type | Retained behavior |
| --- | --- | --- |
| `ARRAY` | `ExpressArray<T>` | Fixed declared index domain, assigned/unset slot state, `OPTIONAL` slots, and optional `UNIQUE` validation |
| `LIST` | `ExpressList<T>` | Order, multiplicity, lower/upper bounds, and optional `UNIQUE` validation |
| `BAG` | `ExpressBag<T>` | Multiplicity and lower/upper bounds without ordering semantics |
| `SET` | `ExpressSet<T>` | Candidate elements, lower/upper bounds, and uniqueness validation without silent duplicate removal |

Each concrete runtime aggregate also implements one matching covariant, read-only view:
`IExpressArray<out T>`, `IExpressList<out T>`, `IExpressBag<out T>`, or `IExpressSet<out T>`.
The views retain the kind-specific bounds and flags required to observe EXPRESS semantics, expose validation,
and contain no member that accepts `T` or mutates the collection.

Element types are resolved recursively. Nested aggregates remain nested runtime categories; entity elements use generated interfaces; SELECT elements use their generated closed union; imported elements are qualified to their declaring generated schema namespace. An outer EXPRESS `OPTIONAL` attribute makes the aggregate reference nullable. An `ARRAY OF OPTIONAL` keeps `T` as its element type because unset state belongs to `ExpressArray<T>` slots.

## Constraint ownership and editing

Generated properties and nominal defined-type wrappers expose the raw matching aggregate type. Bounds, `OPTIONAL`, `UNIQUE`, nesting, and original names remain in the bound schema metadata consumed by generated descriptors. Generated XML documentation publishes the normalized exact EXPRESS aggregate form, including unbounded and symbolic bounds.

Consumers construct and edit the runtime aggregate candidates directly. There is no generated public factory and no reflection-readable constraint attribute. Collection/property mutations never run schema validation or repair a candidate. `ExchangeStructure.Validate()` provides on-demand structural and validation-reachable EXPRESS feedback through directly generated descriptor code; read/write boundaries apply the same final-state constraints.

For a supported same-kind explicit attribute specialization whose entity element type narrows to a subtype,
the concrete generated property remains the narrow mutable aggregate. Every inherited interface getter returns
that same object through the matching covariant view. Bounds, ARRAY `OPTIONAL`/`UNIQUE`, LIST `UNIQUE`, order,
multiplicity, slot state, descriptor projection, validation, and the single physical Part 21 parameter are not copied
or reinterpreted. Kind, metadata, nested aggregate, or non-entity element changes remain explicitly unsupported.

## Current boundary

Aggregate projection preserves every declaration form. Generated entity `DirectReferences` expands nested aggregate/SELECT containers into live one-level physical entity occurrences. The [generated descriptor boundary](generated-schema-descriptors.md) hydrates and projects supported nested aggregates in simple and flat-`ANDOR` complex mappings, while the [structural validation boundary](structural-validation.md) recursively validates literal shape, statically evaluable bounds, slots, uniqueness, and element compatibility. Parsed occurrence hydration and canonical exchange-file writing retain aggregate order and multiplicity. Unsupported validation-reachable dynamic bounds or execution reject generation instead of publishing a validation gap.

Fast generator tests compile and execute all four generated categories with identical candidate elements, bounded and unbounded declarations, optional array slots, inline nesting, entity/SELECT elements, symbolic bounds, nullable outer attributes, cross-schema qualification, and reordered/relocated additional files. They also assert the absence of general collection substitution, reflection, and dynamic code in generated sources.
