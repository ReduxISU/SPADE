using System.Globalization;
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
        Assert.Throws<SpadeParseException>(() => Parse("{N | N is int}", instance));
    }

    [Fact]
    public void IsIntChecksNestedFields()
    {
        string grammar = "{((N,E),K) | N is set, E subset N unorderedcross N, K is int}";
        Parse(grammar, "(({1,2},{{1,2}}),2)");
        Assert.Throws<SpadeParseException>(() => Parse(grammar, "(({1,2},{{1,2}}),two)"));
    }

    [Fact]
    public void IsIntChecksInsideSetComprehension()
    {
        string grammar = "{(N,E) | N is set, E subset {(e, w) | e is N unorderedcross N, w is int}}";
        Parse(grammar, "({1,2,3},{({1,2},4),({2,3},5)})");
        Assert.Throws<SpadeParseException>(() => Parse(grammar, "({1,2,3},{({1,2},heavy)})"));
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
        Assert.Throws<SpadeParseException>(() => Parse("{S | S is set}", instance));
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
        Assert.Throws<SpadeParseException>(() => Parse("{L | L is list}", instance));
    }

    [Fact]
    public void SubsetIntStillChecksEveryElement()
    {
        Parse("{S | S subset int}", "{1,2,3}");
        Assert.Throws<SpadeParseException>(() => Parse("{S | S subset int}", "{1,2,x}"));
    }

    // "is string" accepts any single value and rejects a collection (#22).
    [Theory]
    [InlineData("kitten", "kitten")]
    [InlineData("New York", "New York")]
    [InlineData("  New York  ", "New York")]
    [InlineData("5", "5")]
    [InlineData("q0", "q0")]
    public void IsStringAcceptsSingleValues(string instance, string value)
    {
        StringParser parser = new("{S | S is string}");
        parser.parse(instance);
        Assert.True(parser["S"].IsValue());
        Assert.Equal(value, parser["S"].ToString());
    }

    [Theory]
    [InlineData("{S | S is string}", "{a,b}", "{a,b}")]
    [InlineData("{S | S is string}", "{}", "{}")]
    [InlineData("{S | S is string}", "(a,b)", "(a,b)")]
    [InlineData("{S | S is string}", "()", "()")]
    [InlineData("{S | S is multiset, S is string}", "{a,a}", "{a,a}")]
    public void IsStringRejectsCollections(string grammar, string instance, string element)
    {
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => Parse(grammar, instance));
        Assert.Equal($"Constraint S is string failed: {element} is not a string", e.Message);
        Assert.Equal("S", e.Part);
        Assert.Equal(element, e.Element);
        Assert.Null(e.MissingValue);
    }

    [Fact]
    public void IsStringChecksNestedFields()
    {
        string grammar = "{(x,y) | x is string, y is string}";
        Parse(grammar, "(kitten,sitting)");
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => Parse(grammar, "(kitten,{sitting})"));
        Assert.Equal("y", e.Part);
    }

    [Fact]
    public void IsStringInADfaDefinition()
    {
        string grammar = "{((N,A,E,S,F),I) | N is set, A is set, E is N cross A cross N, S is string, F is set, I is string}";
        StringParser parser = new(grammar);
        parser.parse("(({q0,q1},{0,1},{(q0,0,q1),(q1,1,q0)},q0,{q1}),0110)");
        Assert.Equal("q0", parser["S"].ToString());
        Assert.Equal("0110", parser["I"].ToString());
        Assert.Equal(PartKind.String, parser.Definition["S"].Kind);
        Assert.Equal(PartKind.String, parser.Definition["I"].Kind);

        SpadeParseException e = Assert.Throws<SpadeParseException>(() => Parse(grammar, "(({q0,q1},{0,1},{(q0,0,q1)},{q0},{q1}),0110)"));
        Assert.Equal("Constraint S is string failed: {q0} is not a string", e.Message);
        Assert.Equal("S", e.Part);
    }

    [Fact]
    public void IntsDoNotDependOnTheMachineCulture()
    {
        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            // A culture whose minus sign is not '-' (some cultures use U+2212): "-5" must still be an int.
            CultureInfo culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NegativeSign = "~";
            CultureInfo.CurrentCulture = culture;

            Parse("{N | N is int}", "-5");
            Parse("{S | S subset int}", "{-5, 3}");
            StringParser parser = new("{N | N is int}");
            parser.parse("-5");
            Assert.Equal(-5, parser["N"].parseInt());

            SpadeParseException e = Assert.Throws<SpadeParseException>(() => Parse("{N | N is int}", "~5"));
            Assert.Equal("Constraint N is int failed: ~5 is not an integer", e.Message);
            Assert.Throws<SpadeParseException>(() => Parse("{S | S subset int}", "{~5}"));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
