namespace DigitalSecretary.Features.GmailDownloader;

/// <summary>Pure file-name helpers for saved emails/attachments (separated for unit testing).</summary>
public static class GmailFileNaming
{
    public const int MaxBaseLength = 80;

    /// <summary>Replaces characters that are invalid in file names and trims trailing dots/spaces.</summary>
    public static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Trim().TrimEnd('.');
        return name.Length == 0 ? "_" : name;
    }

    /// <summary>
    /// Builds the base file name for a message: a sortable date prefix plus the sanitized subject
    /// (capped in length). A blank subject becomes "no-subject".
    /// </summary>
    public static string BaseNameFor(DateTimeOffset date, string? subject)
    {
        var cleanSubject = Sanitize(string.IsNullOrWhiteSpace(subject) ? "no-subject" : subject);
        if (cleanSubject.Length > MaxBaseLength)
            cleanSubject = cleanSubject[..MaxBaseLength].Trim();

        var datePrefix = date == DateTimeOffset.MinValue
            ? "0000-00-00_000000"
            : date.ToLocalTime().ToString("yyyy-MM-dd_HHmmss");

        return $"{datePrefix}_{cleanSubject}";
    }

    /// <summary>Returns a path inside <paramref name="dir"/> that doesn't exist yet, appending _1, _2, …</summary>
    public static string GetUniquePath(string dir, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var candidate = Path.Combine(dir, name + ext);

        int counter = 1;
        while (File.Exists(candidate))
            candidate = Path.Combine(dir, $"{name}_{counter++}{ext}");

        return candidate;
    }
}
