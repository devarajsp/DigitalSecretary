using System.Globalization;
using System.Text.Json;

namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>
/// Reads <see cref="VideoFacts"/> with ffprobe (JSON output). The parsing is pure (<see cref="Parse"/>) so it
/// is tested with recorded ffprobe output; <see cref="Read"/> only runs the tool.
/// </summary>
public static class FfprobeReader
{
    /// <summary>The ffprobe arguments: streams + format as JSON, quietly.</summary>
    public static IReadOnlyList<string> Args(string path)
        => ["-v", "error", "-print_format", "json", "-show_format", "-show_streams", path];

    /// <summary>Probes one file; null when ffprobe cannot read it as a video.</summary>
    public static VideoFacts? Read(string ffprobe, FileInfo file, CancellationToken cancel = default)
    {
        var result = ToolRunner.Run(ffprobe, Args(file.FullName), null, cancel);
        return result.Succeeded ? Parse(result.Output, file.FullName, file.Length, file.LastWriteTimeUtc) : null;
    }

    /// <summary>Turns ffprobe JSON into facts; null when there is no video stream or no length.</summary>
    public static VideoFacts? Parse(string json, string path, long size, DateTime modifiedUtc)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("streams", out var streams))
            {
                return null;
            }
            var video = streams.EnumerateArray().FirstOrDefault(s => Str(s, "codec_type") == "video" && !IsCoverArt(s));
            if (video.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var audio = streams.EnumerateArray().FirstOrDefault(s => Str(s, "codec_type") == "audio");
            var duration = Seconds(root.TryGetProperty("format", out var format) ? Str(format, "duration") : "");
            if (duration <= 0)
            {
                duration = Seconds(Str(video, "duration"));
            }
            return duration <= 0 ? null : Build(video, audio, path, size, modifiedUtc, duration);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>"30000/1001" -> 29.97; "0/0" or junk -> 0.</summary>
    public static double Rate(string text)
    {
        var parts = text.Split('/');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            && d > 0)
        {
            return n / d;
        }
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static VideoFacts Build(JsonElement video, JsonElement audio, string path, long size, DateTime modifiedUtc, double duration)
    {
        var pixFmt = Str(video, "pix_fmt");
        var transfer = Str(video, "color_transfer");
        var fps = Rate(Str(video, "avg_frame_rate"));
        if (fps <= 0 || fps > 1000)
        {
            fps = Rate(Str(video, "r_frame_rate"));
        }
        return new VideoFacts
        {
            Path = path,
            Size = size,
            ModifiedUtc = modifiedUtc,
            DurationMs = (long)Math.Round(duration * 1000),
            Width = Int(video, "width"),
            Height = Int(video, "height"),
            Fps = Math.Round(fps, 3),
            Codec = Str(video, "codec_name"),
            BitDepth = pixFmt.Contains("10", StringComparison.Ordinal) ? 10 : pixFmt.Contains("12", StringComparison.Ordinal) ? 12 : 8,
            IsHdr = transfer is "smpte2084" or "arib-std-b67",
            HasAudio = audio.ValueKind == JsonValueKind.Object,
            AudioCodec = audio.ValueKind == JsonValueKind.Object ? Str(audio, "codec_name") : "",
            Rotation = RotationOf(video),
            ReadBy = "ffprobe"
        };
    }

    /// <summary>The rotation from the side data's display matrix (newer ffmpeg) or the old "rotate" tag, as 0-359 clockwise.</summary>
    private static int RotationOf(JsonElement video)
    {
        double degrees = 0;
        if (video.TryGetProperty("side_data_list", out var side))
        {
            foreach (var item in side.EnumerateArray())
            {
                if (item.TryGetProperty("rotation", out var r) && r.TryGetDouble(out var value))
                {
                    degrees = -value; // display-matrix rotation is counter-clockwise
                }
            }
        }
        if (degrees == 0 && video.TryGetProperty("tags", out var tags))
        {
            degrees = Seconds(Str(tags, "rotate"));
        }
        var normal = ((int)Math.Round(degrees) % 360 + 360) % 360;
        return normal;
    }

    private static bool IsCoverArt(JsonElement stream)
        => stream.TryGetProperty("disposition", out var d) && d.TryGetProperty("attached_pic", out var a) && a.TryGetInt32(out var v) && v == 1;

    private static string Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString()) : "";

    private static int Int(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;

    private static double Seconds(string text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
