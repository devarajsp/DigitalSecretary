using System.Text.Json;

namespace DigitalSecretary.Kit;

/// <summary>
/// One file replaced by its smaller version: where the original went (<see cref="StoredAt"/> is empty when
/// it went to the Recycle Bin) and where the smaller file now is.
/// </summary>
public sealed record SwapEntry(string Original, string StoredAt, string Reduced, long OriginalSize, long ReducedSize);

/// <summary>One "Reduce" run: every file it replaced. Undo works on the last batch.</summary>
public sealed record SwapBatch(DateTime WhenUtc, string Root, IReadOnlyList<SwapEntry> Entries);

/// <summary>Everything needed to swap one original for its smaller version.</summary>
public sealed class SwapRequest
{
    /// <summary>The scanned folder (the review folder is created inside it).</summary>
    public required string Root { get; init; }

    /// <summary>Name of the review folder for originals, e.g. "_Reduce size - originals".</summary>
    public required string ReviewFolderName { get; init; }

    public required string Original { get; init; }

    /// <summary>Size and modified time from the scan: a file changed since then is never replaced.</summary>
    public required long OriginalSize { get; init; }

    public required DateTime OriginalModifiedUtc { get; init; }

    /// <summary>The finished, verified smaller file (a temp file next to the original).</summary>
    public required string Reduced { get; init; }

    /// <summary>Where the smaller file ends up (the original's name, or the same name with a new extension).</summary>
    public required string FinalPath { get; init; }

    public RemoveAction Action { get; init; } = RemoveAction.ReviewFolder;

    /// <summary>Sends a file to the Recycle Bin; replaceable so tests never touch the real one.</summary>
    public Action<string> Recycle { get; init; } = ReviewFolder.SendToRecycleBin;
}

/// <summary>
/// The safe "replace with the smaller version" step shared by the Reduce Size features. A smaller file is
/// always made as a temp file beside the original first (<see cref="TempPathFor"/>) and only swapped in
/// after the caller verified it. The swap moves the original to the review folder (undo-able) or the Recycle
/// Bin, renames the temp file into place, and gives it the original's created / modified dates, so photo and
/// video apps keep sorting it the same way. If the rename fails the original is put back. Undo deletes the
/// smaller file (only if it is still exactly the file we made) and restores the original.
/// </summary>
public static class ShrinkSwap
{
    /// <summary>Marker in the name of a smaller file that is still being made.</summary>
    public const string TempMarker = ".ds-reducing";

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>The temp file for <paramref name="original"/>'s smaller version, with the output extension.</summary>
    public static string TempPathFor(string original, string extension)
        => Path.Combine(Path.GetDirectoryName(original) ?? "", Path.GetFileNameWithoutExtension(original) + TempMarker + extension);

    /// <summary>True for a temp file made by <see cref="TempPathFor"/> (left behind only if the app was killed).</summary>
    public static bool IsTemp(string path)
        => Path.GetFileNameWithoutExtension(path).EndsWith(TempMarker, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Where the smaller file goes: the original's own path when the extension stays, otherwise the same name
    /// with the new extension - or "name (reduced).ext" when that name is already taken by another file.
    /// </summary>
    public static string FinalPathFor(string original, string extension, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        if (string.Equals(Path.GetExtension(original), extension, StringComparison.OrdinalIgnoreCase))
        {
            return original;
        }
        var dir = Path.GetDirectoryName(original) ?? "";
        var stem = Path.GetFileNameWithoutExtension(original);
        var target = Path.Combine(dir, stem + extension);
        for (var n = 1; exists(target); n++)
        {
            target = Path.Combine(dir, stem + (n == 1 ? " (reduced)" : " (reduced " + n + ")") + extension);
        }
        return target;
    }

    /// <summary>
    /// Swaps the original for the smaller file. Throws <see cref="InvalidOperationException"/> when the
    /// original changed since the scan, and IO exceptions when a move fails (the original is put back first
    /// when it is in the review folder).
    /// </summary>
    public static SwapEntry Commit(SwapRequest request)
    {
        var info = new FileInfo(request.Original);
        if (!info.Exists || info.Length != request.OriginalSize || info.LastWriteTimeUtc != request.OriginalModifiedUtc)
        {
            throw new InvalidOperationException("The original changed or vanished since the scan; it was left alone.");
        }
        var (created, modified) = (info.CreationTimeUtc, info.LastWriteTimeUtc);
        var reducedSize = new FileInfo(request.Reduced).Length;

        var stored = MoveOriginalAway(request);
        try
        {
            File.Move(request.Reduced, request.FinalPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (stored.Length > 0 && !File.Exists(request.Original))
            {
                File.Move(stored, request.Original);
            }
            throw;
        }
        File.SetCreationTimeUtc(request.FinalPath, created);
        File.SetLastWriteTimeUtc(request.FinalPath, modified);
        return new SwapEntry(request.Original, stored, request.FinalPath, request.OriginalSize, reducedSize);
    }

    /// <summary>
    /// Restores every original of <paramref name="batch"/> it can: the smaller file is deleted only while it
    /// is still the exact size we made, and nothing is ever overwritten. Recycle-Bin entries cannot be undone
    /// here. Returns (restored, skipped).
    /// </summary>
    public static (int Restored, int Skipped) Undo(SwapBatch batch, IProgress<string>? log = null)
    {
        int restored = 0, skipped = 0;
        foreach (var entry in batch.Entries)
        {
            try
            {
                if (UndoOne(entry, log))
                {
                    restored++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skipped++;
                log?.Report("Could not restore " + entry.Original + ": " + ex.Message);
            }
        }
        return (restored, skipped);
    }

    /// <summary>Deletes temp files left by a run that was killed mid-file. Returns how many went.</summary>
    public static int CleanLeftovers(IEnumerable<string> paths, IProgress<string>? log = null)
    {
        var removed = 0;
        foreach (var path in paths.Where(IsTemp))
        {
            try
            {
                File.Delete(path);
                removed++;
                log?.Report("Removed an unfinished file from an earlier run: " + path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log?.Report("Could not remove unfinished file " + path + ": " + ex.Message);
            }
        }
        return removed;
    }

    /// <summary>Saves the last batch so Undo still works after the app is closed.</summary>
    public static void SaveLastBatch(string file, SwapBatch batch)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(batch, JsonOpts));
    }

    /// <summary>The saved last batch, or null when there is none or it is unreadable.</summary>
    public static SwapBatch? LoadLastBatch(string file)
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<SwapBatch>(File.ReadAllText(file), JsonOpts) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string MoveOriginalAway(SwapRequest request)
    {
        if (request.Action == RemoveAction.RecycleBin)
        {
            request.Recycle(request.Original);
            return "";
        }
        var target = ReviewFolder.TargetFor(request.Root, request.ReviewFolderName, request.Original);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(request.Original, target);
        return target;
    }

    private static bool UndoOne(SwapEntry entry, IProgress<string>? log)
    {
        if (entry.StoredAt.Length == 0 || !File.Exists(entry.StoredAt))
        {
            log?.Report("Not restored (in the Recycle Bin, or moved away): " + entry.Original);
            return false;
        }
        var reduced = new FileInfo(entry.Reduced);
        if (reduced.Exists && reduced.Length != entry.ReducedSize)
        {
            log?.Report("Not restored (the smaller file was changed since): " + entry.Original);
            return false;
        }
        var sameName = string.Equals(entry.Original, entry.Reduced, StringComparison.OrdinalIgnoreCase);
        if (!sameName && File.Exists(entry.Original))
        {
            log?.Report("Not restored (something else is already there): " + entry.Original);
            return false;
        }
        if (reduced.Exists)
        {
            reduced.Delete();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(entry.Original)!);
        File.Move(entry.StoredAt, entry.Original);
        return true;
    }
}
