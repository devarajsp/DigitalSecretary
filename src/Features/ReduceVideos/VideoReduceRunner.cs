using System.Globalization;

namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>A video the user chose, with the advice it was chosen on.</summary>
public sealed record VideoCandidate(VideoFacts Facts, VideoAdvice Advice);

/// <summary>Where a run is: file <see cref="Index"/> (1-based) of <see cref="Total"/>, and how far that file is.</summary>
public sealed record ReduceProgress(int Index, int Total, string Path, int FilePercent);

/// <summary>Everything one "Reduce selected" run needs.</summary>
public sealed record VideoReduceJob
{
    public required string Root { get; init; }
    public required IReadOnlyList<VideoCandidate> Candidates { get; init; }
    public required IVideoEngine Engine { get; init; }
    public VideoQuality Quality { get; init; } = VideoQuality.VisuallyLossless;

    /// <summary>A copy that saves less than this share is thrown away and the original kept.</summary>
    public int MinSavingPercent { get; init; } = 25;

    /// <summary>Compare the copy with the original (SSIM) and keep the original when it scores too low.</summary>
    public bool CheckQuality { get; init; } = true;

    public RemoveAction Action { get; init; } = RemoveAction.ReviewFolder;
    public Action<string> Recycle { get; init; } = ReviewFolder.SendToRecycleBin;
    public SavingsLedger? Ledger { get; init; }

    /// <summary>Free bytes on the drive of a path; replaceable in tests.</summary>
    public Func<string, long> FreeSpace { get; init; } = VideoReduceRunner.FreeBytes;

    public IProgress<ReduceProgress>? Progress { get; init; }
    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }
}

/// <summary>The result of a run. <see cref="Batch"/> is what "Undo last run" restores (null for the Recycle Bin).</summary>
public sealed record VideoReduceOutcome(IReadOnlyList<FileOutcome> Files, SwapBatch? Batch, bool Cancelled)
{
    public int Reduced => Files.Count(f => f.Kind == FileOutcomeKind.Reduced);
    public int Skipped => Files.Count(f => f.Kind == FileOutcomeKind.Skipped);
    public int Failed => Files.Count(f => f.Kind == FileOutcomeKind.Failed);
    public long Saved => Files.Where(f => f.Kind == FileOutcomeKind.Reduced).Sum(f => f.Before - f.After);
}

/// <summary>
/// Makes the chosen videos smaller, one at a time, never risking the original: encode to a temp file beside
/// it, read the copy back and verify it (same length, sound kept, really smaller, picture quality when
/// measured), and only then swap it in with the kit's <see cref="ShrinkSwap"/> (original to the review
/// folder or Recycle Bin, dates kept). Anything that fails a check is deleted and the original stays as it
/// was. Every swap is written to the savings history. A stop ends after cleaning up the file in progress.
/// </summary>
public static class VideoReduceRunner
{
    /// <summary>The folder originals go to, inside the scanned folder (shared with Reduce Size - Images).</summary>
    public const string ReviewFolderName = "_Reduced - originals";

    /// <summary>Extra free space required on top of the estimated copy size.</summary>
    public const long SpareBytes = 200L * 1024 * 1024;

    public static VideoReduceOutcome Run(VideoReduceJob job)
    {
        var files = new List<FileOutcome>();
        var entries = new List<SwapEntry>();
        var cancelled = false;
        for (var i = 0; i < job.Candidates.Count; i++)
        {
            if (job.Cancel.IsCancellationRequested || (job.Pause is not null && !job.Pause.Wait(job.Cancel)))
            {
                cancelled = true;
                break;
            }
            var outcome = ReduceOne(job, job.Candidates[i], i + 1, entries);
            files.Add(outcome);
            job.Log?.Report(Describe(outcome));
            if (outcome.Note == EncodeResult.Stopped.Error)
            {
                cancelled = true;
                break;
            }
        }
        var batch = entries.Count > 0 && job.Action == RemoveAction.ReviewFolder ? new SwapBatch(DateTime.UtcNow, job.Root, entries) : null;
        return new VideoReduceOutcome(files, batch, cancelled);
    }

    /// <summary>
    /// Checks the finished copy against the original. Null when it passes; otherwise the reason it is
    /// thrown away. Rotation is not compared: ffmpeg turns the picture upright, so a portrait copy may have
    /// width and height swapped.
    /// </summary>
    public static string? Verify(VideoFacts original, VideoFacts? copy, long copySize, int minSavingPercent)
    {
        if (copy is null || copy.Pixels == 0)
        {
            return "The copy could not be read back - original kept.";
        }
        var tolerance = Math.Max(1000, original.DurationMs / 100);
        if (Math.Abs(copy.DurationMs - original.DurationMs) > tolerance)
        {
            return "The copy's length differs (" + Seconds(copy.DurationMs) + " vs " + Seconds(original.DurationMs) + ") - original kept.";
        }
        if (original.HasAudio && !copy.HasAudio)
        {
            return "The copy lost the sound - original kept.";
        }
        var percent = original.Size > 0 ? 100.0 * (original.Size - copySize) / original.Size : 0;
        return percent < minSavingPercent
            ? "Saved only " + ((int)Math.Round(percent)).ToString(CultureInfo.InvariantCulture) + "% - original kept."
            : null;
    }

    /// <summary>Free bytes on the drive holding <paramref name="path"/> (0 when unknown).</summary>
    public static long FreeBytes(string path)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    /// <summary>One log line for an outcome.</summary>
    public static string Describe(FileOutcome o) => o.Kind switch
    {
        FileOutcomeKind.Reduced => "Reduced " + o.Path + ": " + ByteSize.Format(o.Before) + " -> " + ByteSize.Format(o.After) + (o.Note.Length > 0 ? " (" + o.Note + ")" : ""),
        FileOutcomeKind.Skipped => "Skipped " + o.Path + ": " + o.Note,
        _ => "Failed " + o.Path + ": " + o.Note
    };

    private static FileOutcome ReduceOne(VideoReduceJob job, VideoCandidate candidate, int index, List<SwapEntry> entries)
    {
        var facts = candidate.Facts;
        if (job.FreeSpace(facts.Path) < candidate.Advice.EstimatedBytes + SpareBytes)
        {
            return new FileOutcome(facts.Path, FileOutcomeKind.Skipped, facts.Size, facts.Size, "Not enough free space on the drive for the copy.");
        }
        var temp = ShrinkSwap.TempPathFor(facts.Path, job.Engine.OutputExtension(facts));
        try
        {
            DeleteQuietly(temp);
            var percent = new SyncProgress<int>(p => job.Progress?.Report(new ReduceProgress(index, job.Candidates.Count, facts.Path, p)));
            percent.Report(0);
            var encoded = job.Engine.Encode(new EncodeJob(facts, temp, job.Quality, candidate.Advice.TargetVideoKbps), percent, job.Cancel);
            if (!encoded.Ok)
            {
                return new FileOutcome(facts.Path, encoded.Cancelled ? FileOutcomeKind.Skipped : FileOutcomeKind.Failed, facts.Size, facts.Size, encoded.Error);
            }
            var checkedCopy = CheckCopy(job, facts, temp);
            if (checkedCopy.Problem is not null)
            {
                return new FileOutcome(facts.Path, FileOutcomeKind.Skipped, facts.Size, facts.Size, checkedCopy.Problem);
            }
            return Swap(job, facts, temp, entries, checkedCopy.Note);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new FileOutcome(facts.Path, FileOutcomeKind.Failed, facts.Size, facts.Size, ex.Message);
        }
        finally
        {
            DeleteQuietly(temp);
        }
    }

    private static (string? Problem, string Note) CheckCopy(VideoReduceJob job, VideoFacts facts, string temp)
    {
        var size = new FileInfo(temp).Length;
        if (Verify(facts, job.Engine.Probe(temp), size, job.MinSavingPercent) is { } problem)
        {
            return (problem, "");
        }
        if (!job.CheckQuality || job.Engine.Ssim(temp, facts.Path, job.Cancel) is not { } ssim)
        {
            return (null, "");
        }
        var score = "SSIM " + ssim.ToString("0.000", CultureInfo.InvariantCulture);
        return ssim < FfmpegVideoArgs.MinimumSsim(job.Quality)
            ? ("Picture quality check failed (" + score + ") - original kept.", "")
            : (null, score);
    }

    private static FileOutcome Swap(VideoReduceJob job, VideoFacts facts, string temp, List<SwapEntry> entries, string note)
    {
        var entry = ShrinkSwap.Commit(new SwapRequest
        {
            Root = job.Root,
            ReviewFolderName = ReviewFolderName,
            Original = facts.Path,
            OriginalSize = facts.Size,
            OriginalModifiedUtc = facts.ModifiedUtc,
            Reduced = temp,
            FinalPath = ShrinkSwap.FinalPathFor(facts.Path, Path.GetExtension(temp)),
            Action = job.Action,
            Recycle = job.Recycle
        });
        entries.Add(entry);
        job.Ledger?.Append(new SavingRecord(DateTime.UtcNow, entry.Reduced, entry.OriginalSize, entry.ReducedSize, job.Engine.Name + ", " + job.Quality));
        return new FileOutcome(facts.Path, FileOutcomeKind.Reduced, entry.OriginalSize, entry.ReducedSize, note);
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A temp file still held open; the next scan's leftover clean-up removes it.
        }
    }

    private static string Seconds(long ms) => (ms / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s";
}
