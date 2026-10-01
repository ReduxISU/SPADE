using System.Collections;
using System.Runtime.Serialization;
using System.Text;

namespace SPADE;

#pragma warning disable CS1591

public class UtilCollection
{
    private HashSet<UtilCollection> set = new HashSet<UtilCollection>();
    private List<UtilCollection> list = new List<UtilCollection>();
    private string? value;

    private bool isOrdered;
    private bool isValue;


    public UtilCollection()
    {

    }

    /// <summary>
    /// Reads an instance string such as <c>({1,2},{(1,2)})</c>. Throws
    /// <see cref="SpadeParseException"/> if it isn't a well-formed value, set or list.
    /// See <see cref="InstanceScanner"/> for how whitespace is handled.
    /// </summary>
    public UtilCollection(string instance)
    {
        UtilCollection parsed = InstanceScanner.Parse(instance);
        set = parsed.set;
        list = parsed.list;
        value = parsed.value;
        isOrdered = parsed.isOrdered;
        isValue = parsed.isValue;
    }

    internal static UtilCollection FromValue(string value)
    {
        return new UtilCollection { value = value, isValue = true };
    }

    public UtilCollection(HashSet<string> set_)
    {
        isOrdered = false;
        set = new();
        foreach (string i in set_)
        {
                set.Add(new UtilCollection(i)); 
        }
    }

    public UtilCollection(HashSet<HashSet<string>> set_)
    {
        isOrdered = false;
        set = new();
        foreach (HashSet<string> i in set_)
        {
                set.Add(new UtilCollection(i)); 
        }
    }

    public UtilCollection(List<UtilCollection> list__)
    {
        list = list__;
        isOrdered = true;
    }

    public UtilCollection(HashSet<UtilCollection> set__)
    {
        set = set__;
        isOrdered = false;
    }

    public UtilCollection this[int index]
    {
        get { 
            if (!isOrdered) throw new InvalidOperationException("Cannot index into a set");
            return list[index]; 
            }
        set { 
            if (!isOrdered) throw new InvalidOperationException("Cannot index into a set");
            list[index] = value; 
            }
    }

    public void Add(UtilCollection item)
    {
        if(isOrdered)
        {
            list.Add(item);
        }
        else
        {
            set.Add(item);
        }
    }

    public IEnumerator GetEnumerator()
    {
        if (isValue) throw new InvalidOperationException("Cannot enumerate over a value");

        if (isOrdered)
            return list.GetEnumerator();
        else
            return set.GetEnumerator();
    }

    public void assertPair(int len)
    {
        if (!isOrdered || Count() != len)
            throw new InvalidOperationException("Not a list of correct length");
    }

    public void assertPair()
    {
        assertPair(2);
    }

    public void assertCount(int len)
    {
        if (len != Count())
        {
            throw new InvalidOperationException("collection has incorrect length");
        }
    }

    public void assertOrdered()
    {
        if (!isOrdered) throw new InvalidOperationException("Not ordered");
    }

    public void assertUnordered()
    {
        if(isOrdered) throw new InvalidOperationException("Ordered");
    }

    public bool IsUnordered()
    {
        return !isOrdered;
    }
    public bool IsOrdered()
    {
        return isOrdered;
    }

    public bool IsValue()
    {
        return isValue;
    }

    public bool Contains(UtilCollection collection)
    {
        if (isOrdered)
        {
            return list.Contains(collection);
        }
        else
        {
            return set.Contains(collection);
        }
    }
    public int Count()
    {
        if (isOrdered)
        {
            return list.Count;
        }
        else
        {
            return set.Count;
        }
    }

    public int parseInt()
    {
        return int.Parse(ToString());
    }

    public UtilCollection Intersect(UtilCollection other)
    {
        if (isOrdered != other.isOrdered) throw new InvalidOperationException("Can't take the intersection of a set and a list");
        if (!isOrdered)
        {
            return new UtilCollection(set.Intersect(other.set).ToHashSet());
        }
        else
        {
            throw new NotImplementedException();
        }
    }
    
    public UtilCollection Union(UtilCollection other)
    {
        if (isOrdered != other.isOrdered) throw new InvalidOperationException("Can't take the union of a set and a list");
        if (!isOrdered)
        {
            return new UtilCollection(set.Union(other.set).ToHashSet());
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public UtilCollection Cross(UtilCollection other)
    {
        UtilCollection ret = new UtilCollection();
        foreach (UtilCollection a in this)
        {
            foreach(UtilCollection b in other)
            {
                UtilCollection add = new UtilCollection(new List<UtilCollection>{a,b});
                ret.Add(add);
            }
        }
        return ret;
    }

    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType()) return false;

        UtilCollection other = (UtilCollection)obj;

        // A value, a list and a set are never equal to each other, even when empty.
        if (isValue != other.isValue || isOrdered != other.isOrdered) return false;

        if (isValue)
        {
            return value == other.value;
        }

        if (isOrdered)
        {
            return list.SequenceEqual(other.list);
        }
        else
        {
            return set.SetEquals(other.set);
        }
    }

    public bool NotEquals(object? obj)
    {
        if (Equals(obj)) return false;
        return true;
    }

    public override int GetHashCode()
    {
        if (isValue)
        {
            return value!.GetHashCode();
        }

        if (isOrdered)
        {
            HashCode hash = new HashCode();
            hash.Add(isOrdered);
            foreach (UtilCollection item in list)
            {
                hash.Add(item);
            }
            return hash.ToHashCode();
        }

        // Sets are equal regardless of order, so combine element hashes with
        // an order-independent sum rather than hashing ToString(), which
        // follows insertion order.
        int sum = 0;
        foreach (UtilCollection item in set)
        {
            sum = unchecked(sum + item.GetHashCode());
        }
        return HashCode.Combine(isOrdered, set.Count, sum);
    }

    public List<UtilCollection> ToList()
    {
        if (isOrdered)
        {
            return list;
        }
        else
        {
            return set.ToList();
        }
    }

    override public string ToString()
    {
        if (isValue)
        {
            return value!;
        }

        StringBuilder sb = new StringBuilder();
        AppendTo(sb);
        return sb.ToString();
    }

    // Builds the text in one buffer; adding strings with += copied it once per element.
    private void AppendTo(StringBuilder sb)
    {
        if (isValue)
        {
            sb.Append(value);
            return;
        }

        sb.Append(isOrdered ? '(' : '{');
        bool first = true;
        foreach (UtilCollection item in ToList())
        {
            if (!first) sb.Append(',');
            item.AppendTo(sb);
            first = false;
        }
        sb.Append(isOrdered ? ')' : '}');
    }
}
