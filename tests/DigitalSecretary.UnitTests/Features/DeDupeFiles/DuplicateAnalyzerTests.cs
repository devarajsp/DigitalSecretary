using DigitalSecretary.Features.DeDupeFiles;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

/// <summary>
/// Covers <see cref="DuplicateAnalyzer"/> - the pure arithmetic behind every number the user sees,
/// and the keeper choice that decides which copy survives.
/// </summary>
public sealed class DuplicateAnalyzerTests
{
    private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string HashC = "cccccccccccccccccccccccccccccccc";

    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static ChecksumEntry Entry(string hash, long length, string path, DateTime? modified = null)
        => new(hash, length, modified ?? Stamp, path);

    private static DuplicateFile Dup(string path, long length = 100, int year = 2024)
        => new(path, length, new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    /// <summary>
    /// The worked example used by several tests:
    /// set A = three 100-byte copies, set B = two 1000-byte copies, plus one unique 7-byte file.
    /// </summary>
    private static ChecksumIndex WorkedExample() => ChecksumIndex.FromEntries(new[]
    {
        Entry(HashA, 100, @"C:\r\a.txt"),
        Entry(HashA, 100, @"C:\r\sub\a.txt"),
        Entry(HashA, 100, @"C:\r\sub\deep\a.txt"),
        Entry(HashB, 1000, @"C:\r\b1.bin"),
        Entry(HashB, 1000, @"C:\r\sub\b2.bin"),
        Entry(HashC, 7, @"C:\r\unique.txt"),
    });

    [Fact]
    public void Analyze_finds_no_groups_when_every_file_has_its_own_checksum()
    {
        var index = ChecksumIndex.FromEntries(new[]
        {
            Entry(HashA, 10, @"C:\r\a.txt"),
            Entry(HashB, 20, @"C:\r\b.txt"),
        });

        var report = DuplicateAnalyzer.Analyze(index, KeeperPolicy.ShortestPath, ignoreEmptyFiles: true);

        report.Groups.Should().BeEmpty();
        report.GroupCount.Should().Be(0);
        report.IsClean.Should().BeTrue();
        report.DuplicateFiles.Should().Be(0);
        report.ReclaimableBytes.Should().Be(0);
        report.TotalFiles.Should().Be(2);
        report.TotalBytes.Should().Be(30);
    }

    [Fact]
    public void Analyze_turns_two_identical_files_into_one_group_with_one_duplicate()
    {
        var index = ChecksumIndex.FromEntries(new[]
        {
            Entry(HashA, 500, @"C:\r\original.txt"),
            Entry(HashA, 500, @"C:\r\sub\copy.txt"),
        });

        var report = DuplicateAnalyzer.Analyze(index, KeeperPolicy.ShortestPath, ignoreEmptyFiles: true);

        report.Groups.Should().HaveCount(1);
        report.IsClean.Should().BeFalse();
        var group = report.Groups[0];
        group.Md5.Should().Be(HashA);
        group.Copies.Should().Be(2);
        group.Duplicates.Should().HaveCount(1);
        group.Keeper.Path.Should().Be(@"C:\r\original.txt");
        group.Duplicates[0].Path.Should().Be(@"C:\r\sub\copy.txt");
        group.Reclaimable.Should().Be(500);
        group.OccupiedBytes.Should().Be(1000);
    }

    [Fact]
    public void Analyze_computes_every_total_on_the_worked_example()
    {
        var report = DuplicateAnalyzer.Analyze(
            WorkedExample(), KeeperPolicy.ShortestPath, ignoreEmptyFiles: true);

        report.TotalFiles.Should().Be(6);
        report.TotalBytes.Should().Be(2307);
        report.GroupCount.Should().Be(2);
        report.DuplicateFiles.Should().Be(3);
        report.DuplicateBytes.Should().Be(2300);
        report.ReclaimableBytes.Should().Be(1200);
        report.IgnoredEmptyFiles.Should().Be(0);
    }

    [Fact]
    public void Analyze_sorts_groups_by_reclaimable_space_descending()
    {
        var report = DuplicateAnalyzer.Analyze(
            WorkedExample(), KeeperPolicy.ShortestPath, ignoreEmptyFiles: true);

        report.Groups.Select(g => g.Reclaimable).Should().Equal(1000L, 200L);
        report.Groups[0].Md5.Should().Be(HashB);
        report.Groups[1].Md5.Should().Be(HashA);
    }

    [Fact]
    public void Analyze_never_lists_the_keeper_among_the_duplicates()
    {
        var report = DuplicateAnalyzer.Analyze(
            WorkedExample(), KeeperPolicy.ShortestPath, ignoreEmptyFiles: true);

        foreach (var group in report.Groups)
        {
            group.Duplicates.Select(d => d.Path).Should().NotContain(group.Keeper.Path);
            group.Duplicates.Should().HaveCount(group.Copies - 1);
        }
    }

    [Fact]
    public void Analyze_can_ignore_a_set_of_empty_files()
    {
        var index = ChecksumIndex.FromEntries(new[]
        {
            Entry(HashA, 0, @"C:\r\empty1.txt"),
            Entry(HashA, 0, @"C:\r\empty2.txt"),
            Entry(HashA, 0, @"C:\r\sub\empty3.txt"),
            Entry(HashB, 50, @"C:\r\real1.txt"),
            Entry(HashB, 50, @"C:\r\real2.txt"),
        });

        var report = DuplicateAnalyzer.Analyze(index, KeeperPolicy.ShortestPath, ignoreEmptyFiles: true);

        report.Groups.Should().HaveCount(1);
        report.Groups[0].Md5.Should().Be(HashB);
        report.IgnoredEmptyFiles.Should().Be(3);
    }

    [Fact]
    public void Analyze_includes_empty_files_when_asked_to()
    {
        var index = ChecksumIndex.FromEntries(new[]
        {
            Entry(HashA, 0, @"C:\r\empty1.txt"),
            Entry(HashA, 0, @"C:\r\empty2.txt"),
        });

        var report = DuplicateAnalyzer.Analyze(index, KeeperPolicy.ShortestPath, ignoreEmptyFiles: false);

        report.Groups.Should().HaveCount(1);
        report.IgnoredEmptyFiles.Should().Be(0);
        report.ReclaimableBytes.Should().Be(0);
    }

    [Fact]
    public void Analyze_accepts_plain_entries_as_well_as_an_index()
    {
        var entries = new[]
        {
            Entry(HashA, 42, @"C:\r\a.txt"),
            Entry(HashA, 42, @"C:\r\sub\a.txt"),
        };

        var report = DuplicateAnalyzer.Analyze(entries, KeeperPolicy.ShortestPath, ignoreEmptyFiles: true);

        report.GroupCount.Should().Be(1);
        report.ReclaimableBytes.Should().Be(42);
    }

    [Fact]
    public void Pick_keeper_shortest_path_keeps_the_copy_nearest_the_root()
    {
        PickFromTheFourPolicyFixture(KeeperPolicy.ShortestPath).Path.Should().Be(@"C:\r\m.txt");
    }

    [Fact]
    public void Pick_keeper_oldest_modified_keeps_the_earliest_copy()
    {
        PickFromTheFourPolicyFixture(KeeperPolicy.OldestModified).Path.Should().Be(@"C:\r\old\old.txt");
    }

    [Fact]
    public void Pick_keeper_newest_modified_keeps_the_latest_copy()
    {
        PickFromTheFourPolicyFixture(KeeperPolicy.NewestModified).Path.Should().Be(@"C:\r\new\new.txt");
    }

    [Fact]
    public void Pick_keeper_first_alphabetical_keeps_the_first_path_in_order()
    {
        PickFromTheFourPolicyFixture(KeeperPolicy.FirstAlphabetical).Path.Should().Be(@"C:\r\abc\alpha.txt");
    }

    [Fact]
    public void Pick_keeper_breaks_a_tie_by_path_so_the_answer_is_deterministic()
    {
        var files = new[]
        {
            Dup(@"C:\r\sub\z.txt"),
            Dup(@"C:\r\sub\a.txt"),
            Dup(@"C:\r\sub\m.txt"),
        };

        var first = DuplicateAnalyzer.PickKeeper(files, KeeperPolicy.ShortestPath);
        var again = DuplicateAnalyzer.PickKeeper(files, KeeperPolicy.ShortestPath);

        first.Path.Should().Be(@"C:\r\sub\a.txt");
        again.Path.Should().Be(first.Path);
    }

    [Fact]
    public void Pick_keeper_breaks_an_identical_timestamp_tie_by_path()
    {
        var files = new[]
        {
            Dup(@"C:\r\x\z.txt", year: 2020),
            Dup(@"C:\r\x\a.txt", year: 2020),
        };

        DuplicateAnalyzer.PickKeeper(files, KeeperPolicy.OldestModified).Path
            .Should().Be(@"C:\r\x\a.txt");
        DuplicateAnalyzer.PickKeeper(files, KeeperPolicy.NewestModified).Path
            .Should().Be(@"C:\r\x\a.txt");
    }

    [Fact]
    public void Pick_keeper_returns_the_only_file_when_there_is_one()
    {
        DuplicateAnalyzer.PickKeeper(new[] { Dup(@"C:\r\only.txt") }, KeeperPolicy.ShortestPath)
            .Path.Should().Be(@"C:\r\only.txt");
    }

    [Fact]
    public void Pick_keeper_rejects_an_empty_group()
    {
        var act = () => DuplicateAnalyzer.PickKeeper(Array.Empty<DuplicateFile>(), KeeperPolicy.ShortestPath);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Analyze_honours_the_keeper_policy_it_is_given()
    {
        var index = ChecksumIndex.FromEntries(new[]
        {
            Entry(HashA, 10, @"C:\r\near.txt", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Entry(HashA, 10, @"C:\r\sub\far.txt", new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        });

        DuplicateAnalyzer.Analyze(index, KeeperPolicy.ShortestPath, true).Groups[0].Keeper.Path
            .Should().Be(@"C:\r\near.txt");
        DuplicateAnalyzer.Analyze(index, KeeperPolicy.OldestModified, true).Groups[0].Keeper.Path
            .Should().Be(@"C:\r\sub\far.txt");
    }

    /// <summary>Four copies rigged so each policy has one unambiguous winner.</summary>
    private static DuplicateFile PickFromTheFourPolicyFixture(KeeperPolicy policy)
    {
        var files = new[]
        {
            new DuplicateFile(@"C:\r\m.txt", 100, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            new DuplicateFile(@"C:\r\old\old.txt", 100, new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            new DuplicateFile(@"C:\r\new\new.txt", 100, new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            new DuplicateFile(@"C:\r\abc\alpha.txt", 100, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        };

        return DuplicateAnalyzer.PickKeeper(files, policy);
    }
}
