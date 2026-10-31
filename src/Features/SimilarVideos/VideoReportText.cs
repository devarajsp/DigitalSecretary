using System.Globalization;
using System.Text;

namespace DigitalSecretary.Features.SimilarVideos;

/// <summary>
/// Labels and the saved plain-text report. Pure and culture-free so the window, the file and the tests
/// always agree.
/// </summary>
public static class VideoReportText
{
    /// <summary>Short label for a match kind, as shown on badges and in lists.</summary>
    public static string Label(VideoMatchKind kind) => kind switch
    {
        VideoMatchKind.Exact => "Exact copy",
        VideoMatchKind.SameVideo => "Same video",
        _ => "Similar - check"
    };

    /// <summary>"turned 90°" / "mirrored" / "" for a member's orientation relative to the keeper.</summary>
    public static string Describe(PictureOrientation orientation) => orientation switch
    {
        PictureOrientation.Rotate90 or PictureOrientation.Rotate270 => "turned 90°",
        PictureOrientation.Rotate180 => "turned 180°",
        PictureOrientation.MirrorLeftRight or PictureOrientation.MirrorTopBottom => "mirrored",
        PictureOrientation.MirrorDiagonal or PictureOrientation.MirrorAntiDiagonal => "turned and mirrored",
        _ => ""
    };

    /// <summary>"Same video, turned 90° - 10/10 frames" - how a member matches its keeper.</summary>
    public static string MatchText(VideoMatch match)
    {
        var how = Label(match.Kind) + (Describe(match.Orientation) is { Length: > 0 } d ? ", " + d : "");
        return match.ComparedFrames > 0
            ? how + " - " + match.MatchedFrames.ToString(CultureInfo.InvariantCulture) + "/" + match.ComparedFrames.ToString(CultureInfo.InvariantCulture) + " frames"
            : how;
    }

    /// <summary>"0:42", "12:05" or "1:02:03"; "length unknown" for 0.</summary>
    public static string Duration(long ms)
    {
        if (ms <= 0)
        {
            return "length unknown";
        }
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1
            ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds)
            : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", t.Minutes, t.Seconds);
    }

    /// <summary>"1920 x 1080" or "size unknown".</summary>
    public static string Dimensions(VideoRecord v)
        => v.Width > 0 && v.Height > 0
            ? v.Width.ToString(CultureInfo.InvariantCulture) + " x " + v.Height.ToString(CultureInfo.InvariantCulture)
            : "size unknown";

    /// <summary>"8.2 Mbps" / "950 kbps" / "" (unknown).</summary>
    public static string Bitrate(long kbps) => kbps switch
    {
        <= 0 => "",
        >= 1000 => string.Format(CultureInfo.InvariantCulture, "{0:0.#} Mbps", kbps / 1000d),
        _ => string.Format(CultureInfo.InvariantCulture, "{0} kbps", kbps)
    };

    /// <summary>The headline: videos, groups, how many are pre-marked and the space they would free.</summary>
    public static string Summary(int videos, IReadOnlyList<VideoGroup> groups, int marked, long markedBytes)
    {
        var exact = groups.Count(g => g.Kind == VideoMatchKind.Exact);
        var same = groups.Count(g => g.Kind == VideoMatchKind.SameVideo);
        var similar = groups.Count(g => g.Kind == VideoMatchKind.Similar);
        return string.Format(CultureInfo.InvariantCulture,
            "{0:N0} videos - {1:N0} groups ({2} exact, {3} same video, {4} similar to check) - {5:N0} marked to move, {6}",
            videos, groups.Count, exact, same, similar, marked, ByteSize.Format(markedBytes));
    }

    /// <summary>The saved report: every group, its keeper and each member with its match.</summary>
    public static string Render(string root, DateTime nowUtc, int videos, IReadOnlyList<VideoGroup> groups, IReadOnlySet<string> marked)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Similar Videos report");
        sb.AppendLine("Folder:   " + root);
        sb.AppendLine("Created:  " + nowUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        var markedBytes = groups.SelectMany(g => g.Members).Where(m => marked.Contains(m.Video.Path)).Sum(m => m.Video.Size);
        sb.AppendLine(Summary(videos, groups, marked.Count, markedBytes));
        sb.AppendLine();
        foreach (var g in groups)
        {
            sb.AppendLine("Group " + g.Id.ToString(CultureInfo.InvariantCulture) + " - " + Label(g.Kind) + " - " + g.Count + " videos");
            sb.AppendLine("  KEEP  " + Line(g.Keeper));
            foreach (var m in g.Members)
            {
                var action = marked.Contains(m.Video.Path) ? "MOVE" : "keep";
                sb.AppendLine("  " + action.PadRight(4) + "  " + Line(m.Video) + "  [" + MatchText(m.Match) + "]");
            }
            sb.AppendLine();
        }
        if (groups.Count == 0)
        {
            sb.AppendLine("No similar videos found.");
        }
        return sb.ToString();
    }

    private static string Line(VideoRecord v)
        => ByteSize.Format(v.Size).PadLeft(10) + "  " + Duration(v.DurationMs).PadLeft(8) + "  " + Dimensions(v).PadRight(12) + "  " + v.Path;
}
