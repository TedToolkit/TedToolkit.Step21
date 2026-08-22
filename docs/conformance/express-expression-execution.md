# EXPRESS expression execution

## Status and authority

This inventory records the completed SBRT-016 expression compiler. ISO 10303-11:2004, clauses 12 and 15, is the semantic authority; the grammar families are traced to `src/grammar/Express.g4`. “Complete” means immutable typed binding, static C# generation, generated-code compilation, positive behavior, and applicable UNKNOWN/indeterminate/error behavior pass. Attaching these leaves to reachable rules remains SBRT-017.

## Expression-family inventory

| Family | Static execution evidence | Status |
| --- | --- | --- |
| INTEGER, REAL, BOOLEAN, LOGICAL, STRING, BINARY literals; `E`, `PI`, `?` | `BindingTests`; scalar execution; valid and invalid encoded UCS cases | Complete |
| Unary `+`, `-`, `NOT`; arithmetic `+`, `-`, `*`, `/`, `DIV`, `MOD`, `**` | Exact INTEGER/NUMBER/REAL cases; divisor-sign DIV/MOD; zero/domain/overflow negative cases | Complete |
| Logical `AND`, `OR`, `XOR` | Full three-state execution, nullable BOOLEAN/LOGICAL operands, and short truth-table cases | Complete |
| Value and instance comparisons; `IN`; intervals; `LIKE` | Numeric and enumeration order; entity value-versus-instance identity; optional ARRAY UNKNOWN; complete LIKE token table | Complete |
| STRING/BINARY concatenation, index, and slice | Both value families plus invalid, out-of-range, and indeterminate bounds | Complete |
| Aggregate initializer and repetition | Contextual BAG/SET/LIST/ARRAY construction, empty values, sparse optional ARRAY, repeated values, and invalid dynamic count | Complete |
| Aggregate union, intersection, difference, subset, and superset | LIST order, BAG multiplicity, SET uniqueness, subset/superset, and equality fixtures | Complete |
| ARRAY/LIST/BAG/SET indexing and category restrictions | ARRAY optional slots and declared indices; variable aggregate positions; STRING/BINARY-only slices | Complete |
| Attribute, group, enumeration, `SELF`, entity construction, and complex construction | Direct generated navigation/construction; static SELF/model callbacks; source-located missing-context failures | Complete — SBRT-023 supplies the complex mapper callback |
| `QUERY` over ARRAY/LIST/BAG/SET | Category/bounds matrix; LIST order; BAG multiplicity; SET uniqueness; ARRAY index-domain and sparse-slot preservation | Complete |
| Function/constant/local/parameter/query references and applications | Direct strongly typed resolver calls, defined-value unwrapping, nominal storage, and entity constructor execution | Complete |

## Built-in inventory

| Built-ins | Static execution evidence | Status |
| --- | --- | --- |
| `ABS`, `ACOS`, `ASIN`, `ATAN`, `COS`, `EXP`, `LOG`, `LOG2`, `LOG10`, `SIN`, `SQRT`, `TAN` | Complete generated positive matrix plus domain, non-finite, and indeterminate cases | Complete |
| `BLENGTH`, `LENGTH` | BINARY/STRING generated length execution and lifted indeterminate input | Complete |
| `EXISTS`, `NVL`, `ODD` | Nullable present/missing matrix and QUERY predicate execution | Complete |
| `HIBOUND`, `HIINDEX`, `LOBOUND`, `LOINDEX`, `SIZEOF` | ARRAY/BAG/LIST/SET declared-bound/current-index table, including unbounded HIBOUND | Complete |
| `FORMAT` | ISO symbolic, picture, and default examples; rounding, signs, grouping, and invalid runtime format | Complete |
| `ROLESOF`, `TYPEOF`, `USEDIN` | Strong result types and generated static model-operation calls with source-located missing-context errors | Complete — SBRT-017 supplies the model traversal callbacks |
| `VALUE` | Exact INTEGER/REAL and invalid-to-indeterminate generated execution | Complete |
| `VALUE_IN`, `VALUE_UNIQUE` | Present/absent values, duplicates, and optional ARRAY unresolved-slot truth table | Complete |

## Normative edge decisions

- Source locations use only file path plus 1-based line and column.
- `/`, invalid mathematical domains, non-finite approximations, invalid repetition counts, invalid `FORMAT`, invalid `VALUE`, and invalid indices produce the EXPRESS indeterminate value rather than leaking CLR exceptions.
- `DIV` and `MOD` use the divisor-sign remainder rule, including negative operands.
- Aggregate indexing accepts a second index only as the same single position; only STRING and BINARY use true slices.
- `HIBOUND` reads the declared upper bound, while `HIINDEX` reads the ARRAY upper index or current BAG/LIST/SET cardinality. `LOBOUND` reads the declared lower bound and `LOINDEX` is the ARRAY lower index or one for BAG/LIST/SET.
- ARRAY `QUERY` preserves its declared index domain and represents rejected or absent positions as unset optional slots. Variable-size query results use lower bound zero and preserve the source upper bound.
- Generated model callbacks are static C# spellings, not runtime registration or interpretation APIs. SBRT-017 owns reachable-rule/model attachment; SBRT-023 owns material complex-entity composition.

## Execution boundary

Expression IR and generation are Analyzer-internal. Generated code uses ordinary static C# and the schema-derived strong runtime values. There is no public evaluator, expression tree, registration context, reflection discovery, dynamic dispatch, `System.Linq.Expressions` compilation, or second validation generator. SBRT-017 owns attachment to validation/writing roots, declaration dependency closure, and named aggregate validation failures.
