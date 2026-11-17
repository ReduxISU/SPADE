namespace SPADE;

// Reads boolean formulas for Formula.Parse and Formula.ParseCnf (#44). Hand-written rather than
// part of the ANTLR grammar, since a formula is a value inside an instance, not part of a definition.
//
// The tokens and the CNF rules match Redux's CnfParser (Interfaces/Logic/CnfParser.cs), so every
// formula Redux accepts reads here to the same structure, and is rejected here when Redux rejects it.
//
// General formulas (Parse), with the usual precedence ! > & > |:
//
//   formula : or EOF
//   or      : and ('|' and)*
//   and     : unary ('&' unary)*
//   unary   : '!' unary | primary
//   primary : IDENT | '(' or ')'
//   IDENT   : [A-Za-z_][A-Za-z0-9_]*
//
// CNF formulas (ParseCnf), Redux's grammar:
//
//   formula : clause ('&' clause)* EOF
//   clause  : '(' literal ('|' literal)* ')'
//           | literal ('|' literal)*
//   literal : '!'? IDENT
//
// plus Redux's rule that when there is more than one clause, a clause of several literals must be
// parenthesized: "x1 | x2 & x3" is an error in CNF, and x1 | (x2 & x3) as a general formula.
//
// Errors are SpadeParseException with Position counting from 1 in the formula's text. Redux's
// CnfParseException counts from 0, so a Redux position p is p + 1 here.
internal sealed class FormulaParser
{
    private enum Kind { Identifier, Not, And, Or, LParen, RParen, End }

    private readonly record struct Token(Kind Kind, string Text, int Position)
    {
        public string Describe() => Kind == Kind.End ? "end of input" : $"'{Text}'";
    }

    private readonly List<Token> tokens;
    private int index;

    private FormulaParser(List<Token> tokens)
    {
        this.tokens = tokens;
    }

    public static Formula Parse(string text, bool cnf)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        FormulaParser parser = new(Tokenize(text));
        return cnf ? parser.ParseCnfFormula() : parser.ParseGeneralFormula();
    }

    // The words a definition string uses for the formula types: "phi is formula", "phi is cnf".
    // They are read as plain words (TERM) by the ANTLR grammar, so adding them needed no regeneration.
    internal static bool IsTypeWord(string word) => word == "formula" || word == "cnf";

    public static bool IsVariableName(string name)
    {
        if (name.Length == 0 || !IsIdentifierStart(name[0])) return false;
        foreach (char c in name)
            if (!IsIdentifierPart(c)) return false;
        return true;
    }

    // ASCII only, as in Redux; char.IsAsciiLetter isn't in .NET 6.
    private static bool IsIdentifierStart(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
    private static bool IsIdentifierPart(char c) => IsIdentifierStart(c) || (c >= '0' && c <= '9');

    // position counts from 0 here, as in the tokens; the exception counts from 1.
    private static SpadeParseException Error(int position, string detail)
    {
        return new SpadeParseException($"{detail} (at position {position + 1} of the formula)", position + 1);
    }

    private static SpadeParseException Expected(Token found, string expected)
    {
        return new SpadeParseException(
            $"expected {expected}, found {found.Describe()} (at position {found.Position + 1} of the formula)",
            found.Position + 1, expected, found.Describe());
    }

    private static SpadeParseException TooDeep(int position) => Error(position, $"a formula nests at most {Formula.MaxDepth} levels deep");

    private static List<Token> Tokenize(string text)
    {
        List<Token> list = new();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            Kind? symbol = c switch
            {
                '!' => Kind.Not,
                '&' => Kind.And,
                '|' => Kind.Or,
                '(' => Kind.LParen,
                ')' => Kind.RParen,
                _ => null,
            };
            if (symbol is Kind kind)
            {
                list.Add(new Token(kind, c.ToString(), i));
                i++;
                continue;
            }
            if (IsIdentifierStart(c))
            {
                int start = i;
                while (i < text.Length && IsIdentifierPart(text[i])) i++;
                list.Add(new Token(Kind.Identifier, text.Substring(start, i - start), start));
                continue;
            }
            if (c >= '0' && c <= '9')
                throw Error(i, $"variable names must start with a letter or '_', found '{c}'");
            string shown = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])
                ? text.Substring(i, 2)
                : c.ToString();
            throw Error(i, $"unexpected character '{shown}'");
        }
        list.Add(new Token(Kind.End, "", text.Length));
        return list;
    }

    private Token Current => tokens[index];

    private bool TryConsume(Kind kind)
    {
        if (Current.Kind != kind) return false;
        index++;
        return true;
    }

    // Builds a node, turning the depth limit's ArgumentException into a parse error at the operator.
    private static Formula Build(Func<Formula> make, int position)
    {
        try
        {
            return make();
        }
        catch (ArgumentException)
        {
            throw TooDeep(position);
        }
    }

    // ---- General formulas ----

    private Formula ParseGeneralFormula()
    {
        if (Current.Kind == Kind.End) throw Error(Current.Position, "formula is empty");
        Formula formula = ParseOr(0);
        if (Current.Kind != Kind.End) throw Expected(Current, "'&', '|' or end of input");
        return formula;
    }

    // nesting counts the ( and ! around this point, so the recursion stays shallow (#7).
    private Formula ParseOr(int nesting)
    {
        List<Formula> operands = new() { ParseAnd(nesting) };
        int position = -1;
        while (Current.Kind == Kind.Or)
        {
            if (position < 0) position = Current.Position;
            index++;
            operands.Add(ParseAnd(nesting));
        }
        return operands.Count == 1 ? operands[0] : Build(() => new Formula.Or(operands), position);
    }

    private Formula ParseAnd(int nesting)
    {
        List<Formula> operands = new() { ParseUnary(nesting) };
        int position = -1;
        while (Current.Kind == Kind.And)
        {
            if (position < 0) position = Current.Position;
            index++;
            operands.Add(ParseUnary(nesting));
        }
        return operands.Count == 1 ? operands[0] : Build(() => new Formula.And(operands), position);
    }

    private Formula ParseUnary(int nesting)
    {
        if (Current.Kind != Kind.Not) return ParsePrimary(nesting);
        int position = Current.Position;
        if (nesting + 1 > Formula.MaxDepth) throw TooDeep(position);
        index++;
        Formula operand = ParseUnary(nesting + 1);
        return Build(() => new Formula.Not(operand), position);
    }

    private Formula ParsePrimary(int nesting)
    {
        Token token = Current;
        switch (token.Kind)
        {
            case Kind.Identifier:
                index++;
                return new Formula.Variable(token.Text);
            case Kind.LParen:
                if (nesting + 1 > Formula.MaxDepth) throw TooDeep(token.Position);
                index++;
                Formula inner = ParseOr(nesting + 1);
                if (!TryConsume(Kind.RParen)) throw Expected(Current, "'&', '|' or ')'");
                return inner;
            default:
                throw Expected(token, "a variable, '!' or '('");
        }
    }

    // ---- CNF formulas, as Redux's CnfParser reads them ----

    private Formula ParseCnfFormula()
    {
        if (Current.Kind == Kind.End) throw Error(Current.Position, "formula is empty");

        List<Formula> clauses = new();
        int? firstBareMultiLiteral = null;
        do
        {
            int start = Current.Position;
            bool parenthesized = Current.Kind == Kind.LParen;
            List<Formula> literals = ParseClause();
            if (!parenthesized && literals.Count > 1) firstBareMultiLiteral ??= start;
            clauses.Add(literals.Count == 1 ? literals[0] : new Formula.Or(literals));
        } while (TryConsume(Kind.And));

        if (Current.Kind == Kind.Or)
            throw Error(Current.Position, "'|' cannot join parenthesized clauses; use '&' between clauses");
        if (Current.Kind != Kind.End) throw Expected(Current, "'&' or end of input");
        if (clauses.Count > 1 && firstBareMultiLiteral is int bare)
            throw Error(bare, "a clause with more than one literal must be parenthesized when the formula has several clauses, e.g. (a | b) & c");

        return clauses.Count == 1 ? clauses[0] : new Formula.And(clauses);
    }

    private List<Formula> ParseClause()
    {
        Token start = Current;
        if (!TryConsume(Kind.LParen)) return ParseDisjunction();

        if (Current.Kind == Kind.RParen) throw Error(start.Position, "empty clause '()'");
        List<Formula> literals = ParseDisjunction();
        if (Current.Kind == Kind.And)
            throw Error(Current.Position, "'&' inside a clause; CNF clauses join literals with '|' only");
        if (!TryConsume(Kind.RParen)) throw Expected(Current, "'|' or ')'");
        return literals;
    }

    private List<Formula> ParseDisjunction()
    {
        List<Formula> literals = new() { ParseLiteral() };
        while (TryConsume(Kind.Or)) literals.Add(ParseLiteral());
        return literals;
    }

    private Formula ParseLiteral()
    {
        bool negated = TryConsume(Kind.Not);
        Token token = Current;
        switch (token.Kind)
        {
            case Kind.Identifier:
                index++;
                Formula.Variable variable = new(token.Text);
                return negated ? new Formula.Not(variable) : variable;
            case Kind.Not:
                throw Error(token.Position, "double negation is not a CNF literal");
            case Kind.LParen:
                throw Error(token.Position, negated
                    ? "negating a parenthesized expression is not CNF"
                    : "nested parentheses are not allowed in CNF");
            default:
                throw Expected(token, negated ? "a variable after '!'" : "a literal");
        }
    }
}
