namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>Everything one scan needs. <see cref="Cache"/> is updated in place.</summary>
public sealed class VideoFactsScanRequest
{
    public required string Root { get; init; }
    public required VideoFactsCache Cache { get; init; }

    /// <summary>Reads one video's facts: ffprobe when installed, Windows otherwise; a fake in tests.</summary>
    public required Func<FileInfo, VideoFacts?> Read { get; init; }

    public IProgress<FileScanProgress>? Progress { get; init; }
    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }
    public Action<VideoFactsCache>? AutoSave { get; init; }
}

/// <summary>
/// Finds every video under a folder and makes sure the cache holds its facts. The incremental scan
/// (unchanged files are not read again, autosave, prune only after a complete scan) is the kit's
/// <see cref="IncrementalScanner"/>; this class adds the video extensions and leaves out the originals
/// folder and unfinished copies from a run that was killed (which it deletes).
/// </summary>
public static class VideoFactsScanner
{
    public static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".avi", ".wmv", ".asf", ".mkv", ".webm", ".3gp", ".3g2", ".mts", ".m2ts", ".ts", ".mpg", ".mpeg", ".flv"
    };

    public static bool IsVideo(string path) => Extensions.Contains(Path.GetExtension(path));

    public static FileScanResult Run(VideoFactsScanRequest request)
    {
        var review = ReviewFolder.For(request.Root, VideoReduceRunner.ReviewFolderName);
        var leftovers = IncrementalScanner.Collect(request.Root, review, p => IsVideo(p) && ShrinkSwap.IsTemp(p), request.Log, request.Cancel);
        ShrinkSwap.CleanLeftovers(leftovers.Select(f => f.FullName), request.Log);
        return IncrementalScanner.Run(new FileScanJob<VideoFacts>
        {
            Root = request.Root,
            Cache = request.Cache,
            Include = p => IsVideo(p) && !ShrinkSwap.IsTemp(p),
            Make = request.Read,
            Noun = "video",
            ExcludeFolder = review,
            Parallelism = 2,
            Progress = request.Progress,
            Log = request.Log,
            Pause = request.Pause,
            Cancel = request.Cancel,
            AutoSave = request.AutoSave is null ? null : _ => request.AutoSave(request.Cache),
            AutoSaveEvery = 25
        });
    }
}
