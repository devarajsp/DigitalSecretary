using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>
/// Everything known about one picture after a scan. <see cref="Matches"/> says whether a stored record
/// still describes the file on disk (same size and modified time), which is what makes rescans cheap.
/// </summary>
public sealed record PictureRecord
{
    public required string Path { get; init; }
    public long Size { get; init; }
    public DateTime ModifiedUtc { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public DateTime? TakenUtc { get; init; }
    public required ulong[] PHashes { get; init; }
    public required ulong[] DHashes { get; init; }
    public double Sharpness { get; init; }

    /// <summary>Pixels (width x height), or 0 when the header was unreadable.</summary>
    public long Pixels => (long)Width * Height;

    public ulong PHash => PHashes[0];
    public ulong DHash => DHashes[0];

    /// <summary>The capture time if known, otherwise the file's modified time.</summary>
    public DateTime BestTimeUtc => TakenUtc ?? ModifiedUtc;

    /// <summary>True when the file still has the size and modified time this record was made from.</summary>
    public bool Matches(long size, DateTime modifiedUtc) => Size == size && ModifiedUtc == modifiedUtc;

    /// <summary>Builds a record from a read sample and the file's own size and time.</summary>
    public static PictureRecord From(string path, long size, DateTime modifiedUtc, PictureSample sample)
    {
        var print = ImageHasher.Compute(sample.Gray);
        return new PictureRecord
        {
            Path = path,
            Size = size,
            ModifiedUtc = modifiedUtc,
            Width = sample.Width,
            Height = sample.Height,
            TakenUtc = sample.TakenUtc,
            PHashes = print.PHashes,
            DHashes = print.DHashes,
            Sharpness = print.Sharpness
        };
    }
}

/// <summary>
/// The fingerprint cache for one scanned folder: path -> <see cref="PictureRecord"/>, kept in memory
/// during a scan and saved gzip-compressed under the feature's data folder (one file per folder, named
/// by a hash of the folder path). A picture is only read and hashed again when it changed.
/// </summary>
public sealed class PictureCache
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly Dictionary<string, PictureRecord> _records = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _records.Count;

    public IEnumerable<PictureRecord> Records => _records.Values;

    /// <summary>The stored record for <paramref name="path"/> if it still matches the file.</summary>
    public PictureRecord? FindValid(string path, long size, DateTime modifiedUtc)
        => _records.TryGetValue(path, out var r) && r.Matches(size, modifiedUtc) ? r : null;

    public void Put(PictureRecord record) => _records[record.Path] = record;

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

    /// <summary>The cache file for a scanned folder under <paramref name="dataDirectory"/>.</summary>
    public static string PathFor(string dataDirectory, string root)
    {
        var key = System.IO.Path.GetFullPath(root).TrimEnd('\\', '/').ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant();
        return System.IO.Path.Combine(dataDirectory, "fingerprints", hash + ".json.gz");
    }

    /// <summary>Loads a saved cache; an empty one when missing or unreadable.</summary>
    public static PictureCache Load(string file)
    {
        var cache = new PictureCache();
        try
        {
            if (File.Exists(file))
            {
                using var stream = new GZipStream(File.OpenRead(file), CompressionMode.Decompress);
                foreach (var record in JsonSerializer.Deserialize<List<PictureRecord>>(stream, JsonOpts) ?? [])
                {
                    if (record.PHashes is { Length: 8 } && record.DHashes is { Length: 8 })
                    {
                        cache.Put(record);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return new PictureCache();
        }
        return cache;
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
}
