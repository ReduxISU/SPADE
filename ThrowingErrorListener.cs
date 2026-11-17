using Antlr4.Runtime;

using SPADE.Grammar;

namespace SPADE;

/// <summary>
/// Turns the first lexer or parser error into a FormatException naming the definition string,
/// instead of ANTLR's default of printing the error and guessing past it (#4).
/// </summary>
internal class ThrowingErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
{
    private readonly string definition;

    public ThrowingErrorListener(string definition)
    {
        this.definition = definition;
    }

    /// <summary>
    /// Reads a definition string into its parse tree. Throws FormatException naming the definition string,
    /// and the position of the first error, when it can't be read. Nothing is printed.
    /// </summary>
    public static collParser.StartContext Parse(string definition)
    {
        if (definition == null) throw new FormatException("Definition string is null");

        ThrowingErrorListener listener = new(definition);
        collLexer lexer = new(new AntlrInputStream(definition));
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(listener);
        collParser parser = new(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();
        parser.AddErrorListener(listener);
        parser.BuildParseTree = true;
        return parser.start();
    }

    public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        Fail(charPositionInLine, msg);
    }

    public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        Fail(charPositionInLine, msg);
    }

    private void Fail(int charPositionInLine, string msg)
    {
        throw new FormatException($"Definition string \"{definition}\" can't be read: {msg} at position {charPositionInLine + 1}");
    }
}
