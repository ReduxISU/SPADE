using System.Diagnostics;
using SPADE;
using Xunit;

namespace SPADE.Tests;

// Reading instance strings: whitespace (#2), clear errors (#4), and bounded time and depth (#7).
public class InstanceScannerTests
{
    private static UtilCollection C(string s) => new UtilCollection(s);

    // --- Whitespace (#2) ---

    [Theory]
    [InlineData("{2, 3}", "{2,3}")]
    [InlineData("{1,2}\n", "{1,2}")]
    [InlineData(" \t{ 1 ,\r\n 2 }\r\n", "{1,2}")]
    [InlineData("( {1, 2} , 4 )", "({1,2},4)")]
    [InlineData("{\n  (a, b),\n  (c, d)\n}", "{(a,b),(c,d)}")]
    [InlineData("  3  ", "3")]
    public void SkipsWhitespaceAroundStructure(string input, string expected)
    {
        Assert.Equal(C(expected), C(input));
        Assert.Equal(expected, C(input).ToString());
    }

    [Fact]
    public void SpacedSetContainsItsValues()
    {
        Assert.True(C("{2, 3}").Contains(C("3")));
    }

    [Theory]
    [InlineData("{New York, Los Angeles}", "New York")]
    [InlineData("{x1 | !x2, x3}", "x1 | !x2")]
    [InlineData("{a  b}", "a  b")]
    public void KeepsSpacesInsideValues(string input, string value)
    {
        Assert.True(C(input).Contains(C(value)));
        Assert.Equal(value, C(input).ToList().First(v => v.ToString().Contains(' ')).ToString());
    }

    [Theory]
    [InlineData("{New\nYork}", "New York")]
    [InlineData("{New \r\n York}", "New York")]
    [InlineData("{a\tb}", "a b")]
    public void LineBreaksAndTabsInsideValuesBecomeOneSpace(string input, string value)
    {
        Assert.Equal("{" + value + "}", C(input).ToString());
    }

    [Theory]
    [InlineData("{x1 : True}", "{x1:True}")]
    [InlineData("{a = 1, b= 2}", "{a=1,b=2}")]
    [InlineData("(1 ; 2 ; 3)", "(1;2;3)")]
    [InlineData("{[ 1 ]}", "{[1]}")]
    public void DropsWhitespaceBesideDelimitersInsideValues(string input, string expected)
    {
        Assert.Equal(expected, C(input).ToString());
    }

    // --- Errors (#4) ---

    [Theory]
    [InlineData("", "expected a value, { or ( at position 1, found end of input")]
    [InlineData("   ", "expected a value, { or ( at position 4, found end of input")]
    [InlineData("{1,2", "expected , or } at position 5, found end of input")]
    [InlineData("{1,2}junk", "expected end of input at position 6, found j")]
    [InlineData("{1,(2))}", "expected , or } at position 7, found )")]
    [InlineData("{(1,2},3)", "expected , or ) at position 6, found }")]
    [InlineData("}", "expected a value, { or ( at position 1, found }")]
    [InlineData("{1,,2}", "expected a value, { or ( at position 4, found ,")]
    [InlineData("{1,}", "expected a value, { or ( at position 4, found }")]
    [InlineData("{,1}", "expected a value, { or ( at position 2, found ,")]
    [InlineData("f(x)", "expected end of input at position 2, found (")]
    [InlineData("{1, 2} x", "expected end of input at position 8, found x")]
    public void MalformedInputThrowsWithPosition(string input, string message)
    {
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => C(input));
        Assert.Equal(message, e.Message);
    }

    [Fact]
    public void ErrorExposesPositionExpectedAndFound()
    {
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => C("{1,2}junk"));
        Assert.Equal(6, e.Position);
        Assert.Equal("end of input", e.Expected);
        Assert.Equal("j", e.Found);
    }

    [Fact]
    public void ParseErrorIsAFormatException()
    {
        Assert.IsAssignableFrom<FormatException>(Assert.Throws<SpadeParseException>(() => C("{")));
    }

    // --- Depth and time (#7) ---

    // InstanceScanner.MaxDepth, which is internal.
    private const int MaxDepth = 256;

    private static string Nested(int depth) => new string('{', depth) + new string('}', depth);

    [Fact]
    public void AcceptsNestingUpToTheLimit()
    {
        C(Nested(MaxDepth));
    }

    [Theory]
    [InlineData(MaxDepth + 1)]
    [InlineData(100_000)]
    public void RejectsDeeperNestingWithoutCrashing(int depth)
    {
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => C(Nested(depth)));
        Assert.Equal(MaxDepth + 1, e.Position);
    }

    [Fact]
    public void LargeFlatSetParsesQuickly()
    {
        // 40,000 elements took 104 seconds before; this is 100,000.
        string input = "{" + string.Join(", ", Enumerable.Range(0, 100_000)) + "}";
        Stopwatch timer = Stopwatch.StartNew();
        UtilCollection set = C(input);
        string text = set.ToString();
        timer.Stop();

        Assert.Equal(100_000, set.Count());
        Assert.Equal(input.Replace(" ", "").Length, text.Length);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), $"took {timer.Elapsed}");
    }

    [Fact]
    public void DeepNestingParsesQuickly()
    {
        // Depth 4,000 took 103 seconds before; the limit is lower, so check many limit-depth parses.
        string input = Nested(MaxDepth);
        Stopwatch timer = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) C(input);
        timer.Stop();

        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), $"took {timer.Elapsed}");
    }
}
