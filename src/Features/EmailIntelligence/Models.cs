namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>How a message was classified by local heuristics (no AI).</summary>
public enum MessageCategory
{
    Unknown,
    Personal,
    Newsletter,
    Transactional,
    Notification,
    Spam,
}

/// <summary>One email address plus the display name seen alongside it.</summary>
public sealed record EmailParticipant(string Address, string? Name);

/// <summary>Metadata about one attachment. The bytes are hashed but not retained here.</summary>
public sealed record ParsedAttachment(string FileName, string MimeType, long Size, string Sha256);

/// <summary>A single email, normalized from a .eml or .txt source file.</summary>
public sealed class ParsedEmail
{
    public string SourceFile { get; set; } = "";
    public string? MessageId { get; set; }
    public string? InReplyTo { get; set; }
    public IReadOnlyList<string> References { get; set; } = Array.Empty<string>();
    public DateTimeOffset Date { get; set; }
    public string Subject { get; set; } = "";
    public EmailParticipant? From { get; set; }
    public List<EmailParticipant> To { get; } = new();
    public List<EmailParticipant> Cc { get; } = new();
    public string Body { get; set; } = "";
    public List<ParsedAttachment> Attachments { get; } = new();
    public MessageCategory Category { get; set; } = MessageCategory.Unknown;

    /// <summary>All recipients (To + Cc).</summary>
    public IEnumerable<EmailParticipant> Recipients => To.Concat(Cc);

    /// <summary>Every participant including the sender.</summary>
    public IEnumerable<EmailParticipant> AllParticipants =>
        From is null ? Recipients : Recipients.Prepend(From);
}

/// <summary>A resolved person: one identity that may own several addresses.</summary>
public sealed class Person
{
    /// <summary>Stable key for this person (their primary, lower-cased address).</summary>
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<string> Addresses { get; } = new();
    public List<string> NameVariants { get; } = new();
    public List<string> Phones { get; } = new();
    public List<string> Urls { get; } = new();
    public string? Organization { get; set; }

    /// <summary>Messages the owner sent to this person.</summary>
    public int FromMe { get; set; }

    /// <summary>Messages this person sent to the owner.</summary>
    public int FromThem { get; set; }

    public int TotalMessages { get; set; }
    public DateTimeOffset? FirstContact { get; set; }
    public DateTimeOffset? LastContact { get; set; }
    public double StrengthScore { get; set; }
    public bool Dormant { get; set; }
    public List<string> Topics { get; } = new();

    /// <summary>Average lexicon tone of this relationship (heuristic, not AI). 0 = neutral.</summary>
    public double ToneScore { get; set; }
    public string ToneLabel { get; set; } = "Neutral";
}

/// <summary>An undirected co-occurrence edge between two people (weighted by shared emails).</summary>
public sealed record GraphEdge(string SourceId, string TargetId, int Weight);

/// <summary>One dated event on a person's relationship timeline.</summary>
public sealed record TimelineEntry(
    DateTimeOffset Date, string Kind, string Summary, string? Counterparty, string SourceFile);

/// <summary>A structured "life data" item extracted from a transactional/notification email.</summary>
public sealed record LifeDataItem(
    string Category, DateTimeOffset Date, string Sender, string Subject,
    decimal? Amount, string? Currency, string Detail);

/// <summary>A de-duplicated attachment in the useful-document library.</summary>
public sealed record DocumentItem(
    string FileName, string Type, long Size, string Sha256, int Count, string Senders);

/// <summary>Progress update emitted by the pipeline while it runs.</summary>
public sealed record AnalysisProgress(string Phase, int Percent, string Detail);

/// <summary>The complete result of analysing an archive — the model the report renders from.</summary>
public sealed class IntelligenceReport
{
    public string OwnerAddress { get; set; } = "";
    public DateTimeOffset GeneratedUtc { get; set; } = DateTimeOffset.UtcNow;
    public int MessageCount { get; set; }
    public int DuplicatesRemoved { get; set; }
    public int AttachmentCount { get; set; }
    public List<Person> People { get; } = new();
    public List<GraphEdge> Edges { get; } = new();
    public Dictionary<string, List<TimelineEntry>> Timelines { get; } = new();
    public List<string> TopTopics { get; } = new();
    public List<LifeDataItem> LifeData { get; } = new();
    public List<DocumentItem> Documents { get; } = new();
}
