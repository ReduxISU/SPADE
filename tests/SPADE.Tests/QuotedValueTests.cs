using SPADE;
using Xunit;

namespace SPADE.Tests;

// A value in double quotes may hold , { } ( ) and any whitespace, exactly as written (#22).
public class QuotedValueTests
{
    [Theory]
    [InlineData("\"hello, world\"", "hello, world")]
    [InlineData("\"{a} (b)\"", "{a} (b)")]
    [InlineData("\"  two  spaces \t tab  \"", "  two  spaces \t tab  ")]
    [InlineData("\"x : y\"", "x : y")]
    [InlineData("\"\"", "")]
    [InlineData("  \"padded\"  ", "padded")]
    [InlineData("\"say \\\"hi\\\"\"", "say \"hi\"")]
    [InlineData("\"C:\\\\temp\"", "C:\\temp")]
    public void QuotedValueKeepsItsTextExactly(string instance, string value)
    {
        UtilCollection c = new(instance);
        Assert.True(c.IsValue());
        Assert.Equal(value, c.ToString());
    }

    [Fact]
    public void QuotedValueInASetIsOneElement()
    {
        UtilCollection c = new("{\"a,b\", c}");
        Assert.Equal(2, c.Count());
        Assert.True(c.Contains(new UtilCollection("\"a,b\"")));
        Assert.True(c.Contains(new UtilCollection("c")));
    }

    [Fact]
    public void QuotingAPlainValueChangesNothing()
    {
        Assert.Equal(new UtilCollection("{a,b}"), new UtilCollection("{\"a\", \"b\"}"));
        Assert.Equal(new UtilCollection("{a,b}").GetHashCode(), new UtilCollection("{\"b\",a}").GetHashCode());
        Assert.Equal("{a,b}", new UtilCollection("{\"a\",\"b\"}").ToString());
    }

    [Fact]
    public void SetsWithQuotedValuesCompareByValue()
    {
        UtilCollection x = new("{\"a,b\",\"c d\"}");
        UtilCollection y = new("{\"c d\", \"a,b\"}");
        Assert.Equal(x, y);
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
        Assert.NotEqual(x, new UtilCollection("{a,b,c d}"));
    }

    [Theory]
    [InlineData("{\"a,b\",c}", "{\"a,b\",c}")]
    // A backslash means nothing outside quotes, so C:\temp needs none.
    [InlineData("(\"say \\\"hi\\\"\", \"C:\\\\temp\")", "(\"say \\\"hi\\\"\",C:\\temp)")]
    [InlineData("(\"a\\\\\\\"b, c\")", "(\"a\\\\\\\"b, c\")")]
    [InlineData("(\" lead\",\"trail \",\"a\tb\",\"x : y\",\"\")", "(\" lead\",\"trail \",\"a\tb\",\"x : y\",\"\")")]
    [InlineData("({\"{1}\"},(\"(2)\"))", "({\"{1}\"},(\"(2)\"))")]
    public void ToStringQuotesOnlyWhatNeedsItAndReadsBack(string instance, string written)
    {
        UtilCollection c = new(instance);
        Assert.Equal(written, c.ToString());
        Assert.Equal(c, new UtilCollection(c.ToString()));
    }

    [Theory]
    [InlineData("({New York, x1:True, a[1]}, (1,2))")]
    [InlineData("{C:\\temp, 5, -3.5}")]
    public void ValuesThatDontNeedQuotesPrintAsBefore(string instance)
    {
        UtilCollection c = new(instance);
        Assert.DoesNotContain("\"", c.ToString());
        Assert.Equal(c, new UtilCollection(c.ToString()));
    }

    [Theory]
    [InlineData("\"abc", 5, "closing \"", "end of input")]
    [InlineData("{\"a,b}", 7, "closing \"", "end of input")]
    [InlineData("\"a\\nb\"", 4, "\\\" or \\\\ after \\", "n")]
    [InlineData("\"a\\", 4, "\\\" or \\\\ after \\", "end of input")]
    public void BadQuotesAreSyntaxErrors(string instance, int position, string expected, string found)
    {
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => new UtilCollection(instance));
        Assert.Equal(position, e.Position);
        Assert.Equal(expected, e.Expected);
        Assert.Equal(found, e.Found);
    }

    [Theory]
    [InlineData("{ab\"c\"}", 4, ", or }")]
    [InlineData("{\"a\"b}", 5, ", or }")]
    [InlineData("ab\"c\"", 3, "end of input")]
    public void AQuoteCantBePartOfAnUnquotedValue(string instance, int position, string expected)
    {
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => new UtilCollection(instance));
        Assert.Equal(position, e.Position);
        Assert.Equal(expected, e.Expected);
    }

    [Fact]
    public void QuotedValuesPassIsString()
    {
        StringParser parser = new("{(x,y) | x is string, y is string}");
        parser.parse("(\"hello, world\", \"{not a set}\")");
        Assert.Equal("hello, world", parser["x"].ToString());
        Assert.Equal("{not a set}", parser["y"].ToString());
    }

    [Fact]
    public void QuotedNumberIsStillAnInt()
    {
        StringParser parser = new("{(K,S) | K is int, S subset int}");
        parser.parse("(\"5\", {\"1\", 2})");
        Assert.Equal(5, parser["K"].parseInt());
        Assert.Equal(2, parser["S"].Count());
    }

    [Fact]
    public void QuotedValuesAreMembersByValue()
    {
        StringParser parser = new("{(N,E) | N is set, E subset N cross N}");
        parser.parse("({\"New York, NY\", Boston}, {(\"New York, NY\", Boston)})");
        Assert.Equal(2, parser["N"].Count());
    }
}
