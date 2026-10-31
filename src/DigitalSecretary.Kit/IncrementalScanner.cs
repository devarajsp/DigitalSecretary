namespace DigitalSecretary.Kit;

/// <summary>Live progress of an incremental scan.</summary>
public sealed record FileScanProgress(int Processed, int Total, int Read, int Reused, int Percent, string Current);

/// <summary>What an incremental scan did.</summary>
/// <param name="Items">Files now with a current record (read this time or reused from the cache).</param>
/// <param name="Read">Files read this time.</param>
/// <param name="Reused">Files whose stored record was still valid.</param>
/// <param name="Unreadable">Files the reader could not make a record for.</param>
/// <param name="Removed">Cache entries dropped because the file is gone (completed scans only).</param>
/// <param name="Cancelled">True when the user stopped the scan.</param>
public sealed record FileScanResult(int Items, int Read, int Reused, int Unreadable, int Removed, bool Cancelled);

/// <summary>Everything one scan needs. <see cref="Cache"/> is updated in place.</summary>
public sealed class FileScanJob<T> where T : class, IFileRecord
{
    public required string Root { get; init; }
    public required FileRecordCache<T> Cache { get; init; }

    /// <summary>Which files (by full path) belong to this scan - e.g. picture or video extensions.</summary>
    public required Func<string, bool> Include { get; init; }

    /// <summary>Makes a record for one file, or null when it cannot be read. May throw I/O errors.</summary>
    public required Func<FileInfo, T?> Make { get; init; }

    /// <summary>"picture" / "video" - used in log lines only.</summary>
    public string Noun { get; init; } = "file";

    /// <summary>A folder (usually the review folder) whose contents are never scanned.</summary>
    public string? ExcludeFolder { get; init; }

    public int Parallelism { get; init; } = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

    public IProgress<FileScanProgress>? Progress { get; init; }
    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }

    /// <summary>Called every <see cref="AutoSaveEvery"/> fresh records so Stop never loses finished work.</summary>
    public Action<FileRecordCache<T>>? AutoSave { get; init; }
    public int AutoSaveEvery { get; init; } = 250;
}

/// <summary>
/// Finds every matching file under a folder and makes sure each has a current record in the cache.
/// Incremental: a file whose size and modified time are unchanged is not read again. Stale cache entries
/// are pruned only after a scan that ran to the end - a stopped scan has not seen every file. Files are
/// read in parallel (bounded); the cache is only touched under a lock. Never throws for file problems.
/// </summary>
public static class IncrementalScanner
{
    /// <summary>
    /// Every file under <paramref name="root"/> that <paramref name="include"/> accepts, sorted by path,
    /// skipping links/junctions and anything inside <paramref name="exclude"/>. Unreadable folders are
    /// logged and stepped over.
    /// </summary>
    public static List<FileInfo> Collect(string root, string? exclude, Func<string, bool> include, IProgress<string>? log, CancellationToken cancel)
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
                    else if (entry is FileInfo file && include(file.FullName))
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

    /// <summary>Runs the scan. A stop returns a result marked cancelled.</summary>
    public static FileScanResult Run<T>(FileScanJob<T> job) where T : class, IFileRecord
    {
        var cancelled = job.Cancel.IsCancellationRequested;
        var files = cancelled ? [] : Collect(job.Root, job.ExcludeFolder, job.Include, job.Log, job.Cancel);
        var tally = new Tally();

        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, job.Parallelism) };
        try
        {
            Parallel.ForEach(files, options, (file, state) =>
            {
                if (job.Cancel.IsCancellationRequested || (job.Pause is not null && !job.Pause.Wait(job.Cancel)))
                {
                    state.Stop();
                    return;
                }
                ScanOne(job, file, files.Count, tally);
            });
        }
        catch (OperationCanceledException)
        {
            // Treated as a stop below.
        }

        cancelled = cancelled || job.Cancel.IsCancellationRequested || tally.Processed < files.Count;
        var removed = cancelled ? 0 : job.Cache.RetainOnly(files.Select(f => f.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase));
        return new FileScanResult(tally.Read + tally.Reused, tally.Read, tally.Reused, tally.Unreadable, removed, cancelled);
    }

    private sealed class Tally
    {
        public int Processed;
        public int Read;
        public int Reused;
        public int Unreadable;
    }

    private static void ScanOne<T>(FileScanJob<T> job, FileInfo file, int total, Tally tally) where T : class, IFileRecord
    {
        T? record;
        lock (tally)
        {
            record = job.Cache.FindValid(file.FullName, file.Length, file.LastWriteTimeUtc);
        }
        var fresh = record is null ? ReadOne(job, file) : null;

        lock (tally)
        {
            if (record is not null)
            {
                tally.Reused++;
            }
            else if (fresh is not null)
            {
                job.Cache.Put(fresh);
                tally.Read++;
            }
            else
            {
                tally.Unreadable++;
            }
            tally.Processed++;
            Report(job.Progress, tally, total, file.FullName);
            if (fresh is not null && tally.Read % Math.Max(1, job.AutoSaveEvery) == 0)
            {
                job.AutoSave?.Invoke(job.Cache);
            }
        }
    }

    private static T? ReadOne<T>(FileScanJob<T> job, FileInfo file) where T : class, IFileRecord
    {
        try
        {
            var record = job.Make(file);
            if (record is null)
            {
                job.Log?.Report("Could not read " + job.Noun + ": " + file.FullName);
            }
            return record;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            job.Log?.Report("Could not read " + job.Noun + " " + file.FullName + ": " + ex.Message);
            return null;
        }
    }

    private static void Report(IProgress<FileScanProgress>? progress, Tally tally, int total, string current)
    {
        if (progress is null || (tally.Processed % 10 != 0 && tally.Processed != total))
        {
            return;
        }
        var percent = total == 0 ? 100 : (int)(tally.Processed * 100L / total);
        progress.Report(new FileScanProgress(tally.Processed, total, tally.Read, tally.Reused, percent, current));
    }
}

/// <summary>An <see cref="IProgress{T}"/> that maps each report to another type and passes it on synchronously.</summary>
public sealed class MappedProgress<TFrom, TTo>(IProgress<TTo> target, Func<TFrom, TTo> map) : IProgress<TFrom>
{
    public void Report(TFrom value) => target.Report(map(value));
}
