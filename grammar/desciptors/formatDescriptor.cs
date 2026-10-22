using System.Collections;

using SPADE;

namespace SPADE.Grammar.descriptor;

class FormatDescriptor
{
    public Action<Dictionary<string, UtilCollection>, UtilCollection> parseFunc;

    // The names this format declares, e.g. N and E for (N,E).
    public List<string> names = new();

    public FormatDescriptor(String term)
    {
        names.Add(term);
        parseFunc = (map, coll) => { map[term] = coll; };
    }

    public FormatDescriptor(List<FormatDescriptor> lst, String text)
    {
        names = lst.SelectMany(f => f.names).ToList();
        parseFunc = (map, coll) =>
        {
            if (!coll.IsOrdered() || coll.Count() != lst.Count())
            {
                throw new SpadeParseException($"expected a list of {lst.Count()} elements for {text}, found {ConstraintDescriptor.Describe(coll)}");
            }
            for (int i = 0; i < lst.Count(); i++)
            {
                lst[i].parseFunc(map, coll[i]);
            }
        };
    }

}