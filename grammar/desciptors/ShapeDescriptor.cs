using SPADE;
using SPADE.Grammar.Enums;

namespace SPADE.Grammar.descriptor;

/// <summary>
/// What "list int size m", "matrix int size m n" or "tensor 3 number" declares (#30): an element type,
/// a rank and, optionally, one size per dimension, each a name or a whole-number constant.
/// </summary>
class ShapeDescriptor
{
    /// <summary>"list", "matrix" or "tensor", as written.</summary>
    public string Head { get; }

    public ScalarType ElementType { get; }

    public int Rank { get; }

    /// <summary>One size per dimension as written, a name or a constant; empty when the sizes are free.</summary>
    public IReadOnlyList<string> Sizes { get; }

    private ShapeDescriptor(string head, ScalarType elementType, int rank, IReadOnlyList<string> sizes)
    {
        Head = head;
        ElementType = elementType;
        Rank = rank;
        Sizes = sizes;
    }

    /// <summary>
    /// Reads a shaped type from its parse tree. Throws FormatException naming the definition string when it
    /// is something SPADE can't check, such as "set int", "tensor 0 int" or "matrix int size m".
    /// </summary>
    public static ShapeDescriptor Read(collParser.ShapedContext context, string definition)
    {
        string text = SourceText(context);
        FormatException Fail(string reason) => new($"Definition string \"{definition}\" can't be read: {reason} in \"{text}\"");

        string head;
        int rank;
        if (context.MATRIX() != null)
        {
            head = "matrix";
            rank = 2;
        }
        else if (context.TENSOR() != null)
        {
            head = "tensor";
            string rankText = context.TERM().GetText();
            if (!IsWholeNumber(rankText) || !int.TryParse(rankText, out rank) || rank < 1 || rank > InstanceScanner.MaxDepth)
                throw Fail($"a tensor's number of dimensions must be a whole number from 1 to {InstanceScanner.MaxDepth}, not {rankText}");
        }
        else
        {
            head = context.BUILT_IN().GetText();
            if (head != "list") throw Fail($"only list, matrix and tensor take an element type, not {head}");
            rank = 1;
        }

        string elementText = context.scalar().GetText();
        ScalarType elementType = elementText switch
        {
            "int" => ScalarType.Int,
            "number" => ScalarType.Number,
            "string" => ScalarType.String,
            _ => throw Fail($"the element type must be int, number or string, not {elementText}"),
        };

        List<string> sizes = new();
        if (context.sizes() != null)
        {
            sizes = context.sizes().TERM().Select(t => t.GetText()).ToList();
            if (sizes.Count != rank)
                throw Fail($"a {Describe(head, rank)} needs {rank} {(rank == 1 ? "size" : "sizes")}, found {sizes.Count}");
            foreach (string size in sizes)
            {
                // A size starting with a digit or . is a constant, so it must be a whole number.
                if ((char.IsDigit(size[0]) || size[0] == '.') && (!IsWholeNumber(size) || !int.TryParse(size, out _)))
                    throw Fail($"a size must be a name or a whole number, not {size}");
            }
        }

        if (context.Parent is not collParser.ObjectContext obj
            || obj.Parent is not collParser.ConstraintContext constraint
            || constraint.DEFINITION().GetText() != "is")
            throw Fail($"a {head} with an element type can only follow \"is\", as in \"C is {text}\"");

        return new ShapeDescriptor(head, elementType, rank, sizes.AsReadOnly());
    }

    private static string Describe(string head, int rank)
    {
        return head == "tensor" ? $"tensor {rank}" : head;
    }

    private static bool IsWholeNumber(string text)
    {
        return text.Length > 0 && text.All(c => c >= '0' && c <= '9');
    }

    private static string SourceText(Antlr4.Runtime.ParserRuleContext context)
    {
        return context.Start.InputStream.GetText(Antlr4.Runtime.Misc.Interval.Of(context.Start.StartIndex, context.Stop.StopIndex));
    }

    /// <summary>
    /// Checks a value against this shape and returns it as a tensor. Every list at the same depth must be the
    /// same length; each size must match its constant, or the value its name already has in
    /// <paramref name="sizes"/>, which a free name is then given. Throws through <paramref name="fail"/>, which
    /// turns a reason and the failing element into the constraint's error.
    /// </summary>
    public Tensor Check(string term, UtilCollection value, SizeBindings sizes, Func<string, UtilCollection, SpadeParseException> fail)
    {
        int[] shape = new int[Rank];
        bool[] known = new bool[Rank];
        // The first list met at each depth, for naming it in a size error.
        List<int>[] firstPath = new List<int>[Rank];
        UtilCollection[] firstNode = new UtilCollection[Rank];
        List<string> values = new();

        void Walk(UtilCollection node, int depth, List<int> path)
        {
            if (depth == Rank)
            {
                if (!node.IsValue() || !Tensor.IsElement(ElementType, node.ToString()))
                    throw fail($"{ElementName(term, path)} is not {Tensor.Article(ElementType)}", node);
                values.Add(node.ToString());
                return;
            }

            if (!node.IsOrdered())
                throw fail($"{RowName(term, path)} is not a list", node);

            int count = node.Count();
            if (!known[depth])
            {
                known[depth] = true;
                shape[depth] = count;
                firstPath[depth] = new List<int>(path);
                firstNode[depth] = node;
            }
            else if (count != shape[depth])
            {
                throw fail($"{RowName(term, path)} has {Count(count, depth)}, expected {shape[depth]}", node);
            }

            for (int i = 0; i < count; i++)
            {
                path.Add(i + 1);
                Walk(node[i], depth + 1, path);
                path.RemoveAt(path.Count - 1);
            }
        }

        Walk(value, 0, new List<int>());

        for (int d = 0; d < Sizes.Count; d++)
        {
            // A dimension after an empty one is unknown, so its size isn't checked.
            if (!known[d]) continue;
            string where = RowName(term, firstPath[d]);
            string size = Sizes[d];
            if (char.IsDigit(size[0]))
            {
                int expected = int.Parse(size);
                if (shape[d] != expected)
                    throw fail($"{where} has {Count(shape[d], d)}, expected {expected}", firstNode[d]);
                continue;
            }

            (int Value, string From)? bound = sizes.Lookup(size, fail, term, firstNode[d]);
            if (bound == null)
            {
                sizes.Bind(size, shape[d], term);
            }
            else if (bound.Value.Value != shape[d])
            {
                throw fail($"{where} has {Count(shape[d], d)} but {size} is {bound.Value.Value} (from {bound.Value.From})", firstNode[d]);
            }
        }

        // Dimensions after an empty one are unknown, and reported as 0.
        return new Tensor(ElementType, shape, values);
    }

    // "3 rows" or "1 value": a list at the last depth holds values, one above it holds rows.
    private string Count(int count, int depth)
    {
        string noun = depth == Rank - 1 ? "value" : "row";
        return $"{count} {noun}{(count == 1 ? "" : "s")}";
    }

    // "C", "row 3 of C", or "row (2,1) of T" for a list inside a tensor, counting from 1.
    private static string RowName(string term, List<int> path)
    {
        if (path.Count == 0) return term;
        return $"row {Position(path)} of {term}";
    }

    // "element 2 of d" or "element (2,1) of C", counting from 1.
    private static string ElementName(string term, List<int> path)
    {
        if (path.Count == 0) return term;
        return $"element {Position(path)} of {term}";
    }

    private static string Position(List<int> path)
    {
        return path.Count == 1 ? path[0].ToString() : $"({string.Join(",", path)})";
    }
}

/// <summary>
/// The values size names have taken so far in one instance of a set definition, and which part each came
/// from, so "C is matrix int size m n, d is list int size m" checks that d is as long as C has rows (#30).
/// A size named after a part of the format, such as n in "{(G,n) | G is matrix int size n n, n is int}",
/// takes that part's value.
/// </summary>
class SizeBindings
{
    private readonly Dictionary<string, UtilCollection> map;
    private readonly ICollection<string> parts;
    private readonly Dictionary<string, (int Value, string From)> bound = new();

    public SizeBindings(Dictionary<string, UtilCollection> map, ICollection<string> parts)
    {
        this.map = map;
        this.parts = parts;
    }

    /// <summary>The value a size name has, and the part it came from, or null when it has none yet.</summary>
    public (int Value, string From)? Lookup(string name, Func<string, UtilCollection, SpadeParseException> fail, string term, UtilCollection element)
    {
        if (bound.TryGetValue(name, out (int Value, string From) entry)) return entry;
        if (!parts.Contains(name) || !map.TryGetValue(name, out UtilCollection? part)) return null;

        if (!part.IsValue() || !IntValue.TryParse(part.ToString(), out int value) || value < 0)
            throw fail($"size {name} of {term} is the part {name}, which is {ConstraintDescriptor.Describe(part)}, not a whole number", element);
        bound[name] = (value, name);
        return bound[name];
    }

    public void Bind(string name, int value, string from)
    {
        bound[name] = (value, from);
    }
}
