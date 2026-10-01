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
