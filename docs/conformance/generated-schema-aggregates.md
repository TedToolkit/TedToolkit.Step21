# Generated schema aggregates

The incremental generator projects EXPRESS `ARRAY`, `LIST`, `BAG`, and `SET` declarations to the matching schema-neutral runtime type. It never substitutes `List<T>`, discovers schema rules through reflection, or introduces a public aggregate factory.

| EXPRESS category | Generated C# type | Retained behavior |
| --- | --- | --- |
| `ARRAY` | `ExpressArray<T>` | Fixed declared index domain, assigned/unset slot state, `OPTIONAL` slots, and optional `UNIQUE` validation |
| `LIST` | `ExpressList<T>` | Order, multiplicity, lower/upper bounds, and optional `UNIQUE` validation |
| `BAG` | `ExpressBag<T>` | Multiplicity and lower/upper bounds without ordering semantics |
| `SET` | `ExpressSet<T>` | Candidate elements, lower/upper bounds, and uniqueness validation without silent duplicate removal |

Element types are resolved recursively. Nested aggregates remain nested runtime categories; entity elements use generated interfaces; SELECT elements use their generated closed union; imported elements are qualified to their declaring generated schema namespace. An outer EXPRESS `OPTIONAL` attribute makes the aggregate reference nullable. An `ARRAY OF OPTIONAL` keeps `T` as its element type because unset state belongs to `ExpressArray<T>` slots.

## Constraint ownership and editing

Generated properties and nominal defined-type wrappers expose the raw matching aggregate type. Bounds, `OPTIONAL`, `UNIQUE`, nesting, and original names remain in the bound schema metadata consumed by later generated descriptors. Generated XML documentation publishes the normalized exact EXPRESS aggregate form, including unbounded and symbolic bounds.

Consumers construct and edit the runtime aggregate candidates directly. There is no generated public factory and no reflection-readable constraint attribute. Collection/property mutations never run schema validation or repair a candidate. `Validate` provides on-demand aggregate feedback; later schema descriptors and read/write boundaries apply the authoritative schema constraints to the final state.

## Current boundary

This stage projects aggregate types and preserves their declaration evidence. It does not execute EXPRESS rules, hydrate Part 21 parameters, enumerate nested physical entity references, or serialize aggregates. Those behaviors belong to the descriptor, reference, validation, read, and write work items.

Fast generator tests compile and execute all four generated categories with identical candidate elements, bounded and unbounded declarations, optional array slots, inline nesting, entity/SELECT elements, symbolic bounds, nullable outer attributes, cross-schema qualification, and reordered/relocated additional files. They also assert the absence of general collection substitution, reflection, and dynamic code in generated sources.
