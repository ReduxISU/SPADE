using SPADE;
using Xunit;

namespace SPADE.Tests;

// Boolean formulas (#44): Formula.Parse for general formulas, Formula.ParseCnf for CNF with
// Redux's CnfParser rules. The CNF cases are Redux's CnfParser_Tests (redux-tests/Interfaces/Logic),
// ported with Redux's 0-based positions moved to SPADE's 1-based ones.
public class FormulaTests
{
    // Clauses as e.g. "x1,!x2;x3", the shape Redux's CnfParser_Tests assert.
    private static string Shape(Formula f) =>
        string.Join(";", f.ToClauses().Select(c => string.Join(",", c)));

    // The tree with every node spelled out, e.g. "or(a,and(b,c))".
    private static string Tree(Formula f) => f switch
    {
        Formula.Variable v => v.Name,
        Formula.Not n => "not(" + Tree(n.Operand) + ")",
        Formula.And a => "and(" + string.Join(",", a.Operands.Select(Tree)) + ")",
        Formula.Or o => "or(" + string.Join(",", o.Operands.Select(Tree)) + ")",
        _ => throw new InvalidOperationException(),
    };

    // ---- Redux's accepted CNF inputs ----

    [Theory]
    // SAT and 3SAT default instances
    [InlineData("(!x3 | x4 | !x2 | x1 | x2) & (!x4 | !x1) & (x4 | x3 | !x1)", "!x3,x4,!x2,x1,x2;!x4,!x1;x4,x3,!x1")]
    [InlineData("(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)", "x1,!x2,x3;!x1,x3,x1;x2,!x3,!x1")]
    // Shapes used by Redux's tests and reductions
    [InlineData("(x1) & (!x1)", "x1;!x1")]
    [InlineData("( a | b | c ) & ( !a | b | b )", "a,b,c;!a,b,b")]
    [InlineData("(a|b)&(!c|d)", "a,b;!c,d")]
    // Bare clauses that are unambiguous
    [InlineData("x1", "x1")]
    [InlineData("!x1", "!x1")]
    [InlineData("x1 | !x2 | x3", "x1,!x2,x3")]
    [InlineData("x1 & !x2 & (x3 | x4)", "x1;!x2;x3,x4")]
    // Whitespace of any kind, and between '!' and the variable
    [InlineData("\t(x1 |\n x2)\r\n&\t( ! x3 )  ", "x1,x2;!x3")]
    // Identifiers
    [InlineData("(_a | B_2 | longName99)", "_a,B_2,longName99")]
    public void CnfAcceptsWhatReduxAccepts(string input, string expectedShape)
    {
        Formula cnf = Formula.ParseCnf(input);
        Assert.Equal(expectedShape, Shape(cnf));
        Assert.True(cnf.IsCnf);

        // The general parser reads every CNF formula to the same tree.
        Formula general = Formula.Parse(input);
        Assert.Equal(cnf, general);
        Assert.Equal(expectedShape, Shape(general));
    }

    [Fact]
    public void VariablesAreDistinctInOrderOfFirstAppearance()
    {
        Assert.Equal(new[] { "b", "a", "c" }, Formula.ParseCnf("(b | !a) & (a | c | !b)").Variables);
        Assert.Equal(new[] { "x", "y", "z" }, Formula.Parse("!(x & (y | !x)) | (z & y)").Variables);
    }

    [Theory]
    [InlineData("(!x3 | x4 | !x2 | x1 | x2) & (!x4 | !x1) & (x4 | x3 | !x1)")]
    [InlineData("x1 & !x2 & (x3 | x4)")]
    [InlineData("a | b")]
    public void CnfToStringRoundTrips(string input)
    {
        Formula f = Formula.ParseCnf(input);
        Formula again = Formula.ParseCnf(f.ToString());
        Assert.Equal(Shape(f), Shape(again));
        Assert.Equal(f, again);
        Assert.Equal(f.ToString(), again.ToString());
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    public void EvaluateChecksEveryClause(bool a, bool b, bool expected)
    {
        // (!a | b) is a -> b; the tautological second clause checks that every clause is consulted.
        Formula f = Formula.ParseCnf("(!a | b) & (a | !a)");
        Assert.Equal(expected, f.Evaluate(new Dictionary<string, bool> { ["a"] = a, ["b"] = b }));
    }

    // ---- Redux's rejected CNF inputs; position is Redux's + 1 ----

    [Theory]
    [InlineData("", 1, "empty")]
    [InlineData("   ", 4, "empty")]
    [InlineData("(x1 | x2", 9, "expected '|' or ')'")]
    [InlineData("x1 | x2)", 8, "expected '&' or end of input")]
    [InlineData(")x1(", 1, "expected a literal")]
    [InlineData("()", 1, "empty clause")]
    [InlineData("(x1 | x2) &", 12, "expected a literal")]
    [InlineData("& x1", 1, "expected a literal")]
    [InlineData("(x1 | | x2)", 7, "expected a literal")]
    [InlineData("(x1 x2)", 5, "expected '|' or ')'")]
    [InlineData("!!x1", 2, "double negation")]
    [InlineData("!(x1 | x2)", 2, "negating a parenthesized expression")]
    [InlineData("((x1 | x2))", 2, "nested parentheses")]
    [InlineData("(x1 & x2)", 5, "'&' inside a clause")]
    [InlineData("(x1) | (x2)", 6, "'|' cannot join")]
    [InlineData("x1 | x2 & x3", 1, "must be parenthesized")]
    [InlineData("x1 & x2 | x3", 6, "must be parenthesized")]
    [InlineData("(x-1)", 3, "unexpected character '-'")]
    [InlineData("(x$ | y)", 3, "unexpected character '$'")]
    [InlineData("(1x | y)", 2, "must start with a letter")]
    [InlineData("(x1 ∨ x2)", 5, "unexpected character")]
    public void CnfRejectsWhatReduxRejects(string input, int position, string messageFragment)
    {
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => Formula.ParseCnf(input));
        Assert.Equal(position, e.Position);
        Assert.Contains(messageFragment, e.Message);
        Assert.Contains($"at position {position} of the formula", e.Message);
    }

    [Fact]
    public void ExpectedAndFoundAreSetForSyntaxErrors()
    {
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => Formula.ParseCnf("(x1 | x2"));
        Assert.Equal("expected '|' or ')', found end of input (at position 9 of the formula)", e.Message);
        Assert.Equal("'|' or ')'", e.Expected);
        Assert.Equal("end of input", e.Found);
    }

    [Fact]
    public void NullThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Formula.Parse((string)null!));
        Assert.Throws<ArgumentNullException>(() => Formula.ParseCnf((string)null!));
    }

    // ---- General formulas ----

    [Theory]
    // ! binds tighter than &, which binds tighter than |.
    [InlineData("a | b & c", "or(a,and(b,c))")]
    [InlineData("a & b | c", "or(and(a,b),c)")]
    [InlineData("!a & b", "and(not(a),b)")]
    [InlineData("!a | !b & c", "or(not(a),and(not(b),c))")]
    [InlineData("(a | b) & c", "and(or(a,b),c)")]
    [InlineData("!(a | b)", "not(or(a,b))")]
    [InlineData("!!a", "not(not(a))")]
    [InlineData("! ! a", "not(not(a))")]
    // Chains of one operator are one node, whatever the grouping.
    [InlineData("a & b & c", "and(a,b,c)")]
    [InlineData("(a & b) & c", "and(a,b,c)")]
    [InlineData("a & (b & c)", "and(a,b,c)")]
    [InlineData("a | (b | (c | d))", "or(a,b,c,d)")]
    // Redundant parentheses vanish.
    [InlineData("((a))", "a")]
    [InlineData("((x1 | x2))", "or(x1,x2)")]
    [InlineData("(x1) | (x2)", "or(x1,x2)")]
    // Formulas Redux's CNF parser rejects as not CNF are fine here.
    [InlineData("(x1 & x2)", "and(x1,x2)")]
    [InlineData("!(x1 & (x2 | !x3)) | (x1 & x4)", "or(not(and(x1,or(x2,not(x3)))),and(x1,x4))")]
    public void GeneralFormulasUseStandardPrecedence(string input, string tree)
    {
        Assert.Equal(tree, Tree(Formula.Parse(input)));
    }

    [Theory]
    [InlineData("a | b & c")]
    [InlineData("a & b | c")]
    [InlineData("!a | !b & c")]
    [InlineData("!(a | b) & !(c & d)")]
    public void PrecedenceMatchesTheTruthTable(string input)
    {
        // Each input is checked against the same formula with explicit parentheses, under every assignment.
        Formula f = Formula.Parse(input);
        Func<bool, bool, bool, bool, bool> expected = input switch
        {
            "a | b & c" => (a, b, c, d) => a || (b && c),
            "a & b | c" => (a, b, c, d) => (a && b) || c,
            "!a | !b & c" => (a, b, c, d) => !a || (!b && c),
            _ => (a, b, c, d) => !(a || b) && !(c && d),
        };
        for (int bits = 0; bits < 16; bits++)
        {
            bool a = (bits & 1) != 0, b = (bits & 2) != 0, c = (bits & 4) != 0, d = (bits & 8) != 0;
            Dictionary<string, bool> assignment = new() { ["a"] = a, ["b"] = b, ["c"] = c, ["d"] = d };
            Assert.Equal(expected(a, b, c, d), f.Evaluate(assignment));
        }
    }

    [Theory]
    [InlineData("", 1, "formula is empty")]
    [InlineData("  \t", 4, "formula is empty")]
    [InlineData("(x1 | x2", 9, "expected '&', '|' or ')', found end of input")]
    [InlineData("x1 | x2)", 8, "expected '&', '|' or end of input, found ')'")]
    [InlineData("a b", 3, "expected '&', '|' or end of input, found 'b'")]
    [InlineData(")x1(", 1, "expected a variable, '!' or '(', found ')'")]
    [InlineData("()", 2, "expected a variable, '!' or '(', found ')'")]
    [InlineData("(x1 | x2) &", 12, "expected a variable, '!' or '(', found end of input")]
    [InlineData("& x1", 1, "expected a variable")]
    [InlineData("(x1 | | x2)", 7, "expected a variable")]
    [InlineData("a !", 3, "expected '&', '|' or end of input, found '!'")]
    [InlineData("!", 2, "expected a variable, '!' or '(', found end of input")]
    [InlineData("(x-1)", 3, "unexpected character '-'")]
    [InlineData("(1x | y)", 2, "must start with a letter")]
    [InlineData("a -> b", 3, "unexpected character '-'")]
    [InlineData("a ^ b", 3, "unexpected character '^'")]
    [InlineData("x1 \U0001D465", 4, "unexpected character '\U0001D465'")]
    public void GeneralRejectsWithAPosition(string input, int position, string messageFragment)
    {
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => Formula.Parse(input));
        Assert.Equal(position, e.Position);
        Assert.Contains(messageFragment, e.Message);
        Assert.Contains($"at position {position} of the formula", e.Message);
    }

    [Theory]
    [InlineData("x1 | x2 & x3")]
    [InlineData("x1 & x2 | x3")]
    [InlineData("!!x1")]
    [InlineData("!(x1 | x2)")]
    public void GeneralAcceptsWhatCnfRejectsAsNotCnf(string input)
    {
        Assert.Throws<SpadeParseException>(() => Formula.ParseCnf(input));
        Assert.False(Formula.Parse(input).IsCnf);
    }

    [Theory]
    [InlineData("((x1 | x2))")]
    [InlineData("(x1) | (x2)")]
    [InlineData("(x1 & x2)")]
    public void RedundantParenthesesAreCnfToTheGeneralParserOnly(string input)
    {
        // Redux rejects these spellings, so ParseCnf does too, but the formula itself is in CNF.
        Assert.Throws<SpadeParseException>(() => Formula.ParseCnf(input));
        Assert.True(Formula.Parse(input).IsCnf);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("!a", true)]
    [InlineData("a | !b", true)]
    [InlineData("a & b", true)]
    [InlineData("(a | b) & !c & (c | !a)", true)]
    [InlineData("!!a", false)]
    [InlineData("!(a | b)", false)]
    [InlineData("a | (b & c)", false)]
    [InlineData("(a & b) | c", false)]
    [InlineData("(a | (b & c)) & d", false)]
    public void IsCnf(string input, bool cnf)
    {
        Assert.Equal(cnf, Formula.Parse(input).IsCnf);
    }

    [Fact]
    public void ToClausesNeedsCnf()
    {
        Formula f = Formula.Parse("(a | !b) & c");
        IReadOnlyList<IReadOnlyList<Formula.Literal>> clauses = f.ToClauses();
        Assert.Equal(2, clauses.Count);
        Assert.Equal(new[] { new Formula.Literal("a", false), new Formula.Literal("b", true) }, clauses[0]);
        Assert.Equal(new[] { new Formula.Literal("c", false) }, clauses[1]);
        Assert.Equal("!b", clauses[0][1].ToString());

        Assert.Throws<InvalidOperationException>(() => Formula.Parse("a | (b & c)").ToClauses());
    }

    [Theory]
    [InlineData("(x1|!x2)&x3", "(x1 | !x2) & x3")]
    [InlineData("a | b & c", "a | (b & c)")]
    [InlineData("a&b|c", "(a & b) | c")]
    [InlineData("!(a&b)|c", "!(a & b) | c")]
    [InlineData("!!a", "!!a")]
    [InlineData("!(!a)", "!!a")]
    [InlineData("((a))", "a")]
    [InlineData("(a & b) & c", "a & b & c")]
    [InlineData("!(a | b) & (c | !(d & e))", "!(a | b) & (c | !(d & e))")]
    public void ToStringIsCanonical(string input, string canonical)
    {
        Formula f = Formula.Parse(input);
        Assert.Equal(canonical, f.ToString());
        Assert.Equal(f, Formula.Parse(canonical));
    }

    [Fact]
    public void RandomFormulasRoundTripThroughToString()
    {
        Random random = new(44);
        string[] names = { "a", "b", "c", "x1", "_y" };
        for (int i = 0; i < 500; i++)
        {
            Formula f = RandomFormula(random, names, 5);
            Formula again = Formula.Parse(f.ToString());
            Assert.Equal(f, again);
            Assert.Equal(f.GetHashCode(), again.GetHashCode());
            Assert.Equal(f.ToString(), again.ToString());
            if (f.IsCnf) Assert.Equal(f, Formula.ParseCnf(f.ToString()));
        }
    }

    private static Formula RandomFormula(Random random, string[] names, int depth)
    {
        int pick = depth <= 1 ? 0 : random.Next(4);
        switch (pick)
        {
            case 0: return new Formula.Variable(names[random.Next(names.Length)]);
            case 1: return new Formula.Not(RandomFormula(random, names, depth - 1));
            default:
                Formula[] operands = new Formula[2 + random.Next(2)];
                for (int i = 0; i < operands.Length; i++) operands[i] = RandomFormula(random, names, depth - 1);
                return pick == 2 ? new Formula.And(operands) : new Formula.Or(operands);
        }
    }

    [Fact]
    public void EvaluateNeedsEveryVariable()
    {
        // b can't change the result, but it is still looked up, so a missing value is always reported.
        Formula f = Formula.Parse("a | b");
        ArgumentException e = Assert.Throws<ArgumentException>(() => f.Evaluate(new Dictionary<string, bool> { ["a"] = true }));
        Assert.Contains("no value for b", e.Message);
        Assert.Throws<ArgumentNullException>(() => f.Evaluate(null!));
    }

    [Fact]
    public void EvaluateAllFalse()
    {
        // The assignment Redux's brute-force SAT solver once skipped (#788 there).
        Formula f = Formula.Parse("!a & !b");
        Assert.True(f.Evaluate(new Dictionary<string, bool> { ["a"] = false, ["b"] = false }));
        Assert.False(f.Evaluate(new Dictionary<string, bool> { ["a"] = true, ["b"] = false }));
    }

    [Fact]
    public void EqualityIsStructural()
    {
        Assert.Equal(Formula.Parse("a&b"), Formula.Parse(" a & b "));
        Assert.True(Formula.Parse("a&b") == Formula.Parse("(a) & (b)"));
        Assert.NotEqual(Formula.Parse("a & b"), Formula.Parse("b & a"));
        Assert.NotEqual(Formula.Parse("a & b"), Formula.Parse("a | b"));
        Assert.NotEqual(Formula.Parse("!!a"), Formula.Parse("a"));
        Assert.True(Formula.Parse("a") != Formula.Parse("b"));
        Assert.False(Formula.Parse("a").Equals((object)"a"));
        Assert.Equal(Formula.Parse("!(a | b)").GetHashCode(), Formula.Parse("!((a) | b)").GetHashCode());

        HashSet<Formula> set = new() { Formula.Parse("a | b"), Formula.Parse("(a | b)"), Formula.Parse("b | a") };
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void BuildingByHand()
    {
        Formula.Variable a = new("a");
        Formula.Variable b = new("b");
        Formula f = new Formula.Or(new Formula.Not(a), new Formula.And(a, b));
        Assert.Equal("!a | (a & b)", f.ToString());
        Assert.Equal(Formula.Parse("!a | a & b"), f);
        Assert.Equal(3, f.Depth);

        // An operand of the same kind is merged in, as the parser does.
        Assert.Equal(Formula.Parse("a & b & a"), new Formula.And(new Formula.And(a, b), a));

        Assert.Throws<ArgumentException>(() => new Formula.Variable("1x"));
        Assert.Throws<ArgumentException>(() => new Formula.Variable(""));
        Assert.Throws<ArgumentException>(() => new Formula.Variable("a b"));
        Assert.Throws<ArgumentNullException>(() => new Formula.Variable(null!));
        Assert.Throws<ArgumentException>(() => new Formula.And(a));
        Assert.Throws<ArgumentException>(() => new Formula.Or(a, null!));
        Assert.Throws<ArgumentNullException>(() => new Formula.Not(null!));
        Assert.Throws<ArgumentNullException>(() => new Formula.And((IEnumerable<Formula>)null!));
    }

    [Fact]
    public void IsLiteral()
    {
        Assert.True(Formula.Parse("a").IsLiteral);
        Assert.True(Formula.Parse("!a").IsLiteral);
        Assert.False(Formula.Parse("!!a").IsLiteral);
        Assert.False(Formula.Parse("a | b").IsLiteral);
    }

    // ---- Depth (#7): deep input is an error, never a crash ----

    [Fact]
    public void NestingUpToTheLimitIsFine()
    {
        // A variable is 1 level, so MaxDepth - 1 negations reach the limit exactly.
        Formula f = Formula.Parse(new string('!', Formula.MaxDepth - 1) + "a");
        Assert.Equal(Formula.MaxDepth, f.Depth);
        Assert.Equal(f, Formula.Parse(f.ToString()));
        Assert.True(f.Evaluate(new Dictionary<string, bool> { ["a"] = false }) == ((Formula.MaxDepth - 1) % 2 == 1));

        // Parentheses that add no level count while reading, up to the same limit.
        string parens = new string('(', Formula.MaxDepth) + "a" + new string(')', Formula.MaxDepth);
        Assert.Equal(new Formula.Variable("a"), Formula.Parse(parens));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100_000)]
    public void TooManyNegationsIsAnError(int extra)
    {
        string text = new string('!', Formula.MaxDepth - 1 + extra) + "a";
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => Formula.Parse(text));
        Assert.Contains($"at most {Formula.MaxDepth} levels", e.Message);
        Assert.NotNull(e.Position);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100_000)]
    public void TooManyParenthesesIsAnError(int extra)
    {
        int n = Formula.MaxDepth + extra;
        string text = new string('(', n) + "a" + new string(')', n);
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => Formula.Parse(text));
        Assert.Contains($"at most {Formula.MaxDepth} levels", e.Message);
        Assert.Equal(Formula.MaxDepth + 1, e.Position);
    }

    [Fact]
    public void DeepAlternatingOperatorsAreAnError()
    {
        // Each level of parentheses adds two levels to the tree (an & over an |), so the tree passes
        // the limit well before the parentheses do.
        int levels = Formula.MaxDepth;
        string text = string.Concat(Enumerable.Repeat("a & (b | ", levels)) + "c" + new string(')', levels);
        SpadeParseException e = Assert.Throws<SpadeParseException>(() => Formula.Parse(text));
        Assert.Contains($"at most {Formula.MaxDepth} levels", e.Message);
    }

    [Fact]
    public void BuildingTooDeepByHandThrows()
    {
        Formula f = new Formula.Variable("a");
        for (int i = 1; i < Formula.MaxDepth; i++) f = new Formula.Not(f);
        Assert.Equal(Formula.MaxDepth, f.Depth);
        Formula deepest = f;
        Assert.Throws<ArgumentException>(() => new Formula.Not(deepest));
        Assert.Throws<ArgumentException>(() => new Formula.And(deepest, new Formula.Variable("b")));
    }

    [Fact]
    public void LongFlatFormulasAreFine()
    {
        // 100,000 clauses is wide, not deep: the tree is four levels (and, or, not, variable).
        string text = string.Join(" & ", Enumerable.Range(0, 100_000).Select(i => $"(x{i} | !y{i})"));
        Formula f = Formula.ParseCnf(text);
        Assert.Equal(4, f.Depth);
        Assert.Equal(100_000, f.ToClauses().Count);
        Assert.Equal(200_000, f.Variables.Count);
        Assert.Equal(f, Formula.Parse(text));
    }
}
