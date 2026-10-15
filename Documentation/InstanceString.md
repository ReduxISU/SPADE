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