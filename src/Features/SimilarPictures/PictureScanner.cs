namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>Live progress of a picture scan.</summary>
public sealed record PictureScanProgress(int Processed, int Total, int Read, int Reused, int Percent, string Current);

/// <summary>What a picture scan did.</summary>
/// <param name="Pictures">Pictures now fingerprinted (read this time or reused from the cache).</param>
/// <param name="Read">Pictures read and hashed this time.</param>
/// <param name="Reused">Pictures whose stored fingerprint was still valid.</param>
/// <param name="Unreadable">Picture files no reader could decode.</param>
/// <param name="Removed">Cache entries dropped because the file is gone (completed scans only).</param>
/// <param name="Cancelled">True when the user stopped the scan.</param>
public sealed record PictureScanResult(int Pictures, int Read, int Reused, int Unreadable, int Removed, bool Cancelled);

/// <summary>Everything one scan needs. <see cref="Cache"/> is updated in place.</summary>
public sealed class PictureScanRequest
{
    public required string Root { get; init; }
    public required PictureCache Cache { get; init; }

    /// <summary>Reads one picture; <see cref="PictureReader.Default"/> in the app, a fake in tests.</summary>
    public required Func<string, PictureSample?> Read { get; init; }

    /// <summary>A folder (usually the review folder) whose contents are never scanned.</summary>
    public string? ExcludeFolder { get; init; }

    /// <summary>Pictures read at the same time. Reading is mostly I/O and decode, so 2 - 4 is plenty.</summary>
    public int Parallelism { get; init; } = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

    public IProgress<PictureScanProgress>? Progress { get; init; }
    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }

    /// <summary>Called every <see cref="AutoSaveEvery"/> pictures so Stop never loses finished work.</summary>
    public Action<PictureCache>? AutoSave { get; init; }
    public int AutoSaveEvery { get; init; } = 250;
}

/// <summary>
/// Finds every picture under a folder and makes sure each has a current fingerprint in the cache.
/// The incremental scan itself (unchanged files are not read again, bounded parallel reading, autosave,
/// prune only after a complete scan) is the kit's <see cref="IncrementalScanner"/>; this class supplies
/// what is picture-specific: the extensions and how one picture becomes a <see cref="PictureRecord"/>.
/// </summary>
public static class PictureScanner
{
    /// <summary>File extensions treated as pictures. Windows decides at read time whether it can decode them.</summary>
    public static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".gif", ".bmp", ".tif", ".tiff", ".heic", ".heif", ".webp", ".avif",
        ".dng", ".cr2", ".cr3", ".nef", ".arw", ".orf", ".rw2", ".raf", ".srw", ".pef"
    };

    /// <summary>True when <paramref name="path"/> has a picture extension.</summary>
    public static bool IsPicture(string path) => Extensions.Contains(Path.GetExtension(path));

    /// <summary>Every picture file under <paramref name="root"/>, skipping links/junctions and <paramref name="exclude"/>.</summary>
    public static List<FileInfo> Collect(string root, string? exclude, IProgress<string>? log, CancellationToken cancel)
        => IncrementalScanner.Collect(root, exclude, IsPicture, log, cancel);

    /// <summary>Runs the scan. Never throws for file problems; a stop returns a result marked cancelled.</summary>
    public static PictureScanResult Run(PictureScanRequest request)
    {
        var job = new FileScanJob<PictureRecord>
        {
            Root = request.Root,
            Cache = request.Cache,
            Include = IsPicture,
            Make = file => request.Read(file.FullName) is { } sample
                ? PictureRecord.From(file.FullName, file.Length, file.LastWriteTimeUtc, sample)
                : null,
            Noun = "picture",
            ExcludeFolder = request.ExcludeFolder,
            Parallelism = request.Parallelism,
            Progress = request.Progress is null ? null : new MappedProgress<FileScanProgress, PictureScanProgress>(request.Progress,
                p => new PictureScanProgress(p.Processed, p.Total, p.Read, p.Reused, p.Percent, p.Current)),
            Log = request.Log,
            Pause = request.Pause,
            Cancel = request.Cancel,
            AutoSave = request.AutoSave is null ? null : _ => request.AutoSave(request.Cache),
            AutoSaveEvery = request.AutoSaveEvery
        };
        var r = IncrementalScanner.Run(job);
        return new PictureScanResult(r.Items, r.Read, r.Reused, r.Unreadable, r.Removed, r.Cancelled);
    }
}
