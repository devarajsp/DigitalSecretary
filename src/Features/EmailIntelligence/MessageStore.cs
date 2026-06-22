using System.Text;
using System.Text.Json;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Persists the de-duplicated message corpus to a JSON file beside the report, so a later run can
/// <b>append</b> a new archive and consolidate everything into one de-duplicated dataset. Round-trips
/// through a small DTO because <see cref="ParsedEmail"/> has computed/read-only members.
/// </summary>
public sealed class MessageStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public void Save(IReadOnlyList<ParsedEmail> emails, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var dtos = emails.Select(Dto.From).ToList();
        File.WriteAllText(path, JsonSerializer.Serialize(dtos, Options), new UTF8Encoding(false));
    }

    public IReadOnlyList<ParsedEmail> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return Array.Empty<ParsedEmail>();
            var dtos = JsonSerializer.Deserialize<List<Dto>>(File.ReadAllText(path), Options);
            return dtos is null ? Array.Empty<ParsedEmail>() : dtos.Select(d => d.ToEmail()).ToList();
        }
        catch
        {
            // A corrupt store should not abort a run; treat it as empty.
            return Array.Empty<ParsedEmail>();
        }
    }

    internal sealed class Dto
    {
        public string SourceFile { get; set; } = "";
        public string? MessageId { get; set; }
        public string? InReplyTo { get; set; }
        public List<string> References { get; set; } = new();
        public DateTimeOffset Date { get; set; }
        public string Subject { get; set; } = "";
        public string? FromAddress { get; set; }
        public string? FromName { get; set; }
        public List<Party> To { get; set; } = new();
        public List<Party> Cc { get; set; } = new();
        public string Body { get; set; } = "";
        public int Category { get; set; }
        public List<Att> Attachments { get; set; } = new();

        public static Dto From(ParsedEmail e) => new()
        {
            SourceFile = e.SourceFile,
            MessageId = e.MessageId,
            InReplyTo = e.InReplyTo,
            References = e.References.ToList(),
            Date = e.Date,
            Subject = e.Subject,
            FromAddress = e.From?.Address,
            FromName = e.From?.Name,
            To = e.To.Select(Party.From).ToList(),
            Cc = e.Cc.Select(Party.From).ToList(),
            Body = e.Body,
            Category = (int)e.Category,
            Attachments = e.Attachments.Select(Att.From).ToList(),
        };

        public ParsedEmail ToEmail()
        {
            var email = new ParsedEmail
            {
                SourceFile = SourceFile,
                MessageId = MessageId,
                InReplyTo = InReplyTo,
                References = References.ToArray(),
                Date = Date,
                Subject = Subject,
                Body = Body,
                Category = (MessageCategory)Category,
                From = FromAddress is null ? null : new EmailParticipant(FromAddress, FromName),
            };
            foreach (var p in To)
                email.To.Add(p.ToParticipant());
            foreach (var p in Cc)
                email.Cc.Add(p.ToParticipant());
            foreach (var a in Attachments)
                email.Attachments.Add(a.ToAttachment());
            return email;
        }
    }

    internal sealed class Party
    {
        public string Address { get; set; } = "";
        public string? Name { get; set; }

        public static Party From(EmailParticipant p) => new() { Address = p.Address, Name = p.Name };

        public EmailParticipant ToParticipant() => new(Address, Name);
    }

    internal sealed class Att
    {
        public string FileName { get; set; } = "";
        public string MimeType { get; set; } = "";
        public long Size { get; set; }
        public string Sha256 { get; set; } = "";

        public static Att From(ParsedAttachment a) =>
            new() { FileName = a.FileName, MimeType = a.MimeType, Size = a.Size, Sha256 = a.Sha256 };

        public ParsedAttachment ToAttachment() => new(FileName, MimeType, Size, Sha256);
    }
}
