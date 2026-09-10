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

Reachable expressions pull in only the constants, single-value-return functions, and derived attributes they reference, recursively and in deterministic source order. `QUERY` variables and global RULE populations retain their generated strong types. Supported function bodies include locals, lexical constants, assignments, `IF`, `CASE`, `REPEAT`, `RETURN`, queries, model callbacks, supported complex-entity construction, and the local LIST procedures described below; UNKNOWN propagation and branch-local type facts are preserved by the bound and generated flow. Global RULE algorithm statements remain unsupported and reject generation rather than being skipped. A reachable dependency cycle, unsupported result/operation, or unsupported executable cross-schema dependency stops the importing schema generation with source-located diagnostics. Global RULE populations may contain imported generated entity interfaces; this does not make imported rule bodies executable dependencies. Unrelated declarations remain IR-only.

`INSERT` and `REMOVE` accept direct function-local LIST variables, including nominal LIST types and closed generic element types. INSERT places an element after position P (zero prepends); REMOVE uses one-based positions. These operations and their valid-position preconditions follow clause 16 of ISO 10303-11:1994, as reproduced without technical changes in [JIS B 3700-11-1996](https://kikakurui.com/b3/B3700-11-2002-01.html). This narrow source citation does not replace the repository's EXPRESS edition baseline or claim arbitrary procedure support. Constant mutation is rejected during binding; qualified mutation targets and user-defined procedures remain outside this lowering. Invalid positions are not clamped or ignored. The implemented mandatory-value path propagates an indeterminate argument as an indeterminate enclosing function result; it does not claim a general model for procedures with unknown arguments.

LIST/BAG/SET assignment and function-return lowering share container-copy logic, retain nominal wrappers, and preserve entity reference identity. Fresh aggregate literals avoid a redundant copy. `ListProcedureTests` exercises insertion/removal positions, numeric and entity/SELECT elements, generic and nominal LIST copies, constant protection, indeterminate arguments, and read/write/reread validation. These focused cases are compiler evidence, not AP242 interoperability evidence.

Within a supported REPEAT, `ESCAPE` exits the nearest loop directly; `SKIP` transfers to its post-body control point, still evaluating UNTIL before the next increment/WHILE check (ISO 10303-11:1994, clauses 13.6 and 13.11, in the JIS reproduction above). Transfers outside REPEAT reject generation. Loop-owned jumps remain inside the existing whole-loop emission boundary. Their branch facts join separately at the tail or exit, so a skipped assignment cannot establish determinacy for UNTIL. `CombinedRepeatTests` covers direct, conditional, CASE and nested transfers, combined and conditional controls, unknown-value paths and out-of-loop rejection.

Clause 13.1 null statements execute without an action. Clause 13.2 ALIAS statements introduce a nested lexical name for a variable or parameter and retain the qualified target's inferred type; the name is unavailable after `END_ALIAS`. The current static lowering covers direct targets, entity-group and attribute qualification, and single-element ARRAY/LIST index qualification, including nested aliases, without allocating a runtime alias object. Constants are rejected as ALIAS sources during binding. Range-alias behavior is absent from the authorized Edition 2 files and is therefore source-excluded rather than inferred or claimed.

ISO 10303-11:2004 clause 13.4 CASE selection uses source order and selects only the first label whose value equality is TRUE. FALSE, UNKNOWN and indeterminate label comparisons do not select an action; an indeterminate selector reaches `OTHERWISE`, or falls through when `OTHERWISE` is absent. The static emitter evaluates the selector once, short-circuits labels in source order and retains the unmatched path even for a closed-enumeration label set when the selector can be indeterminate.

Declarations, members, ordinary locals, assignments, `IF`/`CASE`, returns, and compound statements use RoslynHelper structural nodes. The pinned helper lacks a general loop, lambda, pattern/presence, and switch-expression node; reviewed custom fragments are limited to the whole-loop fragment for general EXPRESS `REPEAT`, necessary lambdas, and necessary pattern/presence/switch expressions. No additional whole-statement fragment family, source template, or `SyntaxFactory` path participates in reachable-rule emission.

Literal integer aggregate bounds and statically evaluable integer constant expressions using parentheses, unary signs, `+`, `-`, `*`, and non-negative integral `**` are normalized for generated structural metadata while the original source spelling remains in IR and XML. A validation-reachable boundary that is not statically representable rejects the schema atomically.

## Rule semantics and traceability

`WHERE` accepts only EXPRESS TRUE; FALSE, UNKNOWN, or an indeterminate result produces one failure at the governing entity, property, or schema-rule path. Global RULE populations include compatible subtype instances.

Generated SELECT values keep one shared payload reference per instance, independent of the number of alternatives; their public factories, `TryGet` members, and `Match` behavior are unchanged. Validation-reachable `TYPEOF` calls reuse one private dispatch helper per SELECT declaration instead of repeating an alternative-wide dispatch at every call site. These are schema-neutral representation and code-size boundaries: they do not remove alternatives, change EXPRESS type membership, add reflection, or expose a new public API.

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
