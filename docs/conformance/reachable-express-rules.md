# Reachable EXPRESS rule execution

SBRT-017 attaches the static expression compiler to validation roots. A generated schema either executes every dependency in its accepted reachable closure or is rejected with source-located `STEP21EXP006`; it never publishes a descriptor that silently skips a validation-relevant rule.

## Reachability roots

The generator starts from:

- entity `WHERE` rules, including inherited rules on every concrete subtype candidate;
- defined-type `WHERE` rules reached recursively through explicit entity attributes, aggregates, and SELECT alternatives;
- entity `UNIQUE` rules over the complete typed population;
- global `RULE ... FOR (...)` populations and their `WHERE` rules; and
- aggregate bounds reached through explicit entity attributes.

Rules on types that no generated entity attribute can reach remain source-located IR. Their functions and constants are not emitted, their constraint IDs are not documented as executable, and they do not create a public invocation facade.

## Executable closure

Reachable expressions pull in only the constants, single-value-return functions, and derived attributes they reference, recursively and in deterministic source order. `QUERY` variables and global RULE populations retain their generated strong types. Dependency cycles, mandatory dependency results that can be indeterminate, unsupported generated result types, algorithmic function/RULE bodies, model-traversal operations without a generated model callback, inverse navigation, complex-entity construction, and executable dependencies crossing a schema-import boundary stop the importing schema generation with `STEP21EXP006` at the EXPRESS source. Cross-schema population execution is owned by SBRT-021. Unrelated declarations remain IR-only.

Literal integer aggregate bounds and statically evaluable integer constant expressions using parentheses, unary signs, `+`, `-`, `*`, and non-negative integral `**` are normalized for generated structural metadata while the original source spelling remains in IR and XML. A validation-reachable boundary that is not statically representable rejects the schema atomically.

## Rule semantics and traceability

`WHERE` accepts only EXPRESS TRUE; FALSE, UNKNOWN, or an indeterminate result produces one failure at the governing entity, property, or schema-rule path. Global RULE populations include compatible subtype instances.

`UNIQUE` compares keys with EXPRESS value semantics: LIST/ARRAY keys are order-sensitive, BAG/SET keys are multiplicity-aware and order-insensitive, SELECT payloads use their selected alternative's semantics, and entity references use CLR reference identity. A key containing an indeterminate optional value does not compare equal. Every duplicate after the first candidate produces a failure at that candidate's first key path; validation continues and retains other failures.

Named rules use their EXPRESS labels. Unnamed rules use deterministic one-based `RULE_n` labels. Generated XML and executable failures share the same uppercase codes:

- `<SCHEMA>.<ENTITY>.WHERE.<LABEL>`;
- `<SCHEMA>.<TYPE>.WHERE.<LABEL>`;
- `<SCHEMA>.<ENTITY>.UNIQUE.<LABEL>`; and
- `<SCHEMA>.RULE.<RULE>.WHERE.<LABEL>`.

Messages retain the normalized requirement. `SourceLocation` contains the EXPRESS file leaf name and 1-based line and column. Relocating otherwise identical input does not change generated code or constraint IDs.

## Public and execution boundary

All dependency helpers are private static generated methods on the existing sealed schema descriptor. There is no public function/procedure API, interpreter, evaluator context, registry, reflection discovery, dynamic code, or second validation engine. Validation remains side-effect-free and aggregate; property assignment and collection mutation remain unchecked.
