using System.Collections;

using SPADE;

namespace SPADE.Grammar.descriptor;

class FormatDescriptor
{
    public Action<Dictionary<string, UtilCollection>, UtilCollection> parseFunc;

    public FormatDescriptor(String term)
    {
        parseFunc = (map, coll) => { map[term] = coll; };
    }

    public FormatDescriptor(List<FormatDescriptor> lst)
    {
        parseFunc = (map, coll) =>
        {
            for (int i = 0; i < lst.Count(); i++)
            {
                lst[i].parseFunc(map, coll[i]);
            }
        };
    }

}