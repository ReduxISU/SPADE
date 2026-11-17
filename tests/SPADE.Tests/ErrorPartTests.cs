using SPADE;
using Xunit;

namespace SPADE.Tests;

// A failed constraint names the part it belongs to, the element that failed, and the piece missing
// from another part, so a caller doesn't have to read the message (#19).
public class ErrorPartTests
{
    private const string Graph = "{(N,E) | N is set, E subset N unorderedcross N}";
    private const string Digraph = "{(N,E) | N is set, E subset N cross N}";
    // "e is N unorderedcross N" is not checked yet (see #14); these tests fail on w instead.
    private const string WeightedGraph = "{(N,E) | N is set, E subset {(e,w) | e is N unorderedcross N, w is int}}";

    private static SpadeParseException Fails(string grammar, string instance)
    {
        return Assert.Throws<SpadeParseException>(() => new StringParser(grammar).parse(instance));
    }

    [Fact]
    public void FailedSubsetNamesPartElementAndMissingValue()
    {
        SpadeParseException e = Fails(Graph, "({1,2},{{1,9}})");
        Assert.Equal("Constraint E subset N unorderedcross N failed: {1,9} is not a member", e.Message);
        Assert.Equal("E", e.Part);
        Assert.Equal("{1,9}", e.Element);
        Assert.Equal("9", e.MissingValue);
        Assert.Equal("N", e.ExpectedIn);
    }

    [Fact]
    public void CrossNamesTheMissingNode()
    {
        SpadeParseException e = Fails(Digraph, "({a,b},{(a,b),(a,c)})");
        Assert.Equal("E", e.Part);
        Assert.Equal("(a,c)", e.Element);
        Assert.Equal("c", e.MissingValue);
        Assert.Equal("N", e.ExpectedIn);
    }

    [Fact]
    public void CrossOfTwoPartsNamesTheSideThatIsMissingTheValue()
    {
        SpadeParseException e = Fails("{(A,B,R) | A is set, B is set, R subset A cross B}", "({1,2},{x,y},{(1,x),(1,z)})");
        Assert.Equal("R", e.Part);
        Assert.Equal("z", e.MissingValue);
        Assert.Equal("B", e.ExpectedIn);
    }

    [Fact]
    public void CrossWithTheWrongShapeHasNoMissingValue()
    {
        SpadeParseException e = Fails(Digraph, "({a,b},{(a,b,a)})");
        Assert.Equal("E", e.Part);
        Assert.Equal("(a,b,a)", e.Element);
        Assert.Null(e.MissingValue);
        Assert.Null(e.ExpectedIn);
    }

    [Fact]
    public void SubsetOfIntHasNoMissingValue()
    {
        SpadeParseException e = Fails("{S | S subset int}", "{1,x}");
        Assert.Equal("Constraint S subset int failed: x is not a member", e.Message);
        Assert.Equal("S", e.Part);
        Assert.Equal("x", e.Element);
        Assert.Null(e.MissingValue);
        Assert.Null(e.ExpectedIn);
    }

    [Fact]
    public void SubsetOfAValueNamesTheWholeValue()
    {
        SpadeParseException e = Fails(Graph, "({1,2},edges)");
        Assert.Equal("E", e.Part);
        Assert.Equal("edges", e.Element);
        Assert.Null(e.MissingValue);
    }

    [Fact]
    public void FailedIntCheckNamesPartAndValue()
    {
        SpadeParseException e = Fails("{(N,K) | N is set, K is int}", "({1,2},x)");
        Assert.Equal("Constraint K is int failed: x is not an integer", e.Message);
        Assert.Equal("K", e.Part);
        Assert.Equal("x", e.Element);
        Assert.Null(e.MissingValue);
        Assert.Null(e.ExpectedIn);
    }

    [Fact]
    public void FailedSetCheckNamesPart()
    {
        SpadeParseException e = Fails(Graph, "((1,2),{{1,2}})");
        Assert.Equal("N", e.Part);
        Assert.Equal("(1,2)", e.Element);
    }

    [Fact]
    public void NestedFailureBelongsToTheOuterPart()
    {
        SpadeParseException e = Fails(WeightedGraph, "({1,2},{({1,2},3),({1,2},x)})");
        Assert.Equal("Constraint w is int failed: x is not an integer", e.Message);
        Assert.Equal("E", e.Part);
        Assert.Equal("({1,2},x)", e.Element);
        Assert.Null(e.MissingValue);
        Assert.Null(e.ExpectedIn);
    }

    [Fact]
    public void NestedShapeErrorBelongsToTheOuterPart()
    {
        SpadeParseException e = Fails(WeightedGraph, "({1,2},{({1,2},3,4)})");
        Assert.StartsWith("expected a list of 2 elements for (e,w), found", e.Message);
        Assert.Equal("E", e.Part);
        Assert.Equal("({1,2},3,4)", e.Element);
    }

    [Fact]
    public void NestedMissingValueKeepsATopLevelPart()
    {
        SpadeParseException e = Fails("{(N,E) | N is set, E subset {(e,w) | e subset N, w is int}}", "({1,2},{({1,9},3)})");
        Assert.Equal("Constraint e subset N failed: 9 is not a member", e.Message);
        Assert.Equal("E", e.Part);
        Assert.Equal("({1,9},3)", e.Element);
        Assert.Equal("9", e.MissingValue);
        Assert.Equal("N", e.ExpectedIn);
    }

    [Fact]
    public void NestedMissingValueDropsANestedPart()
    {
        SpadeParseException e = Fails("{(N,E) | N is set, E subset {(a,b) | a is set, b subset a}}", "({1},{({1},{2})})");
        Assert.Equal("E", e.Part);
        Assert.Equal("({1},{2})", e.Element);
        Assert.Null(e.MissingValue);
        Assert.Null(e.ExpectedIn);
    }

    [Fact]
    public void ElementIsInFullWhenTheMessageShortensIt()
    {
        string big = "{" + string.Join(",", Enumerable.Range(0, 1000)) + "}";
        SpadeParseException e = Fails("{L | L is list}", big);
        Assert.Contains("...", e.Message);
        Assert.Equal("L", e.Part);
        Assert.Equal(new UtilCollection(big).ToString(), e.Element);
        Assert.True(e.Element!.Length > 1000);
    }

    [Fact]
    public void SyntaxErrorHasNoPart()
    {
        SpadeParseException e = Fails(Graph, "({1,2},{");
        Assert.Equal("expected a value, { or ( at position 9, found end of input", e.Message);
        Assert.Equal(9, e.Position);
        Assert.Null(e.Part);
        Assert.Null(e.Element);
        Assert.Null(e.MissingValue);
        Assert.Null(e.ExpectedIn);
    }

    [Fact]
    public void ShapeErrorHasNoPart()
    {
        SpadeParseException e = Fails(Graph, "({1,2})");
        Assert.Null(e.Part);
        Assert.Null(e.Element);
    }
}
