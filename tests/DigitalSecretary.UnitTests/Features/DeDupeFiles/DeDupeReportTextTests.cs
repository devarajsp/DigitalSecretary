using DigitalSecretary.Kit;
using DigitalSecretary.Features.DeDupeFiles;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

/// <summary>Covers the saved plain-text report: its wording, its totals and its ASCII-only promise.</summary>
public sealed class DeDupeReportTextTests
{
    private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Root = @"C:\pics\holiday";

    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static DuplicateReport ReportWithTwoSets()
        => DuplicateAnalyzer.Analyze(
            new[]
            {
                new ChecksumEntry(HashA, 2048, Stamp, @"C:\pics\holiday\beach.jpg"),
                new ChecksumEntry(HashA, 2048, Stamp, @"C:\pics\holiday\copies\beach.jpg"),
                new ChecksumEntry(HashA, 2048, Stamp, @"C:\pics\holiday\copies\beach (2).jpg"),
                new ChecksumEntry(HashB, 512, Stamp, @"C:\pics\holiday\map.png"),
                new ChecksumEntry(HashB, 512, Stamp, @"C:\pics\holiday\copies\map.png"),
            },
            KeeperPolicy.ShortestPath,
            ignoreEmptyFiles: true);

    private static DuplicateReport CleanReport()
        => DuplicateAnalyzer.Analyze(
            new[] { new ChecksumEntry(HashA, 10, Stamp, @"C:\pics\holiday\only.jpg") },
            KeeperPolicy.ShortestPath,
            ignoreEmptyFiles: true);

    [Fact]
    public void Render_names_the_folder_and_the_keeper_policy()
    {
        var text = DeDupeReportText.Render(Root, ReportWithTwoSets(), KeeperPolicy.ShortestPath, null);

        text.Should().Contain(Root);
        text.Should().Contain("DeDupe Files");
        text.Should().Contain("shortest path");
    }

    [Fact]
    public void Render_shows_the_totals()
    {
        var report = ReportWithTwoSets();

        var text = DeDupeReportText.Render(Root, report, KeeperPolicy.ShortestPath, null);

        text.Should().Contain("SUMMARY");
        text.Should().Contain("Files checked");
        text.Should().Contain("Duplicate sets     : 2");
        text.Should().Contain("Duplicate copies   : 3");
        text.Should().Contain(ByteSize.Format(report.ReclaimableBytes));
        text.Should().Contain(ByteSize.Format(report.TotalBytes));
    }

    [Fact]
    public void Render_marks_the_keeper_KEEP_and_every_duplicate_DELETE()
    {
        var report = ReportWithTwoSets();

        var text = DeDupeReportText.Render(Root, report, KeeperPolicy.ShortestPath, null);

        foreach (var group in report.Groups)
        {
            text.Should().Contain("KEEP   " + group.Keeper.Path);
            foreach (var duplicate in group.Duplicates)
            {
                text.Should().Contain("DELETE " + duplicate.Path);
            }
        }
    }

    [Fact]
    public void Render_lists_the_duplicate_sets_with_their_checksum_and_copy_count()
    {
        var text = DeDupeReportText.Render(Root, ReportWithTwoSets(), KeeperPolicy.ShortestPath, null);

        text.Should().Contain("DUPLICATE SETS (largest saving first)");
        text.Should().Contain(HashA);
        text.Should().Contain("3 copies");
    }

    [Fact]
    public void Render_says_so_when_there_are_no_duplicates()
    {
        var text = DeDupeReportText.Render(Root, CleanReport(), KeeperPolicy.ShortestPath, null);

        text.Should().Contain("No duplicates found.");
        text.Should().NotContain("DUPLICATE SETS");
    }

    [Fact]
    public void Render_labels_a_dry_run_outcome_as_a_dry_run()
    {
        var outcome = new DeleteOutcome(3, 1, 0, 6144, DryRun: true, Cancelled: false);

        var text = DeDupeReportText.Render(Root, ReportWithTwoSets(), KeeperPolicy.ShortestPath, outcome);

        text.Should().Contain("DRY RUN - nothing was deleted");
        text.Should().Contain("Would delete");
        text.Should().Contain("Would free");
        text.Should().NotContain("DELETION RESULT");
    }

    [Fact]
    public void Render_labels_a_real_outcome_as_a_deletion_result()
    {
        var outcome = new DeleteOutcome(3, 1, 2, 6144, DryRun: false, Cancelled: false);

        var text = DeDupeReportText.Render(Root, ReportWithTwoSets(), KeeperPolicy.ShortestPath, outcome);

        text.Should().Contain("DELETION RESULT");
        text.Should().Contain("Deleted");
        text.Should().Contain("Space freed");
        text.Should().Contain("Failed             : 2");
        text.Should().NotContain("DRY RUN");
    }

    [Fact]
    public void Render_notes_a_run_that_the_user_stopped()
    {
        var outcome = new DeleteOutcome(1, 0, 0, 10, DryRun: false, Cancelled: true);

        var text = DeDupeReportText.Render(Root, ReportWithTwoSets(), KeeperPolicy.ShortestPath, outcome);

        text.Should().Contain("Stopped by the user before finishing.");
    }

    [Fact]
    public void Render_omits_the_outcome_block_when_nothing_has_been_deleted_yet()
    {
        var text = DeDupeReportText.Render(Root, ReportWithTwoSets(), KeeperPolicy.ShortestPath, null);

        text.Should().NotContain("DELETION RESULT");
        text.Should().NotContain("DRY RUN");
    }

    [Fact]
    public void Render_shows_the_ignored_empty_files_line_only_when_there_were_some()
    {
        var withEmpties = DuplicateAnalyzer.Analyze(
            new[]
            {
                new ChecksumEntry(HashA, 0, Stamp, @"C:\pics\holiday\blank1.txt"),
                new ChecksumEntry(HashA, 0, Stamp, @"C:\pics\holiday\blank2.txt"),
            },
            KeeperPolicy.ShortestPath,
            ignoreEmptyFiles: true);

        DeDupeReportText.Render(Root, withEmpties, KeeperPolicy.ShortestPath, null)
            .Should().Contain("Empty files ignored: 2");
        DeDupeReportText.Render(Root, ReportWithTwoSets(), KeeperPolicy.ShortestPath, null)
            .Should().NotContain("Empty files ignored");
    }

    [Theory]
    [InlineData(KeeperPolicy.ShortestPath)]
    [InlineData(KeeperPolicy.OldestModified)]
    [InlineData(KeeperPolicy.NewestModified)]
    [InlineData(KeeperPolicy.FirstAlphabetical)]
    public void Render_describes_every_keeper_policy_in_plain_words(KeeperPolicy policy)
    {
        var text = DeDupeReportText.Render(Root, ReportWithTwoSets(), policy, null);

        var keeping = text.Split('\n').First(l => l.StartsWith("Keeping:", StringComparison.Ordinal));
        keeping.Trim().Should().NotBe("Keeping:");
    }

    [Fact]
    public void Render_is_plain_ascii_so_the_saved_file_can_never_be_mojibake()
    {
        var outcome = new DeleteOutcome(3, 1, 0, 6144, DryRun: true, Cancelled: true);

        var text = DeDupeReportText.Render(Root, ReportWithTwoSets(), KeeperPolicy.ShortestPath, outcome);

        text.Should().NotBeEmpty();
        text.Where(c => c > '\u007F').Should().BeEmpty("the report must stay plain ASCII");
    }
}
