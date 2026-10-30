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
/// Incremental: a picture whose size and modified time are unchanged is not read again. Stale cache
/// entries are pruned only after a scan that ran to the end - a stopped scan has not seen every file.
/// Pictures are read in parallel (bounded), the cache is only touched under a lock.
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

    /// <summary>
    /// Every picture file under <paramref name="root"/>, skipping links/junctions and anything inside
    /// <paramref name="exclude"/>. Unreadable folders are logged and stepped over.
    /// </summary>
    public static List<FileInfo> Collect(string root, string? exclude, IProgress<string>? log, CancellationToken cancel)
    {
        var files = new List<FileInfo>();
        var skip = exclude is null ? null : Path.GetFullPath(exclude).TrimEnd('\\', '/');
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(root));
        while (pending.Count > 0 && !cancel.IsCancellationRequested)
        {
            var dir = pending.Pop();
            try
            {
                foreach (var entry in dir.EnumerateFileSystemInfos())
                {
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                    if (entry is DirectoryInfo sub)
                    {
                        if (skip is null || !string.Equals(sub.FullName.TrimEnd('\\', '/'), skip, StringComparison.OrdinalIgnoreCase))
                        {
                            pending.Push(sub);
                        }
                    }
                    else if (entry is FileInfo file && IsPicture(file.Name))
                    {
                        files.Add(file);
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                log?.Report("Cannot read folder " + dir.FullName + ": " + ex.Message);
            }
        }
        files.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.OrdinalIgnoreCase));
        return files;
    }

    /// <summary>Runs the scan. Never throws for file problems; a stop returns a result marked cancelled.</summary>
    public static PictureScanResult Run(PictureScanRequest request)
    {
        var cancelled = request.Cancel.IsCancellationRequested;
        var files = cancelled ? [] : Collect(request.Root, request.ExcludeFolder, request.Log, request.Cancel);
        var gate = new object();
        int processed = 0, read = 0, reused = 0, unreadable = 0;

        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, request.Parallelism) };
        try
        {
            Parallel.ForEach(files, options, (file, state) =>
            {
                if (request.Cancel.IsCancellationRequested || (request.Pause is not null && !request.Pause.Wait(request.Cancel)))
                {
                    state.Stop();
                    return;
                }

                PictureRecord? record;
                lock (gate)
                {
                    record = request.Cache.FindValid(file.FullName, file.Length, file.LastWriteTimeUtc);
                }
                var fresh = record is null ? ReadOne(file, request) : null;

                lock (gate)
                {
                    if (record is not null)
                    {
                        reused++;
                    }
                    else if (fresh is not null)
                    {
                        request.Cache.Put(fresh);
                        read++;
                    }
                    else
                    {
                        unreadable++;
                    }
                    processed++;
                    Report(request, processed, files.Count, read, reused, file.FullName);
                    if (fresh is not null && read % Math.Max(1, request.AutoSaveEvery) == 0)
                    {
                        request.AutoSave?.Invoke(request.Cache);
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Treated as a stop below.
        }

        cancelled = cancelled || request.Cancel.IsCancellationRequested || processed < files.Count;
        var removed = 0;
        if (!cancelled)
        {
            removed = request.Cache.RetainOnly(files.Select(f => f.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase));
        }
        return new PictureScanResult(read + reused, read, reused, unreadable, removed, cancelled);
    }

    private static PictureRecord? ReadOne(FileInfo file, PictureScanRequest request)
    {
        try
        {
            var sample = request.Read(file.FullName);
            if (sample is null)
            {
                request.Log?.Report("Could not read picture: " + file.FullName);
                return null;
            }
            return PictureRecord.From(file.FullName, file.Length, file.LastWriteTimeUtc, sample);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            request.Log?.Report("Could not read picture " + file.FullName + ": " + ex.Message);
            return null;
        }
    }

    private static void Report(PictureScanRequest request, int processed, int total, int read, int reused, string current)
    {
        if (request.Progress is null || (processed % 10 != 0 && processed != total))
        {
            return;
        }
        var percent = total == 0 ? 100 : (int)(processed * 100L / total);
        request.Progress.Report(new PictureScanProgress(processed, total, read, reused, percent, current));
    }
}
