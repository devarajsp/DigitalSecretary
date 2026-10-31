namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>One encode: the source facts, the temp output, the chosen quality and the target bit rate.</summary>
public sealed record EncodeJob(VideoFacts Source, string Output, VideoQuality Quality, int TargetVideoKbps);

/// <summary>How an encode ended. <see cref="Error"/> is a short, user-readable reason when it failed.</summary>
public sealed record EncodeResult(bool Ok, string Error, bool Cancelled)
{
    public static readonly EncodeResult Success = new(true, "", false);
    public static readonly EncodeResult Stopped = new(false, "Stopped.", true);

    public static EncodeResult Failed(string error) => new(false, error, false);
}

/// <summary>
/// An encoder that makes the smaller copy, and reads the result back for verification. The run loop only
/// talks to this interface, so the tests drive it with a fake engine.
/// </summary>
public interface IVideoEngine
{
    /// <summary>Short name for the log and the history ("ffmpeg x265", "ffmpeg hevc_amf", "Windows HEVC").</summary>
    string Name { get; }

    /// <summary>The extension the copy of <paramref name="facts"/> gets.</summary>
    string OutputExtension(VideoFacts facts);

    EncodeResult Encode(EncodeJob job, IProgress<int>? percent, CancellationToken cancel);

    /// <summary>Reads the finished copy (length, picture, sound) for verification; null when unreadable.</summary>
    VideoFacts? Probe(string path);

    /// <summary>The SSIM score of the copy against the original, or null when this engine cannot measure it.</summary>
    double? Ssim(string reduced, string original, CancellationToken cancel);
}

/// <summary>The ffmpeg engines: x265 on the processor, or the graphics card's HEVC encoder.</summary>
public sealed class FfmpegVideoEngine(MediaToolPaths tools, VideoEngineKind kind, GpuEncoder gpu) : IVideoEngine
{
    public string Name => "ffmpeg " + (kind == VideoEngineKind.FfmpegGpu && gpu != GpuEncoder.None ? FfmpegVideoArgs.EncoderName(gpu) : "x265");

    public string OutputExtension(VideoFacts facts) => FfmpegVideoArgs.OutputExtension(facts.Path);

    public EncodeResult Encode(EncodeJob job, IProgress<int>? percent, CancellationToken cancel)
    {
        var args = FfmpegVideoArgs.Build(job.Source, job.Output, job.Quality, kind, gpu);
        var duration = Math.Max(1, job.Source.DurationMs);
        var result = ToolRunner.Run(tools.Ffmpeg, args, line =>
        {
            if (FfmpegVideoArgs.ProgressMs(line) is { } ms)
            {
                percent?.Report((int)Math.Clamp(ms * 100 / duration, 0, 100));
            }
        }, cancel);
        if (result.Cancelled)
        {
            return EncodeResult.Stopped;
        }
        return result.Succeeded ? EncodeResult.Success : EncodeResult.Failed("ffmpeg failed: " + LastLine(result.ErrorTail));
    }

    public VideoFacts? Probe(string path) => FfprobeReader.Read(tools.Ffprobe, new FileInfo(path));

    public double? Ssim(string reduced, string original, CancellationToken cancel)
    {
        var result = ToolRunner.Run(tools.Ffmpeg, FfmpegVideoArgs.SsimArgs(reduced, original), _ => { }, cancel);
        return result.Cancelled ? null : FfmpegVideoArgs.ParseSsim(result.ErrorTail);
    }

    /// <summary>
    /// Which graphics-card encoder really works on this PC: each family is tried with a one-second test
    /// encode (ffmpeg lists all of them whether or not the hardware is there). None when no card works.
    /// </summary>
    public static GpuEncoder DetectGpu(MediaToolPaths tools)
    {
        foreach (var gpu in new[] { GpuEncoder.Nvidia, GpuEncoder.Amd, GpuEncoder.Intel })
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            if (ToolRunner.Run(tools.Ffmpeg, FfmpegVideoArgs.GpuTestArgs(gpu), _ => { }, timeout.Token).Succeeded)
            {
                return gpu;
            }
        }
        return GpuEncoder.None;
    }

    private static string LastLine(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length == 0 ? "no details" : lines[^1];
    }
}

/// <summary>Windows' own encoder (no ffmpeg): <see cref="MfTranscoder"/> to make the copy, <see cref="MfVideoProbe"/> to check it.</summary>
public sealed class WindowsVideoEngine : IVideoEngine
{
    public string Name => "Windows " + (MfTranscoder.HasHevcEncoder ? "HEVC" : "H.264");

    /// <summary>The Windows engine always writes MP4.</summary>
    public string OutputExtension(VideoFacts facts) => ".mp4";

    public EncodeResult Encode(EncodeJob job, IProgress<int>? percent, CancellationToken cancel)
        => MfTranscoder.Transcode(job, percent, cancel);

    public VideoFacts? Probe(string path) => MfVideoProbe.Read(new FileInfo(path));

    public double? Ssim(string reduced, string original, CancellationToken cancel) => null;
}
