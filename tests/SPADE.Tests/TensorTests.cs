using SPADE;
using Xunit;

namespace SPADE.Tests;

// Matrices and tensors: "C is matrix int", "T is tensor 3 number", "d is list int size m", read from
// nested lists with shape checks (#30).
public class TensorTests
{
    private const string Ilp = "{(C,d) | C is matrix int size m n, d is list int size m}";

    private static StringParser Parse(string grammar, string instance)
    {
        StringParser parser = new(grammar);
        parser.parse(instance);
        return parser;
    }

    private static SpadeParseException Fails(string grammar, string instance)
    {
        return Assert.Throws<SpadeParseException>(() => new StringParser(grammar).parse(instance));
    }

    private static FormatException DefinitionFails(string grammar)
    {
        return Assert.Throws<FormatException>(() => new StringParser(grammar));
    }

    // Parsing, by element type and rank.

    [Fact]
    public void MatrixOfIntHasShapeAndElements()
    {
        Tensor c = Parse("{C | C is matrix int}", "((1,2,3),(4,5,6))").GetTensor("C");

        Assert.Equal(ScalarType.Int, c.ElementType);
        Assert.Equal(2, c.Rank);
        Assert.Equal(new[] { 2, 3 }, c.Shape);
        Assert.Equal(6, c.Count);
        Assert.Equal("6", c[1, 2]);
        Assert.Equal(4, c.GetInt(1, 0));
        Assert.Equal(2.0, c.GetNumber(0, 1));
        Assert.Equal(new[] { "1", "2", "3", "4", "5", "6" }, c.Values);
        Assert.Equal("((1,2,3),(4,5,6))", c.ToString());
    }

    [Fact]
    public void MatrixOfNumberAcceptsDecimalsAndInts()
    {
        Tensor c = Parse("{C | C is matrix number}", "((1.5,-2),(.5,0))").GetTensor("C");

        Assert.Equal(ScalarType.Number, c.ElementType);
        Assert.Equal(-2.0, c.GetNumber(0, 1));
        Assert.Equal(0.5, c.GetNumber(1, 0));
        Assert.Throws<InvalidOperationException>(() => c.GetInt(0, 0));
    }

    [Fact]
    public void MatrixOfStringAcceptsAnySingleValue()
    {
        Tensor c = Parse("{C | C is matrix string}", "((a,\"b, c\"),(5,x))").GetTensor("C");

        Assert.Equal(ScalarType.String, c.ElementType);
        Assert.Equal("b, c", c[0, 1]);
        Assert.Equal("((a,\"b, c\"),(5,x))", c.ToString());
        Assert.Throws<InvalidOperationException>(() => c.GetNumber(0, 0));
        Assert.Throws<InvalidOperationException>(() => c.GetInt(0, 0));
    }

    [Fact]
    public void TensorOfRankThree()
    {
        Tensor t = Parse("{T | T is tensor 3 int}", "(((1,2),(3,4),(5,6)),((7,8),(9,10),(11,12)))").GetTensor("T");

        Assert.Equal(3, t.Rank);
        Assert.Equal(new[] { 2, 3, 2 }, t.Shape);
        Assert.Equal(10, t.GetInt(1, 1, 1));
        Assert.Equal(12, t.Count);
    }

    [Fact]
    public void TensorOfRankThreeOfNumberAndString()
    {
        Assert.Equal(new[] { 1, 1, 2 }, Parse("{T | T is tensor 3 number}", "(((1.5,2)))").GetTensor("T").Shape);
        Assert.Equal("b", Parse("{T | T is tensor 3 string}", "(((a,b)))").GetTensor("T")[0, 0, 1]);
    }

    [Fact]
    public void TensorOfRankOneIsAVector()
    {
        Tensor v = Parse("{v | v is tensor 1 number}", "(1,2.5,3)").GetTensor("v");

        Assert.Equal(1, v.Rank);
        Assert.Equal(new[] { 3 }, v.Shape);
        Assert.Equal(2.5, v.GetNumber(1));
    }

    [Fact]
    public void MatrixIsTensorOfRankTwo()
    {
        Tensor m = Parse("{C | C is matrix int}", "((1,2),(3,4))").GetTensor("C");
        Tensor t = Parse("{C | C is tensor 2 int}", "((1,2),(3,4))").GetTensor("C");

        Assert.Equal(m, t);
    }

    [Fact]
    public void ListWithElementTypeIsReadAsATensor()
    {
        StringParser p = Parse("{d | d is list int}", "(4,5,6)");

        Assert.Equal(new[] { 3 }, p.GetTensor("d").Shape);
        Assert.Equal("(4,5,6)", p["d"].ToString());
    }

    [Fact]
    public void PlainListIsNotATensor()
    {
        StringParser p = Parse("{d | d is list}", "(4,5,6)");

        Assert.False(p.TryGetTensor("d", out Tensor? t));
        Assert.Null(t);
        Assert.Throws<KeyNotFoundException>(() => p.GetTensor("d"));
    }

    [Fact]
    public void TensorBeforeParseThrows()
    {
        StringParser p = new("{C | C is matrix int}");
        Assert.Throws<InvalidOperationException>(() => p.GetTensor("C"));
        Assert.Throws<InvalidOperationException>(() => p.TryGetTensor("C", out _));
    }

    [Fact]
    public void TheValueStaysNestedListsInTheMap()
    {
        StringParser p = Parse("{C | C is matrix int}", "( (1, 2) , (3,4) )");

        Assert.Equal("((1,2),(3,4))", p["C"].ToString());
        Assert.Equal(p.GetTensor("C").ToCollection(), p["C"]);
    }

    // Rectangular checks.

    [Fact]
    public void ShortRowIsNamed()
    {
        SpadeParseException e = Fails("{C | C is matrix int}", "((1,2,3),(4,5,6),(7,8))");

        Assert.Equal("Constraint C is matrix int failed: row 3 of C has 2 values, expected 3", e.Message);
        Assert.Equal("C", e.Part);
        Assert.Equal("(7,8)", e.Element);
    }

    [Fact]
    public void RaggedRowInsideATensorIsNamedByItsPosition()
    {
        SpadeParseException e = Fails("{T | T is tensor 3 int}", "(((1,2),(3,4)),((5,6),(7)))");

        Assert.Contains("row (2,2) of T has 1 value, expected 2", e.Message);
        Assert.Equal("(7)", e.Element);
    }

    [Fact]
    public void RaggedMiddleDimensionIsNamed()
    {
        SpadeParseException e = Fails("{T | T is tensor 3 int}", "(((1,2),(3,4)),((5,6)))");

        Assert.Contains("row 2 of T has 1 row, expected 2", e.Message);
    }

    [Fact]
    public void RowThatIsAValueIsNotAList()
    {
        SpadeParseException e = Fails("{C | C is matrix int}", "((1,2),3)");

        Assert.Contains("row 2 of C is not a list", e.Message);
        Assert.Equal("3", e.Element);
    }

    [Fact]
    public void RowThatIsASetIsNotAList()
    {
        Assert.Contains("row 1 of C is not a list", Fails("{C | C is matrix int}", "({1,2},(3,4))").Message);
    }

    [Fact]
    public void ValueOrSetForTheWholePartIsNotAList()
    {
        Assert.Contains("C is not a list", Fails("{C | C is matrix int}", "5").Message);
        Assert.Contains("d is not a list", Fails("{d | d is list int}", "{1,2}").Message);
    }

    [Fact]
    public void ElementOfTheWrongTypeIsNamedByPosition()
    {
        SpadeParseException e = Fails("{C | C is matrix int}", "((1,2),(x,4))");

        Assert.Equal("Constraint C is matrix int failed: element (2,1) of C is not an int", e.Message);
        Assert.Equal("C", e.Part);
        Assert.Equal("x", e.Element);
    }

    [Fact]
    public void ElementErrorsForEachType()
    {
        Assert.Contains("element 2 of d is not an int", Fails("{d | d is list int}", "(1,2.5)").Message);
        Assert.Contains("element (1,2) of C is not a number", Fails("{C | C is matrix number}", "((1,x))").Message);
        Assert.Contains("element (1,2) of C is not a string", Fails("{C | C is matrix string}", "((a,{b}))").Message);
    }

    [Fact]
    public void TooDeepIsAnElementError()
    {
        Assert.Contains("element (1,1) of C is not an int", Fails("{C | C is matrix int}", "(((1)))").Message);
    }

    // Sizes.

    [Fact]
    public void IlpSizesSharedAcrossParts()
    {
        StringParser p = Parse(Ilp, "(((-1,-1,0),(0,-1,-1),(1,1,1)),(-1,-1,2))");

        Assert.Equal(new[] { 3, 3 }, p.GetTensor("C").Shape);
        Assert.Equal(new[] { 3 }, p.GetTensor("d").Shape);
    }

    [Fact]
    public void IlpWithNonSquareMatrix()
    {
        StringParser p = Parse(Ilp, "(((1,2,3),(4,5,6)),(7,8))");

        Assert.Equal(new[] { 2, 3 }, p.GetTensor("C").Shape);
    }

    [Fact]
    public void IlpMismatchNamesTheSizeAndWhereItCameFrom()
    {
        // Constraints are checked from the last to the first, so d gives m its value and C is blamed.
        SpadeParseException e = Fails(Ilp, "(((1,2),(3,4),(5,6)),(1,2,3,4))");

        Assert.Equal("Constraint C is matrix int size m n failed: C has 3 rows but m is 4 (from d)", e.Message);
        Assert.Equal("C", e.Part);
        Assert.Equal("((1,2),(3,4),(5,6))", e.Element);
    }

    [Fact]
    public void MismatchReportedOnTheOtherPartWhenTheOrderIsSwapped()
    {
        SpadeParseException e = Fails("{(C,d) | d is list int size m, C is matrix int size m n}", "(((1,2),(3,4),(5,6)),(1,2))");

        Assert.Equal("Constraint d is list int size m failed: d has 2 values but m is 3 (from C)", e.Message);
        Assert.Equal("d", e.Part);
        Assert.Equal("(1,2)", e.Element);
    }

    [Fact]
    public void SudokuShapedGridOfConstants()
    {
        string row = "(" + string.Join(",", Enumerable.Range(1, 9)) + ")";
        string grid = "(" + string.Join(",", Enumerable.Repeat(row, 9)) + ")";

        Tensor g = Parse("{G | G is matrix int size 9 9}", grid).GetTensor("G");
        Assert.Equal(new[] { 9, 9 }, g.Shape);
    }

    [Fact]
    public void ConstantSizeMismatch()
    {
        string row = "(" + string.Join(",", Enumerable.Range(1, 9)) + ")";
        string eightRows = "(" + string.Join(",", Enumerable.Repeat(row, 8)) + ")";
        string shortRow = "(" + string.Join(",", Enumerable.Range(1, 8)) + ")";
        string narrow = "(" + string.Join(",", Enumerable.Repeat(shortRow, 9)) + ")";

        Assert.Contains("G has 8 rows, expected 9", Fails("{G | G is matrix int size 9 9}", eightRows).Message);
        Assert.Contains("row 1 of G has 8 values, expected 9", Fails("{G | G is matrix int size 9 9}", narrow).Message);
    }

    [Fact]
    public void SameNameTwiceMakesASquare()
    {
        Assert.Equal(new[] { 2, 2 }, Parse("{G | G is matrix int size n n}", "((1,2),(3,4))").GetTensor("G").Shape);

        SpadeParseException e = Fails("{G | G is matrix int size n n}", "((1,2,3),(4,5,6))");
        Assert.Contains("row 1 of G has 3 values but n is 2 (from G)", e.Message);
    }

    [Fact]
    public void SizeNamedAfterAnIntPartTakesItsValue()
    {
        Assert.Equal(new[] { 2 }, Parse("{(n,v) | n is int, v is list int size n}", "(2,(5,6))").GetTensor("v").Shape);

        SpadeParseException e = Fails("{(n,v) | n is int, v is list int size n}", "(3,(5,6))");
        Assert.Contains("v has 2 values but n is 3 (from n)", e.Message);
    }

    [Fact]
    public void SizeNamedAfterAPartThatIsNotAWholeNumberFails()
    {
        SpadeParseException e = Fails("{(n,v) | v is list int size n}", "(x,(5,6))");
        Assert.Contains("size n of v is the part n, which is x, not a whole number", e.Message);
        Assert.Contains("size n of v is the part n", Fails("{(n,v) | v is list int size n}", "(-1,(5,6))").Message);
    }

    [Fact]
    public void SizesStartFreeForEachMemberOfANestedSet()
    {
        // Each member of E is checked on its own, so m can differ between members.
        StringParser p = Parse("{E | E subset {(a,b) | a is list int size m, b is list int size m}}", "{((1),(2)),((1,2),(3,4))}");
        Assert.Equal(2, p["E"].Count());

        SpadeParseException e = Fails("{E | E subset {(a,b) | a is list int size m, b is list int size m}}", "{((1),(2,3))}");
        Assert.Equal("E", e.Part);
        Assert.Contains("a has 1 value but m is 2 (from b)", e.Message);
    }

    // Empty and degenerate cases.

    [Fact]
    public void OneByOneMatrix()
    {
        Tensor c = Parse("{C | C is matrix int}", "((7))").GetTensor("C");
        Assert.Equal(new[] { 1, 1 }, c.Shape);
        Assert.Equal(7, c.GetInt(0, 0));
    }

    [Fact]
    public void NoRowsIsAZeroByZeroMatrix()
    {
        Tensor c = Parse("{C | C is matrix int}", "()").GetTensor("C");
        Assert.Equal(new[] { 0, 0 }, c.Shape);
        Assert.Equal(0, c.Count);
        Assert.Equal("()", c.ToString());
    }

    [Fact]
    public void EmptyRowsKeepTheirCount()
    {
        Tensor c = Parse("{C | C is matrix int}", "((),())").GetTensor("C");
        Assert.Equal(new[] { 2, 0 }, c.Shape);
        Assert.Equal("((),())", c.ToString());
    }

    [Fact]
    public void DimensionsAfterAnEmptyOneAreNotChecked()
    {
        // With no rows, n can't be read, so it isn't checked against 3.
        StringParser p = Parse("{(C,D) | C is matrix int size m n, D is matrix int size k n}", "((),((1,2,3)))");
        Assert.Equal(new[] { 0, 0 }, p.GetTensor("C").Shape);
        Assert.Contains("C has 1 row, expected 0", Fails("{C | C is matrix int size 0 5}", "((1,2,3,4,5))").Message);
    }

    [Fact]
    public void EmptyRowAmongFullOnesIsRagged()
    {
        Assert.Contains("row 2 of C has 0 values, expected 2", Fails("{C | C is matrix int}", "((1,2),())").Message);
    }

    // Definition errors.

    [Theory]
    [InlineData("{C | C is set int}", "only list, matrix and tensor take an element type, not set")]
    [InlineData("{C | C is tensor 0 int}", "number of dimensions must be a whole number")]
    [InlineData("{C | C is tensor x int}", "number of dimensions must be a whole number")]
    [InlineData("{C | C is tensor 999 int}", "number of dimensions must be a whole number")]
    [InlineData("{C | C is matrix list}", "the element type must be int, number or string, not list")]
    [InlineData("{C | C is matrix int size m}", "a matrix needs 2 sizes, found 1")]
    [InlineData("{C | C is tensor 3 int size a b}", "a tensor 3 needs 3 sizes, found 2")]
    [InlineData("{C | C is list int size a b}", "a list needs 1 size, found 2")]
    [InlineData("{C | C is matrix int size 2.5 3}", "a size must be a name or a whole number, not 2.5")]
    [InlineData("{C | C subset matrix int}", "can only follow \"is\"")]
    [InlineData("{(C,D) | C is set, D is matrix int union C}", "can only follow \"is\"")]
    public void UncheckableShapesAreRejectedWithTheDefinition(string grammar, string reason)
    {
        FormatException e = DefinitionFails(grammar);
        Assert.Contains(reason, e.Message);
        Assert.Contains(grammar, e.Message);
    }

    [Theory]
    [InlineData("{C | C is matrix}")]
    [InlineData("{C | C is tensor int}")]
    [InlineData("{C | C is matrix int size}")]
    public void IncompleteShapesAreSyntaxErrors(string grammar)
    {
        Assert.Contains("can't be read", DefinitionFails(grammar).Message);
    }

    [Fact]
    public void DescribingRejectsAnUncheckableShapeToo()
    {
        Assert.Throws<FormatException>(() => DefinitionDescription.Parse("{C | C is matrix int size m}"));
    }

    // Equality and hashing.

    [Fact]
    public void EqualTensorsHaveEqualHashes()
    {
        Tensor a = Parse("{C | C is matrix int}", "((1,2),(3,4))").GetTensor("C");
        Tensor b = new(ScalarType.Int, new[] { 2, 2 }, new[] { "1", "2", "3", "4" });

        Assert.Equal(a, b);
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Single(new HashSet<Tensor> { a, b });
    }

    [Fact]
    public void ShapeTypeOrValuesMakeTensorsDifferent()
    {
        Tensor a = new(ScalarType.Int, new[] { 2, 2 }, new[] { "1", "2", "3", "4" });

        Assert.NotEqual(a, new Tensor(ScalarType.Int, new[] { 4, 1 }, new[] { "1", "2", "3", "4" }));
        Assert.NotEqual(a, new Tensor(ScalarType.Int, new[] { 4 }, new[] { "1", "2", "3", "4" }));
        Assert.NotEqual(a, new Tensor(ScalarType.Number, new[] { 2, 2 }, new[] { "1", "2", "3", "4" }));
        Assert.NotEqual(a, new Tensor(ScalarType.Int, new[] { 2, 2 }, new[] { "1", "2", "3", "5" }));
        Assert.False(a.Equals(null));
        Assert.False(a.Equals("((1,2),(3,4))"));
        Assert.True(a.Equals(a));
    }

    [Fact]
    public void EmptyShapesAreNormalized()
    {
        Tensor a = new(ScalarType.Int, new[] { 0, 3 }, Array.Empty<string>());
        Assert.Equal(new[] { 0, 0 }, a.Shape);
        Assert.Equal(Parse("{C | C is matrix int}", "()").GetTensor("C"), a);
    }

    [Fact]
    public void ConstructorChecksItsInput()
    {
        Assert.Throws<ArgumentException>(() => new Tensor(ScalarType.Int, Array.Empty<int>(), Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => new Tensor(ScalarType.Int, new[] { -1 }, Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => new Tensor(ScalarType.Int, new[] { 2 }, new[] { "1" }));
        Assert.Throws<ArgumentException>(() => new Tensor(ScalarType.Int, new[] { 1 }, new[] { "x" }));
        Assert.Throws<ArgumentException>(() => new Tensor(ScalarType.Int, new[] { 1 }, new string[] { null! }));
        Assert.Throws<ArgumentException>(() => new Tensor(ScalarType.Int, new[] { 65536, 65536 }, new[] { "1" }));
        Assert.Throws<ArgumentNullException>(() => new Tensor(ScalarType.Int, null!, Array.Empty<string>()));
        Assert.Throws<ArgumentNullException>(() => new Tensor(ScalarType.Int, new[] { 0 }, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tensor((ScalarType)9, new[] { 0 }, Array.Empty<string>()));
    }

    [Fact]
    public void ConstructorCopiesItsInput()
    {
        int[] shape = { 2 };
        string[] values = { "1", "2" };
        Tensor t = new(ScalarType.Int, shape, values);
        shape[0] = 5;
        values[0] = "9";

        Assert.Equal(new[] { 2 }, t.Shape);
        Assert.Equal("1", t[0]);
    }

    [Fact]
    public void BadIndicesThrow()
    {
        Tensor t = new(ScalarType.Int, new[] { 2, 2 }, new[] { "1", "2", "3", "4" });

        Assert.Throws<ArgumentException>(() => t[0]);
        Assert.Throws<IndexOutOfRangeException>(() => t[2, 0]);
        Assert.Throws<IndexOutOfRangeException>(() => t[0, -1]);
    }

    // Description (#18).

    [Fact]
    public void DescriptionReportsElementTypeRankAndSizes()
    {
        DefinitionDescription d = new StringParser(Ilp).Definition;

        Assert.Equal(PartKind.Tensor, d["C"].Kind);
        Assert.Equal(ScalarType.Int, d["C"].ElementType);
        Assert.Equal(2, d["C"].Rank);
        Assert.Equal(new[] { "m", "n" }, d["C"].Sizes);
        Assert.Equal("C: Tensor 2 int size m n", d["C"].ToString());

        Assert.Equal(PartKind.List, d["d"].Kind);
        Assert.Equal(ScalarType.Int, d["d"].ElementType);
        Assert.Equal(1, d["d"].Rank);
        Assert.Equal(new[] { "m" }, d["d"].Sizes);
        Assert.Equal("d: List int size m", d["d"].ToString());
    }

    [Fact]
    public void DescriptionOfTensorsWithFreeAndConstantSizes()
    {
        DefinitionDescription d = DefinitionDescription.Parse("{(G,T,S) | G is matrix int size 9 9, T is tensor 3 number, S is list}");

        Assert.Equal(new[] { "9", "9" }, d["G"].Sizes);
        Assert.Equal(PartKind.Tensor, d["T"].Kind);
        Assert.Equal(3, d["T"].Rank);
        Assert.Equal(ScalarType.Number, d["T"].ElementType);
        Assert.Empty(d["T"].Sizes);
        Assert.Equal("T: Tensor 3 number", d["T"].ToString());

        Assert.Equal(PartKind.List, d["S"].Kind);
        Assert.Null(d["S"].ElementType);
        Assert.Null(d["S"].Rank);
        Assert.Empty(d["S"].Sizes);
        Assert.Equal("S: List", d["S"].ToString());
    }

    [Fact]
    public void FirstTypeConstraintGivesTheShape()
    {
        DefinitionDescription d = DefinitionDescription.Parse("{C | C is set, C is matrix string}");
        Assert.Equal(PartKind.Set, d["C"].Kind);
        Assert.Null(d["C"].ElementType);

        DefinitionDescription e = DefinitionDescription.Parse("{C | C is tensor 2 string, C is matrix int}");
        Assert.Equal(ScalarType.String, e["C"].ElementType);
    }

    [Fact]
    public void ShapeWordsAreReserved()
    {
        // matrix, tensor and size are keywords now, so they can't name a part.
        Assert.Throws<FormatException>(() => new StringParser("{(size,C) | C is set}"));
    }
}
