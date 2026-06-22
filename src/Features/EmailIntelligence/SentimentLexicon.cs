namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// A small, bundled AFINN-style word list (weights roughly -4..+4) for offline, rule-based tone.
/// This is a heuristic, NOT AI — it ships in the assembly and makes no network calls.
/// </summary>
public static class SentimentLexicon
{
    public static readonly IReadOnlyDictionary<string, int> Weights = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        // Positive
        ["thanks"] = 2, ["thank"] = 2, ["appreciate"] = 2, ["appreciated"] = 2, ["grateful"] = 3,
        ["great"] = 3, ["good"] = 2, ["nice"] = 2, ["love"] = 3, ["loved"] = 3, ["like"] = 1,
        ["happy"] = 3, ["glad"] = 2, ["pleased"] = 2, ["excellent"] = 4, ["awesome"] = 4,
        ["wonderful"] = 4, ["fantastic"] = 4, ["perfect"] = 3, ["congrats"] = 3, ["congratulations"] = 3,
        ["welcome"] = 1, ["excited"] = 3, ["enjoy"] = 2, ["enjoyed"] = 2, ["helpful"] = 2, ["best"] = 1,

        // Negative
        ["sorry"] = -1, ["unfortunately"] = -2, ["problem"] = -2, ["issue"] = -1, ["issues"] = -1,
        ["bad"] = -3, ["terrible"] = -4, ["awful"] = -4, ["hate"] = -3, ["angry"] = -3,
        ["disappointed"] = -3, ["disappointing"] = -3, ["delay"] = -1, ["delayed"] = -2,
        ["complaint"] = -2, ["fail"] = -2, ["failed"] = -2, ["failure"] = -2, ["error"] = -1,
        ["wrong"] = -2, ["worried"] = -2, ["concern"] = -1, ["concerned"] = -2, ["frustrated"] = -3,
        ["urgent"] = -1, ["unhappy"] = -3, ["refund"] = -1, ["cancel"] = -1, ["cancelled"] = -1,
    };
}
