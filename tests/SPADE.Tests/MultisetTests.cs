using SPADE;
using Xunit;

namespace SPADE.Tests;

// Multisets keep duplicates when the definition string asks for them (#1).
public class MultisetTests
{
    private static UtilCollection M(string s) => new UtilCollection(s).AsMultiset();

    // --- Reading and counting ---

    [Fact]
    public void SetsStillDropDuplicatesByDefault()
    {
        UtilCollection set = new UtilCollection("{3,3,2,2}");
        Assert.False(set.IsMultiset());
        Assert.Equal(2, set.Count());
    }

    [Fact]
    public void AsMultisetBringsBackDuplicates()
    {
        UtilCollection multiset = M("{3,3,2,2}");
        Assert.True(multiset.IsMultiset());
        Assert.True(multiset.IsUnordered());
        Assert.Equal(4, multiset.Count());
        Assert.Equal(2, multiset.CountOf(new UtilCollection("3")));
        Assert.Equal(0, multiset.CountOf(new UtilCollection("9")));
        Assert.True(multiset.Contains(new UtilCollection("2")));
    }

    [Fact]
    public void EnumeratesEveryOccurrenceInInputOrder()
    {
        UtilCollection multiset = M("{3, 2, 3, 2}");
        Assert.Equal(new[] { "3", "2", "3", "2" }, multiset.ToList().Select(x => x.ToString()));
        Assert.Equal("{3,2,3,2}", multiset.ToString());
        int seen = 0;
        foreach (UtilCollection _ in multiset) seen++;
        Assert.Equal(4, seen);
    }

    [Fact]
    public void KeepsDuplicateTuplesAndNestedCollections()
    {
        Assert.Equal(2, M("{(10,60),(10,60)}").Count());
        Assert.Equal(2, M("{{1,2},{2,1}}").Count());
    }

    [Fact]
    public void AddKeepsCounts()
    {
        UtilCollection set = new UtilCollection("{1,1}");
        set.Add(new UtilCollection("1"));
        Assert.Equal(1, set.Count());
        Assert.Equal(3, set.AsMultiset().CountOf(new UtilCollection("1")));

        UtilCollection multiset = M("{1}");
        multiset.Add(new UtilCollection("1"));
        Assert.Equal(2, multiset.CountOf(new UtilCollection("1")));
    }

    [Fact]
    public void SetBuiltInCodeBecomesMultisetOfItsElements()
    {
        UtilCollection set = new UtilCollection(new HashSet<string> { "a", "b" });
        Assert.Equal(2, set.AsMultiset().Count());
    }

    [Theory]
    [InlineData("(1,1)")]
    [InlineData("x")]
    public void OnlySetsBecomeMultisets(string input)
    {
        Assert.Throws<InvalidOperationException>(() => new UtilCollection(input).AsMultiset());
    }

    // --- Equality and hashing ---

    [Theory]
    [InlineData("{3,3,2}", "{2,3,3}")]
    [InlineData("{(1,2),(1,2),3}", "{3,(1,2),(1,2)}")]
    [InlineData("{}", "{}")]
    public void EqualWhenCountsMatchInAnyOrder(string a, string b)
    {
        Assert.Equal(M(a), M(b));
        Assert.Equal(M(a).GetHashCode(), M(b).GetHashCode());
    }

    [Theory]
    [InlineData("{3,3,2}", "{3,2,2}")]
    [InlineData("{3,3,2}", "{3,2}")]
    [InlineData("{3}", "{3,3}")]
    public void NotEqualWhenCountsDiffer(string a, string b)
    {
        Assert.NotEqual(M(a), M(b));
    }

    [Fact]
    public void SetAndMultisetAreNeverEqual()
    {
        Assert.NotEqual(new UtilCollection("{1,2}"), M("{1,2}"));
        Assert.NotEqual(M("{1,2}"), new UtilCollection("{1,2}"));
    }

    [Fact]
    public void MultisetsWorkAsHashSetKeys()
    {
        HashSet<UtilCollection> seen = new() { M("{1,1,2}") };
        Assert.Contains(M("{2,1,1}"), seen);
        Assert.DoesNotContain(M("{1,2,2}"), seen);
    }

    // --- Sub-multisets, union, intersection ---

    [Theory]
    [InlineData("{7,7}", "{7,7,1}", true)]
    [InlineData("{7,7}", "{7,1}", false)]
    [InlineData("{}", "{1}", true)]
    [InlineData("{1,2}", "{2,1,3}", true)]
    public void IsSubsetOfComparesCounts(string a, string b, bool expected)
    {
        Assert.Equal(expected, M(a).IsSubsetOf(M(b)));
    }

    [Fact]
    public void IsSubsetOfAlsoWorksForSets()
    {
        Assert.True(new UtilCollection("{1,2}").IsSubsetOf(new UtilCollection("{1,2,3}")));
        Assert.False(new UtilCollection("{1,4}").IsSubsetOf(new UtilCollection("{1,2,3}")));
        Assert.Throws<InvalidOperationException>(() => new UtilCollection("(1)").IsSubsetOf(new UtilCollection("{1}")));
    }

    [Fact]
    public void UnionTakesTheLargerCount()
    {
        Assert.Equal(M("{1,1,1,2,3}"), M("{1,1,2}").Union(M("{1,1,1,3}")));
    }

    [Fact]
    public void IntersectTakesTheSmallerCount()
    {
        Assert.Equal(M("{1,1}"), M("{1,1,2}").Intersect(M("{1,1,1,3}")));
    }

    [Fact]
    public void CannotMixSetsAndMultisets()
    {
        Assert.Throws<InvalidOperationException>(() => M("{1}").Union(new UtilCollection("{1}")));
        Assert.Throws<InvalidOperationException>(() => new UtilCollection("{1}").Intersect(M("{1}")));
    }

    // --- Definition strings ---

    private static StringParser Parse(string grammar, string instance)
    {
        StringParser parser = new StringParser(grammar);
        parser.parse(instance);
        return parser;
    }

    [Theory]
    [InlineData("{N | N is multiset, N subset int}")]
    [InlineData("{N | N subset int, N is multiset}")]
    public void IsMultisetKeepsDuplicatesWhereverItIsWritten(string grammar)
    {
        UtilCollection n = Parse(grammar, "{3, 3, 2, 2}")["N"];
        Assert.True(n.IsMultiset());
        Assert.Equal(4, n.Count());
    }

    [Fact]
    public void OtherConstraintsCheckEveryOccurrence()
    {
        SpadeParseException e = Assert.Throws<SpadeParseException>(
            () => Parse("{N | N is multiset, N subset int}", "{3,3,x}"));
        Assert.Equal("Constraint N subset int failed: x is not a member", e.Message);
    }

    [Theory]
    [InlineData("(3,3)")]
    [InlineData("3")]
    public void IsMultisetNeedsBraces(string instance)
    {
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => Parse("{N | N is multiset}", instance));
        Assert.Equal($"Constraint N is multiset failed: {instance} is not a multiset", e.Message);
    }

    [Fact]
    public void IsSetRejectsAMultiset()
    {
        Assert.Throws<SpadeParseException>(() => Parse("{N | N is multiset, N is set}", "{1,1}"));
    }

    [Fact]
    public void FieldsNotMarkedStaySets()
    {
        StringParser parser = Parse("{(S,T) | S is multiset, T is set}", "({1,1},{2,2})");
        Assert.Equal(2, parser["S"].Count());
        Assert.Equal(1, parser["T"].Count());
    }

    // The Redux grammars proposed in #1.

    [Fact]
    public void PartitionKeepsRepeatedNumbers()
    {
        UtilCollection n = Parse("{N | N is multiset, N subset int}", "{1,7,12,15,33,12,11,5,6,9,21,18}")["N"];
        Assert.Equal(12, n.Count());
        Assert.Equal(150, n.ToList().Sum(x => x.parseInt()));
    }

    [Fact]
    public void SubsetSumCertificateIsCheckedByCount()
    {
        UtilCollection s = Parse("{(S,T) | S is multiset, S subset int, T is int}", "({7,7,1},14)")["S"];
        UtilCollection k = Parse("{K | K is multiset}", "{7,7}")["K"];
        Assert.True(k.IsSubsetOf(s));
        Assert.False(Parse("{K | K is multiset}", "{7,7,7}")["K"].IsSubsetOf(s));
    }

    [Fact]
    public void KnapsackKeepsIdenticalItems()
    {
        string grammar = "{(i, w, v) | i is multiset, i subset int cross int, w is int, v is int}";
        UtilCollection items = Parse(grammar, "({(10,60),(10,60),(20,100)},50,220)")["i"];
        Assert.Equal(3, items.Count());
        Assert.Equal(2, items.CountOf(new UtilCollection("(10,60)")));
    }
}
