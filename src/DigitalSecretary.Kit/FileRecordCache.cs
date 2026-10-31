using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DigitalSecretary.Kit;

/// <summary>What a cache needs from a per-file record: its path and whether it still describes the file.</summary>
public interface IFileRecord
{
    string Path { get; }

    /// <summary>True when the file still has the size and modified time this record was made from.</summary>
    bool Matches(long size, DateTime modifiedUtc);
}

/// <summary>Where per-folder caches live.</summary>
public static class CacheFiles
{
    /// <summary>The cache file for a scanned folder: <c>&lt;data&gt;\fingerprints\&lt;hash&gt;.json.gz</c>.</summary>
    public static string PathFor(string dataDirectory, string root)
    {
        var key = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant();
        return Path.Combine(dataDirectory, "fingerprints", hash + ".json.gz");
    }
}

/// <summary>
/// A per-folder cache of per-file records (path -> record), kept in memory during a scan and saved
/// gzip-compressed (machine rule §9.3) under the feature's data folder - one file per scanned folder,
/// named by a hash of the folder path. A file is only read again when its size or modified time changed.
/// Features derive a small sealed type that adds a typed <c>Load</c> with their own validity check.
/// </summary>
public class FileRecordCache<T> where T : class, IFileRecord
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly Dictionary<string, T> _records = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _records.Count;

    public IEnumerable<T> Records => _records.Values;

    /// <summary>The stored record for <paramref name="path"/> if it still matches the file.</summary>
    public T? FindValid(string path, long size, DateTime modifiedUtc)
        => _records.TryGetValue(path, out var r) && r.Matches(size, modifiedUtc) ? r : null;

    public void Put(T record) => _records[record.Path] = record;

    public bool Remove(string path) => _records.Remove(path);

    /// <summary>Drops every record whose path is not in <paramref name="live"/>; returns how many went.</summary>
    public int RetainOnly(ISet<string> live)
    {
        var stale = _records.Keys.Where(k => !live.Contains(k)).ToList();
        foreach (var key in stale)
        {
            _records.Remove(key);
        }
        return stale.Count;
    }

    /// <summary>Saves atomically (write to a temp file, then replace).</summary>
    public void Save(string file)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        var temp = file + ".tmp";
        using (var stream = new GZipStream(File.Create(temp), CompressionLevel.SmallestSize))
        {
            JsonSerializer.Serialize(stream, _records.Values.ToList(), JsonOpts);
        }
        File.Move(temp, file, overwrite: true);
    }

    /// <summary>
    /// Fills <paramref name="cache"/> from a saved file, keeping only records <paramref name="isValid"/>
    /// accepts. Returns false (and leaves the cache to be discarded) when the file is unreadable;
    /// a missing file is simply an empty cache.
    /// </summary>
    protected static bool TryLoadInto(FileRecordCache<T> cache, string file, Func<T, bool> isValid)
    {
        try
        {
            if (File.Exists(file))
            {
                using var stream = new GZipStream(File.OpenRead(file), CompressionMode.Decompress);
                foreach (var record in JsonSerializer.Deserialize<List<T>>(stream, JsonOpts) ?? [])
                {
                    if (record is not null && isValid(record))
                    {
                        cache.Put(record);
                    }
                }
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
