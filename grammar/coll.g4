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
          | TERM
          | BUILT_IN
          | BUILT_IN_SET
          ;
set       : '{' format  '|' constraint? (CM constraint)*  '}' 
          | '{' TERM (CM TERM)*  '}' 
          ;
list      : '(' TERM (CM TERM)* ')';
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
		  ;
BUILT_IN_SET: 'int';
TERM      : [a-zA-Z0-9.]+ ;
WS        : [ \t\r\n]+ -> skip ;
CM        : [,] ; 
