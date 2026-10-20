using System.Collections.ObjectModel;
using System.Globalization;

using SPADE.Grammar.Enums;

namespace SPADE;

/// <summary>The type of every element of a list, matrix or tensor declared with an element type (#30).</summary>
public enum ScalarType
{
    /// <summary>"int": a whole number such as 5 or -3.</summary>
    Int,

    /// <summary>"number": a decimal number such as 2.5, -0.75 or .5; every int is one.</summary>
    Number,

    /// <summary>"string": any single value.</summary>
    String,
}

/// <summary>
/// A rectangular n-dimensional value read from nested lists, such as the matrix <c>((1,2),(3,4))</c> (#30).
/// It is immutable, so it is safe to keep and to read from several threads.
/// </summary>
/// <remarks>
/// Elements are kept as written, row-major, and compared by their text, like <see cref="UtilCollection"/>
/// values, so <c>2</c> and <c>2.0</c> are different numbers. Indices count from 0.
/// When a dimension is empty, every dimension after it is unknown and reported as 0, so <c>()</c> read as a
/// matrix has shape 0×0.
/// </remarks>
public sealed class Tensor : IEquatable<Tensor>
{
    private readonly int[] shape;
    private readonly string[] values;
    private readonly int hashCode;

    /// <summary>The type every element has.</summary>
    public ScalarType ElementType { get; }

    /// <summary>The length of each dimension, outermost first: [rows, columns] for a matrix.</summary>
    public IReadOnlyList<int> Shape { get; }

    /// <summary>The number of dimensions: 1 for a list, 2 for a matrix.</summary>
    public int Rank => shape.Length;

    /// <summary>The number of elements, the product of <see cref="Shape"/>.</summary>
    public int Count => values.Length;

    /// <summary>Every element as written, row-major.</summary>
    public IReadOnlyList<string> Values { get; }

    /// <summary>
    /// Makes a tensor from its shape and its elements in row-major order. Throws ArgumentException when the
    /// shape is empty or negative, the number of values isn't the product of the shape, or a value isn't of
    /// the element type. Dimensions after an empty one are set to 0.
    /// </summary>
    public Tensor(ScalarType elementType, IEnumerable<int> shape, IEnumerable<string> values)
    {
        if (shape == null) throw new ArgumentNullException(nameof(shape));
        if (values == null) throw new ArgumentNullException(nameof(values));
        if (!Enum.IsDefined(elementType)) throw new ArgumentOutOfRangeException(nameof(elementType));

        this.shape = shape.ToArray();
        this.values = values.ToArray();
        if (this.shape.Length == 0) throw new ArgumentException("A tensor has at least one dimension", nameof(shape));
        if (this.shape.Any(n => n < 0)) throw new ArgumentException("A dimension can't be negative", nameof(shape));

        // Every dimension after an empty one is unknown, so it is 0, as when the tensor is read.
        int empty = Array.IndexOf(this.shape, 0);
        if (empty >= 0) Array.Fill(this.shape, 0, empty, this.shape.Length - empty);

        long count = 1;
        foreach (int n in this.shape)
        {
            count *= n;
            if (count > int.MaxValue) break;
        }
        if (count != this.values.Length)
            throw new ArgumentException($"A tensor of shape {ShapeText(this.shape)} has {count} values, not {this.values.Length}", nameof(values));

        foreach (string value in this.values)
        {
            if (value == null || !IsElement(elementType, value))
                throw new ArgumentException($"{value ?? "null"} is not {Article(elementType)}", nameof(values));
        }

        ElementType = elementType;
        Shape = new ReadOnlyCollection<int>(this.shape);
        Values = new ReadOnlyCollection<string>(this.values);
        hashCode = ComputeHashCode();
    }

    /// <summary>The element at an index, one number per dimension, each counting from 0, as written.</summary>
    public string this[params int[] index] => values[Offset(index)];

    /// <summary>The element at an index as an int. Only for a tensor of int.</summary>
    public int GetInt(params int[] index)
    {
        if (ElementType != ScalarType.Int) throw new InvalidOperationException($"This tensor's elements are {Name(ElementType)}, not int");
        return IntValue.Parse(this[index]);
    }

    /// <summary>The element at an index as a double. For a tensor of int or number.</summary>
    public double GetNumber(params int[] index)
    {
        if (ElementType == ScalarType.String) throw new InvalidOperationException("This tensor's elements are string, not number");
        return double.Parse(this[index], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }

    /// <summary>The tensor as nested lists, row-major, such as <c>((1,2),(3,4))</c>. A new collection on every call.</summary>
    public UtilCollection ToCollection()
    {
        int next = 0;
        return Build(0, ref next);
    }

    private UtilCollection Build(int depth, ref int next)
    {
        List<UtilCollection> items = new(shape[depth]);
        for (int i = 0; i < shape[depth]; i++)
        {
            items.Add(depth == shape.Length - 1 ? UtilCollection.FromValue(values[next++]) : Build(depth + 1, ref next));
        }
        return new UtilCollection(items);
    }

    /// <summary>The tensor as an instance string of nested lists, such as <c>((1,2),(3,4))</c>.</summary>
    public override string ToString()
    {
        return ToCollection().ToString();
    }

    /// <summary>Equal when the element type, the shape and every element's text are the same.</summary>
    public bool Equals(Tensor? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ElementType == other.ElementType
            && hashCode == other.hashCode
            && shape.SequenceEqual(other.shape)
            && values.SequenceEqual(other.values, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Tensor);
    }

    public override int GetHashCode()
    {
        return hashCode;
    }

    private int ComputeHashCode()
    {
        HashCode hash = new();
        hash.Add(ElementType);
        foreach (int n in shape) hash.Add(n);
        foreach (string value in values) hash.Add(value, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    private int Offset(int[] index)
    {
        if (index == null || index.Length != shape.Length)
            throw new ArgumentException($"A tensor of rank {shape.Length} needs {shape.Length} indices");
        int offset = 0;
        for (int d = 0; d < shape.Length; d++)
        {
            if (index[d] < 0 || index[d] >= shape[d])
                throw new IndexOutOfRangeException($"Index {index[d]} is outside dimension {d + 1}, of length {shape[d]}");
            offset = offset * shape[d] + index[d];
        }
        return offset;
    }

    // Whether a single value's text is an element of this type, as "is int", "is number" and "is string" read it.
    internal static bool IsElement(ScalarType type, string text)
    {
        return type switch
        {
            ScalarType.Int => IntValue.TryParse(text, out _),
            ScalarType.Number => NumberValue.IsNumber(text),
            _ => true,
        };
    }

    // The type's name as written in a definition string.
    internal static string Name(ScalarType type)
    {
        return type switch
        {
            ScalarType.Int => "int",
            ScalarType.Number => "number",
            _ => "string",
        };
    }

    // "an int", "a number", "a string", for error messages.
    internal static string Article(ScalarType type)
    {
        return type == ScalarType.Int ? "an int" : "a " + Name(type);
    }

    private static string ShapeText(int[] shape)
    {
        return string.Join("x", shape);
    }
}
