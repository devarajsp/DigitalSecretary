using System.Text.RegularExpressions;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Pulls reusable contact details — phone numbers and web/social links — out of a message body
/// (typically the signature block). Pure regex/heuristics; no lookups leave the machine.
/// </summary>
public sealed class SignatureExtractor
{
    private static readonly Regex PhoneRe = new(
        @"(?:\+?\d{1,3}[\s.-]?)?(?:\(?\d{2,4}\)?[\s.-]?){2,4}\d{2,4}", RegexOptions.Compiled);

    private static readonly Regex UrlRe = new(
        @"(?:https?://|www\.)[^\s<>""']+|(?:linkedin\.com|twitter\.com|x\.com|github\.com|facebook\.com)/[^\s<>""']+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record Signature(IReadOnlyList<string> Phones, IReadOnlyList<string> Urls);

    public Signature Extract(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return new Signature(Array.Empty<string>(), Array.Empty<string>());

        var phones = PhoneRe.Matches(body)
            .Select(m => m.Value.Trim())
            .Where(IsLikelyPhone)
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToList();

        var urls = UrlRe.Matches(body)
            .Select(m => m.Value.Trim().TrimEnd('.', ',', ')', ';'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        return new Signature(phones, urls);
    }

    private static bool IsLikelyPhone(string candidate)
    {
        var digits = candidate.Count(char.IsDigit);
        return digits is >= 7 and <= 15;
    }
}
