namespace DigitalSecretary.Features.StorageStudio;

/// <summary>The kinds of clean-up candidate Storage Studio points out.</summary>
public enum CleanupKind
{
    BuildOutput,
    TemporaryFiles,
    Installers,
    LargeAndOld
}

/// <summary>One clean-up card: what it is, why it is safe-ish to remove, and what it covers.</summary>
public sealed record CleanupGroup(CleanupKind Kind, string Title, string Why, long Bytes, IReadOnlyList<SpaceNode> Items);

/// <summary>Thresholds for <see cref="CleanupFinder"/>. Defaults match the user guide.</summary>
public sealed record CleanupOptions
{
    /// <summary>A file at least this large ...</summary>
    public long LargeFileBytes { get; init; } = 1L << 30;

    /// <summary>... and unchanged for at least this long is a "large and old" candidate.</summary>
    public TimeSpan OldAfter { get; init; } = TimeSpan.FromDays(365 * 2);
}

/// <summary>
/// Finds space that is commonly safe to reclaim. <b>Report only</b>: it never deletes or moves
/// anything - each group tells the user where to look and why. Rules are deliberately conservative
/// (a folder named <c>bin</c> only counts when an <c>obj</c> sits beside it, an <c>.exe</c> only
/// inside a Downloads folder), each file is counted in at most one group, and a matched build folder
/// is not searched further, so the totals never double-count.
/// </summary>
public static class CleanupFinder
{
    private static readonly HashSet<string> BuildFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "__pycache__", ".pytest_cache", ".mypy_cache", ".gradle", ".next", ".nuxt", ".parcel-cache", "obj"
    };

    private static readonly HashSet<string> TempExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp", ".temp", ".crdownload", ".part", ".partial", ".download", ".dmp", ".bak", ".old"
    };

    private static readonly HashSet<string> InstallerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".msi", ".msix", ".msixbundle", ".appx", ".iso", ".dmg"
    };

    /// <summary>Title shown on a card.</summary>
    public static string Title(CleanupKind kind) => kind switch
    {
        CleanupKind.BuildOutput => "Build & package folders",
        CleanupKind.TemporaryFiles => "Temporary & partial files",
        CleanupKind.Installers => "Installers & disk images",
        _ => "Large files not changed in years"
    };

    /// <summary>The one-line reason shown under a card's title.</summary>
    public static string Why(CleanupKind kind) => kind switch
    {
        CleanupKind.BuildOutput => "node_modules, obj/bin, __pycache__ and similar are rebuilt by their tools when needed.",
        CleanupKind.TemporaryFiles => "Leftover .tmp, unfinished downloads, crash dumps and backup copies.",
        CleanupKind.Installers => "Setup files and disk images are usually not needed once the program is installed.",
        _ => "Big files nobody has changed in a long time - candidates to archive or move off this drive."
    };

    /// <summary>Runs every rule over <paramref name="folder"/>. Empty groups are left out; largest first.</summary>
    public static IReadOnlyList<CleanupGroup> Find(SpaceNode folder, DateTime nowUtc, CleanupOptions? options = null)
    {
        var opts = options ?? new CleanupOptions();
        var found = new Dictionary<CleanupKind, List<SpaceNode>>();
        var stack = new Stack<SpaceNode>();
        stack.Push(folder);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            foreach (var child in node.Children)
            {
                var kind = child.IsFolder ? ClassifyFolder(child) : ClassifyFile(child, nowUtc, opts);
                if (kind is { } k)
                {
                    (found.TryGetValue(k, out var list) ? list : found[k] = []).Add(child);
                }
                else if (child.IsFolder)
                {
                    stack.Push(child);
                }
            }
        }

        return [.. found
            .Select(p => new CleanupGroup(p.Key, Title(p.Key), Why(p.Key), p.Value.Sum(n => n.Size),
                [.. p.Value.OrderByDescending(n => n.Size)]))
            .Where(g => g.Items.Count > 0)
            .OrderByDescending(g => g.Bytes)];
    }

    /// <summary>A build/package folder, or null. <c>bin</c> needs a sibling <c>obj</c> to count.</summary>
    public static CleanupKind? ClassifyFolder(SpaceNode folder)
    {
        if (BuildFolderNames.Contains(folder.Name))
        {
            return CleanupKind.BuildOutput;
        }
        if (string.Equals(folder.Name, "bin", StringComparison.OrdinalIgnoreCase)
            && folder.Parent is { } parent
            && parent.Children.Any(c => c.IsFolder && string.Equals(c.Name, "obj", StringComparison.OrdinalIgnoreCase)))
        {
            return CleanupKind.BuildOutput;
        }
        return null;
    }

    /// <summary>The first rule a file matches (temporary, installer, large-and-old), or null.</summary>
    public static CleanupKind? ClassifyFile(SpaceNode file, DateTime nowUtc, CleanupOptions options)
    {
        var ext = Path.GetExtension(file.Name);
        if (TempExtensions.Contains(ext) || file.Name.StartsWith("~$", StringComparison.Ordinal))
        {
            return CleanupKind.TemporaryFiles;
        }
        if (InstallerExtensions.Contains(ext)
            || (string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase) && IsInDownloads(file)))
        {
            return CleanupKind.Installers;
        }
        if (file.Size >= options.LargeFileBytes && nowUtc - file.LastWriteUtc >= options.OldAfter)
        {
            return CleanupKind.LargeAndOld;
        }
        return null;
    }

    private static bool IsInDownloads(SpaceNode file)
    {
        // The root's name is a full path, so its own ancestors (e.g. ...\Downloads\Apps) count too.
        for (var node = file.Parent; node is not null; node = node.Parent)
        {
            var segments = node.Parent is null ? node.Name.Split('\\', '/') : [node.Name];
            if (segments.Any(s => string.Equals(s, "Downloads", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }
}
