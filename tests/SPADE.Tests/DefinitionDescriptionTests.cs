using SPADE;
using Xunit;

namespace SPADE.Tests;

// StringParser.Definition describes a definition string's names and types without an instance (#18).
public class DefinitionDescriptionTests
{
    private static DefinitionDescription Describe(string grammar)
    {
        return new StringParser(grammar).Definition;
    }

    [Fact]
    public void NestedOutlineHasPathsAndKinds()
    {
        DefinitionDescription d = Describe("{((N,E),K) | N is set, E subset N unorderedcross N, K is int}");

        Assert.Equal("((N,E),K)", d.Outline);
        Assert.Equal(new[] { "N", "E", "K" }, d.Parts.Select(p => p.Name));

        Assert.Equal(PartKind.Set, d["N"].Kind);
        Assert.Null(d["N"].Of);
        Assert.Equal(new[] { 0, 0 }, d["N"].Path);

        Assert.Equal(PartKind.Subset, d["E"].Kind);
        Assert.Equal("N unorderedcross N", d["E"].Of);
        Assert.Equal(new[] { 0, 1 }, d["E"].Path);
        Assert.Equal(new[] { "E subset N unorderedcross N" }, d["E"].Constraints);

        Assert.Equal(PartKind.Int, d["K"].Kind);
        Assert.Equal(new[] { 1 }, d["K"].Path);
    }

    [Fact]
    public void NestedSetNamesAreNotParts()
    {
        DefinitionDescription d = Describe("{(N,E) | N is set, E subset {(e,w) | e is N unorderedcross N, w is int}}");

        Assert.Equal(new[] { "N", "E" }, d.Parts.Select(p => p.Name));
        Assert.Equal(PartKind.Subset, d["E"].Kind);
        Assert.Equal("{(e,w) | e is N unorderedcross N, w is int}", d["E"].Of);
        Assert.False(d.Contains("e"));
        Assert.False(d.Contains("w"));
    }

    [Theory]
    [InlineData("{(N,E) | N is set, E subset {(u,v) | u is N, v is N}}", "{(u,v) | u is N, v is N}")]
    [InlineData("{(U,S) | U is set, S subset {a | a subset U}}", "{a | a subset U}")]
    public void SubsetOfASetComprehension(string grammar, string of)
    {
        DefinitionDescription d = Describe(grammar);

        Assert.Equal(2, d.Parts.Count);
        Assert.Equal(PartKind.Set, d.Parts[0].Kind);
        Assert.Equal(PartKind.Subset, d.Parts[1].Kind);
        Assert.Equal(of, d.Parts[1].Of);
    }

    [Fact]
    public void IsMultisetWinsKindAndSubsetGivesOf()
    {
        DefinitionDescription d = Describe("{(S,T) | S is multiset, S subset int, T is int}");

        Assert.Equal(PartKind.Multiset, d["S"].Kind);
        Assert.Equal("int", d["S"].Of);
        Assert.Equal(new[] { "S is multiset", "S subset int" }, d["S"].Constraints);
    }

    [Fact]
    public void IsSetWithSubsetKeepsSet()
    {
        DefinitionDescription d = Describe("{(N,A) | N is set, A is set, A subset N}");

        Assert.Equal(PartKind.Set, d["A"].Kind);
        Assert.Equal("N", d["A"].Of);
    }

    [Fact]
    public void SingleNameOutlineHasEmptyPath()
    {
        DefinitionDescription d = Describe("{N | N is set}");

        Assert.Equal("N", d.Outline);
        Assert.Empty(d["N"].Path);
        Assert.Equal(PartKind.Set, d["N"].Kind);
    }

    [Fact]
    public void ListKinds()
    {
        DefinitionDescription d = Describe("{(T,D,P) | T is list, D is list, P is list}");

        Assert.All(d.Parts, p => Assert.Equal(PartKind.List, p.Kind));
        Assert.Equal(new[] { 2 }, d["P"].Path);
    }

    [Fact]
    public void OutlineDropsWhitespace()
    {
        DefinitionDescription d = Describe("{(i, w, v) | i subset int cross int, w is int, v is int}");

        Assert.Equal("(i,w,v)", d.Outline);
        Assert.Equal(PartKind.Subset, d["i"].Kind);
        Assert.Equal("int cross int", d["i"].Of);
    }

    [Fact]
    public void MembersOfOtherPartsAndExpressions()
    {
        DefinitionDescription d = Describe("{(N,A,u,E) | N is set, A is set, u is N, E is N cross A cross N}");

        Assert.Equal(PartKind.Member, d["u"].Kind);
        Assert.Equal("N", d["u"].Of);
        Assert.Equal(PartKind.Member, d["E"].Kind);
        Assert.Equal("N cross A cross N", d["E"].Of);
    }

    [Fact]
    public void UnconstrainedAndUnknownTypesAreAny()
    {
        DefinitionDescription d = Describe("{(A,B,C) | B is text, C is graph}");

        Assert.Equal(PartKind.Any, d["A"].Kind);
        Assert.Empty(d["A"].Constraints);
        Assert.Equal(PartKind.Any, d["B"].Kind);
        Assert.Null(d["B"].Of);
        Assert.Equal(new[] { "B is text" }, d["B"].Constraints);
        Assert.Equal(PartKind.Any, d["C"].Kind);
    }

    [Fact]
    public void IsStringIsTheStringKind()
    {
        DefinitionDescription d = Describe("{(x,y,S) | x is string, y is string, S is set, S is string}");

        Assert.Equal(PartKind.String, d["x"].Kind);
        Assert.Null(d["x"].Of);
        Assert.Equal(new[] { "x is string" }, d["x"].Constraints);
        Assert.Equal(PartKind.String, d["y"].Kind);
        // The first "is" type wins, as for the other types.
        Assert.Equal(PartKind.Set, d["S"].Kind);
    }

    [Fact]
    public void CompoundConstraintsAreListedButDontSetKind()
    {
        DefinitionDescription d = Describe("{(A,B) | A is set or B is set, not A is int}");

        Assert.Equal(PartKind.Any, d["A"].Kind);
        Assert.Equal(new[] { "A is set or B is set", "not A is int" }, d["A"].Constraints);
        Assert.Equal(new[] { "A is set or B is set" }, d["B"].Constraints);
    }

    [Fact]
    public void ConstraintsOnUnknownNamesAreIgnored()
    {
        DefinitionDescription d = Describe("{N | N is set, X is int}");

        Assert.Single(d.Parts);
        Assert.False(d.Contains("X"));
    }

    [Fact]
    public void BraceOutlineKeepsBraces()
    {
        DefinitionDescription d = Describe("{{A,B} | A is set, B is set}");

        Assert.Equal("{A,B}", d.Outline);
        Assert.Equal(new[] { 1 }, d["B"].Path);
    }

    [Fact]
    public void UnknownNameThrowsKeyNotFound()
    {
        DefinitionDescription d = Describe("{N | N is set}");

        KeyNotFoundException e = Assert.Throws<KeyNotFoundException>(() => d["E"]);
        Assert.Contains("E", e.Message);
        Assert.False(d.TryGetPart("E", out _));
        Assert.True(d.TryGetPart("N", out DefinitionPart? n));
        Assert.Equal("N", n!.Name);
    }

    [Fact]
    public void BadDefinitionThrowsOnlyWhenRead()
    {
        StringParser parser = new("not a definition");

        FormatException e = Assert.Throws<FormatException>(() => parser.Definition);
        Assert.Contains("not a definition", e.Message);
    }

    [Fact]
    public void PartToStringShowsKindAndWhatItIsOf()
    {
        DefinitionDescription d = Describe("{(N,E) | N is set, E subset N cross N}");

        Assert.Equal("N: Set", d["N"].ToString());
        Assert.Equal("E: Subset of N cross N", d["E"].ToString());
    }

    [Fact]
    public void ListOfValuesIsNotADefinition()
    {
        FormatException e = Assert.Throws<FormatException>(() => Describe("{a,b}"));
        Assert.Equal("Definition string \"{a,b}\" is a list of values, not a format followed by |", e.Message);
    }

    // Lexer errors (a character the grammar has no token for) and parser errors both give the position.
    [Theory]
    [InlineData("{N | N is set#}", "token recognition error at: '#' at position 14")]
    [InlineData("{N | N is set", "mismatched input '<EOF>' expecting {'}', CM} at position 14")]
    [InlineData("{N | N is set}}", "extraneous input '}' expecting <EOF> at position 15")]
    public void SyntaxErrorsGiveThePosition(string grammar, string reason)
    {
        FormatException e = Assert.Throws<FormatException>(() => Describe(grammar));
        Assert.Equal($"Definition string \"{grammar}\" can't be read: {reason}", e.Message);
    }

    [Fact]
    public void ParseDirectlyMatchesTheProperty()
    {
        Assert.Equal("(N,E)", DefinitionDescription.Parse("{(N,E) | N is set, E subset N cross N}").Outline);
        Assert.Throws<FormatException>(() => DefinitionDescription.Parse("{(N,E) | N is}"));
    }

    [Fact]
    public void WorksBeforeAndAfterParse()
    {
        StringParser graph = new("{(N,E) | N is set, E subset N unorderedcross N}");

        DefinitionDescription before = graph.Definition;
        graph.parse("({1,2,3},{{1,2},{2,3}})");
        DefinitionDescription after = graph.Definition;

        Assert.Same(before, after);
        Assert.Equal(PartKind.Subset, after["E"].Kind);
        Assert.Equal(new UtilCollection("{1,2,3}"), graph["N"]);
        Assert.Equal(new UtilCollection("{{1,2},{2,3}}"), graph["E"]);
    }
}
