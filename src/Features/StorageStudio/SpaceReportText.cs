using System.Globalization;
using System.Text;

namespace DigitalSecretary.Features.StorageStudio;

/// <summary>Everything a saved report shows, gathered so rendering stays a pure function.</summary>
public sealed record SpaceReport(
    SpaceNode Folder,
    DateTime NowUtc,
    IReadOnlyList<BreakdownItem> ByType,
    IReadOnlyList<BreakdownItem> ByAge,
    IReadOnlyList<SpaceNode> LargestFiles,
    IReadOnlyList<SpaceNode> LargestFolders,
    IReadOnlyList<CleanupGroup> Cleanup,
    IReadOnlyList<GrowthEntry>? Growth,
    DateTime? PreviousScanUtc)
{
    /// <summary>Asks every analysis question of <paramref name="folder"/> with the default list sizes.</summary>
    public static SpaceReport For(SpaceNode folder, DateTime nowUtc, IReadOnlyList<GrowthEntry>? growth, DateTime? previousScanUtc, int top = 50)
        => new(folder, nowUtc,
            SpaceAnalyzer.ByType(folder),
            SpaceAnalyzer.ByAge(folder, nowUtc),
            SpaceAnalyzer.LargestFiles(folder, top),
            SpaceAnalyzer.LargestFolders(folder, top),
            CleanupFinder.Find(folder, nowUtc),
            growth,
            previousScanUtc);
}

/// <summary>
/// Renders a <see cref="SpaceReport"/> as plain text for "Save report". Pure and culture-free, so
/// the file matches what the window shows and the tests can assert it exactly.
/// </summary>
public static class SpaceReportText
{
    public static string Render(SpaceReport report)
    {
        var sb = new StringBuilder();
        var f = report.Folder;
        sb.AppendLine("Storage Studio report");
        sb.AppendLine("Folder:    " + f.FullPath);
        sb.AppendLine("Created:   " + report.NowUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        sb.AppendLine("Total:     " + ByteSize.Format(f.Size) + " in " + N(f.FileCount) + " files, " + N(f.FolderCount) + " folders");
        sb.AppendLine();

        Breakdown(sb, "By type", report.ByType);
        Breakdown(sb, "By age (last modified)", report.ByAge);

        sb.AppendLine("Largest files");
        foreach (var file in report.LargestFiles)
        {
            sb.AppendLine("  " + Pad(ByteSize.Format(file.Size)) + file.FullPath);
        }
        sb.AppendLine();

        sb.AppendLine("Largest folders");
        foreach (var folder in report.LargestFolders)
        {
            sb.AppendLine("  " + Pad(ByteSize.Format(folder.Size)) + folder.FullPath);
        }
        sb.AppendLine();

        Cleanup(sb, report.Cleanup);
        Growth(sb, report.Growth, report.PreviousScanUtc);
        return sb.ToString();
    }

    private static void Breakdown(StringBuilder sb, string title, IReadOnlyList<BreakdownItem> items)
    {
        sb.AppendLine(title);
        foreach (var item in items)
        {
            sb.AppendLine("  " + item.Label.PadRight(28) + Pad(ByteSize.Format(item.Bytes))
                + Percent(item.Share).PadLeft(7) + "   " + N(item.Files) + " files");
        }
        sb.AppendLine();
    }

    private static void Cleanup(StringBuilder sb, IReadOnlyList<CleanupGroup> groups)
    {
        sb.AppendLine("Clean-up candidates (report only - nothing was changed)");
        if (groups.Count == 0)
        {
            sb.AppendLine("  None found.");
        }
        foreach (var group in groups)
        {
            sb.AppendLine("  " + group.Title + ": " + ByteSize.Format(group.Bytes) + " in " + N(group.Items.Count) + " item(s)");
            sb.AppendLine("    " + group.Why);
            foreach (var item in group.Items.Take(20))
            {
                sb.AppendLine("    " + Pad(ByteSize.Format(item.Size)) + item.FullPath);
            }
            if (group.Items.Count > 20)
            {
                sb.AppendLine("    ... and " + N(group.Items.Count - 20) + " more");
            }
        }
        sb.AppendLine();
    }

    private static void Growth(StringBuilder sb, IReadOnlyList<GrowthEntry>? growth, DateTime? previous)
    {
        sb.AppendLine("Changes since the previous scan");
        if (growth is null || previous is null)
        {
            sb.AppendLine("  No previous scan of this folder to compare with.");
            return;
        }
        sb.AppendLine("  Previous scan: " + previous.Value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        if (growth.Count == 0)
        {
            sb.AppendLine("  No folder changed noticeably.");
        }
        foreach (var entry in growth)
        {
            sb.AppendLine("  " + WithSign(entry.Delta).PadLeft(12) + "  " + entry.RelativePath);
        }
    }

    /// <summary>"+1.5 GB" / "-200 MB" - a change in size with its sign.</summary>
    public static string WithSign(long delta) => (delta >= 0 ? "+" : "") + ByteSize.Format(delta);

    /// <summary>"42.5%" for a 0 - 1 share.</summary>
    public static string Percent(double share)
        => (share * 100d).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    private static string Pad(string size) => size.PadLeft(11) + "  ";

    private static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
