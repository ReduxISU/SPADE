

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

    // Why an item isn't a member: the first piece of it missing from a part named in the set
    // expression, with that part, or null when the set isn't built from parts (#19).
    private Func<UtilCollection, (string Value, string Part)?> missing = (a) => null;

    // Names declared by nested set definitions in this expression, such as e and w in
    // {(e,w) | ...}. Errors about them belong to the part the nested set constrains.
    public HashSet<string> declaredNames = new();

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

    // A set built from two others, such as N cross N.
    private SetDescriptor(SetDescriptor left, SetDescriptor right, Func<UtilCollection,bool> _isMember, Func<UtilCollection> _genSet, Func<UtilCollection, (string Value, string Part)?> _missing)
        : this(left.map, _isMember, _genSet)
    {
        missing = _missing;
        declaredNames.UnionWith(left.declaredNames);
        declaredNames.UnionWith(right.declaredNames);
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
        missing = (checkedColl) => map[name].Contains(checkedColl) ? null : (checkedColl.ToString(), name);
    }

    public SetDescriptor(Dictionary<String, UtilCollection> _map, BuiltInSet builtInSet)
    {
        map = _map;
        if (builtInSet == BuiltInSet.numbers)
        {
            // A single value only: a set or list's text is never a number anyway, but say so.
            isMember = (checkedColl) => checkedColl.IsValue() && NumberValue.IsNumber(checkedColl.ToString());
            genSet = () => throw new Exception("Can't generate infinite number set");
            return;
        }
        isMember = (checkedColl) => int.TryParse(checkedColl.ToString(), out _);
        genSet = () => throw new Exception("Can't generate infinite integer set");
    }

    public SetDescriptor(Dictionary<string, UtilCollection> _map, FormatDescriptor formatDescriptor, List<ConstraintDescriptor> constraintDescriptors)
    {
        map = _map;
        declaredNames.UnionWith(formatDescriptor.names);
        // OrderBy is stable, so the other constraints keep their order.
        constraintDescriptors = constraintDescriptors.OrderBy(c => c.DeclaresMultiset ? 0 : 1).ToList();
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

    /// <summary>
    /// For an item that isn't a member: the first piece of it missing from a part named in the set
    /// expression, and that part. Null when no such piece is found, e.g. for <c>int</c>, <c>number</c> or a set
    /// literal, or when the item has the wrong shape (a value where cross needs a pair).
    /// </summary>
    public (string Value, string Part)? FindMissing(UtilCollection item)
    {
        try
        {
            return missing(item);
        }
        catch (SpadeParseException)
        {
            // A nested set that throws reports its own error, not a missing piece.
            return null;
        }
    }


    public SetDescriptor Cross(SetDescriptor other)
    {
        Func<UtilCollection, bool> _isMember = (a) =>
            a.IsOrdered() && a.Count() == 2 && isMember(a[0]) && other.isMember(a[1]);
        Func<UtilCollection> _genSet = () => genSet().Cross(other.genSet());
        Func<UtilCollection, (string Value, string Part)?> _missing = (a) =>
        {
            if (!a.IsOrdered() || a.Count() != 2) return null;
            return !isMember(a[0]) ? missing(a[0]) : other.missing(a[1]);
        };
        return new SetDescriptor(this, other, _isMember, _genSet, _missing);
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
        Func<UtilCollection, (string Value, string Part)?> _missing = (a) =>
        {
            if (a.IsValue() || a.IsOrdered()) return null;
            foreach (UtilCollection item in a)
            {
                if (!isMember(item) && !other.isMember(item))
                {
                    return missing(item) ?? other.missing(item);
                }
            }
            return null;
        };
        return new SetDescriptor(this, other, _isMember, _genSet, _missing);
    }

    public SetDescriptor Union(SetDescriptor other)
    {
        
        Func<UtilCollection, bool> _isMember = (a) => isMember(a) || other.isMember(a);
        Func<UtilCollection> _genSet = () => genSet().Union(other.genSet());
        // Missing from both sides; the left one is named.
        Func<UtilCollection, (string Value, string Part)?> _missing = (a) => missing(a) ?? other.missing(a);
        return new SetDescriptor(this, other, _isMember, _genSet, _missing);
    }

    public SetDescriptor Intersect(SetDescriptor other)
    {
        Func<UtilCollection, bool> _isMember = (a) => isMember(a) && other.isMember(a);
        Func<UtilCollection> _genSet = () => genSet().Intersect(other.genSet());
        Func<UtilCollection, (string Value, string Part)?> _missing = (a) => !isMember(a) ? missing(a) : other.missing(a);
        return new SetDescriptor(this, other, _isMember, _genSet, _missing);
    }

}