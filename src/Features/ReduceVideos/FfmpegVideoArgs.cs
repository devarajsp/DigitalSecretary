using System.Globalization;
using System.Text.RegularExpressions;

namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>A graphics-card HEVC encoder ffmpeg can drive.</summary>
public enum GpuEncoder
{
    None,
    Nvidia,
    Amd,
    Intel
}

/// <summary>
/// Builds the ffmpeg command lines and reads ffmpeg's output - all pure, so every rule is unit-tested:
/// which container the copy goes in, when the sound is copied untouched, the quality number per engine,
/// how progress and the SSIM picture-quality score are read back.
/// </summary>
public static partial class FfmpegVideoArgs
{
    /// <summary>Containers that hold HEVC + AAC and keep their own extension; everything else becomes .mp4.</summary>
    private static readonly HashSet<string> KeepContainer = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".m4v", ".mov", ".mkv" };

    /// <summary>Sound formats an MP4 / MOV can carry, so they are copied bit for bit (no loss at all).</summary>
    private static readonly HashSet<string> Mp4Audio = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "aac", "mp3", "ac3", "eac3", "alac", "opus", "flac" };

    /// <summary>The extension of the smaller copy: the same for MP4 / M4V / MOV / MKV, ".mkv" for WebM, ".mp4" for the rest.</summary>
    public static string OutputExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return KeepContainer.Contains(ext) ? ext : ext == ".webm" ? ".mkv" : ".mp4";
    }

    /// <summary>True when the sound track can be copied as it is into the output container.</summary>
    public static bool CanCopyAudio(VideoFacts facts, string outputExtension)
        => !facts.HasAudio || outputExtension == ".mkv" || Mp4Audio.Contains(facts.AudioCodec);

    /// <summary>The constant-quality number for the engine and quality (lower = better and bigger).</summary>
    public static int QualityNumber(VideoQuality quality, VideoEngineKind engine)
    {
        var x265 = quality switch
        {
            VideoQuality.Balanced => 23,
            VideoQuality.Smallest => 26,
            _ => 20
        };
        return engine == VideoEngineKind.FfmpegGpu ? x265 + 2 : x265;
    }

    /// <summary>The full ffmpeg argument list that makes <paramref name="output"/> from <paramref name="facts"/>.</summary>
    public static IReadOnlyList<string> Build(VideoFacts facts, string output, VideoQuality quality, VideoEngineKind engine, GpuEncoder gpu)
    {
        var ext = Path.GetExtension(output).ToLowerInvariant();
        var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-i", facts.Path, "-map", "0:v:0", "-map", "0:a?" };
        if (ext == ".mkv")
        {
            args.AddRange(["-map", "0:s?", "-c:s", "copy"]);
        }
        args.AddRange(["-map_metadata", "0"]);
        args.AddRange(VideoCodecArgs(facts, quality, engine, gpu));
        if (ext != ".mkv")
        {
            args.AddRange(["-tag:v", "hvc1"]);
        }
        args.AddRange(CanCopyAudio(facts, ext) ? ["-c:a", "copy"] : ["-c:a", "aac", "-b:a", "192k"]);
        if (ext is ".mp4" or ".m4v" or ".mov")
        {
            args.AddRange(["-movflags", "+faststart+use_metadata_tags"]);
        }
        args.AddRange(["-progress", "pipe:1", "-nostats", output]);
        return args;
    }

    /// <summary>The video encoder settings for the chosen engine. 10-bit sources stay 10-bit.</summary>
    public static IReadOnlyList<string> VideoCodecArgs(VideoFacts facts, VideoQuality quality, VideoEngineKind engine, GpuEncoder gpu)
    {
        var q = QualityNumber(quality, engine).ToString(CultureInfo.InvariantCulture);
        var pix = facts.BitDepth > 8 ? "yuv420p10le" : "yuv420p";
        if (engine != VideoEngineKind.FfmpegGpu || gpu == GpuEncoder.None)
        {
            return ["-c:v", "libx265", "-preset", "medium", "-crf", q, "-pix_fmt", pix, "-x265-params", "log-level=error"];
        }
        return gpu switch
        {
            GpuEncoder.Nvidia => ["-c:v", "hevc_nvenc", "-preset", "p5", "-rc", "vbr", "-cq", q, "-b:v", "0", "-pix_fmt", facts.BitDepth > 8 ? "p010le" : "yuv420p"],
            GpuEncoder.Intel => ["-c:v", "hevc_qsv", "-preset", "slower", "-global_quality", q],
            _ => ["-c:v", "hevc_amf", "-quality", "quality", "-rc", "cqp", "-qp_i", q, "-qp_p", q, "-pix_fmt", facts.BitDepth > 8 ? "p010le" : "yuv420p"]
        };
    }

    /// <summary>The ffmpeg encoder name for a graphics-card family.</summary>
    public static string EncoderName(GpuEncoder gpu) => gpu switch
    {
        GpuEncoder.Nvidia => "hevc_nvenc",
        GpuEncoder.Amd => "hevc_amf",
        GpuEncoder.Intel => "hevc_qsv",
        _ => "libx265"
    };

    /// <summary>A one-second test encode that succeeds only when the graphics card's encoder really works.</summary>
    public static IReadOnlyList<string> GpuTestArgs(GpuEncoder gpu)
        => ["-hide_banner", "-nostdin", "-f", "lavfi", "-i", "color=c=gray:s=640x360:r=30:d=1", "-c:v", EncoderName(gpu), "-f", "null", "-"];

    /// <summary>
    /// Reads one line of ffmpeg's <c>-progress</c> output. Returns the encoded position in milliseconds
    /// (<c>out_time_us</c> / <c>out_time_ms</c>, both in microseconds), or null for other lines.
    /// </summary>
    public static long? ProgressMs(string line)
    {
        var eq = line.IndexOf('=', StringComparison.Ordinal);
        if (eq <= 0)
        {
            return null;
        }
        var key = line[..eq];
        if (key is not ("out_time_us" or "out_time_ms"))
        {
            return null;
        }
        return long.TryParse(line[(eq + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var us) && us >= 0 ? us / 1000 : null;
    }

    /// <summary>The ffmpeg arguments that compare the copy with the original and print an SSIM score.</summary>
    public static IReadOnlyList<string> SsimArgs(string reduced, string original)
        => ["-hide_banner", "-nostdin", "-i", reduced, "-i", original,
            "-lavfi", "[0:v]settb=AVTB,setpts=PTS-STARTPTS[a];[1:v]settb=AVTB,setpts=PTS-STARTPTS[b];[a][b]ssim",
            "-f", "null", "-"];

    /// <summary>
    /// The overall SSIM ("All:0.987654") from ffmpeg's summary line: 1.0 = identical, above ~0.97 is not
    /// visibly different for typical home video. Null when the line is missing.
    /// </summary>
    public static double? ParseSsim(string ffmpegErrorOutput)
    {
        var match = SsimAll().Matches(ffmpegErrorOutput).LastOrDefault();
        return match is not null && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    /// <summary>The lowest SSIM accepted for each quality before the copy is thrown away.</summary>
    public static double MinimumSsim(VideoQuality quality) => quality switch
    {
        VideoQuality.Balanced => 0.955,
        VideoQuality.Smallest => 0.94,
        _ => 0.965
    };

    [GeneratedRegex(@"All:([0-9.]+)")]
    private static partial Regex SsimAll();
}
