using System.Globalization;
using System.Text.RegularExpressions;

namespace SPADE.Grammar.Enums;
enum BuiltInSet {integers, numbers}

// What "number" accepts, shared by "x is number" and "S subset number" (#23).
static class NumberValue
{
    // An optional sign, then digits with an optional fractional part, or a leading-dot fraction:
    // 5, -3, +7, 2.5, -0.75, .5, -.5. No exponent, no trailing dot, no separators or whitespace.
    // [0-9] rather than \d, which also matches non-ASCII digits; \z rather than $, which allows a trailing newline.
    private static readonly Regex Pattern = new(@"^[+-]?([0-9]+(\.[0-9]+)?|\.[0-9]+)\z", RegexOptions.CultureInvariant);

    /// <summary>Whether a single value's text is a finite decimal number, read the same on every machine.</summary>
    public static bool IsNumber(string text)
    {
        return Pattern.IsMatch(text)
            && double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double value)
            && double.IsFinite(value);
    }
}
