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

## Errors
An instance string that can't be read throws `SpadeParseException`, which says where the problem is, what was expected and what was found:

```
expected , or } at position 5, found end of input
```

for `{1,2`. Its `Position` (counting from 1), `Expected` and `Found` properties hold the same details. Nothing is guessed: unbalanced braces, extra text after the instance, empty elements such as `{1,,2}`, and empty input are all errors.

Sets and lists can be nested up to 256 levels deep. Deeper input is rejected with a `SpadeParseException` rather than risking a stack overflow.

## Multisets
A multiset is written with curly braces, like a set: `{3,3,2,2}`. Read on its own, that is a set, and the repeated elements are dropped, leaving `{3,2}`. When the definition string marks the collection with `is multiset`, every element is kept, so `{3,3,2,2}` has four elements.

Two multisets are equal when they hold the same elements the same number of times, in any order. A set and a multiset are never equal, even with the same elements.
