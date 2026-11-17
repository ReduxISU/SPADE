using SPADE;
using Xunit;

namespace SPADE.Tests;

// Input that is well formed but doesn't fit the definition string throws SpadeParseException
// with a message naming what failed (#4).
public class DefinitionErrorTests
{
    private const string Graph = "{(N,E) | N is set, E subset N unorderedcross N}";

    private static SpadeParseException Fails(string grammar, string instance)
    {
        return Assert.Throws<SpadeParseException>(() => new StringParser(grammar).parse(instance));
    }

    [Theory]
    [InlineData("({1,2},{{1,2}},extra)", "expected a list of 2 elements for (N,E), found ({1,2},{{1,2}},extra)")]
    [InlineData("({1,2})", "expected a list of 2 elements for (N,E), found ({1,2})")]
    [InlineData("{{1,2},{{1,2}}}", "expected a list of 2 elements for (N,E), found ")]
    [InlineData("graph", "expected a list of 2 elements for (N,E), found graph")]
    public void WrongShapeNamesTheFormat(string instance, string messageStart)
    {
        Assert.StartsWith(messageStart, Fails(Graph, instance).Message);
    }

    [Fact]
    public void FailedSubsetNamesConstraintAndMember()
    {
        Assert.Equal(
            "Constraint E subset N unorderedcross N failed: {1,9} is not a member",
            Fails(Graph, "({1,2},{{1,9}})").Message);
    }

    [Fact]
    public void SubsetOfAValueFailsCleanly()
    {
        Assert.Equal(
            "Constraint E subset N unorderedcross N failed: edges is not a set",
            Fails(Graph, "({1,2},edges)").Message);
    }

    [Theory]
    [InlineData("{N | N is int}", "abc", "Constraint N is int failed: abc is not an integer")]
    [InlineData("{S | S is set}", "word", "Constraint S is set failed: word is not a set")]
    [InlineData("{L | L is list}", "{1}", "Constraint L is list failed: {1} is not a list")]
    public void FailedTypeCheckNamesConstraint(string grammar, string instance, string message)
    {
        Assert.Equal(message, Fails(grammar, instance).Message);
    }

    [Fact]
    public void LongValuesAreShortenedInMessages()
    {
        string big = "{" + string.Join(",", Enumerable.Range(0, 1000)) + "}";
        string message = Fails("{L | L is list}", big).Message;
        Assert.True(message.Length < 150, message);
        Assert.Contains("...", message);
    }

    [Theory]
    [InlineData("({a,b},{{a,b}})")]
    [InlineData("({a,b},{(a)})")]
    [InlineData("({a,b},{(a,b,a)})")]
    [InlineData("({a,b},{x})")]
    public void CrossRejectsNonPairsCleanly(string instance)
    {
        Fails("{(N,E) | N is set, E subset N cross N}", instance);
    }

    [Fact]
    public void SyntaxErrorsComeThroughStringParser()
    {
        Assert.Equal("expected a value, { or ( at position 9, found end of input", Fails(Graph, "({1,2},{").Message);
    }
}
