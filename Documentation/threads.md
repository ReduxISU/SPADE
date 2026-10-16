# Using SPADE from several threads

SPADE's objects follow the usual .NET rule: build on one thread, then share. Once an instance has been parsed and frozen, any number of threads can read it at the same time.

## Safe to share

- **A frozen `UtilCollection`.** Hashing, `Equals`, `Contains`, `Count`, indexing, enumerating and `ToString` are all safe from several threads at once. A frozen collection can't change, and its hash is computed once and published safely.
- **Sets as lookup tables.** Everything inside a set is frozen as soon as it goes in, so a shared `HashSet<UtilCollection>` can't be changed under another thread (as long as no thread adds to the set itself).

## Not safe to share

- **A `StringParser` while it parses.** `parse()` replaces the parser's results, so two threads parsing with the same `StringParser` overwrite each other. Use one `StringParser` per thread, or per parse; they are cheap to create. Reading the results of a finished parse from several threads is fine.
- **A collection that is still being built.** Don't call `Add`, or assign to an index, while another thread reads the collection. The same is true of `List` and `HashSet`.

## Recommended pattern

Parse once, freeze the result, then hand it to the workers:

```c#
StringParser parser = new("{((N,E),K) | N is set, E subset N unorderedcross N, K is int}");
parser.parse(instance);

UtilCollection nodes = parser["N"].Freeze();
UtilCollection edges = parser["E"].Freeze();

Dictionary<string, int> degree = new();
Parallel.ForEach(nodes.ToList(), node =>
{
    // Every thread reads the same frozen collections.
    int d = edges.ToList().Count(edge => edge.Contains(node));
    lock (degree) degree[node.ToString()] = d;
});
```

`Freeze()` freezes a collection and everything inside it, and returns the same collection. After that, any attempt to change it throws `InvalidOperationException`, so a stray `Add` in a worker fails loudly instead of corrupting shared data. `IsFrozen()` tells you whether a collection is frozen.
