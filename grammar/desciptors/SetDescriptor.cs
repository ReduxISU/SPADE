

using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

using SPADE;
using SPADE.Grammar.Enums;

namespace SPADE.Grammar.descriptor;

class SetDescriptor
{
    public Func<UtilCollection,bool> isMember;

    public Func<UtilCollection> genSet;

    public Dictionary<String, UtilCollection> map;

    public SetDescriptor(Dictionary<String, UtilCollection> _map)
    {
        map = _map;
        isMember = (a) => false;
        genSet = () => new UtilCollection("");
    }

    public SetDescriptor(Dictionary<String, UtilCollection> _map, Func<UtilCollection,bool> _isMember, Func<UtilCollection> _genSet)
    {
        map = _map;
        isMember = _isMember;
        genSet = _genSet;
    }
    public SetDescriptor(Dictionary<String, UtilCollection> _map, UtilCollection collection)
    {
        map = _map;
        isMember = (checkedColl) => collection.Contains(checkedColl);
        genSet = () => collection;
    }

    public SetDescriptor(Dictionary<String, UtilCollection> _map, String name)
    {
        map = _map;
        isMember = (checkedColl) => map[name].Contains(checkedColl);
        genSet = () => map[name];
    }

    public SetDescriptor(Dictionary<String, UtilCollection> _map, BuiltInSet builtInSet)
    {
        map = _map;
        isMember = (checkedColl) => int.TryParse(checkedColl.ToString(), out _);
        genSet = () => throw new Exception("Can't generate infinite integer set");
    }

    public SetDescriptor(Dictionary<string, UtilCollection> _map, FormatDescriptor formatDescriptor, List<ConstraintDescriptor> constraintDescriptors)
    {
        map = _map;
        isMember = (collection) =>
        {
            formatDescriptor.parseFunc(map, collection);
            foreach (ConstraintDescriptor constraintDescriptor in constraintDescriptors)
            {
                constraintDescriptor.check(map);
            }
            return true; 
        };

        genSet = () => throw new NotImplementedException("generating every set descriptor not implemented");
    }

    /*
        Func<UtilCollection, bool> _isMember = (a) => 
        Func<UtilCollection> _genSet = () =>
        return new SetDescriptor(_isMember, _genSet);
    */


    public SetDescriptor Cross(SetDescriptor other)
    {
        Func<UtilCollection, bool> _isMember = (a) =>
            a.IsOrdered() && a.Count() == 2 && isMember(a[0]) && other.isMember(a[1]);
        Func<UtilCollection> _genSet = () => genSet().Cross(other.genSet());
        return new SetDescriptor(map, _isMember, _genSet);
    }

    public SetDescriptor UnorderedCross(SetDescriptor other)
    {
        Func<UtilCollection, bool> _isMember = (a) => 
        {
            // Each member is a set of nodes, like the edge {a,b}. A list such as (a,b)
            // is an ordered pair and belongs to cross, and a plain value is not an edge.
            if (a.IsValue() || a.IsOrdered()) return false;

            foreach (UtilCollection item in a)
            {
                if (!isMember(item) && !other.isMember(item))
                {
                    return false;
                }
            } 
            return true;
        };
        Func<UtilCollection> _genSet = () => 
        {
            UtilCollection set = new("{}");
            foreach (UtilCollection first in genSet())
            {
                foreach (UtilCollection second in other.genSet())
                {
                    UtilCollection pair = new("{}");
                    pair.Add(first);
                    pair.Add(second);
                    set.Add(pair);
                }
            }
            return set;
        };
        return new SetDescriptor(map, _isMember, _genSet);
    }

    public SetDescriptor Union(SetDescriptor other)
    {
        
        Func<UtilCollection, bool> _isMember = (a) => isMember(a) || other.isMember(a);
        Func<UtilCollection> _genSet = () => genSet().Union(other.genSet());
        return new SetDescriptor(map, _isMember, _genSet);
    }

    public SetDescriptor Intersect(SetDescriptor other)
    {
        Func<UtilCollection, bool> _isMember = (a) => isMember(a) && other.isMember(a);
        Func<UtilCollection> _genSet = () => genSet().Intersect(other.genSet());
        return new SetDescriptor(map, _isMember, _genSet);
    }

}