namespace DigitalSecretary.Features.GoogleDriveDownloader;

/// <summary>A Drive folder's identity for rebuilding the local directory tree.</summary>
public sealed record DriveFolderNode(string Id, string Name, string? ParentId);

/// <summary>Pure file/path helpers for saved Drive files (separated for unit testing).</summary>
public static class DriveFileNaming
{
    public const int MaxBaseLength = 120;

    /// <summary>Replaces characters that are invalid in file names and trims trailing dots/spaces.</summary>
    public static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Trim().TrimEnd('.');
        if (name.Length > MaxBaseLength)
            name = name[..MaxBaseLength].Trim();
        return name.Length == 0 ? "_" : name;
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

    /// <summary>
    /// Walks the parent chain to produce the sanitized, top-down folder segments for a file. Stops at
    /// a folder whose parent is unknown (e.g. "My Drive" root or a shared item), and guards against
    /// cycles. Returns an empty list for files that live directly at the resolvable root.
    /// </summary>
    public static IReadOnlyList<string> ResolveFolderPath(
        IReadOnlyDictionary<string, DriveFolderNode> folders, string? startParentId)
    {
        var segments = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = startParentId;
        while (current is not null && folders.TryGetValue(current, out var node) && visited.Add(current))
        {
            segments.Add(Sanitize(node.Name));
            current = node.ParentId;
        }
        segments.Reverse();
        return segments;
    }
}
