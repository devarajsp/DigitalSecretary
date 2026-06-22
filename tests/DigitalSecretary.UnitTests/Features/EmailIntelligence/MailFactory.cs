using DigitalSecretary.Features.EmailIntelligence;

namespace DigitalSecretary.UnitTests.Features;

/// <summary>Builds <see cref="ParsedEmail"/> fixtures for the Email Intelligence tests.</summary>
internal static class MailFactory
{
    public static ParsedEmail Msg(
        string from,
        string[] to,
        DateTimeOffset date,
        string subject = "",
        string body = "",
        string? fromName = null,
        string? messageId = null)
    {
        var email = new ParsedEmail
        {
            Date = date,
            Subject = subject,
            Body = body,
            MessageId = messageId,
            From = new EmailParticipant(from, fromName),
        };
        foreach (var t in to)
            email.To.Add(new EmailParticipant(t, null));
        return email;
    }
}
