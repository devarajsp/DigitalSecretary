using System.Globalization;
using System.Text;

namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>
/// Labels and the saved plain-text report. Pure and culture-free so the window, the file and the tests
/// always agree.
/// </summary>
public static class SimilarReportText
{
    /// <summary>Short label for a match kind, as shown on badges and in lists.</summary>
    public static string Label(MatchKind kind) => kind switch
    {
        MatchKind.Exact => "Exact copy",
        MatchKind.SamePicture => "Same picture",
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

    /// <summary>"4032 x 3024" or "size unknown".</summary>
    public static string Dimensions(PictureRecord p)
        => p.Width > 0 && p.Height > 0
            ? p.Width.ToString(CultureInfo.InvariantCulture) + " x " + p.Height.ToString(CultureInfo.InvariantCulture)
            : "size unknown";

    /// <summary>The headline: pictures, groups, how many are pre-marked and the space they would free.</summary>
    public static string Summary(int pictures, IReadOnlyList<SimilarGroup> groups, int marked, long markedBytes)
    {
        var exact = groups.Count(g => g.Kind == MatchKind.Exact);
        var same = groups.Count(g => g.Kind == MatchKind.SamePicture);
        var similar = groups.Count(g => g.Kind == MatchKind.Similar);
        return string.Format(CultureInfo.InvariantCulture,
            "{0:N0} pictures - {1:N0} groups ({2} exact, {3} same picture, {4} similar to check) - {5:N0} marked to move, {6}",
            pictures, groups.Count, exact, same, similar, marked, ByteSize.Format(markedBytes));
    }

    /// <summary>The saved report: every group, its keeper and each member with its match.</summary>
    public static string Render(string root, DateTime nowUtc, int pictures, IReadOnlyList<SimilarGroup> groups, IReadOnlySet<string> marked)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Similar Pictures report");
        sb.AppendLine("Folder:   " + root);
        sb.AppendLine("Created:  " + nowUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        var markedBytes = groups.SelectMany(g => g.Members).Where(m => marked.Contains(m.Picture.Path)).Sum(m => m.Picture.Size);
        sb.AppendLine(Summary(pictures, groups, marked.Count, markedBytes));
        sb.AppendLine();
        foreach (var g in groups)
        {
            sb.AppendLine("Group " + g.Id.ToString(CultureInfo.InvariantCulture) + " - " + Label(g.Kind) + " - " + g.Count + " pictures");
            sb.AppendLine("  KEEP  " + Line(g.Keeper));
            foreach (var m in g.Members)
            {
                var action = marked.Contains(m.Picture.Path) ? "MOVE" : "keep";
                var how = Label(m.Kind) + (Describe(m.Orientation) is { Length: > 0 } d ? ", " + d : "");
                sb.AppendLine("  " + action.PadRight(4) + "  " + Line(m.Picture) + "  [" + how + "]");
            }
            sb.AppendLine();
        }
        if (groups.Count == 0)
        {
            sb.AppendLine("No similar pictures found.");
        }
        return sb.ToString();
    }

    private static string Line(PictureRecord p)
        => ByteSize.Format(p.Size).PadLeft(10) + "  " + Dimensions(p).PadRight(13) + "  " + p.Path;
}
