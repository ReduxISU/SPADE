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
After the symbol "|" is a list of assertions or *constraints* which must be true of the instance, separated by commas that are assumed to be true for a valid problem instance. The language used for defining constraints in SPADE is modeled after the language of set theory. Each constraint says that some named element is either a basic object or related to the other objects in some way. A constraint is a terminal (one of the named collections from the format string) followed by a defintion and then an object. 

### Definition
- "is" checks the type of a collection. It can be followed by "set", "list", or "int", and constrains the collection to be that type. An example is "U is set" which constrains U to be a set
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

#### Literal
It will also be possible to specify an object as a literal, using the same format as instance strings, although this is not implemented yet.

