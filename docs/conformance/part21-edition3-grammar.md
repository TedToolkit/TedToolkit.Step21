# ISO 10303-21:2016 Edition 3 grammar boundary

## Supported syntax

`STEPLexer.g4` and `STEPParser.g4` recognize the complete clear-text exchange-structure syntax declared by ISO 10303-21:2016 Edition 3. The authoritative entry rule is `exchangeFile`; it consumes one header, optional anchor and reference sections, zero or more data sections, the closing delimiter, zero or more signature sections, and EOF in that order.

This is a syntactic boundary only. Parsing an anchor, external reference, signature, archive-related URI, or user-defined record does not claim resolution, verification, archive handling, ECMAScript execution, schema binding, or any other operational behavior.

Normative clause navigation uses the publicly accessible [Edition 3 final text](https://www.steptools.com/stds/step/IS_final_p21e3.html). Tables 1–4 and clauses 5.2–6.5 are the evidence for the grammar; RFC 2396 and RFC 4648 are the referenced definitions for URI and Base64 tokens. Repository fixtures are original, minimal examples rather than copies of normative examples.

## Production traceability

| Normative production | Normative location | Grammar realization | Focused evidence |
| --- | --- | --- | --- |
| `EXCHANGE_FILE` | Table 3; 5.3, 5.5 | `exchangeFile` including `EOF` | `edition3-all-sections.p21`, `edition3-no-data-signatures.p21`, `trailing-input.p21`, `section-order.p21` |
| `HEADER_SECTION`, `HEADER_ENTITY_LIST`, `HEADER_ENTITY` | Table 3; 8.1–8.3 | `headerSection`, the three named header rules, `headerEntity` repetition | all valid files, `header-entity-order.p21` |
| `PARAMETER_LIST`, `PARAMETER`, `TYPED_PARAMETER`, `UNTYPED_PARAMETER`, `OMITTED_PARAMETER`, `LIST` | Table 3; 7.1 | `parameterList`, `parameter`, `typedParameter`, `untypedParameter`, `list` | `edition3-all-sections.p21` |
| `ANCHOR_SECTION`, `ANCHOR_LIST`, `ANCHOR`, `ANCHOR_ITEM`, `ANCHOR_ITEM_LIST`, `ANCHOR_TAG` | Table 3; clause 9 | `anchorSection`, `anchor`, `anchorItem`, `anchorItemList`, `anchorTag` | `edition3-all-sections.p21`, `numeric-anchor.p21`, `tag-name-low-line.p21` |
| `REFERENCE_SECTION`, `REFERENCE_LIST`, `REFERENCE` | Table 3; clause 10 | `referenceSection`, `reference` | `edition3-all-sections.p21`, `resource-escape.p21` |
| `DATA_SECTION`, `ENTITY_INSTANCE_LIST`, `ENTITY_INSTANCE`, `SIMPLE_ENTITY_INSTANCE`, `COMPLEX_ENTITY_INSTANCE`, `SIMPLE_RECORD`, `SUBSUPER_RECORD`, `SIMPLE_RECORD_LIST` | Table 3; clauses 11–12 | `dataSection`, `entityInstance`, `simpleEntityInstance`, `complexEntityInstance`, `simpleRecord`, `subSuperRecord` | classic fixtures, `edition3-all-sections.p21`, `value-instance-data-lhs.p21` |
| `SIGNATURE_SECTION` | Table 3; clause 14 | `signatureSection`; lexer `SignatureMode` makes Base64 one contextual token | both Edition 3 valid files, `signature-base64.p21`, `signature-semicolon.p21` |

The parser uses direct repetitions where the WSN names a list production; this changes parse-tree factoring, not the recognized language. Table 3 is authoritative for signature punctuation: `SIGNATURE` is not followed by a semicolon. The contradictory prose spelling in clause 14.1 is not accepted as a compatibility extension.

## Token traceability

| Normative token family | Normative location | Grammar realization | Focused evidence |
| --- | --- | --- | --- |
| Basic alphabet and ignored controls | Table 1; 5.2, 5.6 | lexer character fragments and `IgnoredControl`; U+0000–U+001F and U+007F are ignored inside or between tokens | `ignored-controls-inside-tokens.p21`, `Should_ignore_non_graphic_control_octets` |
| Token separators | 5.6; clause 13 | `Space`, `Comment`, `PrintControl`; print controls also occur inside `String` and `Binary` | `edition3-all-sections.p21` |
| Keyword | Table 2; 6.3 | `UserDefinedKeyword`, `StandardKeyword`, `UpperKeyword`; the split keeps low-line keywords distinct from tag names without changing the WSN language | `edition3-all-sections.p21`, `tag-name-low-line.p21` |
| Integer and real | Table 2; 6.4.1–6.4.2 | `Integer`, `Real` | `edition3-all-sections.p21`, `lowercase-exponent.p21` |
| String and control directives | Tables 2 and 4; 6.4.3 | `String` plus PAGE, ALPHABET, EXTENDED2, EXTENDED4, ARBITRARY fragments | `edition3-all-sections.p21` |
| Occurrence names | Table 2; 6.4.4 | entity, value, constant entity, and constant value tokens; parser distinguishes LHS/RHS use | `edition3-all-sections.p21`, `value-instance-data-lhs.p21` |
| Enumeration and binary | Table 2; 6.4.5–6.4.6 | `Enumeration`, `Binary` | `edition3-all-sections.p21`, `lowercase-enumeration.p21`, `binary-prefix.p21` |
| Anchor name, resource, tag name | Table 2; 6.5.1–6.5.5; RFC 2396 | `UriMode` emits URI characters without lexical overlap; parser `anchorName` requires a non-digit and `resource` implements the RFC 2396 absolute/relative/fragment URI-reference productions, including case-insensitive hexadecimal escapes | Edition 3 fixtures, `resource-escape.p21`, `resource-structure.p21`, and tag/anchor negative fixtures |
| Signature content | Table 2; 6.5.6; RFC 4648 | `SignatureContent` in `SignatureMode`, including quartet and padding forms | both signature-valid fixtures, `signature-base64.p21` |

## Verification boundary

Fast TUnit grammar tests assert both zero syntax errors and EOF for valid files, and at least one lexer/parser error for focused invalid files. Deterministic generation tests prove the split lexer/parser sources regenerate internal visitors without listeners and remain byte-stable. The opted-in external corpus remains a regression layer; it cannot broaden or narrow this clause-backed grammar.
