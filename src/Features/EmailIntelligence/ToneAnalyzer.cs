namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Assigns a per-relationship tone using the offline <see cref="SentimentLexicon"/>. Scores the
/// correspondent's messages and averages them into a score + label (Positive / Neutral / Negative).
/// A transparent heuristic — clearly labelled as such, never an AI call.
/// </summary>
public sealed class ToneAnalyzer
{
    public const double PositiveThreshold = 0.5;
    public const double NegativeThreshold = -0.5;

    /// <summary>Total lexicon weight found in a piece of text (positive = warmer).</summary>
    public double ScoreText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        double sum = 0;
        foreach (var word in MailText.Words(text))
            if (SentimentLexicon.Weights.TryGetValue(word, out var weight))
                sum += weight;
        return sum;
    }

    public static string LabelFor(double score) =>
        score > PositiveThreshold ? "Positive" : score < NegativeThreshold ? "Negative" : "Neutral";

    public void Analyze(IReadOnlyList<Person> people, IReadOnlyList<ParsedEmail> emails, string ownerAddress)
    {
        var index = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
        foreach (var person in people)
            foreach (var address in person.Addresses)
                index[address] = person;

        var totals = new Dictionary<Person, (double Sum, int Count)>();
        foreach (var email in emails)
        {
            var person = CorrespondentFor(email, index, ownerAddress);
            if (person is null)
                continue;
            var score = ScoreText(email.Subject + " " + email.Body);
            var current = totals.GetValueOrDefault(person);
            totals[person] = (current.Sum + score, current.Count + 1);
        }

        foreach (var (person, total) in totals)
        {
            var average = total.Count == 0 ? 0 : total.Sum / total.Count;
            person.ToneScore = Math.Round(average, 2);
            person.ToneLabel = LabelFor(person.ToneScore);
        }
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
