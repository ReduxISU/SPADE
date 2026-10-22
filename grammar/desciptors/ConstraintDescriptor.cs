using SPADE;

namespace SPADE.Grammar.descriptor;

class ConstraintDescriptor
{
    String term;
    String definition;
    SetDescriptor? set;
    String? built_in;
    // The constraint as written in the definition string, e.g. "E subset N unorderedcross N".
    String text;

    public ConstraintDescriptor(String _term, String _definition, SetDescriptor _set, String _text)
    {
        term = _term;
        definition = _definition;
        set = _set;
        text = _text;
    }

    public ConstraintDescriptor(String _term, String _definition, String _built_in, String _text)
    {
        term = _term;
        definition = _definition;
        built_in = _built_in;
        text = _text;
    }

    // Values in error messages are shortened, so a huge instance doesn't make a huge message.
    public static string Describe(UtilCollection collection)
    {
        const int maxLength = 60;
        string s = collection.ToString();
        return s.Length <= maxLength ? s : s.Substring(0, maxLength) + "...";
    }

    // "N is multiset" changes how N is read, so it runs before the other constraints (#1).
    public bool DeclaresMultiset => definition == "is" && built_in == "multiset";

    // The failure belongs to this constraint's name, so a caller can find the part without reading
    // the message (#19). The element is kept in full; the message shortens it.
    private SpadeParseException Failure(string reason, UtilCollection element, (string Value, string Part)? missing = null)
    {
        return new SpadeParseException($"Constraint {text} failed: {reason}")
        {
            Part = term,
            Element = element.ToString(),
            MissingValue = missing?.Value,
            ExpectedIn = missing?.Part,
        };
    }


    public void check(Dictionary<string, UtilCollection> map)
    {
        switch (definition)
        {
            case "is":
                if (built_in != null)
                    switch (built_in)
                    {
                        case "set":
                            if (map[term].IsValue() || map[term].IsOrdered() || map[term].IsMultiset())
                            {
                                throw Failure($"{Describe(map[term])} is not a set", map[term]);
                            }
                            break;
                        case "list":
                            if (map[term].IsUnordered())
                            {
                                throw Failure($"{Describe(map[term])} is not a list", map[term]);
                            }
                            break;
                        case "multiset":
                            if (map[term].IsValue() || map[term].IsOrdered())
                            {
                                throw Failure($"{Describe(map[term])} is not a multiset", map[term]);
                            }
                            map[term] = map[term].AsMultiset();
                            break;
                        case "int":
                            if (!map[term].IsValue() || !int.TryParse(map[term].ToString(), out _))
                            {
                                throw Failure($"{Describe(map[term])} is not an integer", map[term]);
                            }
                            break;
                        case "string":
                            // Any single value is a string, a number-looking one like 5 included.
                            if (!map[term].IsValue())
                            {
                                throw Failure($"{Describe(map[term])} is not a string", map[term]);
                            }
                            break;
                    }
                break;

            case "subset":
                if (set != null)
                {
                    if (map[term].IsValue())
                    {
                        throw Failure($"{Describe(map[term])} is not a set", map[term]);
                    }
                    foreach (UtilCollection item in map[term])
                    {
                        bool member;
                        try
                        {
                            member = set.isMember(item);
                        }
                        catch (SpadeParseException e)
                        {
                            // A constraint inside a nested set failed, such as w is int in
                            // {(e,w) | ...}. The message stays, but the error belongs to this part
                            // and item. A missing piece is dropped if its part is a nested name.
                            bool nested = e.ExpectedIn == null || set.declaredNames.Contains(e.ExpectedIn);
                            throw e.BelongingTo(term, item.ToString(), nested ? null : e.MissingValue, nested ? null : e.ExpectedIn);
                        }
                        if (!member)
                        {
                            throw Failure($"{Describe(item)} is not a member", item, set.FindMissing(item));
                        }
                    }
                }
                break;
        }
    }
}