using System.Diagnostics;
using System.Net.Http.Headers;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

using SPADE.Grammar;

namespace SPADE;

public class StringParser
{
    readonly string format;
    public Dictionary<string, UtilCollection> map = new();

    private bool hasParsed;

    // The definition string's parse tree, read once when the parser is made.
    private readonly IParseTree definitionTree;

    private DefinitionDescription? definition;

    /// <summary>
    /// The names and types the definition string declares, read without parsing an instance.
    /// Read on first use; throws FormatException when the definition string is a list of values, not a format followed by |.
    /// </summary>
    public DefinitionDescription Definition => definition ??= DefinitionDescription.Parse(format);

    /// <summary>
    /// Makes a parser for instances of a definition string such as "{(N,E) | N is set, E subset N unorderedcross N}".
    /// Throws FormatException naming the definition string, and the position of the first error, when it can't be read;
    /// a malformed definition is rejected rather than guessed at.
    /// </summary>
    public StringParser(string f)
    {
        definitionTree = ThrowingErrorListener.Parse(f);
        format = f;
        hasParsed = false;
    }

    public void parse(string instance)
    {
        hasParsed = true;
        codeGen gen = new codeGen();
        ParseTreeWalker.Default.Walk(gen, definitionTree);
        gen.Parse(new UtilCollection(instance));


        map = gen.map;

    }

    public UtilCollection this[string key]
    {
        get
        {
            if (!hasParsed) throw new InvalidOperationException("Must parse an instance string first");
            return map[key];
        }
    }
}