using System.Text.RegularExpressions;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Removes duplicate messages and groups them into threads — all locally. Exact duplicates are
/// matched by <c>Message-Id</c>; messages without one (or copied across accounts) fall back to a
/// hash of sender + minute + subject + normalized body, so the same mail downloaded from both
/// Yahoo and Gmail collapses to a single record.
/// </summary>
public sealed class Deduplicator
{
    private static readonly Regex ReplyPrefixRe =
        new(@"^(re|fwd|fw)\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record Result(IReadOnlyList<ParsedEmail> Unique, int DuplicatesRemoved);

    public Result Deduplicate(IEnumerable<ParsedEmail> emails)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<ParsedEmail>();
        var duplicates = 0;

        foreach (var email in emails)
        {
            if (seen.Add(KeyFor(email)))
                unique.Add(email);
            else
                duplicates++;
        }

        return new Result(unique, duplicates);
    }

    /// <summary>The identity key used for de-duplication.</summary>
    public static string KeyFor(ParsedEmail e)
    {
        if (!string.IsNullOrWhiteSpace(e.MessageId))
            return "mid:" + e.MessageId.Trim();

        var basis = string.Join(
            '|',
            e.From?.Address ?? "",
            e.Date.UtcDateTime.ToString("yyyyMMddHHmm"),
            e.Subject.Trim().ToLowerInvariant(),
            MailText.NormalizeWhitespace(e.Body));
        return "hash:" + MailText.Sha256Hex(basis);
    }

    /// <summary>A stable key grouping messages into the same conversation/thread.</summary>
    public static string ThreadKey(ParsedEmail e)
    {
        if (e.References.Count > 0)
            return "ref:" + e.References[0];
        if (!string.IsNullOrWhiteSpace(e.InReplyTo))
            return "ref:" + e.InReplyTo.Trim();

        var subject = ReplyPrefixRe.Replace(e.Subject.Trim(), "").Trim().ToLowerInvariant();
        return "subj:" + subject;
    }
}
