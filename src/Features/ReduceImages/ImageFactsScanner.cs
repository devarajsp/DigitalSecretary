namespace DigitalSecretary.Features.ReduceImages;

/// <summary>Everything one scan needs. <see cref="Cache"/> is updated in place.</summary>
public sealed class ImageFactsScanRequest
{
    public required string Root { get; init; }
    public required ImageFactsCache Cache { get; init; }

    /// <summary>Reads one picture's facts; <see cref="ImageProbe.Read"/> in the app, a fake in tests.</summary>
    public Func<FileInfo, ImageFacts?> Read { get; init; } = ImageProbe.Read;

    public IProgress<FileScanProgress>? Progress { get; init; }
    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }
    public Action<ImageFactsCache>? AutoSave { get; init; }
}

/// <summary>
/// Finds every picture under a folder and makes sure the cache holds its facts (the kit's incremental scan),
/// leaving out the originals folder and deleting unfinished copies from a run that was killed.
/// </summary>
public static class ImageFactsScanner
{
    public static FileScanResult Run(ImageFactsScanRequest request)
    {
        var review = ReviewFolder.For(request.Root, ImageReduceRunner.ReviewFolderName);
        var all = IncrementalScanner.Collect(request.Root, review, p => ShrinkSwap.IsTemp(p), request.Log, request.Cancel);
        ShrinkSwap.CleanLeftovers(all.Select(f => f.FullName), request.Log);
        return IncrementalScanner.Run(new FileScanJob<ImageFacts>
        {
            Root = request.Root,
            Cache = request.Cache,
            Include = p => ImageProbe.IsPicture(p) && !ShrinkSwap.IsTemp(p),
            Make = request.Read,
            Noun = "picture",
            ExcludeFolder = review,
            Progress = request.Progress,
            Log = request.Log,
            Pause = request.Pause,
            Cancel = request.Cancel,
            AutoSave = request.AutoSave is null ? null : _ => request.AutoSave(request.Cache),
            AutoSaveEvery = 500
        });
    }
}
