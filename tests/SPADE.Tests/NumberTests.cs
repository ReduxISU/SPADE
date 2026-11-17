using System.Globalization;
using System.Text;
using SPADE;
using Xunit;

namespace SPADE.Tests;

// "number" is a decimal type next to "int": a sign, digits and an optional fraction, read with the
// invariant culture. Every int is a number; exponents, NaN and infinities are not (#23).
public class NumberTests
{
    private static void Parse(string grammar, string instance)
    {
        new StringParser(grammar).parse(instance);
    }

    private static SpadeParseException Fails(string grammar, string instance)
    {
        return Assert.Throws<SpadeParseException>(() => new StringParser(grammar).parse(instance));
    }

    // 400 digits: a valid shape, but too big for a double.
    private static readonly string Huge = new('9', 400);

    [Theory]
    [InlineData("5")]
    [InlineData("-3")]
    [InlineData("+7")]
    [InlineData("2.5")]
    [InlineData("-0.75")]
    [InlineData(".5")]
    [InlineData("-.5")]
    [InlineData("+.5")]
    [InlineData("0")]
    [InlineData("0.0")]
    [InlineData("007.50")]
    [InlineData("\"2.5\"")]
    [InlineData("  2.5  ")]
    public void IsNumberAcceptsDecimals(string instance)
    {
        Parse("{B | B is number}", instance);
    }

    [Theory]
    [InlineData("1e3", "1e3")]
    [InlineData("2E-4", "2E-4")]
    [InlineData("NaN", "NaN")]
    [InlineData("Infinity", "Infinity")]
    [InlineData("-Infinity", "-Infinity")]
    [InlineData("∞", "∞")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("5.", "5.")]
    [InlineData(".", ".")]
    [InlineData("-", "-")]
    [InlineData("+", "+")]
    [InlineData("--5", "--5")]
    [InlineData("x", "x")]
    [InlineData("0x1A", "0x1A")]
    [InlineData("1 000", "1 000")]
    [InlineData("\"1,000\"", "1,000")]
    [InlineData("\"2,5\"", "2,5")]
    [InlineData("\" 2.5\"", " 2.5")]
    [InlineData("\"2.5 \"", "2.5 ")]
    [InlineData("\"\"", "")]
    [InlineData("{1,2}", "{1,2}")]
    [InlineData("(1,2)", "(1,2)")]
    [InlineData("{}", "{}")]
    public void IsNumberRejectsNonNumbers(string instance, string described)
    {
        SpadeParseException e = Fails("{B | B is number}", instance);
        Assert.Equal($"Constraint B is number failed: {described} is not a number", e.Message);
    }

    [Fact]
    public void IsNumberRejectsAValueTooBigForADouble()
    {
        SpadeParseException e = Fails("{B | B is number}", Huge);
        Assert.EndsWith(" is not a number", e.Message);
        Assert.Equal("B", e.Part);
        Assert.Equal(Huge, e.Element);
    }

    [Fact]
    public void FailedNumberCheckNamesPartAndValue()
    {
        SpadeParseException e = Fails("{(N,K) | N is set, K is number}", "({1,2},x)");
        Assert.Equal("Constraint K is number failed: x is not a number", e.Message);
        Assert.Equal("K", e.Part);
        Assert.Equal("x", e.Element);
        Assert.Null(e.MissingValue);
        Assert.Null(e.ExpectedIn);
    }

    [Fact]
    public void QuotedNumberIsANumber()
    {
        StringParser parser = new("{(K,S) | K is number, S subset number}");
        parser.parse("(\"2.5\", {\"-0.75\", 3})");
        Assert.Equal("2.5", parser["K"].ToString());
        Assert.Equal(2, parser["S"].Count());
    }

    [Theory]
    [InlineData("3")]
    [InlineData("-12")]
    [InlineData("+4")]
    [InlineData("0")]
    [InlineData("\"5\"")]
    [InlineData("2147483647")]
    [InlineData("-2147483648")]
    public void EveryIntIsANumber(string instance)
    {
        Parse("{N | N is int}", instance);
        Parse("{N | N is number}", instance);
        Parse("{S | S subset number}", "{" + instance + "}");
    }

    [Fact]
    public void IntStillRejectsDecimals()
    {
        SpadeParseException e = Fails("{N | N is int}", "2.5");
        Assert.Equal("Constraint N is int failed: 2.5 is not an integer", e.Message);
    }

    [Fact]
    public void SubsetNumberAcceptsDecimals()
    {
        StringParser parser = new("{S | S subset number}");
        parser.parse("{1, 2.5, -.5, +7}");
        Assert.Equal(4, parser["S"].Count());
    }

    [Theory]
    [InlineData("{1, 2.5, x}", "x")]
    [InlineData("{1, 1e3}", "1e3")]
    [InlineData("{1, {2}}", "{2}")]
    public void SubsetNumberRejectsANonNumber(string instance, string bad)
    {
        SpadeParseException e = Fails("{S | S subset number}", instance);
        Assert.Equal($"Constraint S subset number failed: {bad} is not a member", e.Message);
        Assert.Equal("S", e.Part);
        Assert.Equal(bad, e.Element);
        Assert.Null(e.MissingValue);
        Assert.Null(e.ExpectedIn);
    }

    // Convex Hull: a set of points with real coordinates, written either way.
    [Theory]
    [InlineData("{P | P subset number cross number}")]
    [InlineData("{P | P subset {(x,y) | x is number, y is number}}")]
    public void ConvexHullPointsAreNumberPairs(string grammar)
    {
        StringParser parser = new(grammar);
        parser.parse("{(0,0), (2.5,-1), (-.5,3.25), (+4,\"1.5\")}");
        Assert.Equal(4, parser["P"].Count());
    }

    [Fact]
    public void NumberCrossNumberRejectsANonNumberCoordinate()
    {
        SpadeParseException e = Fails("{P | P subset number cross number}", "{(0,0), (2.5,north)}");
        Assert.Equal("Constraint P subset number cross number failed: (2.5,north) is not a member", e.Message);
        Assert.Equal("P", e.Part);
        Assert.Equal("(2.5,north)", e.Element);
        Assert.Null(e.MissingValue);
        Assert.Null(e.ExpectedIn);
    }

    [Fact]
    public void NestedNumberCheckBelongsToTheOuterPart()
    {
        SpadeParseException e = Fails("{P | P subset {(x,y) | x is number, y is number}}", "{(0,0), (2.5,1e3)}");
        Assert.Equal("P", e.Part);
        Assert.Contains("1e3 is not a number", e.Message);
    }

    [Fact]
    public void NumberCrossNumberRejectsAValue()
    {
        SpadeParseException e = Fails("{P | P subset number cross number}", "{(0,0), 5}");
        Assert.Equal("Constraint P subset number cross number failed: 5 is not a member", e.Message);
        Assert.Null(e.MissingValue);
    }

    [Fact]
    public void NumbersDoNotDependOnTheMachineCulture()
    {
        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            // German writes 2,5 for two and a half.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Parse("{B | B is number}", "2.5");
            Parse("{S | S subset number}", "{2.5, -.75}");
            SpadeParseException e = Fails("{B | B is number}", "\"2,5\"");
            Assert.Equal("Constraint B is number failed: 2,5 is not a number", e.Message);
            Fails("{S | S subset number}", "{\"2,5\"}");
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void DefinitionReportsNumber()
    {
        DefinitionDescription d = new StringParser("{((A,B),C) | A is int, B is number, C subset number cross number}").Definition;
        Assert.Equal(PartKind.Int, d["A"].Kind);
        Assert.Equal(PartKind.Number, d["B"].Kind);
        Assert.Equal(PartKind.Subset, d["C"].Kind);
        Assert.Equal("number cross number", d["C"].Of);
    }
}
