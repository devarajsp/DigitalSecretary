namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Labels a message Personal / Newsletter / Transactional / Notification using local heuristics
/// (sender patterns + subject keywords). No network, no AI; rules are intentionally simple and
/// conservative so a real personal reply is rarely mistaken for bulk mail.
/// </summary>
public sealed class MessageClassifier
{
    private static readonly string[] BulkSender =
    {
        "noreply", "no-reply", "donotreply", "do-not-reply", "newsletter", "mailer", "mailer-daemon",
        "bounce", "notifications", "notification", "notify", "updates", "marketing", "campaign",
    };

    private static readonly string[] TransactionalWords =
    {
        "receipt", "order", "invoice", "payment", "paid", "confirmation", "confirmed", "shipped",
        "delivered", "statement", "booking", "reservation", "ticket", "refund",
    };

    private static readonly string[] NotificationWords =
    {
        "verify", "verification", "reset your password", "security alert", "sign-in", "sign in",
        "your account", "otp", "one-time", "code is",
    };

    public MessageCategory Classify(ParsedEmail e)
    {
        var from = e.From?.Address ?? "";
        var subject = e.Subject.ToLowerInvariant();

        var bulk = BulkSender.Any(b => from.Contains(b, StringComparison.OrdinalIgnoreCase));

        if (NotificationWords.Any(w => subject.Contains(w, StringComparison.Ordinal)))
            return MessageCategory.Notification;
        if (TransactionalWords.Any(w => subject.Contains(w, StringComparison.Ordinal)))
            return MessageCategory.Transactional;
        if (bulk)
            return MessageCategory.Newsletter;

        return MessageCategory.Personal;
    }
}
