using System.Text;

namespace SPADE;

/// <summary>
/// Reads an instance string such as <c>({1,2},{(1,2)})</c> into a <see cref="UtilCollection"/>
/// in one pass, so the time taken grows in proportion to the input's length.
/// </summary>
/// <remarks>
/// Whitespace rules (#2):
/// <list type="bullet">
/// <item>Whitespace before or after <c>{ } ( ) ,</c> and at either end of the input is skipped.</item>
/// <item>Inside a value, whitespace next to <c>[ ] ; : =</c> is dropped, so <c>x1 : True</c> reads as <c>x1:True</c>.</item>
/// <item>Any other whitespace inside a value is kept. A run of plain spaces stays exactly as typed
/// (<c>New York</c>); a run containing a tab or line break becomes one space, since a line break
/// inside a value is a wrap (<c>New\nYork</c> reads as <c>New York</c>).</item>
/// </list>
/// These match the rules Redux applied before parsing, so that layer can be removed.
/// </remarks>
internal class InstanceScanner
{
    /// <summary>
    /// Deepest nesting of sets and lists accepted. Far beyond any real instance; it stops
    /// deeply nested input from overflowing the stack and crashing the process (#7).
    /// </summary>
    public const int MaxDepth = 256;

    // Characters that end a value.
    private const string Structural = "{}(),";

    // Characters inside a value that whitespace never belongs next to.
    private const string ValueDelimiters = "[];:=";

    private readonly string text;
    private int pos;

    private InstanceScanner(string text)
    {
        this.text = text;
    }

    public static UtilCollection Parse(string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        return new InstanceScanner(text).ParseInstance();
    }

    private UtilCollection ParseInstance()
    {
        SkipWhitespace();
        if (AtEnd()) throw Error("a value, { or (");
        UtilCollection result = ParseElement(0);
        SkipWhitespace();
        if (!AtEnd()) throw Error("end of input");
        return result;
    }

    // Expects pos at the first character of an element, after any whitespace.
    private UtilCollection ParseElement(int depth)
    {
        char c = text[pos];
        if (c == '{' || c == '(') return ParseCollection(depth + 1);
        if (Structural.IndexOf(c) >= 0) throw Error("a value, { or (");
        return ParseValue();
    }

    private UtilCollection ParseCollection(int depth)
    {
        if (depth > MaxDepth) throw Error($"at most {MaxDepth} levels of nesting");

        char open = text[pos];
        char close = open == '{' ? '}' : ')';
        pos++;

        List<UtilCollection> items = new();
        SkipWhitespace();
        if (!AtEnd() && text[pos] == close)
        {
            pos++;
        }
        else
        {
            while (true)
            {
                SkipWhitespace();
                if (AtEnd()) throw Error("a value, { or (");
                items.Add(ParseElement(depth));

                SkipWhitespace();
                if (AtEnd() || (text[pos] != ',' && text[pos] != close)) throw Error($", or {close}");
                if (text[pos++] == close) break;
            }
        }

        if (open == '(') return new UtilCollection(items);
        return UtilCollection.FromSetItems(items);
    }

    private UtilCollection ParseValue()
    {
        int start = pos;
        while (!AtEnd() && Structural.IndexOf(text[pos]) < 0) pos++;
        return UtilCollection.FromValue(NormalizeValue(start, pos));
    }

    // Applies the whitespace rules to text[start..end). text[start] is never whitespace.
    private string NormalizeValue(int start, int end)
    {
        StringBuilder sb = new(end - start);
        int i = start;
        while (i < end)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                sb.Append(text[i++]);
                continue;
            }

            int j = i;
            bool plainSpaces = true;
            while (j < end && char.IsWhiteSpace(text[j]))
            {
                if (text[j] != ' ') plainSpaces = false;
                j++;
            }

            bool trailing = j == end;
            bool besideDelimiter = ValueDelimiters.IndexOf(sb[sb.Length - 1]) >= 0
                || (!trailing && ValueDelimiters.IndexOf(text[j]) >= 0);
            if (!trailing && !besideDelimiter)
            {
                if (plainSpaces) sb.Append(text, i, j - i);
                else sb.Append(' ');
            }
            i = j;
        }
        return sb.ToString();
    }

    private void SkipWhitespace()
    {
        while (!AtEnd() && char.IsWhiteSpace(text[pos])) pos++;
    }

    private bool AtEnd() => pos >= text.Length;

    private SpadeParseException Error(string expected)
    {
        string found = AtEnd() ? "end of input" : text[pos].ToString();
        return new SpadeParseException(pos + 1, expected, found);
    }
}
