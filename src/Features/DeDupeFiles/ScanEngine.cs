namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>Live progress of a scan.</summary>
public sealed record ScanProgress(
    int Processed, int Total, int Hashed, int Reused, long BytesHashed, int Percent, string Current);

/// <summary>What a scan did.</summary>
/// <param name="FilesSeen">Files found in the tree (excluding the checksum list itself).</param>
/// <param name="Hashed">Files whose checksum had to be computed.</param>
/// <param name="Reused">Files whose stored checksum was still valid, so nothing was read.</param>
/// <param name="Skipped">Files that vanished mid-scan or were cancelled part-way through.</param>
/// <param name="Removed">Stale list entries pruned because the file no longer exists.</param>
/// <param name="Errors">Files that could not be read (locked, no permission).</param>
/// <param name="TotalBytes">Total size of everything now in the index.</param>
/// <param name="Cancelled">True when the user stopped the scan before it finished.</param>
public sealed record ScanResult(
    int FilesSeen, int Hashed, int Reused, int Skipped, int Removed, int Errors,
    long TotalBytes, bool Cancelled);

/// <summary>Everything one scan needs. <see cref="Index"/> is updated in place.</summary>
public sealed class ScanRequest
{
    /// <summary>Folder to walk. Its checksum list is the one being maintained.</summary>
    public required string Root { get; init; }

    /// <summary>The in-memory checksum list, pre-loaded from disk by the caller.</summary>
    public required ChecksumIndex Index { get; init; }

    /// <summary>Re-hash every file even when its size and stamp are unchanged.</summary>
    public bool Rehash { get; init; }

    public IProgress<ScanProgress>? Progress { get; init; }
    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }

    /// <summary>
    /// Called every <see cref="AutoSaveEvery"/> files so a stopped scan keeps the work it has
    /// already done - the point of the checksum list is never hashing the same file twice.
    /// </summary>
    public Action<ChecksumIndex>? AutoSave { get; init; }

    public int AutoSaveEvery { get; init; } = 500;

    public CancellationToken Cancel { get; init; }
}

/// <summary>
/// Walks a folder tree and fills the in-memory checksum index. The scan is <b>incremental</b>: a
/// file whose size and last-modified time still match the stored entry is not read at all, so a
/// second run over a large folder costs almost nothing. Entries for files that have disappeared are
/// pruned - but only when the scan ran to completion, since a stopped scan has not seen every file.
/// </summary>
public sealed class ScanEngine
{
    private const int ProgressEvery = 20;

    private readonly IFileHasher _hasher;

    public ScanEngine(IFileHasher hasher) => _hasher = hasher;

    private enum FileOutcome
    {
        Reused,
        Hashed,
        Skipped,
        Error
    }

    /// <summary>
    /// Finds every file under <paramref name="root"/>, skipping the checksum list itself and any
    /// reparse point (symlink / junction) so the walk cannot loop or wander outside the folder.
    /// Unreadable folders are logged and stepped over rather than aborting the scan.
    /// </summary>
    public static IReadOnlyList<FileInfo> Collect(string root, IProgress<string>? log, CancellationToken cancel)
    {
        var files = new List<FileInfo>();
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(root));

        while (pending.Count > 0 && !cancel.IsCancellationRequested)
        {
            var dir = pending.Pop();
            try
            {
                foreach (var sub in dir.EnumerateDirectories())
                {
                    if ((sub.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        log?.Report("Skipped link: " + sub.FullName);
                        continue;
                    }
                    pending.Push(sub);
                }

                foreach (var file in dir.EnumerateFiles())
                {
                    if (ChecksumListFile.IsListFile(file.FullName)
                        || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                    files.Add(file);
                }
            }
            catch (UnauthorizedAccessException)
            {
                log?.Report("No access: " + dir.FullName);
            }
            catch (DirectoryNotFoundException)
            {
                log?.Report("Folder vanished: " + dir.FullName);
            }
            catch (IOException ex)
            {
                log?.Report("Cannot read " + dir.FullName + ": " + ex.Message);
            }
        }

        return files;
    }

    /// <summary>Builds or refreshes the checksum index for the requested folder.</summary>
    public ScanResult Run(ScanRequest request)
    {
        var root = Path.GetFullPath(request.Root);
        request.Log?.Report("Scanning " + root + " ...");

        var files = Collect(root, request.Log, request.Cancel);
        request.Log?.Report("Found " + files.Count + " file(s). Hashing new or changed files...");

        var live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tally = new int[4];
        var processed = 0;
        long bytesHashed = 0;

        // Seed this from the token, not from false: Collect() stops early when cancellation arrives
        // during the walk and can return an empty or partial list, in which case the loop below may
        // never run. Without this, a scan cancelled before it saw anything would reach the prune
        // step with an empty "live" set and wipe the whole checksum list.
        var cancelled = request.Cancel.IsCancellationRequested;

        foreach (var file in files)
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
            live.Add(file.FullName);
            tally[(int)Fingerprint(file, request, out var hashedBytes)]++;
            bytesHashed += hashedBytes;

            if (request.AutoSave is not null && processed % request.AutoSaveEvery == 0)
            {
                request.AutoSave(request.Index);
                request.Log?.Report("Saved progress (" + request.Index.Count + " checksums).");
            }

            if (processed % ProgressEvery == 0 || processed == files.Count)
            {
                request.Progress?.Report(new ScanProgress(
                    processed, files.Count, tally[(int)FileOutcome.Hashed], tally[(int)FileOutcome.Reused],
                    bytesHashed, Percent(processed, files.Count), file.FullName));
            }
        }

        IReadOnlyList<string> removed = cancelled ? [] : request.Index.RetainOnly(live);
        if (removed.Count > 0)
        {
            request.Log?.Report("Pruned " + removed.Count + " entry(ies) for files that no longer exist.");
        }

        return new ScanResult(
            files.Count, tally[(int)FileOutcome.Hashed], tally[(int)FileOutcome.Reused],
            tally[(int)FileOutcome.Skipped], removed.Count, tally[(int)FileOutcome.Error],
            request.Index.TotalBytes, cancelled);
    }

    private static int Percent(int done, int total)
        => total <= 0 ? 100 : (int)Math.Clamp(done * 100L / total, 0, 100);

    /// <summary>
    /// Reuses the stored checksum when the file is unchanged, otherwise hashes it and updates the
    /// index. A file that cannot be read is reported and stepped over - one locked file must never
    /// stop a long scan.
    /// </summary>
    private FileOutcome Fingerprint(FileInfo file, ScanRequest request, out long hashedBytes)
    {
        hashedBytes = 0;
        try
        {
            file.Refresh();
            if (!file.Exists)
            {
                return FileOutcome.Skipped;
            }

            var existing = request.Index.Find(file.FullName);
            if (!request.Rehash && existing is not null && existing.Matches(file.Length, file.LastWriteTimeUtc))
            {
                return FileOutcome.Reused;
            }

            var md5 = _hasher.Compute(file.FullName, request.Cancel);
            if (md5 is null)
            {
                return FileOutcome.Skipped;
            }

            request.Index.Put(ChecksumEntry.FromFile(file, md5));
            hashedBytes = file.Length;
            return FileOutcome.Hashed;
        }
        catch (UnauthorizedAccessException)
        {
            request.Log?.Report("No access: " + file.FullName);
            return FileOutcome.Error;
        }
        catch (IOException ex)
        {
            request.Log?.Report("Skipped " + file.FullName + ": " + ex.Message);
            return FileOutcome.Error;
        }
    }
}
