# Development
Feel free to contribute! Also feel free to open any issues on [github](https://github.com/Jetison333/SPADE) with bugs or feature requests.

## Tests
The tests live in tests/SPADE.Tests and use xUnit. Run them from the repository root with

```bash
dotnet test tests/SPADE.Tests
```

The test project targets .NET 10, so you need the .NET 10 SDK or newer. The library itself still targets .NET 6, and SDK 10 builds it.


## antlr
If you need to edit the grammar inside of coll.g4 file, use the following commands (you will need to install antlr4):

antlr commands should be done inside of grammar/
to test grammer rules, use

```bash
antlr4-parse coll.g4 start -gui
```

To regenerate the antlr code after editing coll.g4 use
```bash
antlr4 coll.g4
```