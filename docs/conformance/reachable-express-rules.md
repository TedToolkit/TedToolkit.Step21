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

`UNIQUE` compares keys with EXPRESS value semantics: LIST/ARRAY keys are order-sensitive, BAG/SET keys are multiplicity-aware and order-insensitive, SELECT payloads use their selected alternative's semantics, and entity references use CLR reference identity. A key containing an indeterminate optional value does not compare equal. Every duplicate after the first candidate produces a failure at that candidate's first key path; validation continues and retains other failures.

Named rules use their EXPRESS labels. Unnamed rules use deterministic one-based `RULE_n` labels. Generated XML and executable failures share the same uppercase codes:

- `<SCHEMA>.<ENTITY>.WHERE.<LABEL>`;
- `<SCHEMA>.<TYPE>.WHERE.<LABEL>`;
- `<SCHEMA>.<ENTITY>.UNIQUE.<LABEL>`; and
- `<SCHEMA>.RULE.<RULE>.WHERE.<LABEL>`.

Messages retain the normalized requirement. `SourceLocation` contains the EXPRESS file leaf name and 1-based line and column. Relocating otherwise identical input does not change generated code or constraint IDs.

## Public and execution boundary

All dependency helpers are private static generated methods on the existing sealed schema descriptor. There is no public function/procedure API, interpreter, evaluator context, registry, reflection discovery, dynamic code, or second validation engine. Validation remains side-effect-free and aggregate; property assignment and collection mutation remain unchecked.
