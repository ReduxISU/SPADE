
# UtilCollection

## Introduction
UtilCollections is a class that combines sets or lists, or *collections*, into one type of object, and provides a constructor that builds specified collections from strings. UtilCollections can represent a recursive structure, as the elements of the collection can themselves be collections. Otherwise, elements can be represented as strings.

UtilCollections is meant as a helper class to parse instances of problems in Redux. The class understands the format of Problem instance strings (the strings that define a particular instance of a problem). It provides methods that assert that the problem instance correctly defines a problem's variables, and also provides methods to do calculations with those variables. 

## Format of input string
UtilCollection expects strings in the format that is typically used by Redux. A set (or unordered collection) of elements \(e_0,...,e_k\) uses curly braces as follows:

```
{e_0,...,e_k}
```

So a set may look something like \{a,b,c\}. 

A list (or ordered collection) will be represented using parenthesis as follows:

```
(e_0,...,e_k)
```

As such, an example of a list could look something like (1,2,3). 

As an example, the default instance of hamiltonian path is:

```
({1,2,3,4,5},{{2,1},{1,3},{2,3},{3,5},{2,4},{4,5}})
```

Hamiltonian path is a graph problem, and the string follows the formal definition of a graph, namely a list whose first element is a set of the nodes, and the second element is a set of the edges. 

## Utility Methods
UtilCollections provides a few Utility methods to help process and extract the instance information. Each method works for both ordered and unordered collections, unless specified otherwise.

### Indexing
If the collection is ordered, then you can index into the list using square brackets to get each element of the list, the same as you would for an array. Returns a UtilCollections object. Assigning to an index (`list[0] = item`) replaces that element, unless the list has been hashed (see [Collections freeze once hashed](#collections-freeze-once-hashed)).

### Enumeration
Using foreach loops to iterate over each element in the collection is also possible, for example, using a UtilCollection object named collection:

```
foreach (UtilCollection item in collection)
```

### Intersect
Returns a new UtilCollection object that is the intersection of the two collections. Only works with unordered collections.

### Union
Returns a new UtilCollections object that is the union of the two collections. Only works with unordered collections.

### Equals
Returns True if the two UtilCollections objects are equal to each other and False otherwise. Comparison is done by value, so if two different instances of the UtilCollections class that have the same structure and elements are compared, equals will return True. 

### Count
Returns the length of the collection.

### ToList
Returns a list representing the elements of the collection. If the collection is ordered, it returns a copy of the list, so changing the copy doesn't change the collection. If the collection is unordered, it is cast to a list and then returned.

### ToString
Returns the string representation of the UtilCollection.

### Contains
Checks if the collection contains another UtilCollection object. Comparison is done by value. Returns true if it contains it, false if it doesn't.

### Add
Adds a UtilCollection object to the collection. Throws `InvalidOperationException` if the collection has been hashed (see below).

### Collections freeze once hashed
A `HashSet` or `Dictionary` files each element under the hash code it had when it went in. If the element changed afterwards, it would sit under a stale hash: `Contains` would miss it and duplicates could slip in, with no error. So the first time a UtilCollection is hashed, it freezes, and `Add` or assigning to an index throws `InvalidOperationException`. That happens when it is added to a set (including a UtilCollection set), used as a dictionary key, or put inside a list or set that is itself hashed, since hashing a collection hashes everything inside it.

To freeze a collection on purpose, call `Freeze()`. It freezes the collection and everything inside it, and returns the collection, so `new UtilCollection(text).Freeze()` works. `IsFrozen()` says whether a collection is frozen.

Build each collection fully, then add it to a set:

```c#
UtilCollection edge = new UtilCollection("{}");
edge.Add(new UtilCollection("1"));
edge.Add(new UtilCollection("2"));
edges.Add(edge); // edge is frozen from here on
```

A collection that has never been hashed can still be changed freely. The constructors that take a `List<UtilCollection>` or `HashSet<UtilCollection>` copy it, so changing the original afterwards doesn't change the collection.

A frozen collection can't change, so it is safe to share between threads: hashing, comparing, `Contains` and enumerating it from several threads at once all work. Building a collection is not thread-safe, the same as `List` and `HashSet`: fill it on one thread, then share it.

### parseInt
Attempts to parse and return itself as an integer.

## Assertion Methods
UtilCollection also provides ways to assert that the instance is structured the way that it should be, that things are sets where they should be, and things are lists where they should be. Asserts will cause an error to be thrown if the conditions are not met.

### assertOrdered
Asserts that the collection is ordered (i.e., a list).

### assertUnordered
Asserts that the collection is unordered (i.e., a set).

### assertPair
Asserts that the collection is both ordered (i.e., a list) and has a certain length. The default length is 2, and a custom length can be passed in.

### assertCount
Asserts that the collection has the same count or length as the argument. Works for both unordered and ordered collections.

## Example

The following code is how the knapsack problem uses the UtilCollections class to parse its problem instance.

```c#
public KNAPSACK(string HWVInput) {
    UtilCollection collection = new UtilCollection(HWVInput);
    instance = collection.ToString();
    collection.assertPair(3);
    items = collection[0];
    items.assertUnordered();
    W = int.Parse(collection[1].ToString());
    V = int.Parse(collection[2].ToString());
    foreach (UtilCollection item in items) item.assertPair();
}
```

Knapsack is defined as {<H, W, V> where H is a set of items (w,v) and there is a subset of items in H whose collective weight is less than or equal to W and whose collective value is equal or greater than V.} `collection.assertPair(3)` makes sure that the top level of the collection is a list with three elements, `H`, `W`, and `V`. We can then assign the first element of the collection to `items`, a field of the knapsack class. Since `W` and `V` are integers, we can parse them as so and assign them to the appropiate fields. The last line also asserts that each element in items is a pair, as described in the problem definition.
