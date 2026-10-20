using SPADE;
using Xunit;

namespace SPADE.Tests;

// "is int", "is set" and "is list" must check the type they name (#10).
public class TypeConstraintTests
{
    private static void Parse(string grammar, string instance)
    {
        new StringParser(grammar).parse(instance);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("-12")]
    [InlineData("0")]
    public void IsIntAcceptsIntegers(string instance)
    {
        Parse("{N | N is int}", instance);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("{1}")]
    [InlineData("(1,2)")]
    public void IsIntRejectsNonIntegers(string instance)
    {
        Assert.ThrowsAny<Exception>(() => Parse("{N | N is int}", instance));
    }

    [Fact]
    public void IsIntChecksNestedFields()
    {
        string grammar = "{((N,E),K) | N is set, E subset N unorderedcross N, K is int}";
        Parse(grammar, "(({1,2},{{1,2}}),2)");
        Assert.ThrowsAny<Exception>(() => Parse(grammar, "(({1,2},{{1,2}}),two)"));
    }

    [Fact]
    public void IsIntChecksInsideSetComprehension()
    {
        string grammar = "{(N,E) | N is set, E subset {(e, w) | e is N unorderedcross N, w is int}}";
        Parse(grammar, "({1,2,3},{({1,2},4),({2,3},5)})");
        Assert.ThrowsAny<Exception>(() => Parse(grammar, "({1,2,3},{({1,2},heavy)})"));
    }

    [Theory]
    [InlineData("{1,2}")]
    [InlineData("{}")]
    public void IsSetAcceptsSets(string instance)
    {
        Parse("{S | S is set}", instance);
    }

    [Theory]
    [InlineData("balanced")]
    [InlineData("(1,2)")]
    public void IsSetRejectsValuesAndLists(string instance)
    {
        Assert.ThrowsAny<Exception>(() => Parse("{S | S is set}", instance));
    }

    [Theory]
    [InlineData("(1,2)")]
    [InlineData("()")]
    public void IsListAcceptsLists(string instance)
    {
        Parse("{L | L is list}", instance);
    }

    [Theory]
    [InlineData("balanced")]
    [InlineData("{1,2}")]
    public void IsListRejectsValuesAndSets(string instance)
    {
        Assert.ThrowsAny<Exception>(() => Parse("{L | L is list}", instance));
    }

    [Fact]
    public void SubsetIntStillChecksEveryElement()
    {
        Parse("{S | S subset int}", "{1,2,3}");
        Assert.ThrowsAny<Exception>(() => Parse("{S | S subset int}", "{1,2,x}"));
    }
}
