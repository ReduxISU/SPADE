using SPADE;
using Xunit;

namespace SPADE.Tests;

// Undirected edges must be sets of nodes, such as {a,b} (#12).
public class UnorderedCrossTests
{
    private const string Graph = "{(N,E) | N is set, E subset N unorderedcross N}";
    private const string GraphWithK = "{((N,E),K) | N is set, E subset N unorderedcross N, K is int}";

    private static StringParser Parse(string grammar, string instance)
    {
        StringParser parser = new StringParser(grammar);
        parser.parse(instance);
        return parser;
    }

    [Theory]
    [InlineData("({a,b,c},{{a,b},{b,c}})")]
    [InlineData("({1,2,3},{{2,1},{3,1}})")]
    [InlineData("({a,b},{})")]
    public void AcceptsSetEdges(string instance)
    {
        StringParser parser = Parse(Graph, instance);
        Assert.Equal(new UtilCollection(instance)[1], parser["E"]);
    }

    [Fact]
    public void AcceptsGraphWithK()
    {
        StringParser parser = Parse(GraphWithK, "(({1,2,3},{{1,2},{2,3}}),2)");
        Assert.Equal("2", parser["K"].ToString());
    }

    // Released versions accept list edges, and some grammars for directed
    // graphs rely on it, so this keeps that behavior until they move to cross (#12).
    [Theory]
    [InlineData("({a,b,c},{(a,b)})")]
    [InlineData("({a,b,c},{{a,b},(b,c)})")]
    public void StillAcceptsListEdges(string instance)
    {
        StringParser parser = Parse(Graph, instance);
        Assert.Equal(new UtilCollection(instance)[1], parser["E"]);
    }

    [Fact]
    public void RejectsListEdgesWithUnknownNodes()
    {
        Assert.ThrowsAny<Exception>(() => Parse(Graph, "({a,b,c},{(a,d)})"));
    }

    [Theory]
    [InlineData("({a,b,c},{{a,d}})")]
    [InlineData("({a,b,c},{{x,y}})")]
    public void RejectsEdgesWithUnknownNodes(string instance)
    {
        Assert.ThrowsAny<Exception>(() => Parse(Graph, instance));
    }

    [Fact]
    public void RejectsPlainValueEdgeAsConstraintFailure()
    {
        // A plain value is not an edge. It should fail the constraint, not
        // crash while trying to enumerate the value.
        Exception e = Assert.ThrowsAny<Exception>(() => Parse(Graph, "({a,b},{a})"));
        Assert.IsNotType<InvalidOperationException>(e);
    }
}
