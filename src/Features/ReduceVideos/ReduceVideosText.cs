using System.Globalization;
using System.Text;

namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>
/// All words and numbers the Reduce Size - Videos screen shows, as pure functions: list cells, the help text
/// for each engine and quality, the selection summary, the savings line and the saved report.
/// </summary>
public static class ReduceVideosText
{
    public static readonly (VideoEngineKind Value, string Text)[] EngineChoices =
    [
        (VideoEngineKind.FfmpegX265, "ffmpeg - best quality per MB (recommended)"),
        (VideoEngineKind.FfmpegGpu, "ffmpeg - fast, uses the graphics card"),
        (VideoEngineKind.Windows, "Windows built-in - no ffmpeg needed")
    ];

    public static readonly (VideoQuality Value, string Text)[] QualityChoices =
    [
        (VideoQuality.VisuallyLossless, "Visually lossless (recommended)"),
        (VideoQuality.Balanced, "Balanced - a little smaller"),
        (VideoQuality.Smallest, "Smallest - may show on a big TV")
    ];

    /// <summary>The "which should I pick?" text for the chosen engine, given what is installed.</summary>
    public static string EngineHelp(VideoEngineKind engine, bool ffmpegFound, GpuEncoder gpu, bool windowsHevc) => engine switch
    {
        VideoEngineKind.FfmpegX265 when !ffmpegFound => "ffmpeg is not installed. Install it once (" + FfmpegLocator.InstallCommand + "), or pick \"Windows built-in\".",
        VideoEngineKind.FfmpegX265 => "Best choice for keeping videos: the smallest files for the same picture, sound and camera details copied untouched, and every copy is quality-checked. Slow (about real time for 1080p) - fine to leave running.",
        VideoEngineKind.FfmpegGpu when !ffmpegFound => "ffmpeg is not installed. Install it once (" + FfmpegLocator.InstallCommand + "), or pick \"Windows built-in\".",
        VideoEngineKind.FfmpegGpu => "Several times faster using " + GpuName(gpu) + ", but copies are about 30% bigger than the recommended engine for the same look. Good for a big backlog.",
        _ => "Uses Windows' own " + (windowsHevc ? "HEVC" : "H.264") + " encoder (graphics card when possible) - nothing to install. Copies are larger than ffmpeg's, the sound is re-encoded (AAC), camera details such as location are not kept, and no quality score is measured."
    };

    /// <summary>The help text for the chosen quality.</summary>
    public static string QualityHelp(VideoQuality quality) => quality switch
    {
        VideoQuality.Balanced => "A little smaller. Differences only show if you pause and hunt for them.",
        VideoQuality.Smallest => "Smallest files. Fine on a phone or laptop; dark or busy scenes may look softer on a big TV.",
        _ => "No visible difference, even full-screen. Typical phone videos shrink to a third or less."
    };

    public static string GpuName(GpuEncoder gpu) => gpu switch
    {
        GpuEncoder.Nvidia => "your NVIDIA graphics card",
        GpuEncoder.Amd => "your AMD graphics card",
        GpuEncoder.Intel => "your Intel graphics",
        _ => "the graphics card (none found - x265 is used instead)"
    };

    /// <summary>The list cells for one video: name, size, length, picture, codec, bit rate, advice, new size, saving, why.</summary>
    public static string[] Cells(VideoFacts f, VideoAdvice a) =>
    [
        Path.GetFileName(f.Path),
        ByteSize.Format(f.Size),
        Duration(f.DurationMs),
        f.Width + "x" + f.Height + (f.IsHdr ? " HDR" : ""),
        Codecs.Label(f.Codec),
        VideoAdvisor.Mbps(f.BitrateKbps),
        VerdictLabel(a.Verdict),
        a.Verdict == Verdict.Skip ? "" : "~" + ByteSize.Format(a.EstimatedBytes),
        a.Verdict == Verdict.Skip ? "" : "~" + ByteSize.Format(a.SavingFrom(f.Size)) + " (" + Percent(a.SavingFrom(f.Size), f.Size) + ")",
        a.Reason
    ];

    public static string VerdictLabel(Verdict v) => v switch
    {
        Verdict.Recommended => "Recommended",
        Verdict.Optional => "Your call",
        _ => "Skip"
    };

    /// <summary>"1:05:09" or "4:07".</summary>
    public static string Duration(long ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1
            ? ((int)t.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" + t.ToString(@"mm\:ss", CultureInfo.InvariantCulture)
            : ((int)t.TotalMinutes).ToString(CultureInfo.InvariantCulture) + ":" + t.ToString(@"ss", CultureInfo.InvariantCulture);
    }

    public static string Percent(long part, long whole)
        => whole > 0 ? ((int)Math.Round(100.0 * part / whole)).ToString(CultureInfo.InvariantCulture) + "%" : "0%";

    /// <summary>The scan summary: how many videos, how many worth reducing, and the estimated total saving.</summary>
    public static string Summary(IReadOnlyList<(VideoFacts Facts, VideoAdvice Advice)> rows)
    {
        var total = rows.Sum(r => r.Facts.Size);
        var recommended = rows.Count(r => r.Advice.Verdict == Verdict.Recommended);
        var optional = rows.Count(r => r.Advice.Verdict == Verdict.Optional);
        var saving = rows.Sum(r => r.Advice.SavingFrom(r.Facts.Size));
        return rows.Count + " videos, " + ByteSize.Format(total) + ". " + recommended + " recommended, " + optional + " your call - up to ~"
            + ByteSize.Format(saving) + " could be saved.";
    }

    /// <summary>"3 selected: 2.10 GB -> ~640 MB, saves ~1.46 GB".</summary>
    public static string Selection(IReadOnlyList<(VideoFacts Facts, VideoAdvice Advice)> selected)
    {
        if (selected.Count == 0)
        {
            return "Nothing selected.";
        }
        var before = selected.Sum(r => r.Facts.Size);
        var after = selected.Sum(r => r.Advice.EstimatedBytes);
        return selected.Count + " selected: " + ByteSize.Format(before) + " -> ~" + ByteSize.Format(after) + ", saves ~" + ByteSize.Format(before - after);
    }

    /// <summary>"Saved so far: 37 videos, 8.20 GB".</summary>
    public static string SavedSoFar(SavingsTotal total)
        => total.Files == 0 ? "Nothing reduced yet." : "Saved so far: " + total.Files + " video" + (total.Files == 1 ? "" : "s") + ", " + ByteSize.Format(Math.Max(0, total.Saved));

    /// <summary>The end-of-run message.</summary>
    public static string RunSummary(VideoReduceOutcome o, RemoveAction action)
    {
        var text = new StringBuilder();
        text.Append("Reduced ").Append(o.Reduced).Append(", saved ").Append(ByteSize.Format(o.Saved)).Append('.');
        if (o.Skipped > 0)
        {
            text.Append(' ').Append(o.Skipped).Append(" kept as they were (see the log).");
        }
        if (o.Failed > 0)
        {
            text.Append(' ').Append(o.Failed).Append(" failed.");
        }
        if (o.Cancelled)
        {
            text.Append(" Stopped.");
        }
        if (o.Reduced > 0)
        {
            text.Append(action == RemoveAction.ReviewFolder
                ? " Originals are in \"" + VideoReduceRunner.ReviewFolderName + "\" - check a few, then delete that folder to free the space."
                : " Originals are in the Recycle Bin - empty it to free the space.");
        }
        return text.ToString();
    }

    /// <summary>The saved text report of an analysis.</summary>
    public static string Report(string root, IReadOnlyList<(VideoFacts Facts, VideoAdvice Advice)> rows, VideoAdviceOptions options, DateTime now)
    {
        var text = new StringBuilder();
        text.AppendLine("Reduce Size - Videos report");
        text.AppendLine("Folder:  " + root);
        text.AppendLine("Made:    " + now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        text.AppendLine("Engine:  " + options.Engine + ", quality " + options.Quality + ", minimum saving " + options.MinSavingPercent + "%");
        text.AppendLine(Summary(rows));
        text.AppendLine();
        foreach (var (f, a) in rows.OrderBy(r => r.Advice.Verdict).ThenByDescending(r => r.Advice.SavingFrom(r.Facts.Size)))
        {
            var c = Cells(f, a);
            text.AppendLine(c[6].PadRight(12) + c[1].PadLeft(10) + "  " + (c[7].Length > 0 ? c[7] : "-").PadLeft(10) + "  " + f.Path);
            text.AppendLine(new string(' ', 12) + a.Reason);
        }
        return text.ToString();
    }
}
