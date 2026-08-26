# ADR-0007: Keep separate lexer and parser grammars for Part 21

- Status: Accepted
- Date: 2026-08-26
- Decision owner: repository maintainer
- Decision scope: long-lived organization of the ANTLR grammar boundary used to recognize ISO 10303-21 clear-text exchange structures
- Applicable product intent: [`../product/README.md`](../product/README.md)
- Applicable principles: [`AP-001` and `AP-003`](../principles/architecture.md); [`EP-001`](../principles/engineering.md)
- Supersedes: None
- Superseded by: None

## 📌 Decision at a glance

Maintain one target-independent lexer grammar and one target-independent parser grammar for Part 21 while contextual URI and signature tokenization requires ANTLR lexer modes; one public read capability does not imply one grammar source.

## 🧭 Context and decision question

Part 21 exchange-structure recognition is exposed through one public read operation, but recognition has two distinct language-processing responsibilities:

```text
characters
  -> lexer grammar: default, URI, and signature tokenization contexts
  -> token stream
  -> parser grammar: section order, nesting, cardinality, and EOF
  -> internal syntax transformation
```

The Edition 3 syntax contains two regions whose characters must be tokenized according to their local context:

- A `<` delimiter enters a URI context. Characters inside it follow RFC 2396-oriented rules and overlap with ordinary keywords, names, and punctuation outside that context. The closing `>` restores ordinary tokenization.
- `SIGNATURE` enters a signature context. Its RFC 4648 Base64 payload, including `=` padding, must be recognized as signature content rather than ordinary identifiers and punctuation. Completion of that payload restores ordinary tokenization so `ENDSEC` can be recognized normally.

The lexer expresses these state transitions through `UriMode`, `SignatureMode`, `pushMode`, and `popMode`. The currently pinned ANTLR 4.13.1 toolchain permits mode specifications only in a lexer grammar, not in a combined grammar. The parser therefore consumes the separately generated lexer vocabulary through `tokenVocab`.

This ADR decides whether the Part 21 boundary retains this split or is reorganized into one combined `.g4` merely because both stages serve the same public operation.

## 📍 Scope and non-goals

This decision governs only the ISO 10303-21 runtime grammar. It does not require every grammar in the repository to be split: a language without contextual lexer modes may continue to use a combined grammar. In particular, this decision does not require the EXPRESS grammar to adopt the Part 21 source organization.

This decision does not expose ANTLR types publicly, add another read operation, require a second parse of the input, or claim operational URI resolution or signature verification. It governs syntactic recognition and generated-parser organization only.

## 🎯 Decision drivers and constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | Recognize the declared ISO 10303-21 Edition 3 clear-text syntax, including contextual RFC 2396 URI and RFC 4648 signature forms | [`part21-edition3-grammar.md`](../conformance/part21-edition3-grammar.md); documented repository conformance boundary | Must |
| Hard constraint | The pinned ANTLR 4.13.1 toolchain permits mode specifications only in lexer grammars, not combined grammars | [`Directory.Packages.props`](../../Directory.Packages.props), [ANTLR grammar structure](https://github.com/antlr/antlr4/blob/4.13.1/doc/grammars.md), and [lexer modes](https://github.com/antlr/antlr4/blob/4.13.1/doc/lexer-rules.md#lexical-modes); documented local version and upstream behavior | Must |
| Hard constraint | Grammar behavior remains clause-backed; syntax acceptance is not weakened or moved into an undocumented compatibility path | [`EP-001`](../principles/engineering.md); active repository principle | Must |
| Hard constraint | Generated lexer, parser, and visitor types remain internal implementation infrastructure rather than public domain concepts | [`AP-003`](../principles/architecture.md) and [`schema-bound-round-trip.md`](../architecture/schema-bound-round-trip.md); active architecture | Must |
| Decision driver | Keep contextual lexing declarative and independent of C#-specific embedded actions | [`STEPLexer.g4`](../../src/grammar/STEPLexer.g4) and [`STEPParser.g4`](../../src/grammar/STEPParser.g4); observed current source with no target-language actions | High |
| Decision driver | Preserve distinguishable lexical and syntactic diagnostics at the atomic read boundary | [`ExchangeStructureSyntaxParser.cs`](../../src/TedToolkit.Step21/Syntax/ExchangeStructureSyntaxParser.cs); observed current boundary | High |
| Decision driver | Preserve deterministic internal parser generation | [`../../README.md`](../../README.md) and [`part21-edition3-grammar.md`](../conformance/part21-edition3-grammar.md); documented repository guarantee | High |
| Decision driver | Avoid treating source-file count as a runtime or package optimization | ANTLR generates both a lexer and a parser from a combined grammar; [documented ANTLR 4.13.1 behavior](https://github.com/antlr/antlr4/blob/4.13.1/doc/tool-options.md#-xsave-lexer) | Medium |

## 🔎 Options and evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Separate lexer and parser grammars using a shared token vocabulary | Documented: ANTLR supports modes in lexer grammars; observed: both contextual modes map directly to the declared Edition 3 boundary; high confidence | Yes | Maintainers navigate and coordinate two authoritative grammar sources for one read capability | Selected |
| One combined grammar retaining equivalent lexer modes | Documented: ANTLR 4 rejects mode specifications in combined grammars; high confidence | No | The requested source shape cannot express the selected contextual lexer design | Rejected |
| One combined grammar with flattened, broadly applicable tokens | Assumed feasible only after redesign; medium confidence | Unproven | Removes modes by increasing lexical overlap and shifts context discrimination into more complex parser productions or later validation | Rejected |
| One combined grammar with embedded target-language actions or a custom lexer | Documented ANTLR extension mechanisms make contextual behavior feasible; medium confidence | Partially | Couples the authoritative grammar to implementation code and expands the target-specific correctness surface | Rejected |
| Move URI or Base64 checks out of syntax recognition | Assumed feasible as post-parse validation; high confidence | No | Would accept lexically invalid text past the governed syntax boundary and weaken the meaning of successful parsing | Rejected |
| Replace ANTLR recognition with a hand-written reader | Assumed feasible; low confidence without a proof of concept | Unproven | Replaces the clause-traced grammar and regeneration boundary; source-file count alone supplies no compensating correctness or maintenance benefit | Rejected |

## ✅ Decision

Part 21 recognition uses one authoritative lexer grammar and one authoritative parser grammar.

The lexer owns character-level rules, ignored controls, token identity, and every contextual lexical mode. It enters and exits URI and signature contexts declaratively through ANTLR lexer commands. The parser consumes the lexer's authoritative token vocabulary and owns exchange-structure ordering, nesting, cardinality, and complete-input recognition through its start rule and EOF.

Both grammars remain target-independent: contextual behavior is expressed through ANTLR grammar constructs rather than embedded C# actions. They form one parsing capability and one ordered pipeline, not duplicate parsers or separate product features.

The number of authoritative grammar sources is not a runtime, package-size, or public-API optimization target. A combined grammar would still generate a lexer and parser and would not remove the ANTLR runtime dependency. Any proposal to combine or replace the sources must be justified by a material benefit and prove equivalent conformance rather than relying on a lower file count.

Acceptance approves this direction; it does not by itself claim delivery. The current sources and conformance record linked below are repository evidence that the present baseline already follows the decision. Future changes remain responsible for proving continued conformance.

## 💡 Why this decision now

The separate sources are easy to misread as duplicated parsing or evidence of two public use cases. That interpretation can motivate a merge that removes the declarative mechanism currently handling URI and signature lexical context. Recording the boundary makes the reason discoverable without requiring a maintainer to reconstruct ANTLR restrictions, inspect generated output, or recover delivery history.

The decision conforms to AP-001 because the split is driven by ISO 10303-21 syntax rather than an application protocol. It conforms to AP-003 because both recognizers remain internal implementation infrastructure. It conforms to EP-001 because contextual tokenization remains part of the clause-backed syntax boundary with focused conformance evidence. No principle exception is accepted.

Reconsideration requires changed evidence: a supported parser generator capability that permits the same declarative design in one grammar, a standards or conformance-scope change that removes contextual tokenization, or a proven alternative that supplies a material correctness, maintenance, or performance benefit while preserving every exit requirement.

## 🔗 Evidence and links

- [ANTLR 4.13.1 grammar structure](https://github.com/antlr/antlr4/blob/4.13.1/doc/grammars.md)
- [ANTLR 4.13.1 lexer rules and modes](https://github.com/antlr/antlr4/blob/4.13.1/doc/lexer-rules.md#lexical-modes)
- [ANTLR 4.13.1 tool behavior for combined grammars](https://github.com/antlr/antlr4/blob/4.13.1/doc/tool-options.md#-xsave-lexer)
- [`Directory.Packages.props`](../../Directory.Packages.props)
- [`STEPLexer.g4`](../../src/grammar/STEPLexer.g4)
- [`STEPParser.g4`](../../src/grammar/STEPParser.g4)
- [`part21-edition3-grammar.md`](../conformance/part21-edition3-grammar.md)
- [`schema-bound-round-trip.md`](../architecture/schema-bound-round-trip.md)
- [`../../README.md`](../../README.md)

No performance claim determines this decision, so benchmark evidence is not required.

## ⚖️ Consequences and accepted trade-offs

- Maintainers work with two `.g4` sources for the single Part 21 read capability.
- Lexical context and syntactic structure have explicit ownership rather than competing rules in one source.
- URI and signature recognition stays declarative and target-independent.
- Generation resolves the lexer token vocabulary before generating the parser.
- A grammar change that crosses the token boundary may require coordinated edits to both sources.
- Lexical and syntactic failures remain separately attributable while contributing to one atomic read result.
- Consumers receive no additional public API, parsing entry point, runtime stage, or domain concept because of the source split.
- Combining the sources would not by itself remove generated lexer/parser types, the token stream, or the ANTLR runtime dependency.
- The EXPRESS grammar remains free to use a combined grammar because this decision is driven by Part 21's contextual modes, not a repository-wide preference for multiple files.

## 🛠️ Downstream delivery constraints

- Contextual lexical regions are represented in a lexer grammar through target-independent grammar constructs rather than target-language actions.
- The parser grammar consumes the authoritative lexer token vocabulary and owns the complete exchange-structure start rule.
- Lexical and syntactic failures remain distinguishable at the atomic read boundary.
- Generated recognizers and visitors remain internal and reproducible; generated sources are never edited as grammar authority.
- Grammar changes retain clause-level evidence and focused positive and negative conformance fixtures.
- Syntactic recognition remains distinct from URI resolution, signature verification, and other optional operational semantics.
- No public parser context or raw ANTLR type is introduced by this separation.

These constraints govern outcomes, not private rule names or source layout beyond the required lexer/parser boundary.

## 🔄 Exit requirements

A replacement must preserve the declared Edition 3 syntax boundary, contextual URI and Base64 recognition, complete-input enforcement, deterministic internal generated artifacts, target independence, and distinguishable lexical and syntactic diagnostics. It must keep syntax recognition separate from optional operational semantics and demonstrate a material benefit beyond reducing the count of grammar source files.

## 📅 Follow-ups and review triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Reassess combined-grammar feasibility | repository maintainer | The supported ANTLR toolchain permits mode specifications in combined grammars | Open |
| Reassess contextual tokenization | repository maintainer | The supported Part 21 standard or declared conformance scope no longer requires URI or signature lexical contexts | Open |
| Reassess target independence | repository maintainer | A target-specific grammar action becomes necessary and receives an explicit architecture exception | Open |
| Reassess parser technology | repository maintainer | ANTLR is replaced or an alternative demonstrates a material correctness, maintenance, or performance benefit against representative evidence | Open |
