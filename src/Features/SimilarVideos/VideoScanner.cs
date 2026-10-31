namespace DigitalSecretary.Features.SimilarVideos;

/// <summary>Everything one video scan needs. <see cref="Cache"/> is updated in place.</summary>
public sealed class VideoScanRequest
{
    public required string Root { get; init; }
    public required VideoCache Cache { get; init; }

    /// <summary>Decodes one video; <see cref="VideoDecoder.Read"/> in the app, a fake in tests.</summary>
    public required Func<string, VideoSample?> Read { get; init; }

    /// <summary>A folder (usually the review folder) whose contents are never scanned.</summary>
    public string? ExcludeFolder { get; init; }

    /// <summary>Leave out the short .MOV clips that belong to iPhone Live Photos (on by default).</summary>
    public bool SkipLivePhotos { get; init; } = true;

    /// <summary>Videos decoded at the same time. Decoding is heavy (CPU / GPU), so 2 is the default.</summary>
    public int Parallelism { get; init; } = Math.Clamp(Environment.ProcessorCount / 4, 1, 2);

    public IProgress<FileScanProgress>? Progress { get; init; }
    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }

    /// <summary>Called every <see cref="AutoSaveEvery"/> videos so Stop never loses finished work.</summary>
    public Action<VideoCache>? AutoSave { get; init; }
    public int AutoSaveEvery { get; init; } = 25;
}

/// <summary>
/// Finds every video under a folder and makes sure each has a current fingerprint in the cache. The
/// incremental scan (unchanged files are not decoded again, bounded parallel, autosave, prune only after a
/// complete scan) is the kit's <see cref="IncrementalScanner"/>; this class adds what is video-specific:
/// the extensions, the Live Photo rule and how a decoded sample becomes a <see cref="VideoRecord"/>.
/// </summary>
public static class VideoScanner
{
    /// <summary>File extensions treated as videos. Windows decides at decode time whether it can play them.</summary>
    public static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".avi", ".wmv", ".asf", ".mkv", ".webm", ".3gp", ".3g2", ".mts", ".m2ts", ".ts", ".mpg", ".mpeg"
    };

    private static readonly string[] LivePhotoStills = [".heic", ".heif", ".jpg", ".jpeg"];

    /// <summary>True when <paramref name="path"/> has a video extension.</summary>
    public static bool IsVideo(string path) => Extensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// True for the motion part of an iPhone Live Photo: a .MOV with a still picture of the same name
    /// next to it (IMG_1234.MOV beside IMG_1234.HEIC). Moving it away would break the Live Photo.
    /// </summary>
    public static bool IsLivePhotoCompanion(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".mov", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var stem = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path));
        return LivePhotoStills.Any(ext => File.Exists(stem + ext));
    }

    /// <summary>Every video under <paramref name="root"/> this scan covers, sorted by path.</summary>
    public static List<FileInfo> Collect(string root, string? exclude, bool skipLivePhotos, IProgress<string>? log, CancellationToken cancel)
        => IncrementalScanner.Collect(root, exclude, p => Include(p, skipLivePhotos), log, cancel);

    /// <summary>Runs the scan. Never throws for file problems; a stop returns a result marked cancelled.</summary>
    public static FileScanResult Run(VideoScanRequest request) => IncrementalScanner.Run(new FileScanJob<VideoRecord>
    {
        Root = request.Root,
        Cache = request.Cache,
        Include = p => Include(p, request.SkipLivePhotos),
        Make = file => request.Read(file.FullName) is { } sample
            ? VideoRecord.From(file.FullName, file.Length, file.LastWriteTimeUtc, sample)
            : null,
        Noun = "video",
        ExcludeFolder = request.ExcludeFolder,
        Parallelism = request.Parallelism,
        Progress = request.Progress,
        Log = request.Log,
        Pause = request.Pause,
        Cancel = request.Cancel,
        AutoSave = request.AutoSave is null ? null : _ => request.AutoSave(request.Cache),
        AutoSaveEvery = request.AutoSaveEvery
    });

    private static bool Include(string path, bool skipLivePhotos) => IsVideo(path) && !(skipLivePhotos && IsLivePhotoCompanion(path));
}
