namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>
/// What a scan learns about one video: size, length, picture size, frame rate, codec, colour depth / HDR,
/// audio and rotation. Read by ffprobe when ffmpeg is installed, otherwise by Windows Media Foundation
/// (<see cref="MfVideoProbe"/>). Everything the advisor needs to judge whether a smaller copy is worth making.
/// </summary>
public sealed record VideoFacts : IFileRecord
{
    public required string Path { get; init; }
    public long Size { get; init; }
    public DateTime ModifiedUtc { get; init; }
    public long DurationMs { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public double Fps { get; init; }

    /// <summary>Video codec in ffmpeg's lower-case naming ("h264", "hevc", "av1", "mpeg4", ...), or "" when unknown.</summary>
    public string Codec { get; init; } = "";

    /// <summary>Bits per colour sample (8 for almost all videos, 10 for HDR and some phone modes).</summary>
    public int BitDepth { get; init; } = 8;

    /// <summary>HDR (PQ or HLG transfer). Re-encoding HDR without care loses the colours, so it is skipped by default.</summary>
    public bool IsHdr { get; init; }

    public bool HasAudio { get; init; }

    /// <summary>Audio codec in ffmpeg's naming ("aac", "pcm_s16le", ...), or "" (none / unknown).</summary>
    public string AudioCodec { get; init; } = "";

    /// <summary>Clockwise rotation from the file's rotation tag (0, 90, 180, 270).</summary>
    public int Rotation { get; init; }

    /// <summary>Which reader produced these facts ("ffprobe" or "windows").</summary>
    public string ReadBy { get; init; } = "";

    /// <summary>Pixels per frame.</summary>
    public long Pixels => (long)Width * Height;

    /// <summary>Average bit rate over the whole file (video + audio) in kbit/s.</summary>
    public long BitrateKbps => DurationMs > 0 ? Size * 8 / DurationMs : 0;

    /// <summary>
    /// Bits spent per pixel per frame - the "how generously encoded" number the advice is based on.
    /// Phones record H.264 at about 0.2-0.4; HEVC looks the same at about 0.05-0.08.
    /// </summary>
    public double BitsPerPixel => Pixels > 0 && Fps > 0 && DurationMs > 0 ? Size * 8.0 / (Pixels * Fps * DurationMs / 1000.0) : 0;

    public bool Matches(long size, DateTime modifiedUtc) => Size == size && ModifiedUtc == modifiedUtc;
}

/// <summary>Per-folder cache of <see cref="VideoFacts"/> (gzip, under the feature's data folder).</summary>
public sealed class VideoFactsCache : FileRecordCache<VideoFacts>
{
    public static string PathFor(string dataDirectory, string root) => CacheFiles.PathFor(dataDirectory, root);

    public static VideoFactsCache Load(string file)
    {
        var cache = new VideoFactsCache();
        return TryLoadInto(cache, file, f => f.DurationMs > 0 && f.Width > 0) ? cache : new VideoFactsCache();
    }
}

/// <summary>Codec names: the friendly label, and which codecs are already efficient.</summary>
public static class Codecs
{
    /// <summary>Codecs that already compress about as well as what we would make; re-encoding gains little.</summary>
    public static readonly IReadOnlySet<string> Modern = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "hevc", "av1", "vp9", "vvc" };

    /// <summary>A label for the list, e.g. "H.264", "HEVC (H.265)".</summary>
    public static string Label(string codec) => codec.ToLowerInvariant() switch
    {
        "h264" => "H.264",
        "hevc" => "HEVC (H.265)",
        "av1" => "AV1",
        "vp9" => "VP9",
        "vp8" => "VP8",
        "mpeg4" => "MPEG-4",
        "mpeg2video" => "MPEG-2",
        "mpeg1video" => "MPEG-1",
        "mjpeg" => "Motion JPEG",
        "wmv1" or "wmv2" or "wmv3" or "vc1" => "Windows Media",
        "prores" => "ProRes",
        "dvvideo" => "DV",
        "rawvideo" => "Uncompressed",
        "" => "?",
        _ => codec.ToUpperInvariant()
    };

    /// <summary>
    /// The ffmpeg codec name for a Media Foundation video subtype. Video subtypes are FourCC GUIDs
    /// (the FourCC is the first 4 bytes), except MPEG-2 which has a GUID of its own.
    /// </summary>
    public static string FromMediaFoundation(Guid subtype)
    {
        if (subtype == new Guid("e06d8026-db46-11cf-b4d1-00805f6cbbea"))
        {
            return "mpeg2video";
        }
        var bytes = subtype.ToByteArray();
        var fourCc = new string([.. bytes.Take(4).Select(b => (char)b)]).Trim('\0').ToUpperInvariant();
        return fourCc switch
        {
            "H264" or "AVC1" => "h264",
            "HEVC" or "HVC1" or "HEV1" => "hevc",
            "AV01" => "av1",
            "VP90" => "vp9",
            "VP80" => "vp8",
            "MP4V" or "M4S2" or "MP43" or "XVID" or "DIVX" => "mpeg4",
            "MJPG" => "mjpeg",
            "WMV1" => "wmv1",
            "WMV2" => "wmv2",
            "WMV3" => "wmv3",
            "WVC1" => "vc1",
            "DVSD" or "DVHD" or "DV25" or "DV50" => "dvvideo",
            _ => fourCc.ToLowerInvariant()
        };
    }
}
