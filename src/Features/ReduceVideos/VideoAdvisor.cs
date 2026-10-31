using System.Globalization;

namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>Which encoder makes the smaller copy.</summary>
public enum VideoEngineKind
{
    /// <summary>ffmpeg with x265 on the processor: the smallest files for the same look; the slowest. The default.</summary>
    FfmpegX265,

    /// <summary>ffmpeg with the graphics card's HEVC encoder (AMD / NVIDIA / Intel): several times faster, files ~30% larger.</summary>
    FfmpegGpu,

    /// <summary>Windows' own encoder (Media Foundation, uses the graphics card when it can): no ffmpeg needed.</summary>
    Windows
}

/// <summary>How hard to compress.</summary>
public enum VideoQuality
{
    /// <summary>No visible difference, even full-screen (x265 CRF 20). The default.</summary>
    VisuallyLossless,

    /// <summary>A little smaller; differences only when hunting for them (CRF 23).</summary>
    Balanced,

    /// <summary>Smallest; fine on a phone or laptop, may show in dark or busy scenes on a big TV (CRF 26).</summary>
    Smallest
}

/// <summary>The choices that change the advice. Changing them re-advises instantly; no rescan.</summary>
public sealed record VideoAdviceOptions
{
    public VideoEngineKind Engine { get; init; } = VideoEngineKind.FfmpegX265;
    public VideoQuality Quality { get; init; } = VideoQuality.VisuallyLossless;

    /// <summary>A video is only offered when the estimate saves at least this share of its size.</summary>
    public int MinSavingPercent { get; init; } = 25;

    /// <summary>Leave HDR videos alone (their colours need care a plain re-encode does not give). On by default.</summary>
    public bool SkipHdr { get; init; } = true;

    /// <summary>The Windows engine found an HEVC encoder; without one it makes H.264, which needs more bits.</summary>
    public bool WindowsHasHevc { get; init; } = true;
}

/// <summary>One video's advice: verdict, the reason in plain words, and the estimated new size.</summary>
public sealed record VideoAdvice(Verdict Verdict, string Reason, long EstimatedBytes, int TargetVideoKbps)
{
    /// <summary>Estimated bytes saved (0 when skipped).</summary>
    public long SavingFrom(long size) => Verdict == Verdict.Skip ? 0 : Math.Max(0, size - EstimatedBytes);
}

/// <summary>
/// Decides, per video, whether a smaller copy is worth making - the "help me choose" part. The measure is
/// <b>bits per pixel per frame</b>: how many bits the file spends on each pixel of each frame. A modern
/// encoder needs only so many bits for a given look; whatever the file spends above that is space that can be
/// saved without a visible change. The targets are tuned for HEVC at 1080p30 and scaled for the engine (a
/// graphics-card or H.264 encoder needs more bits for the same look), the resolution (bigger pictures need
/// fewer bits per pixel) and the frame rate. Estimates are deliberately cautious: the real result is measured
/// after encoding and a copy that does not save enough is thrown away.
/// </summary>
public static class VideoAdvisor
{
    /// <summary>Above this share saved, an older codec is "Recommended" rather than "Optional".</summary>
    public const int RecommendedPercent = 40;

    /// <summary>Audio allowance in kbit/s (copied as it is, or AAC 192k when it has to be converted).</summary>
    public const int AudioKbps = 160;

    private const long FullHdPixels = 1920L * 1080;

    public static VideoAdvice Advise(VideoFacts facts, VideoAdviceOptions options)
    {
        if (Unsuitable(facts, options) is { } why)
        {
            return new VideoAdvice(Verdict.Skip, why, facts.Size, 0);
        }
        var targetBpp = TargetBitsPerPixel(facts, options);
        var videoKbps = (int)Math.Ceiling(targetBpp * facts.Pixels * facts.Fps / 1000.0);
        var estimate = EstimateBytes(facts, videoKbps);
        var percent = facts.Size > 0 ? 100.0 * (facts.Size - estimate) / facts.Size : 0;
        var now = Describe(facts);
        var after = OutputCodecLabel(options) + " looks the same at about " + Mbps(videoKbps);

        if (percent <= 0)
        {
            return new VideoAdvice(Verdict.Skip, "Already compact (" + now + ").", facts.Size, videoKbps);
        }
        if (Codecs.Modern.Contains(facts.Codec) && percent < RecommendedPercent)
        {
            return new VideoAdvice(Verdict.Skip, "Already " + Codecs.Label(facts.Codec) + " - another pass would cost quality for ~" + Pct(percent) + ".", facts.Size, videoKbps);
        }
        if (percent < options.MinSavingPercent)
        {
            return new VideoAdvice(Verdict.Skip, "Would save only ~" + Pct(percent) + " (" + now + ").", facts.Size, videoKbps);
        }
        var verdict = percent >= RecommendedPercent ? Verdict.Recommended : Verdict.Optional;
        return new VideoAdvice(verdict, now + "; " + after + ".", estimate, videoKbps);
    }

    /// <summary>The bits per pixel per frame the chosen engine and quality need for this video.</summary>
    public static double TargetBitsPerPixel(VideoFacts facts, VideoAdviceOptions options)
    {
        var quality = options.Quality switch
        {
            VideoQuality.Balanced => 0.055,
            VideoQuality.Smallest => 0.040,
            _ => 0.075
        };
        var engine = options.Engine switch
        {
            VideoEngineKind.FfmpegGpu => 1.35,
            VideoEngineKind.Windows => options.WindowsHasHevc ? 1.35 : 2.0,
            _ => 1.0
        };
        var resolution = Math.Clamp(Math.Pow((double)FullHdPixels / Math.Max(1, facts.Pixels), 0.25), 0.75, 1.6);
        var frameRate = facts.Fps > 0 ? Math.Clamp(Math.Pow(30.0 / facts.Fps, 0.3), 0.7, 1.4) : 1.0;
        return quality * engine * resolution * frameRate;
    }

    /// <summary>Estimated size of the smaller copy: video + audio for the whole length, plus 1% for the container.</summary>
    public static long EstimateBytes(VideoFacts facts, int videoKbps)
    {
        var kbps = videoKbps + (facts.HasAudio ? AudioKbps : 0);
        return (long)(kbps * (double)facts.DurationMs / 8 * 1.01); // kbit/s x ms = bits
    }

    /// <summary>Why a video is not a candidate at all, or null when it is.</summary>
    public static string? Unsuitable(VideoFacts facts, VideoAdviceOptions options)
    {
        if (facts.DurationMs < 2000 || facts.Pixels == 0 || facts.Fps <= 0)
        {
            return "Too short, or its picture could not be read.";
        }
        if (options.SkipHdr && facts.IsHdr)
        {
            return "HDR video - left as it is so its colours stay right.";
        }
        if (options.Engine == VideoEngineKind.Windows && facts.BitDepth > 8)
        {
            return "10-bit video - the Windows engine would make it 8-bit; use ffmpeg for this one.";
        }
        return null;
    }

    /// <summary>"H.264 at 17.4 Mbit/s, 0.28 bits/pixel".</summary>
    public static string Describe(VideoFacts facts)
        => Codecs.Label(facts.Codec) + " at " + Mbps(facts.BitrateKbps) + ", " + facts.BitsPerPixel.ToString("0.00", CultureInfo.InvariantCulture) + " bits/pixel";

    /// <summary>"5.1 Mbit/s" or "800 kbit/s".</summary>
    public static string Mbps(long kbps)
        => kbps >= 1000 ? (kbps / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " Mbit/s" : kbps.ToString(CultureInfo.InvariantCulture) + " kbit/s";

    private static string OutputCodecLabel(VideoAdviceOptions options)
        => options.Engine == VideoEngineKind.Windows && !options.WindowsHasHevc ? "H.264" : "HEVC";

    private static string Pct(double percent) => ((int)Math.Round(percent)).ToString(CultureInfo.InvariantCulture) + "%";
}
