# ISO 10303-11:2004 Edition 2 grammar boundary

## Baseline

`src/grammar/Express.g4` recognizes the EXPRESS language defined by ISO 10303-11:2004 Edition 2. The edition is identified by the [ISO catalogue record](https://www.iso.org/standard/38047.html); production-level review uses the published production names and an independently maintained [machine-readable BNF transcription](https://github.com/IfcOpenShell/IfcOpenShell/blob/v0.8.0/src/ifcopenshell-python/ifcopenshell/express/express.bnf). Repository fixtures are original minimal examples, not copied normative examples.

The normative entry rule is `syntax`: one or more complete `schema_decl` forms followed by EOF. Keywords are case-insensitive. Simple and encoded strings, binary/integer/real literals, doubled apostrophes, nested embedded remarks, and tail remarks are recognized at the lexical boundary.

## Production traceability

| EXPRESS production family | Grammar boundary | Focused evidence |
| --- | --- | --- |
| Complete input, schemas, optional schema version, interfaces, and constants | `syntax`, `schemaDecl`, `schemaVersionId`, `schemaBody`, `referenceClause`, `useClause`, `constantDecl` | `edition2-and-lexical.exp`, `complete-declarations.exp` |
| Entities, inheritance, redeclarations, derived/inverse/unique/where clauses | `entityDecl` through `domainRule` | `complete-declarations.exp`, `complete-types.exp` |
| Edition 2 subtype constraints | `subtypeConstraintDecl`, `abstractSupertype`, `totalOver`, `supertypeExpression` | `complete-declarations.exp` |
| Defined, simple, aggregate, generalized, generic, enumeration, and select types | `typeDecl` through `namedTypes`, including `EXTENSIBLE`, `GENERIC_ENTITY`, `BASED_ON`, and `WITH` | `complete-declarations.exp`, `complete-types.exp` |
| Functions, procedures, rules, algorithm heads, and locals | `functionDecl`, `procedureDecl`, `ruleDecl`, `algorithmHead`, `localDecl` | `complete-declarations.exp`, `complete-expressions.exp` |
| Expressions, operators, aggregate initializers, intervals, queries, qualifiers, calls, and literals | `expression` through `unaryOp` | `complete-expressions.exp`, `edition2-and-lexical.exp` |
| All statement families | `stmt` through `skipStmt` | `complete-expressions.exp` |
| Invalid or incomplete input | `syntax` plus lexer/parser error listeners | `invalid-attribute-type.exp` and inline trailing-input tests |

## Syntax and binding boundary

EXPRESS uses the same physical identifier forms for several semantically distinct references. For example, `name(...)` can be a function call or entity constructor, and a bare or qualified name can denote an attribute, constant, parameter, variable, population, or enumeration. Choosing among them requires schema name binding and is not a context-free syntax decision.

The syntax IR therefore records these forms honestly as `namedApplication` and `namedReference`, retaining every token and half-open, 1-based source span. SBRT-008 resolves their EXPRESS name class. The grammar does not use semantic actions, reflection, or execution, and failed parsing publishes diagnostics without a partial IR.

## Verification boundary

Fast TUnit tests assert complete consumption, production-family preservation, exact source spelling, ordered spans, internal immutable IR, generated-visitor use, and atomic failure. Deterministic generation tests prove that `Express.g4` regenerates internal visitor artifacts without listeners and remains byte-stable. Syntax acceptance does not claim name/type correctness or executable semantics.
