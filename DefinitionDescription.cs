using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

using SPADE.Grammar;

namespace SPADE;

/// <summary>
/// What a definition string's constraints say a named part is.
/// </summary>
public enum PartKind
{
    /// <summary>No constraint says what the part is.</summary>
    Any,

    /// <summary>"is set".</summary>
    Set,

    /// <summary>"is multiset".</summary>
    Multiset,

    /// <summary>"is list".</summary>
    List,

    /// <summary>"is int".</summary>
    Int,

    /// <summary>"is string": a single value, not a set, list or multiset.</summary>
    String,

    /// <summary>"subset X", with no "is" type; <see cref="DefinitionPart.Of"/> holds X.</summary>
    Subset,

    /// <summary>"is X" or "in X" where X is another part or a set expression; <see cref="DefinitionPart.Of"/> holds X.</summary>
    Member,
}

/// <summary>
/// One named part of a definition string's format, such as N or E in "{(N,E) | ...}".
/// </summary>
public class DefinitionPart
{
    /// <summary>The name as written in the format, e.g. "E".</summary>
    public string Name { get; }

    /// <summary>What the part's constraints say it is. An "is set", "is multiset", "is list", "is int" or "is string" constraint wins over "subset" or "in".</summary>
    public PartKind Kind { get; }

    /// <summary>
    /// The object the part is a subset or member of, as written, e.g. "N unorderedcross N" for "E subset N unorderedcross N".
    /// Taken from the first "subset" constraint, else the first "is X" or "in X" constraint; null when there is neither.
    /// </summary>
    public string? Of { get; }

    /// <summary>Child indices from the outline's root to this part, e.g. [0,1] for E in "((N,E),K)". Empty when the outline is a single name.</summary>
    public IReadOnlyList<int> Path { get; }

    /// <summary>Every top-level constraint that names this part, as written, in definition order.</summary>
    public IReadOnlyList<string> Constraints { get; }

    internal DefinitionPart(string name, PartKind kind, string? of, IReadOnlyList<int> path, IReadOnlyList<string> constraints)
    {
        Name = name;
        Kind = kind;
        Of = of;
        Path = path;
        Constraints = constraints;
    }

    public override string ToString()
    {
        return Of == null ? $"{Name}: {Kind}" : $"{Name}: {Kind} of {Of}";
    }
}

/// <summary>
/// The names and types a definition string declares, read without parsing an instance.
/// Only the outermost set's format and constraints describe parts; sets nested inside a constraint are part of that constraint's text.
/// </summary>
public class DefinitionDescription
{
    /// <summary>The format with whitespace removed, e.g. "((N,E),K)".</summary>
    public string Outline { get; }

    /// <summary>The named parts, in the order they appear in <see cref="Outline"/>.</summary>
    public IReadOnlyList<DefinitionPart> Parts { get; }

    private readonly Dictionary<string, DefinitionPart> byName;

    private DefinitionDescription(string outline, List<DefinitionPart> parts)
    {
        Outline = outline;
        Parts = parts.AsReadOnly();
        byName = new();
        foreach (DefinitionPart part in parts)
            byName.TryAdd(part.Name, part);
    }

    /// <summary>The part with this name. Throws KeyNotFoundException when the outline has no such name.</summary>
    public DefinitionPart this[string name]
    {
        get
        {
            if (byName.TryGetValue(name, out DefinitionPart? part)) return part;
            throw new KeyNotFoundException($"The definition {Outline} has no part named {name}");
        }
    }

    /// <summary>Whether the outline has a part with this name.</summary>
    public bool Contains(string name)
    {
        return byName.ContainsKey(name);
    }

    /// <summary>Looks up a part by name without throwing.</summary>
    public bool TryGetPart(string name, out DefinitionPart? part)
    {
        return byName.TryGetValue(name, out part);
    }

    /// <summary>
    /// Reads a definition string such as "{(N,E) | N is set, E subset N unorderedcross N}".
    /// Throws FormatException naming the definition string when it can't be read.
    /// </summary>
    public static DefinitionDescription Parse(string definition)
    {
        collParser.SetContext set = ThrowingErrorListener.Parse(definition).set();
        collParser.FormatContext? format = set.format();
        if (format == null)
            throw new FormatException($"Definition string \"{definition}\" is a list of values, not a format followed by |");

        // Walk the outline once for its text and the path to every name.
        List<(string name, List<int> path)> names = new();
        string outline = WalkOutline(format, new List<int>(), names);
        HashSet<string> nameSet = names.Select(n => n.name).ToHashSet();

        Dictionary<string, List<string>> constraints = new();
        Dictionary<string, List<collParser.ConstraintContext>> simple = new();
        foreach (string name in nameSet)
        {
            constraints[name] = new();
            simple[name] = new();
        }

        foreach (collParser.ConstraintContext constraint in set.constraint())
        {
            string text = SourceText(constraint);
            if (constraint.TERM() != null)
            {
                string subject = constraint.TERM().GetText();
                if (!nameSet.Contains(subject)) continue;
                constraints[subject].Add(text);
                simple[subject].Add(constraint);
                continue;
            }

            // and / or / not: listed under every part it constrains, but says nothing about kind.
            HashSet<string> subjects = new();
            Subjects(constraint, subjects);
            foreach (string subject in subjects.Where(nameSet.Contains))
                constraints[subject].Add(text);
        }

        List<DefinitionPart> parts = new();
        HashSet<string> seen = new();
        foreach ((string name, List<int> path) in names)
        {
            if (!seen.Add(name)) continue;
            (PartKind kind, string? of) = Classify(simple[name], nameSet);
            parts.Add(new DefinitionPart(name, kind, of, path.AsReadOnly(), constraints[name].AsReadOnly()));
        }

        return new DefinitionDescription(outline, parts);
    }

    // The format's text without whitespace, recording the child-index path to each name.
    private static string WalkOutline(collParser.FormatContext format, List<int> path, List<(string, List<int>)> names)
    {
        if (format.TERM() != null)
        {
            string name = format.TERM().GetText();
            names.Add((name, new List<int>(path)));
            return name;
        }

        collParser.FormatContext[] children = format.format();
        List<string> texts = new();
        for (int i = 0; i < children.Length; i++)
        {
            path.Add(i);
            texts.Add(WalkOutline(children[i], path, names));
            path.RemoveAt(path.Count - 1);
        }
        string open = format.Start.Text;
        string close = open == "{" ? "}" : ")";
        return open + string.Join(",", texts) + close;
    }

    // The names a compound constraint constrains: the subject of each simple constraint inside it.
    private static void Subjects(collParser.ConstraintContext constraint, HashSet<string> subjects)
    {
        if (constraint.TERM() != null)
        {
            subjects.Add(constraint.TERM().GetText());
            return;
        }
        foreach (collParser.ConstraintContext inner in constraint.constraint())
            Subjects(inner, subjects);
    }

    // An "is set/multiset/list/int/string" wins the kind; Of comes from the first subset, else the first membership.
    private static (PartKind, string?) Classify(List<collParser.ConstraintContext> constraints, HashSet<string> names)
    {
        PartKind? type = null;
        string? subsetOf = null;
        string? memberOf = null;

        foreach (collParser.ConstraintContext constraint in constraints)
        {
            string definition = constraint.DEFINITION().GetText();
            collParser.ObjectContext obj = constraint.@object();
            string objText = SourceText(obj);

            if (definition == "subset")
            {
                subsetOf ??= objText;
                continue;
            }

            if (definition == "is")
            {
                if (obj.BUILT_IN() != null)
                {
                    PartKind? builtIn = objText switch
                    {
                        "set" => PartKind.Set,
                        "multiset" => PartKind.Multiset,
                        "list" => PartKind.List,
                        "string" => PartKind.String,
                        _ => null,
                    };
                    type ??= builtIn;
                    continue;
                }
                if (obj.BUILT_IN_SET() != null)
                {
                    type ??= PartKind.Int;
                    continue;
                }
            }

            // "is X" / "in X": a member of another part or of a set expression. A bare word that names
            // no part (e.g. "is text") isn't something SPADE knows, so it says nothing.
            if (obj.TERM() != null && !names.Contains(objText)) continue;
            memberOf ??= objText;
        }

        string? of = subsetOf ?? memberOf;
        if (type != null) return (type.Value, of);
        if (subsetOf != null) return (PartKind.Subset, subsetOf);
        if (memberOf != null) return (PartKind.Member, memberOf);
        return (PartKind.Any, null);
    }

    // A rule's text as written in the definition string, spacing included.
    private static string SourceText(ParserRuleContext context)
    {
        return context.Start.InputStream.GetText(Interval.Of(context.Start.StartIndex, context.Stop.StopIndex));
    }
}
