namespace SPADE;

/// <summary>
/// Thrown when an instance string can't be read or doesn't fit its definition string.
/// Every bad-input case throws this type, so callers can tell bad input apart from a bug.
/// </summary>
public class SpadeParseException : FormatException
{
    /// <summary>Character position in the instance string, counting from 1, when the error has one.</summary>
    public int? Position { get; }

    /// <summary>What the parser expected at <see cref="Position"/>, when the error has one.</summary>
    public string? Expected { get; }

    /// <summary>What the parser found instead, when the error has one.</summary>
    public string? Found { get; }

    /// <summary>
    /// The name of the part the error belongs to, from the outermost format of the definition string,
    /// e.g. "E" for a failed <c>E subset N unorderedcross N</c>. Set for failed constraints and type checks,
    /// including those inside a nested set, which belong to the part that set constrains.
    /// Null for syntax errors and for an instance whose shape doesn't match the format.
    /// </summary>
    public string? Part { get; internal init; }

    /// <summary>
    /// The member or value of <see cref="Part"/> that failed, in full, e.g. "{1,9}" for an edge that
    /// isn't a member, or the whole value for a failed type check. The message may shorten it; this doesn't.
    /// </summary>
    public string? Element { get; internal init; }

    /// <summary>
    /// When <see cref="Element"/> failed because a piece of it is missing from another part, that piece,
    /// e.g. "9" for the edge {1,9} when N is {1,2}. Null when the set isn't built from parts, such as <c>int</c>.
    /// </summary>
    public string? MissingValue { get; internal init; }

    /// <summary>The part <see cref="MissingValue"/> is missing from, e.g. "N". Null when <see cref="MissingValue"/> is.</summary>
    public string? ExpectedIn { get; internal init; }

    /// <summary>A syntax error at a position, e.g. "expected , or } at position 14, found x".</summary>
    public SpadeParseException(int position, string expected, string found)
        : base($"expected {expected} at position {position}, found {found}")
    {
        Position = position;
        Expected = expected;
        Found = found;
    }

    /// <summary>An error with no single position, such as a failed constraint.</summary>
    public SpadeParseException(string message)
        : base(message)
    {
    }

    // A copy with the same message and position, for an outer constraint to claim an inner error
    // as its own (#19). The original stays as the inner exception.
    private SpadeParseException(SpadeParseException inner)
        : base(inner.Message, inner)
    {
        Position = inner.Position;
        Expected = inner.Expected;
        Found = inner.Found;
        Part = inner.Part;
        Element = inner.Element;
        MissingValue = inner.MissingValue;
        ExpectedIn = inner.ExpectedIn;
    }

    /// <summary>The same error, message unchanged, belonging to another part and element.</summary>
    internal SpadeParseException BelongingTo(string part, string element, string? missingValue, string? expectedIn)
    {
        return new SpadeParseException(this)
        {
            Part = part,
            Element = element,
            MissingValue = missingValue,
            ExpectedIn = expectedIn,
        };
    }
}
