# Complex mapping round trip

Generated schema descriptors map supported subtype values without reflection through the existing ordered
`IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>>` contract. No public mapping DTO, context, factory,
or generated class base is added.

## Physical mapping

ISO 10303-21:2016 clause 12.2.5 selects the form from the evaluated-set member's leaf count:

- exactly one leaf uses one internal record named for that leaf, with the complete effective explicit-attribute
  sequence; and
- multiple leaves use external mapping with every partial entity value in the member, sorted by uppercase entity name
  using ordinal character order.

Each external component contains only the explicit physical slots introduced by that entity declaration. A renamed
explicit redeclaration has no extra physical parameter: the value remains in the component that originally introduced
the slot, while the old and renamed generated properties address that same storage. A shared ancestor occurs once, and
an entity with no local physical slots remains present as `ENTITY_NAME()`.

The supported multi-leaf scope is a flat top-level `ANDOR` of two to eight factors, each naming a direct concrete
subtype of the common declaring entity. Every multi-factor subset receives an internal sealed synthetic entity class
implementing the existing public leaf interfaces. The class is an implementation detail, so this adds no public entity,
projection, context, or factory type. Missing, repeated, additional, or nonascending components are rejected during
binding. Derived redeclarations (which require `*` plus evaluated derived semantics), larger or nested evaluated-set
expressions, and combinations whose generated property names cannot represent distinct physical storage are outside
this boundary and are rejected rather than guessed.

## Canonical writing and semantic comparison

Generated projection retains the clause-required internal form for a named single-leaf class and uses external mapping
for an internally allocated multi-leaf value. `Write` and `WriteEntity` format multiple component records inside one
parenthesized subsuper record and buffer the complete result before touching the supplied destination.

The conformance fixture covers a single-leaf internal value and flat-`ANDOR` multi-leaf values, explicit renamed
redeclaration, empty/non-empty partial values, OPTIONAL absence, aggregates, references, and BOOLEAN/LOGICAL values. It
writes and rereads each supported form, then compares occurrence identities, exact generated CLR types, ordered
component names, strong values, aggregate order, and normalized reference targets. Source formatting and record order
are deliberately excluded from semantic equality.
