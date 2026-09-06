lexer grammar STEPLexer;

// ISO 10303-21:2016 Edition 3, Tables 1, 2 and 4.

ISO_START
    : 'I' IgnoredControls 'S' IgnoredControls 'O' IgnoredControls '-'
      IgnoredControls '1' IgnoredControls '0' IgnoredControls '3' IgnoredControls '0' IgnoredControls '3'
      IgnoredControls '-' IgnoredControls '2' IgnoredControls '1' IgnoredControls ';'
    ;

ISO_END
    : 'E' IgnoredControls 'N' IgnoredControls 'D' IgnoredControls '-'
      IgnoredControls 'I' IgnoredControls 'S' IgnoredControls 'O' IgnoredControls '-'
      IgnoredControls '1' IgnoredControls '0' IgnoredControls '3' IgnoredControls '0' IgnoredControls '3'
      IgnoredControls '-' IgnoredControls '2' IgnoredControls '1' IgnoredControls ';'
    ;

HEADER
    : 'H' IgnoredControls 'E' IgnoredControls 'A' IgnoredControls 'D'
      IgnoredControls 'E' IgnoredControls 'R' IgnoredControls ';'
    ;

ANCHOR
    : 'A' IgnoredControls 'N' IgnoredControls 'C' IgnoredControls 'H'
      IgnoredControls 'O' IgnoredControls 'R' IgnoredControls ';'
    ;

REFERENCE
    : 'R' IgnoredControls 'E' IgnoredControls 'F' IgnoredControls 'E'
      IgnoredControls 'R' IgnoredControls 'E' IgnoredControls 'N' IgnoredControls 'C'
      IgnoredControls 'E' IgnoredControls ';'
    ;

DATA
    : 'D' IgnoredControls 'A' IgnoredControls 'T' IgnoredControls 'A'
    ;

SIGNATURE
    : 'S' IgnoredControls 'I' IgnoredControls 'G' IgnoredControls 'N'
      IgnoredControls 'A' IgnoredControls 'T' IgnoredControls 'U' IgnoredControls 'R'
      IgnoredControls 'E' -> pushMode(SignatureMode)
    ;

ENDSEC
    : 'E' IgnoredControls 'N' IgnoredControls 'D' IgnoredControls 'S'
      IgnoredControls 'E' IgnoredControls 'C' IgnoredControls ';'
    ;

FILE_DESCRIPTION
    : 'F' IgnoredControls 'I' IgnoredControls 'L' IgnoredControls 'E' IgnoredControls '_'
      IgnoredControls 'D' IgnoredControls 'E' IgnoredControls 'S' IgnoredControls 'C'
      IgnoredControls 'R' IgnoredControls 'I' IgnoredControls 'P' IgnoredControls 'T'
      IgnoredControls 'I' IgnoredControls 'O' IgnoredControls 'N'
    ;

FILE_NAME
    : 'F' IgnoredControls 'I' IgnoredControls 'L' IgnoredControls 'E' IgnoredControls '_'
      IgnoredControls 'N' IgnoredControls 'A' IgnoredControls 'M' IgnoredControls 'E'
    ;

FILE_SCHEMA
    : 'F' IgnoredControls 'I' IgnoredControls 'L' IgnoredControls 'E' IgnoredControls '_'
      IgnoredControls 'S' IgnoredControls 'C' IgnoredControls 'H' IgnoredControls 'E'
      IgnoredControls 'M' IgnoredControls 'A'
    ;

LPAREN : '(' ;
RPAREN : ')' ;
SEMICOLON : ';' ;
COMMA : ',' ;
ASTERISK : '*' ;
DOLLAR : '$' ;
EQUALS : '=' ;
LBRACE : '{' ;
RBRACE : '}' ;
COLON : ':' ;

Real
    : (Sign IgnoredControls)? Digit (IgnoredControls Digit)* IgnoredControls '.'
      (IgnoredControls Digit)*
      (IgnoredControls 'E' (IgnoredControls Sign)? IgnoredControls Digit (IgnoredControls Digit)*)?
    ;

Integer
    : (Sign IgnoredControls)? Digit (IgnoredControls Digit)*
    ;

String
    : Apostrophe StringCharacter* Apostrophe
    ;

EntityInstanceName
    : '#' IgnoredControls Digit (IgnoredControls Digit)*
    ;

ValueInstanceName
    : '@' IgnoredControls Digit (IgnoredControls Digit)*
    ;

ConstantEntityName
    : '#' IgnoredControls Upper (IgnoredControls (Upper | Digit))*
    ;

ConstantValueName
    : '@' IgnoredControls Upper (IgnoredControls (Upper | Digit))*
    ;

UriLt
    : '<' -> pushMode(UriMode)
    ;

Enumeration
    : '.' IgnoredControls Upper (IgnoredControls (Upper | Digit))* IgnoredControls '.'
    ;

Binary
    : '"' IgnoredControls [0-3]
      (IgnoredControls (Hex | PrintControlDirective))* IgnoredControls '"'
    ;

UserDefinedKeyword
    : '!' IgnoredControls Upper (IgnoredControls (Upper | Digit))*
    ;

StandardKeyword
    : ([A-Z] (IgnoredControls [A-Z0-9])* IgnoredControls)? '_'
      (IgnoredControls (Upper | Digit))*
    ;

UpperKeyword
    : [A-Z] (IgnoredControls [A-Z0-9])*
    ;

TagName
    : [A-Za-z_] (IgnoredControls [A-Za-z0-9_])*
    ;

PrintControl
    : PrintControlDirective -> skip
    ;

Comment
    : '/*' .*? '*/' -> skip
    ;

IgnoredControl
    : IgnoredControlChar+ -> skip
    ;

Space
    : ' '+ -> skip
    ;

fragment StringCharacter
    : Apostrophe Apostrophe
    | ReverseSolidus ReverseSolidus
    | PrintControlDirective
    | PageDirective
    | AlphabetDirective
    | Extended2Directive
    | Extended4Directive
    | ArbitraryDirective
    | IgnoredControlChar
    | ~['\\\u0000-\u001F]
    ;

fragment PrintControlDirective
    : ReverseSolidus [NF] ReverseSolidus
    ;

fragment IgnoredControls
    : IgnoredControlChar*
    ;

fragment IgnoredControlChar
    : [\u0000-\u001F\u007F]
    ;

fragment PageDirective
    : ReverseSolidus 'S' ReverseSolidus LatinCodepoint
    ;

fragment AlphabetDirective
    : ReverseSolidus 'P' Upper ReverseSolidus
    ;

fragment Extended2Directive
    : ReverseSolidus 'X2' ReverseSolidus Hex Hex Hex Hex (Hex Hex Hex Hex)* EndExtended
    ;

fragment Extended4Directive
    : ReverseSolidus 'X4' ReverseSolidus Hex Hex Hex Hex Hex Hex Hex Hex
      (Hex Hex Hex Hex Hex Hex Hex Hex)* EndExtended
    ;

fragment ArbitraryDirective
    : ReverseSolidus 'X' ReverseSolidus Hex Hex
    ;

fragment EndExtended
    : ReverseSolidus 'X0' ReverseSolidus
    ;

fragment LatinCodepoint
    : [\u0020-\u007E]
    ;

fragment Apostrophe
    : '\''
    ;

fragment ReverseSolidus
    : '\\'
    ;

fragment Sign
    : [+-]
    ;

fragment Digit
    : [0-9]
    ;

fragment Upper
    : [A-Z_]
    ;

fragment Hex
    : [0-9A-F]
    ;

mode UriMode;

UriGt : '>' -> popMode ;
UriEscaped : '%' IgnoredControls [0-9A-Fa-f] IgnoredControls [0-9A-Fa-f] ;
UriAlpha : [A-Za-z] ;
UriDigit : [0-9] ;
UriMinus : '-' ;
UriLowLine : '_' ;
UriDot : '.' ;
UriExclamation : '!' ;
UriTilde : '~' ;
UriAsterisk : '*' ;
UriApostrophe : '\'' ;
UriLeftParen : '(' ;
UriRightParen : ')' ;
UriSemicolon : ';' ;
UriSlash : '/' ;
UriQuestion : '?' ;
UriColon : ':' ;
UriAt : '@' ;
UriAmpersand : '&' ;
UriEquals : '=' ;
UriPlus : '+' ;
UriDollar : '$' ;
UriComma : ',' ;
UriHash : '#' ;

UriIgnoredControl
    : IgnoredControlChar+ -> skip
    ;

mode SignatureMode;

SignatureContent
    : (Base64Quartet+
      | Base64Quartet* Base64Character Base64Character Base64Character IgnoredControls '='
      | Base64Quartet* Base64Character Base64Character IgnoredControls '=' IgnoredControls '=')
      -> popMode
    ;

SignaturePrintControl
    : PrintControlDirective -> skip
    ;

SignatureComment
    : '/*' .*? '*/' -> skip
    ;

SignatureIgnoredControl
    : IgnoredControlChar+ -> skip
    ;

SignatureSpace
    : ' '+ -> skip
    ;

fragment Base64Quartet
    : Base64Character Base64Character Base64Character Base64Character
    ;

fragment Base64Character
    : [A-Za-z0-9+/] IgnoredControls
    ;
