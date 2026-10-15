using SPADE;
using Xunit;

namespace SPADE.Tests;

// A collection freezes once it is hashed, so it can't end up in a set under a stale hash.
public class UtilCollectionFreezeTests
{
    private static UtilCollection C(string s) => new UtilCollection(s);

    [Fact]
    public void AddThrowsOnceInAHashSet()
    {
        UtilCollection item = C("{1,2}");
        HashSet<UtilCollection> outer = new() { item };

        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => item.Add(C("3")));
        Assert.Contains("has been hashed", e.Message);
        Assert.Equal(2, item.Count());
        Assert.Contains(C("{2,1}"), outer);
    }

    [Fact]
    public void AddThrowsOnceUsedAsADictionaryKey()
    {
        UtilCollection key = C("(1,2)");
        Dictionary<UtilCollection, int> counts = new() { [key] = 1 };

        Assert.Throws<InvalidOperationException>(() => key.Add(C("3")));
        Assert.Equal(1, counts[C("(1,2)")]);
    }

    [Fact]
    public void AddingToASetFreezesTheElement()
    {
        UtilCollection outer = C("{}");
        UtilCollection item = C("(1,2)");
        outer.Add(item);

        Assert.Throws<InvalidOperationException>(() => item.Add(C("3")));
        Assert.True(outer.Contains(C("(1,2)")));
    }

    [Fact]
    public void ChildOfAHashedListIsFrozen()
    {
        UtilCollection child = C("{1}");
        UtilCollection pair = new UtilCollection(new List<UtilCollection> { child, C("x") });
        _ = new HashSet<UtilCollection> { pair };

        Assert.Throws<InvalidOperationException>(() => child.Add(C("2")));
    }

    [Fact]
    public void GrandchildOfAHashedSetIsFrozen()
    {
        UtilCollection outer = C("{({1},a)}");
        _ = outer.GetHashCode();
        UtilCollection pair = outer.ToList()[0];
        UtilCollection grandchild = pair[0];

        Assert.Throws<InvalidOperationException>(() => grandchild.Add(C("2")));
    }

    [Fact]
    public void IndexerSetterThrowsOnAHashedList()
    {
        UtilCollection list = C("(1,2)");
        _ = new HashSet<UtilCollection> { list };

        Assert.Throws<InvalidOperationException>(() => list[0] = C("3"));
        Assert.Equal("(1,2)", list.ToString());
    }

    [Fact]
    public void HashIsTheSameOnEveryCall()
    {
        UtilCollection item = C("{(1,2),{3}}");
        Assert.Equal(item.GetHashCode(), item.GetHashCode());
        Assert.Equal(C("{{3},(1,2)}").GetHashCode(), item.GetHashCode());
    }

    [Fact]
    public void ListConstructorCopiesItsArgument()
    {
        List<UtilCollection> source = new() { C("1"), C("2") };
        UtilCollection list = new UtilCollection(source);
        source.Add(C("3"));
        source[0] = C("9");

        Assert.Equal("(1,2)", list.ToString());
    }

    [Fact]
    public void SetConstructorCopiesItsArgument()
    {
        HashSet<UtilCollection> source = new() { C("1"), C("2") };
        UtilCollection set = new UtilCollection(source);
        source.Add(C("3"));
        source.Remove(C("1"));

        Assert.Equal(C("{1,2}"), set);
    }

    [Fact]
    public void ToListOfAListIsACopy()
    {
        UtilCollection list = C("(1,2)");
        _ = list.GetHashCode();
        list.ToList().Add(C("3"));

        Assert.Equal(2, list.Count());
    }

    [Fact]
    public void BuildingFullyThenInsertingWorks()
    {
        UtilCollection edges = C("{}");
        foreach ((string a, string b) in new[] { ("1", "2"), ("2", "3"), ("2", "1") })
        {
            UtilCollection edge = C("{}");
            edge.Add(C(a));
            edge.Add(C(b));
            edges.Add(edge);
        }

        // {1,2} and {2,1} are the same edge.
        Assert.Equal(2, edges.Count());
        Assert.True(edges.Contains(C("{2,1}")));
        Assert.True(edges.Contains(C("{3,2}")));
        Assert.False(edges.Contains(C("{1,3}")));
    }

    [Fact]
    public void UnhashedCollectionsCanStillChange()
    {
        UtilCollection set = C("{1}");
        set.Add(C("2"));
        UtilCollection list = C("(1,2)");
        list.Add(C("3"));
        list[0] = C("0");

        Assert.Equal(C("{1,2}"), set);
        Assert.Equal("(0,2,3)", list.ToString());
    }

    [Fact]
    public void EqualsDoesNotFreeze()
    {
        UtilCollection set = C("{1}");
        Assert.Equal(C("{1}"), set);
        set.Add(C("2"));

        Assert.Equal(2, set.Count());
    }
}
