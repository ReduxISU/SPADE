using SPADE;
using Xunit;

namespace SPADE.Tests;

// union, intersection and cross in a definition's set expressions, and which part a failure names.
public class SetOperationTests
{
    private const string Union = "{(A,B,C) | A is set, B is set, C subset A union B}";
    private const string Intersection = "{(A,B,C) | A is set, B is set, C subset A intersection B}";

    private static StringParser Parse(string grammar, string instance)
    {
        StringParser parser = new StringParser(grammar);
        parser.parse(instance);
        return parser;
    }

    private static SpadeParseException Fails(string grammar, string instance)
    {
        return Assert.Throws<SpadeParseException>(() => new StringParser(grammar).parse(instance));
    }

    [Theory]
    [InlineData("({1},{2},{1,2})")]
    [InlineData("({1},{2},{2})")]
    [InlineData("({1,2},{2,3},{})")]
    public void UnionAcceptsElementsOfEitherSide(string instance)
    {
        StringParser parser = Parse(Union, instance);
        Assert.Equal(new UtilCollection(instance)[2], parser["C"]);
    }

    // Missing from both sides; the left part is the one named.
    [Fact]
    public void UnionRejectsElementsOfNeitherSide()
    {
        SpadeParseException e = Fails(Union, "({1},{2},{1,3})");

        Assert.Equal("Constraint C subset A union B failed: 3 is not a member", e.Message);
        Assert.Equal("C", e.Part);
        Assert.Equal("3", e.Element);
        Assert.Equal("3", e.MissingValue);
        Assert.Equal("A", e.ExpectedIn);
    }

    [Fact]
    public void UnionWithIntAcceptsIntegers()
    {
        const string grammar = "{(N,E) | N is set, E subset N union int}";
        Assert.Equal(new UtilCollection("{a,3,-4}"), Parse(grammar, "({a},{a,3,-4})")["E"]);

        SpadeParseException e = Fails(grammar, "({a},{a,3,b})");
        Assert.Equal("b", e.Element);
        Assert.Equal("N", e.ExpectedIn);
    }

    [Fact]
    public void IntersectionAcceptsElementsOfBothSides()
    {
        StringParser parser = Parse(Intersection, "({1,2},{2,3},{2})");
        Assert.Equal(new UtilCollection("{2}"), parser["C"]);
    }

    // The first side the element is missing from is named.
    [Theory]
    [InlineData("({1,2},{2,3},{1})", "1", "B")]
    [InlineData("({1,2},{2,3},{3})", "3", "A")]
    [InlineData("({1,2},{2,3},{4})", "4", "A")]
    public void IntersectionNamesTheSideTheElementIsMissingFrom(string instance, string element, string expectedIn)
    {
        SpadeParseException e = Fails(Intersection, instance);

        Assert.Equal($"Constraint C subset A intersection B failed: {element} is not a member", e.Message);
        Assert.Equal("C", e.Part);
        Assert.Equal(element, e.MissingValue);
        Assert.Equal(expectedIn, e.ExpectedIn);
    }

    [Fact]
    public void CrossNamesTheMissingHalfOfAPair()
    {
        SpadeParseException e = Fails("{(N,E) | N is set, E subset N cross N}", "({a},{(a,b)})");

        Assert.Equal("(a,b)", e.Element);
        Assert.Equal("b", e.MissingValue);
        Assert.Equal("N", e.ExpectedIn);
    }

    // A value is not a pair, so there is no missing half to name.
    [Fact]
    public void CrossNamesNoMissingPieceForANonPair()
    {
        SpadeParseException e = Fails("{(N,E) | N is set, E subset N cross N}", "({a},{b})");

        Assert.Equal("E", e.Part);
        Assert.Equal("b", e.Element);
        Assert.Null(e.MissingValue);
        Assert.Null(e.ExpectedIn);
    }
}
