using System.Text;
using MimeKit;
using MimeKit.Utils;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Parses a source file into a <see cref="ParsedEmail"/>. Handles full-fidelity <c>.eml</c>
/// (via MimeKit — MIME, encodings, attachments) and the readable <c>.txt</c> form written by the
/// Download Emails / Download Gmail features. All decoding is offline; a malformed file yields null.
/// </summary>
public sealed class EmailParser
{
    private static readonly string[] HeaderSeparator = { "From:", "To:", "Cc:", "Date:", "Subject:", "Attachments:" };

    /// <summary>Parses by extension; returns null if the file cannot be read/parsed.</summary>
    public ParsedEmail? ParseFile(string path)
    {
        try
        {
            return Path.GetExtension(path).Equals(".eml", StringComparison.OrdinalIgnoreCase)
                ? ParseEml(path)
                : ParseTxt(path);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Parses a raw RFC822 <c>.eml</c> file.</summary>
    public ParsedEmail ParseEml(string path)
    {
        using var stream = File.OpenRead(path);
        return FromMime(MimeMessage.Load(stream), path);
    }

    /// <summary>Builds a <see cref="ParsedEmail"/> from an in-memory MIME message (testable seam).</summary>
    public ParsedEmail FromMime(MimeMessage msg, string sourceFile)
    {
        var email = new ParsedEmail
        {
            SourceFile = sourceFile,
            MessageId = NullIfBlank(msg.MessageId),
            InReplyTo = NullIfBlank(msg.InReplyTo),
            References = msg.References.Count > 0 ? msg.References.ToArray() : Array.Empty<string>(),
            Date = msg.Date,
            Subject = msg.Subject?.Trim() ?? "",
            From = msg.From.Mailboxes.Select(ToParticipant).FirstOrDefault(),
        };
        foreach (var m in msg.To.Mailboxes)
            email.To.Add(ToParticipant(m));
        foreach (var m in msg.Cc.Mailboxes)
            email.Cc.Add(ToParticipant(m));

        email.Body = !string.IsNullOrEmpty(msg.TextBody) ? msg.TextBody : MailText.HtmlToText(msg.HtmlBody);

        foreach (var part in msg.Attachments.OfType<MimePart>())
        {
            if (part.Content is null)
                continue;
            using var ms = new MemoryStream();
            part.Content.DecodeTo(ms);
            var bytes = ms.ToArray();
            var name = part.FileName ?? part.ContentType.Name ?? "attachment";
            email.Attachments.Add(new ParsedAttachment(
                name, part.ContentType.MimeType, bytes.LongLength, MailText.Sha256Hex(bytes)));
        }
        return email;
    }

    /// <summary>
    /// Parses the readable <c>.txt</c> form: header lines (From/To/Cc/Date/Subject/Attachments),
    /// a separator rule of '=', then the body. Missing headers are tolerated.
    /// </summary>
    public ParsedEmail ParseTxt(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        var email = new ParsedEmail { SourceFile = path };
        var body = new StringBuilder();
        var inHeader = true;

        using var reader = new StringReader(text);
        for (var line = reader.ReadLine(); line is not null; line = reader.ReadLine())
        {
            if (inHeader)
            {
                if (IsSeparator(line))
                {
                    inHeader = false;
                    continue;
                }
                if (TryHeader(line, "From:", out var v)) { email.From = ParseAddresses(v).FirstOrDefault(); continue; }
                if (TryHeader(line, "To:", out v)) { email.To.AddRange(ParseAddresses(v)); continue; }
                if (TryHeader(line, "Cc:", out v)) { email.Cc.AddRange(ParseAddresses(v)); continue; }
                if (TryHeader(line, "Date:", out v)) { email.Date = ParseDate(v); continue; }
                if (TryHeader(line, "Subject:", out v)) { email.Subject = v.Trim(); continue; }
                if (TryHeader(line, "Attachments:", out _)) { continue; }
                if (line.Length == 0) { continue; }

                // A non-header, non-separator line means the body has started without a rule.
                inHeader = false;
                body.AppendLine(line);
            }
            else
            {
                body.AppendLine(line);
            }
        }

        email.Body = body.ToString().Trim();
        return email;
    }

    private static bool IsSeparator(string line) =>
        line.Length >= 5 && line.All(c => c == '=');

    private static bool TryHeader(string line, string prefix, out string value)
    {
        if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = line[prefix.Length..].Trim();
            return true;
        }
        value = "";
        return false;
    }

    private static IEnumerable<EmailParticipant> ParseAddresses(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !InternetAddressList.TryParse(value, out var list))
            return Array.Empty<EmailParticipant>();
        return list.Mailboxes.Select(ToParticipant).ToList();
    }

    private static DateTimeOffset ParseDate(string value)
    {
        if (DateUtils.TryParse(value, out var dto))
            return dto;
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : default;
    }

    private static EmailParticipant ToParticipant(MailboxAddress m) =>
        new((m.Address ?? "").Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(m.Name) ? null : m.Name.Trim());

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
