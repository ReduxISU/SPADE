using SPADE;
using Xunit;

namespace SPADE.Tests;

// Equal collections must hash the same, or sets of sets lose lookups (#9).
public class UtilCollectionEqualityTests
{
    private static UtilCollection C(string s) => new UtilCollection(s);

    [Theory]
    [InlineData("{1,2}", "{2,1}")]
    [InlineData("{1,2,3}", "{3,1,2}")]
    [InlineData("{{1,2},{3}}", "{{3},{2,1}}")]
    [InlineData("{(1,2),(3,4)}", "{(3,4),(1,2)}")]
    [InlineData("({1,2},4)", "({2,1},4)")]
    [InlineData("{}", "{}")]
    public void EqualCollectionsHaveEqualHashCodes(string a, string b)
    {
        Assert.Equal(C(a), C(b));
        Assert.Equal(C(a).GetHashCode(), C(b).GetHashCode());
    }

    [Fact]
    public void SetOfSetsContainsReorderedMember()
    {
        Assert.True(C("{{1,2}}").Contains(C("{2,1}")));
    }

    [Fact]
    public void SetOfSetsDropsReorderedDuplicate()
    {
        Assert.Equal(1, C("{{1,2},{2,1}}").Count());
    }

    [Fact]
    public void IntersectMatchesReorderedSets()
    {
        Assert.Equal(1, C("{{1,2}}").Intersect(C("{{2,1}}")).Count());
    }

    [Fact]
    public void UnionMergesReorderedSets()
    {
        Assert.Equal(1, C("{{1,2}}").Union(C("{{2,1}}")).Count());
    }

    [Theory]
    [InlineData("(1,2)", "(2,1)")]
    [InlineData("{1,2}", "(1,2)")]
    [InlineData("{}", "()")]
    [InlineData("{}", "x")]
    [InlineData("()", "x")]
    [InlineData("{1}", "1")]
    [InlineData("{1,2}", "{1,2,3}")]
    public void DifferentCollectionsAreNotEqual(string a, string b)
    {
        Assert.NotEqual(C(a), C(b));
        Assert.NotEqual(C(b), C(a));
    }
}
