using Microsoft.VisualBasic.FileIO;

namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>How a duplicate is removed. Abstracted so the engine can be tested without deleting.</summary>
public interface IFileDeleter
{
    /// <summary>Past-tense wording for the log, e.g. "sent to the Recycle Bin".</summary>
    string Description { get; }

    /// <summary>Removes one file. Throws on failure; the engine logs and carries on.</summary>
    void Delete(string path);
}

/// <summary>Deletes outright. Opt-in: nothing can be recovered afterwards.</summary>
public sealed class PermanentFileDeleter : IFileDeleter
{
    public string Description => "deleted permanently";

    public void Delete(string path) => File.Delete(path);
}

/// <summary>
/// Sends duplicates to the Windows Recycle Bin - the default, so a mistake is always recoverable.
/// A thin shell over the OS call; the engine around it is what the unit tests cover.
/// </summary>
public sealed class RecycleBinFileDeleter : IFileDeleter
{
    public string Description => "sent to the Recycle Bin";

    public void Delete(string path)
        => FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin,
            UICancelOption.ThrowException);
}

/// <summary>Live progress of a deletion pass.</summary>
public sealed record DeleteProgress(
    int Processed, int Total, int Deleted, long BytesFreed, int Percent, string Current);

/// <summary>What a deletion pass did.</summary>
/// <param name="Deleted">Copies removed - or that would be removed, in a dry run.</param>
/// <param name="Skipped">Copies left alone (already gone, or failed verification).</param>
/// <param name="Failed">Copies that could not be deleted.</param>
/// <param name="BytesFreed">Space freed - or that would be freed, in a dry run.</param>
/// <param name="DryRun">True when nothing was actually touched.</param>
/// <param name="Cancelled">True when the user stopped part-way.</param>
public sealed record DeleteOutcome(
    int Deleted, int Skipped, int Failed, long BytesFreed, bool DryRun, bool Cancelled);

/// <summary>Everything one deletion pass needs.</summary>
public sealed class DeleteRequest
{
    /// <summary>The groups to act on. One copy of each is always kept.</summary>
    public required IReadOnlyList<DuplicateGroup> Groups { get; init; }

    /// <summary>Report only, touching nothing. Defaults to <c>true</c> - the safe setting.</summary>
    public bool DryRun { get; init; } = true;

    /// <summary>Kept in step with the deletions so the saved checksum list stays truthful.</summary>
    public ChecksumIndex? Index { get; init; }

    public IProgress<DeleteProgress>? Progress { get; init; }
    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }
}

/// <summary>
/// Deletes the duplicates of each group, never the copy being kept. Two guards stand in front of
/// every delete: the kept copy must still exist, and - when verification is on - the duplicate must
/// be byte-for-byte identical to it, because two different files can share an MD5. Anything that
/// fails a guard is skipped and logged rather than removed.
/// </summary>
public sealed class DeleteEngine
{
    private readonly IFileDeleter _deleter;
    private readonly Func<string, string, bool>? _verify;

    /// <param name="deleter">How to remove a file (Recycle Bin or permanent).</param>
    /// <param name="verify">
    /// Byte-for-byte check against the kept copy, or <c>null</c> to trust the checksum alone.
    /// </param>
    public DeleteEngine(IFileDeleter deleter, Func<string, string, bool>? verify)
    {
        _deleter = deleter;
        _verify = verify;
    }

    private enum DeleteStep
    {
        Deleted,
        Skipped,
        Failed
    }

    /// <summary>Works through every group's duplicates.</summary>
    public DeleteOutcome Run(DeleteRequest request)
    {
        var total = request.Groups.Sum(g => g.Duplicates.Count);
        var tally = new int[3];
        var processed = 0;
        long bytesFreed = 0;
        var cancelled = false;

        request.Log?.Report(request.DryRun
            ? "Dry run: listing what would be deleted. Nothing will be touched."
            : "Deleting duplicates (" + _deleter.Description + ")...");

        foreach (var group in request.Groups)
        {
            foreach (var duplicate in group.Duplicates)
            {
                if (request.Pause is not null && !request.Pause.Wait(request.Cancel))
                {
                    cancelled = true;
                    break;
                }
                if (request.Cancel.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                processed++;
                var step = DeleteOne(group, duplicate, request);
                tally[(int)step]++;
                if (step == DeleteStep.Deleted)
                {
                    bytesFreed += duplicate.Length;
                }

                request.Progress?.Report(new DeleteProgress(
                    processed, total, tally[(int)DeleteStep.Deleted], bytesFreed,
                    Percent(processed, total), duplicate.Path));
            }

            if (cancelled)
            {
                break;
            }
        }

        return new DeleteOutcome(
            tally[(int)DeleteStep.Deleted], tally[(int)DeleteStep.Skipped], tally[(int)DeleteStep.Failed],
            bytesFreed, request.DryRun, cancelled);
    }

    private static int Percent(int done, int total)
        => total <= 0 ? 100 : (int)Math.Clamp(done * 100L / total, 0, 100);

    /// <summary>Applies the guards to one duplicate and removes it if they all pass.</summary>
    private DeleteStep DeleteOne(DuplicateGroup group, DuplicateFile duplicate, DeleteRequest request)
    {
        if (!File.Exists(group.Keeper.Path))
        {
            request.Log?.Report("Kept copy is missing, so nothing in this set was touched: "
                + group.Keeper.Path);
            return DeleteStep.Skipped;
        }
        if (!File.Exists(duplicate.Path))
        {
            request.Log?.Report("Already gone: " + duplicate.Path);
            return DeleteStep.Skipped;
        }
        if (_verify is not null && !_verify(group.Keeper.Path, duplicate.Path))
        {
            request.Log?.Report("NOT identical to the kept copy - left alone: " + duplicate.Path);
            return DeleteStep.Skipped;
        }

        if (request.DryRun)
        {
            request.Log?.Report("[dry run] would delete " + duplicate.Path
                + " (" + ByteSize.Format(duplicate.Length) + ")");
            return DeleteStep.Deleted;
        }

        try
        {
            _deleter.Delete(duplicate.Path);
            request.Index?.Remove(duplicate.Path);
            request.Log?.Report("Deleted " + duplicate.Path + " (" + ByteSize.Format(duplicate.Length) + ")");
            return DeleteStep.Deleted;
        }
        catch (Exception ex)
        {
            request.Log?.Report("FAILED to delete " + duplicate.Path + ": " + ex.Message);
            return DeleteStep.Failed;
        }
    }
}
