grammar GS2;

options { language=CSharp; }

script: declaration* EOF;
declaration: constDeclaration | enumDeclaration | functionDeclaration | statement;
constDeclaration: CONST IDENTIFIER ASSIGN expression SEMI?;
enumDeclaration: ENUM IDENTIFIER LBRACE enumMember (COMMA enumMember)* COMMA? RBRACE SEMI?;
enumMember: IDENTIFIER (ASSIGN expression)?;
functionDeclaration: PUBLIC? FUNCTION qualifiedName LPAREN parameterList? RPAREN block;
parameterList: IDENTIFIER (COMMA IDENTIFIER)* COMMA?;
block: LBRACE statement* RBRACE;
statement: block | ifStatement | forStatement | whileStatement | switchStatement | withStatement | newStatement | returnStatement | breakStatement | expressionStatement | SEMI;
ifStatement: IF LPAREN expression RPAREN statement (ELSE statement)?;
forStatement: FOR LPAREN expression? SEMI expression? SEMI expression? RPAREN statement | FOR LPAREN IDENTIFIER COLON expression RPAREN statement;
whileStatement: WHILE LPAREN expression RPAREN statement;
switchStatement: SWITCH LPAREN expression RPAREN LBRACE switchCase* RBRACE;
switchCase: (CASE expression | DEFAULT) COLON statement*;
withStatement: WITH LPAREN expression RPAREN statement;
newStatement: NEW qualifiedName LPAREN argumentList? RPAREN block?;
returnStatement: RETURN expression? SEMI?;
breakStatement: BREAK SEMI?;
expressionStatement: expression SEMI?;
expression: expression QUESTION expression COLON expression
	| expression op=(MUL | DIV | MOD) expression
	| expression op=(PLUS | MINUS) expression
	| expression op=(SHL | SHR) expression
	| expression op=(LT | LTE | LTE_ALT | GT | GTE | GTE_ALT) expression
	| expression op=(EQ | NEQ | NEQ_ALT) expression
	| expression BAND expression
	| expression BXOR expression
	| expression BOR expression
	| expression IN rangeExpression
	| expression AND expression
	| expression OR expression
	| expression op=(ASSIGN | WALRUS | PLUS_ASSIGN | MINUS_ASSIGN | MUL_ASSIGN | DIV_ASSIGN | MOD_ASSIGN | CONCAT_ASSIGN) expression
	| expression op=(CONCAT | SPC | NL | TAB) expression
	| prefixExpression;
prefixExpression: op=(INC | DEC | NOT | MINUS | CONCAT) prefixExpression | postfixExpression;
postfixExpression: primaryExpression postfixPart*;
postfixPart: LPAREN argumentList? RPAREN | DOT IDENTIFIER | DOT LPAREN expression RPAREN | LBRACK argumentList? RBRACK | op=(INC | DEC);
primaryExpression: NUMBER | STRING | CHAR | TRUE | FALSE | NULL | qualifiedName | arrayLiteral | lambdaExpression | newExpression | LPAREN expression RPAREN;
arrayLiteral: LBRACE argumentList? RBRACE;
lambdaExpression: FUNCTION LPAREN argumentList? RPAREN block;
newExpression: NEW qualifiedName LPAREN argumentList? RPAREN | NEW LBRACK numberList RBRACK;
rangeExpression: BOR expression (COMMA expression)? BOR | expression;
argumentList: expression (COMMA expression)* COMMA?;
numberList: NUMBER (COMMA NUMBER)* COMMA?;
qualifiedName: IDENTIFIER (DOUBLE_COLON IDENTIFIER | DOT IDENTIFIER)*;

CONST: 'const';
ENUM: 'enum';
PUBLIC: 'public';
FUNCTION: 'function';
IF: 'if';
ELSE: 'else';
FOR: 'for';
WHILE: 'while';
SWITCH: 'switch';
CASE: 'case';
DEFAULT: 'default';
WITH: 'with';
NEW: 'new';
RETURN: 'return';
BREAK: 'break';
IN: 'in';
TRUE: 'true';
FALSE: 'false';
NULL: 'null';
WALRUS: ':=';
LTE_ALT: '=<';
GTE_ALT: '=>';
NEQ_ALT: '<>';
DOUBLE_COLON: '::';
PLUS_ASSIGN: '+=';
MINUS_ASSIGN: '-=';
MUL_ASSIGN: '*=';
DIV_ASSIGN: '/=';
MOD_ASSIGN: '%=';
CONCAT_ASSIGN: '@=';
INC: '++';
DEC: '--';
SHL: '<<';
SHR: '>>';
AND: '&&';
OR: '||';
EQ: '==';
NEQ: '!=';
LTE: '<=';
GTE: '>=';
ASSIGN: '=';
LT: '<';
GT: '>';
PLUS: '+';
MINUS: '-';
MUL: '*';
DIV: '/';
MOD: '%';
CONCAT: '@';
NOT: '!';
BAND: '&';
BOR: '|';
BXOR: '^' | 'xor';
QUESTION: '?';
COLON: ':';
SEMI: ';';
COMMA: ',';
DOT: '.';
LPAREN: '(';
RPAREN: ')';
LBRACE: '{';
RBRACE: '}';
LBRACK: '[';
RBRACK: ']';
SPC: 'SPC';
NL: 'NL';
TAB: 'TAB';
NUMBER: '0' [xX] [0-9a-fA-F]+ | [0-9]+ ('.' [0-9]*)? | '.' [0-9]+;
STRING: '"' ('\\' . | ~["\\])* '"';
CHAR: '\'' ('\\' . | ~['\\])* '\'';
IDENTIFIER: [A-Za-z_$] [A-Za-z0-9_$]*;
LINE_COMMENT: '//' ~[\r\n]* -> channel(HIDDEN);
BLOCK_COMMENT: '/*' .*? ('*/' | EOF) -> channel(HIDDEN);
WS: [ \t\r\n]+ -> channel(HIDDEN);
