# Reachable EXPRESS rule execution

SBRT-017 attaches the static expression compiler to validation roots within the documented supported closure. A generated schema either executes every dependency in that accepted closure or is rejected with source-located `STEP21EXP006`; it never publishes a descriptor that silently skips a validation-relevant rule. This is not a claim that arbitrary EXPRESS algorithms or cross-schema executable dependencies are supported.

## Reachability roots

The generator starts from:

- entity `WHERE` rules, including inherited rules on every concrete subtype candidate;
- defined-type `WHERE` rules reached recursively through explicit entity attributes, aggregates, and SELECT alternatives;
- entity `UNIQUE` rules over the complete typed population;
- global `RULE ... FOR (...)` populations and their `WHERE` rules; and
- aggregate bounds reached through explicit entity attributes.

Rules on types that no generated entity attribute can reach remain source-located IR. Their functions and constants are not emitted, their constraint IDs are not documented as executable, and they do not create a public invocation facade.

## Executable closure

Reachable expressions pull in only the constants, single-value-return functions, and derived attributes they reference, recursively and in deterministic source order. `QUERY` variables and global RULE populations retain their generated strong types. Supported function and RULE bodies include locals, assignments, `IF`, `CASE`, `REPEAT`, `RETURN`, queries, model callbacks, and complex-entity construction; UNKNOWN propagation and branch-local type facts are preserved by the bound and generated flow. A reachable dependency cycle, unsupported result/operation, or unsupported executable cross-schema dependency stops the importing schema generation with source-located diagnostics. Global RULE populations may contain imported generated entity interfaces; this does not make imported rule bodies executable dependencies. Unrelated declarations remain IR-only.

Declarations, members, ordinary locals, assignments, `IF`/`CASE`, returns, and compound statements use RoslynHelper structural nodes. The pinned helper lacks a general loop, lambda, pattern/presence, and switch-expression node; reviewed custom fragments are limited to the whole-loop fragment for general EXPRESS `REPEAT`, necessary lambdas, and necessary pattern/presence/switch expressions. No additional whole-statement fragment family, source template, or `SyntaxFactory` path participates in reachable-rule emission.

Literal integer aggregate bounds and statically evaluable integer constant expressions using parentheses, unary signs, `+`, `-`, `*`, and non-negative integral `**` are normalized for generated structural metadata while the original source spelling remains in IR and XML. A validation-reachable boundary that is not statically representable rejects the schema atomically.

## Rule semantics and traceability

`WHERE` accepts only EXPRESS TRUE; FALSE, UNKNOWN, or an indeterminate result produces one failure at the governing entity, property, or schema-rule path. Global RULE populations include compatible subtype instances.

A validation-reachable entity-valued inverse is computed lazily from the complete ordered validation population. The generated descriptor matches the declared forward role, counts distinct compatible owner CLR identities, and supplies the strongly typed owner only when the count is exactly one. For a registered inverse source, a missing or multiply populated relationship emits one `<SCHEMA>.<DECLARING_ENTITY>.<INVERSE>.INVERSE_CARDINALITY` failure at the current registration path plus the generated inverse member. The message reports the role and actual distinct-owner count, and the source location points to the inverse declaration. If mutable graph edits leave the inverse source unregistered, the existing `P21.STRUCTURE.REFERENCE.REGISTRATION` evidence remains authoritative: the dependent rule is suppressed without inventing an inverse path or allowing an exception to escape. Resolution is cached per current entity reference and inverse declaration for one validation invocation, so repeated direct, cross-rule, and function-mediated access neither rescans nor duplicates the failure.

If an accessed singular inverse is unavailable, only the rule evaluation that depends on that value is suppressed; later independent rules continue. Existing boolean and control-flow lowering remains authoritative: a short-circuited or otherwise non-evaluated inverse operand performs no lookup, creates no cache entry, and emits no inverse failure. Aggregate-valued `SET`/`BAG` inverse behavior is unchanged. Singular inverse values remain private computed validation dependencies: no public inverse property, mutable storage, physical Part 21 parameter, hydration slot, writer projection, reflection, or dynamic lookup is generated.

`UNIQUE` compares keys with EXPRESS value semantics: LIST/ARRAY keys are order-sensitive, BAG/SET keys are multiplicity-aware and order-insensitive, SELECT payloads use their selected alternative's semantics, and entity references use CLR reference identity. A key containing an indeterminate optional value does not compare equal. Every duplicate after the first candidate produces a failure at that candidate's first key path; validation continues and retains other failures.

Named rules use their EXPRESS labels. Unnamed rules use deterministic one-based `RULE_n` labels. Generated XML and executable failures share the same uppercase codes:

- `<SCHEMA>.<ENTITY>.WHERE.<LABEL>`;
- `<SCHEMA>.<TYPE>.WHERE.<LABEL>`;
- `<SCHEMA>.<ENTITY>.UNIQUE.<LABEL>`; and
- `<SCHEMA>.<ENTITY>.<INVERSE>.INVERSE_CARDINALITY`; and
- `<SCHEMA>.RULE.<RULE>.WHERE.<LABEL>`.

Messages retain the normalized requirement. `SourceLocation` contains the EXPRESS file leaf name and 1-based line and column. Relocating otherwise identical input does not change generated code or constraint IDs.

## Public and execution boundary

All dependency helpers are private static generated methods on the existing sealed schema descriptor. There is no public function/procedure API, interpreter, evaluator context, registry, reflection discovery, dynamic code, or second validation engine. Validation remains side-effect-free and aggregate; property assignment and collection mutation remain unchecked.
