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

    private Dictionary<string, Tensor> tensors = new();

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
        codeGen gen = new codeGen(format);
        ParseTreeWalker.Default.Walk(gen, definitionTree);
        gen.Parse(new UtilCollection(instance));


        map = gen.map;
        tensors = gen.tensors;

    }

    /// <summary>
    /// A part declared "is list int", "is matrix int" or "is tensor 3 number", read as a tensor with a shape (#30).
    /// Throws KeyNotFoundException when the part isn't declared with an element type.
    /// </summary>
    public Tensor GetTensor(string key)
    {
        if (!hasParsed) throw new InvalidOperationException("Must parse an instance string first");
        if (tensors.TryGetValue(key, out Tensor? tensor)) return tensor;
        throw new KeyNotFoundException($"{key} is not declared as a list, matrix or tensor with an element type");
    }

    /// <summary>Looks up a part read as a tensor without throwing; false when there is none.</summary>
    public bool TryGetTensor(string key, out Tensor? tensor)
    {
        if (!hasParsed) throw new InvalidOperationException("Must parse an instance string first");
        return tensors.TryGetValue(key, out tensor);
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