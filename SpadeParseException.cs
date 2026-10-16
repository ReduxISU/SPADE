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
}
