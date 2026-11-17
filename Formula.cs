using System.Text;

namespace SPADE;

/// <summary>
/// A boolean formula over named variables, such as <c>(x1 | !x2) &amp; x3</c> (#44).
/// A formula is a <see cref="Variable"/>, a <see cref="Not"/>, an <see cref="And"/> or an <see cref="Or"/>.
/// </summary>
/// <remarks>
/// <para>
/// Read one with <see cref="Parse(string)"/>, or with <see cref="ParseCnf(string)"/> to also require
/// conjunctive normal form. <c>!</c> binds tightest, then <c>&amp;</c>, then <c>|</c>; parentheses group.
/// A variable name is a letter or <c>_</c> followed by letters, digits and <c>_</c> (ASCII only).
/// Whitespace between tokens is ignored.
/// </para>
/// <para>
/// Formulas are immutable and compared by structure. A chain of the same operator is one node, so
/// <c>a &amp; b &amp; c</c> and <c>(a &amp; b) &amp; c</c> are both an <see cref="And"/> of three
/// operands; operand order is kept. <c>!!a</c> stays a double negation.
/// </para>
/// <para>
/// A formula nests at most <see cref="MaxDepth"/> levels deep, so no operation on it can overflow the stack (#7).
/// </para>
/// </remarks>
public abstract class Formula : IEquatable<Formula>
{
    /// <summary>
    /// The deepest a formula may nest: a variable is 1 level, and each <c>!</c>, <c>&amp;</c> or <c>|</c>
    /// above it adds one. Parsing also allows at most this many nested parentheses and <c>!</c>.
    /// </summary>
    public const int MaxDepth = 256;

    /// <summary>A variable or the negation of one, as in a CNF clause.</summary>
    public readonly record struct Literal(string Variable, bool Negated)
    {
        /// <summary><c>x</c> or <c>!x</c>.</summary>
        public override string ToString() => Negated ? "!" + Variable : Variable;
    }

    // Only the four node types below derive from Formula.
    private protected Formula(int depth, int hash)
    {
        Depth = depth;
        hashCode = hash;
    }

    private readonly int hashCode;
    private IReadOnlyList<string>? variables;

    /// <summary>How many levels deep the formula nests: 1 for a variable.</summary>
    public int Depth { get; }

    /// <summary>
    /// Reads a boolean formula such as <c>!(x1 &amp; (x2 | !x3)) | x4</c>. <c>a | b &amp; c</c> reads as
    /// <c>a | (b &amp; c)</c>. Every formula <see cref="ParseCnf(string)"/> accepts reads here to the same formula.
    /// </summary>
    /// <exception cref="SpadeParseException">The text is not a formula. <see cref="SpadeParseException.Position"/>
    /// is where in the text, counting from 1.</exception>
    public static Formula Parse(string text) => FormulaParser.Parse(text, cnf: false);

    /// <summary>
    /// Reads a formula in conjunctive normal form, such as <c>(x1 | !x2) &amp; x3</c>, with the same rules as
    /// Redux's CNF parser: clauses joined by <c>&amp;</c>, each a literal or literals joined by <c>|</c>,
    /// parenthesized or not; a literal is a variable or <c>!</c> and a variable. When there is more than one clause,
    /// a clause of several literals must be parenthesized, so <c>x1 | x2 &amp; x3</c> is an error here.
    /// The result is the same formula <see cref="Parse(string)"/> gives, and <see cref="IsCnf"/> is true.
    /// </summary>
    /// <exception cref="SpadeParseException">The text is not a CNF formula.</exception>
    public static Formula ParseCnf(string text) => FormulaParser.Parse(text, cnf: true);

    /// <summary>Reads a part of a parsed instance, such as <c>parser["phi"]</c>, as a formula.</summary>
    /// <exception cref="SpadeParseException">The part is a set or list, or its value is not a formula.</exception>
    public static Formula Parse(UtilCollection value) => Parse(ValueText(value));

    /// <summary>Reads a part of a parsed instance as a CNF formula.</summary>
    /// <exception cref="SpadeParseException">The part is a set or list, or its value is not a CNF formula.</exception>
    public static Formula ParseCnf(UtilCollection value) => ParseCnf(ValueText(value));

    private static string ValueText(UtilCollection value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (!value.IsValue())
            throw new SpadeParseException($"{value} is a set or list, not a formula. A formula with ( ) or , in it is written in double quotes in an instance, like \"(x1 | x2) & x3\"");
        return value.ToString();
    }

    /// <summary>The distinct variable names, in order of first appearance from the left.</summary>
    public IReadOnlyList<string> Variables
    {
        get
        {
            if (variables != null) return variables;
            List<string> list = new();
            HashSet<string> seen = new();
            CollectVariables(list, seen);
            // Immutable, so a race only computes the same list twice.
            variables = list.AsReadOnly();
            return variables;
        }
    }

    private protected abstract void CollectVariables(List<string> list, HashSet<string> seen);

    /// <summary>
    /// The formula's value when each variable has the value the assignment gives it. Every variable is looked up,
    /// even ones whose value can't change the result.
    /// </summary>
    /// <exception cref="ArgumentException">The assignment has no value for a variable of the formula.</exception>
    public bool Evaluate(IReadOnlyDictionary<string, bool> assignment)
    {
        if (assignment == null) throw new ArgumentNullException(nameof(assignment));
        return Eval(assignment);
    }

    private protected abstract bool Eval(IReadOnlyDictionary<string, bool> assignment);

    /// <summary>Whether this is a literal: a variable, or <c>!</c> applied to a variable.</summary>
    public bool IsLiteral => this is Variable || (this is Not n && n.Operand is Variable);

    /// <summary>
    /// Whether the formula is in conjunctive normal form: an <see cref="And"/> of clauses, or a single clause,
    /// where a clause is a literal or an <see cref="Or"/> of literals.
    /// </summary>
    public bool IsCnf => this is And conj ? conj.Operands.All(IsClause) : IsClause(this);

    private static bool IsClause(Formula f) => f.IsLiteral || (f is Or disj && disj.Operands.All(o => o.IsLiteral));

    /// <summary>
    /// The clauses of a CNF formula, each a list of literals, in order; e.g. [[x1, !x2], [x3]] for
    /// <c>(x1 | !x2) &amp; x3</c>. Repeated literals are kept.
    /// </summary>
    /// <exception cref="InvalidOperationException">The formula is not in CNF (<see cref="IsCnf"/> is false).</exception>
    public IReadOnlyList<IReadOnlyList<Literal>> ToClauses()
    {
        if (!IsCnf) throw new InvalidOperationException($"{this} is not in conjunctive normal form");
        IEnumerable<Formula> clauses = this is And conj ? conj.Operands : new[] { this };
        return clauses.Select(ClauseLiterals).ToList().AsReadOnly();
    }

    private static IReadOnlyList<Literal> ClauseLiterals(Formula clause)
    {
        IEnumerable<Formula> literals = clause is Or disj ? disj.Operands : new[] { clause };
        return literals.Select(l => l is Not n
            ? new Literal(((Variable)n.Operand).Name, true)
            : new Literal(((Variable)l).Name, false)).ToList().AsReadOnly();
    }

    /// <summary>
    /// The canonical text: single spaces around <c>&amp;</c> and <c>|</c>, and parentheses only where needed,
    /// except that an operand of <c>|</c> that is an <c>&amp;</c> is parenthesized too, as in <c>a | (b &amp; c)</c>.
    /// <see cref="Parse(string)"/> reads it back to an equal formula, and so does <see cref="ParseCnf(string)"/>
    /// when <see cref="IsCnf"/> is true.
    /// </summary>
    public override string ToString()
    {
        StringBuilder sb = new();
        Write(sb);
        return sb.ToString();
    }

    private protected abstract void Write(StringBuilder sb);

    // An operand of an operator, parenthesized unless it is a literal-like node that can't be misread.
    private protected static void WriteOperand(StringBuilder sb, Formula operand)
    {
        if (operand is Variable || operand is Not)
        {
            operand.Write(sb);
            return;
        }
        sb.Append('(');
        operand.Write(sb);
        sb.Append(')');
    }

    /// <inheritdoc/>
    public abstract bool Equals(Formula? other);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Formula f && Equals(f);

    /// <inheritdoc/>
    public override int GetHashCode() => hashCode;

    /// <summary>Whether two formulas have the same structure.</summary>
    public static bool operator ==(Formula? left, Formula? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Whether two formulas differ in structure.</summary>
    public static bool operator !=(Formula? left, Formula? right) => !(left == right);

    private static int CheckDepth(int depth)
    {
        if (depth > MaxDepth)
            throw new ArgumentException($"a formula nests at most {MaxDepth} levels deep");
        return depth;
    }

    /// <summary>A variable, such as <c>x1</c>.</summary>
    public sealed class Variable : Formula
    {
        /// <summary>The variable's name.</summary>
        public string Name { get; }

        /// <summary>A variable. The name is a letter or <c>_</c> followed by letters, digits and <c>_</c>.</summary>
        /// <exception cref="ArgumentException">The name is not a valid variable name.</exception>
        public Variable(string name)
            : base(1, HashCode.Combine(1, name))
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (!FormulaParser.IsVariableName(name))
                throw new ArgumentException($"'{name}' is not a variable name: use a letter or _ followed by letters, digits and _", nameof(name));
            Name = name;
        }

        private protected override void CollectVariables(List<string> list, HashSet<string> seen)
        {
            if (seen.Add(Name)) list.Add(Name);
        }

        private protected override bool Eval(IReadOnlyDictionary<string, bool> assignment)
        {
            if (assignment.TryGetValue(Name, out bool value)) return value;
            throw new ArgumentException($"the assignment has no value for {Name}", nameof(assignment));
        }

        private protected override void Write(StringBuilder sb) => sb.Append(Name);

        /// <inheritdoc/>
        public override bool Equals(Formula? other) => other is Variable v && v.Name == Name;
    }

    /// <summary>The negation of a formula, <c>!a</c>.</summary>
    public sealed class Not : Formula
    {
        /// <summary>The negated formula.</summary>
        public Formula Operand { get; }

        /// <summary>The negation of a formula.</summary>
        /// <exception cref="ArgumentException">The result would nest deeper than <see cref="MaxDepth"/>.</exception>
        public Not(Formula operand)
            : base(CheckDepth((operand ?? throw new ArgumentNullException(nameof(operand))).Depth + 1),
                   HashCode.Combine(2, operand))
        {
            Operand = operand;
        }

        private protected override void CollectVariables(List<string> list, HashSet<string> seen) => Operand.CollectVariables(list, seen);

        private protected override bool Eval(IReadOnlyDictionary<string, bool> assignment) => !Operand.Eval(assignment);

        private protected override void Write(StringBuilder sb)
        {
            sb.Append('!');
            WriteOperand(sb, Operand);
        }

        /// <inheritdoc/>
        public override bool Equals(Formula? other) => other is Not n && n.Operand.Equals(Operand);
    }

    /// <summary>An <see cref="And"/> or <see cref="Or"/> of two or more operands.</summary>
    public abstract class Junction : Formula
    {
        /// <summary>The operands, in order. None is of the same type as this node.</summary>
        public IReadOnlyList<Formula> Operands { get; }

        private protected Junction(IReadOnlyList<Formula> operands, int tag)
            : base(CheckDepth(operands.Max(o => o.Depth) + 1), Hash(operands, tag))
        {
            Operands = operands;
        }

        // The operands with any operand of the same type replaced by its own operands.
        private protected static IReadOnlyList<Formula> Flatten<T>(IEnumerable<Formula> operands) where T : Junction
        {
            if (operands == null) throw new ArgumentNullException(nameof(operands));
            List<Formula> list = new();
            foreach (Formula operand in operands)
            {
                if (operand == null) throw new ArgumentException("an operand is null", nameof(operands));
                if (operand is T same) list.AddRange(same.Operands);
                else list.Add(operand);
            }
            if (list.Count < 2) throw new ArgumentException("needs at least two operands", nameof(operands));
            return list.AsReadOnly();
        }

        private static int Hash(IReadOnlyList<Formula> operands, int tag)
        {
            HashCode hash = new();
            hash.Add(tag);
            foreach (Formula operand in operands) hash.Add(operand);
            return hash.ToHashCode();
        }

        private protected override void CollectVariables(List<string> list, HashSet<string> seen)
        {
            foreach (Formula operand in Operands) operand.CollectVariables(list, seen);
        }

        private protected void WriteJoined(StringBuilder sb, string op)
        {
            for (int i = 0; i < Operands.Count; i++)
            {
                if (i > 0) sb.Append(op);
                WriteOperand(sb, Operands[i]);
            }
        }

        private protected bool SameOperands(Junction other)
        {
            if (other.Operands.Count != Operands.Count || other.GetHashCode() != GetHashCode()) return false;
            for (int i = 0; i < Operands.Count; i++)
                if (!Operands[i].Equals(other.Operands[i])) return false;
            return true;
        }
    }

    /// <summary>The conjunction of two or more formulas, <c>a &amp; b &amp; c</c>.</summary>
    public sealed class And : Junction
    {
        /// <summary>The conjunction of the operands. An operand that is itself an <see cref="And"/> is merged in.</summary>
        /// <exception cref="ArgumentException">Fewer than two operands, or the result would nest deeper than <see cref="MaxDepth"/>.</exception>
        public And(IEnumerable<Formula> operands) : base(Flatten<And>(operands), 3) { }

        /// <summary>The conjunction of the operands.</summary>
        public And(params Formula[] operands) : this((IEnumerable<Formula>)operands) { }

        private protected override bool Eval(IReadOnlyDictionary<string, bool> assignment)
        {
            // No short-circuit, so a missing variable is always reported.
            bool result = true;
            foreach (Formula operand in Operands) result &= operand.Eval(assignment);
            return result;
        }

        private protected override void Write(StringBuilder sb) => WriteJoined(sb, " & ");

        /// <inheritdoc/>
        public override bool Equals(Formula? other) => other is And a && SameOperands(a);
    }

    /// <summary>The disjunction of two or more formulas, <c>a | b | c</c>.</summary>
    public sealed class Or : Junction
    {
        /// <summary>The disjunction of the operands. An operand that is itself an <see cref="Or"/> is merged in.</summary>
        /// <exception cref="ArgumentException">Fewer than two operands, or the result would nest deeper than <see cref="MaxDepth"/>.</exception>
        public Or(IEnumerable<Formula> operands) : base(Flatten<Or>(operands), 4) { }

        /// <summary>The disjunction of the operands.</summary>
        public Or(params Formula[] operands) : this((IEnumerable<Formula>)operands) { }

        private protected override bool Eval(IReadOnlyDictionary<string, bool> assignment)
        {
            bool result = false;
            foreach (Formula operand in Operands) result |= operand.Eval(assignment);
            return result;
        }

        private protected override void Write(StringBuilder sb) => WriteJoined(sb, " | ");

        /// <inheritdoc/>
        public override bool Equals(Formula? other) => other is Or o && SameOperands(o);
    }
}
