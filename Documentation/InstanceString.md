## Instance Strings
Instance strings represent an individual instance of a problem, and are made of a few components. The highest level is a *collection*, and SPADE makes a distinction of collections between sets and lists, where sets are unordered groupings and lists are ordered groupings. Collections contain *elements*, which may be another collection, or a basic object like an integer or word. Particular instance strings are typically nested, for example, set cover being a *pair* (a list of length 2) that contains a set of objects and a set of sets of objects. 

Collections are represented within an instance string as strings. We represent a set **U** of elements $e_0,...,e_k$ using curly braces as follows

```
{e_0,...,e_k}
```

So a set may look something like {a,b,c}. A list **V**, will be represented using parenthesis as follows
```
(e_0,...,e_k)
```
as such, an example of a list could look something like (1,2,3). Also note that since an element of a set or list may also be a set of a list, so a set of lists can be represented as {(1,2),(2,3)}.

## Whitespace
Whitespace is ignored where it can't be part of a value, so an instance can be spread over several lines:

```
{
  (1, 2),
  (2, 3)
}
```

reads the same as `{(1,2),(2,3)}`. The rules are:

- Spaces, tabs and line breaks before or after `{ } ( ) ,`, and at the start and end of the input, are skipped.
- Inside a value, whitespace next to `[ ] ; : =` is dropped, so `x1 : True` reads as `x1:True`.
- Other whitespace inside a value is kept. Plain spaces stay as typed, so `{New York, Los Angeles}` holds `New York`. A run containing a tab or line break becomes a single space, so a value wrapped onto two lines, `New` and `York`, reads as `New York`.

## Quoted values
A value can be written in double quotes, so it can hold text that would otherwise end it: `"hello, world"` is one value, and `{"a,b", c}` is a set of two. Inside quotes, everything is kept exactly as written, including `{ } ( ) ,` and all whitespace. Two escapes exist inside quotes: `\"` for a quote and `\\` for a backslash. Any other backslash in quotes is an error, as is a missing closing quote (`expected closing " at position 5, found end of input` for `"abc`).

The quotes aren't part of the value. `"kitten"` and `kitten` are the same value, so `"5"` passes `is int`, and a quoted value passes `is string`.

A quote can only start a value, so `ab"c"` is an error. Outside quotes a backslash is an ordinary character.

When a set or list is turned back into text, a value inside it is quoted only if it has to be: it contains `{ } ( ) , "`, is empty, or has whitespace that the rules above would change. Every other value is written as before, so `{"a,b",c}` prints as `{"a,b",c}` and `{"a","b"}` prints as `{a,b}`. A value on its own prints as its text, without quotes.

## Errors
An instance string that can't be read throws `SpadeParseException`, which says where the problem is, what was expected and what was found:

```
expected , or } at position 5, found end of input
```

for `{1,2`. Its `Position` (counting from 1), `Expected` and `Found` properties hold the same details. Nothing is guessed: unbalanced braces, extra text after the instance, empty elements such as `{1,,2}`, and empty input are all errors.

Sets and lists can be nested up to 256 levels deep. Deeper input is rejected with a `SpadeParseException` rather than risking a stack overflow.

## Matrices and tensors
A matrix is written as a list of rows, each row a list of values, row-major: `((1,2,3),(4,5,6))` is a matrix with 2 rows and 3 columns. A tensor nests one level further for each extra dimension, so `(((1,2),(3,4)),((5,6),(7,8)))` has shape 2×2×2. There are no new separators, and the [whitespace](#whitespace) rules let a matrix be spread over lines:

```
(
  (1, 2, 3),
  (4, 5, 6)
)
```

When the definition string says a part is a matrix or tensor (see [matrices and tensors](DefinitionString.md#matrices-and-tensors)), every row at the same depth must have the same length, and every element must be a single value of the element type.

Empty and small cases:

- `((7))` is a 1×1 matrix, and `(5)` a list of one.
- `()` read as a matrix has no rows. Its shape is 0×0: when a dimension is empty, every dimension after it can't be read, so it is reported as 0 and its size isn't checked against a constant or name.
- `((),())` is a matrix with 2 rows and no columns, shape 2×0.
- A row that is empty among full ones, `((1,2),())`, is not rectangular.

Two tensors are equal when their element type, shape and every element's text are the same, so `2` and `2.0` are different numbers, as they are in a set.

## Multisets
A multiset is written with curly braces, like a set: `{3,3,2,2}`. Read on its own, that is a set, and the repeated elements are dropped, leaving `{3,2}`. When the definition string marks the collection with `is multiset`, every element is kept, so `{3,3,2,2}` has four elements.

Two multisets are equal when they hold the same elements the same number of times, in any order. A set and a multiset are never equal, even with the same elements.
