using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>Serializes the report to portable JSON (the source the HTML report reads from).</summary>
public sealed class JsonExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Compact, presentation-friendly projection of the report.</summary>
    public static object BuildModel(IntelligenceReport r) => new
    {
        meta = new
        {
            owner = r.OwnerAddress,
            generatedUtc = r.GeneratedUtc,
            messageCount = r.MessageCount,
            duplicatesRemoved = r.DuplicatesRemoved,
            attachmentCount = r.AttachmentCount,
            peopleCount = r.People.Count,
        },
        topTopics = r.TopTopics,
        people = r.People.Select(p => new
        {
            id = p.Id,
            name = p.DisplayName,
            addresses = p.Addresses,
            phones = p.Phones,
            urls = p.Urls,
            org = p.Organization,
            fromMe = p.FromMe,
            fromThem = p.FromThem,
            total = p.TotalMessages,
            first = p.FirstContact,
            last = p.LastContact,
            strength = p.StrengthScore,
            dormant = p.Dormant,
            topics = p.Topics,
            tone = p.ToneScore,
            toneLabel = p.ToneLabel,
        }),
        edges = r.Edges.Select(e => new { source = e.SourceId, target = e.TargetId, weight = e.Weight }),
        timelines = r.Timelines.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(t => new { date = t.Date, kind = t.Kind, summary = t.Summary, sourceFile = t.SourceFile })),
        lifeData = r.LifeData.Select(l => new
        {
            category = l.Category, date = l.Date, sender = l.Sender, subject = l.Subject,
            amount = l.Amount, currency = l.Currency, detail = l.Detail,
        }),
        documents = r.Documents.Select(d => new
        {
            fileName = d.FileName, type = d.Type, size = d.Size, count = d.Count, senders = d.Senders,
        }),
    };

    public string Serialize(IntelligenceReport r) => JsonSerializer.Serialize(BuildModel(r), Options);

    public void Write(IntelligenceReport r, string path) =>
        File.WriteAllText(path, Serialize(r), new UTF8Encoding(false));
}

/// <summary>Writes the contacts/relationships as CSV (opens directly in Excel; no dependency).</summary>
public sealed class CsvExporter
{
    public void WriteContacts(IntelligenceReport r, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,Primary Address,All Addresses,Phones,URLs,Organization,From Me,From Them,Total,First Contact,Last Contact,Strength,Dormant,Topics,Tone,Tone Score");
        foreach (var p in r.People)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                Field(p.DisplayName),
                Field(p.Id),
                Field(string.Join("; ", p.Addresses)),
                Field(string.Join("; ", p.Phones)),
                Field(string.Join("; ", p.Urls)),
                Field(p.Organization ?? ""),
                p.FromMe.ToString(CultureInfo.InvariantCulture),
                p.FromThem.ToString(CultureInfo.InvariantCulture),
                p.TotalMessages.ToString(CultureInfo.InvariantCulture),
                Field(p.FirstContact?.ToString("yyyy-MM-dd") ?? ""),
                Field(p.LastContact?.ToString("yyyy-MM-dd") ?? ""),
                p.StrengthScore.ToString(CultureInfo.InvariantCulture),
                p.Dormant ? "yes" : "no",
                Field(string.Join("; ", p.Topics)),
                Field(p.ToneLabel),
                p.ToneScore.ToString(CultureInfo.InvariantCulture),
            }));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public void WriteLifeData(IntelligenceReport r, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Category,Date,Sender,Subject,Amount,Currency,Detail");
        foreach (var l in r.LifeData)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                Field(l.Category),
                Field(l.Date == default ? "" : l.Date.ToString("yyyy-MM-dd")),
                Field(l.Sender),
                Field(l.Subject),
                l.Amount?.ToString(CultureInfo.InvariantCulture) ?? "",
                Field(l.Currency ?? ""),
                Field(l.Detail),
            }));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public void WriteDocuments(IntelligenceReport r, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("File Name,Type,Size (bytes),Count,Senders");
        foreach (var d in r.Documents)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                Field(d.FileName),
                Field(d.Type),
                d.Size.ToString(CultureInfo.InvariantCulture),
                d.Count.ToString(CultureInfo.InvariantCulture),
                Field(d.Senders),
            }));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static string Field(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;
}

/// <summary>Exports contacts as a vCard 3.0 file for import into Gmail / Outlook.</summary>
public sealed class VCardExporter
{
    public void Write(IntelligenceReport r, string path)
    {
        var sb = new StringBuilder();
        foreach (var p in r.People)
        {
            sb.AppendLine("BEGIN:VCARD");
            sb.AppendLine("VERSION:3.0");
            sb.AppendLine("FN:" + Escape(p.DisplayName));
            foreach (var address in p.Addresses)
                sb.AppendLine("EMAIL;TYPE=INTERNET:" + address);
            foreach (var phone in p.Phones)
                sb.AppendLine("TEL:" + Escape(phone));
            foreach (var url in p.Urls)
                sb.AppendLine("URL:" + Escape(url));
            if (!string.IsNullOrEmpty(p.Organization))
                sb.AppendLine("ORG:" + Escape(p.Organization));
            sb.AppendLine("END:VCARD");
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,");
}

/// <summary>Exports the relationship network as GraphML (opens in Gephi, yEd, etc.).</summary>
public sealed class GraphMlExporter
{
    public void Write(IntelligenceReport r, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<graphml xmlns=\"http://graphml.graphdrawing.org/xmlns\">");
        sb.AppendLine("  <key id=\"name\" for=\"node\" attr.name=\"name\" attr.type=\"string\"/>");
        sb.AppendLine("  <key id=\"strength\" for=\"node\" attr.name=\"strength\" attr.type=\"double\"/>");
        sb.AppendLine("  <key id=\"weight\" for=\"edge\" attr.name=\"weight\" attr.type=\"int\"/>");
        sb.AppendLine("  <graph edgedefault=\"undirected\">");
        foreach (var p in r.People)
            sb.AppendLine($"    <node id=\"{Xml(p.Id)}\"><data key=\"name\">{Xml(p.DisplayName)}</data>" +
                          $"<data key=\"strength\">{p.StrengthScore.ToString(CultureInfo.InvariantCulture)}</data></node>");
        var i = 0;
        foreach (var e in r.Edges)
            sb.AppendLine($"    <edge id=\"e{i++}\" source=\"{Xml(e.SourceId)}\" target=\"{Xml(e.TargetId)}\">" +
                          $"<data key=\"weight\">{e.Weight}</data></edge>");
        sb.AppendLine("  </graph>");
        sb.AppendLine("</graphml>");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Xml(string s) => System.Security.SecurityElement.Escape(s) ?? "";
}
