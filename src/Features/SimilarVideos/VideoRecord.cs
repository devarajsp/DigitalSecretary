namespace DigitalSecretary.Features.SimilarVideos;

/// <summary>The fingerprint of one sampled frame: pHash and dHash in all eight orientations (index 0 = as it is).</summary>
public sealed record FrameHash(ulong[] P, ulong[] D);

/// <summary>
/// Everything known about one video after a scan: size, length, picture size and one
/// <see cref="FrameHash"/> per sampling point (null where the frame was flat - black, white, one colour -
/// or could not be decoded; such frames say nothing about the content and are never compared).
/// </summary>
public sealed record VideoRecord : IFileRecord
{
    /// <summary>
    /// Frames whose brightness varies less than this (standard deviation, 0 - 255 scale) are "flat":
    /// fades, black frames, title cards on one colour. Their hashes would be noise.
    /// </summary>
    public const double FlatFrameLimit = 6.0;

    public required string Path { get; init; }
    public long Size { get; init; }
    public DateTime ModifiedUtc { get; init; }
    public long DurationMs { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public required FrameHash?[] Frames { get; init; }

    /// <summary>Pixels per frame (width x height), or 0 when unknown.</summary>
    public long Pixels => (long)Width * Height;

    /// <summary>Average bit rate over the whole file in kbit/s (size / length), or 0 when the length is unknown.</summary>
    public long BitrateKbps => DurationMs > 0 ? Size * 8 / DurationMs : 0;

    /// <summary>How many sampled frames carry content (not flat, decoded).</summary>
    public int UsableFrames => Frames.Count(f => f is not null);

    /// <summary>True when the file still has the size and modified time this record was made from.</summary>
    public bool Matches(long size, DateTime modifiedUtc) => Size == size && ModifiedUtc == modifiedUtc;

    /// <summary>Builds a record from a decoded sample and the file's own size and time.</summary>
    public static VideoRecord From(string path, long size, DateTime modifiedUtc, VideoSample sample) => new()
    {
        Path = path,
        Size = size,
        ModifiedUtc = modifiedUtc,
        DurationMs = sample.DurationMs,
        Width = sample.Width,
        Height = sample.Height,
        Frames = [.. sample.Frames.Select(HashOf)]
    };

    /// <summary>The frame's hashes, or null for a missing or flat frame.</summary>
    public static FrameHash? HashOf(GrayImage? frame)
    {
        if (frame is null || frame.StandardDeviation() < FlatFrameLimit)
        {
            return null;
        }
        var print = ImageHasher.Compute(frame);
        return new FrameHash(print.PHashes, print.DHashes);
    }
}

/// <summary>
/// The fingerprint cache for one scanned folder (path -> <see cref="VideoRecord"/>), saved gzip-compressed
/// under the feature's data folder. The mechanics are the kit's <see cref="FileRecordCache{T}"/>; this type
/// adds the typed <see cref="Load"/> that rejects records not made with the current sampling plan.
/// </summary>
public sealed class VideoCache : FileRecordCache<VideoRecord>
{
    /// <summary>The cache file for a scanned folder under <paramref name="dataDirectory"/>.</summary>
    public static string PathFor(string dataDirectory, string root) => CacheFiles.PathFor(dataDirectory, root);

    /// <summary>Loads a saved cache; an empty one when missing or unreadable.</summary>
    public static VideoCache Load(string file)
    {
        var cache = new VideoCache();
        return TryLoadInto(cache, file, IsCurrent) ? cache : new VideoCache();
    }

    /// <summary>A stored record is used only if it has one entry per sampling point and eight hashes per kind.</summary>
    public static bool IsCurrent(VideoRecord r)
        => r.Frames is { Length: VideoDecoder.FrameCount }
           && r.Frames.All(f => f is null || (f.P is { Length: 8 } && f.D is { Length: 8 }));
}
