using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

namespace DigitalSecretary.Kit;

/// <summary>One file moved into the review folder - enough to put it back.</summary>
public sealed record MoveEntry(string From, string To, long Size);

/// <summary>One "Move" click: when it happened and every file it moved.</summary>
public sealed record MoveBatch(DateTime WhenUtc, string Root, IReadOnlyList<MoveEntry> Entries);

/// <summary>What a removal pass did.</summary>
public sealed record RemoveOutcome(int Removed, int Skipped, int Failed, long Bytes, bool Cancelled, MoveBatch? Batch);

/// <summary>Where removed files go.</summary>
public enum RemoveAction
{
    /// <summary>Into the review folder inside the scanned folder (the default; Undo puts them back).</summary>
    ReviewFolder,

    /// <summary>The Windows Recycle Bin (restore from the Recycle Bin, not from the app).</summary>
    RecycleBin
}

/// <summary>A file to remove, with the size and modified time it had when it was scanned.</summary>
public sealed record MoveCandidate(string Path, long Size, DateTime ModifiedUtc);

/// <summary>Everything one removal pass needs. The caller has already decided which files go.</summary>
public sealed class MoveJob
{
    public required string Root { get; init; }

    /// <summary>Name of the review folder created inside <see cref="Root"/> (e.g. "_Similar videos - review").</summary>
    public required string ReviewFolderName { get; init; }

    public required IReadOnlyList<MoveCandidate> Candidates { get; init; }
    public RemoveAction Action { get; init; } = RemoveAction.ReviewFolder;

    /// <summary>Sends a file to the Recycle Bin; replaceable so tests never touch the real one.</summary>
    public Action<string> Recycle { get; init; } = ReviewFolder.SendToRecycleBin;

    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }
}

/// <summary>
/// The undo-able "move out of the way" used by the look-alike finders. Files go into a review folder
/// inside the scanned folder (keeping their sub-folder layout) or to the Recycle Bin, and the last batch
/// can be put back. Guards: a file that changed since the scan is skipped, a name clash in the review
/// folder gets " (2)" rather than overwriting, and a single file's failure never stops the pass.
/// Which files may go (e.g. "never a group's keeper") is the calling feature's rule, not this class's.
/// </summary>
public static class ReviewFolder
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>The review folder for <paramref name="root"/>.</summary>
    public static string For(string root, string folderName) => Path.Combine(root, folderName);

    /// <summary>Sends one file to the Recycle Bin.</summary>
    public static void SendToRecycleBin(string path)
        => FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);

    /// <summary>Where <paramref name="path"/> lands in the review folder, avoiding existing names.</summary>
    public static string TargetFor(string root, string folderName, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var target = Path.Combine(For(root, folderName), relative);
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
    public static RemoveOutcome Run(MoveJob job)
    {
        var entries = new List<MoveEntry>();
        int removed = 0, skipped = 0, failed = 0;
        long bytes = 0;
        var cancelled = false;

        foreach (var file in job.Candidates)
        {
            if (job.Cancel.IsCancellationRequested || (job.Pause is not null && !job.Pause.Wait(job.Cancel)))
            {
                cancelled = true;
                break;
            }
            var info = new FileInfo(file.Path);
            if (!info.Exists || info.Length != file.Size || info.LastWriteTimeUtc != file.ModifiedUtc)
            {
                skipped++;
                job.Log?.Report("Skipped (missing or changed since the scan): " + file.Path);
                continue;
            }
            try
            {
                entries.Add(RemoveOne(job, file.Path, info.Length));
                removed++;
                bytes += info.Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                failed++;
                job.Log?.Report("Could not remove " + file.Path + ": " + ex.Message);
            }
        }

        var batch = job.Action == RemoveAction.ReviewFolder && entries.Count > 0
            ? new MoveBatch(DateTime.UtcNow, job.Root, entries)
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

    private static MoveEntry RemoveOne(MoveJob job, string path, long size)
    {
        if (job.Action == RemoveAction.RecycleBin)
        {
            job.Recycle(path);
            return new MoveEntry(path, "", size);
        }
        var target = TargetFor(job.Root, job.ReviewFolderName, path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(path, target);
        return new MoveEntry(path, target, size);
    }
}
