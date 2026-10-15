using System.Collections;
using System.Runtime.Serialization;

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

    public UtilCollection(string instance)
    {
        if (instance[0] == '{')
        {
            isOrdered = false;
            isValue = false;
            instance = instance.Substring(1, instance.Length - 2);
            while (instance != "")
            {
                string item = findMatchingBrace(instance);
                set.Add(new UtilCollection(item));
                instance = instance.Substring(item.Length).TrimStart(',');
            }
        }
        else if (instance[0] == '(')
        {
            isOrdered = true;
            isValue = false;
            instance = instance.Substring(1, instance.Length - 2);
            while (instance != "")
            {
                string item = findMatchingBrace(instance);
                list.Add(new UtilCollection(item));
                instance = instance.Substring(item.Length).TrimStart(',');
            }
        }
        else
        {
            isValue = true;
            value = instance;
        }
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

    private string findMatchingBrace(string instance)
    {
        Dictionary<char, char> braces = new Dictionary<char, char>
        {
            { '(', ')' },
            { '{', '}' },
        };

        if (!braces.ContainsKey(instance[0]))
        {
            return instance.Split(',')[0];
        }

        Stack<char> stack = new Stack<char>();

        for (int i = 0; i < instance.Length; i++)
        {
            if (braces.ContainsKey(instance[i]))
            {
                stack.Push(instance[i]);
            }
            else if (braces.Values.Contains(instance[i]))
            {
                char openBracket = stack.Pop();
                if (braces[openBracket] != instance[i])
                {
                    throw new Exception("Braces not matched");
                }
                if (stack.Count() == 0)
                {
                    return instance.Substring(0, i + 1);
                }
            }
        }
        throw new Exception("Braces not matched");
    }

    public UtilCollection this[int index]
    {
        get
        {
            if (!isOrdered) throw new InvalidOperationException("Cannot index into a set");
            return list[index];
        }
        set
        {
            if (!isOrdered) throw new InvalidOperationException("Cannot index into a set");
            list[index] = value;
        }
    }

    public void Add(UtilCollection item)
    {
        if (isOrdered)
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
        if (isOrdered) throw new InvalidOperationException("Ordered");
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
            foreach (UtilCollection b in other)
            {
                UtilCollection add = new UtilCollection(new List<UtilCollection> { a, b });
                ret.Add(add);
            }
        }
        return ret;
    }

    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType()) return false;

        UtilCollection other = (UtilCollection)obj;

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
        return ToString().GetHashCode();
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

        string str = "";
        if (isOrdered)
            str += "(";
        else
            str += "{";

        foreach (var item in this)
        {
            //Console.WriteLine(item.ToString());
            str += item.ToString() + ",";
        }
        str = str.TrimEnd(',');

        if (isOrdered)
            str += ")";
        else
            str += "}";

        return str;
    }
}
