namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>
/// Everything known about one picture after a scan. <see cref="Matches"/> says whether a stored record
/// still describes the file on disk (same size and modified time), which is what makes rescans cheap.
/// </summary>
public sealed record PictureRecord : IFileRecord
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
/// The fingerprint cache for one scanned folder (path -> <see cref="PictureRecord"/>), saved gzip-compressed
/// under the feature's data folder. All the mechanics are the kit's <see cref="FileRecordCache{T}"/>; this
/// type only adds the typed <see cref="Load"/> that rejects records without eight hashes per kind.
/// </summary>
public sealed class PictureCache : FileRecordCache<PictureRecord>
{
    /// <summary>The cache file for a scanned folder under <paramref name="dataDirectory"/>.</summary>
    public static string PathFor(string dataDirectory, string root) => CacheFiles.PathFor(dataDirectory, root);

    /// <summary>Loads a saved cache; an empty one when missing or unreadable.</summary>
    public static PictureCache Load(string file)
    {
        var cache = new PictureCache();
        return TryLoadInto(cache, file, r => r.PHashes is { Length: 8 } && r.DHashes is { Length: 8 }) ? cache : new PictureCache();
    }
}
