namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Finds the archive owner — the person whose mailbox this is. In a personal archive the owner is
/// overwhelmingly the most frequent <c>From</c> address (their Sent mail), so that is the signal
/// used. The detected value can be overridden in settings.
/// </summary>
public sealed class ContactExtractor
{
    public string DetectOwner(IEnumerable<ParsedEmail> emails)
    {
        return emails
            .Where(e => e.From is not null && !string.IsNullOrEmpty(e.From.Address))
            .GroupBy(e => e.From!.Address, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "";
    }
}
