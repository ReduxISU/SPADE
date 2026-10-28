# Definition String
SPADE models a new problem as defined by a *definition string*. This string allows the user to specify how an instance of the problem is to be formatted, and the constraints that that problem should follow. SPADE follows a set builder notation, such that the set builder describes the set of every possible instance string of that problem. SPADE is equipped to parse and validate that instance strings are in the set as defined by the definition string. Using [Set Cover](https://en.wikipedia.org/wiki/Set_cover_problem) as an example, a possible definition string is as follows:
```
{(U,S) | U is set, S subset {a | a subset U}}
```
This is a description of the Set Cover problem as a pair, where the first element in the pair is the universal set U, and the second element is a set that is defined as a subset of all possible subsets of U, which is an equivalent way to phrase that every element of S should be a subset of U. This is a version of the formal definition of the Set Cover problem.

The definition string is made out of the following parts:

## Format String
In the definition string, first there is a *format string*, which is the part to the left of the "|". The format string represents the high-level structure of a problem, and gives names to the different parts of it. This uses the same convention as the problem instance strings do, with sets being represented by curly braces and lists with parentheses. Then, each element inside the collection is given a name, which is used in the later parts of the definition string. In the set cover example, **(U,S)** means that set cover is a list of size 2 (i.e., an ordered pair) where the first element is called **U** and the second is called **S**, where **U** and **S** each contain the specific elements of a particular problem instance.

## Constraint
After the symbol "|" is a list of assertions or *constraints* which must be true of the instance, separated by commas that are assumed to be true for a valid problem instance. The language used for defining constraints in SPADE is modeled after the language of set theory. Each constraint says that some named element is either a basic object or related to the other objects in some way. A constraint is a terminal (one of the named collections from the format string) followed by a definition and then an object. 

### Definition
- "is" checks the type of a collection. It can be followed by "set", "list", "int", "number" or "string", and constrains the collection to be that type. An example is "U is set" which constrains U to be a set
- "is multiset" marks a collection written with curly braces as a multiset, so repeated elements are kept instead of dropped. For example, "{N | N is multiset, N subset int}" reads {3,3,2,2} as four numbers. It applies before the other constraints, wherever it is written. See [multisets](InstanceString.md#multisets).
- "is number" checks that a collection is a single decimal number, such as a coordinate or a weight. It accepts an optional `+` or `-` sign, then digits with an optional fractional part, or a fraction with a leading dot: `5`, `-3`, `+7`, `2.5`, `-0.75`, `.5` and `-.5` are all numbers. It rejects exponents (`1e3`, `2E-4`), `NaN` and infinities, a trailing dot (`5.`), more than one dot (`1.2.3`), a sign or dot on its own, thousands separators (`"1,000"`), whitespace inside the number, and anything too large to hold as a double. The decimal point is always `.`, whatever language the machine is set to, so `"2,5"` is not a number. Every value `int` accepts is also a `number`, and a quoted value like `"2.5"` counts the same as `2.5`. Like `int`, `number` is also a set: "S subset number" and "P subset number cross number" (points with real coordinates, as in Convex Hull) both work, as does "P subset {(x,y) | x is number, y is number}". Since `number` is a keyword, it can't be used as a name in the format string or a constraint.
- "is string" checks that a collection is a single value, not a set, list or multiset. Any value counts, a word like `kitten`, words with spaces like `New York`, or a number like `5`. For example, "{(x,y) | x is string, y is string}" accepts (kitten,sitting) and rejects (kitten,{sitting}). A value with commas or brackets in it can be written in quotes, like "hello, world"; see [quoted values](InstanceString.md#quoted-values). Since `string` is a keyword, it can't be used as a name in the format string or a constraint.
- "is list int", "is matrix int" and "is tensor 3 int" check that a collection is a list, matrix or tensor whose elements all have one type. See [matrices and tensors](#matrices-and-tensors).
- "subset" checks that the collection is a subset of the object on the right. An example is "A subset B" which constrains A to be a subset of B. 

### Objects
Objects are a type of collection that can be built out of named collections and various operations on those collections. 

#### Binary Operations
One way to build an object is with binary operations, building an object out of two other objects. SPADE supports the following:
- "union" gives the union of two sets, "A union B" results in the union of A and B
- "intersection" gives the intersection of two sets, "A intersection B" results in the intersection of A and B, or only the elements in both A and B
- "cross" gives the cross product of two sets, "A cross B" results in a pair of every element of A with every element from B
- "unorderedcross" similar to cross, it gives the cross product of two sets, but with each element as a set instead of a list, "A unorderedcross B"

#### Set comprehension
Another way to build an object is with set comprehension. This allows SPADE to nest a definition inside of a larger definition. An example is "S subset {a | a subset U}", which constrains S to be a subset of all possible sets described by the set comprehension, which in this case is all possible subsets of U.

### Matrices and tensors
A matrix or tensor is a rectangular grid of values, written in an instance string as nested lists, row by row: the matrix with rows 1 2 and 3 4 is `((1,2),(3,4))`. See [matrices and tensors](InstanceString.md#matrices-and-tensors) for the instance side.

- "C is matrix int" says C is a matrix, 2 dimensions, of ints.
- "T is tensor 3 int" says T has 3 dimensions. Any number from 1 up works: "tensor 1" is a vector and "tensor 2" is the same as "matrix".
- "d is list int" says d is a list of ints. It is the same check as "tensor 1 int"; a plain "d is list" still accepts any list.

The element type after "list", "matrix" or "tensor n" is `int`, `number` or `string`, with the same meaning as "is int", "is number" and "is string".

By default the sizes are free: they are read from the instance. The value must be rectangular, so every row has the same length, at every depth. `((1,2,3),(4,5))` is not a matrix, and fails with `row 2 of C has 2 values, expected 3`.

A definition can name the sizes with "size", one per dimension, outermost first. Each is a name or a whole-number constant:

```
{(C,d) | C is matrix int size m n, d is list int size m}
{G | G is matrix int size 9 9}
{G | G is matrix int size n n}
```

The first is 0-1 Integer Programming: C has m rows and n columns, and d has one value per row of C. The second is a 9×9 Sudoku grid, and the third any square grid. A name takes the value of the first dimension it is read from, and every other dimension with that name must have the same value, in the same part or in another part of the instance. When a size has the name of a part of the format that holds a whole number, such as n in `{(n,G) | n is int, G is matrix int size n n}`, it takes that part's value. Sizes start fresh for each instance, and for each member of a nested set.

A failed check throws `SpadeParseException` with `Part` set to the part and `Element` to the row or element that failed, and a message naming its position, counting from 1:

- `Constraint C is matrix int failed: row 3 of C has 2 values, expected 3`
- `Constraint C is matrix int size m n failed: C has 3 rows but m is 4 (from d)`
- `Constraint C is matrix int failed: element (2,1) of C is not an int`
- `Constraint T is tensor 3 int failed: row (2,2) of T has 1 value, expected 2`

Constraints are checked from the last one written to the first, so in the 0-1 Integer Programming definition d gives m its value, and a C with the wrong number of rows is the one blamed.

Some definitions can't be checked, so they are rejected when the definition string is read, with a `FormatException`: an element type after anything but "list", "matrix" or "tensor n" (`set int`), an element type other than `int`, `number` or `string`, a tensor with no dimensions (`tensor 0 int`), the wrong number of sizes (`matrix int size m`), a size that is neither a name nor a whole number (`size 2.5 3`), and a shape after anything but "is" (`C subset matrix int`, or `matrix int union C`).

Since `matrix`, `tensor` and `size` are keywords, they can't be used as names in the format string or a constraint.

After parsing, `StringParser.GetTensor(name)` returns the part as a `Tensor`, with its `ElementType`, `Shape` (for example `[2, 3]` for 2 rows of 3), `Rank`, `Count`, and its elements by index, counting from 0: `t[1, 2]`, `t.GetInt(1, 2)` or `t.GetNumber(1, 2)`. `parser["C"]` still returns the nested lists. The parts' element types, ranks and sizes are also in the [definition description](example.md#describing-a-definition-string).

#### Literal
It will also be possible to specify an object as a literal, using the same format as instance strings, although this is not implemented yet.

