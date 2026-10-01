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

    private SpadeParseException Failure(string reason)
    {
        return new SpadeParseException($"Constraint {text} failed: {reason}");
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
                                throw Failure($"{Describe(map[term])} is not a set");
                            }
                            break;
                        case "list":
                            if (map[term].IsUnordered())
                            {
                                throw Failure($"{Describe(map[term])} is not a list");
                            }
                            break;
                        case "multiset":
                            if (map[term].IsValue() || map[term].IsOrdered())
                            {
                                throw Failure($"{Describe(map[term])} is not a multiset");
                            }
                            map[term] = map[term].AsMultiset();
                            break;
                        case "int":
                            if (!map[term].IsValue() || !int.TryParse(map[term].ToString(), out _))
                            {
                                throw Failure($"{Describe(map[term])} is not an integer");
                            }
                            break;
                    }
                break;

            case "subset":
                if (set != null)
                {
                        if (map[term].IsValue())
                        {
                            throw Failure($"{Describe(map[term])} is not a set");
                        }
                        foreach (UtilCollection item in map[term])
                        {
                            if (!set.isMember(item))
                            {
                                throw Failure($"{Describe(item)} is not a member");
                            }
                        }
                }
                break;
        }
    }
}