grammar Express;

options {
    caseInsensitive = true;
}

syntax
    : schemaDecl+ EOF
    ;

schemaDecl
    : SCHEMA schemaId schemaVersionId? ';' schemaBody END_SCHEMA ';'
    ;

schemaId
    : SimpleId
    ;

schemaVersionId
    : stringLiteral
    ;

schemaBody
    : interfaceSpecification* constantDecl? (declaration | ruleDecl)*
    ;

interfaceSpecification
    : referenceClause
    | useClause
    ;

referenceClause
    : REFERENCE FROM schemaRef ('(' resourceOrRename (',' resourceOrRename)* ')')? ';'
    ;

useClause
    : USE FROM schemaRef ('(' namedTypeOrRename (',' namedTypeOrRename)* ')')? ';'
    ;

resourceOrRename
    : resourceRef (AS renameId)?
    ;

namedTypeOrRename
    : namedTypes (AS (entityId | typeId))?
    ;

resourceRef
    : constantRef
    | entityRef
    | functionRef
    | procedureRef
    | typeRef
    ;

renameId
    : constantId
    | entityId
    | functionId
    | procedureId
    | typeId
    ;

declaration
    : entityDecl
    | functionDecl
    | procedureDecl
    | subtypeConstraintDecl
    | typeDecl
    ;

constantDecl
    : CONSTANT constantBody+ END_CONSTANT ';'
    ;

constantBody
    : constantId ':' instantiableType ':=' expression ';'
    ;

entityDecl
    : entityHead entityBody END_ENTITY ';'
    ;

entityHead
    : ENTITY entityId subsuper ';'
    ;

subsuper
    : supertypeConstraint? subtypeDeclaration?
    ;

supertypeConstraint
    : abstractSupertypeDeclaration
    | abstractEntityDeclaration
    | supertypeRule
    ;

abstractSupertypeDeclaration
    : ABSTRACT SUPERTYPE subtypeConstraint?
    ;

abstractEntityDeclaration
    : ABSTRACT
    ;

supertypeRule
    : SUPERTYPE subtypeConstraint
    ;

subtypeConstraint
    : OF '(' supertypeExpression ')'
    ;

subtypeDeclaration
    : SUBTYPE OF '(' entityRef (',' entityRef)* ')'
    ;

supertypeExpression
    : supertypeFactor (ANDOR supertypeFactor)*
    ;

supertypeFactor
    : supertypeTerm (AND supertypeTerm)*
    ;

supertypeTerm
    : oneOf
    | '(' supertypeExpression ')'
    | entityRef
    ;

oneOf
    : ONEOF '(' supertypeExpression (',' supertypeExpression)* ')'
    ;

entityBody
    : explicitAttr* deriveClause? inverseClause? uniqueClause? whereClause?
    ;

explicitAttr
    : attributeDecl (',' attributeDecl)* ':' OPTIONAL? parameterType ';'
    ;

attributeDecl
    : redeclaredAttribute
    | attributeId
    ;

redeclaredAttribute
    : qualifiedAttribute (RENAMED attributeId)?
    ;

qualifiedAttribute
    : SELF groupQualifier attributeQualifier
    ;

deriveClause
    : DERIVE derivedAttr+
    ;

derivedAttr
    : attributeDecl ':' parameterType ':=' expression ';'
    ;

inverseClause
    : INVERSE inverseAttr+
    ;

inverseAttr
    : attributeDecl ':' ((SET | BAG) boundSpec? OF)? entityRef FOR (entityRef '.')? attributeRef ';'
    ;

uniqueClause
    : UNIQUE uniqueRule ';' (uniqueRule ';')*
    ;

uniqueRule
    : (ruleLabelId ':')? referencedAttribute (',' referencedAttribute)*
    ;

referencedAttribute
    : attributeRef
    | qualifiedAttribute
    ;

whereClause
    : WHERE domainRule ';' (domainRule ';')*
    ;

domainRule
    : (ruleLabelId ':')? expression
    ;

functionDecl
    : functionHead algorithmHead stmt+ END_FUNCTION ';'
    ;

functionHead
    : FUNCTION functionId ('(' formalParameter (';' formalParameter)* ')')? ':' parameterType ';'
    ;

procedureDecl
    : procedureHead algorithmHead stmt* END_PROCEDURE ';'
    ;

procedureHead
    : PROCEDURE procedureId ('(' VAR? formalParameter (';' VAR? formalParameter)* ')')? ';'
    ;

formalParameter
    : parameterId (',' parameterId)* ':' parameterType
    ;

algorithmHead
    : declaration* constantDecl? localDecl?
    ;

localDecl
    : LOCAL localVariable+ END_LOCAL ';'
    ;

localVariable
    : variableId (',' variableId)* ':' parameterType (':=' expression)? ';'
    ;

ruleDecl
    : ruleHead algorithmHead stmt* whereClause END_RULE ';'
    ;

ruleHead
    : RULE ruleId FOR '(' entityRef (',' entityRef)* ')' ';'
    ;

subtypeConstraintDecl
    : subtypeConstraintHead subtypeConstraintBody END_SUBTYPE_CONSTRAINT ';'
    ;

subtypeConstraintHead
    : SUBTYPE_CONSTRAINT subtypeConstraintId FOR entityRef ';'
    ;

subtypeConstraintBody
    : abstractSupertype? totalOver? (supertypeExpression ';')?
    ;

abstractSupertype
    : ABSTRACT SUPERTYPE ';'
    ;

totalOver
    : TOTAL_OVER '(' entityRef (',' entityRef)* ')' ';'
    ;

typeDecl
    : TYPE typeId '=' underlyingType ';' whereClause? END_TYPE ';'
    ;

underlyingType
    : constructedTypes
    | concreteTypes
    ;

constructedTypes
    : enumerationType
    | selectType
    ;

enumerationType
    : EXTENSIBLE? ENUMERATION (OF enumerationItems | enumerationExtension)?
    ;

enumerationExtension
    : BASED_ON typeRef (WITH enumerationItems)?
    ;

enumerationItems
    : '(' enumerationId (',' enumerationId)* ')'
    ;

selectType
    : (EXTENSIBLE GENERIC_ENTITY?)? SELECT (selectList | selectExtension)?
    ;

selectExtension
    : BASED_ON typeRef (WITH selectList)?
    ;

selectList
    : '(' namedTypes (',' namedTypes)* ')'
    ;

concreteTypes
    : aggregationTypes
    | simpleTypes
    | typeRef
    ;

aggregationTypes
    : arrayType
    | bagType
    | listType
    | setType
    ;

arrayType
    : ARRAY boundSpec OF OPTIONAL? UNIQUE? instantiableType
    ;

bagType
    : BAG boundSpec? OF instantiableType
    ;

listType
    : LIST boundSpec? OF UNIQUE? instantiableType
    ;

setType
    : SET boundSpec? OF instantiableType
    ;

boundSpec
    : '[' bound1 ':' bound2 ']'
    ;

bound1
    : numericExpression
    ;

bound2
    : numericExpression
    ;

instantiableType
    : concreteTypes
    | entityRef
    ;

simpleTypes
    : binaryType
    | booleanType
    | integerType
    | logicalType
    | numberType
    | realType
    | stringType
    ;

binaryType
    : BINARY widthSpec?
    ;

booleanType
    : BOOLEAN
    ;

integerType
    : INTEGER
    ;

logicalType
    : LOGICAL
    ;

numberType
    : NUMBER
    ;

realType
    : REAL ('(' precisionSpec ')')?
    ;

stringType
    : STRING widthSpec?
    ;

widthSpec
    : '(' width ')' FIXED?
    ;

width
    : numericExpression
    ;

precisionSpec
    : numericExpression
    ;

parameterType
    : generalizedTypes
    | simpleTypes
    | namedTypes
    ;

generalizedTypes
    : aggregateType
    | generalAggregationTypes
    | genericEntityType
    | genericType
    ;

aggregateType
    : AGGREGATE (':' typeLabel)? OF parameterType
    ;

generalAggregationTypes
    : generalArrayType
    | generalBagType
    | generalListType
    | generalSetType
    ;

generalArrayType
    : ARRAY boundSpec? OF OPTIONAL? UNIQUE? parameterType
    ;

generalBagType
    : BAG boundSpec? OF parameterType
    ;

generalListType
    : LIST boundSpec? OF UNIQUE? parameterType
    ;

generalSetType
    : SET boundSpec? OF parameterType
    ;

genericEntityType
    : GENERIC_ENTITY (':' typeLabel)?
    ;

genericType
    : GENERIC (':' typeLabel)?
    ;

namedTypes
    : entityRef
    | typeRef
    ;

expression
    : simpleExpression (relOpExtended simpleExpression)?
    ;

simpleExpression
    : term (addLikeOp term)*
    ;

addLikeOp
    : '+'
    | '-'
    | OR
    | XOR
    ;

term
    : factor (multiplicationLikeOp factor)*
    ;

multiplicationLikeOp
    : '*'
    | '/'
    | DIV
    | MOD
    | AND
    | '||'
    ;

factor
    : simpleFactor ('**' simpleFactor)?
    ;

simpleFactor
    : aggregateInitializer
    | interval
    | queryExpression
    | unaryOp? ('(' expression ')' | primary)
    ;

primary
    : literal
    | namedApplication qualifier*
    | namedReference qualifier*
    ;

// EXPRESS name classes require schema binding. Syntax IR therefore preserves the
// two physical forms without falsely choosing function vs entity construction,
// or attribute vs constant vs variable vs population vs enumeration.
namedApplication
    : builtInFunction actualParameterList
    | SimpleId actualParameterList
    ;

namedReference
    : builtInConstant
    | SimpleId
    ;

builtInConstant
    : CONST_E
    | PI
    | SELF
    | '?'
    ;

literal
    : BinaryLiteral
    | logicalLiteral
    | IntegerLiteral
    | RealLiteral
    | stringLiteral
    ;

logicalLiteral
    : FALSE
    | TRUE
    | UNKNOWN
    ;

stringLiteral
    : SimpleStringLiteral
    | EncodedStringLiteral
    ;

aggregateInitializer
    : '[' (element (',' element)*)? ']'
    ;

element
    : expression (':' repetition)?
    ;

repetition
    : numericExpression
    ;

interval
    : '{' intervalLow intervalOp intervalItem intervalOp intervalHigh '}'
    ;

intervalLow
    : simpleExpression
    ;

intervalItem
    : simpleExpression
    ;

intervalHigh
    : simpleExpression
    ;

intervalOp
    : '<='
    | '<'
    ;

queryExpression
    : QUERY '(' variableId '<*' aggregateSource '|' logicalExpression ')'
    ;

aggregateSource
    : simpleExpression
    ;

logicalExpression
    : expression
    ;

numericExpression
    : simpleExpression
    ;

actualParameterList
    : '(' parameter (',' parameter)* ')'
    ;

parameter
    : expression
    ;

builtInFunction
    : ABS
    | ACOS
    | ASIN
    | ATAN
    | BLENGTH
    | COS
    | EXISTS
    | EXP
    | FORMAT
    | HIBOUND
    | HIINDEX
    | LENGTH
    | LOBOUND
    | LOINDEX
    | LOG
    | LOG2
    | LOG10
    | NVL
    | ODD
    | ROLESOF
    | SIN
    | SIZEOF
    | SQRT
    | TAN
    | TYPEOF
    | USEDIN
    | VALUE
    | VALUE_IN
    | VALUE_UNIQUE
    ;

qualifier
    : attributeQualifier
    | groupQualifier
    | indexQualifier
    ;

attributeQualifier
    : '.' attributeRef
    ;

groupQualifier
    : '\\' entityRef
    ;

indexQualifier
    : '[' index1 (':' index2)? ']'
    ;

index1
    : index
    ;

index2
    : index
    ;

index
    : numericExpression
    ;

relOpExtended
    : relOp
    | IN
    | LIKE
    ;

relOp
    : '<='
    | '>='
    | '<>'
    | '='
    | ':<>:'
    | ':=:'
    | '<'
    | '>'
    ;

unaryOp
    : '+'
    | '-'
    | NOT
    ;

stmt
    : aliasStmt
    | assignmentStmt
    | caseStmt
    | compoundStmt
    | escapeStmt
    | ifStmt
    | nullStmt
    | procedureCallStmt
    | repeatStmt
    | returnStmt
    | skipStmt
    ;

aliasStmt
    : ALIAS variableId FOR generalRef qualifier* ';' stmt+ END_ALIAS ';'
    ;

assignmentStmt
    : generalRef qualifier* ':=' expression ';'
    ;

caseStmt
    : CASE selector OF caseAction* (OTHERWISE ':' stmt)? END_CASE ';'
    ;

selector
    : expression
    ;

caseAction
    : caseLabel (',' caseLabel)* ':' stmt
    ;

caseLabel
    : expression
    ;

compoundStmt
    : BEGIN stmt+ END ';'
    ;

escapeStmt
    : ESCAPE ';'
    ;

ifStmt
    : IF logicalExpression THEN stmt+ (ELSE stmt+)? END_IF ';'
    ;

nullStmt
    : ';'
    ;

procedureCallStmt
    : (builtInProcedure | procedureRef) actualParameterList? ';'
    ;

builtInProcedure
    : INSERT
    | REMOVE
    ;

repeatStmt
    : REPEAT repeatControl ';' stmt+ END_REPEAT ';'
    ;

repeatControl
    : incrementControl? whileControl? untilControl?
    ;

incrementControl
    : variableId ':=' bound1 TO bound2 (BY increment)?
    ;

increment
    : numericExpression
    ;

whileControl
    : WHILE logicalExpression
    ;

untilControl
    : UNTIL logicalExpression
    ;

returnStmt
    : RETURN ('(' expression ')')? ';'
    ;

skipStmt
    : SKIP_KEYWORD ';'
    ;

generalRef
    : parameterRef
    | variableRef
    ;

attributeId
    : SimpleId
    ;

attributeRef
    : attributeId
    ;

constantId
    : SimpleId
    ;

constantRef
    : constantId
    ;

entityId
    : SimpleId
    ;

entityRef
    : entityId
    ;

enumerationId
    : SimpleId
    ;

enumerationRef
    : enumerationId
    ;

functionId
    : SimpleId
    ;

functionRef
    : functionId
    ;

parameterId
    : SimpleId
    ;

parameterRef
    : parameterId
    ;

procedureId
    : SimpleId
    ;

procedureRef
    : procedureId
    ;

ruleId
    : SimpleId
    ;

ruleLabelId
    : SimpleId
    ;

schemaRef
    : schemaId
    ;

subtypeConstraintId
    : SimpleId
    ;

typeId
    : SimpleId
    ;

typeRef
    : typeId
    ;

typeLabel
    : typeLabelId
    | typeLabelRef
    ;

typeLabelId
    : SimpleId
    ;

typeLabelRef
    : SimpleId
    ;

variableId
    : SimpleId
    ;

variableRef
    : variableId
    ;

ABS : 'ABS';
ABSTRACT : 'ABSTRACT';
ACOS : 'ACOS';
AGGREGATE : 'AGGREGATE';
ALIAS : 'ALIAS';
AND : 'AND';
ANDOR : 'ANDOR';
ARRAY : 'ARRAY';
AS : 'AS';
ASIN : 'ASIN';
ATAN : 'ATAN';
BAG : 'BAG';
BASED_ON : 'BASED_ON';
BEGIN : 'BEGIN';
BINARY : 'BINARY';
BLENGTH : 'BLENGTH';
BOOLEAN : 'BOOLEAN';
BY : 'BY';
CASE : 'CASE';
CONSTANT : 'CONSTANT';
CONST_E : 'CONST_E';
COS : 'COS';
DERIVE : 'DERIVE';
DIV : 'DIV';
ELSE : 'ELSE';
END : 'END';
END_ALIAS : 'END_ALIAS';
END_CASE : 'END_CASE';
END_CONSTANT : 'END_CONSTANT';
END_ENTITY : 'END_ENTITY';
END_FUNCTION : 'END_FUNCTION';
END_IF : 'END_IF';
END_LOCAL : 'END_LOCAL';
END_PROCEDURE : 'END_PROCEDURE';
END_REPEAT : 'END_REPEAT';
END_RULE : 'END_RULE';
END_SCHEMA : 'END_SCHEMA';
END_SUBTYPE_CONSTRAINT : 'END_SUBTYPE_CONSTRAINT';
END_TYPE : 'END_TYPE';
ENTITY : 'ENTITY';
ENUMERATION : 'ENUMERATION';
ESCAPE : 'ESCAPE';
EXISTS : 'EXISTS';
EXP : 'EXP';
EXTENSIBLE : 'EXTENSIBLE';
FALSE : 'FALSE';
FIXED : 'FIXED';
FOR : 'FOR';
FORMAT : 'FORMAT';
FROM : 'FROM';
FUNCTION : 'FUNCTION';
GENERIC : 'GENERIC';
GENERIC_ENTITY : 'GENERIC_ENTITY';
HIBOUND : 'HIBOUND';
HIINDEX : 'HIINDEX';
IF : 'IF';
IN : 'IN';
INSERT : 'INSERT';
INTEGER : 'INTEGER';
INVERSE : 'INVERSE';
LENGTH : 'LENGTH';
LIKE : 'LIKE';
LIST : 'LIST';
LOCAL : 'LOCAL';
LOG : 'LOG';
LOG10 : 'LOG10';
LOG2 : 'LOG2';
LOGICAL : 'LOGICAL';
LOBOUND : 'LOBOUND';
LOINDEX : 'LOINDEX';
MOD : 'MOD';
NOT : 'NOT';
NUMBER : 'NUMBER';
NVL : 'NVL';
ODD : 'ODD';
OF : 'OF';
ONEOF : 'ONEOF';
OPTIONAL : 'OPTIONAL';
OR : 'OR';
OTHERWISE : 'OTHERWISE';
PI : 'PI';
PROCEDURE : 'PROCEDURE';
QUERY : 'QUERY';
REAL : 'REAL';
REFERENCE : 'REFERENCE';
REMOVE : 'REMOVE';
RENAMED : 'RENAMED';
REPEAT : 'REPEAT';
RETURN : 'RETURN';
ROLESOF : 'ROLESOF';
RULE : 'RULE';
SCHEMA : 'SCHEMA';
SELECT : 'SELECT';
SELF : 'SELF';
SET : 'SET';
SIN : 'SIN';
SIZEOF : 'SIZEOF';
SKIP_KEYWORD : 'SKIP';
SQRT : 'SQRT';
STRING : 'STRING';
SUBTYPE : 'SUBTYPE';
SUBTYPE_CONSTRAINT : 'SUBTYPE_CONSTRAINT';
SUPERTYPE : 'SUPERTYPE';
TAN : 'TAN';
THEN : 'THEN';
TO : 'TO';
TOTAL_OVER : 'TOTAL_OVER';
TRUE : 'TRUE';
TYPE : 'TYPE';
TYPEOF : 'TYPEOF';
UNIQUE : 'UNIQUE';
UNKNOWN : 'UNKNOWN';
UNTIL : 'UNTIL';
USE : 'USE';
USEDIN : 'USEDIN';
VALUE : 'VALUE';
VALUE_IN : 'VALUE_IN';
VALUE_UNIQUE : 'VALUE_UNIQUE';
VAR : 'VAR';
WHERE : 'WHERE';
WHILE : 'WHILE';
WITH : 'WITH';
XOR : 'XOR';

SimpleId
    : Letter (Letter | Digit | '_')*
    ;

BinaryLiteral
    : '%' [01]+
    ;

EncodedStringLiteral
    : '"' EncodedCharacter+ '"'
    ;

IntegerLiteral
    : Digits
    ;

RealLiteral
    : Digits '.' Digits* ([e] [+-]? Digits)?
    ;

SimpleStringLiteral
    : '\'' ('\'\'' | ~['\r\n])* '\''
    ;

BlockComment
    : '(*' (BlockComment | .)*? '*)' -> skip
    ;

LineComment
    : '--' ~[\r\n]* -> skip
    ;

Whitespace
    : [ \t\r\n\u000c]+ -> skip
    ;

fragment EncodedCharacter
    : HexDigit HexDigit HexDigit HexDigit HexDigit HexDigit HexDigit HexDigit
    ;

fragment HexDigit
    : [0-9a-f]
    ;

fragment Digits
    : Digit+
    ;

fragment Digit
    : [0-9]
    ;

fragment Letter
    : [a-z]
    ;
