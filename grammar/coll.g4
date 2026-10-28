grammar coll;

options {
    language = CSharp;
}
start     : set EOF ;
format    : '{' format (CM format)* '}' 
          | '(' format (CM format)* ')' 
          | TERM
          ;
constraint: TERM DEFINITION object
          | constraint BIN_LOG_OP constraint
          | NOT constraint
          ;
BIN_LOG_OP: 'and'
          | 'or'
          | 'nand'
          | 'xor'
          | 'nor'
          | 'xnor'
          ;
NOT       : 'not' ;
DEFINITION: 'is' 
          | 'in' 
          | 'subset'
          ;
object    : object BIN_OP object
          | set 
          | list 
          | shaped
          | TERM
          | BUILT_IN
          | BUILT_IN_SET
          ;
set       : '{' format  '|' constraint? (CM constraint)*  '}' 
          | '{' TERM (CM TERM)*  '}' 
          ;
list      : '(' TERM (CM TERM)* ')';
// A list, matrix or tensor of scalars, with optional sizes (#30):
// "list int size m", "matrix number size m n", "tensor 3 string".
// Which heads and element types are allowed is checked after parsing.
shaped    : BUILT_IN scalar sizes?
          | MATRIX scalar sizes?
          | TENSOR TERM scalar sizes?
          ;
scalar    : BUILT_IN_SET
          | BUILT_IN
          ;
sizes     : SIZE TERM+ ;
BIN_OP    : 'union' 
          | 'intersection' 
          | 'cross'
          | 'unorderedcross'
          ;
BUILT_IN  : 'set' 
          | 'multiset' 
          | 'list' 
          | 'graph' 
          | 'weightedGraph' 
          | 'string' 
		  ;
BUILT_IN_SET: 'int' | 'number';
MATRIX    : 'matrix' ;
TENSOR    : 'tensor' ;
SIZE      : 'size' ;
TERM      : [a-zA-Z0-9.]+ ;
WS        : [ \t\r\n]+ -> skip ;
CM        : [,] ; 
