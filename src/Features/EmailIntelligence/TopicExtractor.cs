namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Derives the keywords/topics associated with each person and across the whole archive, using
/// local word-frequency over subjects + bodies (stop-words removed). No AI, no network.
/// </summary>
public sealed class TopicExtractor
{
    public void AssignTopics(
        IReadOnlyList<Person> people,
        IReadOnlyList<ParsedEmail> emails,
        string ownerAddress,
        int perPerson = 8)
    {
        var index = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
        foreach (var person in people)
            foreach (var address in person.Addresses)
                index[address] = person;

        var counts = new Dictionary<Person, Dictionary<string, int>>();

        foreach (var email in emails)
        {
            var person = CorrespondentFor(email, index, ownerAddress);
            if (person is null)
                continue;

            if (!counts.TryGetValue(person, out var dict))
                counts[person] = dict = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var word in MailText.Words(email.Subject + " " + email.Body))
                if (!MailText.StopWords.Contains(word))
                    dict[word] = dict.GetValueOrDefault(word) + 1;
        }

        foreach (var (person, dict) in counts)
            person.Topics.AddRange(dict
                .OrderByDescending(kv => kv.Value)
                .Take(perPerson)
                .Select(kv => kv.Key));
    }

    public IReadOnlyList<string> GlobalTopics(IReadOnlyList<ParsedEmail> emails, int top = 20)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var email in emails)
            foreach (var word in MailText.Words(email.Subject + " " + email.Body))
                if (!MailText.StopWords.Contains(word))
                    counts[word] = counts.GetValueOrDefault(word) + 1;

        return counts
            .OrderByDescending(kv => kv.Value)
            .Take(top)
            .Select(kv => kv.Key)
            .ToList();
    }

    private static Person? CorrespondentFor(
        ParsedEmail email,
        Dictionary<string, Person> index,
        string ownerAddress)
    {
        if (email.From is not null &&
            !email.From.Address.Equals(ownerAddress, StringComparison.OrdinalIgnoreCase) &&
            index.TryGetValue(email.From.Address, out var sender))
            return sender;

        foreach (var recipient in email.Recipients)
            if (index.TryGetValue(recipient.Address, out var p))
                return p;

        return null;
    }
}
