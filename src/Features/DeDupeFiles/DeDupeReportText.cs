using System.Globalization;
using System.Text;

namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>
/// Renders the analysis (and the deletion result, once there is one) as plain text for the
/// "Save report" button. Pure and ASCII-only, so the saved file is readable anywhere and the
/// wording can be asserted in a test.
/// </summary>
public static class DeDupeReportText
{
    private const int MaxGroups = 2000;

    /// <summary>Builds the whole report.</summary>
    public static string Render(
        string root, DuplicateReport report, KeeperPolicy policy, DeleteOutcome? outcome)
    {
        var text = new StringBuilder();
        AppendHeader(text, root, policy);
        AppendTotals(text, report);
        if (outcome is not null)
        {
            AppendOutcome(text, outcome);
        }
        AppendGroups(text, report);
        return text.ToString();
    }

    private static void AppendHeader(StringBuilder text, string root, KeeperPolicy policy)
    {
        text.AppendLine("DeDupe Files - duplicate report");
        text.AppendLine("Digital Secretary");
        text.AppendLine();
        text.AppendLine("Folder : " + root);
        text.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        text.AppendLine("Keeping: " + Describe(policy));
        text.AppendLine();
    }

    private static void AppendTotals(StringBuilder text, DuplicateReport report)
    {
        text.AppendLine("SUMMARY");
        text.AppendLine("  Files checked      : " + Count(report.TotalFiles)
            + " (" + ByteSize.Format(report.TotalBytes) + ")");
        text.AppendLine("  Duplicate sets     : " + Count(report.GroupCount));
        text.AppendLine("  Duplicate copies   : " + Count(report.DuplicateFiles));
        text.AppendLine("  Space they occupy  : " + ByteSize.Format(report.DuplicateBytes));
        text.AppendLine("  Reclaimable        : " + ByteSize.Format(report.ReclaimableBytes)
            + "  (one copy of each set is always kept)");
        if (report.IgnoredEmptyFiles > 0)
        {
            text.AppendLine("  Empty files ignored: " + Count(report.IgnoredEmptyFiles));
        }
        text.AppendLine();
    }

    private static void AppendOutcome(StringBuilder text, DeleteOutcome outcome)
    {
        text.AppendLine(outcome.DryRun ? "DRY RUN - nothing was deleted" : "DELETION RESULT");
        text.AppendLine("  " + (outcome.DryRun ? "Would delete" : "Deleted") + "       : "
            + Count(outcome.Deleted));
        text.AppendLine("  " + (outcome.DryRun ? "Would free" : "Space freed") + "        : "
            + ByteSize.Format(outcome.BytesFreed));
        text.AppendLine("  Skipped            : " + Count(outcome.Skipped));
        text.AppendLine("  Failed             : " + Count(outcome.Failed));
        if (outcome.Cancelled)
        {
            text.AppendLine("  Stopped by the user before finishing.");
        }
        text.AppendLine();
    }

    private static void AppendGroups(StringBuilder text, DuplicateReport report)
    {
        if (report.IsClean)
        {
            text.AppendLine("No duplicates found.");
            return;
        }

        text.AppendLine("DUPLICATE SETS (largest saving first)");
        text.AppendLine();

        var shown = 0;
        foreach (var group in report.Groups)
        {
            if (shown == MaxGroups)
            {
                text.AppendLine("... and " + Count(report.GroupCount - shown) + " more set(s).");
                break;
            }

            text.AppendLine(group.Md5 + "  " + ByteSize.Format(group.Length) + " each  "
                + group.Copies + " copies  reclaim " + ByteSize.Format(group.Reclaimable));
            text.AppendLine("    KEEP   " + group.Keeper.Path);
            foreach (var duplicate in group.Duplicates)
            {
                text.AppendLine("    DELETE " + duplicate.Path);
            }
            text.AppendLine();
            shown++;
        }
    }

    private static string Describe(KeeperPolicy policy) => policy switch
    {
        KeeperPolicy.OldestModified => "the oldest copy (earliest modified date)",
        KeeperPolicy.NewestModified => "the newest copy (latest modified date)",
        KeeperPolicy.FirstAlphabetical => "the first copy in alphabetical order",
        _ => "the copy with the shortest path (nearest the folder you picked)"
    };

    private static string Count(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
