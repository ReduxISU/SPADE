## Installation

to install SPADE into a c# project use
```bash
dotnet add package SPADE
```

and then import with
```c#
using SPADE;
```

## Code example

```c#
string definitionString = "{{(N,E) | N is set, E subset N cross N";
string InstanceString = "({A,B,C,D},{(A,B),(B,C),(C,D),(D,A),(A,C)})";

StringParser graph = new(defintionString);
graph.parse(instanceString);
    
Console.WriteLine(graph["N"]);

foreach (UtilCollection c in graph["E"])
{
    Console.WriteLine(c);
}
```

Above is example code for parsing a graph type problem. The StringParser class is provided with a [definition string](DefinitionString.md), and then with a [instance string](InstanceString.md). It then parses and verifies the instance string. Now, you can access items of that problem by using a key access, which returns a [UtilCollection](utilcollection.md) class, which can then be further used however you need to use it. 

## Handling bad input

Every problem with an instance string throws `SpadeParseException`: text that can't be read, a shape that doesn't match the definition string, or a failed constraint. Catch it to report bad input, for example as a 400 response:

```c#
try
{
    graph.parse(instanceString);
}
catch (SpadeParseException e)
{
    // e.g. "expected , or } at position 5, found end of input"
    // or   "Constraint E subset N cross N failed: (A,X) is not a member"
    Console.WriteLine(e.Message);
}
```

Any other exception type means a bug in SPADE or in the definition string, not bad input.

## Describing a definition string

`Definition` tells you the names and types a [definition string](DefinitionString.md) declares, without needing an instance string. This is useful for building an editor or form for a problem before any instance exists.

```c#
StringParser problem = new("{((N,E),K) | N is set, E subset N unorderedcross N, K is int}");
DefinitionDescription d = problem.Definition;

Console.WriteLine(d.Outline);       // ((N,E),K)

foreach (DefinitionPart part in d.Parts)
{
    // N: Set at [0,0]
    // E: Subset of N unorderedcross N at [0,1]
    // K: Int at [1]
    Console.WriteLine($"{part} at [{string.Join(",", part.Path)}]");
}

Console.WriteLine(d["E"].Constraints[0]); // E subset N unorderedcross N
```

Each part has a `Name`, a `Kind` (`Set`, `Multiset`, `List`, `Int`, `Subset`, `Member`, or `Any` when nothing says what it is), `Of` (what it is a subset or member of, as written), `Path` (where it sits in the outline), and `Constraints` (every constraint naming it, as written). Only the outer format's names are parts; names inside a nested set, like `a` in `S subset {a | a subset U}`, are not.

`Definition` is read the first time you use it, so creating a `StringParser` never throws. If the definition string can't be read, `Definition` throws a `FormatException` naming it. `DefinitionDescription.Parse(definitionString)` does the same without a `StringParser`.
