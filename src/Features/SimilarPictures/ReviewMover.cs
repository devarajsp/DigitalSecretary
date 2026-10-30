using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>One file moved into the review folder - enough to put it back.</summary>
public sealed record MoveEntry(string From, string To, long Size);

/// <summary>One "Move" click: when it happened and every file it moved.</summary>
public sealed record MoveBatch(DateTime WhenUtc, string Root, IReadOnlyList<MoveEntry> Entries);

/// <summary>What a removal pass did.</summary>
public sealed record RemoveOutcome(int Removed, int Skipped, int Failed, long Bytes, bool Cancelled, MoveBatch? Batch);

/// <summary>Where removed pictures go.</summary>
public enum RemoveAction
{
    /// <summary>Into the review folder inside the scanned folder (the default; Undo puts them back).</summary>
    ReviewFolder,

    /// <summary>The Windows Recycle Bin (restore from the Recycle Bin, not from this window).</summary>
    RecycleBin
}

/// <summary>Everything one removal pass needs.</summary>
public sealed class RemoveRequest
{
    public required string Root { get; init; }
    public required IReadOnlyList<SimilarGroup> Groups { get; init; }

    /// <summary>The paths the user marked. Keepers in this set are refused - one picture of every group always stays.</summary>
    public required IReadOnlySet<string> Marked { get; init; }

    public RemoveAction Action { get; init; } = RemoveAction.ReviewFolder;

    /// <summary>Sends a file to the Recycle Bin; replaceable so tests never touch the real one.</summary>
    public Action<string> Recycle { get; init; } = ReviewMover.SendToRecycleBin;

    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }
}

/// <summary>
/// Moves the pictures the user marked out of the way - into a review folder inside the scanned folder
/// (keeping their sub-folder layout) or to the Recycle Bin - and can undo the last move. Guards:
/// a group's keeper is never removed, a file that changed since the scan is skipped, a name clash in the
/// review folder gets " (2)" rather than overwriting, and the review folder is never scanned.
/// </summary>
public static class ReviewMover
{
    /// <summary>Name of the review folder created inside the scanned folder.</summary>
    public const string ReviewFolderName = "_Similar pictures - review";

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static string ReviewFolderFor(string root) => Path.Combine(root, ReviewFolderName);

    /// <summary>Sends one file to the Recycle Bin.</summary>
    public static void SendToRecycleBin(string path)
        => FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);

    /// <summary>
    /// The members to remove: marked, not a keeper, de-duplicated, in group order. Pure - this is the
    /// rule that guarantees at least one picture of every group survives.
    /// </summary>
    public static IReadOnlyList<PictureRecord> Plan(IReadOnlyList<SimilarGroup> groups, IReadOnlySet<string> marked)
    {
        var keepers = groups.Select(g => g.Keeper.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return [.. groups.SelectMany(g => g.Members).Select(m => m.Picture)
            .Where(p => marked.Contains(p.Path) && !keepers.Contains(p.Path) && seen.Add(p.Path))];
    }

    /// <summary>Where <paramref name="path"/> lands in the review folder, avoiding existing names.</summary>
    public static string TargetFor(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var target = Path.Combine(ReviewFolderFor(root), relative);
        var dir = Path.GetDirectoryName(target)!;
        var stem = Path.GetFileNameWithoutExtension(target);
        var ext = Path.GetExtension(target);
        for (var n = 2; File.Exists(target); n++)
        {
            target = Path.Combine(dir, stem + " (" + n + ")" + ext);
        }
        return target;
    }

    /// <summary>Runs the removal. Never throws for a single file; failures are counted and logged.</summary>
    public static RemoveOutcome Run(RemoveRequest request)
    {
        var plan = Plan(request.Groups, request.Marked);
        var entries = new List<MoveEntry>();
        int removed = 0, skipped = 0, failed = 0;
        long bytes = 0;
        var cancelled = false;

        foreach (var picture in plan)
        {
            if (request.Cancel.IsCancellationRequested || (request.Pause is not null && !request.Pause.Wait(request.Cancel)))
            {
                cancelled = true;
                break;
            }
            var info = new FileInfo(picture.Path);
            if (!info.Exists || !picture.Matches(info.Length, info.LastWriteTimeUtc))
            {
                skipped++;
                request.Log?.Report("Skipped (missing or changed since the scan): " + picture.Path);
                continue;
            }
            try
            {
                entries.Add(RemoveOne(request, picture.Path, info.Length));
                removed++;
                bytes += info.Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                failed++;
                request.Log?.Report("Could not remove " + picture.Path + ": " + ex.Message);
            }
        }

        var batch = request.Action == RemoveAction.ReviewFolder && entries.Count > 0
            ? new MoveBatch(DateTime.UtcNow, request.Root, entries)
            : null;
        return new RemoveOutcome(removed, skipped, failed, bytes, cancelled, batch);
    }

    /// <summary>Moves every file of <paramref name="batch"/> back. Returns (restored, skipped).</summary>
    public static (int Restored, int Skipped) Undo(MoveBatch batch, IProgress<string>? log = null)
    {
        int restored = 0, skipped = 0;
        foreach (var entry in batch.Entries)
        {
            try
            {
                if (!File.Exists(entry.To) || File.Exists(entry.From))
                {
                    skipped++;
                    log?.Report("Not restored (moved away, or something is already there): " + entry.From);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(entry.From)!);
                File.Move(entry.To, entry.From);
                restored++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skipped++;
                log?.Report("Could not restore " + entry.From + ": " + ex.Message);
            }
        }
        return (restored, skipped);
    }

    /// <summary>Saves the last batch so Undo still works after the app is closed.</summary>
    public static void SaveLastBatch(string file, MoveBatch batch)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(batch, JsonOpts));
    }

    /// <summary>The saved last batch, or null when there is none or it is unreadable.</summary>
    public static MoveBatch? LoadLastBatch(string file)
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<MoveBatch>(File.ReadAllText(file), JsonOpts) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static MoveEntry RemoveOne(RemoveRequest request, string path, long size)
    {
        if (request.Action == RemoveAction.RecycleBin)
        {
            request.Recycle(path);
            return new MoveEntry(path, "", size);
        }
        var target = TargetFor(request.Root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(path, target);
        return new MoveEntry(path, target, size);
    }
}
