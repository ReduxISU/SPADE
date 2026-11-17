# Boolean formulas

SPADE reads boolean formulas such as `(x1 | !x2) & x3` or `!(x1 & (x2 | !x3)) | x4` into a tree you can inspect and evaluate. Use them on their own with `Formula.Parse`, or declare a part of a definition string as a formula with `is formula` or `is cnf`.

## Syntax

| Write | Means |
|---|---|
| `x1`, `_tmp`, `Var_2` | a variable: a letter or `_`, then letters, digits and `_` (ASCII only) |
| `!a` | not a |
| `a & b` | a and b |
| `a \| b` | a or b |
| `( ... )` | grouping |

Whitespace between tokens is ignored, including between `!` and its variable.

`!` binds tightest, then `&`, then `|`, so `!a & b | c` means `((!a) & b) | c`, and `a | b & c` means `a | (b & c)`. Parentheses nest freely, and `!` can be applied to anything, including another `!` (`!!a`) or a parenthesized formula (`!(a | b)`).

There are no constants (`true`, `false`) and no `->` or `<->`. A word like `true` is a variable name. `->` and `<->` can be added later without changing what any formula means today.

### Why `a | b & c` is allowed

Redux's CNF parser rejects `x1 | x2 & x3`, because a reader might take it as `(x1 | x2) & x3`. SPADE's general formulas accept it and read it with the standard precedence, as `x1 | (x2 & x3)`, for three reasons:

- The general-formula SAT problem planned for Redux (ReduxISU/Redux#856) asks for exactly this precedence, with `a | b & c` reading as `a | (b & c)`.
- It is the precedence of C, C#, Java and most logic textbooks, so anyone writing a formula by hand expects it.
- It never changes the meaning of a formula Redux accepts. Redux rejects every formula whose meaning depends on it, and `is cnf` and `Formula.ParseCnf` keep rejecting them.

`ToString()` always writes parentheses around an `&` inside an `|`, as in `x1 | (x2 & x3)`, so SPADE's own output never relies on precedence between `&` and `|`.

## CNF

`Formula.ParseCnf(text)` and `is cnf` accept only conjunctive normal form, with the same rules as Redux's CNF parser (`Interfaces/Logic/CnfParser.cs` in ReduxISU/Redux):

- clauses joined by `&`; each clause is a literal or literals joined by `|`, parenthesized or not;
- a literal is a variable or `!` and a variable;
- when there is more than one clause, a clause of several literals must be parenthesized, so `x1 | x2 & x3` and `x1 & x2 | x3` are errors;
- no `!!`, no `!( ... )`, no nested parentheses `((a | b))`, no `&` inside a clause's parentheses, no `|` between parenthesized clauses `(a) | (b)`.

Every formula Redux's CNF parser accepts, `ParseCnf` accepts and reads to the same clauses, and every formula it rejects, `ParseCnf` rejects at the same place. Redux could switch to SPADE's parser without users changing any formula they type. `Parse` reads every formula `ParseCnf` accepts to an equal tree.

A formula that `Parse` reads can be in CNF without `ParseCnf` accepting its spelling: `((a | b))` is a clause, so `Formula.Parse("((a | b))").IsCnf` is true. `IsCnf` is about the formula, `ParseCnf` about how it is written.

## The tree

`Formula` is abstract. Each node is one of:

- `Formula.Variable`, with a `Name`;
- `Formula.Not`, with an `Operand`;
- `Formula.And` and `Formula.Or`, with two or more `Operands`, in order. Both derive from `Formula.Junction`.

A chain of one operator is one node: `a & b & c`, `(a & b) & c` and `a & (b & c)` are all an `And` of three operands, and an `And` never has an `And` operand (the same for `Or`). Redundant parentheses leave no trace. Nothing else is simplified: `!!a` stays a double negation, and `a & a` keeps both operands.

```c#
Formula f = Formula.Parse("!(x1 & (x2 | !x3)) | x4");

string text = f switch
{
    Formula.Or o => $"or of {o.Operands.Count}",
    Formula.And a => $"and of {a.Operands.Count}",
    Formula.Not n => $"not of {n.Operand}",
    Formula.Variable v => v.Name,
    _ => "",
};
```

Members:

- `Variables`: the distinct variable names, in order of first appearance from the left. For `(b | !a) & (a | c)` it is `b, a, c`.
- `Evaluate(assignment)`: the formula's value under an `IReadOnlyDictionary<string, bool>`. Every variable is looked up, so a variable missing from the assignment always throws `ArgumentException`, even when its value couldn't change the result.
- `IsCnf`: whether the formula is an `And` of clauses, or a single clause, where a clause is a literal or an `Or` of literals.
- `ToClauses()`: a CNF formula's clauses, each a list of `Formula.Literal` (`Variable`, `Negated`). Throws `InvalidOperationException` when `IsCnf` is false.
- `IsLiteral`: a variable or the negation of one.
- `Depth`: 1 for a variable, one more for each operator above it.
- `ToString()`: the canonical text, with single spaces around `&` and `|`, and parentheses only where needed, plus around an `&` inside an `|`. `Parse` reads it back to an equal formula, and so does `ParseCnf` when `IsCnf` is true. `(x1|!x2)&x3` prints as `(x1 | !x2) & x3`. Unlike Redux's `CnfFormula`, a one-literal clause isn't parenthesized: Redux prints `(x1 | !x2) & (x3)`.
- `Equals`, `GetHashCode`, `==` and `!=`: by structure. `a & b` and `(a) & (b)` are equal; `a & b` and `b & a` are not.

Formulas are immutable. They can also be built in code: `new Formula.Or(new Formula.Not(a), new Formula.And(a, b))`. The constructors check what the parser checks: a valid variable name, at least two operands, and the depth limit, throwing `ArgumentException`.

## Formulas in a definition string

```c#
StringParser p = new("{(phi,X) | phi is cnf, X is set}");
p.parse("(\"(x1 | !x2) & x3\", {x1,x2,x3})");
Formula phi = Formula.Parse(p["phi"]);
```

`is formula` and `is cnf` check that the part is a single value and that it reads as a formula (or as CNF). Read the value with `Formula.Parse(part)`; for a part that passed `is cnf`, `Formula.Parse` and `Formula.ParseCnf` give the same formula.

In an instance, a formula containing parentheses or commas must be in double quotes, as any value with them must be ([quoted values](InstanceString.md#quoted-values)): `"(x1 | !x2) & x3"`. A formula without them, like `a & !b | c`, can be written as it is. Unquoted, `(x1 | x2)` reads as a list, and the check fails with a reminder to quote it.

A definition string can't yet say that every variable of the formula belongs to another part, such as X above. Check that with `Variables`.

## Errors

A formula that can't be read throws `SpadeParseException`. Its `Position` counts from 1 in the formula's text, and the message says so:

```
expected '|' or ')', found end of input (at position 9 of the formula)
```

`Expected` and `Found` are set for "expected ..., found ..." errors. Other errors, such as `double negation is not a CNF literal (at position 2 of the formula)`, have only `Position`. Redux's `CnfParseException` counts positions from 0, so Redux's position p is SPADE's p + 1.

When an `is formula` or `is cnf` check fails, the error names the part, like every failed constraint, and the formula's error is its `InnerException`:

```
Constraint phi is cnf failed: (x1 | x2 is not a CNF formula: expected '|' or ')', found end of input (at position 9 of the formula)
```

Here `Part` is `phi`, `Element` is `(x1 | x2`, and the inner exception's `Position` is 9.

## Depth limit

A formula nests at most `Formula.MaxDepth` (256) levels deep, and the parser allows at most 256 nested parentheses and `!`. Deeper input is a `SpadeParseException` ("a formula nests at most 256 levels deep"), never a stack overflow (see #7). Long formulas are fine: 100,000 clauses joined by `&` are only four levels deep.
