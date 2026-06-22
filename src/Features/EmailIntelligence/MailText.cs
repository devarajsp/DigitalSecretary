using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Pure text helpers shared across the pipeline: HTML-to-text, whitespace normalization,
/// hashing, and word tokenization. Separated so every step can be unit-tested.
/// </summary>
public static class MailText
{
    private static readonly Regex ScriptStyleRe = new(
        "<(script|style)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex TagRe = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRe = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex WordRe = new(@"[a-z][a-z'\-]{2,}", RegexOptions.Compiled);

    /// <summary>Strips HTML to readable plain text (removes scripts/styles, decodes entities).</summary>
    public static string HtmlToText(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return "";
        var noScript = ScriptStyleRe.Replace(html, " ");
        var noTags = TagRe.Replace(noScript, " ");
        return NormalizeWhitespace(System.Net.WebUtility.HtmlDecode(noTags));
    }

    /// <summary>Collapses runs of whitespace to single spaces and trims.</summary>
    public static string NormalizeWhitespace(string? s) =>
        string.IsNullOrEmpty(s) ? "" : WhitespaceRe.Replace(s, " ").Trim();

    /// <summary>Lower-cased hex SHA-256 of a string (UTF-8).</summary>
    public static string Sha256Hex(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    /// <summary>Lower-cased hex SHA-256 of raw bytes.</summary>
    public static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Lower-cased alphabetic words of length 3+ (for topic/keyword analysis).</summary>
    public static IEnumerable<string> Words(string? text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;
        foreach (Match m in WordRe.Matches(text.ToLowerInvariant()))
            yield return m.Value;
    }

    /// <summary>Common words excluded from topic extraction.</summary>
    public static readonly IReadOnlySet<string> StopWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "the", "and", "you", "for", "are", "but", "not", "this", "that", "with", "have", "your",
        "from", "was", "will", "can", "all", "our", "out", "get", "they", "there", "their", "what",
        "when", "would", "could", "should", "about", "just", "like", "than", "then", "them", "were",
        "been", "being", "his", "her", "its", "one", "two", "new", "now", "may", "also", "any",
        "how", "who", "why", "email", "mail", "wrote", "sent", "subject", "http", "https", "www",
        "com", "please", "thanks", "thank", "regards", "best", "dear", "hello", "fwd",
    };
}
