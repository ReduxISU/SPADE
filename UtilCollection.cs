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

    // A multiset keeps every occurrence in `list`, in input order, and how often each element
    // occurs in `counts` (#1). It is unordered, like a set.
    private bool isMultiset;
    private Dictionary<UtilCollection, int>? counts;

    // For a set read from an instance string: every element as written, duplicates included,
    // so the set can still be read as a multiset if its definition says so.
    private List<UtilCollection>? occurrences;

    // Set the first time this collection is hashed. A HashSet keeps an element under the hash it
    // had when it went in, so changing the element (or anything inside it) afterwards would leave
    // it under a stale hash, and lookups would quietly miss it. Once frozen, every change throws.
    // Hashing a list or set hashes its elements, so they freeze too, all the way down.
    // volatile: GetHashCode writes hashCode before frozen, so a thread that sees frozen also sees
    // the hash. A frozen collection is safe to read from several threads.
    private volatile bool frozen;
    private int hashCode;

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
        isMultiset = parsed.isMultiset;
        counts = parsed.counts;
        occurrences = parsed.occurrences;
    }

    internal static UtilCollection FromValue(string value)
    {
        return new UtilCollection { value = value, isValue = true };
    }

    // Copies items, like the constructors, so the caller's list can't change this set later.
    internal static UtilCollection FromSetItems(List<UtilCollection> items)
    {
        return new UtilCollection(items.ToHashSet()) { occurrences = new List<UtilCollection>(items) };
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

    // Both copy, so later changes to the caller's list or set can't change this collection.
    public UtilCollection(List<UtilCollection> list__)
    {
        list = new List<UtilCollection>(list__);
        isOrdered = true;
    }

    public UtilCollection(HashSet<UtilCollection> set__)
    {
        set = new HashSet<UtilCollection>(set__);
        isOrdered = false;
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
            ThrowIfFrozen();
            list[index] = value;
        }
    }

    public void Add(UtilCollection item)
    {
        ThrowIfFrozen();
        if (isMultiset)
        {
            list.Add(item);
            counts![item] = CountOf(item) + 1;
        }
        else if (isOrdered)
        {
            list.Add(item);
        }
        else
        {
            set.Add(item);
            occurrences?.Add(item);
        }
    }

    public IEnumerator GetEnumerator()
    {
        if (isValue) throw new InvalidOperationException("Cannot enumerate over a value");

        if (isOrdered || isMultiset)
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

    public bool IsMultiset()
    {
        return isMultiset;
    }

    /// <summary>
    /// The same elements as a multiset. For a set read from an instance string, this brings back
    /// the duplicates the set dropped, so <c>{3,3,2,2}</c> gives four elements. Only for sets and multisets.
    /// </summary>
    public UtilCollection AsMultiset()
    {
        if (isValue || isOrdered) throw new InvalidOperationException("Only a set can be read as a multiset");
        if (isMultiset) return this;

        UtilCollection multiset = new UtilCollection { isMultiset = true, counts = new() };
        foreach (UtilCollection item in occurrences ?? set.ToList())
        {
            multiset.Add(item);
        }
        return multiset;
    }

    /// <summary>How many times item occurs: 0 or 1 in a set, any number in a multiset or list.</summary>
    public int CountOf(UtilCollection item)
    {
        if (isValue) throw new InvalidOperationException("Cannot count in a value");
        if (isMultiset) return counts!.TryGetValue(item, out int n) ? n : 0;
        if (isOrdered) return list.Count(x => x.Equals(item));
        return set.Contains(item) ? 1 : 0;
    }

    /// <summary>
    /// True if every element occurs in other at least as often as it does here, so the multiset
    /// {7,7} is in {7,7,1} but not in {7,1}. Only for sets and multisets.
    /// </summary>
    public bool IsSubsetOf(UtilCollection other)
    {
        if (isValue || isOrdered || other.isValue || other.isOrdered)
            throw new InvalidOperationException("IsSubsetOf needs two sets or multisets");

        IEnumerable<UtilCollection> distinct = isMultiset ? counts!.Keys : set;
        return distinct.All(item => CountOf(item) <= other.CountOf(item));
    }

    public bool Contains(UtilCollection collection)
    {
        if (isMultiset)
        {
            return counts!.ContainsKey(collection);
        }
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
        if (isOrdered || isMultiset)
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
        if (isMultiset != other.isMultiset) throw new InvalidOperationException("Can't take the intersection of a set and a multiset");
        if (isMultiset)
        {
            // Each element occurs as often as in whichever side has fewer.
            UtilCollection result = new UtilCollection { isMultiset = true, counts = new() };
            foreach (UtilCollection item in list)
            {
                if (result.CountOf(item) < other.CountOf(item)) result.Add(item);
            }
            return result;
        }
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
        if (isMultiset != other.isMultiset) throw new InvalidOperationException("Can't take the union of a set and a multiset");
        if (isMultiset)
        {
            // Each element occurs as often as in whichever side has more.
            UtilCollection result = new UtilCollection { isMultiset = true, counts = new() };
            foreach (UtilCollection item in list) result.Add(item);
            foreach (UtilCollection item in other.list)
            {
                if (result.CountOf(item) < other.CountOf(item)) result.Add(item);
            }
            return result;
        }
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

        // A value, a list, a set and a multiset are never equal to each other, even when empty.
        if (isValue != other.isValue || isOrdered != other.isOrdered || isMultiset != other.isMultiset) return false;

        if (isValue)
        {
            return value == other.value;
        }

        if (isMultiset)
        {
            return list.Count == other.list.Count
                && counts!.Count == other.counts!.Count
                && counts.All(entry => other.CountOf(entry.Key) == entry.Value);
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

    private void ThrowIfFrozen()
    {
        if (frozen)
        {
            throw new InvalidOperationException(
                "This collection has been hashed (it is in a set or used as a key) and can't be changed; "
                + "build it fully before adding it to a set.");
        }
    }

    // The first call freezes the collection, so the hash can't change and is worked out only once.
    public override int GetHashCode()
    {
        if (!frozen)
        {
            hashCode = ComputeHashCode();
            frozen = true;
        }
        return hashCode;
    }

    private int ComputeHashCode()
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

        if (isMultiset)
        {
            // Order-independent, like a set, but every occurrence counts.
            int total = 0;
            foreach (UtilCollection item in list)
            {
                total = unchecked(total + item.GetHashCode());
            }
            return HashCode.Combine(isOrdered, isMultiset, list.Count, total);
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
        if (isMultiset)
        {
            // A copy, so changes to it can't get out of step with the counts.
            return new List<UtilCollection>(list);
        }
        if (isOrdered)
        {
            // A copy, so changes to it can't reach a collection that may already be frozen.
            return new List<UtilCollection>(list);
        }
        else
        {
            return set.ToList();
        }
    }

    /// <summary>
    /// A set or list as an instance string that reads back as the same collection. A value inside it
    /// is quoted when it has to be, such as <c>{"a,b",c}</c>. A value on its own is its text, unquoted,
    /// so <c>"hello, world"</c> read alone gives <c>hello, world</c>.
    /// </summary>
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
            // Inside a collection, a value that couldn't be read back as written, such as
            // "hello, world", is quoted (#22). Every other value is written as it is.
            sb.Append(InstanceScanner.NeedsQuotes(value!) ? InstanceScanner.Quote(value!) : value);
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
