using SPADE;
using Xunit;

namespace SPADE.Tests;

// "phi is formula" and "phi is cnf" in a definition string (#44).
public class FormulaPartTests
{
    // A SAT-like instance: a CNF formula and its variable set.
    private const string Sat = "{(phi,X) | phi is cnf, X is set}";
    private const string General = "{(phi,X) | phi is formula, X is set}";

    private static StringParser Parsed(string definition, string instance)
    {
        StringParser parser = new(definition);
        parser.parse(instance);
        return parser;
    }

    private static SpadeParseException Fails(string definition, string instance)
    {
        return Assert.Throws<SpadeParseException>(() => new StringParser(definition).parse(instance));
    }

    [Fact]
    public void CnfPartReadsAsAFormula()
    {
        StringParser p = Parsed(Sat, "(\"(x1 | !x2) & x3\", {x1,x2,x3})");
        Formula phi = Formula.Parse(p["phi"]);
        Assert.True(phi.IsCnf);
        Assert.Equal("(x1 | !x2) & x3", phi.ToString());
        Assert.Equal(phi, Formula.ParseCnf(p["phi"]));

        // The definition can't say every variable is in X; a caller checks it with Variables.
        Assert.All(phi.Variables, v => Assert.True(p["X"].Contains(new UtilCollection(v))));
    }

    [Fact]
    public void ReduxDefaultSatInstanceFitsACnfPart()
    {
        const string formula = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)";
        StringParser p = Parsed(Sat, $"(\"{formula}\", {{x1,x2,x3}})");
        Assert.Equal(Formula.ParseCnf(formula), Formula.Parse(p["phi"]));
    }

    [Fact]
    public void GeneralFormulaPart()
    {
        StringParser p = Parsed(General, "(\"!(x1 & (x2 | !x3)) | (x1 & x4)\", {x1,x2,x3,x4})");
        Formula phi = Formula.Parse(p["phi"]);
        Assert.False(phi.IsCnf);
        Assert.Equal(new[] { "x1", "x2", "x3", "x4" }, phi.Variables);
    }

    [Fact]
    public void FormulaWithoutParenthesesOrCommasNeedsNoQuotes()
    {
        StringParser p = Parsed("{phi | phi is formula}", "a & !b | c");
        Assert.Equal("(a & !b) | c", Formula.Parse(p["phi"]).ToString());
    }

    [Fact]
    public void WholeInstanceCanBeAQuotedFormula()
    {
        StringParser p = Parsed("{phi | phi is cnf}", "\"(a | b) & !c\"");
        Assert.Equal(2, Formula.Parse(p["phi"]).ToClauses().Count);
    }

    [Fact]
    public void BadFormulaNamesThePartAndKeepsThePosition()
    {
        SpadeParseException e = Fails(Sat, "(\"(x1 | x2\", {x1,x2})");
        Assert.Equal("Constraint phi is cnf failed: (x1 | x2 is not a CNF formula: expected '|' or ')', found end of input (at position 9 of the formula)", e.Message);
        Assert.Equal("phi", e.Part);
        Assert.Equal("(x1 | x2", e.Element);
        Assert.Null(e.Position);

        SpadeParseException inner = Assert.IsType<SpadeParseException>(e.InnerException);
        Assert.Equal(9, inner.Position);
        Assert.Equal("'|' or ')'", inner.Expected);
    }

    [Fact]
    public void CnfPartRejectsWhatAFormulaPartAccepts()
    {
        const string instance = "(\"x1 | x2 & x3\", {x1,x2,x3})";
        SpadeParseException e = Fails(Sat, instance);
        Assert.Contains("must be parenthesized", e.Message);
        Assert.Equal("phi", e.Part);

        StringParser p = Parsed(General, instance);
        Assert.Equal("x1 | (x2 & x3)", Formula.Parse(p["phi"]).ToString());
    }

    [Fact]
    public void SetIsNotAFormula()
    {
        SpadeParseException e = Fails(General, "({a,b}, {a,b})");
        Assert.Contains("is not a formula; a formula with ( ) or , in it is written in double quotes", e.Message);
        Assert.Equal("phi", e.Part);
        Assert.Equal("{a,b}", e.Element);

        // An unquoted formula in parentheses reads as a list.
        e = Fails(General, "((a | b), {a,b})");
        Assert.Contains("is not a formula", e.Message);

        Assert.Throws<SpadeParseException>(() => Formula.Parse(new UtilCollection("{a,b}")));
        Assert.Throws<ArgumentNullException>(() => Formula.Parse((UtilCollection)null!));
    }

    [Fact]
    public void FormulasInsideANestedSet()
    {
        const string definition = "{F | F subset {f | f is formula}}";
        StringParser p = Parsed(definition, "{\"a & b\", \"!(a | c)\", d}");
        Assert.Equal(3, p["F"].Count());

        SpadeParseException e = Fails(definition, "{\"a & b\", \"a &\"}");
        Assert.Equal("F", e.Part);
        Assert.Equal("a &", e.Element);
        Assert.Contains("expected a variable, '!' or '(', found end of input", e.Message);
    }

    [Fact]
    public void FormulaPartsInALargerDefinition()
    {
        // Two formulas and a variable set, e.g. an equivalence-checking instance.
        const string definition = "{((phi,psi),X) | phi is formula, psi is cnf, X is set}";
        StringParser p = Parsed(definition, "((\"!(a & b)\", \"(!a | !b)\"), {a,b})");
        Formula phi = Formula.Parse(p["phi"]);
        Formula psi = Formula.Parse(p["psi"]);
        foreach (bool a in new[] { false, true })
            foreach (bool b in new[] { false, true })
            {
                Dictionary<string, bool> assignment = new() { ["a"] = a, ["b"] = b };
                Assert.Equal(phi.Evaluate(assignment), psi.Evaluate(assignment));
            }
    }

    [Fact]
    public void DescriptionKnowsFormulaParts()
    {
        DefinitionDescription d = DefinitionDescription.Parse("{((phi,psi),X) | phi is formula, psi is cnf, X is set}");
        Assert.Equal(PartKind.Formula, d["phi"].Kind);
        Assert.Equal(PartKind.Cnf, d["psi"].Kind);
        Assert.Equal(PartKind.Set, d["X"].Kind);
        Assert.Null(d["phi"].Of);
    }

    [Fact]
    public void InFormulaIsStillMembership()
    {
        // Only "is" makes formula a type; "in formula" names a part called formula, as before.
        DefinitionDescription d = DefinitionDescription.Parse("{(x,formula) | x in formula, formula is set}");
        Assert.Equal(PartKind.Member, d["x"].Kind);
        Assert.Equal("formula", d["x"].Of);
    }

    [Fact]
    public void DeeplyNestedFormulaInAnInstanceIsAnError()
    {
        string deep = new string('!', 10_000) + "a";
        SpadeParseException e = Fails("{phi | phi is formula}", deep);
        Assert.Contains($"at most {Formula.MaxDepth} levels", e.Message);
        Assert.Equal("phi", e.Part);
    }
}
