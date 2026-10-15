using SPADE;

namespace SPADE.Grammar.descriptor;

class ConstraintDescriptor
{
    String term;
    String definition;
    SetDescriptor? set;
    String? built_in;

    public ConstraintDescriptor(String _term, String _definition, SetDescriptor _set)
    {
        term = _term;
        definition = _definition;
        set = _set;
    }

    public ConstraintDescriptor(String _term, String _definition, String _built_in)
    {
        term = _term;
        definition = _definition;
        built_in = _built_in;
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
                            if (map[term].IsValue() || map[term].IsOrdered())
                            {
                                throw new Exception($"Constraint {term} {definition} {built_in} failed because {term} was not a set");
                            }
                            break;
                        case "list":
                            if (map[term].IsUnordered())
                            {
                                throw new Exception($"Constraint {term} {definition} {built_in} failed because {term} was not a list");
                            }
                            break;
                        case "int":
                            if (!map[term].IsValue() || !int.TryParse(map[term].ToString(), out _))
                            {
                                throw new Exception($"Constraint {term} {definition} {built_in} failed because {map[term]} was not an integer");
                            }
                            break;
                    }
                break;

            case "subset":
                if (set != null)
                {
                    foreach (UtilCollection item in map[term])
                    {
                        if (!set.isMember(item))
                        {
                            throw new Exception($"Constraint {term} {definition} PLACEHOLDER failed because {item} was not a subset of PLACEHOLDER");
                        }
                    }
                }
                break;
        }
    }
}