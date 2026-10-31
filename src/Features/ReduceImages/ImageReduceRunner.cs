using System.Globalization;

namespace DigitalSecretary.Features.ReduceImages;

/// <summary>A picture the user chose, with the advice it was chosen on.</summary>
public sealed record ImageCandidate(ImageFacts Facts, ImageAdvice Advice);

/// <summary>Where a run is: picture <see cref="Index"/> (1-based) of <see cref="Total"/>.</summary>
public sealed record ImageProgress(int Index, int Total, string Path);

/// <summary>Everything one "Reduce selected" run needs.</summary>
public sealed record ImageReduceJob
{
    public required string Root { get; init; }
    public required IReadOnlyList<ImageCandidate> Candidates { get; init; }
    public ImageQuality Quality { get; init; } = ImageQuality.VisuallyLossless;
    public int MinSavingPercent { get; init; } = 20;

    /// <summary>ffmpeg, needed only for WebP / AVIF.</summary>
    public MediaToolPaths? Tools { get; init; }

    public RemoveAction Action { get; init; } = RemoveAction.ReviewFolder;
    public Action<string> Recycle { get; init; } = ReviewFolder.SendToRecycleBin;
    public SavingsLedger? Ledger { get; init; }

    /// <summary>Makes the copy (null = done, else the reason); <see cref="ImageEncoder.Encode"/> in the app, a fake in tests.</summary>
    public Func<ImageCandidate, string, string?>? Encode { get; init; }

    public IProgress<ImageProgress>? Progress { get; init; }
    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }
}

/// <summary>The result of a run. <see cref="Batch"/> is what "Undo last run" restores (null for the Recycle Bin).</summary>
public sealed record ImageReduceOutcome(IReadOnlyList<FileOutcome> Files, SwapBatch? Batch, bool Cancelled)
{
    public int Reduced => Files.Count(f => f.Kind == FileOutcomeKind.Reduced);
    public int Skipped => Files.Count(f => f.Kind == FileOutcomeKind.Skipped);
    public int Failed => Files.Count(f => f.Kind == FileOutcomeKind.Failed);
    public long Saved => Files.Where(f => f.Kind == FileOutcomeKind.Reduced).Sum(f => f.Before - f.After);
}

/// <summary>
/// Makes the chosen pictures smaller one by one, never risking the original: write the copy beside it, check
/// it opens, is really smaller and - for same-size copies - that its picture quality (PSNR) is above the limit,
/// then swap it in with the kit's <see cref="ShrinkSwap"/> (original to the review folder or Recycle Bin,
/// dates kept) and record the saving. Anything that fails a check is deleted and the original stays.
/// </summary>
public static class ImageReduceRunner
{
    /// <summary>The folder originals go to, inside the scanned folder (shared with Reduce Size - Videos).</summary>
    public const string ReviewFolderName = "_Reduced - originals";

    public static ImageReduceOutcome Run(ImageReduceJob job)
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
            var candidate = job.Candidates[i];
            job.Progress?.Report(new ImageProgress(i + 1, job.Candidates.Count, candidate.Facts.Path));
            var outcome = ReduceOne(job, candidate, entries);
            files.Add(outcome);
            if (outcome.Kind != FileOutcomeKind.Reduced)
            {
                job.Log?.Report(Describe(outcome));
            }
        }
        var batch = entries.Count > 0 && job.Action == RemoveAction.ReviewFolder ? new SwapBatch(DateTime.UtcNow, job.Root, entries) : null;
        return new ImageReduceOutcome(files, batch, cancelled);
    }

    /// <summary>
    /// Checks the copy: it must open, have the planned size in pixels (WebP / AVIF are not opened here), save
    /// at least the minimum and - when it is the same size - keep its picture quality. Null when it passes.
    /// </summary>
    public static string? Verify(ImageCandidate c, string copy, long copySize, ImageQuality quality, int minSavingPercent, Func<string, string, double?> psnr)
    {
        var percent = 100.0 * (c.Facts.Size - copySize) / Math.Max(1, c.Facts.Size);
        if (percent < minSavingPercent)
        {
            return "Saved only " + ((int)Math.Round(percent)).ToString(CultureInfo.InvariantCulture) + "% - original kept.";
        }
        if (c.Advice.Action == ImageAction.ToModern || c.Advice.TargetWidth != c.Facts.Width)
        {
            return null;
        }
        var score = psnr(c.Facts.Path, copy);
        if (score is null)
        {
            return "The copy could not be opened and compared - original kept.";
        }
        return score < ImageQualityCheck.MinimumPsnr(quality)
            ? "Picture quality check failed (PSNR " + score.Value.ToString("0.0", CultureInfo.InvariantCulture) + " dB) - original kept."
            : null;
    }

    public static string Describe(FileOutcome o) => o.Kind switch
    {
        FileOutcomeKind.Reduced => "Reduced " + o.Path + ": " + ByteSize.Format(o.Before) + " -> " + ByteSize.Format(o.After),
        FileOutcomeKind.Skipped => "Skipped " + o.Path + ": " + o.Note,
        _ => "Failed " + o.Path + ": " + o.Note
    };

    private static FileOutcome ReduceOne(ImageReduceJob job, ImageCandidate c, List<SwapEntry> entries)
    {
        var f = c.Facts;
        var temp = ShrinkSwap.TempPathFor(f.Path, c.Advice.Extension);
        try
        {
            DeleteQuietly(temp);
            var encode = job.Encode ?? ((cand, output) => ImageEncoder.Encode(cand.Facts, cand.Advice, job.Quality, output, job.Tools, job.Cancel));
            if (encode(c, temp) is { } error)
            {
                return new FileOutcome(f.Path, FileOutcomeKind.Failed, f.Size, f.Size, error);
            }
            var size = new FileInfo(temp).Length;
            if (Verify(c, temp, size, job.Quality, job.MinSavingPercent, ImageQualityCheck.Compare) is { } problem)
            {
                return new FileOutcome(f.Path, FileOutcomeKind.Skipped, f.Size, f.Size, problem);
            }
            var entry = ShrinkSwap.Commit(new SwapRequest
            {
                Root = job.Root,
                ReviewFolderName = ReviewFolderName,
                Original = f.Path,
                OriginalSize = f.Size,
                OriginalModifiedUtc = f.ModifiedUtc,
                Reduced = temp,
                FinalPath = ShrinkSwap.FinalPathFor(f.Path, KeepJpegSpelling(f.Path, c.Advice.Extension)),
                Action = job.Action,
                Recycle = job.Recycle
            });
            entries.Add(entry);
            job.Ledger?.Append(new SavingRecord(DateTime.UtcNow, entry.Reduced, entry.OriginalSize, entry.ReducedSize, c.Advice.Action + ", " + job.Quality));
            return new FileOutcome(f.Path, FileOutcomeKind.Reduced, entry.OriginalSize, entry.ReducedSize, "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new FileOutcome(f.Path, FileOutcomeKind.Failed, f.Size, f.Size, ex.Message);
        }
        finally
        {
            DeleteQuietly(temp);
        }
    }

    /// <summary>A re-compressed ".jpeg" / ".JPG" keeps its own spelling, so its name does not change.</summary>
    public static string KeepJpegSpelling(string path, string extension)
    {
        var own = Path.GetExtension(path);
        return extension == ".jpg" && own.ToLowerInvariant() is ".jpg" or ".jpeg" or ".jfif" or ".jpe" ? own : extension;
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still held open; the next scan's leftover clean-up removes it.
        }
    }
}
