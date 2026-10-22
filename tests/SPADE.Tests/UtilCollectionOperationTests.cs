using SPADE;
using Xunit;

namespace SPADE.Tests;

// Set operations, shape checks and the other UtilCollection helpers callers use on parsed parts.
public class UtilCollectionOperationTests
{
    private static UtilCollection C(string s) => new UtilCollection(s);

    [Fact]
    public void SetOfStringSetsBuildsNestedSets()
    {
        UtilCollection built = new UtilCollection(new HashSet<HashSet<string>>
        {
            new HashSet<string> { "1", "2" },
            new HashSet<string> { "3" },
        });

        Assert.True(built.IsUnordered());
        Assert.Equal(C("{{2,1},{3}}"), built);
        Assert.True(built.Contains(C("{1,2}")));
    }

    [Fact]
    public void UnionOfSetsHasEveryElementOnce()
    {
        Assert.Equal(C("{1,2,3}"), C("{1,2}").Union(C("{2,3}")));
    }

    [Fact]
    public void UnionOfMultisetsKeepsTheLargerCount()
    {
        UtilCollection union = C("{1,1,2}").AsMultiset().Union(C("{1,2,2,3}").AsMultiset());

        Assert.True(union.IsMultiset());
        Assert.Equal(C("{1,1,2,2,3}").AsMultiset(), union);
        Assert.Equal(2, union.CountOf(C("1")));
        Assert.Equal(2, union.CountOf(C("2")));
        Assert.Equal(1, union.CountOf(C("3")));
    }

    [Fact]
    public void UnionAndIntersectionRejectMixedKinds()
    {
        InvalidOperationException listUnion = Assert.Throws<InvalidOperationException>(() => C("{1}").Union(C("(1)")));
        Assert.Equal("Can't take the union of a set and a list", listUnion.Message);

        InvalidOperationException multisetUnion = Assert.Throws<InvalidOperationException>(() => C("{1}").Union(C("{1}").AsMultiset()));
        Assert.Equal("Can't take the union of a set and a multiset", multisetUnion.Message);

        InvalidOperationException listIntersect = Assert.Throws<InvalidOperationException>(() => C("(1)").Intersect(C("{1}")));
        Assert.Equal("Can't take the intersection of a set and a list", listIntersect.Message);

        InvalidOperationException multisetIntersect = Assert.Throws<InvalidOperationException>(() => C("{1}").AsMultiset().Intersect(C("{1}")));
        Assert.Equal("Can't take the intersection of a set and a multiset", multisetIntersect.Message);
    }

    // Lists have no union or intersection yet; saying so beats guessing at an order.
    [Fact]
    public void UnionAndIntersectionOfListsAreNotImplemented()
    {
        Assert.Throws<NotImplementedException>(() => C("(1,2)").Union(C("(2,3)")));
        Assert.Throws<NotImplementedException>(() => C("(1,2)").Intersect(C("(2,3)")));
    }

    [Fact]
    public void CrossPairsEveryElementOfTheLeftWithEveryElementOfTheRight()
    {
        UtilCollection cross = C("{1,2}").Cross(C("{a,b}"));

        Assert.True(cross.IsUnordered());
        Assert.Equal(C("{(1,a),(1,b),(2,a),(2,b)}"), cross);
    }

    [Fact]
    public void CrossOfListsIsASetOfPairs()
    {
        UtilCollection cross = C("(1,1)").Cross(C("(x)"));

        // (1,x) twice collapses to one pair, since the result is a set.
        Assert.Equal(C("{(1,x)}"), cross);
        Assert.Equal(C("(1,x)"), cross.ToList().Single());
    }

    [Fact]
    public void CrossWithAnEmptySetIsEmpty()
    {
        Assert.Equal(0, C("{1,2}").Cross(C("{}")).Count());
    }

    [Fact]
    public void AssertPairAcceptsOnlyListsOfTheGivenLength()
    {
        Assert.Null(Record.Exception(() => C("(1,2)").assertPair()));
        Assert.Null(Record.Exception(() => C("(1,2,3)").assertPair(3)));

        Assert.Equal("Not a list of correct length", Assert.Throws<InvalidOperationException>(() => C("(1,2,3)").assertPair()).Message);
        Assert.Equal("Not a list of correct length", Assert.Throws<InvalidOperationException>(() => C("{1,2}").assertPair()).Message);
        Assert.Equal("Not a list of correct length", Assert.Throws<InvalidOperationException>(() => C("(1,2)").assertPair(3)).Message);
    }

    [Fact]
    public void AssertCountChecksTheNumberOfElements()
    {
        Assert.Null(Record.Exception(() => C("{1,2,3}").assertCount(3)));
        Assert.Null(Record.Exception(() => C("(1,1)").assertCount(2)));

        // A set drops its duplicate, so {1,1} has one element.
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => C("{1,1}").assertCount(2));
        Assert.Equal("collection has incorrect length", e.Message);
    }

    [Fact]
    public void AssertOrderedAndUnorderedTellListsFromSets()
    {
        Assert.Null(Record.Exception(() => C("(1,2)").assertOrdered()));
        Assert.Null(Record.Exception(() => C("{1,2}").assertUnordered()));

        Assert.Equal("Not ordered", Assert.Throws<InvalidOperationException>(() => C("{1,2}").assertOrdered()).Message);
        Assert.Equal("Ordered", Assert.Throws<InvalidOperationException>(() => C("(1,2)").assertUnordered()).Message);
    }

    [Theory]
    [InlineData("{1,2}", "{2,1}", false)]
    [InlineData("(1,2)", "(1,2)", false)]
    [InlineData("(1,2)", "(2,1)", true)]
    [InlineData("{1,2}", "(1,2)", true)]
    [InlineData("{1,2}", "{1}", true)]
    [InlineData("a", "a", false)]
    [InlineData("a", "b", true)]
    public void NotEqualsIsTheOppositeOfEquals(string a, string b, bool notEqual)
    {
        Assert.Equal(notEqual, C(a).NotEquals(C(b)));
        Assert.Equal(!notEqual, C(a).Equals(C(b)));
    }

    [Fact]
    public void NotEqualToNullOrAnotherType()
    {
        Assert.True(C("{1}").NotEquals(null));
        Assert.True(C("1").NotEquals("1"));
    }
}
