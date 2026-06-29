parser grammar GS2Parser;

options { tokenVocab=GS2Lexer; language=CSharp; }

script: declaration* EOF;
declaration: constDeclaration | enumDeclaration | functionDeclaration | statement;
constDeclaration: CONST IDENTIFIER ASSIGN expression SEMI?;
enumDeclaration: ENUM IDENTIFIER LBRACE enumMember (COMMA enumMember)* COMMA? RBRACE SEMI?;
enumMember: IDENTIFIER (ASSIGN expression)?;
functionDeclaration: PUBLIC? FUNCTION qualifiedName LPAREN parameterList? RPAREN statement?;
parameterList: argumentList;
block: LBRACE statement* RBRACE;
statement: block | ifStatement | forStatement | whileStatement | switchStatement | withStatement | newStatement | returnStatement | breakStatement | continueStatement | expressionStatement | SEMI;
ifStatement: IF LPAREN expression RPAREN statement (ELSE statement | ELSEIF ifTail)?;
ifTail: LPAREN expression RPAREN statement (ELSE statement | ELSEIF ifTail)?;
forStatement: FOR LPAREN expression SEMI expression SEMI expression? RPAREN statement | FOR LPAREN SEMI expression SEMI expression? RPAREN statement | FOR LPAREN expression COLON expression RPAREN statement;
whileStatement: WHILE LPAREN expression RPAREN statement;
switchStatement: SWITCH LPAREN expression RPAREN LBRACE switchCase* RBRACE;
switchCase: (CASE expression | DEFAULT) COLON statement*;
withStatement: WITH LPAREN expression RPAREN statement;
newStatement: NEW qualifiedName LPAREN argumentList? RPAREN block?;
returnStatement: RETURN expression? SEMI?;
breakStatement: BREAK SEMI?;
continueStatement: CONTINUE SEMI?;
expressionStatement: expression SEMI?;
expression: expression QUESTION expression COLON expression
	| expression op=(MUL | DIV | MOD | POW) expression
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
	| expression op=(ASSIGN | WALRUS | PLUS_ASSIGN | MINUS_ASSIGN | MUL_ASSIGN | DIV_ASSIGN | POW_ASSIGN | MOD_ASSIGN | CONCAT_ASSIGN | SHL_ASSIGN | SHR_ASSIGN) expression
	| expression CONCAT expression
	| prefixExpression;
prefixExpression: op=(INC | DEC | NOT | MINUS | CONCAT | BIT_INVERT) prefixExpression | postfixExpression;
postfixExpression: primaryExpression postfixPart*;
postfixPart: LPAREN argumentList? RPAREN | DOT IDENTIFIER | DOT LPAREN expression RPAREN | LBRACK argumentList? RBRACK | op=(INC | DEC);
primaryExpression: NUMBER | STRING | TRUE | FALSE | NULL | castExpression | qualifiedName | arrayLiteral | lambdaExpression | newExpression | LPAREN expression RPAREN;
castExpression: INT_CAST LPAREN expression RPAREN | FLOAT_CAST LPAREN expression RPAREN | TRANSLATE LPAREN expression RPAREN;
arrayLiteral: LBRACE argumentList? RBRACE;
lambdaExpression: FUNCTION LPAREN argumentList? RPAREN statement;
newExpression: NEW qualifiedName LPAREN argumentList? RPAREN | NEW arrayRank+;
arrayRank: LBRACK NUMBER RBRACK;
rangeExpression: BOR expression (COMMA expression)? BOR | expression;
argumentList: expression (COMMA expression)* COMMA?;
numberList: NUMBER (COMMA NUMBER)* COMMA?;
qualifiedName: IDENTIFIER (DOT IDENTIFIER)*;
