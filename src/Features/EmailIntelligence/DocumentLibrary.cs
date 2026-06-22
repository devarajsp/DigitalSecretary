namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Builds the useful-document library from all attachments: de-duplicates identical files by content
/// hash, classifies each by type, and records how often it appears and who sent it. Purely local.
/// </summary>
public sealed class DocumentLibrary
{
    public IReadOnlyList<DocumentItem> Build(IReadOnlyList<ParsedEmail> emails)
    {
        var byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);

        foreach (var email in emails)
        {
            foreach (var attachment in email.Attachments)
            {
                var key = string.IsNullOrEmpty(attachment.Sha256)
                    ? attachment.FileName + "|" + attachment.Size
                    : attachment.Sha256;

                if (!byKey.TryGetValue(key, out var entry))
                {
                    entry = new Entry(attachment.FileName, Classify(attachment), attachment.Size, key);
                    byKey[key] = entry;
                }
                entry.Count++;
                if (email.From is not null && !string.IsNullOrEmpty(email.From.Address))
                    entry.Senders.Add(email.From.Address);
            }
        }

        return byKey.Values
            .Select(e => new DocumentItem(e.FileName, e.Type, e.Size, e.Sha256, e.Count, string.Join("; ", e.Senders)))
            .OrderByDescending(d => d.Count)
            .ThenByDescending(d => d.Size)
            .ToList();
    }

    /// <summary>Classifies an attachment into a coarse document type by extension / MIME.</summary>
    public static string Classify(ParsedAttachment attachment)
    {
        var ext = Path.GetExtension(attachment.FileName).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "pdf" => "PDF",
            "jpg" or "jpeg" or "png" or "gif" or "bmp" or "webp" or "heic" or "svg" => "Image",
            "doc" or "docx" or "odt" or "rtf" or "txt" or "pages" => "Document",
            "xls" or "xlsx" or "ods" or "csv" => "Spreadsheet",
            "ppt" or "pptx" or "odp" or "key" => "Presentation",
            "zip" or "rar" or "7z" or "gz" or "tar" => "Archive",
            _ => attachment.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "Image" : "Other",
        };
    }

    private sealed class Entry(string fileName, string type, long size, string sha256)
    {
        public string FileName { get; } = fileName;
        public string Type { get; } = type;
        public long Size { get; } = size;
        public string Sha256 { get; } = sha256;
        public int Count { get; set; }
        public HashSet<string> Senders { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
