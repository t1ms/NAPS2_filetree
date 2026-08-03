using System.Text;
using System.Text.RegularExpressions;

namespace NAPS2.Ocr;

/// <summary>
/// Substitutes content-based tokens in file name patterns. Supports both "$(FieldName)" and legacy
/// "{FieldName}" syntax. Field names come from zonal OCR zone templates; additionally a set of
/// generic document tokens (DOC_DATE etc.) can be filled by local-LLM whole-page extraction.
/// </summary>
public static class ContentPlaceholders
{
    public const string DOC_DATE = "DOC_DATE";
    public const string DOC_SENDER = "DOC_SENDER";
    public const string DOC_TYPE = "DOC_TYPE";
    public const string DOC_REF = "DOC_REF";

    /// <summary>
    /// The generic document tokens fillable via local-LLM whole-page extraction, with the field
    /// description used in the extraction prompt.
    /// </summary>
    public static readonly (string Name, string PromptDescription)[] GenericTokens =
    {
        (DOC_DATE, "document date in YYYY-MM-DD format"),
        (DOC_SENDER, "sender, vendor or company name"),
        (DOC_TYPE, "document type as a single word (e.g. Invoice, Letter, Receipt, Contract)"),
        (DOC_REF, "reference, invoice or document number")
    };

    public static IEnumerable<string> GenericTokenNames => GenericTokens.Select(x => x.Name);

    /// <summary>
    /// Names reserved by NAPS2's original date/time and auto-number placeholders. Zone field
    /// names may match these names, but must never override the built-in behavior.
    /// </summary>
    public static readonly HashSet<string> ReservedTokenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "YYYY", "YY", "MM", "DD", "hh", "mm", "ss", "n", "nn", "nnn", "nnnn"
    };

    /// <summary>
    /// The value used when a referenced content field has no usable value.
    /// </summary>
    public const string FallbackValue = "Unknown";

    // Cap individual substituted values so filenames stay a reasonable length
    private const int MaxValueLength = 60;

    // Fixed (Windows) set so behavior is consistent across platforms
    private static readonly char[] InvalidFileNameChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    /// <summary>
    /// Replaces "$(FieldName)" and "{FieldName}" tokens with sanitized field values. Fields with
    /// empty values are substituted with the fallback value.
    /// </summary>
    public static string SubstituteFieldTokens(string pattern, IEnumerable<ZonalOcrField> fields)
    {
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name) || ReservedTokenNames.Contains(field.Name))
            {
                continue;
            }
            string value = SanitizeValue(field.Value);
            pattern = ReplaceToken(pattern, field.Name, value);
        }
        return pattern;
    }

    /// <summary>
    /// Replaces any remaining tokens with the given names by the fallback value, so unresolved
    /// content tokens never leak into saved file names.
    /// </summary>
    public static string SubstituteFallbacks(string pattern, IEnumerable<string> tokenNames)
    {
        foreach (var name in tokenNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }
            pattern = ReplaceToken(pattern, name, FallbackValue);
        }
        return pattern;
    }

    /// <summary>
    /// Whether the pattern references any of the given token names (in either syntax).
    /// </summary>
    public static bool ContainsAnyToken(string pattern, IEnumerable<string> tokenNames)
    {
        return tokenNames.Any(name => TokenRegex(name).IsMatch(pattern));
    }

    private static string ReplaceToken(string pattern, string name, string value)
    {
        return TokenRegex(name).Replace(pattern, value.Replace("$", "$$"));
    }

    private static Regex TokenRegex(string name)
    {
        string escaped = Regex.Escape(name);
        return new Regex($@"\$\({escaped}\)|\{{{escaped}\}}", RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Makes a raw extracted value safe for use in a file name: strips illegal characters,
    /// collapses whitespace, caps the length, and falls back if nothing usable remains.
    /// </summary>
    public static string SanitizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return FallbackValue;
        }
        var sb = new StringBuilder();
        foreach (char c in value)
        {
            // '$' is legal in a Windows filename, but NAPS2's regular filename placeholder
            // expansion runs after content tokens. Treat it as unsafe here so an OCR value such
            // as "$(n)" cannot accidentally become an auto-numbering placeholder.
            sb.Append(InvalidFileNameChars.Contains(c) || char.IsControl(c) || c == '$' ? '_' : c);
        }
        var result = Regex.Replace(sb.ToString(), @"\s+", " ").Trim().Trim('.');
        if (result.Length > MaxValueLength)
        {
            result = result.Substring(0, MaxValueLength).TrimEnd();
        }
        return result.Length == 0 ? FallbackValue : result;
    }
}
