namespace DigitalSecretary.Features.StorageStudio;

/// <summary>Live progress of a space scan.</summary>
public sealed record SpaceScanProgress(int Files, int Folders, long Bytes, string Current);

/// <summary>What a space scan produced.</summary>
/// <param name="Root">The tree, sorted largest first. Partial when <paramref name="Cancelled"/>.</param>
/// <param name="Errors">Folders that could not be read (no access, vanished, I/O error).</param>
/// <param name="SkippedLinks">Symlinks / junctions not followed.</param>
/// <param name="Cancelled">True when the user stopped the scan before it finished.</param>
/// <param name="Elapsed">Wall-clock time the walk took.</param>
public sealed record SpaceScanResult(
    SpaceNode Root, int Errors, int SkippedLinks, bool Cancelled, TimeSpan Elapsed);

/// <summary>Everything one scan needs.</summary>
public sealed class SpaceScanRequest
{
    /// <summary>The folder whose space is being mapped.</summary>
    public required string Root { get; init; }

    public IProgress<SpaceScanProgress>? Progress { get; init; }
    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }

    /// <summary>Report progress after this many files (and once at the end).</summary>
    public int ProgressEvery { get; init; } = 500;
}

/// <summary>
/// Walks a folder tree and builds a <see cref="SpaceNode"/> tree of sizes. Read-only: it never opens
/// a file, only reads directory metadata, so it is fast and safe on any folder. Reparse points
/// (symlinks, junctions) are not followed, so the walk cannot loop or count a folder twice, and
/// unreadable folders are logged and stepped over rather than aborting the scan.
/// </summary>
public static class SpaceScanner
{
    private sealed class Counters
    {
        public int Files;
        public int Folders;
        public long Bytes;
        public int Errors;
        public int Links;
    }

    /// <summary>
    /// Runs the scan. Never throws for file-system problems; a stopped scan returns the partial
    /// tree with <see cref="SpaceScanResult.Cancelled"/> set.
    /// </summary>
    public static SpaceScanResult Run(SpaceScanRequest request)
    {
        var started = DateTime.UtcNow;
        var root = SpaceNode.CreateRoot(Path.GetFullPath(request.Root));
        var counters = new Counters();
        var pending = new Stack<(DirectoryInfo Dir, SpaceNode Node)>();
        pending.Push((new DirectoryInfo(root.Name), root));
        var cancelled = request.Cancel.IsCancellationRequested;

        while (!cancelled && pending.Count > 0)
        {
            var (dir, node) = pending.Pop();
            cancelled = !ScanFolder(dir, node, pending, counters, request);
        }

        root.SortBySize();
        request.Progress?.Report(new SpaceScanProgress(counters.Files, counters.Folders, counters.Bytes, ""));
        return new SpaceScanResult(root, counters.Errors, counters.Links, cancelled, DateTime.UtcNow - started);
    }

    /// <summary>Lists one folder into <paramref name="node"/>. Returns false when the scan was stopped.</summary>
    private static bool ScanFolder(
        DirectoryInfo dir, SpaceNode node, Stack<(DirectoryInfo, SpaceNode)> pending,
        Counters counters, SpaceScanRequest request)
    {
        try
        {
            foreach (var entry in dir.EnumerateFileSystemInfos())
            {
                if (request.Pause is not null && !request.Pause.Wait(request.Cancel))
                {
                    return false;
                }
                if (request.Cancel.IsCancellationRequested)
                {
                    return false;
                }
                AddEntry(entry, node, pending, counters, request);
            }
        }
        catch (UnauthorizedAccessException)
        {
            counters.Errors++;
            request.Log?.Report("No access: " + dir.FullName);
        }
        catch (DirectoryNotFoundException)
        {
            counters.Errors++;
            request.Log?.Report("Folder vanished: " + dir.FullName);
        }
        catch (IOException ex)
        {
            counters.Errors++;
            request.Log?.Report("Cannot read " + dir.FullName + ": " + ex.Message);
        }
        return true;
    }

    private static void AddEntry(
        FileSystemInfo entry, SpaceNode node, Stack<(DirectoryInfo, SpaceNode)> pending,
        Counters counters, SpaceScanRequest request)
    {
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            counters.Links++;
            request.Log?.Report("Skipped link: " + entry.FullName);
            return;
        }

        if (entry is DirectoryInfo sub)
        {
            counters.Folders++;
            pending.Push((sub, node.AddFolder(sub.Name)));
            return;
        }

        if (entry is FileInfo file)
        {
            node.AddFile(file.Name, file.Length, file.LastWriteTimeUtc);
            counters.Files++;
            counters.Bytes += file.Length;
            if (counters.Files % Math.Max(1, request.ProgressEvery) == 0)
            {
                request.Progress?.Report(new SpaceScanProgress(
                    counters.Files, counters.Folders, counters.Bytes, file.DirectoryName ?? ""));
            }
        }
    }
}
