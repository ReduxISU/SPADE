using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Xunit;

namespace SPADE.Tests;

// The generated parser and collBaseListener are public, so a caller can walk a definition string's
// parse tree. These pin down the tree coll.g4 gives for each kind of definition.
public class GrammarListenerTests
{
    // Records each rule as it is entered and each token as it is visited; every other callback
    // is collBaseListener's.
    private class Recorder : collBaseListener
    {
        private readonly collParser parser;
        public readonly List<string> Rules = new();
        public readonly List<string> Tokens = new();

        public Recorder(collParser parser)
        {
            this.parser = parser;
        }

        public override void EnterEveryRule(ParserRuleContext context)
        {
            Rules.Add(parser.RuleNames[context.RuleIndex]);
        }

        public override void VisitTerminal(ITerminalNode node)
        {
            Tokens.Add(node.GetText());
        }
    }

    private static (Recorder Recorder, collParser.StartContext Tree, collParser Parser) Walk(string definition)
    {
        collLexer lexer = new(new AntlrInputStream(definition));
        collParser parser = new(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();
        collParser.StartContext tree = parser.start();
        Recorder recorder = new(parser);
        ParseTreeWalker.Default.Walk(recorder, tree);
        return (recorder, tree, parser);
    }

    [Fact]
    public void WalksEveryRuleAndTokenInOrder()
    {
        var (recorder, _, parser) = Walk("{(N,E) | N is set, E subset N cross N}");

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        Assert.Equal(
            new[] { "start", "set", "format", "format", "format", "constraint", "object", "constraint", "object", "object", "object" },
            recorder.Rules);
        Assert.Equal(
            new[] { "{", "(", "N", ",", "E", ")", "|", "N", "is", "set", ",", "E", "subset", "N", "cross", "N", "}", "<EOF>" },
            recorder.Tokens);
    }

    [Fact]
    public void BinaryOperatorsNestToTheLeft()
    {
        var (_, tree, _) = Walk("{(A,B,C,D) | D subset A union B intersection C}");

        collParser.ObjectContext obj = tree.set().constraint(0).@object();
        Assert.Equal("intersection", obj.BIN_OP().GetText());
        Assert.Equal("AunionB", obj.@object(0).GetText());
        Assert.Equal("union", obj.@object(0).BIN_OP().GetText());
        Assert.Equal("C", obj.@object(1).TERM().GetText());
    }

    [Fact]
    public void LogicalOperatorsAndNotFormOneConstraint()
    {
        var (recorder, tree, parser) = Walk("{(A,B) | A is set or not B is int}");

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        collParser.ConstraintContext constraint = Assert.Single(tree.set().constraint());
        Assert.Equal("or", constraint.BIN_LOG_OP().GetText());
        Assert.Equal("Aisset", constraint.constraint(0).GetText());
        Assert.NotNull(constraint.constraint(1).NOT());
        Assert.Equal("int", constraint.constraint(1).constraint(0).@object().BUILT_IN_SET().GetText());
        Assert.Equal(4, recorder.Rules.Count(r => r == "constraint"));
    }

    [Fact]
    public void ListAndSetLiteralsAreObjects()
    {
        var (recorder, tree, parser) = Walk("{(A,B) | A subset (a,b), B subset {c,d}}");

        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        collParser.ListContext list = tree.set().constraint(0).@object().list();
        Assert.Equal(new[] { "a", "b" }, list.TERM().Select(t => t.GetText()));
        Assert.Single(list.CM());

        collParser.SetContext literal = tree.set().constraint(1).@object().set();
        Assert.Null(literal.format());
        Assert.Equal(new[] { "c", "d" }, literal.TERM().Select(t => t.GetText()));
        Assert.Contains("list", recorder.Rules);
        Assert.Equal(2, recorder.Rules.Count(r => r == "set"));
    }

    // With the default listeners off, the parser still recovers and counts what it skipped.
    [Theory]
    [InlineData("{N | N is set")]
    [InlineData("{N | N is set}}")]
    [InlineData("{N | N is (}")]
    [InlineData("{N | N subset (a,}")]
    [InlineData("{(N,) | N is set}")]
    public void MalformedDefinitionsAreCountedAsSyntaxErrors(string definition)
    {
        var (_, _, parser) = Walk(definition);
        Assert.True(parser.NumberOfSyntaxErrors > 0);
    }
}
