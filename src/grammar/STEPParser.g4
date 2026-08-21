parser grammar STEPParser;

options {
    tokenVocab = STEPLexer;
}

// ISO 10303-21:2016 Edition 3, Table 3 (WSN of the exchange structure).

exchangeFile
    : ISO_START headerSection anchorSection? referenceSection? dataSection* ISO_END signatureSection* EOF
    ;

headerSection
    : HEADER fileDescription fileName fileSchema headerEntity* ENDSEC
    ;

fileDescription
    : FILE_DESCRIPTION LPAREN parameterList? RPAREN SEMICOLON
    ;

fileName
    : FILE_NAME LPAREN parameterList? RPAREN SEMICOLON
    ;

fileSchema
    : FILE_SCHEMA LPAREN parameterList? RPAREN SEMICOLON
    ;

headerEntity
    : keyword LPAREN parameterList? RPAREN SEMICOLON
    ;

parameterList
    : parameter (COMMA parameter)*
    ;

parameter
    : typedParameter
    | untypedParameter
    | ASTERISK
    ;

typedParameter
    : keyword LPAREN parameter RPAREN
    ;

untypedParameter
    : DOLLAR
    | Integer
    | Real
    | String
    | rhsOccurrenceName
    | Enumeration
    | Binary
    | list
    ;

list
    : LPAREN (parameter (COMMA parameter)*)? RPAREN
    ;

anchorSection
    : ANCHOR anchor* ENDSEC
    ;

anchor
    : anchorName EQUALS anchorItem anchorTag* SEMICOLON
    ;

anchorItem
    : DOLLAR
    | Integer
    | Real
    | String
    | Enumeration
    | Binary
    | rhsOccurrenceName
    | resource
    | anchorItemList
    ;

anchorItemList
    : LPAREN (anchorItem (COMMA anchorItem)*)? RPAREN
    ;

anchorTag
    : LBRACE tagName COLON anchorItem RBRACE
    ;

tagName
    : UpperKeyword
    | TagName
    ;

referenceSection
    : REFERENCE reference* ENDSEC
    ;

reference
    : lhsOccurrenceName EQUALS resource SEMICOLON
    ;

resource
    : UriLt uriReference UriGt
    ;

anchorName
    : UriLt UriDigit* uriNonDigit uriCharacter* UriGt
    ;

// IETF RFC 2396 URI-reference syntax used by ISO 10303-21 clause 6.5.
uriReference
    : absoluteUri (UriHash uriFragment)?
    | relativeUri (UriHash uriFragment)?
    | UriHash uriFragment
    ;

absoluteUri
    : scheme UriColon (hierPart | opaquePart)
    ;

hierPart
    : (netPath | absPath) (UriQuestion query)?
    ;

opaquePart
    : uriNoSlash uriCharacter*
    ;

relativeUri
    : (netPath | absPath | relPath) (UriQuestion query)?
    ;

netPath
    : UriSlash UriSlash authority absPath?
    ;

absPath
    : UriSlash pathSegments
    ;

relPath
    : relSegment absPath?
    ;

pathSegments
    : segment (UriSlash segment)*
    ;

segment
    : pchar* (UriSemicolon param)*
    ;

param
    : pchar*
    ;

relSegment
    : relCharacter+
    ;

scheme
    : UriAlpha (UriAlpha | UriDigit | UriPlus | UriMinus | UriDot)*
    ;

authority
    : authorityCharacter*
    ;

query
    : uriCharacter*
    ;

uriFragment
    : uriCharacter*
    ;

pchar
    : uriUnreserved
    | UriEscaped
    | UriColon
    | UriAt
    | UriAmpersand
    | UriEquals
    | UriPlus
    | UriDollar
    | UriComma
    ;

relCharacter
    : uriUnreserved
    | UriEscaped
    | UriSemicolon
    | UriAt
    | UriAmpersand
    | UriEquals
    | UriPlus
    | UriDollar
    | UriComma
    ;

authorityCharacter
    : uriUnreserved
    | UriEscaped
    | UriDollar
    | UriComma
    | UriSemicolon
    | UriColon
    | UriAt
    | UriAmpersand
    | UriEquals
    | UriPlus
    ;

uriNoSlash
    : uriUnreserved
    | UriEscaped
    | UriSemicolon
    | UriQuestion
    | UriColon
    | UriAt
    | UriAmpersand
    | UriEquals
    | UriPlus
    | UriDollar
    | UriComma
    ;

uriCharacter
    : uriReserved
    | uriUnreserved
    | UriEscaped
    ;

uriNonDigit
    : UriAlpha
    | UriMinus
    | UriLowLine
    | UriDot
    | UriExclamation
    | UriTilde
    | UriAsterisk
    | UriApostrophe
    | UriLeftParen
    | UriRightParen
    | UriEscaped
    | uriReserved
    ;

uriReserved
    : UriSemicolon
    | UriSlash
    | UriQuestion
    | UriColon
    | UriAt
    | UriAmpersand
    | UriEquals
    | UriPlus
    | UriDollar
    | UriComma
    ;

uriUnreserved
    : UriAlpha
    | UriDigit
    | UriMinus
    | UriLowLine
    | UriDot
    | UriExclamation
    | UriTilde
    | UriAsterisk
    | UriApostrophe
    | UriLeftParen
    | UriRightParen
    ;

dataSection
    : DATA (LPAREN parameterList RPAREN)? SEMICOLON entityInstance* ENDSEC
    ;

entityInstance
    : simpleEntityInstance
    | complexEntityInstance
    ;

simpleEntityInstance
    : EntityInstanceName EQUALS simpleRecord SEMICOLON
    ;

complexEntityInstance
    : EntityInstanceName EQUALS subSuperRecord SEMICOLON
    ;

simpleRecord
    : keyword LPAREN parameterList? RPAREN
    ;

subSuperRecord
    : LPAREN simpleRecord+ RPAREN
    ;

signatureSection
    : SIGNATURE SignatureContent ENDSEC
    ;

keyword
    : UserDefinedKeyword
    | StandardKeyword
    | UpperKeyword
    ;

lhsOccurrenceName
    : EntityInstanceName
    | ValueInstanceName
    ;

rhsOccurrenceName
    : EntityInstanceName
    | ValueInstanceName
    | ConstantEntityName
    | ConstantValueName
    ;
