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

    private DefinitionDescription? definition;

    /// <summary>
    /// The names and types the definition string declares, read without parsing an instance.
    /// Read on first use; throws FormatException if the definition string can't be read.
    /// </summary>
    public DefinitionDescription Definition => definition ??= DefinitionDescription.Parse(format);

    public StringParser(string f)
    {
        format = f;
        hasParsed = false;
    }

    public void parse(string instance)
    {
        hasParsed = true;
        AntlrInputStream stream = new AntlrInputStream(format);
        collLexer lex = new collLexer(stream);
        CommonTokenStream tokens = new CommonTokenStream(lex);
        collParser parser = new collParser(tokens);

        parser.BuildParseTree = true;
        IParseTree parseTree = parser.start();
        codeGen gen = new codeGen();
        ParseTreeWalker.Default.Walk(gen, parseTree);
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