namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Computes per-person relationship metrics from the messages: counts each direction, first/last
/// contact, a transparent strength score, and a dormant flag. All arithmetic is local.
/// </summary>
public sealed class RelationshipAnalyzer
{
    public void Analyze(
        IReadOnlyList<Person> people,
        IReadOnlyList<ParsedEmail> emails,
        string ownerAddress,
        DateTimeOffset now,
        int dormantMonths = 12)
    {
        var index = BuildAddressIndex(people);

        foreach (var email in emails)
        {
            var fromOwner = email.From is not null &&
                            email.From.Address.Equals(ownerAddress, StringComparison.OrdinalIgnoreCase);

            if (fromOwner)
            {
                foreach (var recipient in email.Recipients)
                    if (index.TryGetValue(recipient.Address, out var p))
                        Touch(p, email.Date, fromMe: true);
            }
            else if (email.From is not null && index.TryGetValue(email.From.Address, out var sender))
            {
                Touch(sender, email.Date, fromMe: false);
            }
        }

        foreach (var person in people)
        {
            person.TotalMessages = person.FromMe + person.FromThem;
            person.StrengthScore = Score(person, now);
            person.Dormant = person.LastContact is { } last &&
                             last < now.AddMonths(-dormantMonths) &&
                             person.TotalMessages >= 3;
        }
    }

    private static Dictionary<string, Person> BuildAddressIndex(IReadOnlyList<Person> people)
    {
        var index = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
        foreach (var person in people)
            foreach (var address in person.Addresses)
                index[address] = person;
        return index;
    }

    private static void Touch(Person p, DateTimeOffset date, bool fromMe)
    {
        if (fromMe)
            p.FromMe++;
        else
            p.FromThem++;

        if (date == default)
            return;
        if (p.FirstContact is null || date < p.FirstContact)
            p.FirstContact = date;
        if (p.LastContact is null || date > p.LastContact)
            p.LastContact = date;
    }

    /// <summary>Strength = 50% volume + 20% reciprocity + 30% recency, scaled to 0..100.</summary>
    private static double Score(Person p, DateTimeOffset now)
    {
        var volume = Math.Min(1.0, Math.Log10(p.TotalMessages + 1) / 2.0);
        var reciprocity = p.TotalMessages == 0
            ? 0
            : 1.0 - Math.Abs(p.FromMe - p.FromThem) / (double)p.TotalMessages;
        var recency = 0.0;
        if (p.LastContact is { } last)
            recency = Math.Max(0, 1.0 - (now - last).TotalDays / 730.0);

        return Math.Round((0.5 * volume + 0.2 * reciprocity + 0.3 * recency) * 100, 1);
    }
}
