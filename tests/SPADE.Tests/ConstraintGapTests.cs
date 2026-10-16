using SPADE;
using Xunit;

namespace SPADE.Tests;

// Definitions coll.g4 accepts but StringParser.parse doesn't handle. Each test states the behaviour
// wanted and is skipped until it's fixed; the reason says what happens today.
public class ConstraintGapTests
{
    private static StringParser Parse(string grammar, string instance)
    {
        StringParser parser = new StringParser(grammar);
        parser.parse(instance);
        return parser;
    }

    [Fact(Skip = "Bug: not, and, or and the other logical operators parse, but parse() throws NullReferenceException in codeGen.ExitConstraint")]
    public void LogicalConstraintsAreChecked()
    {
        Assert.Equal("a", Parse("{N | not N is int}", "a")["N"].ToString());
        Assert.Throws<SpadeParseException>(() => Parse("{N | not N is int}", "5"));
        Assert.Equal(new UtilCollection("{a}"), Parse("{N | N is set or N is list}", "{a}")["N"]);
        Assert.Throws<SpadeParseException>(() => Parse("{N | N is set and N is list}", "{a}"));
    }

    [Fact(Skip = "Bug: a set or list literal on the right of subset throws InvalidOperationException (Stack empty) from codeGen.ExitSet")]
    public void SubsetOfALiteralIsChecked()
    {
        Assert.Equal(new UtilCollection("{a}"), Parse("{N | N subset {a,b}}", "{a}")["N"]);
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => Parse("{N | N subset {a,b}}", "{c}"));
        Assert.Equal("N", e.Part);
        Assert.Equal("c", e.Element);
    }

    [Fact(Skip = "Bug: 'x in A' is never checked, so ({1},2) is accepted (ConstraintDescriptor.check has no 'in' case; see #14)")]
    public void InChecksMembership()
    {
        Assert.Equal("1", Parse("{(A,x) | A is set, x in A}", "({1},1)")["x"].ToString());
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => Parse("{(A,x) | A is set, x in A}", "({1},2)"));
        Assert.Equal("x", e.Part);
    }

    [Theory(Skip = "Bug: parse() keeps ANTLR's default error recovery, so a malformed definition is printed to the console and read as best it can, instead of throwing like Definition does")]
    [InlineData("{N | N is set")]
    [InlineData("{N | N is set#}")]
    [InlineData("{N | N is set}}")]
    public void MalformedDefinitionThrowsOnParse(string grammar)
    {
        Assert.Throws<FormatException>(() => Parse(grammar, "{a}"));
    }
}
