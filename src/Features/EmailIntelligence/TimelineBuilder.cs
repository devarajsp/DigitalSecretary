namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Builds each person's relationship-history timeline: the ordered list of interactions (sent /
/// received, with subject and any attachments) that the report renders as a zoomable timeline.
/// </summary>
public sealed class TimelineBuilder
{
    public IReadOnlyDictionary<string, List<TimelineEntry>> Build(
        IReadOnlyList<Person> people,
        IReadOnlyList<ParsedEmail> emails,
        string ownerAddress)
    {
        var index = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
        foreach (var person in people)
            foreach (var address in person.Addresses)
                index[address] = person;

        var timelines = new Dictionary<string, List<TimelineEntry>>(StringComparer.Ordinal);

        foreach (var email in emails.OrderBy(e => e.Date))
        {
            var fromOwner = email.From is not null &&
                            email.From.Address.Equals(ownerAddress, StringComparison.OrdinalIgnoreCase);

            var parties = PartiesFor(email, index, fromOwner);
            foreach (var person in parties)
            {
                if (!timelines.TryGetValue(person.Id, out var entries))
                    timelines[person.Id] = entries = new List<TimelineEntry>();

                var subject = string.IsNullOrWhiteSpace(email.Subject) ? "(no subject)" : email.Subject;
                if (email.Attachments.Count > 0)
                    subject += $"  [{email.Attachments.Count} attachment(s)]";

                entries.Add(new TimelineEntry(
                    email.Date,
                    fromOwner ? "sent" : "received",
                    subject,
                    person.DisplayName,
                    email.SourceFile));
            }
        }

        return timelines;
    }

    private static IEnumerable<Person> PartiesFor(
        ParsedEmail email,
        Dictionary<string, Person> index,
        bool fromOwner)
    {
        if (fromOwner)
        {
            foreach (var recipient in email.Recipients)
                if (index.TryGetValue(recipient.Address, out var p))
                    yield return p;
        }
        else if (email.From is not null && index.TryGetValue(email.From.Address, out var sender))
        {
            yield return sender;
        }
    }
}
